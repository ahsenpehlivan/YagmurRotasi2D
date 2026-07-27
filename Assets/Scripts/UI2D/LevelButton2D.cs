using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// One dynamically-instantiated campaign level card in LevelSelectScene2D -
    /// there is no manually-authored button per level; LevelSelectController2D
    /// instantiates one of these per catalog entry at runtime. The API already
    /// accepts isUnlocked/isCompleted/earnedStars/bestScore (Phase 9A Part F -
    /// preparation for the later save system) even though every level is
    /// currently always unlocked/never completed/0 stars - no PlayerPrefs, no
    /// save-system code lives here yet.
    ///
    /// Readability fix: levelNumberText/gridSizeText/devInfoText are
    /// TextMeshProUGUI (TMP_Text), not legacy Text - see
    /// LevelButtonPrefabBuilder2D's doc comment for why (SDF rendering stays
    /// crisp under CanvasScaler, legacy Text's bitmap glyphs do not).
    /// </summary>
    public class LevelButton2D : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text levelNumberText;
        [SerializeField] private TMP_Text gridSizeText;
        [SerializeField] private GameObject completedBadge;
        [SerializeField] private Image[] starImages;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private TMP_Text devInfoText;

        [Tooltip("Multiplies the star sprite's own art at full opacity - shows its natural (gold) color.")]
        [SerializeField] private Color earnedStarColor = Color.white;

        [Tooltip("Muted/desaturated multiply tint for an unearned star - stays clearly visible (never a faint low-alpha ghost) while still reading as distinct from an earned star.")]
        [SerializeField] private Color unearnedStarColor = new Color(0.55f, 0.52f, 0.46f, 0.9f);

        public int LevelNumber { get; private set; }

        /// <summary>
        /// devInfo is only ever displayed in the Editor or a Development Build
        /// (see the #if guard below) - release builds never show generator
        /// version/pipe count/grid-size internals on the card, even if a
        /// non-empty string is passed in.
        /// </summary>
        public void Configure(
            int levelNumber, int gridWidth, int gridHeight,
            bool isUnlocked, bool isCompleted, int earnedStars, int bestScore,
            string devInfo, Action<int> onClicked)
        {
            LevelNumber = levelNumber;

            if (levelNumberText != null) levelNumberText.text = levelNumber.ToString();
            if (gridSizeText != null) gridSizeText.text = $"{gridWidth}×{gridHeight}";

            if (lockedOverlay != null) lockedOverlay.SetActive(!isUnlocked);
            if (completedBadge != null) completedBadge.SetActive(isCompleted);
            SetStars(earnedStars);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (devInfoText != null)
            {
                devInfoText.gameObject.SetActive(!string.IsNullOrEmpty(devInfo));
                devInfoText.text = devInfo;
            }
#else
            if (devInfoText != null)
            {
                devInfoText.gameObject.SetActive(false);
            }
#endif

            if (button != null)
            {
                button.interactable = isUnlocked;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClicked?.Invoke(LevelNumber));
            }
        }

        /// <summary>All three star placeholders stay active/visible (unearned = muted color, not near-invisible low alpha) rather than being hidden - keeps the card's layout stable once real star data exists.</summary>
        private void SetStars(int earnedStars)
        {
            if (starImages == null)
                return;

            int clamped = Mathf.Clamp(earnedStars, 0, 3);
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null)
                    continue;

                starImages[i].color = i < clamped ? earnedStarColor : unearnedStarColor;
            }
        }
    }
}
