using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Part F (Phase 8A.1): a deterministic 12-point checklist for the 5x5
/// playable area, run across multiple fixed seeds - specifically the actual
/// attempt seeds CampaignGenerateCommand2D's pilot batch would derive for
/// Levels 7-10 (the real 5x5 tier), via the same
/// CampaignSeededRandom2D.DeriveAttemptSeed formula and the same
/// PilotBaseSeed/GeneratorVersion constants, never a re-typed copy. Calls
/// CampaignGraphBuilder2D.TryBuildGraph directly (with its optional
/// backboneCellsOut param) so the raw backbone path can be inspected, not
/// just the final graph. Read-only - never saves anything.
/// </summary>
public static class CampaignSmallGridValidator2D
{
    private static readonly int[] SmallGridLevelNumbers = { 7, 8, 9, 10 };
    private const int AttemptsPerLevel = 5;
    private const int MaxSearchStepsPerPath = 4000;

    [MenuItem("YagmurRotasi2D/Phase 8/Validate 5x5 Playable Area")]
    public static bool ValidatePlayableArea()
    {
        int passCount = 0;
        int failCount = 0;
        var lines = new List<string>();
        var spawned = new List<GameObject>();

        try
        {
            foreach (int levelNumber in SmallGridLevelNumbers)
            {
                CampaignDifficultyProfiles2D.Profile profile = CampaignDifficultyProfiles2D.ForLevel(levelNumber);
                var bounds = new GridBounds2D(profile.GridWidth, profile.GridHeight);

                for (int attempt = 0; attempt < AttemptsPerLevel; attempt++)
                {
                    int seed = CampaignSeededRandom2D.DeriveAttemptSeed(
                        CampaignGenerateCommand2D.PilotBaseSeed, levelNumber, CampaignLevelGenerator2D.GeneratorVersion, attempt);

                    var problems = new List<string>();
                    RunChecklist(bounds, profile, seed, spawned, problems);

                    string label = $"Level {levelNumber} attempt {attempt} (seed={seed})";
                    if (problems.Count == 0)
                    {
                        lines.Add($"PASS: {label}");
                        passCount++;
                    }
                    else
                    {
                        lines.Add($"FAIL: {label} - " + string.Join("; ", problems));
                        failCount++;
                    }
                }
            }
        }
        finally
        {
            foreach (GameObject go in spawned) { if (go != null) Object.DestroyImmediate(go); }
        }

        lines.Add($"5x5 playable area validation: {passCount}/{passCount + failCount} PASSED" + (failCount > 0 ? $", {failCount} FAILED" : "."));
        Debug.Log(string.Join("\n", lines));
        return failCount == 0;
    }

    private static void RunChecklist(GridBounds2D bounds, CampaignDifficultyProfiles2D.Profile profile, int seed, List<GameObject> spawned, List<string> problems)
    {
        var rng = new CampaignSeededRandom2D(seed);
        var backboneCells = new List<Vector2Int>();

        CampaignGraphBuilder2D.ComputeAnchors(bounds, out Vector2Int source, out Vector2Int target,
            out Direction2D sourceOutputDirection, out Direction2D targetEntryDirection,
            out Vector2Int backboneStart, out Vector2Int backboneEnd);

        // 1. Source is top-left.
        if (source != bounds.TopLeft) problems.Add($"Source {source} != bounds.TopLeft {bounds.TopLeft}");
        // 2. Source outputs Right.
        if (sourceOutputDirection != Direction2D.Right) problems.Add($"sourceOutputDirection {sourceOutputDirection} != Right");
        // 3. Target is bottom-right.
        if (target != bounds.BottomRight) problems.Add($"Target {target} != bounds.BottomRight {bounds.BottomRight}");
        // 4. Target accepts from Up.
        if (targetEntryDirection != Direction2D.Up) problems.Add($"targetEntryDirection {targetEntryDirection} != Up");
        // 5. Backbone start is inside bounds.
        if (!bounds.Contains(backboneStart)) problems.Add($"backboneStart {backboneStart} outside bounds");
        // 6. Backbone end is inside bounds.
        if (!bounds.Contains(backboneEnd)) problems.Add($"backboneEnd {backboneEnd} outside bounds");

        CampaignGraphBuilder2D.SolvedGraph graph = CampaignGraphBuilder2D.TryBuildGraph(
            bounds, profile.BranchAttempts, profile.MinActivePipes, profile.PreferredActivePipes, profile.MaxActivePipes,
            rng, MaxSearchStepsPerPath, out string rejectionReason, backboneCells);

        if (graph == null)
        {
            problems.Add($"TryBuildGraph rejected this attempt ({rejectionReason})");
            return;
        }

        // 7. Every path cell is inside bounds.
        foreach (Vector2Int cell in backboneCells)
        {
            if (!bounds.Contains(cell)) problems.Add($"backbone cell {cell} outside bounds");
        }

        // 8. Source and Target are not overwritten (never appear as normal graph cells).
        if (graph.OpenSidesByCell.ContainsKey(source)) problems.Add("Source cell appears as a normal pipe cell in the solved graph");
        if (graph.OpenSidesByCell.ContainsKey(target)) problems.Add("Target cell appears as a normal pipe cell in the solved graph");

        // 9. Consecutive path cells are orthogonally adjacent.
        for (int i = 1; i < backboneCells.Count; i++)
        {
            int manhattan = Mathf.Abs(backboneCells[i].x - backboneCells[i - 1].x) + Mathf.Abs(backboneCells[i].y - backboneCells[i - 1].y);
            if (manhattan != 1) problems.Add($"backbone cells {backboneCells[i - 1]} -> {backboneCells[i]} are not orthogonally adjacent (Manhattan={manhattan})");
        }

        // 10. The path reaches the target-side endpoint (and starts at the source-side endpoint).
        if (backboneCells.Count == 0 || backboneCells[0] != backboneStart) problems.Add("backbone path does not start at backboneStart");
        if (backboneCells.Count == 0 || backboneCells[backboneCells.Count - 1] != backboneEnd) problems.Add("backbone path does not end at backboneEnd");

        // 11. Every converted pipe has supported degree (BuildSolvedPipes throws otherwise).
        var probeInstances = new List<GameObject>();
        List<PipeSpawnData2D> pipes = null;
        try
        {
            pipes = CampaignPipeConverter2D.BuildSolvedPipes(graph.OpenSidesByCell, probeInstances);
        }
        catch (System.Exception ex)
        {
            problems.Add($"BuildSolvedPipes threw ({ex.Message})");
        }
        finally
        {
            foreach (GameObject go in probeInstances) { if (go != null) Object.DestroyImmediate(go); }
        }

        if (pipes == null)
        {
            return;
        }

        // 12. The solved layout succeeds with FlowSolver2D.
        BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
        board.SetGridSize(bounds.Width, bounds.Height);
        FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);
        foreach (PipeSpawnData2D pipe in pipes)
        {
            BranchingSolverTestRunner.CreatePipe(board, spawned, pipe.pipeType, pipe.gridPos, pipe.solvedRotationIndex);
        }
        FlowSolveResult2D result = solver.Solve(source, sourceOutputDirection, target, targetEntryDirection);
        if (!result.IsSuccess)
        {
            problems.Add($"solved layout failed FlowSolver2D (FailureReason={result.FailureReason})");
        }
    }
}
