using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Core2D;

/// <summary>
/// Phase 8A.3 Part O: measures the optimized uniqueness solver's real cost on
/// a genuine Level 10 candidate - generates in memory only (reuses
/// CampaignLevelGenerator2D.TryGenerateOnce directly, the exact same
/// pipeline the real pilot batch uses, never a second copy), stops at the
/// first attempt that reaches the uniqueness stage regardless of outcome,
/// reports every diagnostic Part A/O asked for, and saves nothing.
/// </summary>
public static class CampaignUniquenessBenchmark2D
{
    private const int BenchmarkLevelNumber = 10;

    [MenuItem("YagmurRotasi2D/Phase 8/Benchmark Level 10 Uniqueness Solver")]
    public static bool BenchmarkLevel10()
    {
        var request = CampaignGenerateCommand2D.BuildRequest(BenchmarkLevelNumber);
        CampaignDifficultyProfiles2D.Profile profile = CampaignDifficultyProfiles2D.ForLevel(BenchmarkLevelNumber);

        CampaignLevelDefinition2D candidate = null;
        CampaignUniquenessSolver2D.UniquenessSearchDiagnostics uniquenessDiag = null;
        string lastReason = "no attempts made";
        int attemptsUsed = 0;

        for (int attempt = 0; attempt < request.MaxAttempts; attempt++)
        {
            int seed = CampaignSeededRandom2D.DeriveAttemptSeed(request.BaseCampaignSeed, BenchmarkLevelNumber, CampaignLevelGenerator2D.GeneratorVersion, attempt);
            var rng = new CampaignSeededRandom2D(seed);

            candidate = CampaignLevelGenerator2D.TryGenerateOnce(
                request, profile, seed, rng, attempt + 1, out lastReason,
                out CampaignGraphBuilder2D.GraphBuildDiagnostics _, out uniquenessDiag);

            attemptsUsed = attempt + 1;

            // Stop at the first attempt that reached the uniqueness stage at
            // all (uniquenessDiag gets populated only once the pipeline gets
            // that far) - regardless of whether uniqueness itself passed,
            // since the benchmark's job is to measure the solver, not to
            // find an accepted level.
            if (uniquenessDiag != null)
            {
                break;
            }
        }

        if (uniquenessDiag == null)
        {
            Debug.LogError($"CampaignUniquenessBenchmark2D: no attempt reached the uniqueness stage within {request.MaxAttempts} attempts - " +
                $"every attempt was rejected earlier in the pipeline (last reason: {lastReason}). Cannot benchmark the uniqueness solver itself from this.");
            return false;
        }

        int straightCount = 0, cornerCount = 0, teeCount = 0, crossCount = 0;
        int pipeCount = candidate != null ? candidate.pipes.Count : 0;
        if (candidate != null)
        {
            foreach (var pipe in candidate.pipes)
            {
                switch (pipe.pipeType)
                {
                    case PipeType2D.Straight: straightCount++; break;
                    case PipeType2D.Corner: cornerCount++; break;
                    case PipeType2D.Tee: teeCount++; break;
                    case PipeType2D.Cross: crossCount++; break;
                }
            }
        }

        Debug.Log($"CampaignUniquenessBenchmark2D: Level {BenchmarkLevelNumber} benchmark (attempt {attemptsUsed}/{request.MaxAttempts}, candidate {(candidate != null ? "ACCEPTED so far" : "rejected at/after uniqueness")}):\n" +
            $"Pipes={pipeCount} (Straight={straightCount}, Corner={cornerCount}, Tee={teeCount}, Cross={crossCount})\n" +
            $"Initial domain size sum={uniquenessDiag.InitialDomainSizeSum}, forced before search={uniquenessDiag.VariablesForcedBeforeSearch}\n" +
            $"Pruning removed: boardEdge={uniquenessDiag.RemovedByBoardEdge}, emptyNeighbor={uniquenessDiag.RemovedByEmptyNeighbor}, " +
            $"sourceConnection={uniquenessDiag.RemovedBySourceConnection}, targetConnection={uniquenessDiag.RemovedByTargetConnection}\n" +
            $"Nodes visited={uniquenessDiag.NodesVisited}, propagations={uniquenessDiag.PropagationOperations}, backtracks={uniquenessDiag.Backtracks}, " +
            $"maxDepth={uniquenessDiag.MaxRecursionDepth}, memoHits={uniquenessDiag.MemoCacheHits}\n" +
            $"Elapsed={uniquenessDiag.ElapsedMilliseconds}ms, nodeBudget={uniquenessDiag.NodeBudget}, secondSolutionNode={uniquenessDiag.SecondSolutionNode}\n" +
            $"Termination reason: {uniquenessDiag.TerminationReason}\n" +
            (lastReason != null ? $"Pipeline rejection reason (if any, after uniqueness): {lastReason}\n" : "") +
            "Nothing was saved to disk or the catalog by this command.");

        if (candidate != null)
        {
            Object.DestroyImmediate(candidate);
        }

        return true;
    }
}
