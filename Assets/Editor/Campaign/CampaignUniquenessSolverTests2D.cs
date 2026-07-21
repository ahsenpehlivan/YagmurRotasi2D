using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Phase 8A.3 Part O: a small deterministic fixture suite for
/// CampaignUniquenessSolver2D. Cases that have a single, hand-verifiable
/// correct answer (a lone forced pipe, or the real production Levels 4-6
/// data already validated across many earlier phases) assert that exact
/// answer. Cases built specifically to be densely-connected (where a pipe
/// has more real neighbors than it needs, the one situation that can
/// produce genuine rotation ambiguity in this puzzle model - see the class
/// comment on BruteForceCanonicalCount) are NOT hand-predicted to a specific
/// solution count - hand-deriving multi-pipe reciprocal combinatorics
/// without being able to execute the code is exactly the kind of reasoning
/// that is easy to get subtly wrong, so instead each one is cross-checked
/// against an independent, deliberately naive brute-force enumeration
/// written fresh in this file (simple nested enumeration over canonical
/// masks, correct by inspection rather than by cleverness). The pass
/// criterion is agreement between the optimized solver and the brute-force
/// reference, whatever the true count turns out to be - this still catches
/// real regressions without requiring a possibly-wrong manual prediction.
/// </summary>
public static class CampaignUniquenessSolverTests2D
{
    private sealed class Case
    {
        public string Name;
        public System.Action Execute;
    }

    [MenuItem("YagmurRotasi2D/Phase 8/Run Uniqueness Solver Fixture Suite")]
    public static bool RunFixtureSuite()
    {
        var cases = new List<Case>
        {
            new Case { Name = "Unique Straight/Corner route (single forced Corner)", Execute = Case1_UniqueSingleCorner },
            new Case { Name = "Dense Tee hub, cross-checked against brute force", Execute = Case2_DenseTeeHubCrossCheck },
            new Case { Name = "Production Level 4 (Tee network) reports exactly one solution", Execute = () => CaseProductionLevel(4) },
            new Case { Name = "Production Level 5 (Cross network) reports exactly one solution", Execute = () => CaseProductionLevel(5) },
            new Case { Name = "Production Level 6 (Tee+Cross network) reports exactly one solution", Execute = () => CaseProductionLevel(6) },
            new Case { Name = "Dense 4-neighbor Corner hub, cross-checked against brute force", Execute = Case6_DenseCornerHubCrossCheck },
            new Case { Name = "Memo key: same domains/same cells produce the same key", Execute = Case7_MemoKeySameStateSameKey },
            new Case { Name = "Memo key: domains swapped between cells produce different keys", Execute = Case8_MemoKeySwappedCellsDiffer },
            new Case { Name = "Memo key: differs=false vs differs=true produce different keys", Execute = Case9_MemoKeyDiffersFlagDistinguishes },
            new Case { Name = "Memo key: stable/deterministic across repeated independent builds", Execute = Case10_MemoKeyStableAcrossRepeatedRuns },
            new Case { Name = "Memoized vs non-memoized solver agree on every fixture", Execute = Case11_MemoizedVsNonMemoizedAgreement },
        };

        int passCount = 0;
        int failCount = 0;
        for (int i = 0; i < cases.Count; i++)
        {
            string label = $"{i + 1:00}/{cases.Count:00}";
            try
            {
                cases[i].Execute();
                passCount++;
                Debug.Log($"[PASS {label}] {cases[i].Name}");
            }
            catch (System.Exception ex)
            {
                failCount++;
                Debug.LogError($"[FAIL {label}] {cases[i].Name}\n{ex}");
            }
        }

        string summary = failCount == 0
            ? $"Uniqueness Solver Fixture Suite: {passCount}/{cases.Count} PASSED"
            : $"Uniqueness Solver Fixture Suite: {passCount}/{cases.Count} PASSED, {failCount} FAILED";
        if (failCount == 0) Debug.Log(summary); else Debug.LogError(summary);

        return failCount == 0;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new System.InvalidOperationException(message);
    }

