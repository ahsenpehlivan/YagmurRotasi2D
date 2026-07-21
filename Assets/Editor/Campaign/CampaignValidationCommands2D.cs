using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Batch validation commands (Phase 8A Part T/U) over the entire saved
/// campaign catalog (Assets/Resources/CampaignLevelCatalog2D.asset) - always
/// re-derives every check from the asset's CURRENT field values using the
/// real, unchanged production classes (FlowSolver2D, CampaignPipeConverter2D,
/// CampaignUniquenessSolver2D, CampaignContentHash2D), never trusting a
/// previously-stored value at face value. Read-only: never modifies or saves
/// any asset.
/// </summary>
public static class CampaignValidationCommands2D
{
    [MenuItem("YagmurRotasi2D/Phase 8/Validate All Campaign Levels")]
    public static bool ValidateAllCampaignLevels()
    {
        bool ok = RunStructuralValidation(out List<string> lines);
        Debug.Log(string.Join("\n", lines));
        return ok;
    }

    [MenuItem("YagmurRotasi2D/Phase 8/Validate Unique Solutions")]
    public static bool ValidateUniqueSolutions()
    {
        bool ok = RunUniquenessValidation(out List<string> lines);
        Debug.Log(string.Join("\n", lines));
        return ok;
    }

    [MenuItem("YagmurRotasi2D/Phase 8/Export Campaign Validation Report")]
    public static void ExportCampaignValidationReport()
    {
        bool structuralOk = RunStructuralValidation(out List<string> structuralLines);
        bool uniquenessOk = RunUniquenessValidation(out List<string> uniquenessLines);

        var report = new List<string>
        {
            $"YagmurRotasi2D Campaign Validation Report - generated {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            $"Overall: {(structuralOk && uniquenessOk ? "PASS" : "FAIL")}",
            "",
            "=== Structural Validation ===",
        };
        report.AddRange(structuralLines);
        report.Add("");
        report.Add("=== Unique Solution Validation ===");
        report.AddRange(uniquenessLines);

        string path = Path.Combine(Application.dataPath, "..", "CampaignValidationReport.txt");
        File.WriteAllLines(path, report);

        Debug.Log($"CampaignValidationCommands2D: Report written to {Path.GetFullPath(path)} " +
            $"(Overall: {(structuralOk && uniquenessOk ? "PASS" : "FAIL")}). This file is outside Assets/ and is not imported by Unity.");
    }

    // ---------------- Structural validation ----------------

    private static bool RunStructuralValidation(out List<string> lines)
    {
        lines = new List<string>();
        CampaignLevelCatalog2D catalog = AssetDatabase.LoadAssetAtPath<CampaignLevelCatalog2D>(CampaignAssetIO2D.CatalogAssetPath);

        if (catalog == null || catalog.levels == null || catalog.levels.Count == 0)
        {
            lines.Add("FAIL: No catalog found (or catalog is empty) at " + CampaignAssetIO2D.CatalogAssetPath +
                " - run the migration/generation commands first.");
            return false;
        }

        int passCount = 0;
        int failCount = 0;
        var seenLevelNumbers = new HashSet<int>();
        var spawned = new List<GameObject>();

        try
        {
            for (int i = 0; i < catalog.levels.Count; i++)
            {
                CampaignLevelDefinition2D def = catalog.levels[i];
                int expectedLevelNumber = i + 1;

                if (def == null)
                {
                    lines.Add($"FAIL: catalog slot {expectedLevelNumber} is empty (null entry).");
                    failCount++;
                    continue;
                }

                var problems = new List<string>();
                ValidateOneLevel(def, expectedLevelNumber, seenLevelNumbers, problems, spawned);

                if (problems.Count == 0)
                {
                    lines.Add($"PASS: Level {def.levelNumber} ('{def.displayName}', {def.gridWidth}x{def.gridHeight}, " +
                        $"{def.pipes.Count} pipes, minTaps={def.minimumRequiredTaps}).");
                    passCount++;
                }
                else
                {
                    lines.Add($"FAIL: Level {expectedLevelNumber} ('{def.displayName}') - " + string.Join("; ", problems));
                    failCount++;
                }
            }
        }
        finally
        {
            foreach (GameObject go in spawned) { if (go != null) UnityEngine.Object.DestroyImmediate(go); }
        }

        lines.Add($"Structural validation: {passCount}/{catalog.levels.Count} PASSED" + (failCount > 0 ? $", {failCount} FAILED" : "."));
        return failCount == 0;
    }

