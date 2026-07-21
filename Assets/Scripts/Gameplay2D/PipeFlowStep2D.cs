using System.Collections.Generic;
using YagmurRotasi2D.Core2D;

namespace YagmurRotasi2D.Gameplay2D
{
    /// <summary>
    /// One pipe's real BFS traversal result from FlowSolver2D.Solve() - which
    /// pipe, how far from Source (for wave grouping), and which side(s) water
    /// actually arrives from. Used to drive direction-aware fill animation
    /// (WaterFlowAnimator2D/PipeWaterVisual2D) so a pipe's water always visibly
    /// starts from the side it was really entered from, instead of always
    /// playing the same fixed animation regardless of arrival direction.
    /// </summary>
    public sealed class PipeFlowStep2D
    {
        public PipeTile2D Pipe;
        public int Distance;

        /// <summary>The side used for animation - the first-discovered valid entry (fixed GetOpenDirections() iteration order, never HashSet iteration order), never replaced afterward even if other branches also reach this tile.</summary>
        public Direction2D PrimaryEntrySide;

        /// <summary>Every valid reciprocal side water reaches this pipe from, in discovery order (PrimaryEntrySide is always IncomingSides[0]). More than one entry only happens at a branch merge or cycle rejoin.</summary>
        public List<Direction2D> IncomingSides;
    }
}
