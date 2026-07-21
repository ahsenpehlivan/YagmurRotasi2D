using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.Gameplay2D;
using YagmurRotasi2D.Visual2D;

namespace YagmurRotasi2D.UI2D
{
    public class UIManager2D : MonoBehaviour
    {
        [SerializeField] private LevelManager2D levelManager;
        [SerializeField] private FlowSolver2D flowSolver;
        [SerializeField] private ScoreManager2D scoreManager;
        [SerializeField] private WaterFlowAnimator2D waterFlowAnimator;
        [SerializeField] private SuccessFXController2D successFXController;

        [SerializeField] private Button startWaterButton;
        [SerializeField] private Button reloadButton;
        [SerializeField] private Text levelNameText;
        [SerializeField] private Text moveText;
        [SerializeField] private Text resultText;

        [Tooltip("Small top-HUD button next to LevelBadge. Opens inGameMenu below (guarded against opening during pipe-fill/success FX or while DedicatedSuccessPanel is visible).")]
        [SerializeField] private Button menuButton;

        [Tooltip("Legacy InfoPanel hierarchy - kept only for serialization/rollback compatibility. It is never activated as the live success UI anymore (see dedicatedSuccessPanel below); it stays inactive.")]
        [SerializeField] private GameObject infoPanel;
        [SerializeField] private Text infoTitleText;
        [SerializeField] private Text infoStarText;
        [SerializeField] private Text infoDescriptionText;
        [SerializeField] private Button infoNextLevelButton;
        [SerializeField] private Image[] starImages;
        private const float EmptyStarAlpha = 0.3f;

        [Tooltip("The live success screen (SafeAreaRoot/SuccessPanelHost/DedicatedSuccessPanel), a self-contained SuccessPanel2D.prefab instance. Owns its own title/body/star/button references independently of the legacy InfoPanel hierarchy above.")]
        [SerializeField] private SuccessPanelView2D dedicatedSuccessPanel;

        [Tooltip("The in-game pause menu (SafeAreaRoot/InGameMenuHost/DedicatedInGameMenu), opened by the top menuButton.")]
        [SerializeField] private InGameMenuView2D inGameMenu;

        private const string ReadyMessage = "Hazır";
        private const string SuccessMessage = "Su hedefe ulaştı!";
        private const string FailMessage = "Bağlantı eksik!";
        private const string MainMenuSceneName = "MainMenuScene2D";

        private void Awake()
        {
            // Subscribed in Awake (not Start) so these listeners are registered before
            // LevelManager2D.Start() fires its first OnLevelLoaded event.
            levelManager.OnLevelLoaded += HandleLevelLoaded;
            levelManager.OnPlayerMoved += HandlePlayerMoved;
            waterFlowAnimator.OnStageTextChanged += HandleStageTextChanged;
            waterFlowAnimator.OnAnimationComplete += HandleAnimationComplete;

            startWaterButton.onClick.AddListener(OnStartWaterPressed);
            reloadButton.onClick.AddListener(() => levelManager.ReloadCurrentLevel());
            // Routed through the same forwarding method as dedicatedSuccessPanel's
            // Next Level button, so both paths call the exact same level-advance
            // logic - no duplicated implementation.
            infoNextLevelButton.onClick.AddListener(HandleNextLevelRequested);

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(HandleMenuButtonPressed);
            }

            HideInfoPanel();
        }

        private void HandleMenuButtonPressed()
        {
            if (!CanOpenInGameMenu())
                return;

            LockGameplayForMenu();

            inGameMenu.Show(HandleContinueRequested, HandleRestartRequested, HandleMainMenuRequested);
        }

        /// <summary>Guards against opening the menu during pipe-fill/success FX (GameState2D.IsInputLocked stays true for that whole sequence) or while DedicatedSuccessPanel is visible (which only becomes true after input unlocks again, so it needs its own explicit check) or if the menu is already open.</summary>
        private bool CanOpenInGameMenu()
        {
            if (inGameMenu == null)
                return false;

            if (GameState2D.IsInputLocked)
                return false;

            if (dedicatedSuccessPanel != null && dedicatedSuccessPanel.IsVisible)
                return false;

            if (inGameMenu.IsVisible)
                return false;

            return true;
        }

        private void LockGameplayForMenu()
        {
            GameState2D.IsInputLocked = true;
            SetGameplayButtonsInteractable(false);
        }

        /// <summary>Safe to call unconditionally - CanOpenInGameMenu() only ever allowed the menu to open when nothing else needed the lock, and nothing else can grab it while the menu is open (opening it is the only path that can trigger a level change while it's up, and that path closes the menu itself first).</summary>
        private void UnlockGameplayAfterMenu()
        {
            GameState2D.IsInputLocked = false;
            SetGameplayButtonsInteractable(true);
        }

        /// <summary>Devam Et: closes the menu and restores input. Does not touch pipes, move count, score or saved progress.</summary>
        private void HandleContinueRequested()
        {
            inGameMenu.Hide();
            UnlockGameplayAfterMenu();
        }

        /// <summary>Bölümü Yeniden Başlat: routed through the exact same restart path as the existing ReloadButton (levelManager.ReloadCurrentLevel()) - HandleLevelLoaded (already subscribed) resets move count, pipe visuals, flowers, ducks, DedicatedSuccessPanel and input/button state, so none of that is duplicated here. Reloads the SAME level, not level 1, and never touches saved progress.</summary>
        private void HandleRestartRequested()
        {
            inGameMenu.Hide();
            levelManager.ReloadCurrentLevel();
        }

