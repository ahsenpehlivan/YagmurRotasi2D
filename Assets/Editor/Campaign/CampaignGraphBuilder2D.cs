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

        public int BranchCount;
        public int CycleCount;
    }

    private static readonly Direction2D[] AllDirections = { Direction2D.Up, Direction2D.Right, Direction2D.Down, Direction2D.Left };

    /// <summary>
    /// Attempts to build one complete solved graph for the given bounds.
    /// Returns null (with a reason) if the backbone itself could not be
    /// found - callers should treat that as a rejected attempt and retry with
    /// a different seed, exactly like every other rejection reason (Part S).
    /// </summary>
    public static SolvedGraph TryBuildGraph(
        GridBounds2D bounds,
        int branchAttempts,
        int minActivePipes,
        int maxActivePipes,
        CampaignSeededRandom2D rng,
        int maxSearchStepsPerPath,
        out string rejectionReason)
    {
        rejectionReason = null;

        Vector2Int source = bounds.TopLeft;
        Vector2Int target = bounds.BottomRight;
        const Direction2D sourceOutputDirection = Direction2D.Right;
        const Direction2D targetEntryDirection = Direction2D.Up;

        Vector2Int backboneStart = source + sourceOutputDirection.ToVector();
        Vector2Int backboneEnd = target + targetEntryDirection.Opposite().ToVector();

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

        CommitPath(backbone, openSidesByCell, used);
        AddOpenSide(openSidesByCell, backbone[0], sourceOutputDirection.Opposite());
        AddOpenSide(openSidesByCell, backbone[backbone.Count - 1], targetEntryDirection.Opposite());

        int branchCount = 0;
        int cycleCount = 0;
        int maxBranchLength = Mathf.Max(2, (bounds.Width + bounds.Height) / 2);

        for (int attempt = 0; attempt < branchAttempts; attempt++)
        {
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
                break;
            }

            Vector2Int a = rng.Choose(candidates);
            Vector2Int b = rng.Choose(candidates);
            if (a == b || openSidesByCell[a].Count >= 4 || openSidesByCell[b].Count >= 4)
            {
                continue;
            }

            // A branch whose two anchors are already mutually reachable
            // through the network built so far forms a graph cycle once
            // committed; a branch reaching a not-yet-reachable anchor pair
            // simply extends the network (still valid - Part J only requires
            // "not a dead end", not that every branch close a cycle).
            bool formsCycle = IsReachableWithinNetwork(a, b, openSidesByCell);

            List<Vector2Int> branchPath = TryFindPath(a, b, used, bounds, rng, 0, maxBranchLength, maxSearchStepsPerPath);
            if (branchPath == null)
            {
                continue;
            }

            CommitPath(branchPath, openSidesByCell, used);

            branchCount++;
            if (formsCycle)
            {
                cycleCount++;
            }
        }

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
            CycleCount = cycleCount
        };
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
