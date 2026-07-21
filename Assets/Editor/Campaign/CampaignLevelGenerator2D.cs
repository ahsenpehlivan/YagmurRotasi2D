using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Orchestrates one campaign level's generation (Part H-S): builds a solved
/// graph (CampaignGraphBuilder2D), converts it to real pipe data
/// (CampaignPipeConverter2D), validates the solved layout with the real,
/// unchanged FlowSolver2D, checks solution uniqueness
/// (CampaignUniquenessSolver2D), generates a deterministic starting scramble,
/// and computes metrics/thresholds/hash - retrying with deterministic
/// per-attempt seeds (CampaignSeededRandom2D.DeriveAttemptSeed) up to a
/// configurable attempt cap. Never saves an invalid candidate (Part S) -
/// returns a clear failure with the last rejection reason instead. Runs only
/// in the Editor (Assembly-CSharp-Editor) - this entire file is never part of
/// a build.
/// </summary>
public static class CampaignLevelGenerator2D
{
    public const string GeneratorVersion = "8A.1";

    public sealed class GenerationRequest
    {
        public int LevelNumber;
        public string DisplayName;
        public string EducationalMessage;
        public int BaseCampaignSeed;
        public int MaxAttempts;
        public int MaxSearchStepsPerPath = 4000;
        public int MaxUniquenessAssignments = 300000;
    }

    public sealed class GenerationResult
    {
        public bool Success;
        public CampaignLevelDefinition2D Definition;
        public int AttemptsUsed;
        public string LastRejectionReason;
        public int AcceptedSeed;
        public CampaignMetrics2D.LevelMetrics Metrics;
    }

    public static GenerationResult Generate(GenerationRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        CampaignDifficultyProfiles2D.Profile profile = CampaignDifficultyProfiles2D.ForLevel(request.LevelNumber);
        string lastReason = "no attempts made";

        for (int attempt = 0; attempt < request.MaxAttempts; attempt++)
        {
            int seed = CampaignSeededRandom2D.DeriveAttemptSeed(request.BaseCampaignSeed, request.LevelNumber, GeneratorVersion, attempt);
            var rng = new CampaignSeededRandom2D(seed);

            CampaignLevelDefinition2D candidate = TryGenerateOnce(request, profile, seed, rng, attempt + 1, out lastReason);
            if (candidate != null)
            {
                stopwatch.Stop();
                CampaignMetrics2D.LevelMetrics metrics = FinalizeCandidate(candidate, seed, attempt + 1, (float)stopwatch.Elapsed.TotalSeconds);

                return new GenerationResult
                {
                    Success = true,
                    Definition = candidate,
                    AttemptsUsed = attempt + 1,
                    AcceptedSeed = seed,
                    Metrics = metrics
                };
            }
        }

        stopwatch.Stop();
        return new GenerationResult
        {
            Success = false,
            AttemptsUsed = request.MaxAttempts,
            LastRejectionReason = lastReason
        };
    }

    /// <summary>
    /// Regenerates a level directly from an already-known accepted seed (as
    /// stored on a CampaignLevelDefinition2D asset's deterministicSeed field) -
    /// no attempt-derivation/retry loop, since the exact seed that produced
    /// the original candidate is already known. Used by "Regenerate Selected
    /// Level From Stored Seed" to reproduce byte-identical output for
    /// verification, or to re-run generation after a code change using the
    /// same seed. Returns a failed GenerationResult (never throws) if that
    /// exact seed no longer produces a valid candidate - e.g. after a
    /// deliberate generator algorithm change - which is itself useful
    /// information (content drift), not treated as a crash.
    /// </summary>
    public static GenerationResult GenerateFromExactSeed(GenerationRequest request, int exactSeed)
    {
        var stopwatch = Stopwatch.StartNew();
        CampaignDifficultyProfiles2D.Profile profile = CampaignDifficultyProfiles2D.ForLevel(request.LevelNumber);
        var rng = new CampaignSeededRandom2D(exactSeed);

        CampaignLevelDefinition2D candidate = TryGenerateOnce(request, profile, exactSeed, rng, 1, out string rejectionReason);
        stopwatch.Stop();

        if (candidate == null)
        {
            return new GenerationResult
            {
                Success = false,
                AttemptsUsed = 1,
                LastRejectionReason = rejectionReason
            };
        }

        CampaignMetrics2D.LevelMetrics metrics = FinalizeCandidate(candidate, exactSeed, 1, (float)stopwatch.Elapsed.TotalSeconds);

        return new GenerationResult
        {
            Success = true,
            Definition = candidate,
            AttemptsUsed = 1,
            AcceptedSeed = exactSeed,
            Metrics = metrics
        };
    }

