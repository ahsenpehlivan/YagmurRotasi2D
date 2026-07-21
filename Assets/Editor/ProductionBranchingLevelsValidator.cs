using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Read-only report on the production branching test levels (4-6): data
/// integrity (normalized rotations, Cross fixed at 0, no duplicate
/// coordinates, positions inside the board), pipe-type counts, Tee/Cross
/// counts, solved-route success/leak/wave/reachable info, and the minimum
/// rotation taps between each pipe's startRotationIndex and its explicit,
/// hand-authored solvedRotationIndex (production level metadata - see
/// PipeSpawnData2D.cs). Never derives a solved rotation from spawn-list
/// order or any other inference - only ever reads solvedRotationIndex.
/// Reuses BranchingSolverTestRunner's fixture helpers (same
/// Assembly-CSharp-Editor assembly, internal - no duplicated board/solver
/// construction). Builds everything in-memory, exactly like the solver
/// validation harness - never touches saved progress, the currently open
/// scene, or any scene object.
/// </summary>
public static class ProductionBranchingLevelsValidator
{
    private const int FirstBranchingLevelNumber = 4;
    private const int LastBranchingLevelNumber = 6;

    [MenuItem("YagmurRotasi2D/Validate Production Branching Levels 4-6")]
    public static void ValidateMenuCommand()
    {
        ValidateLevels456(true);
    }

    public static bool ValidateLevels456(bool logDetails)
    {
        List<LevelData2D> levels = LevelManager2D.ResolveLevels();

        if (levels.Count < FirstBranchingLevelNumber)
        {
            Debug.LogWarning($"ProductionBranchingLevelsValidator: expected at least {FirstBranchingLevelNumber} production levels, found {levels.Count} - Levels 4-6 are missing.");
            return false;
        }

        // Fixed at 4-6 regardless of how many campaign levels now exist (as of
        // Phase 8A the catalog can grow well past 6) - this command is
        // specifically scoped to the original Tee/Cross introduction levels,
        // not "every level from 4 onward".
        int lastIndex = Mathf.Min(LastBranchingLevelNumber, levels.Count) - 1;

        bool allOk = true;
        for (int levelIndex = FirstBranchingLevelNumber - 1; levelIndex <= lastIndex; levelIndex++)
        {
            bool levelOk = ValidateOneLevel(levelIndex + 1, levels[levelIndex], logDetails);
            allOk &= levelOk;
        }

        Debug.Log(allOk
            ? "ProductionBranchingLevelsValidator: all branching production levels validated successfully."
            : "ProductionBranchingLevelsValidator: one or more branching production levels FAILED validation - see log above.");

        return allOk;
    }

    private static bool ValidateOneLevel(int levelNumber, LevelData2D level, bool logDetails)
    {
        var spawned = new List<GameObject>();
        try
        {
            BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
            FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);

            if (!ValidatePipeDataIntegrity(levelNumber, level, board))
            {
                return false;
            }

            int straightCount = 0, cornerCount = 0, teeCount = 0, crossCount = 0;
            int totalTapsNeeded = 0;
            var perTileLog = new StringBuilder();

            foreach (PipeSpawnData2D spawn in level.pipes)
            {
                switch (spawn.pipeType)
                {
                    case PipeType2D.Straight: straightCount++; break;
                    case PipeType2D.Corner: cornerCount++; break;
                    case PipeType2D.Tee: teeCount++; break;
                    case PipeType2D.Cross: crossCount++; break;
                }

                int taps = MinimumTaps(spawn.pipeType, spawn.startRotationIndex, spawn.solvedRotationIndex);
                totalTapsNeeded += taps;
                perTileLog.AppendLine($"    {spawn.gridPos} {spawn.pipeType}: start={spawn.startRotationIndex}, solved={spawn.solvedRotationIndex}, minTaps={taps}");

                BranchingSolverTestRunner.CreatePipe(board, spawned, spawn.pipeType, spawn.gridPos, spawn.solvedRotationIndex);
            }

            FlowSolveResult2D result = solver.Solve(
                level.sourcePos, level.sourceOutputDirection,
                level.targetPos, level.targetInputDirection);

            if (logDetails)
            {
                Debug.Log($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName}\n" +
                    $"  Pipe counts: Straight={straightCount}, Corner={cornerCount}, Tee={teeCount}, Cross={crossCount} (total {level.pipes.Count})\n" +
                    $"{perTileLog}" +
                    $"  Solved route success: {result.IsSuccess} | TargetReached: {result.TargetReached} | HasLeak: {result.HasLeak}\n" +
                    $"  FlowWave count: {result.FlowSteps.Count} | ReachableTiles: {result.ReachableTiles.Count}\n" +
                    $"  Minimum rotation taps, summed across all pipes: {totalTapsNeeded}\n" +
                    $"  FailureReason: {result.FailureReason}");
            }