    private static void ValidateOneLevel(CampaignLevelDefinition2D def, int expectedLevelNumber, HashSet<int> seenLevelNumbers, List<string> problems, List<GameObject> spawned)
    {
        if (def.levelNumber != expectedLevelNumber)
        {
            problems.Add($"levelNumber {def.levelNumber} does not match catalog slot {expectedLevelNumber}");
        }
        if (!seenLevelNumbers.Add(def.levelNumber))
        {
            problems.Add($"duplicate levelNumber {def.levelNumber}");
        }
        if (string.IsNullOrEmpty(def.stableLevelId))
        {
            problems.Add("stableLevelId is empty");
        }
        if (def.gridWidth < 1 || def.gridWidth > 10 || def.gridHeight < 1 || def.gridHeight > 10)
        {
            problems.Add($"grid size {def.gridWidth}x{def.gridHeight} outside supported range 1-10");
        }
        if (def.pipes == null || def.pipes.Count == 0)
        {
            problems.Add("pipes list is null or empty");
            return;
        }
        if (def.twoStarScore <= 0 || def.threeStarScore <= 0 || def.twoStarScore >= def.threeStarScore)
        {
            problems.Add($"score thresholds invalid (twoStarScore={def.twoStarScore}, threeStarScore={def.threeStarScore})");
        }

        var bounds = new GridBounds2D(def.gridWidth, def.gridHeight);
        var seenPositions = new HashSet<Vector2Int>();
        foreach (PipeSpawnData2D pipe in def.pipes)
        {
            if (!bounds.Contains(pipe.gridPos))
            {
                problems.Add($"pipe at {pipe.gridPos} is outside the {def.gridWidth}x{def.gridHeight} grid");
            }
            if (pipe.gridPos == def.sourceCell || pipe.gridPos == def.targetCell)
            {
                problems.Add($"pipe at {pipe.gridPos} overlaps Source/Target cell");
            }
            if (!seenPositions.Add(pipe.gridPos))
            {
                problems.Add($"duplicate pipe position {pipe.gridPos}");
            }
        }

        int recomputedMinTaps = CampaignPipeConverter2D.ComputeMinimumTaps(def.pipes);
        if (recomputedMinTaps != def.minimumRequiredTaps)
        {
            problems.Add($"stored minimumRequiredTaps={def.minimumRequiredTaps} does not match recomputed value {recomputedMinTaps} (asset drifted from its last save)");
        }

        string recomputedHash = CampaignContentHash2D.ComputeHash(def);
        if (recomputedHash != def.contentHash)
        {
            problems.Add("stored contentHash does not match the asset's current field values (edited without re-saving through a generator/migration command)");
        }

        if (def.generatorVersion != "hand-authored")
        {
            CampaignDifficultyProfiles2D.Profile profile = CampaignDifficultyProfiles2D.ForLevel(def.levelNumber);
            if (profile.GridWidth != def.gridWidth || profile.GridHeight != def.gridHeight)
            {
                problems.Add($"grid size {def.gridWidth}x{def.gridHeight} does not match the difficulty plan's expected " +
                    $"{profile.GridWidth}x{profile.GridHeight} for level {def.levelNumber}");
            }
        }

        if (CampaignPipeConverter2D.IsAlreadySolvedState(def.pipes))
        {
            problems.Add("starting layout is already logically solved");
            return;
        }

        BoardManager2D solvedBoard = BranchingSolverTestRunner.CreateBoard(spawned);
        solvedBoard.SetGridSize(def.gridWidth, def.gridHeight);
        FlowSolver2D solvedSolver = BranchingSolverTestRunner.CreateSolver(solvedBoard, spawned);
        foreach (PipeSpawnData2D pipe in def.pipes)
        {
            BranchingSolverTestRunner.CreatePipe(solvedBoard, spawned, pipe.pipeType, pipe.gridPos, pipe.solvedRotationIndex);
        }
        FlowSolveResult2D solvedResult = solvedSolver.Solve(def.sourceCell, def.sourceOutputDirection, def.targetCell, def.targetEntryDirection);

        if (!solvedResult.IsSuccess)
        {
            problems.Add($"solved layout FAILS FlowSolver2D (FailureReason={solvedResult.FailureReason})");
        }
        else if (solvedResult.ReachableTiles.Count != def.pipes.Count)
        {
            problems.Add($"solved layout leaves {def.pipes.Count - solvedResult.ReachableTiles.Count} pipe(s) unreached");
        }

        BoardManager2D startBoard = BranchingSolverTestRunner.CreateBoard(spawned);
        startBoard.SetGridSize(def.gridWidth, def.gridHeight);
        FlowSolver2D startSolver = BranchingSolverTestRunner.CreateSolver(startBoard, spawned);
        foreach (PipeSpawnData2D pipe in def.pipes)
        {
            BranchingSolverTestRunner.CreatePipe(startBoard, spawned, pipe.pipeType, pipe.gridPos, pipe.startRotationIndex);
        }
        FlowSolveResult2D startResult = startSolver.Solve(def.sourceCell, def.sourceOutputDirection, def.targetCell, def.targetEntryDirection);

        if (startResult.IsSuccess)
        {
            problems.Add("starting layout already produces a successful route (player would win without moving anything)");
        }
    }

