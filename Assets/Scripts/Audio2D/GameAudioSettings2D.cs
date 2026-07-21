using UnityEngine;

namespace YagmurRotasi2D.Audio2D
{
    /// <summary>
    /// Static PlayerPrefs-backed Music/SFX preference storage. Only stores the
    /// on/off state - no AudioClip or actual playback is implemented here, by
    /// design (reserved for a later audio-system phase). Both default to
    /// enabled and persist across scene loads and app restarts.
    /// </summary>
    public static class GameAudioSettings2D
    {
        private const string MusicEnabledKey = "YagmurRotasi.Settings.MusicEnabled";
        private const string SfxEnabledKey = "YagmurRotasi.Settings.SfxEnabled";

        public static bool MusicEnabled => PlayerPrefs.GetInt(MusicEnabledKey, 1) != 0;

        public static bool SfxEnabled => PlayerPrefs.GetInt(SfxEnabledKey, 1) != 0;

        public static void SetMusicEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetSfxEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(SfxEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ToggleMusic()
        {
            SetMusicEnabled(!MusicEnabled);
        }

        public static void ToggleSfx()
        {
            SetSfxEnabled(!SfxEnabled);
        }
    }
}