    /// <summary>
    /// Phase 8A.3.1 fixture preflight check - added after Case 02's failure
    /// (a pipe placed outside the brute-force reference's actual grid size)
    /// turned out to surface as a deep, confusing IsInsideGrid=False/NRE
    /// inside CreatePipe rather than a clear fixture-authoring error. Every
    /// fixture that declares its own GridBounds2D should validate against it
    /// BEFORE any board/solver is built, so a future bounds mistake fails
    /// immediately with an exact coordinate and reason instead of a
    /// confusing downstream crash.
    /// </summary>
    private static void RequireFixtureFitsBounds(
        GridBounds2D bounds, Vector2Int source, Vector2Int target, List<PipeSpawnData2D> pipes)
    {
        Require(bounds.Contains(source), $"fixture bounds preflight: Source {source} lies outside declared bounds {bounds.MinCell}..{bounds.MaxCell}");
        Require(bounds.Contains(target), $"fixture bounds preflight: Target {target} lies outside declared bounds {bounds.MinCell}..{bounds.MaxCell}");

        var seenPositions = new HashSet<Vector2Int>();
        foreach (PipeSpawnData2D pipe in pipes)
        {
            Require(bounds.Contains(pipe.gridPos), $"fixture bounds preflight: pipe at {pipe.gridPos} lies outside declared bounds {bounds.MinCell}..{bounds.MaxCell}");
            Require(pipe.gridPos != source, $"fixture bounds preflight: pipe at {pipe.gridPos} overlaps Source");
            Require(pipe.gridPos != target, $"fixture bounds preflight: pipe at {pipe.gridPos} overlaps Target");
            Require(seenPositions.Add(pipe.gridPos), $"fixture bounds preflight: duplicate pipe position {pipe.gridPos}");
        }
    }

    // ---------------- Case 1: single forced Corner ----------------

