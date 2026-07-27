using UnityEngine;
using YagmurRotasi2D.Gameplay2D;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Phase 9B board-fitting (Option A: one calculated world-space container,
    /// chosen over fitting the camera to a viewport rect - see class remarks
    /// below for why). Drives ONE wrapper Transform ("BoardFitContainer")
    /// that is the sole parent of BOTH the BoardManager2D GameObject and
    /// BoardRoot (the grid-cell/pipe/Source/Target visual container) - the
    /// Phase 9B layout audit found these were previously two SEPARATE root
    /// GameObjects that only coincided at world origin by coincidence.
    ///
    /// Why this matters: BoardManager2D.GridToWorld() computes
    /// `transform.position + local offset`, using BoardManager2D's OWN
    /// transform - not BoardRoot's. Scaling/moving BoardRoot alone (without
    /// touching BoardManager2D identically) would desync GridToWorld()'s
    /// returned coordinates from where pipes actually render on screen.
    /// Reparenting BOTH under one shared fit container (both at local
    /// (0,0,0)/scale 1 relative to it) makes them move/scale in perfect
    /// lockstep - GridToWorld()'s output and the actual rendered position
    /// always agree, at any fit scale/offset. This preserves the entire
    /// existing pipe/world coordinate system exactly (BoardManager2D/
    /// LevelManager2D/GridBounds2D/CellSize math is completely untouched);
    /// this component only ever applies ONE additional uniform scale+offset
    /// on top of it, at the outermost level - never touches an individual
    /// pipe/cell/Source/Target Transform.
    ///
    /// Recomputes only when WebViewportState2D reports an actual viewport
    /// change (never per-frame) - deterministic and idempotent (a pure
    /// function of camera + target board-area rect, so repeated calls with
    /// the same inputs always produce the identical output - no cumulative
    /// drift is possible since nothing is ever multiplied onto the previous
    /// frame's result).
    ///
    /// Phase 9C: the board-area rect is no longer a pair of hand-tuned
    /// camera-viewport-fraction constants - it is read directly from
    /// BoardViewport's actual on-screen RectTransform every recompute (its
    /// 4 world corners -> screen pixels via RectTransformUtility.
    /// WorldToScreenPoint(null, ...), since GameCanvas is Screen Space -
    /// Overlay -> normalized to a 0..1 viewport fraction -> fed into the
    /// same Camera.ViewportToWorldPoint math as before). This keeps the
    /// board correctly fitted even if BoardViewport's real width changes for
    /// a reason other than a raw screen resize (e.g. ControlPanel's
    /// LayoutElement clamping between its min/max width at an unusual
    /// aspect ratio) - the old fixed fractions could silently drift out of
    /// sync with BoardViewport's real rect in exactly that case.
    /// </summary>
    public class GameplayBoardFitter2D : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform boardFitContainer;
        [SerializeField] private WebViewportState2D viewportState;
        [SerializeField] private LevelManager2D levelManager;

        [Tooltip("The gameplay board's on-screen slot (SafeAreaRoot/MainGameplayArea/BoardViewport) - its actual screen rect is read every recompute, never a hardcoded fraction.")]
        [SerializeField] private RectTransform boardViewport;

        private readonly Vector3[] worldCornersBuffer = new Vector3[4];

        private float lastAppliedScale = -1f;
        private Vector3 lastAppliedPosition = new Vector3(float.NaN, float.NaN, float.NaN);

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (viewportState == null) viewportState = FindFirstObjectByType<WebViewportState2D>();
        }

        private void OnEnable()
        {
            if (viewportState != null)
            {
                viewportState.OnViewportChanged += HandleViewportChanged;
            }
            if (levelManager != null)
            {
                levelManager.OnLevelLoaded += HandleLevelLoaded;
            }

            Recompute(force: true);
        }

        private void OnDisable()
        {
            if (viewportState != null)
            {
                viewportState.OnViewportChanged -= HandleViewportChanged;
            }
            if (levelManager != null)
            {
                levelManager.OnLevelLoaded -= HandleLevelLoaded;
            }
        }

        private void HandleViewportChanged()
        {
            Recompute(force: false);
        }

        /// <summary>Defensive only - the fit is a pure function of camera + board-area rect and is completely independent of which level/grid size is loaded (BoardManager2D.ReferenceBoardWorldSize is constant regardless of grid width/height), so this should never actually change the result. Cheap to call, kept for resilience.</summary>
        private void HandleLevelLoaded()
        {
            Recompute(force: false);
        }

        private void Recompute(bool force)
        {
            if (targetCamera == null || boardFitContainer == null || boardViewport == null)
                return;

            // Force any pending layout rebuild (HorizontalLayoutGroup/
            // VerticalLayoutGroup rebuilds are deferred, not immediate) to
            // complete before reading BoardViewport's rect. Without this,
            // the very first Recompute() (called from OnEnable, before
            // Unity's layout system has run even once) reads a stale/
            // default-sized RectTransform instead of the real laid-out one -
            // the confirmed cause of the board rendering permanently tiny
            // after the first load, since nothing else re-triggers a
            // recompute unless the window is actually resized afterward.
            Canvas.ForceUpdateCanvases();

            // Step 1-2 (Part E): read BoardViewport's actual screen-space
            // rectangle and convert it to the camera's viewport fractions.
            // GetWorldCorners order is documented as [0]=bottom-left,
            // [1]=top-left, [2]=top-right, [3]=bottom-right. Camera is
            // passed as null to WorldToScreenPoint because GameCanvas is
            // Screen Space - Overlay (no render camera - Overlay canvas
            // world corners already correspond 1:1 to screen pixels, and
            // null is Unity's documented convention for that case).
            boardViewport.GetWorldCorners(worldCornersBuffer);
            Vector2 screenBottomLeft = RectTransformUtility.WorldToScreenPoint(null, worldCornersBuffer[0]);
            Vector2 screenTopRight = RectTransformUtility.WorldToScreenPoint(null, worldCornersBuffer[2]);

            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);

            Vector2 viewportMin = new Vector2(screenBottomLeft.x / screenWidth, screenBottomLeft.y / screenHeight);
            Vector2 viewportMax = new Vector2(screenTopRight.x / screenWidth, screenTopRight.y / screenHeight);

            // z distance from the camera to the board's own Z plane (0) -
            // irrelevant to the resulting X/Y for an orthographic camera
            // (any in-range distance yields the identical world X/Y for a
            // given viewport fraction), kept explicit/documented rather than
            // a magic constant.
            float boardPlaneDistance = Mathf.Abs(targetCamera.transform.position.z);

            Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(viewportMin.x, viewportMin.y, boardPlaneDistance));
            Vector3 topRight = targetCamera.ViewportToWorldPoint(new Vector3(viewportMax.x, viewportMax.y, boardPlaneDistance));

            float areaWidth = topRight.x - bottomLeft.x;
            float areaHeight = topRight.y - bottomLeft.y;

            if (areaWidth <= 0.01f || areaHeight <= 0.01f)
                return;

            float fitScale = Mathf.Min(areaWidth, areaHeight) / BoardManager2D.ReferenceBoardWorldSize;
            Vector3 areaCenter = (bottomLeft + topRight) * 0.5f;
            Vector3 newPosition = new Vector3(areaCenter.x, areaCenter.y, boardFitContainer.position.z);

            if (!force
                && Mathf.Approximately(fitScale, lastAppliedScale)
                && (newPosition - lastAppliedPosition).sqrMagnitude < 0.0001f)
            {
                return;
            }

            boardFitContainer.localScale = Vector3.one * fitScale;
            boardFitContainer.position = newPosition;

            lastAppliedScale = fitScale;
            lastAppliedPosition = newPosition;
        }
    }
}
