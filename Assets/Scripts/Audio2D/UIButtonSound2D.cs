using UnityEngine;
using UnityEngine.UI;

namespace YagmurRotasi2D.Audio2D
{
    /// <summary>
    /// Reusable, additive UI click sound - attaches to an EXISTING Button
    /// without touching any of its existing onClick listeners (adds one
    /// extra listener alongside whatever the button already does). Safe to
    /// add to any Button idempotently (see RegisterListenerOnce) - the
    /// builder can attach this to every button across all three scenes
    /// without ever double-registering.
    ///
    /// Unity's own Button/Selectable implementation never invokes onClick at
    /// all when interactable is false, so a disabled button (or a locked
    /// LevelButton2D card, which sets button.interactable = isUnlocked)
    /// never reaches HandleClick in the first place; the explicit
    /// interactable check below is a documented, zero-cost safety net on
    /// top of that guarantee, not the only thing preventing sound on a
    /// disabled control.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonSound2D : MonoBehaviour
    {
        private Button button;
        private bool listenerRegistered;

        private void Awake()
        {
            button = GetComponent<Button>();
            RegisterListenerOnce();
        }

        private void OnEnable()
        {
            // Defensive - covers a component added at runtime after Awake
            // already ran elsewhere, or the object being disabled/re-enabled.
            RegisterListenerOnce();
        }

        private void RegisterListenerOnce()
        {
            if (listenerRegistered || button == null)
                return;

            button.onClick.AddListener(HandleClick);
            listenerRegistered = true;
        }

        private void HandleClick()
        {
            if (button != null && !button.interactable)
                return;

            if (GameAudioManager2D.Instance != null)
            {
                GameAudioManager2D.Instance.PlayUIClick();
            }
        }
    }
}