    private static void Case1_UniqueSingleCorner()
    {
        // Target's required predecessor cell is target + targetEntryDirection
        // (Up) - verified against real production Level 1 data, whose last
        // pipe before Target sits exactly one row ABOVE Target. So for the
        // single pipe at (1,0) to be Target's predecessor, Target must sit
        // at (1,-1) (one row BELOW the pipe), not (1,1).
        Vector2Int source = new Vector2Int(0, 0);
        Vector2Int target = new Vector2Int(1, -1);
        var pipes = new List<PipeSpawnData2D>
        {
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 0), 0, 0),
        };
        var bounds = new GridBounds2D(4, 4);

        // Required mask = {Left (toward Source), Down (toward Target)} = Corner rotationIndex 2 (Down+Left).
        pipes[0].solvedRotationIndex = 2;

        var outcome = CampaignUniquenessSolver2D.CountSolutions(
            bounds, source, Direction2D.Right, target, Direction2D.Up, pipes, 10000, out int solutionsFound);

        Require(outcome == CampaignUniquenessSolver2D.UniquenessOutcome.One, $"expected exactly One, got {outcome} ({solutionsFound} found)");
    }

    // ---------------- Case 2/6: brute-force cross-checked dense hubs ----------------

    private static void Case2_DenseTeeHubCrossCheck()
    {
        // M is a Tee with all 4 sides real (Source + 3 pipes) - the one
        // configuration in this puzzle model where a single cell retains
        // genuine rotational choice (see class comment). Short simple arms;
        // the true solution count is whatever brute force finds it to be.
        Vector2Int source = new Vector2Int(0, 0);
        Vector2Int target = new Vector2Int(3, -1);
        var pipes = new List<PipeSpawnData2D>
        {
            new PipeSpawnData2D(PipeType2D.Tee, new Vector2Int(1, 0), 0, 0),
            new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(2, 0), 0, 0),
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(3, 0), 0, 0),
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 1), 0, 0),
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, 1), 0, 0),
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, -1), 0, 0),
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, -1), 0, 0),
        };

        CrossCheckAgainstBruteForce(source, Direction2D.Right, target, Direction2D.Up, pipes);
    }

    private static void Case6_DenseCornerHubCrossCheck()
    {
        // BUG HISTORY (Phase 8A.3.1): the previous fixture here was a plain
        // 2x2 block of 4 Corners (Source -> (1,0) -> (1,1) -> (2,1) -> (2,0)
        // -> Target). That shape is unsolvable BY CONSTRUCTION, not just a
        // brute-force fluke: every one of those 4 cells only ever has 2 real
        // (in-bounds, occupied-or-Source/Target) neighbors, which forces
        // each one down to exactly one canonical mask with zero search
        // freedom - and tracing those 4 forced masks through shows (2,0)
        // would need Up open (to receive from (2,1) above it) AND Down open
        // (to reach Target below it) simultaneously, i.e. a straight
        // pass-through, which no Corner mask can ever provide (Corner masks
        // are only the 4 ADJACENT pairs - Up+Right/Right+Down/Down+Left/
        // Left+Up - never the opposite-side pairs a Straight has). Brute
        // force correctly found 0 solutions; this was a bad fixture, not a
        // solver bug.
        //
        // Replacement (graph-first, hand-traced port-by-port below): H is
        // the source-adjacent Corner with FOUR real neighbors (Source, plus
        // 3 placed pipes on its other 3 sides) - a genuine dense hub, since
        // a Corner adjacent to Source always retains a real 2-way choice
        // between its two Left-containing masks (Left+Up or Down+Left;
        // Right is structurally impossible alongside the Source-forced Left
        // for ANY Corner, so H's 4th neighbor W never actually receives a
        // connection through H in either choice - see below).
        //
        // Choice A - H=Left+Up: forces a fully-reciprocal, leak-free chain
        // that reaches Target: H(Left+Up) -> U(1,1)=Right+Down -> V(2,1)
        // =Down+Left -> W(2,0)=Up+Right -> Target(3,0) (targetEntryDirection
        // Left, so W is Target's predecessor and is forced to include
        // Right). Every port pairing was traced and confirmed reciprocal:
        // H.Up<->U.Down, U.Right<->V.Left, V.Down<->W.Up, W.Right<->Target,
        // and the H.Right<->W.Left edge is consistently CLOSED on both
        // sides (neither mask includes it) - not a leak.
        // Choice B - H=Down+Left: opens toward a second placed group
        // (Dn1..Dn4, a small closed loop at (1,-1)/(0,-1)/(0,-2)/(1,-2), each
        // forced to exactly one mask by its own 2 real neighbors) that never
        // connects onward to Target - so this choice never reaches Target
        // and contributes zero solutions, regardless of how Dn1..Dn4
        // resolve. This loop's only purpose is to legitimately give H its
        // required 4th real neighbor without introducing a leak: it is
        // fully self-consistent (Dn1-Dn2-Dn3-Dn4 forced masks all
        // reciprocate each other) and entirely disconnected from Source/
        // Target, so FlowSolver2D never even inspects it when H picks
        // Left+Up (Choice A) - an unreached pipe's rotation can never fail
        // the route.
        Vector2Int source = new Vector2Int(0, 0);
        Vector2Int target = new Vector2Int(3, 0);
        var pipes = new List<PipeSpawnData2D>
        {
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 0), 0, 0),   // H - dense hub, Source-adjacent
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 1), 0, 0),   // U
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, 1), 0, 0),   // V
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, 0), 0, 0),   // W - Target-adjacent
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, -1), 0, 0),  // Dn1 - H's 4th real neighbor
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(0, -1), 0, 0),  // Dn2
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(0, -2), 0, 0),  // Dn3
            new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, -2), 0, 0),  // Dn4
        };

        CrossCheckAgainstBruteForce(source, Direction2D.Right, target, Direction2D.Left, pipes);
    }

    private static void CrossCheckAgainstBruteForce(
        Vector2Int source, Direction2D sourceOutputDirection, Vector2Int target, Direction2D targetEntryDirection,
        List<PipeSpawnData2D> pipes)
    {
        // BUG HISTORY (Phase 8A.3.1): this call site used to omit `bounds`
        // entirely when calling the two brute-force helpers below, even
        // though both had already been updated to accept it and resize
        // their own throwaway BoardManager2D via SetGridSize(). That left
        // the brute-force board stuck at its 5x5 default while this fixture
        // (declared here as 6x6) legitimately places pipes out to x=3 -
        // valid for 6x6 (MinCell/MaxCell -2..3) but rejected as
        // "IsInsideGrid=False" against an unresized 5x5 (max x=2). Both
        // helpers must always receive the SAME bounds CountSolutions()
        // receives below - never let the two silently disagree on board size.
        var bounds = new GridBounds2D(6, 6);
        RequireFixtureFitsBounds(bounds, source, target, pipes);

        int bruteForceCount = BruteForceCanonicalCount(bounds, source, sourceOutputDirection, target, targetEntryDirection, pipes, capAt: 3);

        Require(bruteForceCount >= 1, "brute-force reference found 0 solutions - fixture itself is unsolvable, fix the fixture, not the solver");

        // The known-solution-first API requires a stored solvedRotationIndex
        // per pipe - use the first canonical mask brute force found to be
        // valid as "the known solution" for this check.
        int[] solvedRotations = FindAnyValidRotationAssignment(bounds, source, sourceOutputDirection, target, targetEntryDirection, pipes);
        Require(solvedRotations != null, "could not find any valid rotation assignment to seed as the known solution (contradicts brute force finding >=1)");
        for (int i = 0; i < pipes.Count; i++) pipes[i].solvedRotationIndex = solvedRotations[i];

        var outcome = CampaignUniquenessSolver2D.CountSolutions(
            bounds, source, sourceOutputDirection, target, targetEntryDirection, pipes, 50000, out int solverFound);

        if (bruteForceCount == 1)
        {
            Require(outcome == CampaignUniquenessSolver2D.UniquenessOutcome.One, $"brute force found exactly 1, solver reported {outcome} ({solverFound})");
        }
        else
        {
            Require(outcome == CampaignUniquenessSolver2D.UniquenessOutcome.TwoOrMore, $"brute force found {bruteForceCount} (>1), solver reported {outcome} ({solverFound})");
        }
    }

    // ---------------- Production Levels 4-6 ----------------

    private static void CaseProductionLevel(int levelNumberOneIndexed)
    {
        List<YagmurRotasi2D.Data2D.LevelData2D> levels = LevelManager2D.BuildLevels();
        Require(levelNumberOneIndexed - 1 < levels.Count, $"BuildLevels() does not contain level {levelNumberOneIndexed}");
        YagmurRotasi2D.Data2D.LevelData2D data = levels[levelNumberOneIndexed - 1];

        var pipes = new List<PipeSpawnData2D>(data.pipes);
        var bounds = new GridBounds2D(data.gridWidth, data.gridHeight);

        var outcome = CampaignUniquenessSolver2D.CountSolutions(
            bounds, data.sourcePos, data.sourceOutputDirection, data.targetPos, data.targetInputDirection,
            pipes, 200000, out int solutionsFound);

        Require(outcome == CampaignUniquenessSolver2D.UniquenessOutcome.One,
            $"Level {levelNumberOneIndexed} ('{data.levelName}') expected exactly One solution, got {outcome} ({solutionsFound} found) - this is real production data, already hand-verified solvable in earlier phases, so a non-One result here means a solver bug, not a bad fixture");
    }

    // ---------------- Phase 8A.4: memoization key correctness ----------------

    private static void Case7_MemoKeySameStateSameKey()
    {
        var typeByIndex = new PipeType2D[] { PipeType2D.Corner, PipeType2D.Corner };
        var cellsOrder = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
        var cornerMasks = CampaignPipeOrientationCatalog2D.CanonicalMasks(PipeType2D.Corner);
        Require(cornerMasks.Count >= 2, "Corner must have at least 2 canonical masks for this test");

        int[] stableRank = CampaignUniquenessSolver2D.ComputeStableCoordinateOrder(cellsOrder, 2);

        var domainsA = new List<int>[] { new List<int> { cornerMasks[0] }, new List<int> { cornerMasks[1] } };
        var domainsB = new List<int>[] { new List<int> { cornerMasks[0] }, new List<int> { cornerMasks[1] } };

        bool ok1 = CampaignUniquenessSolver2D.TryBuildMemoKey(domainsA, typeByIndex, stableRank, 2, false, out var key1);
        bool ok2 = CampaignUniquenessSolver2D.TryBuildMemoKey(domainsB, typeByIndex, stableRank, 2, false, out var key2);

        Require(ok1 && ok2, "TryBuildMemoKey should succeed for 2 variables");
        Require(key1.Equals(key2), "identical domains in identical cells must produce the same key");
    }

    private static void Case8_MemoKeySwappedCellsDiffer()
    {
        var typeByIndex = new PipeType2D[] { PipeType2D.Corner, PipeType2D.Corner };
        var cellsOrder = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
        var cornerMasks = CampaignPipeOrientationCatalog2D.CanonicalMasks(PipeType2D.Corner);
        Require(cornerMasks.Count >= 2, "Corner must have at least 2 canonical masks for this test");
        int maskX = cornerMasks[0];
        int maskY = cornerMasks[1];

        int[] stableRank = CampaignUniquenessSolver2D.ComputeStableCoordinateOrder(cellsOrder, 2);

        var domainsOriginal = new List<int>[] { new List<int> { maskX }, new List<int> { maskY } };
        var domainsSwapped = new List<int>[] { new List<int> { maskY }, new List<int> { maskX } };

        bool ok1 = CampaignUniquenessSolver2D.TryBuildMemoKey(domainsOriginal, typeByIndex, stableRank, 2, false, out var keyOriginal);
        bool ok2 = CampaignUniquenessSolver2D.TryBuildMemoKey(domainsSwapped, typeByIndex, stableRank, 2, false, out var keySwapped);

        Require(ok1 && ok2, "TryBuildMemoKey should succeed for 2 variables");
        Require(!keyOriginal.Equals(keySwapped), "swapping domain content between two different cells must produce a different key");
    }

    private static void Case9_MemoKeyDiffersFlagDistinguishes()
    {
        var typeByIndex = new PipeType2D[] { PipeType2D.Corner };
        var cellsOrder = new Vector2Int[] { new Vector2Int(0, 0) };
        var cornerMasks = CampaignPipeOrientationCatalog2D.CanonicalMasks(PipeType2D.Corner);
        int[] stableRank = CampaignUniquenessSolver2D.ComputeStableCoordinateOrder(cellsOrder, 1);

        var domains = new List<int>[] { new List<int> { cornerMasks[0] } };

        bool okFalse = CampaignUniquenessSolver2D.TryBuildMemoKey(domains, typeByIndex, stableRank, 1, false, out var keyFalse);
        bool okTrue = CampaignUniquenessSolver2D.TryBuildMemoKey(domains, typeByIndex, stableRank, 1, true, out var keyTrue);

        Require(okFalse && okTrue, "TryBuildMemoKey should succeed for 1 variable");
        Require(!keyFalse.Equals(keyTrue), "differsFromKnownSolution=false must produce a different key than an otherwise-identical differs=true state");
    }

    private static void Case10_MemoKeyStableAcrossRepeatedRuns()
    {
        var cornerMasks = CampaignPipeOrientationCatalog2D.CanonicalMasks(PipeType2D.Corner);
        var teeMasks = CampaignPipeOrientationCatalog2D.CanonicalMasks(PipeType2D.Tee);
        Require(cornerMasks.Count >= 2 && teeMasks.Count >= 1, "need at least 2 Corner masks and 1 Tee mask for this test");

        // Build the SAME logical state twice, from entirely independent
        // objects/arrays - the second build's domain Lists are populated in
        // a deliberately different internal order (same SET, different
        // List sequence) to confirm the key depends on the set of masks
        // present, never on incidental List ordering or object identity.
        var typeByIndex1 = new PipeType2D[] { PipeType2D.Corner, PipeType2D.Tee, PipeType2D.Corner };
        var cellsOrder1 = new Vector2Int[] { new Vector2Int(2, 5), new Vector2Int(-1, 0), new Vector2Int(0, 0) };
        var domains1 = new List<int>[]
        {
            new List<int> { cornerMasks[0], cornerMasks[1] },
            new List<int> { teeMasks[0] },
            new List<int> { cornerMasks[1] }
        };

        var typeByIndex2 = new PipeType2D[] { PipeType2D.Corner, PipeType2D.Tee, PipeType2D.Corner };
        var cellsOrder2 = new Vector2Int[] { new Vector2Int(2, 5), new Vector2Int(-1, 0), new Vector2Int(0, 0) };
        var domains2 = new List<int>[]
        {
            new List<int> { cornerMasks[1], cornerMasks[0] },
            new List<int> { teeMasks[0] },
            new List<int> { cornerMasks[1] }
        };

        int[] stableRank1 = CampaignUniquenessSolver2D.ComputeStableCoordinateOrder(cellsOrder1, 3);
        int[] stableRank2 = CampaignUniquenessSolver2D.ComputeStableCoordinateOrder(cellsOrder2, 3);

        bool ok1 = CampaignUniquenessSolver2D.TryBuildMemoKey(domains1, typeByIndex1, stableRank1, 3, true, out var key1);
        bool ok2 = CampaignUniquenessSolver2D.TryBuildMemoKey(domains2, typeByIndex2, stableRank2, 3, true, out var key2);

        Require(ok1 && ok2, "TryBuildMemoKey should succeed for 3 variables");
        Require(key1.Equals(key2), "the same logical state built from independent objects (with domain lists in a different internal order) must produce an identical key");
    }

    private static void Case11_MemoizedVsNonMemoizedAgreement()
    {
        // Single forced Corner (same geometry as Case1).
        CompareMemoizedVsNonMemoized(
            new Vector2Int(0, 0), Direction2D.Right, new Vector2Int(1, -1), Direction2D.Up,
            new GridBounds2D(4, 4),
            new List<PipeSpawnData2D> { new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 0), 0, 2) },
            "single forced Corner");

        // Real production Levels 4-6 - already hand-verified solvable data.
        List<YagmurRotasi2D.Data2D.LevelData2D> levels = LevelManager2D.BuildLevels();
        for (int levelNumber = 4; levelNumber <= 6; levelNumber++)
        {
            YagmurRotasi2D.Data2D.LevelData2D data = levels[levelNumber - 1];
            CompareMemoizedVsNonMemoized(
                data.sourcePos, data.sourceOutputDirection, data.targetPos, data.targetInputDirection,
                new GridBounds2D(data.gridWidth, data.gridHeight), new List<PipeSpawnData2D>(data.pipes),
                $"production Level {levelNumber}");
        }
    }

    private static void CompareMemoizedVsNonMemoized(
        Vector2Int source, Direction2D sourceDir, Vector2Int target, Direction2D targetDir,
        GridBounds2D bounds, List<PipeSpawnData2D> pipes, string label)
    {
        var memoizedOutcome = CampaignUniquenessSolver2D.CountSolutions(
            bounds, source, sourceDir, target, targetDir, pipes, 200000, out int foundMemoized, 2000, null, enableMemoization: true);
        var nonMemoizedOutcome = CampaignUniquenessSolver2D.CountSolutions(
            bounds, source, sourceDir, target, targetDir, pipes, 200000, out int foundNonMemoized, 2000, null, enableMemoization: false);

        Require(memoizedOutcome == nonMemoizedOutcome, $"[{label}] memoized outcome {memoizedOutcome} disagrees with non-memoized outcome {nonMemoizedOutcome}");
        Require(foundMemoized == foundNonMemoized, $"[{label}] memoized solutionsFound {foundMemoized} disagrees with non-memoized {foundNonMemoized}");
    }

    // ---------------- Independent brute-force reference ----------------

    /// <summary>
    /// Deliberately naive: enumerate every canonical-mask combination (NOT
    /// raw rotationIndex - already Straight-symmetry-collapsed via
    /// CampaignPipeOrientationCatalog2D, same as the optimized solver, so
    /// this comparison is apples-to-apples) via plain nested recursion, no
    /// pruning, no propagation, validating each complete combination with
    /// the real FlowSolver2D. Correct by construction/inspection rather
    /// than by cleverness - this is the independent ground truth the
    /// optimized solver is cross-checked against for fixtures too dense to
    /// hand-verify. Caps at <paramref name="capAt"/> found solutions purely
    /// to bound runtime; fixtures here are small enough (<=7 pipes) that an
    /// uncapped run would also be fast, but the cap keeps this consistent
    /// with how the optimized solver itself is bounded.
    /// </summary>
    private static int BruteForceCanonicalCount(
        GridBounds2D bounds, Vector2Int sourceCell, Direction2D sourceOutputDirection, Vector2Int targetCell, Direction2D targetEntryDirection,
        List<PipeSpawnData2D> pipes, int capAt)
    {
        var spawned = new List<GameObject>();
        int found = 0;
        try
        {
            // BUG HISTORY (Phase 8A.3.1): CreateBoard() alone leaves the
            // board at its default 5x5 - a fixture using ANY bounds other
            // than 5x5 (e.g. Case 02's 6x6) would have every pipe outside
            // that default range rejected by IsInsideGrid, throwing from
            // CreatePipe's own Require() check exactly as reported. Must
            // use the SAME bounds the fast solver (CountSolutions) receives -
            // never let the two silently disagree on board size.
            BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
            board.SetGridSize(bounds.Width, bounds.Height);
            FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);
            var instances = new PipeTile2D[pipes.Count];
            var maskLists = new List<int>[pipes.Count];
            for (int i = 0; i < pipes.Count; i++)
            {
                instances[i] = BranchingSolverTestRunner.CreatePipe(board, spawned, pipes[i].pipeType, pipes[i].gridPos, 0);
                maskLists[i] = new List<int>(CampaignPipeOrientationCatalog2D.CanonicalMasks(pipes[i].pipeType));
            }

            void Recurse(int index)
            {
                if (found >= capAt) return;
                if (index == pipes.Count)
                {
                    FlowSolveResult2D result = solver.Solve(sourceCell, sourceOutputDirection, targetCell, targetEntryDirection);
                    if (result.IsSuccess) found++;
                    return;
                }
                foreach (int mask in maskLists[index])
                {
                    if (found >= capAt) return;
                    int rotation = CampaignPipeOrientationCatalog2D.RotationForMask(pipes[index].pipeType, mask);
                    instances[index].Initialize(pipes[index].pipeType, rotation, pipes[index].gridPos);
                    Recurse(index + 1);
                }
            }

            Recurse(0);
        }
        finally
        {
            foreach (GameObject go in spawned) { if (go != null) Object.DestroyImmediate(go); }
        }
        return found;
    }

    private static int[] FindAnyValidRotationAssignment(
        GridBounds2D bounds, Vector2Int sourceCell, Direction2D sourceOutputDirection, Vector2Int targetCell, Direction2D targetEntryDirection,
        List<PipeSpawnData2D> pipes)
    {
        var spawned = new List<GameObject>();
        int[] resultRotations = null;
        try
        {
            BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
            board.SetGridSize(bounds.Width, bounds.Height);
            FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);
            var instances = new PipeTile2D[pipes.Count];
            var maskLists = new List<int>[pipes.Count];
            var currentRotations = new int[pipes.Count];
            for (int i = 0; i < pipes.Count; i++)
            {
                instances[i] = BranchingSolverTestRunner.CreatePipe(board, spawned, pipes[i].pipeType, pipes[i].gridPos, 0);
                maskLists[i] = new List<int>(CampaignPipeOrientationCatalog2D.CanonicalMasks(pipes[i].pipeType));
            }

            bool Recurse(int index)
            {
                if (index == pipes.Count)
                {
                    FlowSolveResult2D result = solver.Solve(sourceCell, sourceOutputDirection, targetCell, targetEntryDirection);
                    return result.IsSuccess;
                }
                foreach (int mask in maskLists[index])
                {
                    int rotation = CampaignPipeOrientationCatalog2D.RotationForMask(pipes[index].pipeType, mask);
                    instances[index].Initialize(pipes[index].pipeType, rotation, pipes[index].gridPos);
                    currentRotations[index] = rotation;
                    if (Recurse(index + 1)) return true;
                }
                return false;
            }

            if (Recurse(0))
            {
                resultRotations = (int[])currentRotations.Clone();
            }
        }
        finally
        {
            foreach (GameObject go in spawned) { if (go != null) Object.DestroyImmediate(go); }
        }
        return resultRotations;
    }
}
