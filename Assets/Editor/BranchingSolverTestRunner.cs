using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Deterministic Assembly-CSharp-Editor validation harness for the branching
/// (Tee/Cross) flow solver. Replaces an earlier attempt that used a separate
/// asmdef-based NUnit EditMode test assembly
/// (Assets/Tests/EditMode/YagmurRotasi2D.Tests.EditMode.asmdef) - that
/// assembly could not resolve PipeTile2D/FlowSolver2D/BoardManager2D/etc.
/// because a plain asmdef cannot reference the predefined Assembly-CSharp
/// without InternalsVisibleTo plumbing this project deliberately avoids. This
/// file is a normal Editor script (compiles into Assembly-CSharp-Editor,
/// which can see Assembly-CSharp's internal members via the
/// [assembly: InternalsVisibleTo("Assembly-CSharp-Editor")] declared in
/// LevelManager2D.cs)
/// and calls the real production classes directly - no NUnit, no
/// UnityEditor.TestTools.TestRunner.Api, no test asmdef.
///
/// Every case builds its own isolated in-memory board, never reads the
/// currently open scene, never touches saved progress, and destroys every
/// spawned GameObject in a finally block via RunFixture.
/// </summary>
public static class BranchingSolverTestRunner
{
    internal sealed class ValidationCase
    {
        public string Name;
        public Action Execute;
    }

    /// <summary>Runs all 16 cases and logs one [PASS/FAIL] line per case plus a final summary. Returns true only if every case passed - callers (e.g. the Phase 7F master install command) must not report success when this returns false.</summary>
    [MenuItem("YagmurRotasi2D/Run Phase 7F Branching Solver Tests")]
    public static bool RunTests()
    {
        List<ValidationCase> cases = BuildCases();
        int passCount = 0;
        int failCount = 0;

        for (int i = 0; i < cases.Count; i++)
        {
            ValidationCase testCase = cases[i];
            string label = $"{i + 1:00}/{cases.Count:00}";
            try
            {
                testCase.Execute();
                passCount++;
                Debug.Log($"[PASS {label}] {testCase.Name}");
            }
            catch (Exception ex)
            {
                failCount++;
                // Full exception.ToString() - type, message and complete stack
                // trace - not just ex.Message, so a fixture bug can be
                // pinpointed to an exact file/line from this log alone.
                Debug.LogError($"[FAIL {label}] {testCase.Name}\n{ex}");
            }
        }

        string summary = failCount == 0
            ? $"Phase 7F Branching Solver Validation: {passCount}/{cases.Count} PASSED"
            : $"Phase 7F Branching Solver Validation: {passCount}/{cases.Count} PASSED, {failCount} FAILED";

        if (failCount == 0)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }

