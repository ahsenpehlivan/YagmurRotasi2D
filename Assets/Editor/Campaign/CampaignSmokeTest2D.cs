using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Core2D;

/// <summary>
/// Fast, isolated per-level generation checks (Phase 8A.1 Part G / 8A.2 Part
/// H), using the EXACT same request configuration
/// CampaignGenerateCommand2D.GeneratePilotLevels() will use (reuses its
/// internal BuildRequest, never a second copy). Never touches the saved
/// catalog or any asset on disk - the generated CampaignLevelDefinition2D is
/// an in-memory-only ScriptableObject instance, destroyed at the end
/// regardless of outcome. Intended to be run BEFORE the full pilot batch, so
/// a geometry/algorithm regression is caught in seconds instead of after
/// burning the full 14-level attempt budget.
/// </summary>
public static class CampaignSmokeTest2D
{
    [MenuItem("YagmurRotasi2D/Phase 8/Smoke Test Level 7 Generation")]
    public static bool SmokeTestLevel7()
    {
        return RunSmokeTest(levelNumber: 7, expectedGridWidth: 5, expectedGridHeight: 5, minExpectedPipes: 0, requireTee: false);
    }

    /// <summary>Phase 8A.2 Part H / 8A.3 Part P - Level 10 additionally must clear its own density floor (>=12 active pipes), have a real Tee, and a real branch/cycle, as part of the smoke test itself, not just whatever the shared pipeline already checks.</summary>
    [MenuItem("YagmurRotasi2D/Phase 8/Smoke Test Level 10 Generation")]
    public static bool SmokeTestLevel10()
    {
        return RunSmokeTest(levelNumber: 10, expectedGridWidth: 5, expectedGridHeight: 5, minExpectedPipes: 12, requireTee: true);
    }

    private static bool RunSmokeTest(int levelNumber, int expectedGridWidth, int expectedGridHeight, int minExpectedPipes, bool requireTee)
    {
        var request = CampaignGenerateCommand2D.BuildRequest(levelNumber);
        CampaignLevelGenerator2D.GenerationResult result = CampaignLevelGenerator2D.Generate(request);

        if (!result.Success)
        {
            Debug.LogError($"CampaignSmokeTest2D: Level {levelNumber} generation FAILED after {result.AttemptsUsed} attempt(s) - " +
                $"{result.LastRejectionReason}. Nothing was saved (this command never writes to disk in the first place).");
            return false;
        }

        CampaignLevelDefinition2D def = result.Definition;
        var problems = new List<string>();

        if (def.gridWidth != expectedGridWidth || def.gridHeight != expectedGridHeight)
        {
            problems.Add($"expected a {expectedGridWidth}x{expectedGridHeight} grid, got {def.gridWidth}x{def.gridHeight}");
        }
        if (minExpectedPipes > 0 && def.pipes.Count < minExpectedPipes)
        {
            problems.Add($"expected at least {minExpectedPipes} active pipes, got {def.pipes.Count}");
        }
        if (CampaignPipeConverter2D.IsAlreadySolvedState(def.pipes))
        {
            problems.Add("starting layout is already logically solved");
        }
        if (result.Metrics == null || result.Metrics.SolutionCount != 1)
        {
            problems.Add($"expected exactly 1 solution, got {(result.Metrics == null ? "no metrics" : result.Metrics.SolutionCount.ToString())}");
        }
        if (string.IsNullOrEmpty(def.contentHash))
        {
            problems.Add("contentHash is empty");
        }
        if (def.branchCount < 1)
        {
            problems.Add($"expected a meaningful branch/cycle (Part L), got branchCount={def.branchCount}");
        }
        if (requireTee)
        {
            int teeCount = 0;
            foreach (var pipe in def.pipes)
            {
                if (pipe.pipeType == PipeType2D.Tee) teeCount++;
            }
            if (teeCount < 1)
            {
                problems.Add("expected at least one Tee junction (Part L/P), found none");
            }
        }
        if (result.UniquenessDiagnostics != null && result.UniquenessDiagnostics.TerminationReason != null &&
            result.UniquenessDiagnostics.TerminationReason.Contains("budget exceeded"))
        {
            problems.Add("uniqueness result was reported as accepted, but its own diagnostics say the search budget was exceeded (should be unreachable - Generate() only accepts UniquenessOutcome.One)");
        }

        bool ok = problems.Count == 0;

        string uniquenessStats = result.UniquenessDiagnostics == null
            ? "no uniqueness diagnostics captured"
            : $"nodesVisited={result.UniquenessDiagnostics.NodesVisited}, propagations={result.UniquenessDiagnostics.PropagationOperations}, " +
              $"backtracks={result.UniquenessDiagnostics.Backtracks}, maxDepth={result.UniquenessDiagnostics.MaxRecursionDepth}, " +
              $"elapsed={result.UniquenessDiagnostics.ElapsedMilliseconds}ms, memoHits={result.UniquenessDiagnostics.MemoCacheHits}, " +
              $"termination=\"{result.UniquenessDiagnostics.TerminationReason}\"";

        Debug.Log($"CampaignSmokeTest2D: Level {levelNumber} smoke test {(ok ? "PASSED" : "FAILED")}\n" +
            $"Grid={def.gridWidth}x{def.gridHeight}, Seed={result.AcceptedSeed}, Attempts={result.AttemptsUsed}, " +
            $"Pipes={def.pipes.Count}, MinimumTaps={def.minimumRequiredTaps}, SolutionCount={def.solutionCount}, " +
            $"BranchCount={def.branchCount}, CycleCount={def.cycleCount}, DifficultyScore={def.difficultyScore:0.##}, " +
            $"Hash={def.contentHash}\n" +
            $"Uniqueness search: {uniquenessStats}" +
            (ok ? "" : "\nProblems:\n- " + string.Join("\n- ", problems)) +
            "\nThis candidate was never saved to disk or the catalog - discarding it now.");

        Object.DestroyImmediate(def);
        return ok;
    }
}
