using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Idempotent Editor-only migration (Phase 8A Part C) of the hand-authored
/// Levels 1-6 (LevelManager2D.BuildLevels()) into CampaignLevelDefinition2D
/// assets referenced by CampaignLevelCatalog2D
/// (Assets/Resources/CampaignLevelCatalog2D.asset) - the same catalog asset
/// LevelManager2D.ResolveLevels() loads at runtime via Resources.Load. Every
/// value is copied verbatim from BuildLevels() (never re-derived, never
/// re-solved, never re-scrambled) and then field-by-field compared back
/// against the original before anything is saved - any mismatch aborts the
/// whole migration with no assets written. Running this command again after
/// a successful migration re-writes the exact same values to the exact same
/// asset paths (idempotent - never creates a duplicate or a second source of
/// truth). BuildLevels() itself is never modified by this file.
/// </summary>
public static class CampaignMigration2D
{
    private const int MigratedLevelCount = 6;

    [MenuItem("YagmurRotasi2D/Phase 8/Migrate Existing Levels 1-6 To Campaign Catalog")]
    public static bool Migrate()
    {
        List<LevelData2D> source = LevelManager2D.BuildLevels();

        if (source.Count != MigratedLevelCount)
        {
            Debug.LogError($"CampaignMigration2D: BuildLevels() returned {source.Count} level(s), expected exactly {MigratedLevelCount} - " +
                "refusing to migrate. This command only ever migrates the original hand-authored Levels 1-6; if BuildLevels() has " +
                "legitimately changed, update MigratedLevelCount deliberately rather than silently migrating a different count.");
            return false;
        }

        var mismatches = new List<string>();
        var migratedDefinitions = new List<CampaignLevelDefinition2D>();
        var spawnedForValidation = new List<GameObject>();

        try
        {
            for (int i = 0; i < source.Count; i++)
            {
                int levelNumber = i + 1;
                LevelData2D data = source[i];

                CampaignLevelDefinition2D definition = CampaignAssetIO2D.LoadOrCreateDefinitionAsset(levelNumber);
                ApplyLevelData(definition, levelNumber, data);

                ValidateSolvedLayout(definition, levelNumber, mismatches, spawnedForValidation);
                ValidateStartingLayoutUnsolved(definition, levelNumber, mismatches, spawnedForValidation);
                CompareFieldByField(data, definition.ToLevelData(), levelNumber, mismatches);

                migratedDefinitions.Add(definition);
            }
        }
        finally
        {
            foreach (GameObject go in spawnedForValidation)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        if (mismatches.Count > 0)
        {
            Debug.LogError("CampaignMigration2D: Migration REFUSED - " + mismatches.Count + " problem(s) found. " +
                "No asset was saved. Details:\n" + string.Join("\n", mismatches));
            return false;
        }

        for (int i = 0; i < migratedDefinitions.Count; i++)
        {
            EditorUtility.SetDirty(migratedDefinitions[i]);
        }

        CampaignLevelCatalog2D catalog = CampaignAssetIO2D.LoadOrCreateCatalogAsset();
        for (int i = 0; i < migratedDefinitions.Count; i++)
        {
            CampaignAssetIO2D.AssignCatalogSlot(catalog, i + 1, migratedDefinitions[i]);
        }
        EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"CampaignMigration2D: Migration SUCCEEDED - Levels 1-{MigratedLevelCount} verified field-for-field against " +
            $"LevelManager2D.BuildLevels(), every solved layout re-validated with FlowSolver2D, every starting layout confirmed " +
            $"intentionally unsolved, and saved to {CampaignAssetIO2D.CatalogAssetPath}. LevelManager2D.ResolveLevels() will now load these from " +
            "the catalog (BuildLevels() remains as a fallback only, unused while the catalog is present and non-empty).");
        return true;
    }

    private static void ApplyLevelData(CampaignLevelDefinition2D definition, int levelNumber, LevelData2D data)
    {
        definition.stableLevelId = $"level-{levelNumber:000}";
        definition.levelNumber = levelNumber;
        definition.displayName = data.levelName;
        definition.chapterNumber = 1;

        definition.gridWidth = data.gridWidth;
        definition.gridHeight = data.gridHeight;
        definition.sourceCell = data.sourcePos;
        definition.sourceOutputDirection = data.sourceOutputDirection;
        definition.targetCell = data.targetPos;
        definition.targetEntryDirection = data.targetInputDirection;

        // Hand-authored, not generator output - no generator seed/attempt
        // history exists for these, unlike Levels 7+ (Part C explicitly
        // distinguishes migrated content from generated content).
        definition.deterministicSeed = 0;
        definition.generatorVersion = "hand-authored";
        definition.difficultyTier = 0;

        definition.educationalMessage = data.infoText;
        definition.twoStarScore = data.twoStarScore;
        definition.threeStarScore = data.threeStarScore;

        // Copied verbatim, never regenerated - PipeSpawnData2D is a plain
        // class, so each entry is cloned field-by-field rather than aliasing
        // BuildLevels()'s own list instances.
        definition.pipes = new List<PipeSpawnData2D>(data.pipes.Count);
        foreach (PipeSpawnData2D pipe in data.pipes)
        {
            definition.pipes.Add(new PipeSpawnData2D(pipe.pipeType, pipe.gridPos, pipe.startRotationIndex, pipe.solvedRotationIndex, pipe.straightVisualVariant));
        }

        // Generator-only metrics (branch/cycle/difficulty/solution counts)
        // deliberately left at their defaults - these levels were never built
        // by CampaignGraphBuilder2D/CampaignUniquenessSolver2D, so those
        // numbers would be meaningless. minimumRequiredTaps/reachable-tile/
        // flow-wave counts below ARE filled in, since they are cheap, real
        // derived facts about the copied data itself.
        definition.minimumRequiredTaps = CampaignPipeConverter2D.ComputeMinimumTaps(definition.pipes);

        definition.contentHash = CampaignContentHash2D.ComputeHash(definition);
    }

