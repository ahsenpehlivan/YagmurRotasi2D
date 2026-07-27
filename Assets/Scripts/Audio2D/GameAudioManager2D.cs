using UnityEngine;
using UnityEngine.InputSystem;

namespace YagmurRotasi2D.Audio2D
{
    /// <summary>
    /// Cross-scene audio playback: one persistent DontDestroyOnLoad instance
    /// with a MusicAudioSource (looping background track) and an
    /// SFXAudioSource (PlayOneShot for pipe/UI clicks). Enabled/disabled
    /// state is deliberately delegated to the EXISTING GameAudioSettings2D
    /// (Assets/Scripts/Audio2D/GameAudioSettings2D.cs) rather than a second,
    /// disconnected PlayerPrefs store - that class already backs the
    /// Music/SFX toggle buttons in MainMenuController2D and InGameMenuView2D,
    /// so routing through it here is what makes toggling music/SFX off in
    /// the existing Settings panel actually silence real playback instead of
    /// only changing a label. Only volume is new persisted state (no prior
    /// storage existed for it).
    /// </summary>
    public class GameAudioManager2D : MonoBehaviour
    {
        public static GameAudioManager2D Instance { get; private set; }

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource waterFlowSource;
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private AudioClip pipeClickClip;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip waterFlowClip;
        [SerializeField] private AudioClip duckClip;
        [SerializeField] private AudioClip sparkleClip;

        private const string MusicVolumeKey = "YagmurRotasi2D_MusicVolume";
        private const string SFXVolumeKey = "YagmurRotasi2D_SFXVolume";
        public const float DefaultMusicVolume = 0.35f;
        public const float DefaultSFXVolume = 0.8f;
        public const float DefaultWaterFlowVolume = 0.45f;

        // Set once music has genuinely been asked to start - PlayMusic() is
        // then a no-op for the rest of this play session (level reset, next
        // level, scene changes), so the same track is never restarted or
        // overlapped with a second instance.
        private bool musicStarted;

        // WebGL autoplay policies can silently block AudioSource.Play() until
        // a real user gesture occurs. When that happens this is set true and
        // Update() cheaply checks (no retry, no per-frame Play() call) for
        // the first pointer/key/touch press to safely resume - never logs
        // per-frame, never retries every frame.
        private bool awaitingFirstInteractionForMusic;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Safe duplicate teardown - a scene loaded with its own baked-in
                // GameAudioManager2D while a persistent one already exists from
                // an earlier scene (the expected steady state after the very
                // first scene load).
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ApplyStoredVolumes();
            ApplyMusicEnabledState();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("GameAudioManager2D: initialized.");
#endif
        }

        private void Start()
        {
            PlayMusic();
        }

        private void Update()
        {
            if (!awaitingFirstInteractionForMusic)
                return;

            bool interacted =
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame);

            if (!interacted)
                return;

