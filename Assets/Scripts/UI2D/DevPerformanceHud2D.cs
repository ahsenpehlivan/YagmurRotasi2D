#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YagmurRotasi2D.Gameplay2D;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Phase 9C: a proper UGUI (not OnGUI) developer performance overlay -
    /// FPS, resolution, layout category, current level, active pipe count.
    ///
    /// Root cause of the old version's "covers gameplay elements" bug:
    /// IMGUI's OnGUI draws in RAW SCREEN PIXEL space (top-left origin),
    /// completely ignoring CanvasScaler/Canvas layout - its fixed
    /// Rect(4, 4, 250, 104) landed directly on top of TopHUD's
    /// BackButton/LevelBadge at every resolution, and the old script had no
    /// visibility gate at all (always drawn whenever its GameObject was
    /// active, which the builder always made true in Editor/Development
    /// builds). This version is a normal top-right anchored UGUI panel,
    /// hidden by default, toggled with F3, with raycastTarget false on every
    /// visual/text piece so it can never intercept a click. It is parented as
    /// a top-level sibling of SafeAreaRoot directly under Canvas (see
    /// GameSceneWebLayoutBuilder2D.BuildDevPerformanceHud) - outside every
    /// LayoutGroup in the scene, so it never reserves layout space for
    /// anything else.
    ///
    /// The script's own GameObject stays active/enabled at all times (so
    /// Update() keeps listening for F3 even while hidden); only the child
    /// visual root (panelRoot) is toggled.
    ///
    /// The whole file is compiled out of release builds by the #if guard
    /// around it (not just hidden at runtime) - it does not exist at all in
    /// a non-development Web build.
    /// </summary>
    public class DevPerformanceHud2D : MonoBehaviour
    {
        [SerializeField] private WebViewportState2D viewportState;
        [SerializeField] private LevelManager2D levelManager;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text statsText;

        private float deltaTimeSmoothed;
        private bool isVisible;

        private void Awake()
        {
            isVisible = false;
            ApplyVisibility();
        }

        private void Update()
        {
            deltaTimeSmoothed += (Time.unscaledDeltaTime - deltaTimeSmoothed) * 0.1f;

            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                isVisible = !isVisible;
                ApplyVisibility();
            }

            if (isVisible)
            {
                RefreshText();
            }
        }

        private void ApplyVisibility()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(isVisible);
            }
        }

        private void RefreshText()
        {
            if (statsText == null)
                return;

            float fps = deltaTimeSmoothed > 0f ? 1f / deltaTimeSmoothed : 0f;
            string layoutCategory = viewportState != null ? viewportState.LayoutCategory.ToString() : "n/a";
            string levelInfo = levelManager != null ? $"{levelManager.CurrentLevelIndex + 1}/{levelManager.LevelCount}" : "n/a";
            int pipeCount = levelManager != null ? levelManager.ActivePipes.Count : 0;

            statsText.text =
                $"FPS: {fps:0.}\n" +
                $"Resolution: {Screen.width}x{Screen.height}\n" +
                $"Layout: {layoutCategory}\n" +
                $"Level: {levelInfo}\n" +
                $"Pipes: {pipeCount}\n" +
                "(F3 to hide)";
        }
    }
}
#endif