    // ---------------- Uniqueness validation ----------------

    private const int MaxUniquenessAssignmentsPerLevel = 300000;

    private static bool RunUniquenessValidation(out List<string> lines)
    {
        lines = new List<string>();
        CampaignLevelCatalog2D catalog = AssetDatabase.LoadAssetAtPath<CampaignLevelCatalog2D>(CampaignAssetIO2D.CatalogAssetPath);

        if (catalog == null || catalog.levels == null || catalog.levels.Count == 0)
        {
            lines.Add("FAIL: No catalog found (or catalog is empty) at " + CampaignAssetIO2D.CatalogAssetPath + ".");
            return false;
        }

        int passCount = 0;
        int failCount = 0;

        foreach (CampaignLevelDefinition2D def in catalog.levels)
        {
            if (def == null) continue;

            List<PipeSpawnData2D> pipesInBfsOrder = ComputeBfsOrderOrNull(def, out string bfsFailure);
            if (pipesInBfsOrder == null)
            {
                lines.Add($"FAIL: Level {def.levelNumber} - could not establish solved BFS order ({bfsFailure}), cannot check uniqueness.");
                failCount++;
                continue;
            }

            CampaignUniquenessSolver2D.UniquenessOutcome outcome = CampaignUniquenessSolver2D.CountSolutions(
                def.sourceCell, def.sourceOutputDirection, def.targetCell, def.targetEntryDirection,
                pipesInBfsOrder, MaxUniquenessAssignmentsPerLevel, out int solutionsFound);

            if (outcome == CampaignUniquenessSolver2D.UniquenessOutcome.One)
            {
                lines.Add($"PASS: Level {def.levelNumber} - exactly one solution confirmed.");
                passCount++;
            }
            else
            {
                string reason;
                if (outcome == CampaignUniquenessSolver2D.UniquenessOutcome.Zero)
                {
                    reason = "0 solutions found (should be impossible for a level that passed structural validation)";
                }
                else if (outcome == CampaignUniquenessSolver2D.UniquenessOutcome.TwoOrMore)
                {
                    reason = $"{solutionsFound}+ solutions found - not unique";
                }
                else
                {
                    reason = $"inconclusive (search budget of {MaxUniquenessAssignmentsPerLevel} assignments exceeded)";
                }
                lines.Add($"FAIL: Level {def.levelNumber} - {reason}.");
                failCount++;
            }
        }

        lines.Add($"Uniqueness validation: {passCount}/{passCount + failCount} PASSED" + (failCount > 0 ? $", {failCount} FAILED" : "."));
        return failCount == 0;
    }

    /// <summary>Solves the level at solvedRotationIndex to obtain the real BFS-from-source discovery order (same ordering the generator itself uses for uniqueness search efficiency) - returns null if the solved layout itself does not succeed.</summary>
    private static List<PipeSpawnData2D> ComputeBfsOrderOrNull(CampaignLevelDefinition2D def, out string failureReason)
    {
        var spawned = new List<GameObject>();
        try
        {
            BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
            board.SetGridSize(def.gridWidth, def.gridHeight);
            FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);

            foreach (PipeSpawnData2D pipe in def.pipes)
            {
                BranchingSolverTestRunner.CreatePipe(board, spawned, pipe.pipeType, pipe.gridPos, pipe.solvedRotationIndex);
            }

            FlowSolveResult2D result = solver.Solve(def.sourceCell, def.sourceOutputDirection, def.targetCell, def.targetEntryDirection);
            if (!result.IsSuccess)
            {
                failureReason = $"solved layout failed (FailureReason={result.FailureReason})";
                return null;
            }

            var byPosition = new Dictionary<Vector2Int, PipeSpawnData2D>();
            foreach (PipeSpawnData2D pipe in def.pipes) byPosition[pipe.gridPos] = pipe;

            var ordered = new List<PipeSpawnData2D>(def.pipes.Count);
            foreach (IReadOnlyList<PipeFlowStep2D> wave in result.FlowSteps)
            {
                foreach (PipeFlowStep2D step in wave)
                {
                    ordered.Add(byPosition[step.Pipe.GridPosition]);
                }
            }

            failureReason = null;
            return ordered;
        }
        finally
        {
            foreach (GameObject go in spawned) { if (go != null) UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
