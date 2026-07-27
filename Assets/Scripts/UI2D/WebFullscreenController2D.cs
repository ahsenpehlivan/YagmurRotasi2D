using System.Runtime.InteropServices;
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
    ///
    /// On WebGL, Unity's own Screen.fullScreen setter does not reliably issue
    /// a real browser requestFullscreen() call synchronously within the
    /// click's call stack, so browsers silently refuse it. WebFullscreen2D.jslib
    /// calls requestFullscreen()/exitFullscreen() directly against the existing
    /// #unity-container/#unity-canvas element instead - this method must keep
    /// calling it directly (no coroutine, no delayed invocation) so the
    /// browser still sees it as originating from the user gesture.
    /// </summary>
    public class WebFullscreenController2D : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void YagmurRotasi_RequestFullscreen();

        [DllImport("__Internal")]
        private static extern void YagmurRotasi_ExitFullscreen();

        [DllImport("__Internal")]
        private static extern int YagmurRotasi_IsFullscreen();
#endif

        public void ToggleFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                if (YagmurRotasi_IsFullscreen() != 0)
                {
                    YagmurRotasi_ExitFullscreen();
                }
                else
                {
                    YagmurRotasi_RequestFullscreen();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"WebFullscreenController2D: fullscreen toggle failed - {e.Message}");
            }
#else
            // Editor/Standalone: no browser to talk to, Screen.fullScreen is the
            // correct (and only) mechanism here, kept so behavior is testable
            // without a Web build.
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