            if (!result.IsSuccess)
            {
                Debug.LogError($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName} FAILED\n" +
                    BranchingSolverTestRunner.DescribeLevelSolvedDiagnostics(board, level, result));
                return false;
            }

            if (teeCount == 0 && crossCount == 0)
            {
                Debug.LogWarning($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName} has no Tee or Cross pipes - is this really a branching test level?");
            }

            return true;
        }
        finally
        {
            foreach (GameObject go in spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }

    /// <summary>solved rotation valid for its type, Cross fixed at 0, both indices normalized 0-3, no duplicate grid coordinates, every position (including Source/Target) inside the board.</summary>
    private static bool ValidatePipeDataIntegrity(int levelNumber, LevelData2D level, BoardManager2D board)
    {
        bool ok = true;
        var seenPositions = new HashSet<Vector2Int>();

        foreach (PipeSpawnData2D spawn in level.pipes)
        {
            if (spawn.startRotationIndex < 0 || spawn.startRotationIndex > 3)
            {
                Debug.LogError($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName}, tile {spawn.gridPos} ({spawn.pipeType}): " +
                    $"startRotationIndex={spawn.startRotationIndex} is not normalized to 0-3.");
                ok = false;
            }

            if (spawn.solvedRotationIndex < 0 || spawn.solvedRotationIndex > 3)
            {
                Debug.LogError($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName}, tile {spawn.gridPos} ({spawn.pipeType}): " +
                    $"solvedRotationIndex={spawn.solvedRotationIndex} is not normalized to 0-3.");
                ok = false;
            }

            if (spawn.pipeType == PipeType2D.Cross && (spawn.startRotationIndex != 0 || spawn.solvedRotationIndex != 0))
            {
                Debug.LogError($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName}, tile {spawn.gridPos}: " +
                    $"Cross must have start=0 and solved=0 (non-rotatable), found start={spawn.startRotationIndex}, solved={spawn.solvedRotationIndex}.");
                ok = false;
            }

            if (!seenPositions.Add(spawn.gridPos))
            {
                Debug.LogError($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName}: duplicate grid coordinate {spawn.gridPos}.");
                ok = false;
            }

            if (!board.IsInsideGrid(spawn.gridPos))
            {
                Debug.LogError($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName}, tile {spawn.gridPos} ({spawn.pipeType}): position is outside the board.");
                ok = false;
            }
        }

        if (!board.IsInsideGrid(level.sourcePos))
        {
            Debug.LogError($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName}: sourcePos {level.sourcePos} is outside the board.");
            ok = false;
        }

        if (!board.IsInsideGrid(level.targetPos))
        {
            Debug.LogError($"ProductionBranchingLevelsValidator: Level {levelNumber} - {level.levelName}: targetPos {level.targetPos} is outside the board.");
            ok = false;
        }

        return ok;
    }

    /// <summary>
    /// Minimum forward taps (PipeTile2D.RotatePipe() only ever advances +1
    /// mod 4) from startRotationIndex to a rotation that opens the same
    /// ports as solvedRotationIndex. Straight has only 2 distinct logical
    /// orientations - rotationIndex and (rotationIndex + 2) % 4 both open
    /// the identical Left/Right or Up/Down pair - so reaching EITHER counts
    /// as solved; Corner and Tee have 4 logically distinct rotations, so no
    /// such shortcut applies; Cross never rotates and always contributes 0.
    /// </summary>
    private static int MinimumTaps(PipeType2D type, int startRotation, int solvedRotation)
    {
        if (type == PipeType2D.Cross)
        {
            return 0;
        }

        if (type == PipeType2D.Straight)
        {
            int equivalentSolved = (solvedRotation + 2) % 4;
            int tapsToSolved = ((solvedRotation - startRotation) % 4 + 4) % 4;
            int tapsToEquivalent = ((equivalentSolved - startRotation) % 4 + 4) % 4;
            return Mathf.Min(tapsToSolved, tapsToEquivalent);
        }

        return ((solvedRotation - startRotation) % 4 + 4) % 4;
    }
}
