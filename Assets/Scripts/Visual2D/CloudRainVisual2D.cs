using UnityEngine;

namespace YagmurRotasi2D.Visual2D
{
    /// <summary>
    /// Toggles the placeholder Cloud2D visual depending on whether the active
    /// rain-cloud animation already includes the cloud itself.
    /// </summary>
    public class CloudRainVisual2D : MonoBehaviour
    {
        [SerializeField] private GameObject cloudObject;
        [SerializeField] private SpriteFrameAnimator2D rainCloudAnimator;
        [SerializeField] private bool animationIncludesCloud = true;

        private void Awake()
        {
            ApplyVisualMode();
        }

        public void ApplyVisualMode()
        {
            if (cloudObject != null)
            {
                cloudObject.SetActive(!animationIncludesCloud);
            }
        }
    }
}