    private static void ValidateSolvedLayout(CampaignLevelDefinition2D definition, int levelNumber, List<string> mismatches, List<GameObject> spawned)
    {
        BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
        FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);

        foreach (PipeSpawnData2D pipe in definition.pipes)
        {
            BranchingSolverTestRunner.CreatePipe(board, spawned, pipe.pipeType, pipe.gridPos, pipe.solvedRotationIndex);
        }

        FlowSolveResult2D result = solver.Solve(definition.sourceCell, definition.sourceOutputDirection, definition.targetCell, definition.targetEntryDirection);

        if (!result.IsSuccess)
        {
            mismatches.Add($"Level {levelNumber}: solved layout (every pipe at solvedRotationIndex) FAILED FlowSolver2D validation " +
                $"(FailureReason={result.FailureReason}) - the shipped solvedRotationIndex data does not actually form a valid route.");
        }
        else
        {
            definition.solvedReachablePipeCount = result.ReachableTiles.Count;
            definition.flowWaveCount = result.FlowSteps.Count;
        }

        foreach (GameObject go in spawned) { if (go != null) Object.DestroyImmediate(go); }
        spawned.Clear();
    }

    private static void ValidateStartingLayoutUnsolved(CampaignLevelDefinition2D definition, int levelNumber, List<string> mismatches, List<GameObject> spawned)
    {
        if (CampaignPipeConverter2D.IsAlreadySolvedState(definition.pipes))
        {
            mismatches.Add($"Level {levelNumber}: starting layout (startRotationIndex for every pipe) is already logically solved - " +
                "the player would win without making a single move.");
            return;
        }

        BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
        FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);

        foreach (PipeSpawnData2D pipe in definition.pipes)
        {
            BranchingSolverTestRunner.CreatePipe(board, spawned, pipe.pipeType, pipe.gridPos, pipe.startRotationIndex);
        }

        FlowSolveResult2D result = solver.Solve(definition.sourceCell, definition.sourceOutputDirection, definition.targetCell, definition.targetEntryDirection);

        if (result.IsSuccess)
        {
            mismatches.Add($"Level {levelNumber}: starting layout (startRotationIndex for every pipe) ALREADY produces a successful " +
                "route through FlowSolver2D - some other combination of rotations completes the puzzle before the player moves anything.");
        }

        foreach (GameObject go in spawned) { if (go != null) Object.DestroyImmediate(go); }
        spawned.Clear();
    }

    private static void CompareFieldByField(LevelData2D expected, LevelData2D actual, int levelNumber, List<string> mismatches)
    {
        void Check(bool condition, string fieldName)
        {
            if (!condition)
            {
                mismatches.Add($"Level {levelNumber}: field '{fieldName}' does not match the original BuildLevels() value.");
            }
        }

        Check(expected.levelName == actual.levelName, "levelName");
        Check(expected.gridWidth == actual.gridWidth, "gridWidth");
        Check(expected.gridHeight == actual.gridHeight, "gridHeight");
        Check(expected.sourcePos == actual.sourcePos, "sourcePos");
        Check(expected.sourceOutputDirection == actual.sourceOutputDirection, "sourceOutputDirection");
        Check(expected.targetPos == actual.targetPos, "targetPos");
        Check(expected.targetInputDirection == actual.targetInputDirection, "targetInputDirection");
        Check(expected.infoText == actual.infoText, "infoText");
        Check(expected.twoStarScore == actual.twoStarScore, "twoStarScore");
        Check(expected.threeStarScore == actual.threeStarScore, "threeStarScore");
        Check(expected.pipes.Count == actual.pipes.Count, "pipes.Count");

        int pipeCount = Mathf.Min(expected.pipes.Count, actual.pipes.Count);
        for (int i = 0; i < pipeCount; i++)
        {
            PipeSpawnData2D e = expected.pipes[i];
            PipeSpawnData2D a = actual.pipes[i];
            Check(e.pipeType == a.pipeType, $"pipes[{i}].pipeType");
            Check(e.gridPos == a.gridPos, $"pipes[{i}].gridPos");
            Check(e.startRotationIndex == a.startRotationIndex, $"pipes[{i}].startRotationIndex");
            Check(e.solvedRotationIndex == a.solvedRotationIndex, $"pipes[{i}].solvedRotationIndex");
            Check(e.straightVisualVariant == a.straightVisualVariant, $"pipes[{i}].straightVisualVariant");
        }
    }

}
