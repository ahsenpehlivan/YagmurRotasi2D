using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Gameplay2D;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Drives LevelSelectScene2D: populates one LevelButton2D card per entry in
    /// the campaign catalog (the single source of truth - CampaignLevelCatalog2D,
    /// the same Resources asset LevelManager2D.ResolveLevels() loads), and
    /// handles Back (-&gt; MainMenuScene2D) and level-card clicks (validate -&gt;
    /// CampaignSession2D.SetSelectedLevel -&gt; load GameScene2D). Locked/unlocked
    /// and star display are driven by GameProgress2D (HighestUnlockedLevel/
    /// GetBestStars) - the single source of truth for saved progress, read
    /// fresh here on every PopulateLevelCards() call. bestScore has no
    /// persisted store (GameProgress2D only ever saved best-stars-per-level,
    /// per the project's progress-system scope) and stays 0.
    /// </summary>
    public class LevelSelectController2D : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private RectTransform content;
        [SerializeField] private GameObject levelButtonPrefab;

        [SerializeField] private string mainMenuSceneName = "MainMenuScene2D";
        [SerializeField] private string gameplaySceneName = "GameScene2D";

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(HandleBackPressed);
        }

        private void Start()
        {
            PopulateLevelCards();
        }

        private void HandleBackPressed()
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void PopulateLevelCards()
        {
            if (content == null || levelButtonPrefab == null)
            {
                Debug.LogError("LevelSelectController2D: content or levelButtonPrefab is not assigned - no level cards can be created.");
                return;
            }

            // Defensive - clears any pre-existing children before repopulating,
            // so this is safe to call more than once (e.g. a future "refresh
            // after progress changed" call) without ever duplicating cards.
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }

            CampaignLevelCatalog2D catalog = LoadCatalog();
            if (catalog == null || catalog.levels == null || catalog.levels.Count == 0)
            {
                Debug.LogError("LevelSelectController2D: campaign catalog could not be loaded or is empty - no level cards were created.");
                return;
            }

            for (int i = 0; i < catalog.levels.Count; i++)
            {
                CampaignLevelDefinition2D definition = catalog.levels[i];
                int levelNumber = i + 1;

                if (definition == null)
                {
                    Debug.LogError($"LevelSelectController2D: catalog slot {levelNumber} is empty - skipping this card.");
                    continue;
                }

                GameObject instance = Instantiate(levelButtonPrefab, content);
                LevelButton2D card = instance.GetComponent<LevelButton2D>();
                if (card == null)
                {
                    Debug.LogError($"LevelSelectController2D: levelButtonPrefab has no LevelButton2D component - level {levelNumber} card is inert.");
                    continue;
                }

                int pipeCount = definition.pipes != null ? definition.pipes.Count : 0;
                string devInfo = $"{definition.gridWidth}x{definition.gridHeight} | {pipeCount} pipes | {definition.generatorVersion}";

                // Driven by GameProgress2D (the single saved-progress source of
                // truth): a level is unlocked once it's <= HighestUnlockedLevel,
                // and "completed" means it was ever finished at least once -
                // ScoreManager2D.CurrentStarRating can never fall below 1 after
                // a genuine completion, so earnedStars > 0 is a reliable
                // completed check.
                int earnedStars = GameProgress2D.GetBestStars(levelNumber);
                bool isUnlocked = levelNumber <= GameProgress2D.HighestUnlockedLevel;
                bool isCompleted = earnedStars > 0;

                card.Configure(
                    levelNumber, definition.gridWidth, definition.gridHeight,
                    isUnlocked, isCompleted, earnedStars, bestScore: 0,
                    devInfo, HandleLevelCardClicked);
            }
        }

        private void HandleLevelCardClicked(int levelNumber)
        {
            CampaignLevelCatalog2D catalog = LoadCatalog();
            bool exists = catalog != null && catalog.levels != null
                && levelNumber >= 1 && levelNumber <= catalog.levels.Count
                && catalog.levels[levelNumber - 1] != null;

            if (!exists)
            {
                Debug.LogError($"LevelSelectController2D: Level {levelNumber} does not exist in the campaign catalog - not loading gameplay.");
                return;
            }

            // Defense-in-depth on top of the card's own button.interactable=false
            // (set from isUnlocked in PopulateLevelCards) - a locked level can
            // never be loaded even if a click somehow still reaches here.
            if (levelNumber > GameProgress2D.HighestUnlockedLevel)
            {
                Debug.LogWarning($"LevelSelectController2D: Level {levelNumber} is locked - not loading gameplay.");
                return;
            }

            CampaignSession2D.SetSelectedLevel(levelNumber);
            SceneManager.LoadScene(gameplaySceneName);
        }

        private static CampaignLevelCatalog2D LoadCatalog()
        {
            return Resources.Load<CampaignLevelCatalog2D>(LevelManager2D.CatalogResourcePath);
        }
    }
}
