using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YagmurRotasi2D.Audio2D
{
    /// <summary>
    /// Reusable volume-slider behavior, attached alongside a
    /// UnityEngine.UI.Slider in both the Main Menu and in-game settings
    /// panels - both instances drive the exact same GameAudioManager2D
    /// state (SetMusicVolume/SetSFXVolume), never a separate per-scene
    /// audio-settings store.
    ///
    /// Refresh-on-open and save-on-close/scene-exit both happen for free
    /// through normal Unity GameObject activation: this component's own
    /// GameObject is always a child of the settings panel/page, so
    /// OnEnable() fires exactly when that panel is shown
    /// (settingsPanel.SetActive(true)) and OnDisable() fires exactly when
    /// it's hidden OR the scene unloads - no explicit wiring needed in
    /// MainMenuController2D/InGameMenuView2D for either behavior.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class AudioVolumeSlider2D : MonoBehaviour, IPointerUpHandler
    {
        public enum VolumeType
        {
            Music,
            SFX
        }

        [SerializeField] private Slider slider;
        [SerializeField] private Text percentageText;
        [SerializeField] private VolumeType volumeType;

        private bool listenerRegistered;
        private bool rangeInitialized;

        private void OnEnable()
        {
            EnsureSliderRange();
            RegisterListenerOnce();
            RefreshFromCurrentVolume();
        }

        /// <summary>
        /// Persistence checkpoint - PlayerPrefs.Save() is deliberately never
        /// called from HandleValueChanged (every drag tick), only here: on
        /// pointer release (below) and whenever this GameObject is disabled
        /// (panel closed, page switched, or the whole scene unloading) -
        /// covering "settings panel close" and "scene change safety" with
        /// the same one hook, since both are ordinary GameObject
        /// deactivation from Unity's perspective.
        /// </summary>
        private void OnDisable()
        {
            GameAudioManager2D.Instance?.SaveVolumePreferences();
        }

        private void EnsureSliderRange()
        {
            if (rangeInitialized || slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            rangeInitialized = true;
        }

        private void RegisterListenerOnce()
        {
            if (listenerRegistered || slider == null)
                return;

            slider.onValueChanged.AddListener(HandleValueChanged);
            listenerRegistered = true;
        }

        /// <summary>Re-syncs the slider position and percentage label from GameAudioManager2D's current live/saved volume - called on every OnEnable (i.e. every time the settings panel/page becomes visible), so a value changed from a different panel (Main Menu vs in-game) is always reflected correctly.</summary>
        public void RefreshFromCurrentVolume()
        {
            float volume = GetCurrentVolume();

            if (slider != null)
            {
                // SetValueWithoutNotify - refreshing the display must never
                // itself re-trigger HandleValueChanged (which would re-apply
                // the same volume and needlessly touch PlayerPrefs).
                slider.SetValueWithoutNotify(volume);
            }

            UpdatePercentageText(volume);
        }

        private float GetCurrentVolume()
        {
            if (GameAudioManager2D.Instance == null)
            {
                // Manager not initialized yet (should be rare - the settings
                // panel starts hidden, so this only matters if somehow shown
                // before the DontDestroyOnLoad singleton exists). The next
                // OnEnable (or any later RefreshFromCurrentVolume call) will
                // pick up the real value once it does.
                return volumeType == VolumeType.Music ? GameAudioManager2D.DefaultMusicVolume : GameAudioManager2D.DefaultSFXVolume;
            }

            return volumeType == VolumeType.Music
                ? GameAudioManager2D.Instance.GetMusicVolume()
                : GameAudioManager2D.Instance.GetSFXVolume();
        }

        private void HandleValueChanged(float value)
        {
            if (GameAudioManager2D.Instance != null)
            {
                if (volumeType == VolumeType.Music)
                {
                    GameAudioManager2D.Instance.SetMusicVolume(value);
                }
                else
                {
                    GameAudioManager2D.Instance.SetSFXVolume(value);
                }
            }

            UpdatePercentageText(value);
        }

        private void UpdatePercentageText(float value)
        {
            if (percentageText != null)
            {
                percentageText.text = Mathf.RoundToInt(value * 100f) + "%";
            }
        }

        /// <summary>
        /// Drag-end checkpoint - saves immediately on release (rather than
        /// waiting for the panel to close), and previews the new SFX level
        /// with the existing one-shot UI click sound (never for Music - a
        /// click sound previewing music volume wouldn't make sense). This
        /// never duplicates UIButtonSound2D's per-click sound since that
        /// component is deliberately never attached to Slider objects.
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            GameAudioManager2D.Instance?.SaveVolumePreferences();

            if (volumeType == VolumeType.SFX)
            {
                GameAudioManager2D.Instance?.PlayUIClick();
            }
        }
    }
}
