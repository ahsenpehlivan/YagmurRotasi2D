using UnityEngine;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Phase 9B: fullscreen is only ever requested from an explicit user click
    /// (ToggleFullscreen is wired to a Button.onClick, never called from
    /// Awake/Start/OnEnable anywhere in this project) - browsers refuse a
    /// fullscreen request that didn't originate from a user gesture anyway, so
    /// this is both the correct UX and the only approach that actually works
    /// on WebGL. One instance is shared per scene (Main Menu, Level Select
    /// header, Gameplay top bar each wire their own "Tam Ekran" button to the
    /// same simple API).
    /// </summary>
    public class WebFullscreenController2D : MonoBehaviour
    {
        public void ToggleFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Screen.fullScreen = !Screen.fullScreen;
#else
            // Editor/Standalone: Screen.fullScreen still works (harmless no-op
            // risk is only that some platforms silently ignore it), kept
            // identical so behavior is testable without a Web build.
            Screen.fullScreen = !Screen.fullScreen;
#endif
        }

        /// <summary>True when a fullscreen control makes sense to show at all - hidden/no-op outside supported contexts rather than shown-but-broken.</summary>
        public static bool IsSupported()
        {
#if UNITY_WEBGL || UNITY_STANDALONE || UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }
}