    /// <summary>Shared finalization for an accepted candidate (both Generate() and GenerateFromExactSeed()): builds the reportable metrics object, computes the difficulty score, and stamps the content hash - always in this exact order so contentHash is computed only after every other field is already final.</summary>
    private static CampaignMetrics2D.LevelMetrics FinalizeCandidate(CampaignLevelDefinition2D candidate, int seed, int attemptsUsed, float generationTimeSeconds)
    {
        var metrics = new CampaignMetrics2D.LevelMetrics
        {
            GridWidth = candidate.gridWidth,
            GridHeight = candidate.gridHeight,
            ActivePipeCount = candidate.pipes.Count,
            BranchCount = candidate.branchCount,
            CycleCount = candidate.cycleCount,
            MinimumRequiredTaps = candidate.minimumRequiredTaps,
            FlowWaveCount = candidate.flowWaveCount,
            ReachableTileCount = candidate.solvedReachablePipeCount,
            SolutionCount = candidate.solutionCount,
            GenerationAttempts = attemptsUsed,
            GenerationTimeSeconds = generationTimeSeconds,
            Seed = seed
        };
        CountPipeTypes(candidate.pipes, metrics);
        metrics.DifficultyScore = CampaignMetrics2D.ComputeDifficultyScore(metrics);
        candidate.difficultyScore = metrics.DifficultyScore;
        candidate.contentHash = CampaignContentHash2D.ComputeHash(candidate);
        return metrics;
    }

    private static void CountPipeTypes(List<PipeSpawnData2D> pipes, CampaignMetrics2D.LevelMetrics metrics)
    {
        foreach (PipeSpawnData2D pipe in pipes)
        {
            switch (pipe.pipeType)
            {
                case PipeType2D.Straight: metrics.StraightCount++; break;
                case PipeType2D.Corner: metrics.CornerCount++; break;
                case PipeType2D.Tee: metrics.TeeCount++; break;
                case PipeType2D.Cross: metrics.CrossCount++; break;
            }
        }
    }

