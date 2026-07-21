using UnityEngine;
using UnityEngine.UI;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Phase 9A Part B: recomputes a GridLayoutGroup's column count and cell
    /// width from the CURRENT available width instead of a fixed constraint -
    /// no hardcoded absolute positions for the 100 level cards. Lives on the
    /// same GameObject as the GridLayoutGroup (the ScrollRect's Content, whose
    /// RectTransform is anchor-stretched horizontally to the Viewport - see
    /// LevelSelectSceneBuilder2D), so Unity's own OnRectTransformDimensionsChange
    /// callback fires automatically whenever the Viewport's width actually
    /// changes (screen resize, orientation change, Editor Game view resize) -
    /// no manual polling needed. Reusable as-is for the later Phase 9B web/
    /// landscape conversion.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ResponsiveLevelGrid2D : MonoBehaviour
    {
        // Phase 9B: retuned for the 1920x1080 landscape reference (was tuned
        // for a 1080-wide portrait reference in Phase 9A). Content.rect.width
        // is already in CanvasScaler-adjusted canvas units (not raw
        // Screen.width), so these constants are tuned against that space
        // directly - approximate content width at the ScrollView's ~90%-of-
        // SafeAreaRoot horizontal margin, verified against the requested
        // column targets: ~1728 canvas units at 1920 screen -> 10 columns,
        // ~1229 at 1366 -> 7, ~1152 at 1280 -> 6, ~864 at 960 -> 5. All land
        // within (or at the edge of) the requested "approximately" bands;
        // spot-check in the Editor Game view at each target resolution.
        [SerializeField] private GridLayoutGroup gridLayoutGroup;
        [SerializeField] private float minCardWidth = 150f;
        [SerializeField] private float maxCardWidth = 220f;
        [SerializeField] private float cardHeight = 200f;
        [SerializeField] private float spacing = 18f;
        [SerializeField] private int minColumns = 3;
        [SerializeField] private int maxColumns = 12;

        private RectTransform rectTransform;
        private float lastWidth = -1f;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            if (gridLayoutGroup == null) gridLayoutGroup = GetComponent<GridLayoutGroup>();
        }

        private void OnEnable()
        {
            Recompute(force: true);
        }

        /// <summary>Unity calls this automatically whenever this RectTransform's own width/height changes - including when its anchors are stretched to a parent (the Viewport) that resized. This is the actual "adapts when Game view width changes" mechanism, not a per-frame poll.</summary>
        private void OnRectTransformDimensionsChange()
        {
            // Awake() may not have run yet if this fires from the Editor while
            // the object is being instantiated/enabled for the first time.
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            Recompute(force: false);
        }

        private void Recompute(bool force)
        {
            if (gridLayoutGroup == null || rectTransform == null)
                return;

            float width = rectTransform.rect.width;
            if (width <= 1f)
                return;

            if (!force && Mathf.Approximately(width, lastWidth))
                return;

            lastWidth = width;

            // Widest column count that keeps each cell at least minCardWidth,
            // clamped to [minColumns, maxColumns] - matches the requested
            // rough column bands (3-4 narrow, 5-7 medium, 8-10 wide) purely by
            // following available width, with no per-breakpoint special case.
            int columns = Mathf.FloorToInt((width + spacing) / (minCardWidth + spacing));
            columns = Mathf.Clamp(columns, minColumns, maxColumns);

            float cellWidth = (width - spacing * (columns - 1)) / columns;
            cellWidth = Mathf.Clamp(cellWidth, minCardWidth, maxCardWidth);

            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayoutGroup.constraintCount = columns;
            gridLayoutGroup.cellSize = new Vector2(cellWidth, cardHeight);
            gridLayoutGroup.spacing = new Vector2(spacing, spacing);
        }
    }
}
