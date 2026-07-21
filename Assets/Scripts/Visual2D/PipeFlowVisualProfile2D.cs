using YagmurRotasi2D.Core2D;

namespace YagmurRotasi2D.Visual2D
{
    /// <summary>
    /// Per-pipe-type authored water-fill entry side (in LOCAL/canonical
    /// rotationIndex=0 terms) and the WaterOverlay-only spatial correction
    /// needed when a pipe's REAL entry side (from FlowSolveResult2D/
    /// PipeFlowStep2D.PrimaryEntrySide, converted via Direction2DExtensions.
    /// ToLocalDirection) differs from the authored one. All four water-fill
    /// frames always play forward (0..3, ending on the fully-filled frame) -
    /// direction is achieved entirely through this correction, never by
    /// reversing frame order (which would end on the wrong, empty-looking
    /// frame for a progressive fill sheet).
    ///
    /// Determined by direct inspection of pipes_tileset.png's four rows
    /// (Corner/Tee/Cross/WideStraight/NarrowStraight all visibly fill
    /// "bottom-up" in their own raw pixel frame; Tee specifically fills its
    /// branch/stem first, then spreads into the through-line), combined with
    /// each type's already-established, UNCHANGED fixed BaseVisual/
    /// WaterOverlay art-alignment offset (90 degrees for Straight/Corner/
    /// Cross, 180 for Tee - see BranchingPipeAssetBinder.GetVisualRotationOffset)
    /// and the verified relationship that a +90 degree Euler Z rotation shifts
    /// displayed content by -1 Direction2D enum step relative to raw pixels
    /// (cross-checked against the already-working Corner/Straight/Tee open-
    /// direction tables, not assumed).
    ///
    /// Confidence: HIGH for Tee (the branch-fills-first pattern is the
    /// clearest signal in the sheet) and for the general method (Straight,
    /// Corner and Cross all independently derive to the SAME "Right" local
    /// entry, which is exactly what's expected since all three share the same
    /// fixed offset and the same bottom-up authoring convention - a strong
    /// internal consistency check). MODERATE for the exact raw-pixel geometry
    /// this was derived from (read from a small sprite sheet, not confirmed
    /// pixel-by-pixel). If Play Mode testing shows a type still filling from
    /// the wrong side, adjust ONLY that type's entry in AuthoredLocalEntrySide
    /// below - nothing else needs to change.
    /// </summary>
    public static class PipeFlowVisualProfile2D
    {
        public static Direction2D AuthoredLocalEntrySide(PipeType2D pipeType)
        {
            switch (pipeType)
            {
                case PipeType2D.Straight: return Direction2D.Right;
                case PipeType2D.Corner: return Direction2D.Right;
                case PipeType2D.Tee: return Direction2D.Up; // the branch/stem, not a through-line side
                case PipeType2D.Cross: return Direction2D.Right;
                default: return Direction2D.Up;
            }
        }

        /// <summary>
        /// Resolves the WaterOverlay-only correction - an extra Z rotation on
        /// top of the type's existing fixed offset, plus an optional local
        /// mirror (SpriteRenderer.flipX) - needed so the animation visually
        /// starts from localEntrySide instead of the type's authored default.
        /// </summary>
        public static void ResolveCorrection(PipeType2D pipeType, Direction2D localEntrySide, out float extraRotationZ, out bool flipX)
        {
            extraRotationZ = 0f;
            flipX = false;

            Direction2D authored = AuthoredLocalEntrySide(pipeType);
            if (localEntrySide == authored)
            {
                return;
            }

            switch (pipeType)
            {
                case PipeType2D.Cross:
                    // Fully rotationally symmetric (always open on all four
                    // sides, rotationIndex always 0) - ANY 90-degree multiple
                    // keeps the "+" silhouette perfectly aligned with
                    // BaseVisual, so this exactly matches any of the four real
                    // entry sides, not just a binary flip. A +90 degree Euler Z
                    // shifts displayed content by -1 step, so -90 degrees per
                    // step is needed to shift content forward by +1 step.
                    int steps = (((int)localEntrySide - (int)authored) % 4 + 4) % 4;
                    extraRotationZ = -90f * steps;
                    break;

                case PipeType2D.Straight:
                    // Only two logically distinct orientations - the through-
                    // pair's other side is always exactly opposite the
                    // authored one, and a fully-filled/symmetric straight
                    // channel looks identical rotated 180 degrees, so this is
                    // always exactly a 180-degree correction (self-inverse,
                    // sign-independent).
                    extraRotationZ = 180f;
                    break;

                case PipeType2D.Corner:
                    // Corner's two open sides are always adjacent (never
                    // opposite), so a plain rotation would misalign the
                    // L-shaped water channel from BaseVisual. Rotating 90
                    // degrees then mirroring local X is a diagonal reflection
                    // ((x,y) -> (y,x)) - a valid symmetry of an L-shape's own
                    // diagonal axis that swaps exactly which of its two
                    // adjacent open sides the fill starts from while keeping
                    // the same two-side silhouette.
                    extraRotationZ = 90f;
                    flipX = true;
                    break;

                case PipeType2D.Tee:
                    // Tee's authored animation only shows water entering the
                    // branch/stem and spreading into the through-line - no
                    // rotation/mirror maps a through-line entry onto that same
                    // visual pattern (the branch and the two through-line
                    // ports are topologically different roles, not swappable
                    // by any symmetry of the shape). When the real entry is a
                    // through-line side, this applies the one safe partial
                    // improvement available (distinguishing which through-line
                    // side reads as "first") - this is NOT a full fix for that
                    // case; see the Known Limitations note in CURRENT_TASK.md.
                    // The branch-entry case above remains exactly correct.
                    int clockwiseNeighbor = ((int)authored + 1) % 4;
                    flipX = (int)localEntrySide == clockwiseNeighbor;
                    break;
            }
        }
    }
}
