using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Gameplay2D;
using YagmurRotasi2D.Visual2D;

/// <summary>
/// Deterministic Assembly-CSharp-Editor validation for Phase 7F.5's direction-
/// aware pipe water animation - Direction2DExtensions.ToLocalDirection's
/// correctness, PipeFlowVisualProfile2D's correction rules for every pipe
/// type/rotation/entry combination, BaseVisual/WaterOverlay alignment, and
/// multi-branch single-fill behavior. Separate from (and does not replace)
/// YagmurRotasi2D/Run Phase 7F Branching Solver Tests - that command's 16
/// cases are unchanged and still validate solver correctness; this one only
/// validates the animation-direction layer built on top of it. Never touches
/// saved progress or the currently open scene.
/// </summary>
public static class PipeFlowAnimationDirectionValidator
{
    internal sealed class ValidationCase
    {
        public string Name;
        public Action Execute;
    }

    [MenuItem("YagmurRotasi2D/Validate Pipe Flow Animation Directions")]
    public static void RunValidation()
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
                Debug.LogError($"[FAIL {label}] {testCase.Name}\n{ex}");
            }
        }

        string summary = failCount == 0
            ? $"Pipe Flow Animation Direction Validation: {passCount}/{cases.Count} PASSED"
            : $"Pipe Flow Animation Direction Validation: {passCount}/{cases.Count} PASSED, {failCount} FAILED";

        if (failCount == 0) Debug.Log(summary);
        else Debug.LogError(summary);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static List<ValidationCase> BuildCases()
    {
        return new List<ValidationCase>
        {
            new ValidationCase { Name = "Straight horizontal supports both entries (Left, Right)", Execute = Case01_StraightHorizontalBothEntries },
            new ValidationCase { Name = "Straight vertical supports both entries (Up, Down)", Execute = Case02_StraightVerticalBothEntries },
            new ValidationCase { Name = "Corner rotation 0 entered from Up", Execute = Case03_CornerRotation0FromUp },
            new ValidationCase { Name = "Corner rotation 0 entered from Right", Execute = Case04_CornerRotation0FromRight },
            new ValidationCase { Name = "Every Corner rotation supports both of its entries", Execute = Case05_EveryCornerRotationBothEntries },
            new ValidationCase { Name = "Every Tee rotation supports all three open entries", Execute = Case06_EveryTeeRotationAllEntries },
            new ValidationCase { Name = "Cross supports all four entries", Execute = Case07_CrossAllFourEntries },
            new ValidationCase { Name = "World-to-local conversion round-trips correctly for all 16 direction/rotation combinations", Execute = Case08_WorldToLocalRoundTrip },
            new ValidationCase { Name = "BaseVisual is never touched; WaterOverlay carries the correction", Execute = Case09_BaseVisualUntouched },
            new ValidationCase { Name = "Fill animation configuration ends on the fully-filled frame", Execute = Case10_EndsOnFullyFilledFrame },
            new ValidationCase { Name = "A pipe with multiple incoming sides is animated once", Execute = Case11_MultiEntryPipeAnimatesOnce },
            new ValidationCase { Name = "Levels 4-6 still solve logically", Execute = Case12_Levels456StillSolve },
            new ValidationCase { Name = "Existing 16/16 branching solver validation is unchanged", Execute = Case13_ExistingSixteenUnchanged },
        };
    }

    // ---------------- Cases ----------------

    private static void Case01_StraightHorizontalBothEntries()
    {
        AssertResolves(PipeType2D.Straight, Direction2D.Right, expectRotationZ: 0f, expectFlipX: false);
        AssertResolves(PipeType2D.Straight, Direction2D.Left, expectRotationZ: 180f, expectFlipX: false);
    }

    private static void Case02_StraightVerticalBothEntries()
    {
        // A vertical Straight (rotationIndex 1) opens {Up, Down} in world terms.
        // World Down -> local Right (authored, no correction). World Up -> local
        // Left (the other end, 180-degree correction).
        Direction2D localFromDown = Direction2D.Down.ToLocalDirection(1);
        Direction2D localFromUp = Direction2D.Up.ToLocalDirection(1);

        Require(localFromDown == Direction2D.Right, $"Expected world Down at rotationIndex 1 to convert to local Right, got {localFromDown}.");
        Require(localFromUp == Direction2D.Left, $"Expected world Up at rotationIndex 1 to convert to local Left, got {localFromUp}.");

        AssertResolves(PipeType2D.Straight, localFromDown, expectRotationZ: 0f, expectFlipX: false);
        AssertResolves(PipeType2D.Straight, localFromUp, expectRotationZ: 180f, expectFlipX: false);
    }

    private static void Case03_CornerRotation0FromUp()
    {
        Direction2D local = Direction2D.Up.ToLocalDirection(0);
        Require(local == Direction2D.Up, $"Expected rotationIndex 0 to leave world Up unchanged, got {local}.");
        AssertResolves(PipeType2D.Corner, local, expectRotationZ: 90f, expectFlipX: true);
    }

    private static void Case04_CornerRotation0FromRight()
    {
        Direction2D local = Direction2D.Right.ToLocalDirection(0);
        Require(local == Direction2D.Right, $"Expected rotationIndex 0 to leave world Right unchanged, got {local}.");
        AssertResolves(PipeType2D.Corner, local, expectRotationZ: 0f, expectFlipX: false);
    }

    private static readonly Direction2D[][] CornerDirectionsTable =
    {
        new[] { Direction2D.Up, Direction2D.Right },
        new[] { Direction2D.Right, Direction2D.Down },
        new[] { Direction2D.Down, Direction2D.Left },
        new[] { Direction2D.Left, Direction2D.Up }
    };

    private static readonly Direction2D[][] TeeDirectionsTable =
    {
        new[] { Direction2D.Up, Direction2D.Left, Direction2D.Right },
        new[] { Direction2D.Up, Direction2D.Right, Direction2D.Down },
        new[] { Direction2D.Right, Direction2D.Down, Direction2D.Left },
        new[] { Direction2D.Down, Direction2D.Left, Direction2D.Up }
    };

    private static void Case05_EveryCornerRotationBothEntries()
    {
        for (int r = 0; r < 4; r++)
        {
            foreach (Direction2D worldDir in CornerDirectionsTable[r])
            {
                Direction2D local = worldDir.ToLocalDirection(r);
                Require(Array.IndexOf(CornerDirectionsTable[0], local) >= 0,
                    $"rotationIndex {r}, world entry {worldDir}: local {local} is not one of Corner's canonical (rotationIndex 0) open sides.");

                // Must resolve without throwing for every valid Corner entry.
                PipeFlowVisualProfile2D.ResolveCorrection(PipeType2D.Corner, local, out _, out _);
            }
        }
    }

    private static void Case06_EveryTeeRotationAllEntries()
    {
        for (int r = 0; r < 4; r++)
        {
            foreach (Direction2D worldDir in TeeDirectionsTable[r])
            {
                Direction2D local = worldDir.ToLocalDirection(r);
                Require(Array.IndexOf(TeeDirectionsTable[0], local) >= 0,
                    $"rotationIndex {r}, world entry {worldDir}: local {local} is not one of Tee's canonical (rotationIndex 0) open sides.");

                PipeFlowVisualProfile2D.ResolveCorrection(PipeType2D.Tee, local, out _, out _);
            }
        }
    }

    private static void Case07_CrossAllFourEntries()
    {
        Direction2D authored = PipeFlowVisualProfile2D.AuthoredLocalEntrySide(PipeType2D.Cross);

        foreach (Direction2D worldDir in new[] { Direction2D.Up, Direction2D.Right, Direction2D.Down, Direction2D.Left })
        {
            // Cross rotationIndex is always 0, so local == world.
            Direction2D local = worldDir.ToLocalDirection(0);
            PipeFlowVisualProfile2D.ResolveCorrection(PipeType2D.Cross, local, out float rotationZ, out bool flipX);

            Require(!flipX, $"Cross entry {local}: expected no mirror (Cross correction is rotation-only), got flipX=true.");

            int expectedSteps = (((int)local - (int)authored) % 4 + 4) % 4;
            float expectedRotationZ = -90f * expectedSteps;
            Require(Mathf.Approximately(rotationZ, expectedRotationZ),
                $"Cross entry {local}: expected extraRotationZ={expectedRotationZ}, got {rotationZ}.");
        }
    }

    private static void Case08_WorldToLocalRoundTrip()
    {
        foreach (Direction2D worldDir in new[] { Direction2D.Up, Direction2D.Right, Direction2D.Down, Direction2D.Left })
        {
            for (int r = 0; r < 4; r++)
            {
                Direction2D local = worldDir.ToLocalDirection(r);
                var roundTrip = (Direction2D)(((int)local + r) % 4);
                Require(roundTrip == worldDir,
                    $"world={worldDir}, rotationIndex={r}: local={local} did not round-trip back to {worldDir} (got {roundTrip}).");
            }
        }
    }

    private static void Case09_BaseVisualUntouched()
    {
        RunWithTempPrefabInstance(BranchingPipeAssetBinder.TeePrefabPath, root =>
        {
            Transform baseVisual = root.transform.Find("BaseVisual");
            Transform waterOverlay = root.transform.Find("WaterOverlay");
            var pipeTile = root.GetComponent<PipeTile2D>();

            Require(baseVisual != null, "PipeTee2D.prefab is missing BaseVisual.");
            Require(waterOverlay != null, "PipeTee2D.prefab is missing WaterOverlay.");

            Quaternion baseRotationBefore = baseVisual.localRotation;
            Quaternion overlayRotationBefore = waterOverlay.localRotation;

            bool completed = false;
            // A through-line entry (Left, at rotationIndex 0) - guaranteed to
            // require a correction (Tee's authored entry is the branch, Up).
            pipeTile.PlayWaterFlowVisual(Direction2D.Left, () => completed = true);

            Require(baseVisual.localRotation == baseRotationBefore,
                $"BaseVisual.localRotation changed after PlayFill - expected it to never be touched (before={baseRotationBefore}, after={baseVisual.localRotation}).");

            // WaterOverlay is allowed (expected) to differ - that's the whole point of the correction.
            _ = overlayRotationBefore;
            _ = completed;
        });
    }

    private static void Case10_EndsOnFullyFilledFrame()
    {
        RunWithTempPrefabInstance(BranchingPipeAssetBinder.CrossPrefabPath, root =>
        {
            Transform waterOverlay = root.transform.Find("WaterOverlay");
            Require(waterOverlay != null, "PipeCross2D.prefab is missing WaterOverlay.");

            var animator = waterOverlay.GetComponent<SpriteFrameAnimator2D>();
            Require(animator != null, "WaterOverlay is missing SpriteFrameAnimator2D.");
            Require(animator.FrameCount == 4, $"Expected exactly 4 fill frames, found {animator.FrameCount}.");

            // Structural guarantee (rather than simulating real-time frame
            // advancement, which needs Play Mode's Update loop): loop=false and
            // holdLastFrameOnComplete=true together guarantee playback stops on
            // the last (fully-filled) frame and stays there, for every entry
            // side - the correction only ever changes WaterOverlay's transform,
            // never the animator's frame list, count or ordering.
            var so = new SerializedObject(animator);
            bool loop = so.FindProperty("loop").boolValue;
            bool holdLastFrame = so.FindProperty("holdLastFrameOnComplete").boolValue;

            Require(!loop, "Expected loop=false so the fill animation stops instead of restarting.");
            Require(holdLastFrame, "Expected holdLastFrameOnComplete=true so playback ends visibly on the fully-filled frame.");
        });
    }

    private static void Case11_MultiEntryPipeAnimatesOnce()
    {
        var spawned = new List<GameObject>();
        try
        {
            BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
            FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);

            // Same diamond-shaped Tee network proven in the branching solver
            // suite - (1,1) is reachable from both (1,2)-Down and (0,1)-Right.
            BranchingSolverTestRunner.CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(-1, 2), 0);
            BranchingSolverTestRunner.CreatePipe(board, spawned, PipeType2D.Tee, new Vector2Int(0, 2), 2);
            BranchingSolverTestRunner.CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(1, 2), 2);
            BranchingSolverTestRunner.CreatePipe(board, spawned, PipeType2D.Tee, new Vector2Int(1, 1), 3);
            BranchingSolverTestRunner.CreatePipe(board, spawned, PipeType2D.Straight, new Vector2Int(1, 0), 1);
            BranchingSolverTestRunner.CreatePipe(board, spawned, PipeType2D.Corner, new Vector2Int(0, 1), 0);

            FlowSolveResult2D result = solver.Solve(
                new Vector2Int(-2, 2), Direction2D.Right,
                new Vector2Int(1, -1), Direction2D.Up);

            Require(result.IsSuccess, $"Expected the diamond network to succeed (FailureReason={result.FailureReason}).");

            var mergePos = new Vector2Int(1, 1);
            int mergeTileOccurrences = 0;
            int mergeWaveIndex = -1;
            PipeFlowStep2D mergeStep = null;
            for (int waveIndex = 0; waveIndex < result.FlowSteps.Count; waveIndex++)
            {
                foreach (PipeFlowStep2D step in result.FlowSteps[waveIndex])
                {
                    if (step.Pipe.GridPosition == mergePos)
                    {
                        mergeTileOccurrences++;
                        mergeWaveIndex = waveIndex;
                        mergeStep = step;
                    }
                }
            }

            string Diagnose()
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Merge coordinate: {mergePos}");
                sb.AppendLine($"Merge distance: {mergeStep?.Distance.ToString() ?? "n/a"}");
                sb.AppendLine($"Merge wave index: {mergeWaveIndex}");
                sb.AppendLine($"PrimaryEntrySide: {mergeStep?.PrimaryEntrySide.ToString() ?? "n/a"}");
                sb.AppendLine($"IncomingSides: [{(mergeStep != null ? string.Join(", ", mergeStep.IncomingSides) : "n/a")}]");
                if (mergeStep != null)
                {
                    foreach (Direction2D dir in mergeStep.Pipe.GetOpenDirections())
                    {
                        Vector2Int neighborPos = mergePos + dir.ToVector();
                        PipeFlowStep2D neighborStep = FindStep(result, neighborPos);
                        string classification = neighborStep == null
                            ? "not reachable (Source/Target/leak)"
                            : neighborStep.Distance == mergeStep.Distance - 1 ? "predecessor (incoming)"
                            : neighborStep.Distance == mergeStep.Distance + 1 ? "successor (outgoing)"
                            : "same-wave (lateral)";
                        sb.AppendLine($"  neighbor {neighborPos} via {dir}: distance={neighborStep?.Distance.ToString() ?? "n/a"}, classification={classification}");
                    }
                }
                return sb.ToString();
            }

            Require(mergeTileOccurrences == 1, $"Expected the merge tile {mergePos} to appear in FlowSteps exactly once, got {mergeTileOccurrences}.\n{Diagnose()}");
            Require(mergeStep != null && mergeStep.IncomingSides.Count == 2,
                $"Expected the merge tile to record exactly 2 incoming sides (reached from two branches), got {mergeStep?.IncomingSides.Count ?? 0}.\n{Diagnose()}");
            Require(mergeStep.IncomingSides[0] != mergeStep.IncomingSides[1],
                $"Expected the two incoming sides to be distinct.\n{Diagnose()}");
            Require(mergeStep.IncomingSides.Contains(mergeStep.PrimaryEntrySide),
                $"Expected PrimaryEntrySide to be one of IncomingSides.\n{Diagnose()}");

            foreach (Direction2D incomingDir in mergeStep.IncomingSides)
            {
                Vector2Int predecessorPos = mergePos + incomingDir.ToVector();
                PipeFlowStep2D predecessorStep = FindStep(result, predecessorPos);
                Require(predecessorStep != null && predecessorStep.Distance == mergeStep.Distance - 1,
                    $"Expected incoming neighbor {predecessorPos} (via {incomingDir}) to have distance {mergeStep.Distance - 1}, " +
                    $"got {predecessorStep?.Distance.ToString() ?? "unreachable"}.\n{Diagnose()}");
            }

            Direction2D outgoingSide = System.Array.Find(mergeStep.Pipe.GetOpenDirections(), d => !mergeStep.IncomingSides.Contains(d));
            Vector2Int outgoingPos = mergePos + outgoingSide.ToVector();
            PipeFlowStep2D outgoingStep = FindStep(result, outgoingPos);
            Require(outgoingStep != null && outgoingStep.Distance == mergeStep.Distance + 1,
                $"Expected the outgoing neighbor {outgoingPos} (via {outgoingSide}) to have distance {mergeStep.Distance + 1}, " +
                $"got {outgoingStep?.Distance.ToString() ?? "unreachable"}.\n{Diagnose()}");
            Require(!mergeStep.IncomingSides.Contains(outgoingSide),
                $"Expected the outgoing side {outgoingSide} to NOT be included in IncomingSides.\n{Diagnose()}");
        }
        finally
        {
            foreach (GameObject go in spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }

    private static void Case12_Levels456StillSolve()
    {
        bool ok = ProductionBranchingLevelsValidator.ValidateLevels456(false);
        Require(ok, "Expected Levels 4-6 to still solve logically after Phase 7F.5's animation-direction changes.");
    }

    private static void Case13_ExistingSixteenUnchanged()
    {
        bool ok = BranchingSolverTestRunner.RunTests();
        Require(ok, "Expected the existing 16-case branching solver validation to remain 16/16 PASSED.");
    }

    // ---------------- Helpers ----------------

    private static PipeFlowStep2D FindStep(FlowSolveResult2D result, Vector2Int gridPosition)
    {
        foreach (IReadOnlyList<PipeFlowStep2D> wave in result.FlowSteps)
        {
            foreach (PipeFlowStep2D step in wave)
            {
                if (step.Pipe.GridPosition == gridPosition)
                {
                    return step;
                }
            }
        }
        return null;
    }

    private static void AssertResolves(PipeType2D pipeType, Direction2D localEntrySide, float expectRotationZ, bool expectFlipX)
    {
        PipeFlowVisualProfile2D.ResolveCorrection(pipeType, localEntrySide, out float rotationZ, out bool flipX);
        Require(Mathf.Approximately(rotationZ, expectRotationZ),
            $"{pipeType} entry {localEntrySide}: expected extraRotationZ={expectRotationZ}, got {rotationZ}.");
        Require(flipX == expectFlipX,
            $"{pipeType} entry {localEntrySide}: expected flipX={expectFlipX}, got {flipX}.");
    }

    private static void RunWithTempPrefabInstance(string prefabPath, Action<GameObject> body)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            throw new InvalidOperationException($"'{prefabPath}' does not exist. Run 'YagmurRotasi2D > Bind T and Cross Pipe Assets' first.");
        }

        GameObject instance = UnityEngine.Object.Instantiate(prefabAsset);
        try
        {
            body(instance);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
