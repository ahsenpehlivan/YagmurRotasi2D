using System;
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
    /// </summary>
    public class LevelButton2D : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text levelNumberText;
        [SerializeField] private Text gridSizeText;
        [SerializeField] private GameObject completedBadge;
        [SerializeField] private Image[] starImages;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private Text devInfoText;
        [SerializeField] private float earnedStarAlpha = 1f;
        [SerializeField] private float unearnedStarAlpha = 0.3f;

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

        /// <summary>All three star placeholders stay active/visible (unearned = reduced alpha, same convention as SuccessPanelView2D) rather than being hidden - keeps the card's layout stable once real star data exists.</summary>
        private void SetStars(int earnedStars)
        {
            if (starImages == null)
                return;

            int clamped = Mathf.Clamp(earnedStars, 0, 3);
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null)
                    continue;

                Color c = starImages[i].color;
                c.a = i < clamped ? earnedStarAlpha : unearnedStarAlpha;
                starImages[i].color = c;
            }
        }
    }
}
