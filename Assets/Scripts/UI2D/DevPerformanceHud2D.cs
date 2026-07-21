#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using YagmurRotasi2D.Gameplay2D;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Phase 9B Part K: a simple optional development performance overlay -
    /// FPS, resolution, layout category, current level, active pipe count.
    /// The entire file is compiled out of release builds by the #if guard
    /// around it (not just hidden at runtime) - it does not exist at all in
    /// a non-development Web build.
    /// </summary>
    public class DevPerformanceHud2D : MonoBehaviour
    {
        [SerializeField] private WebViewportState2D viewportState;
        [SerializeField] private LevelManager2D levelManager;

        private float deltaTimeSmoothed;

        private void Update()
        {
            deltaTimeSmoothed += (Time.unscaledDeltaTime - deltaTimeSmoothed) * 0.1f;
        }

        private void OnGUI()
        {
            float fps = deltaTimeSmoothed > 0f ? 1f / deltaTimeSmoothed : 0f;
            string layoutCategory = viewportState != null ? viewportState.LayoutCategory.ToString() : "n/a";
            string levelInfo = levelManager != null ? $"{levelManager.CurrentLevelIndex + 1}/{levelManager.LevelCount}" : "n/a";
            int pipeCount = levelManager != null ? levelManager.ActivePipes.Count : 0;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(4, 4, 250, 104), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(10, 8, 250, 104));
            GUILayout.Label($"FPS: {fps:0.}");
            GUILayout.Label($"Resolution: {Screen.width}x{Screen.height}");
            GUILayout.Label($"Layout: {layoutCategory}");
            GUILayout.Label($"Level: {levelInfo}");
            GUILayout.Label($"Pipes: {pipeCount}");
            GUILayout.EndArea();
        }
    }
}
#endif
