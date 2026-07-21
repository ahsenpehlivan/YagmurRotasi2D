using UnityEngine;
using UnityEngine.UI;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Phase 9B: the blocking "please rotate to landscape" overlay. Shows/hides
    /// itself purely by subscribing to WebViewportState2D.OnLayoutCategoryChanged
    /// (never polls independently) - appears when the category is
    /// PortraitBlocked and disappears automatically the moment a resize makes
    /// the viewport valid again, with no scene reload. Sits above every other
    /// UI element in the Canvas and carries its own full-screen raycastTarget
    /// Image so it blocks clicks to whatever is behind it while visible.
    /// </summary>
    public class LandscapeRequirementOverlay2D : MonoBehaviour
    {
        [SerializeField] private WebViewportState2D viewportState;
        [SerializeField] private GameObject root;
        [SerializeField] private Button fullscreenButton;
        [SerializeField] private WebFullscreenController2D fullscreenController;

        private void Awake()
        {
            if (viewportState == null) viewportState = FindFirstObjectByType<WebViewportState2D>();

            if (fullscreenButton != null && fullscreenController != null)
            {
                fullscreenButton.onClick.AddListener(fullscreenController.ToggleFullscreen);
            }
        }

        private void OnEnable()
        {
            if (viewportState != null)
            {
                viewportState.OnLayoutCategoryChanged += HandleLayoutCategoryChanged;
                Refresh(viewportState.LayoutCategory);
            }
        }

        private void OnDisable()
        {
            if (viewportState != null)
            {
                viewportState.OnLayoutCategoryChanged -= HandleLayoutCategoryChanged;
            }
        }

        private void HandleLayoutCategoryChanged(WebLayoutCategory2D category)
        {
            Refresh(category);
        }

        private void Refresh(WebLayoutCategory2D category)
        {
            if (root != null)
            {
                root.SetActive(category == WebLayoutCategory2D.PortraitBlocked);
            }
        }
    }
}
