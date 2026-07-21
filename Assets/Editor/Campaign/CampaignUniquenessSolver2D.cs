using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Editor-only exact uniqueness solver (Phase 8A.3 rewrite of the Phase 8A
/// brute-force version) - counts, up to 2, how many rotation assignments of
/// a level's already-placed pipes (position/type fixed - only orientation is
/// searched) make it solvable, using a real CSP engine instead of raw
/// 4^N-ish brute force: canonical orientation domains (Part B), the known
/// solvedRotationIndex assignment used as a given first solution so the
/// search only ever looks for a DIFFERING second one (Part C/D), AC-3
/// reciprocal-arc propagation (Part F), MRV variable ordering (Part G),
/// trail-based incremental rollback instead of dictionary cloning (Part H),
/// safe possible-connectivity pruning (Part I), and bounded per-candidate
/// state memoization (Part K). The real, unchanged FlowSolver2D remains the
/// sole authority for "is this complete assignment actually a valid route" -
/// every differing complete assignment the CSP search reaches is still
/// confirmed through it before being counted as a second solution; nothing
/// here approximates or replaces that check, only how few assignments ever
/// need to reach it.
/// </summary>
public static class CampaignUniquenessSolver2D
{
    public enum UniquenessOutcome
    {
        Zero,
        One,
        TwoOrMore,
        Inconclusive
    }

    /// <summary>Part A/O: aggregated stats for one CountSolutions() call, for the benchmark command and any future diagnostics.</summary>
    public sealed class UniquenessSearchDiagnostics
    {
        public int ActivePipeCount;
        public int InitialDomainSizeSum;
        public int RemovedByBoardEdge;
        public int RemovedByEmptyNeighbor;
        public int RemovedBySourceConnection;
        public int RemovedByTargetConnection;
        public int VariablesForcedBeforeSearch;
        public int NodesVisited;
        public int PropagationOperations;
        public int Backtracks;
        public int MaxRecursionDepth;
        public int SecondSolutionNode = -1;
        public long ElapsedMilliseconds;
        public string TerminationReason;
        public int NodeBudget;
        public int MemoCacheHits;
    }

    /// <summary>
    /// Part N: a lightweight, CHEAP pre-search ambiguity signal - never a
    /// substitute for the exact search, never used to gate/reject a
    /// candidate on its own. Computed from static pruning only (no AC-3, no
    /// search) so it is safe to call on every candidate before deciding
    /// whether the expensive exact check is worth running. Higher numbers
    /// mean "more orientation freedom left after the cheap checks" - a
    /// rough proxy for how likely an exact search is to be slow or non-
    /// unique, nothing more.
    /// </summary>
    public sealed class AmbiguityMetric
    {
        public int PipesWithFullDomainAfterStaticPruning;
        public float AverageInitialDomainSize;
    }

    /// <summary>See AmbiguityMetric - Part N. Purely informational: callers may use it to prioritize or log, but must never treat it as proof of (non-)uniqueness.</summary>
    public static AmbiguityMetric ComputeAmbiguityMetric(
        GridBounds2D bounds, Vector2Int sourceCell, Direction2D sourceOutputDirection,
        Vector2Int targetCell, Direction2D targetEntryDirection, IReadOnlyList<PipeSpawnData2D> pipes)
    {
        int n = pipes.Count;
        var cellsOrder = new Vector2Int[n];
        var indexByCell = new Dictionary<Vector2Int, int>();
        var domains = new List<int>[n];
        for (int i = 0; i < n; i++)
        {
            cellsOrder[i] = pipes[i].gridPos;
            indexByCell[cellsOrder[i]] = i;
            domains[i] = new List<int>(CampaignPipeOrientationCatalog2D.CanonicalMasks(pipes[i].pipeType));
        }

        Vector2Int firstHopCell = sourceCell + sourceOutputDirection.ToVector();
        Vector2Int lastHopCell = targetCell + targetEntryDirection.ToVector();

        for (int i = 0; i < n; i++)
        {
            foreach (Direction2D dir in AllDirections)
            {
                Vector2Int neighborCell = cellsOrder[i] + dir.ToVector();
                if (indexByCell.ContainsKey(neighborCell)) continue;
                if (!bounds.Contains(neighborCell) ||
                    (neighborCell != sourceCell && neighborCell != targetCell))
                {
                    RemoveDirection(domains[i], dir);
                }
            }
        }

        if (indexByCell.TryGetValue(firstHopCell, out int sourceAdjacentIndex))
        {
            Direction2D required = sourceOutputDirection.Opposite();
            domains[sourceAdjacentIndex].RemoveAll(m => !CampaignPipeOrientationCatalog2D.HasDirection(m, required));
        }
        if (indexByCell.TryGetValue(lastHopCell, out int targetAdjacentIndex))
        {
            Direction2D required = targetEntryDirection.Opposite();
            domains[targetAdjacentIndex].RemoveAll(m => !CampaignPipeOrientationCatalog2D.HasDirection(m, required));
        }

        var metric = new AmbiguityMetric();
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            sum += domains[i].Count;
            if (domains[i].Count >= 4) metric.PipesWithFullDomainAfterStaticPruning++;
        }
        metric.AverageInitialDomainSize = n > 0 ? sum / (float)n : 0f;
        return metric;
    }

    private const int DefaultMaxSearchNodes = 200000;
    private const int DefaultMaxElapsedMilliseconds = 2000;
    private const int MaxMemoCacheEntries = 200000;

    private static readonly Direction2D[] AllDirections = { Direction2D.Up, Direction2D.Right, Direction2D.Down, Direction2D.Left };

    private enum SearchStatus { Continue, Found, BudgetExceeded }

    /// <summary>
    /// <paramref name="maxSearchNodes"/> is a DFS node-visit budget (Phase
    /// 8A.3 Part L) - NOT the old "complete assignments checked" leaf
    /// counter. Existing call sites that pass their old
    /// MaxUniquenessAssignments-style constant still work unchanged (same
    /// parameter slot), just with the corrected meaning documented here.
    /// </summary>
    public static UniquenessOutcome CountSolutions(
        GridBounds2D bounds,
        Vector2Int sourceCell,
        Direction2D sourceOutputDirection,
        Vector2Int targetCell,
        Direction2D targetEntryDirection,
        IReadOnlyList<PipeSpawnData2D> pipesInSearchOrder,
        int maxSearchNodes,
        out int solutionsFound,
        int maxElapsedMilliseconds = DefaultMaxElapsedMilliseconds,
        UniquenessSearchDiagnostics diagnosticsOut = null,
        bool enableMemoization = true)
    {
        int n = pipesInSearchOrder.Count;
        var diag = diagnosticsOut ?? new UniquenessSearchDiagnostics();
        diag.ActivePipeCount = n;
        diag.NodeBudget = maxSearchNodes;

        var stopwatch = Stopwatch.StartNew();
        solutionsFound = 0;

        // ---- Build canonical initial domains + adjacency (Part B/E) ----
        var cellsOrder = new Vector2Int[n];
        var typeByIndex = new PipeType2D[n];
        var indexByCell = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < n; i++)
        {
            cellsOrder[i] = pipesInSearchOrder[i].gridPos;
            typeByIndex[i] = pipesInSearchOrder[i].pipeType;
            indexByCell[cellsOrder[i]] = i;
        }

        var domains = new List<int>[n];
        for (int i = 0; i < n; i++)
        {
            domains[i] = new List<int>(CampaignPipeOrientationCatalog2D.CanonicalMasks(typeByIndex[i]));
        }

        Vector2Int firstHopCell = sourceCell + sourceOutputDirection.ToVector();
        Vector2Int lastHopCell = targetCell + targetEntryDirection.ToVector();

        if (!indexByCell.TryGetValue(firstHopCell, out int sourceAdjacentIndex) ||
            !indexByCell.TryGetValue(lastHopCell, out int targetAdjacentIndex))
        {
            // Should be unreachable - the caller already validated the solved
            // layout with FlowSolver2D, which requires these exact cells to
            // hold real pipes. Defensive Zero rather than a crash.
            diag.TerminationReason = "Source/Target adjacent cell not found among placed pipes (should be unreachable)";
            return UniquenessOutcome.Zero;
        }

        var adjacency = new List<(int neighborIndex, Direction2D dir)>[n];
        for (int i = 0; i < n; i++)
        {
            adjacency[i] = new List<(int, Direction2D)>();
        }

        for (int i = 0; i < n; i++)
        {
            foreach (Direction2D dir in AllDirections)
            {
                Vector2Int neighborCell = cellsOrder[i] + dir.ToVector();

                if (!bounds.Contains(neighborCell))
                {
                    int removed = RemoveDirection(domains[i], dir);
                    diag.RemovedByBoardEdge += removed;
                    continue;
                }

                if (indexByCell.TryGetValue(neighborCell, out int neighborIndex))
                {
                    adjacency[i].Add((neighborIndex, dir));
                    continue;
                }

                bool isSourceSide = i == sourceAdjacentIndex && neighborCell == sourceCell;
                bool isTargetSide = i == targetAdjacentIndex && neighborCell == targetCell;
                if (isSourceSide || isTargetSide)
                {
                    continue;
                }

                int removedEmpty = RemoveDirection(domains[i], dir);
                diag.RemovedByEmptyNeighbor += removedEmpty;
            }
        }

        Direction2D requiredSourceOpening = sourceOutputDirection.Opposite();
        int beforeSource = domains[sourceAdjacentIndex].Count;
        domains[sourceAdjacentIndex].RemoveAll(m => !CampaignPipeOrientationCatalog2D.HasDirection(m, requiredSourceOpening));
        diag.RemovedBySourceConnection += beforeSource - domains[sourceAdjacentIndex].Count;

        Direction2D requiredTargetOpening = targetEntryDirection.Opposite();
        int beforeTarget = domains[targetAdjacentIndex].Count;
        domains[targetAdjacentIndex].RemoveAll(m => !CampaignPipeOrientationCatalog2D.HasDirection(m, requiredTargetOpening));
        diag.RemovedByTargetConnection += beforeTarget - domains[targetAdjacentIndex].Count;

        for (int i = 0; i < n; i++)
        {
            diag.InitialDomainSizeSum += domains[i].Count;
        }

        // ---- Full-fixpoint AC-3 before search starts (Part F) ----
        bool consistent = InitialPropagate(domains, adjacency, n, diag);
        if (!consistent)
        {
            diag.TerminationReason = "initial propagation found a contradiction (should be unreachable - solved layout already validated)";
            return UniquenessOutcome.Zero;
        }

        for (int i = 0; i < n; i++)
        {
            if (domains[i].Count == 1)
            {
                diag.VariablesForcedBeforeSearch++;
            }
        }

        // ---- Known solution as the given first solution (Part C) ----
        var knownMask = new int[n];
        for (int i = 0; i < n; i++)
        {
            knownMask[i] = CampaignPipeOrientationCatalog2D.ToMask(ProbeOpenDirections(typeByIndex[i], pipesInSearchOrder[i].solvedRotationIndex));
        }

        var spawned = new List<GameObject>();
        UniquenessOutcome outcome;
        try
        {
            // BUG HISTORY (Phase 8A.3.1): CreateBoard() alone leaves the
            // board at its default 5x5 - without this, any level whose
            // bounds are not 5x5 (the entire 6x6-10x10 range, including
            // Levels 11-20) would have every pipe outside that default
            // range rejected by BoardManager2D.IsInsideGrid, and
            // BranchingSolverTestRunner.CreatePipe's own Require() check
            // would throw. Must match the SAME bounds passed into this
            // method, not whatever CreateBoard defaults to.
            BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
            board.SetGridSize(bounds.Width, bounds.Height);
            FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);
            var pipeInstances = new PipeTile2D[n];
            for (int i = 0; i < n; i++)
            {
                int rotation = CampaignPipeOrientationCatalog2D.RotationForMask(typeByIndex[i], knownMask[i]);
                pipeInstances[i] = BranchingSolverTestRunner.CreatePipe(board, spawned, typeByIndex[i], cellsOrder[i], rotation);
            }

            FlowSolveResult2D knownResult = solver.Solve(sourceCell, sourceOutputDirection, targetCell, targetEntryDirection);
            if (!knownResult.IsSuccess)
            {
                diag.TerminationReason = "known solvedRotationIndex assignment failed FlowSolver2D validation (should be unreachable - caller already validated it)";
                solutionsFound = 0;
                return UniquenessOutcome.Zero;
            }

            solutionsFound = 1;

            // ---- Search for a genuinely differing second solution (Part D) ----
            var trail = new List<(int index, int mask)>();
            var assigned = new bool[n];
            var memoCache = new HashSet<MemoKey>();
            int[] stableRankOfSearchIndex = ComputeStableCoordinateOrder(cellsOrder, n);
            if (enableMemoization && n > MemoKey.MaxSupportedVariables)
            {
                UnityEngine.Debug.LogWarning($"CampaignUniquenessSolver2D: {n} pipes exceeds the exact memoization key's supported {MemoKey.MaxSupportedVariables} variables - memoization disabled for this candidate (correctness over speed); the search still runs, just without the memo cache's speedup.");
            }
            int nodesVisited = 0;
            int backtracks = 0;
            int propagationOps = 0;
            int maxDepth = 0;
            // Any domain already forced to size 1 by initial pruning/AC-3
            // (Cross, Source/Target-adjacent pipes, etc.) must be marked
            // assigned here too - not just counted - so assignedCount and
            // the assigned[] flags stay consistent. Getting this wrong
            // double-counts those variables once SelectNextVariable visits
            // them later (MRV always picks them first, since their domain
            // is smallest), making assignedCount reach n before every
            // domain is actually resolved.
            int assignedCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (domains[i].Count == 1)
                {
                    assigned[i] = true;
                    assignedCount++;
                }
            }

            SearchStatus status = Search(
                domains, adjacency, n, trail, assigned, assignedCount, 0, false,
                knownMask, sourceAdjacentIndex, targetAdjacentIndex,
                typeByIndex, cellsOrder, pipeInstances, solver, sourceCell, sourceOutputDirection, targetCell, targetEntryDirection,
                maxSearchNodes, maxElapsedMilliseconds, stopwatch,
                enableMemoization, stableRankOfSearchIndex, memoCache,
                ref nodesVisited, ref backtracks, ref propagationOps, ref maxDepth, diag);

            diag.NodesVisited = nodesVisited;
            diag.Backtracks = backtracks;
            diag.PropagationOperations = propagationOps;
            diag.MaxRecursionDepth = maxDepth;

            if (status == SearchStatus.Found)
            {
                solutionsFound = 2;
                outcome = UniquenessOutcome.TwoOrMore;
                diag.TerminationReason = "second differing solution found";
            }
            else if (status == SearchStatus.BudgetExceeded)
            {
                outcome = UniquenessOutcome.Inconclusive;
                if (diag.TerminationReason == null)
                {
                    diag.TerminationReason = "node/time budget exceeded before an exhaustive proof was reached";
                }
            }
            else
            {
                outcome = UniquenessOutcome.One;
                diag.TerminationReason = "exhaustive search completed - no differing valid assignment exists";
            }
        }
        finally
        {
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        diag.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        return outcome;
    }

    private static Direction2D[] ProbeOpenDirections(PipeType2D type, int rotationIndex)
    {
        var go = new GameObject("CampaignUniquenessKnownSolutionProbe");
        try
        {
            var probe = go.AddComponent<PipeTile2D>();
            probe.Initialize(type, rotationIndex, Vector2Int.zero);
            return probe.GetOpenDirections();
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ---------------- AC-3 propagation (Part F) ----------------

    private static int RemoveDirection(List<int> domain, Direction2D dir)
    {
        int removed = 0;
        for (int i = domain.Count - 1; i >= 0; i--)
        {
            if (CampaignPipeOrientationCatalog2D.HasDirection(domain[i], dir))
            {
                domain.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

    private static bool Revise(List<int>[] domains, int a, int b, Direction2D dirAtoB, List<(int index, int mask)> trail)
    {
        Direction2D dirBtoA = dirAtoB.Opposite();
        bool changed = false;
        for (int i = domains[a].Count - 1; i >= 0; i--)
        {
            int maskA = domains[a][i];
            bool aOpen = CampaignPipeOrientationCatalog2D.HasDirection(maskA, dirAtoB);
            bool supported = false;
            foreach (int maskB in domains[b])
            {
                if (CampaignPipeOrientationCatalog2D.HasDirection(maskB, dirBtoA) == aOpen)
                {
                    supported = true;
                    break;
                }
            }
            if (!supported)
            {
                domains[a].RemoveAt(i);
                trail?.Add((a, maskA));
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>One-time full-fixpoint AC-3 before search begins - permanent, no trail/rollback needed since it never gets undone.</summary>
    private static bool InitialPropagate(List<int>[] domains, List<(int neighborIndex, Direction2D dir)>[] adjacency, int n, UniquenessSearchDiagnostics diag)
    {
        var queue = new Queue<int>();
        var queued = new bool[n];
        for (int i = 0; i < n; i++)
        {
            queue.Enqueue(i);
            queued[i] = true;
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            queued[current] = false;

            foreach ((int neighborIndex, Direction2D dir) in adjacency[current])
            {
                diag.PropagationOperations++;
                bool changed = Revise(domains, neighborIndex, current, dir.Opposite(), null);
                if (domains[neighborIndex].Count == 0)
                {
                    return false;
                }
                if (changed && !queued[neighborIndex])
                {
                    queued[neighborIndex] = true;
                    queue.Enqueue(neighborIndex);
                }
            }
        }

        return true;
    }

    private static bool PropagateFrom(List<int>[] domains, List<(int neighborIndex, Direction2D dir)>[] adjacency, int start, List<(int, int)> trail, ref int propagationOps)
    {
        var queue = new Queue<int>();
        var queued = new HashSet<int> { start };
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            queued.Remove(current);

            foreach ((int neighborIndex, Direction2D dir) in adjacency[current])
            {
                propagationOps++;
                bool changed = Revise(domains, neighborIndex, current, dir.Opposite(), trail);
                if (domains[neighborIndex].Count == 0)
                {
                    return false;
                }
                if (changed && queued.Add(neighborIndex))
                {
                    queue.Enqueue(neighborIndex);
                }
            }
        }

        return true;
    }

    private static void Undo(List<int>[] domains, List<(int index, int mask)> trail, int checkpoint)
    {
        for (int i = trail.Count - 1; i >= checkpoint; i--)
        {
            domains[trail[i].index].Add(trail[i].mask);
        }
        trail.RemoveRange(checkpoint, trail.Count - checkpoint);
    }

    // ---------------- Connectivity pruning (Part I) ----------------

    private static bool PossiblyConnected(List<int> domainA, List<int> domainB, Direction2D dirAtoB)
    {
        Direction2D dirBtoA = dirAtoB.Opposite();
        foreach (int maskA in domainA)
        {
            if (!CampaignPipeOrientationCatalog2D.HasDirection(maskA, dirAtoB)) continue;
            foreach (int maskB in domainB)
            {
                if (CampaignPipeOrientationCatalog2D.HasDirection(maskB, dirBtoA)) return true;
            }
        }
        return false;
    }

    private static bool ConnectivityStillPossible(List<int>[] domains, List<(int neighborIndex, Direction2D dir)>[] adjacency, int n, int sourceAdjacentIndex, int targetAdjacentIndex)
    {
        var visited = new bool[n];
        var queue = new Queue<int>();
        visited[sourceAdjacentIndex] = true;
        queue.Enqueue(sourceAdjacentIndex);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach ((int neighborIndex, Direction2D dir) in adjacency[current])
            {
                if (visited[neighborIndex]) continue;
                if (PossiblyConnected(domains[current], domains[neighborIndex], dir))
                {
                    visited[neighborIndex] = true;
                    queue.Enqueue(neighborIndex);
                }
            }
        }

        if (!visited[targetAdjacentIndex])
        {
            return false;
        }

        for (int i = 0; i < n; i++)
        {
            if (!visited[i]) return false;
        }

        return true;
    }

    // ---------------- MRV selection (Part G) ----------------

    private static int SelectNextVariable(List<int>[] domains, bool[] assigned, List<(int neighborIndex, Direction2D dir)>[] adjacency, Vector2Int[] cellsOrder, int n)
    {
        int best = -1;
        for (int i = 0; i < n; i++)
        {
            if (assigned[i]) continue;
            if (best == -1) { best = i; continue; }

            if (domains[i].Count != domains[best].Count)
            {
                if (domains[i].Count < domains[best].Count) best = i;
                continue;
            }

            if (adjacency[i].Count != adjacency[best].Count)
            {
                if (adjacency[i].Count > adjacency[best].Count) best = i;
                continue;
            }

            Vector2Int a = cellsOrder[i];
            Vector2Int b = cellsOrder[best];
            if (a.x != b.x ? a.x < b.x : a.y < b.y)
            {
                best = i;
            }
        }
        return best;
    }

    // ---------------- Core recursive search (Parts C/D/H/K) ----------------

    private static SearchStatus Search(
        List<int>[] domains, List<(int neighborIndex, Direction2D dir)>[] adjacency, int n,
        List<(int index, int mask)> trail, bool[] assigned, int assignedCount, int depth, bool differsFromKnown,
        int[] knownMask, int sourceAdjacentIndex, int targetAdjacentIndex,
        PipeType2D[] typeByIndex, Vector2Int[] cellsOrder, PipeTile2D[] pipeInstances, FlowSolver2D solver,
        Vector2Int sourceCell, Direction2D sourceOutputDirection, Vector2Int targetCell, Direction2D targetEntryDirection,
        int maxSearchNodes, int maxElapsedMilliseconds, Stopwatch stopwatch,
        bool enableMemoization, int[] stableRankOfSearchIndex, HashSet<MemoKey> memoCache,
        ref int nodesVisited, ref int backtracks, ref int propagationOps, ref int maxDepth, UniquenessSearchDiagnostics diag)
    {
        if (depth > maxDepth) maxDepth = depth;

        if (nodesVisited >= maxSearchNodes || stopwatch.ElapsedMilliseconds >= maxElapsedMilliseconds)
        {
            return SearchStatus.BudgetExceeded;
        }

        if (assignedCount == n)
        {
            if (!differsFromKnown)
            {
                return SearchStatus.Continue;
            }

            nodesVisited++;
            for (int i = 0; i < n; i++)
            {
                int rotation = CampaignPipeOrientationCatalog2D.RotationForMask(typeByIndex[i], domains[i][0]);
                pipeInstances[i].Initialize(typeByIndex[i], rotation, cellsOrder[i]);
            }
            FlowSolveResult2D result = solver.Solve(sourceCell, sourceOutputDirection, targetCell, targetEntryDirection);
            if (result.IsSuccess)
            {
                diag.SecondSolutionNode = nodesVisited;
                return SearchStatus.Found;
            }
            return SearchStatus.Continue;
        }

        MemoKey stateKey = default;
        bool haveKey = false;
        if (enableMemoization)
        {
            haveKey = TryBuildMemoKey(domains, typeByIndex, stableRankOfSearchIndex, n, differsFromKnown, out stateKey);
        }
        if (haveKey && memoCache.Contains(stateKey))
        {
            diag.MemoCacheHits++;
            return SearchStatus.Continue;
        }

        int pipeIndex = SelectNextVariable(domains, assigned, adjacency, cellsOrder, n);
        assigned[pipeIndex] = true;

        List<int> orderedMasks = OrderValuesPreferringNonKnown(domains[pipeIndex], knownMask[pipeIndex]);

        foreach (int mask in orderedMasks)
        {
            nodesVisited++;
            if (nodesVisited >= maxSearchNodes || stopwatch.ElapsedMilliseconds >= maxElapsedMilliseconds)
            {
                assigned[pipeIndex] = false;
                return SearchStatus.BudgetExceeded;
            }

            int checkpoint = trail.Count;
            AssignForced(domains, pipeIndex, mask, trail);
            bool consistent = PropagateFrom(domains, adjacency, pipeIndex, trail, ref propagationOps);
            if (consistent)
            {
                consistent = ConnectivityStillPossible(domains, adjacency, n, sourceAdjacentIndex, targetAdjacentIndex);
            }

            if (consistent)
            {
                bool nowDiffers = differsFromKnown || mask != knownMask[pipeIndex];
                SearchStatus result = Search(
                    domains, adjacency, n, trail, assigned, assignedCount + 1, depth + 1, nowDiffers,
                    knownMask, sourceAdjacentIndex, targetAdjacentIndex,
                    typeByIndex, cellsOrder, pipeInstances, solver, sourceCell, sourceOutputDirection, targetCell, targetEntryDirection,
                    maxSearchNodes, maxElapsedMilliseconds, stopwatch,
                    enableMemoization, stableRankOfSearchIndex, memoCache,
                    ref nodesVisited, ref backtracks, ref propagationOps, ref maxDepth, diag);

                if (result != SearchStatus.Continue)
                {
                    Undo(domains, trail, checkpoint);
                    assigned[pipeIndex] = false;
                    return result;
                }
            }

            Undo(domains, trail, checkpoint);
            backtracks++;
        }

        assigned[pipeIndex] = false;

        // Every value of pipeIndex was tried and none led to a differing
        // solution (or every such attempt was itself proven empty) - this
        // exact (domains, differsFromKnown) state is now proven to contain
        // no second solution anywhere in its subtree, regardless of which
        // differs value it carries; safe to cache symmetrically for both.
        if (haveKey && memoCache.Count < MaxMemoCacheEntries)
        {
            memoCache.Add(stateKey);
        }

        return SearchStatus.Continue;
    }

    private static void AssignForced(List<int>[] domains, int pipeIndex, int mask, List<(int index, int mask)> trail)
    {
        for (int i = domains[pipeIndex].Count - 1; i >= 0; i--)
        {
            int m = domains[pipeIndex][i];
            if (m != mask)
            {
                domains[pipeIndex].RemoveAt(i);
                trail.Add((pipeIndex, m));
            }
        }
    }

    /// <summary>Part D: try masks OTHER than the known solved orientation first, so a genuinely differing second solution is found as fast as possible when one exists. Deterministic - ascending mask value, known mask always last.</summary>
    private static List<int> OrderValuesPreferringNonKnown(List<int> domain, int knownMaskForThisPipe)
    {
        var ordered = new List<int>(domain.Count);
        var sorted = new List<int>(domain);
        sorted.Sort();
        foreach (int m in sorted)
        {
            if (m != knownMaskForThisPipe) ordered.Add(m);
        }
        if (sorted.Contains(knownMaskForThisPipe)) ordered.Add(knownMaskForThisPipe);
        return ordered;
    }

    // ---------------- Exact state memoization (Phase 8A.4 Part K rewrite) ----------------

    /// <summary>
    /// BUG HISTORY (Phase 8A.4): the original memoization key was a folded
    /// 64-bit FNV-style hash of the raw per-pipe port-mask domains, combined
    /// with the search-discovery index (not a stable grid-coordinate index)
    /// and gated entirely off differsFromKnownSolution (differs=false states
    /// were simply never cached, rather than the flag being part of the
    /// key). That hash was a genuine many-to-one mapping from an
    /// exponentially larger state space down to 2^64 values - a collision,
    /// however astronomically unlikely on any single run, would make the
    /// search wrongly treat a genuinely different, unexplored state as
    /// already-proven-empty, which could silently produce a false "exactly
    /// one solution" verdict for a level that is actually NOT unique. For a
    /// decision that gates what ships in the campaign, "extremely unlikely"
    /// is not an acceptable bar - MemoKey below is instead an EXACT,
    /// injective packed encoding (never a lossy hash of the state), so two
    /// different states are STRUCTURALLY GUARANTEED to produce different
    /// keys, not just "very probably" different ones.
    ///
    /// Encoding: each pipe has at most
    /// CampaignPipeOrientationCatalog2D.MaxCanonicalMasksPerType (4)
    /// possible canonical masks, so its remaining domain is exactly
    /// representable as a 4-bit "which of my own up-to-4 masks are still
    /// possible" subset (a per-TYPE-relative canonical index, never the raw
    /// port-mask value, which would need up to 16 bits for no reason since
    /// at most 4 of those 16 raw values are ever legal for a given type).
    /// Variables are packed in a FIXED, stable grid-coordinate order (sorted
    /// by x then y, computed once before search) - not search-discovery
    /// order - so which physical cell contributed which 4 bits never
    /// depends on anything that varies between candidates or between
    /// runs. differsFromKnownSolution is one additional explicit bit,
    /// always included, so a differs=false state and an otherwise-identical
    /// differs=true state are two different keys by construction, and both
    /// participate in memoization symmetrically (the old asymmetric gate
    /// that only ever cached differs=true states is gone).
    ///
    /// Four ulong words (256 bits) support up to 64 variables - comfortably
    /// covers every Phase 8A pilot candidate (max 15) with large headroom
    /// for Phase 8B's larger boards; MaxSupportedVariables documents the
    /// current hard limit explicitly rather than letting a future larger
    /// level silently overflow the packed encoding.
    /// </summary>
    /// <summary>Internal (not private) so CampaignUniquenessSolverTests2D (Phase 8A.4) can directly unit-test key construction, instead of only testing it indirectly through CountSolutions' behavior.</summary>
    internal readonly struct MemoKey : System.IEquatable<MemoKey>
    {
        public const int BitsPerVariable = 4;
        public const int VariablesPerWord = 64 / BitsPerVariable; // 16
        public const int WordCount = 4;
        public const int MaxSupportedVariables = VariablesPerWord * WordCount; // 64

        private readonly ulong word0, word1, word2, word3;
        private readonly bool differs;

        public MemoKey(ulong word0, ulong word1, ulong word2, ulong word3, bool differs)
        {
            this.word0 = word0;
            this.word1 = word1;
            this.word2 = word2;
            this.word3 = word3;
            this.differs = differs;
        }

        public bool Equals(MemoKey other)
        {
            return word0 == other.word0 && word1 == other.word1 && word2 == other.word2 && word3 == other.word3 && differs == other.differs;
        }

        public override bool Equals(object obj) => obj is MemoKey other && Equals(other);

        /// <summary>Only used for hash-BUCKET placement inside the HashSet - correctness never depends on this being collision-free, only Equals (an exact field comparison above) does. A collision here costs a little performance (an extra Equals check within the bucket), never correctness.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                long h = word0.GetHashCode();
                h = h * 31 + word1.GetHashCode();
                h = h * 31 + word2.GetHashCode();
                h = h * 31 + word3.GetHashCode();
                h = h * 31 + (differs ? 1 : 0);
                return (int)h;
            }
        }
    }

    /// <summary>Stable grid-coordinate order (sorted by x then y) for the n search-indices 0..n-1 - computed once before search begins, used ONLY to decide which 4-bit slot each variable's domain packs into for MemoKey, never for search/MRV itself (which keeps using its own dynamic ordering). Internal (not private) for direct unit testing.</summary>
    internal static int[] ComputeStableCoordinateOrder(Vector2Int[] cellsOrder, int n)
    {
        var searchIndices = new int[n];
        for (int i = 0; i < n; i++) searchIndices[i] = i;

        System.Array.Sort(searchIndices, (a, b) =>
        {
            Vector2Int ca = cellsOrder[a];
            Vector2Int cb = cellsOrder[b];
            return ca.x != cb.x ? ca.x.CompareTo(cb.x) : ca.y.CompareTo(cb.y);
        });

        // Invert: stableRankOfSearchIndex[i] = this search-index's rank in coordinate order.
        var stableRankOfSearchIndex = new int[n];
        for (int rank = 0; rank < n; rank++)
        {
            stableRankOfSearchIndex[searchIndices[rank]] = rank;
        }
        return stableRankOfSearchIndex;
    }

    /// <summary>Packs the current domains into an exact MemoKey - returns false (key left default) if n exceeds MemoKey.MaxSupportedVariables, in which case the caller must skip memoization entirely for this candidate rather than silently truncate/corrupt the encoding. Internal (not private) for direct unit testing.</summary>
    internal static bool TryBuildMemoKey(
        List<int>[] domains, PipeType2D[] typeByIndex, int[] stableRankOfSearchIndex, int n, bool differsFromKnown, out MemoKey key)
    {
        if (n > MemoKey.MaxSupportedVariables)
        {
            key = default;
            return false;
        }

        ulong word0 = 0, word1 = 0, word2 = 0, word3 = 0;

        for (int i = 0; i < n; i++)
        {
            int rank = stableRankOfSearchIndex[i];
            int wordIndex = rank / MemoKey.VariablesPerWord;
            int bitOffset = (rank % MemoKey.VariablesPerWord) * MemoKey.BitsPerVariable;

            int domainBits = 0;
            foreach (int mask in domains[i])
            {
                domainBits |= 1 << CampaignPipeOrientationCatalog2D.CanonicalIndexOf(typeByIndex[i], mask);
            }

            ulong shifted = ((ulong)(uint)domainBits) << bitOffset;
            switch (wordIndex)
            {
                case 0: word0 |= shifted; break;
                case 1: word1 |= shifted; break;
                case 2: word2 |= shifted; break;
                default: word3 |= shifted; break;
            }
        }

        key = new MemoKey(word0, word1, word2, word3, differsFromKnown);
        return true;
    }
}
