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
        [SerializeField] private Image[] starImages;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private float earnedStarAlpha = 1f;
        [SerializeField] private float unearnedStarAlpha = 0.3f;

        public bool IsVisible { get; private set; }

        public void Show(int earnedStars, string title, string body, Action onNextLevel)
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

            SetStars(earnedStars);

            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveAllListeners();
                if (onNextLevel != null)
                {
                    nextLevelButton.onClick.AddListener(() => onNextLevel());
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
