using UnityEngine;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Applies Screen.safeArea to this RectTransform's anchors (normalized 0-1), so
    /// its children stay clear of notches/rounded corners/gesture bars on any device.
    /// Only re-applies when resolution, orientation or the safe area actually change
    /// (cheap value-type comparisons, no per-frame allocation).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter2D : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private ScreenOrientation lastOrientation;
        private bool hasAppliedOnce;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            Apply(force: true);
        }

        private void Update()
        {
            Apply(force: false);
        }

        private void Apply(bool force)
        {
            if (rectTransform == null)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            ScreenOrientation orientation = Screen.orientation;

            if (!force && hasAppliedOnce && safeArea == lastSafeArea &&
                screenSize == lastScreenSize && orientation == lastOrientation)
            {
                return;
            }

            if (screenSize.x <= 0 || screenSize.y <= 0)
            {
                return;
            }

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
            lastOrientation = orientation;
            hasAppliedOnce = true;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }
    }
}
