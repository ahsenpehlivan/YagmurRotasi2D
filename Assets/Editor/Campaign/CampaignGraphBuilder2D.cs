using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Core2D;

/// <summary>
/// Graph-first solved-network construction (Phase 8A Part H/J): builds a
/// Source-to-Target backbone path, then grows additional branches/cycles that
/// always reconnect to an existing network node (never a dead end - Part J).
/// Produces only the LOGICAL open-direction graph (which sides are open at
/// each cell) - no PipeType2D/rotationIndex/PipeSpawnData2D yet, that
/// conversion is CampaignPipeConverter2D's job (Part K), kept separate so
/// this class has one responsibility: valid graph topology only.
/// </summary>
public static class CampaignGraphBuilder2D
{
    public sealed class SolvedGraph
    {
        public Vector2Int Source;
        public Vector2Int Target;
        public Direction2D SourceOutputDirection;
        public Direction2D TargetEntryDirection;

        /// <summary>Every normal pipe cell's solved open directions - degree 2/3/4 only (Part I). Never contains Source or Target.</summary>
        public Dictionary<Vector2Int, HashSet<Direction2D>> OpenSidesByCell;

        /// <summary>Count of ear insertions (Phase 8A.2 Part C) that each added at least one brand-new normal pipe cell - never a zero-new-cell edge (see BranchCount BUG HISTORY on TryBuildGraph).</summary>
        public int BranchCount;
        public int CycleCount;

        /// <summary>Count of edge-detour operations (Phase 8A.2 Part F) applied - an existing direct connection replaced by a longer all-new-cell route. Tracked separately from BranchCount since a detour doesn't change which nodes are connected, only how.</summary>
        public int DetourCount;
    }

    /// <summary>Phase 8A.2 Part A/I: aggregated (never per-attempt-verbose) reasons a single TryBuildGraph call's density-growth loop rejected a candidate ear/detour, plus the resulting cell count - so a caller retrying many seeds can log one summary line instead of hundreds of near-identical ones.</summary>
    public sealed class GraphBuildDiagnostics
    {
        public int BackboneCellCount;
        public int EarAttempts;
        public int EarSuccesses;
        public int DetourAttempts;
        public int DetourSuccesses;
        public int RejectedEndpointDegreeLimit;
        public int RejectedNoCandidatePairs;
        public int RejectedNoValidUnusedPath;
        public int RejectedZeroNewCellPath;
        public int RejectedWouldExceedMax;
        public int FinalActivePipeCount;
    }

    private static readonly Direction2D[] AllDirections = { Direction2D.Up, Direction2D.Right, Direction2D.Down, Direction2D.Left };

    /// <summary>Fixed Source/Target corner+direction convention (Part I/original design) - Source is always TopLeft outputting Right, Target is always BottomRight accepting from Up. A single source of truth so TryBuildGraph, the Part B pre-flight check, and diagnostic logging can never disagree on what these values are.</summary>
    public const Direction2D FixedSourceOutputDirection = Direction2D.Right;
    public const Direction2D FixedTargetEntryDirection = Direction2D.Up;

    /// <summary>
    /// Pure, non-stochastic anchor geometry for a given board size - the two
    /// backbone endpoints a valid path must connect. Derived only from
    /// GridBounds2D + the fixed Source/Target convention above - no
    /// randomness, no grid-size-specific special casing.
    ///
    /// BUG HISTORY (Phase 8A.1): backboneEnd previously used
    /// `target + targetEntryDirection.Opposite().ToVector()` - a flawed
    /// analogy with backboneStart. The correct relationship (cross-checked
    /// against the real, hand-authored Level 1 data - its last pipe before
    /// Target sits at target + Direction2D.Up.ToVector(), i.e. exactly
    /// `target + targetEntryDirection.ToVector()` with NO Opposite()) is: a
    /// port facing direction D on a cell connects to the neighbor cell in
    /// direction D, not its opposite - Source's own formula
    /// (`source + sourceOutputDirection.ToVector()`) already used this
    /// correctly; only the Target side had the sign flipped. Because Target
    /// sits at bounds.BottomRight (the grid's minimum-Y row) on every
    /// supported size, the old formula always stepped one row below the
    /// bottom edge - a 100% deterministic failure every attempt, on every
    /// grid size 5x5-10x10 alike, not a small-grid-only defect.
    /// </summary>
    public static void ComputeAnchors(
        GridBounds2D bounds,
        out Vector2Int source, out Vector2Int target,
        out Direction2D sourceOutputDirection, out Direction2D targetEntryDirection,
        out Vector2Int backboneStart, out Vector2Int backboneEnd)
    {
        source = bounds.TopLeft;
        target = bounds.BottomRight;
        sourceOutputDirection = FixedSourceOutputDirection;
        targetEntryDirection = FixedTargetEntryDirection;

        backboneStart = source + sourceOutputDirection.ToVector();
        // See BUG HISTORY above - the neighbor in the entry direction ITSELF, not its opposite.
        backboneEnd = target + targetEntryDirection.ToVector();
    }

