using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace YagmurRotasi2D.Gameplay2D
{
    /// <summary>
    /// Phase 9B web mouse-hover feedback for pipes - deliberately centralized
    /// here instead of each PipeTile2D doing its own per-frame raycast (which
    /// would cost one Physics2D.Raycast PER PIPE per frame, up to 100 on a
    /// 10x10 board). This does exactly ONE raycast per frame for the entire
    /// board and tells only the single previously/currently-hovered
    /// PipeTile2D to update via PipeTile2D.SetHovered - O(1) regardless of
    /// board size. Mirrors PipeTile2D.Update()'s own existing
    /// input-locked/UI-blocked/no-pointer guards exactly, so hover state is
    /// always consistent with whether a click would actually be allowed to
    /// rotate anything right now.
    /// </summary>
    public class PipeHoverCoordinator2D : MonoBehaviour
    {
        private PipeTile2D currentlyHovered;

        private void Update()
        {
            if (GameState2D.IsInputLocked)
            {
                ClearHover();
                return;
            }

            if (Pointer.current == null)
            {
                ClearHover();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearHover();
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                ClearHover();
                return;
            }

            Vector2 screenPos = Pointer.current.position.ReadValue();
            Vector2 worldPos = cam.ScreenToWorldPoint(screenPos);
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

            PipeTile2D hitPipe = hit.collider != null ? hit.collider.GetComponent<PipeTile2D>() : null;
            if (hitPipe != null && !hitPipe.IsRotatable)
            {
                hitPipe = null;
            }

            if (hitPipe == currentlyHovered)
                return;

            if (currentlyHovered != null) currentlyHovered.SetHovered(false);
            currentlyHovered = hitPipe;
            if (currentlyHovered != null) currentlyHovered.SetHovered(true);
        }

        private void OnDisable()
        {
            ClearHover();
        }

        private void ClearHover()
        {
            if (currentlyHovered == null)
                return;

            currentlyHovered.SetHovered(false);
            currentlyHovered = null;
        }
    }
}
