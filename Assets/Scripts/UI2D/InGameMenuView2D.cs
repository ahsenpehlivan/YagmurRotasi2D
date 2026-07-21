using System;
using UnityEngine;
using UnityEngine.UI;
using YagmurRotasi2D.Audio2D;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Self-contained in-game pause/menu view, independent of DedicatedSuccessPanel
    /// and the old InfoPanel. Owns two internal pages (main menu / settings) and
    /// talks to GameAudioSettings2D directly for Music/SFX - UIManager2D never
    /// passes audio callbacks in, only the three action callbacks (Continue,
    /// Restart, Main Menu) that require gameplay-level coordination.
    /// </summary>
    public class InGameMenuView2D : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private GameObject mainMenuPage;
        [SerializeField] private GameObject settingsPage;
        [SerializeField] private Button musicToggleButton;
        [SerializeField] private Button sfxToggleButton;
        [SerializeField] private Text musicToggleText;
        [SerializeField] private Text sfxToggleText;
        [SerializeField] private Button settingsBackButton;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            // Internal navigation (settings/back) and the audio toggles never
            // change behavior between Show() calls, so they're registered exactly
            // once here - only the three external action buttons get their
            // listeners rebound per Show() call (see below).
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettingsPressed);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(HandleSettingsBackPressed);
            if (musicToggleButton != null) musicToggleButton.onClick.AddListener(HandleMusicTogglePressed);
            if (sfxToggleButton != null) sfxToggleButton.onClick.AddListener(HandleSfxTogglePressed);
        }

        public void Show(Action onContinue, Action onRestart, Action onReturnToMainMenu)
        {
            if (root != null) root.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            ShowMainMenuPage();
            RefreshToggleTexts();

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                if (onContinue != null) continueButton.onClick.AddListener(() => onContinue());
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                if (onRestart != null) restartButton.onClick.AddListener(() => onRestart());
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                if (onReturnToMainMenu != null) mainMenuButton.onClick.AddListener(() => onReturnToMainMenu());
            }

            IsVisible = true;
        }

        public void Hide()
        {
            if (continueButton != null) continueButton.onClick.RemoveAllListeners();
            if (restartButton != null) restartButton.onClick.RemoveAllListeners();
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveAllListeners();

            // Reset to the main page so the next Show() always opens on it,
            // never leaving the settings sub-page stuck open.
            ShowMainMenuPage();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (root != null) root.SetActive(false);

            IsVisible = false;
        }

        private void HandleSettingsPressed()
        {
            if (mainMenuPage != null) mainMenuPage.SetActive(false);
            if (settingsPage != null) settingsPage.SetActive(true);
            RefreshToggleTexts();
        }

        private void HandleSettingsBackPressed()
        {
            ShowMainMenuPage();
        }

        private void ShowMainMenuPage()
        {
            if (settingsPage != null) settingsPage.SetActive(false);
            if (mainMenuPage != null) mainMenuPage.SetActive(true);
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
