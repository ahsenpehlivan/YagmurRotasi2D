using System;
using UnityEngine;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>Phase 9B layout category, derived purely from current width/height/aspect - see WebViewportState2D.Classify.</summary>
    public enum WebLayoutCategory2D
    {
        PortraitBlocked,
        CompactLandscape,
        StandardLandscape,
        WideLandscape,
        UltraWide
    }

    /// <summary>
    /// Phase 9B shared responsive foundation: the single source of truth for
    /// "how big/what shape is the browser viewport right now" in one scene.
    /// One instance per scene (MainMenuScene2D/LevelSelectScene2D/GameScene2D
    /// each get their own via the Phase 9B scene builders) - deliberately NOT
    /// a cross-scene DontDestroyOnLoad singleton, since nothing here needs to
    /// survive a scene load and a fresh instance per scene is simpler and
    /// avoids any stale-reference risk.
    ///
    /// Follows the exact same proven idiom SafeAreaFitter2D already uses
    /// (Assets/Scripts/UI2D/SafeAreaFitter2D.cs): a cheap Update() equality
    /// check against the last known Screen.width/Screen.height, with the real
    /// recompute-and-notify work only running on an ACTUAL change - never a
    /// full per-frame rebuild, never a scene reload.
    /// </summary>
    public class WebViewportState2D : MonoBehaviour
    {
        [Header("Layout category thresholds (reference: 1920x1080 primary design resolution)")]
        [SerializeField] private float portraitBlockedMaxAspect = 1.2f;
        [SerializeField] private int portraitBlockedMinWidth = 640;
        [SerializeField] private int compactLandscapeMaxWidth = 1279;
        [SerializeField] private int standardLandscapeMaxWidth = 1599;
        [SerializeField] private int wideLandscapeMaxWidth = 2199;

        public event Action<WebLayoutCategory2D> OnLayoutCategoryChanged;
        public event Action OnViewportChanged;

        public int ViewportWidth { get; private set; }
        public int ViewportHeight { get; private set; }
        public float AspectRatio { get; private set; }
        public WebLayoutCategory2D LayoutCategory { get; private set; }

        private int lastWidth = -1;
        private int lastHeight = -1;

        private void OnEnable()
        {
            Recompute();
        }

        private void Update()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight)
                return;

            Recompute();
        }

        private void Recompute()
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);

            if (width == lastWidth && height == lastHeight)
                return;

            lastWidth = width;
            lastHeight = height;

            ViewportWidth = width;
            ViewportHeight = height;
            AspectRatio = width / (float)height;

            WebLayoutCategory2D previousCategory = LayoutCategory;
            LayoutCategory = Classify(width, height, AspectRatio);

            OnViewportChanged?.Invoke();
            if (LayoutCategory != previousCategory)
            {
                OnLayoutCategoryChanged?.Invoke(LayoutCategory);
            }
        }

        private WebLayoutCategory2D Classify(int width, int height, float aspect)
        {
            if (aspect < portraitBlockedMaxAspect || height > width || width < portraitBlockedMinWidth)
            {
                return WebLayoutCategory2D.PortraitBlocked;
            }

            if (width <= compactLandscapeMaxWidth) return WebLayoutCategory2D.CompactLandscape;
            if (width <= standardLandscapeMaxWidth) return WebLayoutCategory2D.StandardLandscape;
            if (width <= wideLandscapeMaxWidth) return WebLayoutCategory2D.WideLandscape;
            return WebLayoutCategory2D.UltraWide;
        }

        /// <summary>Editor/testing helper: recomputes immediately instead of waiting for the next Update() - used by the Phase 9B Web Layout Validator to query what category a given resolution WOULD classify as, without needing to actually resize the Game view.</summary>
        public static WebLayoutCategory2D ClassifyStatic(int width, int height, float portraitBlockedMaxAspect = 1.2f, int portraitBlockedMinWidth = 640,
            int compactLandscapeMaxWidth = 1279, int standardLandscapeMaxWidth = 1599, int wideLandscapeMaxWidth = 2199)
        {
            float aspect = width / (float)Mathf.Max(1, height);
            if (aspect < portraitBlockedMaxAspect || height > width || width < portraitBlockedMinWidth)
            {
                return WebLayoutCategory2D.PortraitBlocked;
            }
            if (width <= compactLandscapeMaxWidth) return WebLayoutCategory2D.CompactLandscape;
            if (width <= standardLandscapeMaxWidth) return WebLayoutCategory2D.StandardLandscape;
            if (width <= wideLandscapeMaxWidth) return WebLayoutCategory2D.WideLandscape;
            return WebLayoutCategory2D.UltraWide;
        }
    }
}
