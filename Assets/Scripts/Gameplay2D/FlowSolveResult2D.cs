using System.Collections.Generic;
using YagmurRotasi2D.Core2D;

namespace YagmurRotasi2D.Gameplay2D
{
    public enum FlowFailureReason2D
    {
        None,
        SourceDisconnected,
        TargetNotReached,
        OpenPortOutsideBoard,
        MissingNeighbor,
        ReciprocalConnectionMissing,
        ReachableBranchLeak
    }

    /// <summary>
    /// Full outcome of a FlowSolver2D.Solve() call. A route only succeeds when
    /// Target is reached AND every water-reachable open port is a valid
    /// reciprocal connection - reaching Target while another reachable branch
    /// leaks still fails (IsSuccess = TargetReached &amp;&amp; !HasLeak).
    /// </summary>
    public class FlowSolveResult2D
    {
        public bool IsSuccess;
        public bool TargetReached;
        public bool HasLeak;

        /// <summary>Every pipe reached by the BFS from Source, each exactly once, in no particular order. Populated even on failure (diagnostic).</summary>
        public IReadOnlyList<PipeTile2D> ReachableTiles;

        /// <summary>Reachable pipes grouped by BFS distance from Source, each carrying the real entry side(s) water arrives from, for direction-aware wave-by-wave fill animation. Only meaningful when IsSuccess is true.</summary>
        public IReadOnlyList<IReadOnlyList<PipeFlowStep2D>> FlowSteps;

        /// <summary>Reason for the first leak/failure encountered, if any (None on success).</summary>
        public FlowFailureReason2D FailureReason;

        /// <summary>The pipe whose open port caused the first recorded leak, or null if the leak occurred at the Source itself.</summary>
        public PipeTile2D LeakTile;

        /// <summary>The open direction that caused the first recorded leak, if any.</summary>
        public Direction2D? LeakDirection;
    }
}
