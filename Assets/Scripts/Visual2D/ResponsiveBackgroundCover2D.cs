using UnityEngine;
using YagmurRotasi2D.UI2D;

namespace YagmurRotasi2D.Visual2D
{
    /// <summary>
    /// Keeps a full-screen background SpriteRenderer covering the ENTIRE
    /// orthographic camera frustum at any browser aspect ratio - aspect-
    /// preserving "cover" (envelope) behavior, never stretched, never
    /// letterboxed. Recomputes only when WebViewportState2D reports an
    /// actual viewport change (same proven idiom GameplayBoardFitter2D
    /// already uses - a cheap Screen.width/height equality check driving an
    /// event, never a per-frame recompute), so this is safe across browser
    /// resize, fullscreen toggle, and exiting fullscreen.
    ///
    /// Deliberately independent of BoardFitContainer/BoardRoot - this scales
    /// relative to the CAMERA's frustum, not the board's on-screen area, so
    /// it must never be a descendant of BoardFitContainer (which
    /// GameplayBoardFitter2D rescales/repositions to fit the board area
    /// specifically). The GameScene builder parents this under
    /// IndependentWorldFXRoot instead - the same reasoning Phase 9L already
    /// established for CloudAndRain/SuccessFXZone needing to stay outside
    /// that hierarchy.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ResponsiveBackgroundCover2D : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private WebViewportState2D viewportState;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private float lastAppliedScale = -1f;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (viewportState == null) viewportState = FindFirstObjectByType<WebViewportState2D>();
        }

        private void OnEnable()
        {
            if (viewportState != null)
            {
                viewportState.OnViewportChanged += HandleViewportChanged;
            }

            Recompute(force: true);
        }

        private void OnDisable()
        {
            if (viewportState != null)
            {
                viewportState.OnViewportChanged -= HandleViewportChanged;
            }
        }

        private void HandleViewportChanged()
        {
            Recompute(force: false);
        }

        /// <summary>
        /// scale = max(cameraWorldWidth / spriteWorldWidth, cameraWorldHeight
        /// / spriteWorldHeight), applied uniformly to both axes (never a
        /// separate X/Y scale, so the artwork's own aspect ratio is always
        /// preserved - cropping at the outer edges is the intended trade-off,
        /// never distortion). Centered on the camera every recompute so it
        /// tracks any camera movement too, though this project's gameplay
        /// camera does not currently move.
        /// </summary>
        private void Recompute(bool force)
        {
            if (targetCamera == null || spriteRenderer == null || spriteRenderer.sprite == null || !targetCamera.orthographic)
                return;

            float cameraWorldHeight = targetCamera.orthographicSize * 2f;
            float cameraWorldWidth = cameraWorldHeight * targetCamera.aspect;

            Vector2 spriteWorldSize = spriteRenderer.sprite.bounds.size;
            if (spriteWorldSize.x <= 0f || spriteWorldSize.y <= 0f)
                return;

            float scale = Mathf.Max(cameraWorldWidth / spriteWorldSize.x, cameraWorldHeight / spriteWorldSize.y);

            if (!force && Mathf.Approximately(scale, lastAppliedScale))
                return;

            transform.localScale = new Vector3(scale, scale, 1f);

            Vector3 cameraPosition = targetCamera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, transform.position.z);

            lastAppliedScale = scale;
        }
    }
}
