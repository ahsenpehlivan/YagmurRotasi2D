using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.Education2D;
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

        [Tooltip("Phase 9B: top-bar 'Tam Ekran' button - only ever toggled from this explicit click, never automatically.")]
        [SerializeField] private Button fullscreenButton;
        [SerializeField] private WebFullscreenController2D fullscreenController;

        [Tooltip("Phase 9C: 'Ayarlar' button living inside the desktop ControlPanel - opens the exact same DedicatedInGameMenu as the top-bar MenuButton (HandleMenuButtonPressed), just a second entry point next to Suyu Başlat/Sıfırla so all three priority actions are visible in one place. Optional - null-checked, top MenuButton keeps working either way.")]
        [SerializeField] private Button settingsButton;

        [Tooltip("Phase 9C: ControlPanel's own level-name/grid-size/move-count labels (LevelDetails/StatsArea) - separate Text elements from the compact TopHUD badges (levelNameText/moveText) so both can show the same data without one field pointing two places at once.")]
        [SerializeField] private Text controlPanelLevelText;
        [SerializeField] private Text controlPanelGridSizeText;
        [SerializeField] private Text controlPanelStatsText;
        [SerializeField] private BoardManager2D boardManagerForDisplay;

        private const string ReadyMessage = "Hazır";
        private const string SuccessMessage = "Su hedefe ulaştı!";
        private const string FailMessage = "Bağlantı eksik!";

        /// <summary>Phase 9B: shown instead of FailMessage specifically when the layout IS a fully connected, leak-free route but does not match the level's intended solution - distinguishes "nothing is connected yet" from "connected, but not the right way" without exposing the underlying mask/coordinate diagnostics (those stay in the Debug.Log below, Editor/Console only).</summary>
        private const string OrientationMismatchMessage = "Bazı borular doğru yönde değil.";

        // Phase 9A: normal gameplay Back (the in-game pause menu's former "Ana
        // Menüye Dön" button, now relabeled "Bölüm Seçimine Dön") and the
        // success panel's new second button both return to LevelSelectScene2D,
        // not MainMenuScene2D - see HandleReturnToLevelSelectRequested below.
        private const string LevelSelectSceneName = "LevelSelectScene2D";

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

            if (fullscreenButton != null && fullscreenController != null)
            {
                fullscreenButton.onClick.AddListener(fullscreenController.ToggleFullscreen);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(HandleMenuButtonPressed);
            }

            HideInfoPanel();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
        }

        /// <summary>
        /// Phase 9B web keyboard shortcuts: Escape opens/closes the pause menu,
        /// R resets the current level, Enter/Space starts the water - each
        /// gated by IsGameplayInputAvailable so a shortcut can never fire
        /// during water-flow animation, while the pause menu or success panel
        /// is open, or while a text-input UI element has focus (this project
        /// has no InputField anywhere today, but the guard is here regardless
        /// per spec - defensive, not currently load-bearing).
        /// </summary>
        private void HandleKeyboardShortcuts()
        {
            if (Keyboard.current == null)
                return;

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.GetComponent<InputField>() != null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                HandleEscapePressed();
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                HandleResetShortcut();
            }

            if (Keyboard.current.enterKey.wasPressedThisFrame
                || Keyboard.current.numpadEnterKey.wasPressedThisFrame
                || Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                HandleStartWaterShortcut();
            }
        }

        private void HandleEscapePressed()
        {
            if (inGameMenu == null)
                return;

            if (inGameMenu.IsVisible)
            {
                HandleContinueRequested();
            }
            else if (CanOpenInGameMenu())
            {
                LockGameplayForMenu();
                inGameMenu.Show(HandleContinueRequested, HandleRestartRequested, HandleReturnToLevelSelectRequested);
            }
        }

        /// <summary>Shared gate for both keyboard shortcuts (R/Enter/Space) - true only during normal, uninterrupted gameplay (not during water-flow animation, not while the pause menu or success panel is open).</summary>
        private bool IsGameplayInputAvailable()
        {
            if (GameState2D.IsInputLocked) return false;
            if (inGameMenu != null && inGameMenu.IsVisible) return false;
            if (dedicatedSuccessPanel != null && dedicatedSuccessPanel.IsVisible) return false;
            return true;
        }

        private void HandleResetShortcut()
        {
            if (!IsGameplayInputAvailable())
                return;

            levelManager.ReloadCurrentLevel();
        }

        private void HandleStartWaterShortcut()
        {
            if (!IsGameplayInputAvailable())
                return;

            if (startWaterButton != null && !startWaterButton.interactable)
                return;

            OnStartWaterPressed();
        }

        private void HandleMenuButtonPressed()
        {
            if (!CanOpenInGameMenu())
                return;

            LockGameplayForMenu();

            inGameMenu.Show(HandleContinueRequested, HandleRestartRequested, HandleReturnToLevelSelectRequested);
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

        /// <summary>Bölüm Seçimine Dön: progress is already saved incrementally elsewhere (GameProgress2D.SetCurrentLevel/MarkLevelCompleted at their own trigger points) - nothing is reset here. Loads LevelSelectScene2D non-additively (Phase 9A - normal gameplay Back no longer returns all the way to MainMenuScene2D); DedicatedInGameMenu/DedicatedSuccessPanel live inside GameScene2D (not DontDestroyOnLoad) so they're simply destroyed with the rest of the scene, never duplicated. Shared by the in-game pause menu, the success panel's "Bölüm Seçimine Dön" button, and the gameplay top bar's direct Back button (GameSceneBackButtonForwarder2D, via ReturnToLevelSelect() below) - one implementation, like HandleNextLevelRequested below. waterFlowAnimator.CancelActiveAnimation() is safe to call even when nothing is running (its own doc guarantee) - required so the top-bar Back button can never leave GameState2D.IsInputLocked stuck true if pressed mid water-flow animation; the pause-menu path can never actually reach this method while animation is running (CanOpenInGameMenu already refuses to open the menu while GameState2D.IsInputLocked is true), so it stays a no-op there.</summary>
        private void HandleReturnToLevelSelectRequested()
        {
            waterFlowAnimator.CancelActiveAnimation();
            inGameMenu.Hide();
            SceneManager.LoadScene(LevelSelectSceneName);
        }

        /// <summary>Public entry point so any button that should behave exactly like "Bölüm Seçimine Dön" (currently only the gameplay top bar's direct Back button) can trigger it without UIManager2D needing a new serialized field for that one extra button reference.</summary>
        public void ReturnToLevelSelect()
        {
            HandleReturnToLevelSelectRequested();
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

        /// <summary>
        /// The complete campaign runtime success rule (Phase 8 Fast-Track QA):
        /// FlowSolver2D.Solve() alone only proves the CURRENT rotation state is
        /// a connected, leak-free Source-to-Target route - it has no notion of
        /// "every placed pipe must belong to that route" or "this must be the
        /// level's INTENDED solution", so success additionally requires every
        /// placed pipe to be reached (a disconnected filler/branch pipe left
        /// out of the route is not success) and every pipe's current logical
        /// orientation to match the level asset's stored solved orientation
        /// (LevelManager2D.ValidateCurrentMatchesSolved - logical port masks,
        /// not raw rotationIndex). Deliberately does not touch FlowSolver2D
        /// itself - this campaign-specific rule is layered on top of its
        /// result here instead.
        /// </summary>
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

            bool allPipesReachable = result.ReachableTiles.Count == levelManager.ActivePipes.Count;
            bool connectivityOk = result.TargetReached && !result.HasLeak && allPipesReachable;
            bool orientationOk = levelManager.ValidateCurrentMatchesSolved(out int mismatchCount, out Vector2Int firstMismatchCell, out int currentMask, out int expectedMask);
            bool success = connectivityOk && orientationOk;

            if (!success)
            {
                // Editor/Console-only diagnostics (raw masks/coordinates never
                // reach the player - see the child-friendly resultText below).
                Debug.Log($"CampaignLevelValidation: Level {levelManager.CurrentLevelIndex + 1} did not pass the full success check - " +
                    $"TargetReached={result.TargetReached}, HasLeak={result.HasLeak}, " +
                    $"Reachable={result.ReachableTiles.Count}/{levelManager.ActivePipes.Count}, " +
                    $"OrientationMismatches={mismatchCount}, FirstMismatchCell={firstMismatchCell}, " +
                    $"CurrentMask={currentMask}, ExpectedMask={expectedMask}.");
            }

            if (success)
            {
                // CurrentScore is purely a numeric display value (Skor: on the
                // success panel) - stars are never derived from it. The one
                // and only star source is scoreManager.CurrentStarRating,
                // already kept live and up to date by every accepted move
                // (see ScoreManager2D.RegisterMove/ResetState). ReachableTiles
                // .Count is the branching-aware equivalent of the old linear
                // path length (same value for any Straight/Corner-only route).
                scoreManager.CalculateSuccess(result.ReachableTiles.Count);

                SetGameplayButtonsInteractable(false);
                waterFlowAnimator.PlaySuccess(result.FlowSteps, successFXController);
            }
            else
            {
                scoreManager.RegisterWrongAttempt();
                // Distinguishes "nothing is connected yet" from "connected, but
                // not the level's intended solution" - both are visible,
                // child-friendly UI feedback, never just a Console log.
                resultText.text = connectivityOk && !orientationOk ? OrientationMismatchMessage : FailMessage;
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
            // app while looking at the success panel without advancing. No
            // further move can happen between success and this point (input
            // stays locked through the whole flow/success animation), so
            // CurrentStarRating here is exactly the live preview the player
            // last saw - the single authoritative earned-stars value, never a
            // second formula. GameProgress2D.MarkLevelCompleted itself still
            // keeps the best historical result (Mathf.Max-style) untouched.
            int earnedStars = scoreManager.CurrentStarRating;
            GameProgress2D.MarkLevelCompleted(levelManager.CurrentLevelIndex + 1, earnedStars);

            if (dedicatedSuccessPanel != null)
            {
                // Phase 9A: Level 100 (the last entry in the campaign catalog's
                // own ordering, never a hardcoded count) has no next level -
                // SuccessPanelView2D hides NextLevelButton entirely when false.
                bool hasNextLevel = levelManager.CurrentLevelIndex + 1 < levelManager.LevelCount;

                dedicatedSuccessPanel.Show(
                    earnedStars,
                    "Tebrikler!",
                    levelManager.CurrentInfoText,
                    scoreManager.CurrentScore,
                    scoreManager.MoveCount,
                    hasNextLevel,
                    HandleNextLevelRequested,
                    HandleRestartRequested,
                    HandleReturnToLevelSelectRequested);

                // Educational water-awareness message for the just-completed
                // level (looked up by level number, never by earnedStars/
                // score) - overwrites the existing gray body area Show()
                // just set, reusing that same field/hierarchy rather than a
                // separate message section. Purely a text assignment: does
                // not influence earnedStars, does not touch GameProgress2D,
                // and never delays this call.
                WaterMessageEntry2D waterMessage = WaterMessageCatalog2D.GetMessageForLevel(levelManager.CurrentLevelIndex + 1);
                dedicatedSuccessPanel.SetWaterMessage(waterMessage.message);
            }
        }

        /// <summary>
        /// The single update path for every move-driven HUD element - fired
        /// only from LevelManager2D.OnPlayerMoved, which itself only fires
        /// after ScoreManager2D.RegisterMove() has already run (see
        /// LevelManager2D.HandlePipeRotatedByPlayer), so scoreManager.
        /// MoveCount/CurrentStarRating are always already up to date here.
        /// Both move counters (top moveText and lower-right
        /// controlPanelStatsText) and the lower-right live star preview all
        /// read the same ScoreManager2D state - none of them keep a local
        /// count of their own.
        /// </summary>
        private void HandlePlayerMoved()
        {
            UpdateMoveText();
            UpdateStarImages(scoreManager.CurrentStarRating);
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

            if (controlPanelLevelText != null)
            {
                controlPanelLevelText.text = $"Bölüm {levelManager.CurrentLevelIndex + 1}";
            }

            if (controlPanelGridSizeText != null && boardManagerForDisplay != null)
            {
                controlPanelGridSizeText.text = $"{boardManagerForDisplay.Width}×{boardManagerForDisplay.Height}";
            }

            UpdateMoveText();
            HideInfoPanel();
            // scoreManager.ResetState() (already called by LevelManager2D.
            // LoadLevel before OnLevelLoaded fires) always leaves
            // CurrentStarRating at 3 for a freshly-loaded/reloaded level -
            // read live here rather than hardcoding 3, so this stays correct
            // even if ScoreManager2D's reset behavior ever changes.
            UpdateStarImages(scoreManager.CurrentStarRating);
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

            if (controlPanelStatsText != null)
            {
                controlPanelStatsText.text = $"Hamle: {scoreManager.MoveCount}";
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