            awaitingFirstInteractionForMusic = false;
            TryStartMusicSource();
        }

        /// <summary>Starts the background track exactly once for the whole play session - safe to call from every scene's Start()/Awake() (Main Menu, Level Select, Game Scene all do) since only the first call after the singleton is established ever actually starts playback.</summary>
        public void PlayMusic()
        {
            if (musicStarted || musicSource == null || musicClip == null)
                return;

            musicStarted = true;
            TryStartMusicSource();
        }

        private void TryStartMusicSource()
        {
            if (musicSource == null || musicSource.isPlaying)
                return;

            musicSource.Play();

            if (!musicSource.isPlaying)
            {
                // Blocked by a WebGL autoplay policy - wait for a genuine
                // user gesture (checked cheaply in Update, never retried
                // every frame) instead of erroring or spinning.
                awaitingFirstInteractionForMusic = true;
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("GameAudioManager2D: music started.");
#endif
        }

        /// <summary>
        /// Starts the looping water-flow sound - idempotent (a no-op if
        /// already playing or if SFX is currently disabled), so pressing
        /// Start Water repeatedly while input is locked, or any other
        /// accidental repeat call, can never restart the clip or stack a
        /// second overlapping loop. Callers are expected to call this only
        /// from the actual water-animation start point (WaterFlowAnimator2D),
        /// never from a button click directly.
        /// </summary>
        public void StartWaterFlow()
        {
            if (waterFlowSource == null || waterFlowClip == null)
                return;

            if (!GameAudioSettings2D.SfxEnabled)
                return;

            if (waterFlowSource.isPlaying)
                return;

            if (waterFlowSource.clip != waterFlowClip)
            {
                waterFlowSource.clip = waterFlowClip;
            }

            waterFlowSource.Play();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("GameAudioManager2D: water flow started.");
#endif
        }

        /// <summary>Safe to call even when nothing is playing (reset/reload/scene-exit/cancel all call this unconditionally).</summary>
        public void StopWaterFlow()
        {
            if (waterFlowSource == null || !waterFlowSource.isPlaying)
                return;

            waterFlowSource.Stop();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("GameAudioManager2D: water flow stopped.");
#endif
        }

        /// <summary>One-shot duck success sound - call exactly once from the actual duck animation start point (SuccessFXController2D.PlaySuccessFX), never from a reset/reopen path.</summary>
        public void PlayDuckSuccess()
        {
            if (!PlaySfx(duckClip))
                return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("GameAudioManager2D: duck success sound played.");
#endif
        }

        /// <summary>One-shot sparkle success sound - call exactly once from the actual flower/sparkle animation start point (SuccessFXController2D.PlaySuccessFX), never from a reset/reopen path.</summary>
        public void PlaySparkleSuccess()
        {
            if (!PlaySfx(sparkleClip))
                return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("GameAudioManager2D: sparkle success sound played.");
#endif
        }

        public void PlayPipeClick()
        {
            if (!PlaySfx(pipeClickClip))
                return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("GameAudioManager2D: pipe click played.");
#endif
        }

        public void PlayUIClick()
        {
            if (!PlaySfx(uiClickClip))
                return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("GameAudioManager2D: UI click played.");
#endif
        }

        private bool PlaySfx(AudioClip clip)
        {
            if (sfxSource == null || clip == null || !GameAudioSettings2D.SfxEnabled)
                return false;

            sfxSource.PlayOneShot(clip);
            return true;
        }

        public void SetMusicEnabled(bool isEnabled)
        {
            GameAudioSettings2D.SetMusicEnabled(isEnabled);
            ApplyMusicEnabledState();
        }

        public void SetSFXEnabled(bool isEnabled)
        {
            GameAudioSettings2D.SetSfxEnabled(isEnabled);

            if (!isEnabled)
            {
                // One-shot sounds (pipe/UI/duck/sparkle) already gate on
                // GameAudioSettings2D.SfxEnabled inside PlaySfx() before ever
                // playing, so they're implicitly muted from now on. The
                // looping water-flow source is different - it may already be
                // playing, so it must be stopped explicitly right here.
                StopWaterFlow();
            }
        }

        /// <summary>
        /// Clamps, applies to the live MusicAudioSource immediately, and
        /// writes to PlayerPrefs (SetFloat only - no disk flush here). Never
        /// restarts/re-triggers playback (only the .volume field changes),
        /// never creates a source. Intended to be called on every slider
        /// drag tick (AudioVolumeSlider2D) - callers must call
        /// SaveVolumePreferences() at a meaningful checkpoint (pointer
        /// release/panel close/scene exit) to persist to disk; calling this
        /// alone does not survive a hard refresh/crash.
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicVolumeKey, volume);
            if (musicSource != null) musicSource.volume = volume;
        }

        /// <summary>Same contract as SetMusicVolume, for SFX - also keeps the looping water-flow source proportional (ApplyWaterFlowVolume) so it never drifts out of sync with the SFX slider.</summary>
        public void SetSFXVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SFXVolumeKey, volume);
            if (sfxSource != null) sfxSource.volume = volume;
            ApplyWaterFlowVolume(volume);
        }

        /// <summary>The live music volume (from the AudioSource if already wired, otherwise the stored/default PlayerPrefs value) - never null/exception, safe to call whether or not this instance has finished initializing yet.</summary>
        public float GetMusicVolume()
        {
            return musicSource != null ? musicSource.volume : PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
        }

        /// <summary>The live SFX volume (from the AudioSource if already wired, otherwise the stored/default PlayerPrefs value).</summary>
        public float GetSFXVolume()
        {
            return sfxSource != null ? sfxSource.volume : PlayerPrefs.GetFloat(SFXVolumeKey, DefaultSFXVolume);
        }

        /// <summary>
        /// The explicit persistence checkpoint for SetMusicVolume/SetSFXVolume's
        /// already-written PlayerPrefs values - a plain PlayerPrefs.Save(), safe
        /// to call as often as needed (e.g. every slider pointer-release, every
        /// settings-panel close, every scene exit) since Save() itself is a
        /// cheap no-op when nothing changed since the last flush.
        /// </summary>
        public void SaveVolumePreferences()
        {
            PlayerPrefs.Save();
        }

        private void ApplyStoredVolumes()
        {
            float sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, DefaultSFXVolume);
            if (musicSource != null) musicSource.volume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
            if (sfxSource != null) sfxSource.volume = sfxVolume;
            ApplyWaterFlowVolume(sfxVolume);
        }

        /// <summary>Keeps the looping water-flow source's volume proportional to its own authored default (0.45, quieter than one-shot SFX by design) while still tracking the same SFX volume setting/slider - so turning SFX down/up moves both together without water flow ever becoming louder than intended.</summary>
        private void ApplyWaterFlowVolume(float sfxVolume)
        {
            if (waterFlowSource == null)
                return;

            float scale = DefaultSFXVolume > 0f ? sfxVolume / DefaultSFXVolume : 1f;
            waterFlowSource.volume = DefaultWaterFlowVolume * scale;
        }

        /// <summary>Mute (not Stop/Play) is the enable/disable mechanism - toggling music off then back on resumes from the same position instead of restarting, and never creates a second overlapping playback.</summary>
        private void ApplyMusicEnabledState()
        {
            if (musicSource == null)
                return;

            musicSource.mute = !GameAudioSettings2D.MusicEnabled;
        }
    }
}