        /// <summary>Ana Menüye Dön: progress is already saved incrementally elsewhere (GameProgress2D.SetCurrentLevel/MarkLevelCompleted at their own trigger points) - nothing is reset here. Loads MainMenuScene2D non-additively; DedicatedInGameMenu lives inside GameScene2D (not DontDestroyOnLoad) so it's simply destroyed with the rest of the scene, never duplicated.</summary>
        private void HandleMainMenuRequested()
        {
            inGameMenu.Hide();
            SceneManager.LoadScene(MainMenuSceneName);
        }

        /// <summary>The single implementation of "advance to the next level" - used by both the legacy InfoNextLevelButton and dedicatedSuccessPanel's Next Level button.</summary>
        private void HandleNextLevelRequested()
        {
            // The just-completed level's stars were already recorded in
            // HandleAnimationComplete; this only advances the saved "current
            // level" pointer, clamped so completing the final level keeps
            // progress safely pinned there instead of requesting a nonexistent
            // next level.
            int nextLevelNumber = Mathf.Min(levelManager.CurrentLevelIndex + 2, levelManager.LevelCount);
            GameProgress2D.SetCurrentLevel(nextLevelNumber);

            bool isFinalLevel = levelManager.CurrentLevelIndex + 1 >= levelManager.LevelCount;
            if (isFinalLevel)
            {
                // Final production level (dynamically whichever one that is -
                // Level 6 as of Phase 7F.4): reload it in place rather than
                // LoadNextLevel()'s modulo wrap-around to Level 1, and never
                // request a nonexistent next level.
                levelManager.ReloadCurrentLevel();
            }
            else
            {
                levelManager.LoadNextLevel();
            }
        }

        private void OnStartWaterPressed()
        {
            if (GameState2D.IsInputLocked)
                return;

            ResetHighlights();

            FlowSolveResult2D result = flowSolver.Solve(
                levelManager.SourceCell,
                levelManager.SourceOutputDirection,
                levelManager.TargetCell,
                levelManager.TargetInputDirection);

            if (result.IsSuccess)
            {
                // Score is still calculated (StarCount depends on it) but is never
                // shown to the player - only the resulting star count is displayed.
                // ReachableTiles.Count is the branching-aware equivalent of the old
                // linear path length (same value for any Straight/Corner-only route).
                scoreManager.CalculateSuccess(
                    result.ReachableTiles.Count, levelManager.CurrentTwoStarScore, levelManager.CurrentThreeStarScore);

                SetGameplayButtonsInteractable(false);
                waterFlowAnimator.PlaySuccess(result.FlowSteps, successFXController);
            }
            else
            {
                scoreManager.RegisterWrongAttempt();
                resultText.text = FailMessage;
            }
        }

        private void HandleStageTextChanged(string stageText)
        {
            resultText.text = stageText;
        }

        private void HandleAnimationComplete()
        {
            resultText.text = SuccessMessage;
            SetGameplayButtonsInteractable(true);

            // Recorded at the moment of completion (not when Next Level is
            // pressed) so the earned stars persist even if the player closes the
            // app while looking at the success panel without advancing.
            GameProgress2D.MarkLevelCompleted(levelManager.CurrentLevelIndex + 1, scoreManager.StarCount);

            if (dedicatedSuccessPanel != null)
            {
                dedicatedSuccessPanel.Show(
                    scoreManager.StarCount,
                    "Tebrikler!",
                    levelManager.CurrentInfoText,
                    HandleNextLevelRequested);
            }
        }

        private void HandlePlayerMoved()
        {
            UpdateMoveText();
        }

        private void HandleLevelLoaded()
        {
            // Guards against a level change happening mid-sequence (e.g. Reload/Next):
            // stops the coroutine and unlocks input.
            waterFlowAnimator.CancelActiveAnimation();

            // Cancels any in-progress 6-second flower/duck sequence (no stale
            // callback can open InfoPanel afterwards) and resets flowers to frame 0
            // / hides ducks immediately.
            if (successFXController != null)
            {
                successFXController.ResetFX();
            }

            resultText.text = ReadyMessage;

            if (levelNameText != null)
            {
                levelNameText.text = $"Bölüm {levelManager.CurrentLevelIndex + 1}";
            }

            UpdateMoveText();
            HideInfoPanel();
            UpdateStarImages(0);
            if (dedicatedSuccessPanel != null)
            {
                dedicatedSuccessPanel.Hide();
            }
            // Defensive - HandleRestartRequested already hides the menu itself
            // before reloading, but this guards any other future path that
            // reloads the level while the menu happens to be open.
            if (inGameMenu != null)
            {
                inGameMenu.Hide();
            }
            ResetHighlights();
            SetGameplayButtonsInteractable(true);
        }

        private void UpdateMoveText()
        {
            if (moveText != null)
            {
                moveText.text = $"Hamle: {scoreManager.MoveCount}";
            }
        }

        private void SetGameplayButtonsInteractable(bool interactable)
        {
            startWaterButton.interactable = interactable;
            reloadButton.interactable = interactable;
        }

        private void UpdateStarImages(int starCount)
        {
            if (starImages == null)
                return;

            int clampedCount = Mathf.Clamp(starCount, 0, 3);
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null)
                    continue;

                Color color = starImages[i].color;
                color.a = i < clampedCount ? 1f : EmptyStarAlpha;
                starImages[i].color = color;
            }
        }

        private void HideInfoPanel()
        {
            if (infoPanel != null)
            {
                infoPanel.SetActive(false);
            }
        }

        private void ResetHighlights()
        {
            foreach (PipeTile2D pipe in levelManager.ActivePipes)
            {
                pipe.ResetWaterFlowVisual();
            }
        }
    }
}