    private static CampaignLevelDefinition2D TryGenerateOnce(
        GenerationRequest request, CampaignDifficultyProfiles2D.Profile profile, int seed, CampaignSeededRandom2D rng, int attemptNumber, out string rejectionReason)
    {
        var bounds = new GridBounds2D(profile.GridWidth, profile.GridHeight);

        CampaignGraphBuilder2D.SolvedGraph graph = CampaignGraphBuilder2D.TryBuildGraph(
            bounds, profile.BranchAttempts, profile.MinActivePipes, profile.MaxActivePipes,
            rng, request.MaxSearchStepsPerPath, out rejectionReason);

        if (graph == null)
        {
            return null;
        }

        var probeInstances = new List<GameObject>();
        List<PipeSpawnData2D> pipes;
        try
        {
            pipes = CampaignPipeConverter2D.BuildSolvedPipes(graph.OpenSidesByCell, probeInstances);
        }
        finally
        {
            foreach (GameObject go in probeInstances)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        // Deterministic pipe ordering for reproducibility (Part V) - by
        // position, independent of Dictionary enumeration order.
        pipes.Sort((a, b) => a.gridPos.x != b.gridPos.x ? a.gridPos.x.CompareTo(b.gridPos.x) : a.gridPos.y.CompareTo(b.gridPos.y));

        // Validate the solved layout with the real, unchanged FlowSolver2D -
        // this is the authoritative safety net: no candidate is ever accepted
        // without genuinely passing the same solver production gameplay uses.
        var spawned = new List<GameObject>();
        FlowSolveResult2D solvedResult;
        List<PipeTile2D> solvedOrderPipes;
        try
        {
            BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
            FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);

            foreach (PipeSpawnData2D spawn in pipes)
            {
                BranchingSolverTestRunner.CreatePipe(board, spawned, spawn.pipeType, spawn.gridPos, spawn.solvedRotationIndex);
            }

            solvedResult = solver.Solve(graph.Source, graph.SourceOutputDirection, graph.Target, graph.TargetEntryDirection);

            solvedOrderPipes = new List<PipeTile2D>();
            foreach (IReadOnlyList<PipeFlowStep2D> wave in solvedResult.FlowSteps)
            {
                foreach (PipeFlowStep2D step in wave)
                {
                    solvedOrderPipes.Add(step.Pipe);
                }
            }
        }
        finally
        {
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        if (!solvedResult.IsSuccess)
        {
            rejectionReason = $"solved layout failed FlowSolver2D validation (FailureReason={solvedResult.FailureReason})";
            return null;
        }

        if (solvedResult.ReachableTiles.Count != pipes.Count)
        {
            rejectionReason = $"solved layout leaves {pipes.Count - solvedResult.ReachableTiles.Count} placed pipe(s) unreached by Source (Part H requires every placed pipe to belong to the solved network)";
            return null;
        }

        // Re-order pipes to match the real BFS-from-source discovery order for
        // the uniqueness solver's variable ordering (a real MRV heuristic was
        // not attempted - documented simplification, see CampaignUniquenessSolver2D).
        var pipesInBfsOrder = new List<PipeSpawnData2D>(pipes.Count);
        var byPosition = new Dictionary<Vector2Int, PipeSpawnData2D>();
        foreach (PipeSpawnData2D p in pipes) byPosition[p.gridPos] = p;
        foreach (PipeTile2D tile in solvedOrderPipes) pipesInBfsOrder.Add(byPosition[tile.GridPosition]);

        CampaignUniquenessSolver2D.UniquenessOutcome outcome = CampaignUniquenessSolver2D.CountSolutions(
            graph.Source, graph.SourceOutputDirection, graph.Target, graph.TargetEntryDirection,
            pipesInBfsOrder, request.MaxUniquenessAssignments, out int solutionsFound);

        if (outcome != CampaignUniquenessSolver2D.UniquenessOutcome.One)
        {
            if (outcome == CampaignUniquenessSolver2D.UniquenessOutcome.Zero)
            {
                rejectionReason = "uniqueness solver found 0 solutions (should be impossible - solved layout already validated)";
            }
            else if (outcome == CampaignUniquenessSolver2D.UniquenessOutcome.TwoOrMore)
            {
                rejectionReason = $"solution not unique ({solutionsFound}+ valid rotation assignments found)";
            }
            else
            {
                rejectionReason = "uniqueness check inconclusive (search budget exceeded)";
            }
            return null;
        }

        // Deterministic starting scramble (Part L).
        CampaignPipeConverter2D.GenerateScramble(pipes, profile.MinMinimumTaps, profile.MaxMinimumTaps, rng);
        int minimumTaps = CampaignPipeConverter2D.ComputeMinimumTaps(pipes);

        if (CampaignPipeConverter2D.IsAlreadySolvedState(pipes))
        {
            rejectionReason = "scramble accidentally left the level already solved";
            return null;
        }

        if (minimumTaps < profile.MinMinimumTaps || minimumTaps > profile.MaxMinimumTaps)
        {
            rejectionReason = $"minimum taps {minimumTaps} outside target range [{profile.MinMinimumTaps},{profile.MaxMinimumTaps}]";
            return null;
        }

        (int twoStarScore, int threeStarScore) = CampaignMetrics2D.ComputeScoreThresholds(minimumTaps, pipes.Count);

        var definition = ScriptableObject.CreateInstance<CampaignLevelDefinition2D>();
        definition.stableLevelId = $"level-{request.LevelNumber:000}";
        definition.levelNumber = request.LevelNumber;
        definition.displayName = request.DisplayName;
        definition.chapterNumber = profile.ChapterNumber;
        definition.gridWidth = profile.GridWidth;
        definition.gridHeight = profile.GridHeight;
        definition.sourceCell = graph.Source;
        definition.sourceOutputDirection = graph.SourceOutputDirection;
        definition.targetCell = graph.Target;
        definition.targetEntryDirection = graph.TargetEntryDirection;
        definition.deterministicSeed = seed;
        definition.generatorVersion = GeneratorVersion;
        definition.difficultyTier = profile.DifficultyTier;
        definition.educationalMessage = request.EducationalMessage;
        definition.twoStarScore = twoStarScore;
        definition.threeStarScore = threeStarScore;
        definition.minimumRequiredTaps = minimumTaps;
        definition.solvedReachablePipeCount = solvedResult.ReachableTiles.Count;
        definition.flowWaveCount = solvedResult.FlowSteps.Count;
        definition.solutionCount = solutionsFound;
        definition.branchCount = graph.BranchCount;
        definition.cycleCount = graph.CycleCount;
        definition.pipes = pipes;

        rejectionReason = null;
        return definition;
    }
}