    /// <summary>
    /// Part B (Phase 8A.1): a pure, single, non-stochastic pre-flight check.
    /// If the anchor geometry is invalid, it is invalid for every seed and
    /// every attempt identically (it depends only on grid size/Source/
    /// Target/the fixed directions above) - callers must fail once with a
    /// clear message here instead of burning an entire attempt budget
    /// repeating the same deterministic failure. Never touches
    /// CampaignSeededRandom2D - genuinely stochastic failures (path
    /// collision, unsupported degree, non-unique solution, etc.) are NOT
    /// this method's concern and still go through the normal per-attempt
    /// retry loop in CampaignLevelGenerator2D.
    /// </summary>
    public static bool ValidateProfileGeometry(GridBounds2D bounds, out string reason)
    {
        ComputeAnchors(bounds, out Vector2Int source, out Vector2Int target,
            out Direction2D sourceOutputDirection, out Direction2D targetEntryDirection,
            out Vector2Int backboneStart, out Vector2Int backboneEnd);

        if (!bounds.Contains(backboneStart))
        {
            reason = $"backboneStart {backboneStart} (Source {source} + output {sourceOutputDirection}) is outside bounds [{bounds.MinCell}..{bounds.MaxCell}]";
            return false;
        }

        if (!bounds.Contains(backboneEnd))
        {
            reason = $"backboneEnd {backboneEnd} (Target {target} + entry {targetEntryDirection}) is outside bounds [{bounds.MinCell}..{bounds.MaxCell}]";
            return false;
        }

        if (backboneStart == source || backboneStart == target || backboneEnd == source || backboneEnd == target)
        {
            reason = $"backbone anchor collides with Source/Target (backboneStart={backboneStart}, backboneEnd={backboneEnd}, Source={source}, Target={target})";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Phase 8A.2 Part G: pure geometric feasibility - catches only a
    /// profile that is mathematically impossible for ANY algorithm on this
    /// board size (density target exceeding the number of cells that could
    /// ever be normal pipes), never a generator-implementation limitation
    /// (those are legitimate per-attempt rejections that belong in the
    /// normal retry loop, not a pre-flight failure). Deliberately does NOT
    /// attempt to model Tee/Cross ratios or minimum-tap compatibility with
    /// any precision - a full combinatorial feasibility model would need to
    /// actually attempt generation, which the retry loop already does
    /// safely; this check only rules out the one thing that's provably
    /// impossible from cell-counting alone.
    /// </summary>
    public static bool ValidateProfileFeasibility(GridBounds2D bounds, int minActivePipes, int maxActivePipes, out string reason)
    {
        int usableCells = bounds.Width * bounds.Height - 2; // minus Source and Target's own cells.

        if (minActivePipes > usableCells)
        {
            reason = $"minActivePipes {minActivePipes} exceeds the {usableCells} cells available for normal pipes on a {bounds.Width}x{bounds.Height} board (25 total minus Source and Target) - geometrically impossible regardless of algorithm";
            return false;
        }

        if (maxActivePipes > usableCells)
        {
            reason = $"maxActivePipes {maxActivePipes} exceeds the {usableCells} cells available for normal pipes on a {bounds.Width}x{bounds.Height} board";
            return false;
        }

        if (minActivePipes > maxActivePipes)
        {
            reason = $"minActivePipes {minActivePipes} exceeds maxActivePipes {maxActivePipes} - contradictory profile";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Part A's required diagnostic dump - formats every quantity the failure report must show for one level/attempt. Performs no stochastic work itself.</summary>
    public static string DescribeAnchorDiagnostics(int levelNumber, GridBounds2D bounds, int attemptNumber)
    {
        ComputeAnchors(bounds, out Vector2Int source, out Vector2Int target,
            out Direction2D sourceOutputDirection, out Direction2D targetEntryDirection,
            out Vector2Int backboneStart, out Vector2Int backboneEnd);

        bool startOk = bounds.Contains(backboneStart);
        bool endOk = bounds.Contains(backboneEnd);

        return $"Level {levelNumber}:\n" +
            $"Grid={bounds.Width}x{bounds.Height}\n" +
            $"Bounds=min({bounds.MinCell}), max({bounds.MaxCell})\n" +
            $"Source={source}\n" +
            $"SourceOutputDirection={sourceOutputDirection}\n" +
            $"Target={target}\n" +
            $"TargetEntryDirection={targetEntryDirection}\n" +
            $"BackboneStart={backboneStart} (AnchorInsideBounds={startOk})\n" +
            $"BackboneEnd={backboneEnd} (AnchorInsideBounds={endOk})\n" +
            $"Attempt={attemptNumber}";
    }

    /// <summary>
    /// Attempts to build one complete solved graph for the given bounds.
    /// Returns null (with a reason) if the backbone itself could not be
    /// found - callers should treat that as a rejected attempt and retry with
    /// a different seed, exactly like every other rejection reason (Part S).
    /// `backboneCellsOut`, when non-null, receives the literal backbone path
    /// cells in order - used only by Part F's playable-area validator to
    /// assert adjacency/bounds on the raw path, never by production code.
    /// </summary>
    public static SolvedGraph TryBuildGraph(
        GridBounds2D bounds,
        int branchAttempts,
        int minActivePipes,
        int preferredActivePipes,
        int maxActivePipes,
        CampaignSeededRandom2D rng,
        int maxSearchStepsPerPath,
        out string rejectionReason,
        List<Vector2Int> backboneCellsOut = null,
        GraphBuildDiagnostics diagnosticsOut = null)
    {
        rejectionReason = null;

        ComputeAnchors(bounds, out Vector2Int source, out Vector2Int target,
            out Direction2D sourceOutputDirection, out Direction2D targetEntryDirection,
            out Vector2Int backboneStart, out Vector2Int backboneEnd);

        if (!bounds.Contains(backboneStart) || !bounds.Contains(backboneEnd))
        {
            rejectionReason = "backbone anchor cell outside board bounds (grid too small)";
            return null;
        }

        var used = new HashSet<Vector2Int> { source, target };
        var openSidesByCell = new Dictionary<Vector2Int, HashSet<Direction2D>>();

        int minBackboneLength = Mathf.Max(1, Mathf.Min(bounds.Width, bounds.Height) - 1);
        int maxBackboneLength = bounds.Width + bounds.Height + 2;

        List<Vector2Int> backbone = TryFindPath(backboneStart, backboneEnd, used, bounds, rng, minBackboneLength, maxBackboneLength, maxSearchStepsPerPath);
        if (backbone == null)
        {
            rejectionReason = "no backbone path found within search budget";
            return null;
        }

        if (backboneCellsOut != null)
        {
            backboneCellsOut.AddRange(backbone);
        }

        CommitPath(backbone, openSidesByCell, used);
        AddOpenSide(openSidesByCell, backbone[0], sourceOutputDirection.Opposite());
        AddOpenSide(openSidesByCell, backbone[backbone.Count - 1], targetEntryDirection.Opposite());

        if (diagnosticsOut != null)
        {
            diagnosticsOut.BackboneCellCount = backbone.Count;
        }

        int branchCount = 0;
        int cycleCount = 0;
        int detourCount = 0;
        int maxBranchLength = Mathf.Max(2, (bounds.Width + bounds.Height) / 2);

        // Part C/D (Phase 8A.2): grow density via deterministic ear insertion
        // until preferredActivePipes is reached, instead of stopping after a
        // small fixed attempt count regardless of how far short of the
        // target that left the graph. The search budget scales with how
        // many cells are actually still needed - not a flat retry count -
        // since a nearly-saturated small board legitimately needs many more
        // candidate (A,B) pairs tried than a sparse one does. branchAttempts
        // remains a floor so tiny density gaps never get an unreasonably
        // small budget.
        int earBudget = Mathf.Max(branchAttempts, (preferredActivePipes - openSidesByCell.Count) * 15 + 20);

        for (int attempt = 0; attempt < earBudget && openSidesByCell.Count < preferredActivePipes; attempt++)
        {
            if (diagnosticsOut != null) diagnosticsOut.EarAttempts++;

            if (openSidesByCell.Count >= maxActivePipes)
            {
                break;
            }

            List<Vector2Int> candidates = new List<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, HashSet<Direction2D>> kv in openSidesByCell)
            {
                if (kv.Value.Count < 4)
                {
                    candidates.Add(kv.Key);
                }
            }

            if (candidates.Count < 2)
            {
                if (diagnosticsOut != null) diagnosticsOut.RejectedNoCandidatePairs++;
                break;
            }

            Vector2Int a = rng.Choose(candidates);
            Vector2Int b = rng.Choose(candidates);
            if (a == b || openSidesByCell[a].Count >= 4 || openSidesByCell[b].Count >= 4)
            {
                if (diagnosticsOut != null) diagnosticsOut.RejectedEndpointDegreeLimit++;
                continue;
            }

            // A branch whose two anchors are already mutually reachable
            // through the network built so far forms a graph cycle once
            // committed; a branch reaching a not-yet-reachable anchor pair
            // simply extends the network (still valid - Part J only requires
            // "not a dead end", not that every branch close a cycle).
            bool formsCycle = IsReachableWithinNetwork(a, b, openSidesByCell);

            // BUG HISTORY (Phase 8A.2 Part B): this previously passed
            // minLength=0, which let TryFindPath accept a trivial two-cell
            // [a,b] "path" whenever a and b happened to already be
            // orthogonal neighbors - a real new EDGE (and a branchCount/
            // cycleCount increment) but ZERO new cells, silently inflating
            // both metrics without growing density at all. minLength=2
            // forces at least 2 steps (3 cells: a, >=1 new interior cell,
            // b), so every accepted ear is a genuine density increase by
            // construction - never a reclassification or duplicate edge.
            List<Vector2Int> earPath = TryFindPath(a, b, used, bounds, rng, 2, maxBranchLength, maxSearchStepsPerPath);
            if (earPath == null)
            {
                if (diagnosticsOut != null) diagnosticsOut.RejectedNoValidUnusedPath++;
                continue;
            }

            int newCellsAdded = earPath.Count - 2; // interior cells only, excluding the a/b endpoints.
            if (newCellsAdded < 1)
            {
                // Should be unreachable given minLength=2 above - kept as a
                // defense-in-depth invariant check, not the primary guard.
                if (diagnosticsOut != null) diagnosticsOut.RejectedZeroNewCellPath++;
                continue;
            }

            if (openSidesByCell.Count + newCellsAdded > maxActivePipes)
            {
                if (diagnosticsOut != null) diagnosticsOut.RejectedWouldExceedMax++;
                continue;
            }

            CommitPath(earPath, openSidesByCell, used);

            branchCount++;
            if (formsCycle)
            {
                cycleCount++;
            }
            if (diagnosticsOut != null) diagnosticsOut.EarSuccesses++;
        }

        // Part F: if ear insertion alone couldn't clear the MINIMUM (not
        // just the preferred) density, fall back to a bounded number of
        // edge-detour operations - replacing an already-committed direct
        // connection with a longer all-new-cell route - since detours can
        // add density even when every remaining degree-available node pair
        // has no free unused-cell path directly between them (the exact
        // saturation case ear insertion alone cannot escape).
        int detourBudget = Mathf.Max(10, (minActivePipes - openSidesByCell.Count) * 10);
        for (int attempt = 0; attempt < detourBudget && openSidesByCell.Count < minActivePipes; attempt++)
        {
            if (diagnosticsOut != null) diagnosticsOut.DetourAttempts++;

            if (!TryApplyDetour(openSidesByCell, used, bounds, rng, maxBranchLength, maxSearchStepsPerPath, maxActivePipes, out int detourNewCells))
            {
                continue;
            }

            detourCount++;
            if (diagnosticsOut != null) diagnosticsOut.DetourSuccesses++;
        }

        if (diagnosticsOut != null) diagnosticsOut.FinalActivePipeCount = openSidesByCell.Count;

        if (openSidesByCell.Count < minActivePipes)
        {
            rejectionReason = $"active pipe count {openSidesByCell.Count} below minimum {minActivePipes}";
            return null;
        }

        foreach (KeyValuePair<Vector2Int, HashSet<Direction2D>> kv in openSidesByCell)
        {
            if (kv.Value.Count < 2 || kv.Value.Count > 4)
            {
                rejectionReason = $"cell {kv.Key} has unsupported degree {kv.Value.Count} (must be 2-4)";
                return null;
            }
        }

        return new SolvedGraph
        {
            Source = source,
            Target = target,
            SourceOutputDirection = sourceOutputDirection,
            TargetEntryDirection = targetEntryDirection,
            OpenSidesByCell = openSidesByCell,
            BranchCount = branchCount,
            CycleCount = cycleCount,
            DetourCount = detourCount
        };
    }

    /// <summary>
    /// Part F: picks an existing committed direct edge (A,B) at random,
    /// temporarily removes it, and searches for a brand-new all-unused-cell
    /// route between the same A and B (minLength=3, so at least 2 new
    /// interior cells are required - "a path containing at least two new
    /// cells" per spec). On success the new route's own CommitPath call
    /// re-establishes A and B's connectivity (now via the new interior
    /// cells instead of directly) - on failure the original edge is
    /// restored exactly, leaving the graph unchanged. Never touches Source/
    /// Target (neither is ever a key of openSidesByCell, so no edge toward
    /// either can ever be selected here).
    /// </summary>
    private static bool TryApplyDetour(
        Dictionary<Vector2Int, HashSet<Direction2D>> openSidesByCell,
        HashSet<Vector2Int> used,
        GridBounds2D bounds,
        CampaignSeededRandom2D rng,
        int maxBranchLength,
        int maxSearchStepsPerPath,
        int maxActivePipes,
        out int newCellsAdded)
    {
        newCellsAdded = 0;

        var edges = new List<(Vector2Int a, Vector2Int b, Direction2D dirAtoB)>();
        foreach (KeyValuePair<Vector2Int, HashSet<Direction2D>> kv in openSidesByCell)
        {
            foreach (Direction2D dir in kv.Value)
            {
                Vector2Int neighbor = kv.Key + dir.ToVector();
                if (openSidesByCell.ContainsKey(neighbor))
                {
                    edges.Add((kv.Key, neighbor, dir));
                }
            }
        }

        if (edges.Count == 0)
        {
            return false;
        }

        rng.Shuffle(edges);

        foreach ((Vector2Int a, Vector2Int b, Direction2D dirAtoB) in edges)
        {
            Direction2D dirBtoA = dirAtoB.Opposite();

            openSidesByCell[a].Remove(dirAtoB);
            openSidesByCell[b].Remove(dirBtoA);

            List<Vector2Int> detourPath = TryFindPath(a, b, used, bounds, rng, 3, maxBranchLength, maxSearchStepsPerPath);
            int added = detourPath != null ? detourPath.Count - 2 : 0;

            if (detourPath != null && added >= 2 && openSidesByCell.Count + added <= maxActivePipes)
            {
                CommitPath(detourPath, openSidesByCell, used);
                newCellsAdded = added;
                return true;
            }

            // Restore the original edge exactly - this candidate didn't work.
            openSidesByCell[a].Add(dirAtoB);
            openSidesByCell[b].Add(dirBtoA);
        }

        return false;
    }

    /// <summary>Cheap same-attempt-only reachability check (BFS over the graph built so far) used only to classify a just-added branch as "cycle" vs "extension" for reporting - never used for correctness.</summary>
    private static bool IsReachableWithinNetwork(Vector2Int from, Vector2Int to, Dictionary<Vector2Int, HashSet<Direction2D>> openSidesByCell)
    {
        var visited = new HashSet<Vector2Int> { from };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == to)
            {
                return true;
            }

            if (!openSidesByCell.TryGetValue(current, out HashSet<Direction2D> sides))
            {
                continue;
            }

            foreach (Direction2D dir in sides)
            {
                Vector2Int next = current + dir.ToVector();
                if (openSidesByCell.ContainsKey(next) && visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }

    private static Direction2D DirectionOrDefault(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;
        foreach (Direction2D dir in AllDirections)
        {
            if (dir.ToVector() == delta)
            {
                return dir;
            }
        }
        return Direction2D.Up;
    }

    private static void AddOpenSide(Dictionary<Vector2Int, HashSet<Direction2D>> openSidesByCell, Vector2Int cell, Direction2D dir)
    {
        if (!openSidesByCell.TryGetValue(cell, out HashSet<Direction2D> sides))
        {
            sides = new HashSet<Direction2D>();
            openSidesByCell[cell] = sides;
        }
        sides.Add(dir);
    }

    /// <summary>
    /// Adds every direction toward each cell's immediate list-neighbor(s) -
    /// works identically whether the endpoints are brand-new cells (backbone)
    /// or pre-existing network nodes gaining exactly one new direction each
    /// (branch), since a pre-existing node's HashSet already has its earlier
    /// directions and this only ever adds.
    /// </summary>
    private static void CommitPath(List<Vector2Int> path, Dictionary<Vector2Int, HashSet<Direction2D>> openSidesByCell, HashSet<Vector2Int> used)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int cell = path[i];
            used.Add(cell);

            if (i > 0)
            {
                AddOpenSide(openSidesByCell, cell, DirectionOrDefault(cell, path[i - 1]));
            }
            if (i < path.Count - 1)
            {
                AddOpenSide(openSidesByCell, cell, DirectionOrDefault(cell, path[i + 1]));
            }
        }
    }

    /// <summary>
    /// Randomized DFS with backtracking, biased toward the endpoint by
    /// Manhattan distance (a standard, effective maze/path heuristic) so
    /// search converges within the step budget even on larger grids, while
    /// still producing varied paths across different seeds. Interior cells
    /// must be unused; start/end may already be used (they are the path's own
    /// anchors). Bounded by maxSearchSteps (a node-visit count, not wall-clock
    /// time) so a stuck search fails deterministically instead of hanging the
    /// Editor - the caller treats a null result as a normal rejected attempt.
    /// </summary>
    private static List<Vector2Int> TryFindPath(
        Vector2Int start, Vector2Int end, HashSet<Vector2Int> used, GridBounds2D bounds,
        CampaignSeededRandom2D rng, int minLength, int maxLength, int maxSearchSteps)
    {
        var path = new List<Vector2Int> { start };
        var visitedThisAttempt = new HashSet<Vector2Int> { start };
        int steps = 0;

        bool Search()
        {
            steps++;
            if (steps > maxSearchSteps)
            {
                return false;
            }

            Vector2Int current = path[path.Count - 1];
            if (current == end && path.Count - 1 >= minLength)
            {
                return true;
            }

            if (path.Count - 1 >= maxLength)
            {
                return false;
            }

            var dirs = new List<Direction2D>(AllDirections);
            rng.Shuffle(dirs);
            dirs.Sort((d1, d2) =>
                ManhattanDistance(current + d1.ToVector(), end).CompareTo(ManhattanDistance(current + d2.ToVector(), end)));

            foreach (Direction2D dir in dirs)
            {
                Vector2Int next = current + dir.ToVector();
                if (!bounds.Contains(next))
                {
                    continue;
                }

                if (next == end)
                {
                    if (path.Count - 1 + 1 < minLength)
                    {
                        continue;
                    }
                    path.Add(next);
                    if (Search()) return true;
                    path.RemoveAt(path.Count - 1);
                    continue;
                }

                if (used.Contains(next) || visitedThisAttempt.Contains(next))
                {
                    continue;
                }

                path.Add(next);
                visitedThisAttempt.Add(next);
                if (Search()) return true;
                visitedThisAttempt.Remove(next);
                path.RemoveAt(path.Count - 1);
            }

            return false;
        }

        return Search() ? path : null;
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
