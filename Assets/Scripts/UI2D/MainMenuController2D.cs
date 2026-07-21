using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.Audio2D;
using YagmurRotasi2D.Gameplay2D;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Drives MainMenuScene2D: Oyuna Başla (resume saved progress into
    /// GameScene2D), Ayarlar (Music/SFX toggle panel) and Level Sıfırla (a
    /// confirmation panel before actually resetting progress). Listeners are
    /// registered exactly once in Awake - opening/closing panels never
    /// re-registers them.
    /// </summary>
    public class MainMenuController2D : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button resetProgressButton;

        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject resetConfirmationPanel;

        [SerializeField] private Button musicToggleButton;
        [SerializeField] private Button sfxToggleButton;
        [SerializeField] private Text musicToggleText;
        [SerializeField] private Text sfxToggleText;
        [SerializeField] private Button settingsCloseButton;

        [SerializeField] private Button confirmResetButton;
        [SerializeField] private Button cancelResetButton;

        [SerializeField] private string gameplaySceneName = "GameScene2D";

        private bool listenersRegistered;

        private void Awake()
        {
            RegisterListenersOnce();

            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (resetConfirmationPanel != null) resetConfirmationPanel.SetActive(false);

            RefreshToggleTexts();

            if (playButton != null) playButton.interactable = true;
        }

        private void RegisterListenersOnce()
        {
            if (listenersRegistered)
                return;

            if (playButton != null) playButton.onClick.AddListener(HandlePlayPressed);
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettingsPressed);
            if (resetProgressButton != null) resetProgressButton.onClick.AddListener(HandleResetProgressPressed);

            if (musicToggleButton != null) musicToggleButton.onClick.AddListener(HandleMusicTogglePressed);
            if (sfxToggleButton != null) sfxToggleButton.onClick.AddListener(HandleSfxTogglePressed);
            if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(HandleSettingsClosePressed);

            if (confirmResetButton != null) confirmResetButton.onClick.AddListener(HandleConfirmResetPressed);
            if (cancelResetButton != null) cancelResetButton.onClick.AddListener(HandleCancelResetPressed);

            listenersRegistered = true;
        }

        private void HandlePlayPressed()
        {
            // Prevents a double-click from triggering a second LoadScene call.
            if (playButton != null) playButton.interactable = false;

            // GameProgress2D.CurrentLevel already defaults safely to 1 and is
            // clamped to the valid level range - LevelManager2D.Start() reads it
            // directly when GameScene2D loads, so nothing further needs to be
            // stored here.
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void HandleSettingsPressed()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        private void HandleResetProgressPressed()
        {
            if (resetConfirmationPanel != null) resetConfirmationPanel.SetActive(true);
        }

        private void HandleMusicTogglePressed()
        {
            GameAudioSettings2D.ToggleMusic();
            RefreshToggleTexts();
        }

        private void HandleSfxTogglePressed()
        {
            GameAudioSettings2D.ToggleSfx();
            RefreshToggleTexts();
        }

        private void HandleSettingsClosePressed()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void HandleConfirmResetPressed()
        {
            // Only project progress keys are cleared - GameProgress2D never
            // touches GameAudioSettings2D's keys or calls PlayerPrefs.DeleteAll().
            GameProgress2D.ResetLevelProgress();
            if (resetConfirmationPanel != null) resetConfirmationPanel.SetActive(false);
        }

        private void HandleCancelResetPressed()
        {
            if (resetConfirmationPanel != null) resetConfirmationPanel.SetActive(false);
        }

        private void RefreshToggleTexts()
        {
            if (musicToggleText != null)
            {
                musicToggleText.text = GameAudioSettings2D.MusicEnabled ? "Müzik: Açık" : "Müzik: Kapalı";
            }

            if (sfxToggleText != null)
            {
                sfxToggleText.text = GameAudioSettings2D.SfxEnabled ? "Ses Efektleri: Açık" : "Ses Efektleri: Kapalı";
            }
        }
    }
}
