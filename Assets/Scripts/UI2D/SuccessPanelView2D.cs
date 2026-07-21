using System;
using UnityEngine;
using UnityEngine.UI;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Self-contained success-screen view. Owns every visual reference the
    /// dedicated success panel needs (title, body, three stars, Next Level
    /// button) independently of the old InfoPanel hierarchy. All three star
    /// GameObjects/Images are always active and visible - "unearned" is
    /// represented only by reduced alpha on the same sprite, never by hiding it.
    /// </summary>
    public class SuccessPanelView2D : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text statsText;
        [SerializeField] private Image[] starImages;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button returnToLevelSelectButton;
        [SerializeField] private float earnedStarAlpha = 1f;
        [SerializeField] private float unearnedStarAlpha = 0.3f;

        public bool IsVisible { get; private set; }

        /// <summary>
        /// Phase 9A: hasNextLevel gates NextLevelButton - on the last campaign
        /// level (Level 100) the button is hidden entirely rather than left
        /// clickable with nothing to advance to, per the campaign catalog's own
        /// ordering (never a hardcoded level count). onReturnToLevelSelect wires
        /// the "Bölüm Seçimine Dön" button, always available regardless of
        /// hasNextLevel. Phase 9B adds score/moveCount display and onRetry
        /// ("Tekrar Oyna" - replays the just-completed level) - no time field
        /// (this project has no timer system; see Phase 9B report for why one
        /// wasn't added here, since building one would be new gameplay logic,
        /// out of scope for a layout/web-presentation phase).
        /// </summary>
        public void Show(int earnedStars, string title, string body, int score, int moveCount,
            bool hasNextLevel, Action onNextLevel, Action onRetry, Action onReturnToLevelSelect)
        {
            if (root != null) root.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
            if (statsText != null) statsText.text = $"Skor: {score}   Hamle: {moveCount}";

            SetStars(earnedStars);

            if (nextLevelButton != null)
            {
                nextLevelButton.gameObject.SetActive(hasNextLevel);
                nextLevelButton.onClick.RemoveAllListeners();
                if (hasNextLevel && onNextLevel != null)
                {
                    nextLevelButton.onClick.AddListener(() => onNextLevel());
                }
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                if (onRetry != null)
                {
                    retryButton.onClick.AddListener(() => onRetry());
                }
            }

            if (returnToLevelSelectButton != null)
            {
                returnToLevelSelectButton.onClick.RemoveAllListeners();
                if (onReturnToLevelSelect != null)
                {
                    returnToLevelSelectButton.onClick.AddListener(() => onReturnToLevelSelect());
                }
            }

            IsVisible = true;
        }

        public void Hide()
        {
            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveAllListeners();
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
            }

            if (returnToLevelSelectButton != null)
            {
                returnToLevelSelectButton.onClick.RemoveAllListeners();
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (root != null) root.SetActive(false);

            IsVisible = false;
        }

        /// <summary>Updates only the star alphas - all three stars remain active/enabled/visible regardless of earnedStars.</summary>
        public void SetStars(int earnedStars)
        {
            if (starImages == null)
                return;

            int clamped = Mathf.Clamp(earnedStars, 0, 3);
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null)
                    continue;

                starImages[i].gameObject.SetActive(true);
                starImages[i].enabled = true;

                Color c = starImages[i].color;
                c.a = i < clamped ? earnedStarAlpha : unearnedStarAlpha;
                starImages[i].color = c;
            }
        }
    }
}