        return failCount == 0;
    }

    private static void Require(bool condition, string failureMessage)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }

    private static List<ValidationCase> BuildCases()
    {
        return new List<ValidationCase>
        {
            new ValidationCase { Name = "Existing Straight/Corner valid route succeeds", Execute = Case01_StraightCornerValidRoute },
            new ValidationCase { Name = "Broken reciprocal Straight/Corner route fails", Execute = Case02_StraightCornerBrokenReciprocal },
            new ValidationCase { Name = "All production levels remain solvable", Execute = Case03_AllProductionLevelsRemainSolvable },
            new ValidationCase { Name = "Valid Tee network succeeds", Execute = Case04_TeeValidNetwork },
            new ValidationCase { Name = "Tee dangling branch fails", Execute = Case05_TeeDanglingBranch },
            new ValidationCase { Name = "Tee reciprocal mismatch fails", Execute = Case06_TeeReciprocalMismatch },
            new ValidationCase { Name = "Tee rotation mapping works for all four indices", Execute = Case07_TeeRotationMapping },
            new ValidationCase { Name = "Valid Cross network succeeds", Execute = Case08_CrossValidNetwork },
            new ValidationCase { Name = "Cross dangling side fails", Execute = Case09_CrossDanglingSide },
            new ValidationCase { Name = "Cross tap does not rotate", Execute = Case10_CrossTapDoesNotRotate },
            new ValidationCase { Name = "Cross tap does not increase move count", Execute = Case11_CrossTapDoesNotIncrementMoves },
            new ValidationCase { Name = "Valid cycle with Target reachable succeeds", Execute = Case12_ValidCycleSucceeds },
            new ValidationCase { Name = "Cycle traversal terminates", Execute = Case13_CycleTraversalTerminates },
            new ValidationCase { Name = "A tile reached through multiple branches is processed once", Execute = Case14_TileVisitedOnce },
            new ValidationCase { Name = "A disconnected pipe outside the Source network does not fail", Execute = Case15_DisconnectedPipeIgnored },
            new ValidationCase { Name = "A reachable open port outside the board fails", Execute = Case16_OpenPortOutsideBoard },
        };
    }

    // ---------------- Fixture helpers ----------------

    private static void RunFixture(Action<List<GameObject>> body)
    {
        var spawned = new List<GameObject>();
        try
        {
            body(spawned);
        }
        finally
        {
            foreach (GameObject go in spawned)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }
    }

    /// <summary>
    /// Edit Mode-created MonoBehaviours must not rely on Unity automatically
    /// invoking Awake() at a predictable time, so this explicitly calls
    /// BoardManager2D.InitializeGrid() (the same allocation Awake() performs
    /// during normal gameplay) instead of assuming it already ran.
    /// </summary>
    internal static BoardManager2D CreateBoard(List<GameObject> spawned)
    {
        var go = new GameObject("ValidationBoard");
        spawned.Add(go);
        var board = go.AddComponent<BoardManager2D>();
        Require(board != null, "Fixture BoardManager2D is null immediately after AddComponent.");

        board.InitializeGrid();

        return board;
    }

    /// <summary>Wires the solver's private boardManager field via SerializedObject (the same pattern every other Editor tool in this project already uses) - no reflection needed. Verifies the serialized property exists and that the assignment actually took effect, rather than assuming it silently worked.</summary>
    internal static FlowSolver2D CreateSolver(BoardManager2D board, List<GameObject> spawned)
    {
        Require(board != null, "CreateSolver: board argument is null - CreateBoard must be called first.");

        var go = new GameObject("ValidationSolver");
        spawned.Add(go);
        var solver = go.AddComponent<FlowSolver2D>();
        Require(solver != null, "Fixture FlowSolver2D is null immediately after AddComponent.");

        var so = new SerializedObject(solver);
        SerializedProperty boardProperty = so.FindProperty("boardManager");
        Require(boardProperty != null, "FlowSolver2D serialized field 'boardManager' was not found via SerializedObject - field name mismatch?");

        boardProperty.objectReferenceValue = board;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Re-read (fresh SerializedObject, not the same property instance) to
        // confirm the write actually reached the live component instead of
        // silently no-oping.
        var verify = new SerializedObject(solver);
        SerializedProperty verifyProperty = verify.FindProperty("boardManager");
        Require(verifyProperty != null && verifyProperty.objectReferenceValue == board,
            "FlowSolver2D.boardManager was not successfully assigned after ApplyModifiedPropertiesWithoutUndo() - " +
            $"expected '{board.name}', got '{(verifyProperty != null ? verifyProperty.objectReferenceValue?.name ?? "null" : "property missing")}'.");

        return solver;
    }

    internal static PipeTile2D CreatePipe(BoardManager2D board, List<GameObject> spawned, PipeType2D type, Vector2Int pos, int rotationIndex)
    {
        Require(board != null, "CreatePipe: board argument is null - CreateBoard must be called first.");

        var go = new GameObject($"ValidationPipe_{type}_{pos.x}_{pos.y}");
        spawned.Add(go);
        var pipe = go.AddComponent<PipeTile2D>();
        Require(pipe != null, "Fixture PipeTile2D is null immediately after AddComponent.");

        pipe.Initialize(type, rotationIndex, pos);
        Require(pipe.GridPosition == pos, $"PipeTile2D.GridPosition mismatch after Initialize: expected {pos}, got {pipe.GridPosition}.");

        board.SetPipe(pos, pipe);
        Require(board.GetPipe(pos) == pipe,
            $"BoardManager2D.SetPipe/GetPipe round-trip failed at {pos} (IsInsideGrid={board.IsInsideGrid(pos)}) - " +
            "the board's internal grid may not have been initialized before this call.");

        return pipe;
    }

    internal static string DescribeTile(PipeTile2D tile)
    {
        return tile != null ? $"{tile.PipeType}@{tile.GridPosition}" : "null (leak originates at Source)";
    }

    private static void RequireDirectionsEqual(Direction2D[] actual, Direction2D[] expected, string context)
    {
        Require(actual.Length == expected.Length,
            $"{context}: expected {expected.Length} open direction(s) [{string.Join(",", expected)}], got {actual.Length} [{string.Join(",", actual)}].");

        foreach (Direction2D dir in expected)
        {
            Require(Array.IndexOf(actual, dir) >= 0,
                $"{context}: expected direction {dir} to be open, actual open set was [{string.Join(",", actual)}].");
        }
    }

    /// <summary>
    /// Diamond-shaped branching network: source -> Straight -> Tee(0,2), whose two
    /// extra ports (Right, Down) each lead - via a distinct 2-pipe path - into a
    /// second Tee at (1,1), which then continues down to Target. Forms a genuine
    /// 4-cycle (Tee(0,2)-(1,2)-(1,1)-(0,1)-Tee(0,2)) with zero leaks - used by
    /// cases 4, 12, 13 and 14.
    /// </summary>
    private static void BuildDiamondTeeNetwork(BoardManager2D board, List<GameObject> spawned)
    {
        CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 2), 0);
        CreatePipe(board, spawned, PipeType2D.Tee, new Vector2Int(0, 2), 2);       // Right+Down+Left, closed Up
        CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(1, 2), 2);    // Down+Left
        CreatePipe(board, spawned, PipeType2D.Tee, new Vector2Int(1, 1), 3);       // Down+Left+Up, closed Right
        CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(1, 0), 1);  // Up+Down
        CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(0, 1), 0);    // Up+Right
    }

    private static FlowSolveResult2D SolveDiamondTeeNetwork(BoardManager2D board, FlowSolver2D solver, List<GameObject> spawned)
    {
        BuildDiamondTeeNetwork(board, spawned);
        return solver.Solve(
            new Vector2Int(-2, 2), Direction2D.Right,
            new Vector2Int(1, -1), Direction2D.Up);
    }

    /// <summary>Two Crosses + 4 Corners, all four ports of both Crosses validly closed - used by case 8.</summary>
    private static void BuildFullyValidCrossNetwork(BoardManager2D board, List<GameObject> spawned)
    {
        CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 0), 0);
        CreatePipe(board, spawned, PipeType2D.Cross, new Vector2Int(0, 0), 0);
        CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(0, 1), 1);   // Right+Down
        CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(1, 1), 2);   // Down+Left
        CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(0, -1), 0);  // Up+Right
        CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(1, -1), 3);  // Left+Up
        CreatePipe(board, spawned, PipeType2D.Cross, new Vector2Int(1, 0), 0);
    }

    // ---------------- Case 01-03: existing behavior ----------------

    private static void Case01_StraightCornerValidRoute()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);

            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 0), 0);
            CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(0, 0), 2);
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(0, -1), 1);

            FlowSolveResult2D result = solver.Solve(
                new Vector2Int(-2, 0), Direction2D.Right,
                new Vector2Int(0, -2), Direction2D.Up);

            Require(result.IsSuccess, $"Expected IsSuccess=true, got false (FailureReason={result.FailureReason}, LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
            Require(result.TargetReached, "Expected TargetReached=true.");
            Require(!result.HasLeak, "Expected HasLeak=false.");
            Require(result.ReachableTiles.Count == 3, $"Expected ReachableTiles.Count=3, got {result.ReachableTiles.Count}.");
        });
    }

    private static void Case02_StraightCornerBrokenReciprocal()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);

            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 0), 0);
            // Corner rotationIndex 0 = Up+Right, does not open Left - breaks the connection.
            CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(0, 0), 0);
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(0, -1), 1);

            FlowSolveResult2D result = solver.Solve(
                new Vector2Int(-2, 0), Direction2D.Right,
                new Vector2Int(0, -2), Direction2D.Up);

            Require(!result.IsSuccess, "Expected IsSuccess=false for a broken reciprocal connection.");
            Require(result.HasLeak, "Expected HasLeak=true.");
            Require(result.FailureReason == FlowFailureReason2D.ReciprocalConnectionMissing,
                $"Expected FailureReason=ReciprocalConnectionMissing, got {result.FailureReason} (LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
        });
    }

    /// <summary>
    /// "All production levels remain solvable" - validates every level's REAL
    /// solved configuration using each pipe's explicit, hand-authored
    /// PipeSpawnData2D.solvedRotationIndex (production level metadata - see
    /// PipeSpawnData2D.cs). An earlier version of this case derived "solved"
    /// from the previous/next entries in the spawn list, assuming every level
    /// was one linear ordered path from Source to Target - that assumption
    /// held for Levels 1-3 but is structurally invalid for Levels 4-6, since
    /// two pipes that are both adjacent to the same Tee/Cross split are not
    /// necessarily adjacent to EACH OTHER (confirmed by the exact failure:
    /// "(1, 2) and (0, 1) are not grid-adjacent" in Level 4). solvedRotationIndex
    /// is authored once by hand and is never re-derived at validation time.
    /// </summary>
    private static void Case03_AllProductionLevelsRemainSolvable()
    {
        RunFixture(spawned =>
        {
            // Not hardcoded to 3 - Phase 7F.4 appended Levels 4-6, and this
            // must keep validating whatever the real production catalog
            // contains without needing a second count to update here.
            List<LevelData2D> levels = LevelManager2D.ResolveLevels();
            Require(levels.Count >= 3, $"Expected at least the original 3 production levels, found {levels.Count}.");

            for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
            {
                LevelData2D level = levels[levelIndex];
                int levelNumber = levelIndex + 1;

                BoardManager2D board = CreateBoard(spawned);
                FlowSolver2D solver = CreateSolver(board, spawned);

                foreach (PipeSpawnData2D spawn in level.pipes)
                {
                    CreatePipe(board, spawned, spawn.pipeType, spawn.gridPos, spawn.solvedRotationIndex);
                }

                FlowSolveResult2D result = solver.Solve(
                    level.sourcePos, level.sourceOutputDirection,
                    level.targetPos, level.targetInputDirection);

                if (result.IsSuccess && result.TargetReached && !result.HasLeak)
                {
                    Debug.Log($"[PASS 03.{levelNumber}] Level {levelNumber} - {level.levelName} solved configuration succeeds");
                }
                else
                {
                    string diagnostics = DescribeLevelSolvedDiagnostics(board, level, result);
                    Debug.LogError($"[FAIL 03.{levelNumber}] Level {levelNumber} - {level.levelName} solved configuration failed\n{diagnostics}");
                    throw new InvalidOperationException(
                        $"Level {levelNumber} '{level.levelName}' should succeed using its stored solvedRotationIndex values " +
                        $"(IsSuccess={result.IsSuccess}, TargetReached={result.TargetReached}, HasLeak={result.HasLeak}, " +
                        $"FailureReason={result.FailureReason}, LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}). " +
                        $"See [FAIL 03.{levelNumber}] log above for full tile-by-tile diagnostics.");
                }
            }
        });
    }

    /// <summary>Per Part C/E: full tile-by-tile diagnostic dump for a failed level - coordinate, type, stored solvedRotationIndex, open directions, every neighbor's coordinate/type/rotation and whether it reciprocates, with Source/Target handled explicitly (never treated as an ordinary empty neighbor).</summary>
    internal static string DescribeLevelSolvedDiagnostics(BoardManager2D board, LevelData2D level, FlowSolveResult2D result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Level: {level.levelName}");
        sb.AppendLine($"Source: {level.sourcePos} facing {level.sourceOutputDirection} | Target: {level.targetPos} facing {level.targetInputDirection}");
        sb.AppendLine("Rotations below are each pipe's stored solvedRotationIndex (production level metadata) - NOT startRotationIndex.");

        foreach (PipeSpawnData2D spawn in level.pipes)
        {
            PipeTile2D tile = board.GetPipe(spawn.gridPos);
            Direction2D[] openings = tile != null ? tile.GetOpenDirections() : Array.Empty<Direction2D>();
            sb.AppendLine($"Tile {spawn.gridPos}: {spawn.pipeType}, solvedRotationIndex={spawn.solvedRotationIndex}, opens [{string.Join(",", openings)}]");

            foreach (Direction2D dir in openings)
            {
                Vector2Int neighborPos = spawn.gridPos + dir.ToVector();

                if (neighborPos == level.targetPos)
                {
                    sb.AppendLine($"  {dir} neighbor {neighborPos}: TARGET (accepts from input direction {level.targetInputDirection}) - reciprocal: {dir.Opposite() == level.targetInputDirection}");
                    continue;
                }

                if (neighborPos == level.sourcePos)
                {
                    sb.AppendLine($"  {dir} neighbor {neighborPos}: SOURCE (emits {level.sourceOutputDirection}) - reciprocal: {dir.Opposite() == level.sourceOutputDirection}");
                    continue;
                }

                PipeTile2D neighborTile = board.GetPipe(neighborPos);
                if (neighborTile == null)
                {
                    sb.AppendLine($"  {dir} neighbor {neighborPos}: EMPTY (no tile) - reciprocal: false");
                    continue;
                }

                bool reciprocal = neighborTile.HasOpening(dir.Opposite());
                sb.AppendLine($"  {dir} neighbor {neighborPos}: {neighborTile.PipeType} rotation={neighborTile.RotationIndex} opens [{string.Join(",", neighborTile.GetOpenDirections())}] - opens {dir.Opposite()} back: {reciprocal}");
            }
        }

        sb.AppendLine($"Solve result: IsSuccess={result.IsSuccess}, TargetReached={result.TargetReached}, HasLeak={result.HasLeak}, " +
            $"FailureReason={result.FailureReason}, LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}, ReachableTiles={result.ReachableTiles.Count}");

        return sb.ToString();
    }

    // ---------------- Case 04-07: Tee ----------------

    private static void Case04_TeeValidNetwork()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);
            FlowSolveResult2D result = SolveDiamondTeeNetwork(board, solver, spawned);

            Require(result.IsSuccess, $"Expected IsSuccess=true, got false (FailureReason={result.FailureReason}, LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
            Require(result.TargetReached, "Expected TargetReached=true.");
            Require(!result.HasLeak, "Expected HasLeak=false.");
            Require(result.ReachableTiles.Count == 6, $"Expected ReachableTiles.Count=6, got {result.ReachableTiles.Count}.");
        });
    }

    private static void Case05_TeeDanglingBranch()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);

            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 0), 0);
            CreatePipe(board, spawned, PipeType2D.Tee, new Vector2Int(0, 0), 0); // Up+Left+Right, closed Down
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(1, 0), 0);
            // Tee's Up port at (0,1) is deliberately left empty - dangling branch.

            FlowSolveResult2D result = solver.Solve(
                new Vector2Int(-2, 0), Direction2D.Right,
                new Vector2Int(2, 0), Direction2D.Left);

            Require(result.TargetReached, "Expected TargetReached=true (the other branch is fully valid).");
            Require(result.HasLeak, "Expected HasLeak=true (dangling Up branch).");
            Require(!result.IsSuccess, "Expected IsSuccess=false even though Target was reached, because a reachable branch leaks.");
            Require(result.FailureReason == FlowFailureReason2D.MissingNeighbor,
                $"Expected FailureReason=MissingNeighbor, got {result.FailureReason} (LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
        });
    }

    private static void Case06_TeeReciprocalMismatch()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);

            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 0), 0);
            CreatePipe(board, spawned, PipeType2D.Tee, new Vector2Int(0, 0), 0); // Up+Left+Right, closed Down
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(1, 0), 0);
            // (0,1) exists but does not open Down - fails to reciprocate the Tee's Up port.
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(0, 1), 0); // Left+Right only

            FlowSolveResult2D result = solver.Solve(
                new Vector2Int(-2, 0), Direction2D.Right,
                new Vector2Int(2, 0), Direction2D.Left);

            Require(!result.IsSuccess, "Expected IsSuccess=false.");
            Require(result.HasLeak, "Expected HasLeak=true.");
            Require(result.FailureReason == FlowFailureReason2D.ReciprocalConnectionMissing,
                $"Expected FailureReason=ReciprocalConnectionMissing, got {result.FailureReason} (LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
        });
    }

    private static void Case07_TeeRotationMapping()
    {
        RunFixture(spawned =>
        {
            var go = new GameObject("ValidationTeeRotation");
            spawned.Add(go);
            var pipe = go.AddComponent<PipeTile2D>();

            pipe.Initialize(PipeType2D.Tee, 0, Vector2Int.zero);
            RequireDirectionsEqual(pipe.GetOpenDirections(), new[] { Direction2D.Up, Direction2D.Left, Direction2D.Right }, "rotationIndex 0");

            pipe.Initialize(PipeType2D.Tee, 1, Vector2Int.zero);
            RequireDirectionsEqual(pipe.GetOpenDirections(), new[] { Direction2D.Up, Direction2D.Right, Direction2D.Down }, "rotationIndex 1");

            pipe.Initialize(PipeType2D.Tee, 2, Vector2Int.zero);
            RequireDirectionsEqual(pipe.GetOpenDirections(), new[] { Direction2D.Right, Direction2D.Down, Direction2D.Left }, "rotationIndex 2");

            pipe.Initialize(PipeType2D.Tee, 3, Vector2Int.zero);
            RequireDirectionsEqual(pipe.GetOpenDirections(), new[] { Direction2D.Down, Direction2D.Left, Direction2D.Up }, "rotationIndex 3");
        });
    }

    // ---------------- Case 08-11: Cross ----------------

    private static void Case08_CrossValidNetwork()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);
            BuildFullyValidCrossNetwork(board, spawned);

            FlowSolveResult2D result = solver.Solve(
                new Vector2Int(-2, 0), Direction2D.Right,
                new Vector2Int(2, 0), Direction2D.Left);

            Require(result.IsSuccess, $"Expected IsSuccess=true, got false (FailureReason={result.FailureReason}, LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
            Require(result.TargetReached, "Expected TargetReached=true.");
            Require(!result.HasLeak, "Expected HasLeak=false.");
            Require(result.ReachableTiles.Count == 7, $"Expected ReachableTiles.Count=7, got {result.ReachableTiles.Count}.");
        });
    }

    private static void Case09_CrossDanglingSide()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);

            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 0), 0);
            CreatePipe(board, spawned, PipeType2D.Cross, new Vector2Int(0, 0), 0);
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(1, 0), 0);
            // Cross's Up and Down ports point at empty cells - dangling.

            FlowSolveResult2D result = solver.Solve(
                new Vector2Int(-2, 0), Direction2D.Right,
                new Vector2Int(2, 0), Direction2D.Left);

            Require(!result.IsSuccess, "Expected IsSuccess=false.");
            Require(result.HasLeak, "Expected HasLeak=true.");
            Require(result.FailureReason == FlowFailureReason2D.MissingNeighbor,
                $"Expected FailureReason=MissingNeighbor, got {result.FailureReason} (LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
        });
    }

    private static void Case10_CrossTapDoesNotRotate()
    {
        RunFixture(spawned =>
        {
            var go = new GameObject("ValidationCrossRotate");
            spawned.Add(go);
            var pipe = go.AddComponent<PipeTile2D>();
            pipe.Initialize(PipeType2D.Cross, 0, Vector2Int.zero);

            bool rotated = pipe.TryRotateByPlayer();

            Require(!rotated, "Expected TryRotateByPlayer() to return false for Cross.");
            Require(pipe.RotationIndex == 0, $"Expected RotationIndex to remain 0, got {pipe.RotationIndex}.");
            Require(!pipe.IsRotatable, "Expected IsRotatable=false for Cross.");
        });
    }

    private static void Case11_CrossTapDoesNotIncrementMoves()
    {
        RunFixture(spawned =>
        {
            var go = new GameObject("ValidationCrossMove");
            spawned.Add(go);
            var pipe = go.AddComponent<PipeTile2D>();
            pipe.Initialize(PipeType2D.Cross, 0, Vector2Int.zero);

            int rotatedEventCount = 0;
            pipe.OnPlayerRotated += () => rotatedEventCount++;

            pipe.TryRotateByPlayer();

            Require(rotatedEventCount == 0, $"Expected OnPlayerRotated to never fire for Cross, fired {rotatedEventCount} time(s).");
        });
    }

    // ---------------- Case 12-16: graph behavior ----------------

    private static void Case12_ValidCycleSucceeds()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);
            FlowSolveResult2D result = SolveDiamondTeeNetwork(board, solver, spawned);

            Require(result.IsSuccess,
                $"Expected the cyclic network to succeed, got IsSuccess=false (FailureReason={result.FailureReason}, LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
        });
    }

    private static void Case13_CycleTraversalTerminates()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);
            FlowSolveResult2D result = SolveDiamondTeeNetwork(board, solver, spawned);

            // A hung or duplicated traversal would over-count reachable tiles/waves;
            // a correctly cycle-safe BFS visits each of the 6 tiles exactly once.
            Require(result.ReachableTiles.Count == 6, $"Expected exactly 6 reachable tiles (cycle-safe termination), got {result.ReachableTiles.Count}.");

            int waveTileTotal = 0;
            foreach (IReadOnlyList<PipeFlowStep2D> wave in result.FlowSteps)
            {
                waveTileTotal += wave.Count;
            }
            Require(waveTileTotal == 6, $"Expected FlowSteps to total 6 tiles, got {waveTileTotal}.");
        });
    }

    private static void Case14_TileVisitedOnce()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);
            FlowSolveResult2D result = SolveDiamondTeeNetwork(board, solver, spawned);

            int occurrences = 0;
            foreach (PipeTile2D tile in result.ReachableTiles)
            {
                if (tile.GridPosition == new Vector2Int(1, 1))
                {
                    occurrences++;
                }
            }
            Require(occurrences == 1, $"Expected the merge tile (1,1) to be visited exactly once, got {occurrences}.");
        });
    }

    private static void Case15_DisconnectedPipeIgnored()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);

            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 0), 0);
            CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(0, 0), 2);
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(0, -1), 1);
            // Isolated pipe, not connected to anything - must be ignored entirely.
            CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(-2, -2), 0);

            FlowSolveResult2D result = solver.Solve(
                new Vector2Int(-2, 0), Direction2D.Right,
                new Vector2Int(0, -2), Direction2D.Up);

            Require(result.IsSuccess, $"Expected IsSuccess=true, got false (FailureReason={result.FailureReason}, LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
            Require(result.ReachableTiles.Count == 3, $"Expected ReachableTiles.Count=3 (isolated pipe excluded), got {result.ReachableTiles.Count}.");
        });
    }

    private static void Case16_OpenPortOutsideBoard()
    {
        RunFixture(spawned =>
        {
            BoardManager2D board = CreateBoard(spawned);
            FlowSolver2D solver = CreateSolver(board, spawned);

            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 0), 0);
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(0, 0), 0);
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(1, 0), 0);
            // (2,0)'s Right port points to (3,0), outside the default 5x5 board (-2..2).
            CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(2, 0), 0);

            FlowSolveResult2D result = solver.Solve(
                new Vector2Int(-2, 0), Direction2D.Right,
                new Vector2Int(0, -2), Direction2D.Up);

            Require(!result.IsSuccess, "Expected IsSuccess=false.");
            Require(result.HasLeak, "Expected HasLeak=true.");
            Require(result.FailureReason == FlowFailureReason2D.OpenPortOutsideBoard,
                $"Expected FailureReason=OpenPortOutsideBoard, got {result.FailureReason} (LeakTile={DescribeTile(result.LeakTile)}, LeakDirection={result.LeakDirection}).");
        });
    }
}
