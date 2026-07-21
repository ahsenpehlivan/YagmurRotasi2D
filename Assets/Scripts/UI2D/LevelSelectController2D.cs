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
    /// CampaignSession2D.SetSelectedLevel -&gt; load GameScene2D). Every level is
    /// currently unlocked and never completed (Phase 9A Part F - no save system
    /// yet); see LevelButton2D.Configure for the forward-compatible API shape.
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

                // Part F preparation: isUnlocked/isCompleted/earnedStars/bestScore
                // are already real parameters on LevelButton2D.Configure - always
                // true/false/0/0 for now (no save system yet), never PlayerPrefs.
                card.Configure(
                    levelNumber, definition.gridWidth, definition.gridHeight,
                    isUnlocked: true, isCompleted: false, earnedStars: 0, bestScore: 0,
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

            CampaignSession2D.SetSelectedLevel(levelNumber);
            SceneManager.LoadScene(gameplaySceneName);
        }

        private static CampaignLevelCatalog2D LoadCatalog()
        {
            return Resources.Load<CampaignLevelCatalog2D>(LevelManager2D.CatalogResourcePath);
        }
    }
}
