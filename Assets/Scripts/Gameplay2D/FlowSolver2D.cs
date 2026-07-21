using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Core2D;

namespace YagmurRotasi2D.Gameplay2D
{
    /// <summary>
    /// Graph-based flow solver. Performs a cycle-safe BFS from Source across the
    /// reachable pipe network (Straight/Corner/Tee/Cross alike), validating every
    /// water-reachable open port for a reciprocal connection. A tile is visited
    /// at most once no matter how many branches reach it, so cycles terminate
    /// safely. Disconnected pipes outside the reachable network are never
    /// inspected and can never fail the route. See FlowSolveResult2D for the
    /// full success/leak/wave breakdown.
    /// </summary>
    public class FlowSolver2D : MonoBehaviour
    {
        [SerializeField] private BoardManager2D boardManager;

        public FlowSolveResult2D Solve(
            Vector2Int sourceCell,
            Direction2D sourceOutputDirection,
            Vector2Int targetCell,
            Direction2D targetInputDirection)
        {
            var reachable = new List<PipeTile2D>();
            var waves = new List<List<PipeFlowStep2D>>();
            var visited = new HashSet<Vector2Int>();
            // Keyed by cell so a later reciprocal discovery of an already-visited
            // tile (branch merge or cycle rejoin) can append to its existing
            // step's IncomingSides without ever touching PrimaryEntrySide.
            var stepsByCell = new Dictionary<Vector2Int, PipeFlowStep2D>();
            var queue = new Queue<(PipeTile2D tile, int distance)>();

            bool targetReached = false;
            bool hasLeak = false;
            FlowFailureReason2D failureReason = FlowFailureReason2D.None;
            PipeTile2D leakTile = null;
            Direction2D? leakDirection = null;

            void RegisterLeak(FlowFailureReason2D reason, PipeTile2D tile, Direction2D dir)
            {
                if (!hasLeak)
                {
                    failureReason = reason;
                    leakTile = tile;
                    leakDirection = dir;
                }
                hasLeak = true;
            }

            // --- First hop: Source -> first pipe (or directly Source -> Target
            // with no pipes between them at all). Any failure here is reported
            // as SourceDisconnected - nothing leaves Source at all. The first
            // pipe's entry side is the opposite of the direction water travels
            // INTO it (Source outputs Right -> the first pipe is entered from Left). ---
            Vector2Int firstCell = sourceCell + sourceOutputDirection.ToVector();
            if (firstCell == targetCell)
            {
                if (sourceOutputDirection.Opposite() == targetInputDirection)
                {
                    targetReached = true;
                }
                else
                {
                    RegisterLeak(FlowFailureReason2D.ReachableBranchLeak, null, sourceOutputDirection);
                }
            }
            else if (!boardManager.IsInsideGrid(firstCell))
            {
                RegisterLeak(FlowFailureReason2D.SourceDisconnected, null, sourceOutputDirection);
            }
            else
            {
                PipeTile2D firstPipe = boardManager.GetPipe(firstCell);
                Direction2D firstEntrySide = sourceOutputDirection.Opposite();
                if (firstPipe == null || !firstPipe.HasOpening(firstEntrySide))
                {
                    RegisterLeak(FlowFailureReason2D.SourceDisconnected, null, sourceOutputDirection);
                }
                else
                {
                    visited.Add(firstCell);
                    stepsByCell[firstCell] = new PipeFlowStep2D
                    {
                        Pipe = firstPipe,
                        Distance = 0,
                        PrimaryEntrySide = firstEntrySide,
                        IncomingSides = new List<Direction2D> { firstEntrySide }
                    };
                    queue.Enqueue((firstPipe, 0));
                }
            }

            // --- BFS across the reachable pipe network. ---
            while (queue.Count > 0)
            {
                (PipeTile2D tile, int distance) = queue.Dequeue();
                reachable.Add(tile);

                while (waves.Count <= distance)
                {
                    waves.Add(new List<PipeFlowStep2D>());
                }
                waves[distance].Add(stepsByCell[tile.GridPosition]);

                foreach (Direction2D dir in tile.GetOpenDirections())
                {
                    Vector2Int neighborCell = tile.GridPosition + dir.ToVector();

                    if (neighborCell == targetCell)
                    {
                        if (dir.Opposite() == targetInputDirection)
                        {
                            targetReached = true;
                        }
                        else
                        {
                            // Points at Target, but from a side Target doesn't accept - an invalid endpoint.
                            RegisterLeak(FlowFailureReason2D.ReachableBranchLeak, tile, dir);
                        }
                        continue;
                    }

                    if (neighborCell == sourceCell)
                    {
                        // Only the designated inlet side is a valid connection back to Source.
                        if (dir.Opposite() != sourceOutputDirection)
                        {
                            RegisterLeak(FlowFailureReason2D.ReachableBranchLeak, tile, dir);
                        }
                        continue;
                    }

                    if (!boardManager.IsInsideGrid(neighborCell))
                    {
                        RegisterLeak(FlowFailureReason2D.OpenPortOutsideBoard, tile, dir);
                        continue;
                    }

                    Direction2D entrySide = dir.Opposite();

                    if (visited.Contains(neighborCell))
                    {
                        // Already validated/enqueued from elsewhere (cycle or
                        // multi-branch merge) - safe, no duplicate work for
                        // reciprocal/leak purposes either way. But this same
                        // edge can mean three different things for ANIMATION
                        // arrival metadata, distinguished purely by comparing
                        // BFS distances (an unweighted-BFS invariant: any real
                        // edge's two endpoints differ in distance by at most 1):
                        //   distance+1 == neighbor's distance -> current tile is
                        //     genuinely one step closer to Source than neighbor,
                        //     so this is a real second arrival into neighbor
                        //     (branch merge) - record it.
                        //   neighbor's distance+1 == distance -> neighbor is
                        //     closer to Source than current tile, i.e. this is
                        //     current's OWN outgoing/entry edge being re-observed
                        //     from the far side - never record an arrival here
                        //     (that would record water "entering" a tile from a
                        //     tile it already flowed OUT of).
                        //   neighbor's distance == distance -> same BFS wave, a
                        //     lateral cycle edge - neither tile "arrives" via it.
                        PipeFlowStep2D neighborStep = stepsByCell[neighborCell];
                        if (distance + 1 == neighborStep.Distance && !neighborStep.IncomingSides.Contains(entrySide))
                        {
                            neighborStep.IncomingSides.Add(entrySide);
                        }
                        continue;
                    }

                    PipeTile2D neighborPipe = boardManager.GetPipe(neighborCell);
                    if (neighborPipe == null)
                    {
                        RegisterLeak(FlowFailureReason2D.MissingNeighbor, tile, dir);
                        continue;
                    }

                    if (!neighborPipe.HasOpening(entrySide))
                    {
                        RegisterLeak(FlowFailureReason2D.ReciprocalConnectionMissing, tile, dir);
                        continue;
                    }

                    visited.Add(neighborCell);
                    stepsByCell[neighborCell] = new PipeFlowStep2D
                    {
                        Pipe = neighborPipe,
                        Distance = distance + 1,
                        PrimaryEntrySide = entrySide,
                        IncomingSides = new List<Direction2D> { entrySide }
                    };
                    queue.Enqueue((neighborPipe, distance + 1));
                }
            }

            if (!targetReached && !hasLeak)
            {
                failureReason = FlowFailureReason2D.TargetNotReached;
            }

            return new FlowSolveResult2D
            {
                IsSuccess = targetReached && !hasLeak,
                TargetReached = targetReached,
                HasLeak = hasLeak,
                ReachableTiles = reachable,
                FlowSteps = waves,
                FailureReason = failureReason,
                LeakTile = leakTile,
                LeakDirection = leakDirection
            };
        }
    }
}
