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
    /// <summary>
    /// Bumped 8A.1 -> 8A.2 for the density-aware graph expansion rewrite
    /// (ear insertion, the branch/cycle zero-new-cell fix, edge detours,
    /// challenge-level Tee/Cross quality checks), then 8A.2 -> 8A.3 for the
    /// uniqueness solver rewrite (canonical domains, AC-3, MRV, connectivity
    /// pruning, known-solution-first alternative search) plus the Part M
    /// pipeline reorder (scramble/tap-target check now runs before
    /// uniqueness, not after). Both are genuine content-affecting changes,
    /// not just bugfixes that happen to preserve output: the uniqueness
    /// rewrite can ACCEPT candidates (like Level 10's 13-15 pipe case) that
    /// 8A.2 could only report Inconclusive on and reject - so the actual set
    /// of seeds that produce an accepted candidate can differ between
    /// versions, not just the candidate's internal details. The version
    /// string is part of CampaignContentHash2D's input specifically so this
    /// kind of change is never silently invisible in a stored hash.
    /// </summary>
    public const string GeneratorVersion = "8A.3";

    public sealed class GenerationRequest
    {
        public int LevelNumber;
        public string DisplayName;
        public string EducationalMessage;
        public int BaseCampaignSeed;
        public int MaxAttempts;
        public int MaxSearchStepsPerPath = 4000;

        /// <summary>
        /// Phase 8A.3 Part L: a DFS node-visit budget for the exact
        /// uniqueness search (replaces the old, much coarser "complete
        /// assignments checked" counter - the CSP rewrite makes a node
        /// budget the meaningful unit). 200,000 nodes comfortably covers a
        /// 13-15 pipe 5x5 candidate after canonical domains/AC-3/MRV/
        /// connectivity pruning - see the benchmark command for actual
        /// measured costs before tuning this further.
        /// </summary>
        public int MaxUniquenessSearchNodes = 200000;

        /// <summary>Phase 8A.3 Part L: wall-clock ceiling per candidate's uniqueness search, independent of the node budget (a slow-but-shallow search could exhaust time before nodes, or vice versa). A timeout is always treated as Inconclusive (rejected), never as proof of uniqueness.</summary>
        public int MaxUniquenessElapsedMilliseconds = 2000;

        /// <summary>
        /// Phase 8 Fast-Track: when false, CampaignUniquenessSolver2D is never
        /// invoked for this request - cheaper structural gates (no leak, full
        /// Source reachability, in-bounds, no duplicate coordinates, unsolved
        /// start state, active-pipe-count and minimum-tap-count within the
        /// difficulty profile) are the acceptance gate instead. Defaults to
        /// true so every existing caller (the original pilot batch, smoke
        /// tests) is completely unaffected by this field's existence.
        /// </summary>
        public bool RequireExactUniqueness = true;

        /// <summary>
        /// Phase 8 Fast-Track: overrides the stamped generatorVersion and the
        /// seed-derivation version string for this request. Null (default)
        /// uses the static GeneratorVersion constant, matching every existing
        /// caller exactly. Fast-Track requests set this to a distinct string
        /// because skipping the uniqueness gate is itself a content-affecting
        /// pipeline change (same reasoning as the GeneratorVersion doc
        /// comment above) - it must never be silently indistinguishable from
        /// an exact-uniqueness-checked "8A.3" asset in a stored hash/seed.
        /// </summary>
        public string GeneratorVersionOverride;
    }

    public sealed class GenerationResult
    {
        public bool Success;
        public CampaignLevelDefinition2D Definition;
        public int AttemptsUsed;
        public string LastRejectionReason;
        public int AcceptedSeed;
        public CampaignMetrics2D.LevelMetrics Metrics;

        /// <summary>Phase 8A.3 Part P: the uniqueness search diagnostics for the ACCEPTED attempt (null if generation failed, or if no attempt ever reached the uniqueness stage) - lets callers like the smoke tests report exact uniqueness stats, not just the pass/fail outcome.</summary>
        public CampaignUniquenessSolver2D.UniquenessSearchDiagnostics UniquenessDiagnostics;
    }

    public static GenerationResult Generate(GenerationRequest request)
    {
        CampaignDifficultyProfiles2D.Profile profile = CampaignDifficultyProfiles2D.ForLevel(request.LevelNumber);
        var bounds = new GridBounds2D(profile.GridWidth, profile.GridHeight);

        // Part B (Phase 8A.1): a pure, non-stochastic pre-flight check. If
        // the anchor geometry is invalid it is invalid for EVERY seed and
        // EVERY attempt identically - fail once here instead of burning the
        // entire attempt budget repeating the same deterministic failure.
        if (!CampaignGraphBuilder2D.ValidateProfileGeometry(bounds, out string geometryReason))
        {
            UnityEngine.Debug.LogError(CampaignGraphBuilder2D.DescribeAnchorDiagnostics(request.LevelNumber, bounds, 0) +
                $"\nProfile=Tier{profile.DifficultyTier} ({profile.GridWidth}x{profile.GridHeight})");
            return new GenerationResult
            {
                Success = false,
                AttemptsUsed = 0,
                LastRejectionReason = $"Campaign profile invalid before generation attempts. {geometryReason}"
            };
        }

        // Part G (Phase 8A.2): pure geometric feasibility - catches only a
        // density target that is mathematically impossible on this board
        // size, never a generator-implementation limitation (those are
        // legitimate per-attempt rejections and still go through the retry
        // loop below).
        if (!CampaignGraphBuilder2D.ValidateProfileFeasibility(bounds, profile.MinActivePipes, profile.MaxActivePipes, out string feasibilityReason))
        {
            UnityEngine.Debug.LogError($"Level {request.LevelNumber}: profile infeasible - {feasibilityReason}");
            return new GenerationResult
            {
                Success = false,
                AttemptsUsed = 0,
                LastRejectionReason = $"Campaign profile invalid before generation attempts. {feasibilityReason}"
            };
        }

        var stopwatch = Stopwatch.StartNew();
        string lastReason = "no attempts made";
        string effectiveVersion = request.GeneratorVersionOverride ?? GeneratorVersion;

        // Part A/I (Phase 8A.2): aggregated across every failed attempt, so
        // a final failure logs ONE summary line instead of up to
        // MaxAttempts near-identical ones.
        var aggregate = new AggregatedGenerationDiagnostics();

        for (int attempt = 0; attempt < request.MaxAttempts; attempt++)
        {
            int seed = CampaignSeededRandom2D.DeriveAttemptSeed(request.BaseCampaignSeed, request.LevelNumber, effectiveVersion, attempt);
            var rng = new CampaignSeededRandom2D(seed);

            CampaignLevelDefinition2D candidate = TryGenerateOnce(request, profile, seed, rng, attempt + 1, out lastReason, out CampaignGraphBuilder2D.GraphBuildDiagnostics diagnostics, out CampaignUniquenessSolver2D.UniquenessSearchDiagnostics uniquenessDiag);
            aggregate.Accumulate(diagnostics);

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
                    Metrics = metrics,
                    UniquenessDiagnostics = uniquenessDiag
                };
            }
        }

        stopwatch.Stop();
        UnityEngine.Debug.LogError($"Level {request.LevelNumber} generation diagnostics (Phase 8A.2 Part A):\n" + aggregate.Describe(request.MaxAttempts));
        return new GenerationResult
        {
            Success = false,
            AttemptsUsed = request.MaxAttempts,
            LastRejectionReason = lastReason
        };
    }

    /// <summary>Part A/I: sums GraphBuildDiagnostics across every attempt in one Generate() call and tracks the min/max/avg final active-pipe count actually reached, so a batch failure can be diagnosed from a single log line instead of scrolling through hundreds.</summary>
    private sealed class AggregatedGenerationDiagnostics
    {
        private int attemptsWithData;
        private int sumFinalActivePipes;
        private int minFinalActivePipes = int.MaxValue;
        private int maxFinalActivePipes = int.MinValue;
        private int sumBackboneCells;
        private int totalEarAttempts, totalEarSuccesses;
        private int totalDetourAttempts, totalDetourSuccesses;
        private int totalRejectedEndpointDegreeLimit, totalRejectedNoCandidatePairs, totalRejectedNoValidUnusedPath, totalRejectedZeroNewCellPath, totalRejectedWouldExceedMax;

        public void Accumulate(CampaignGraphBuilder2D.GraphBuildDiagnostics d)
        {
            if (d == null) return;
            attemptsWithData++;
            sumFinalActivePipes += d.FinalActivePipeCount;
            minFinalActivePipes = Mathf.Min(minFinalActivePipes, d.FinalActivePipeCount);
            maxFinalActivePipes = Mathf.Max(maxFinalActivePipes, d.FinalActivePipeCount);
            sumBackboneCells += d.BackboneCellCount;
            totalEarAttempts += d.EarAttempts;
            totalEarSuccesses += d.EarSuccesses;
            totalDetourAttempts += d.DetourAttempts;
            totalDetourSuccesses += d.DetourSuccesses;
            totalRejectedEndpointDegreeLimit += d.RejectedEndpointDegreeLimit;
            totalRejectedNoCandidatePairs += d.RejectedNoCandidatePairs;
            totalRejectedNoValidUnusedPath += d.RejectedNoValidUnusedPath;
            totalRejectedZeroNewCellPath += d.RejectedZeroNewCellPath;
            totalRejectedWouldExceedMax += d.RejectedWouldExceedMax;
        }

        public string Describe(int totalAttempts)
        {
            if (attemptsWithData == 0)
            {
                return "- no attempts reached graph construction (rejected before backbone search - see the geometry/feasibility pre-flight message above).";
            }

            float avg = sumFinalActivePipes / (float)attemptsWithData;
            float avgBackbone = sumBackboneCells / (float)attemptsWithData;

            return $"- attempts: {totalAttempts}\n" +
                $"- active pipes reached: min={minFinalActivePipes}, max={maxFinalActivePipes}, avg={avg:0.#}\n" +
                $"- average backbone cell count: {avgBackbone:0.#}\n" +
                $"- ear insertions: {totalEarSuccesses} succeeded / {totalEarAttempts} tried\n" +
                $"- detour insertions: {totalDetourSuccesses} succeeded / {totalDetourAttempts} tried\n" +
                "- ear/detour rejections:\n" +
                $"  - endpoint degree limit: {totalRejectedEndpointDegreeLimit}\n" +
                $"  - no candidate pairs (degree saturated): {totalRejectedNoCandidatePairs}\n" +
                $"  - no valid unused path found: {totalRejectedNoValidUnusedPath}\n" +
                $"  - zero-new-cell path rejected: {totalRejectedZeroNewCellPath}\n" +
                $"  - would exceed max active pipes: {totalRejectedWouldExceedMax}";
        }
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
        CampaignDifficultyProfiles2D.Profile profile = CampaignDifficultyProfiles2D.ForLevel(request.LevelNumber);
        var bounds = new GridBounds2D(profile.GridWidth, profile.GridHeight);

        if (!CampaignGraphBuilder2D.ValidateProfileGeometry(bounds, out string geometryReason))
        {
            UnityEngine.Debug.LogError(CampaignGraphBuilder2D.DescribeAnchorDiagnostics(request.LevelNumber, bounds, 0) +
                $"\nProfile=Tier{profile.DifficultyTier} ({profile.GridWidth}x{profile.GridHeight})");
            return new GenerationResult
            {
                Success = false,
                AttemptsUsed = 0,
                LastRejectionReason = $"Campaign profile invalid before generation attempts. {geometryReason}"
            };
        }

        if (!CampaignGraphBuilder2D.ValidateProfileFeasibility(bounds, profile.MinActivePipes, profile.MaxActivePipes, out string feasibilityReason))
        {
            UnityEngine.Debug.LogError($"Level {request.LevelNumber}: profile infeasible - {feasibilityReason}");
            return new GenerationResult
            {
                Success = false,
                AttemptsUsed = 0,
                LastRejectionReason = $"Campaign profile invalid before generation attempts. {feasibilityReason}"
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var rng = new CampaignSeededRandom2D(exactSeed);

        CampaignLevelDefinition2D candidate = TryGenerateOnce(request, profile, exactSeed, rng, 1, out string rejectionReason, out _, out CampaignUniquenessSolver2D.UniquenessSearchDiagnostics uniquenessDiag);
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
            Metrics = metrics,
            UniquenessDiagnostics = uniquenessDiag
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

    /// <summary>Internal (not private) so CampaignUniquenessBenchmark2D (Phase 8A.3 Part O) can reuse this EXACT real pipeline up to (and including) the uniqueness stage, instead of a second, potentially-drifting copy.</summary>
    internal static CampaignLevelDefinition2D TryGenerateOnce(
        GenerationRequest request, CampaignDifficultyProfiles2D.Profile profile, int seed, CampaignSeededRandom2D rng, int attemptNumber,
        out string rejectionReason, out CampaignGraphBuilder2D.GraphBuildDiagnostics diagnostics,
        out CampaignUniquenessSolver2D.UniquenessSearchDiagnostics uniquenessDiagnostics)
    {
        var bounds = new GridBounds2D(profile.GridWidth, profile.GridHeight);
        diagnostics = new CampaignGraphBuilder2D.GraphBuildDiagnostics();
        uniquenessDiagnostics = null;

        CampaignGraphBuilder2D.SolvedGraph graph = CampaignGraphBuilder2D.TryBuildGraph(
            bounds, profile.BranchAttempts, profile.MinActivePipes, profile.PreferredActivePipes, profile.MaxActivePipes,
            rng, request.MaxSearchStepsPerPath, out rejectionReason, backboneCellsOut: null, diagnosticsOut: diagnostics);

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

        // Part L (Phase 8A.2): a challenge level must be a genuine network,
        // not density satisfied by one long trivial snake - require at
        // least one real Tee junction, and cap Cross density on small
        // boards (a 5x5 board especially has little room to route AROUND a
        // fully-open 4-way junction, so more than one quickly forces
        // near-snake topology around it anyway).
        if (profile.IsChallengeLevel)
        {
            if (graph.BranchCount + graph.DetourCount < 1)
            {
                rejectionReason = "challenge level has no reconnecting branch/cycle/detour - density was reached by backbone length alone (Part L difficulty-quality requirement)";
                return null;
            }

            int teeCount = 0;
            int crossCount = 0;
            foreach (PipeSpawnData2D pipe in pipes)
            {
                if (pipe.pipeType == PipeType2D.Tee) teeCount++;
                else if (pipe.pipeType == PipeType2D.Cross) crossCount++;
            }

            if (teeCount < 1)
            {
                rejectionReason = "challenge level lacks a Tee junction (Part L difficulty-quality requirement)";
                return null;
            }

            int maxCrossForBoard = Mathf.Max(1, (profile.GridWidth * profile.GridHeight) / 25);
            if (crossCount > maxCrossForBoard)
            {
                rejectionReason = $"challenge level has {crossCount} Cross junction(s), exceeding the {maxCrossForBoard} allowed on a {profile.GridWidth}x{profile.GridHeight} board (Part L - avoid excessive Cross density)";
                return null;
            }
        }

        // Validate the solved layout with the real, unchanged FlowSolver2D -
        // this is the authoritative safety net: no candidate is ever accepted
        // without genuinely passing the same solver production gameplay uses.
        var spawned = new List<GameObject>();
        FlowSolveResult2D solvedResult;
        List<PipeTile2D> solvedOrderPipes;
        try
        {
            // BUG HISTORY (Phase 8A.3.1): CreateBoard() alone leaves the
            // board at its default 5x5 - without this, every candidate for
            // a non-5x5 profile (the entire 6x6-10x10 range, including
            // Levels 11-20 of THIS pilot batch) would have pipes outside
            // that default range rejected by BoardManager2D.IsInsideGrid,
            // and BranchingSolverTestRunner.CreatePipe's own Require()
            // check would throw instead of this candidate being cleanly
            // rejected and retried. Found while investigating an unrelated
            // test-fixture bounds bug that turned out to share the exact
            // same root cause.
            BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
            board.SetGridSize(bounds.Width, bounds.Height);
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

        // Part M (Phase 8A.3): every CHEAP gate runs before the expensive
        // uniqueness search - scramble generation and the tap-target check
        // used to run AFTER uniqueness, wasting the whole uniqueness search
        // budget on candidates that were always going to be rejected for an
        // unrelated, nearly-free reason. Nothing about the scramble depends
        // on uniqueness (it only reads solved/start rotations), so moving it
        // earlier changes nothing about correctness or determinism within
        // this call - rng isn't reused after TryGenerateOnce returns.
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

        int solutionsFound = CampaignLevelDefinition2D.SolutionCountNotChecked;

        if (request.RequireExactUniqueness)
        {
            // Re-order pipes to match the real BFS-from-source discovery
            // order - used only as CampaignUniquenessSolver2D's initial MRV
            // tie-break seed order, not a correctness requirement (the
            // solver reorders dynamically via its own MRV selection during
            // search). Only needed when the solver actually runs.
            var pipesInBfsOrder = new List<PipeSpawnData2D>(pipes.Count);
            var byPosition = new Dictionary<Vector2Int, PipeSpawnData2D>();
            foreach (PipeSpawnData2D p in pipes) byPosition[p.gridPos] = p;
            foreach (PipeTile2D tile in solvedOrderPipes) pipesInBfsOrder.Add(byPosition[tile.GridPosition]);

            uniquenessDiagnostics = new CampaignUniquenessSolver2D.UniquenessSearchDiagnostics();
            CampaignUniquenessSolver2D.UniquenessOutcome outcome = CampaignUniquenessSolver2D.CountSolutions(
                bounds, graph.Source, graph.SourceOutputDirection, graph.Target, graph.TargetEntryDirection,
                pipesInBfsOrder, request.MaxUniquenessSearchNodes, out solutionsFound,
                request.MaxUniquenessElapsedMilliseconds, uniquenessDiagnostics);

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
        }
        else
        {
            // Phase 8 Fast-Track acceptance: the expensive exact uniqueness
            // solver is never invoked here. "Solved layout reaches Target",
            // "has no leak" and "every production pipe is Source-reachable"
            // were already established above via the real, unchanged
            // FlowSolver2D (solvedResult.IsSuccess/HasLeak/ReachableTiles) -
            // the checks below restate the leak/bounds/duplicate guarantees
            // explicitly as a hard gate (defense-in-depth on top of what the
            // graph builder already guarantees by construction) and add the
            // one check that ISN'T already implied by an earlier gate: final
            // active pipe count against the difficulty profile. Never treats
            // a disconnected filler pipe as valid - every check here is a
            // hard rejection, not a warning.
            if (solvedResult.HasLeak)
            {
                rejectionReason = $"solved layout has a leak (FailureReason={solvedResult.FailureReason})";
                return null;
            }

            var seenPositions = new HashSet<Vector2Int>();
            foreach (PipeSpawnData2D pipe in pipes)
            {
                if (!bounds.Contains(pipe.gridPos))
                {
                    rejectionReason = $"pipe at {pipe.gridPos} is outside the {bounds.Width}x{bounds.Height} board bounds";
                    return null;
                }
                if (!seenPositions.Add(pipe.gridPos))
                {
                    rejectionReason = $"duplicate pipe coordinate {pipe.gridPos}";
                    return null;
                }
            }

            if (pipes.Count < profile.MinActivePipes || pipes.Count > profile.MaxActivePipes)
            {
                rejectionReason = $"active pipe count {pipes.Count} outside profile range [{profile.MinActivePipes},{profile.MaxActivePipes}]";
                return null;
            }
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
        definition.generatorVersion = request.GeneratorVersionOverride ?? GeneratorVersion;
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
