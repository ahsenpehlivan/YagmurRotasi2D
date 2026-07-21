using System;
using System.Collections;
using UnityEngine;
using YagmurRotasi2D.Core2D;

namespace YagmurRotasi2D.Visual2D
{
    /// <summary>
    /// Plays a pipe's 4-frame water-fill animation via SpriteFrameAnimator2D,
    /// oriented so it visually starts from the real side water enters the pipe
    /// from (see PipeFlowVisualProfile2D). Frame order always plays forward
    /// (0..3, ending on the fully-filled frame) - direction is achieved
    /// entirely through a WaterOverlay-only spatial correction (rotation/
    /// mirror), never by reversing frame order. BaseVisual is never touched.
    /// Falls back to simply showing the (placeholder) WaterOverlay for a fixed
    /// duration if no real 4-frame array has been bound yet, so the sequence
    /// never freezes.
    /// </summary>
    public class PipeWaterVisual2D : MonoBehaviour
    {
        [SerializeField] private GameObject waterOverlay;
        [SerializeField] private SpriteFrameAnimator2D animator;
        [SerializeField] private float fallbackDuration = 0.25f;

        private const int RequiredFrameCount = 4;

        private Action pendingCompletion;
        private Coroutine fallbackCoroutine;
        private SpriteRenderer overlayRenderer;
        private Quaternion baseOverlayLocalRotation;
        private bool baseStateCaptured;

        public bool IsPlaying => (animator != null && animator.IsPlaying) || fallbackCoroutine != null;

        private void Awake()
        {
            CaptureBaseState();
        }

        /// <summary>
        /// Plays the fill animation oriented so it visually starts from
        /// worldEntrySide - the real side FlowSolver2D determined water enters
        /// from (PipeFlowStep2D.PrimaryEntrySide), never guessed from position/
        /// rotation alone. pipeType/rotationIndex come from the owning
        /// PipeTile2D, which is the sole source of truth for both.
        /// </summary>
        public void PlayFill(PipeType2D pipeType, int rotationIndex, Direction2D worldEntrySide, Action onCompleted)
        {
            ClearPendingCallback();
            CaptureBaseState();

            if (waterOverlay != null)
            {
                waterOverlay.SetActive(true);
            }

            ApplyEntrySideCorrection(pipeType, rotationIndex, worldEntrySide);

            if (animator != null && animator.FrameCount >= RequiredFrameCount)
            {
                pendingCompletion = onCompleted;
                animator.OnAnimationCompleted += HandleAnimatorCompleted;
                animator.PlayFromStart();
            }
            else
            {
                pendingCompletion = onCompleted;
                fallbackCoroutine = StartCoroutine(FallbackRoutine());
            }
        }

        public void ResetFillAnimation()
        {
            ClearPendingCallback();

            if (animator != null)
            {
                animator.Stop();
                animator.ResetToFirstFrame();
            }

            if (waterOverlay != null)
            {
                waterOverlay.SetActive(false);

                // Undo any entry-side correction so the next fill always starts
                // from the same known baseline instead of compounding on top of
                // whatever the previous attempt/level left behind.
                CaptureBaseState();
                waterOverlay.transform.localRotation = baseOverlayLocalRotation;
                if (overlayRenderer != null)
                {
                    overlayRenderer.flipX = false;
                }
            }
        }

        private void CaptureBaseState()
        {
            if (baseStateCaptured || waterOverlay == null)
            {
                return;
            }

            overlayRenderer = waterOverlay.GetComponent<SpriteRenderer>();
            baseOverlayLocalRotation = waterOverlay.transform.localRotation;
            baseStateCaptured = true;
        }

        private void ApplyEntrySideCorrection(PipeType2D pipeType, int rotationIndex, Direction2D worldEntrySide)
        {
            if (waterOverlay == null)
            {
                return;
            }

            Direction2D localEntrySide = worldEntrySide.ToLocalDirection(rotationIndex);
            PipeFlowVisualProfile2D.ResolveCorrection(pipeType, localEntrySide, out float extraRotationZ, out bool flipX);

            waterOverlay.transform.localRotation = baseOverlayLocalRotation * Quaternion.Euler(0f, 0f, extraRotationZ);

            if (overlayRenderer != null)
            {
                overlayRenderer.flipX = flipX;
            }
        }

        private void HandleAnimatorCompleted()
        {
            if (animator != null)
            {
                animator.OnAnimationCompleted -= HandleAnimatorCompleted;
            }

            InvokePending();
        }

        private IEnumerator FallbackRoutine()
        {
            yield return new WaitForSeconds(fallbackDuration);
            fallbackCoroutine = null;
            InvokePending();
        }

        private void InvokePending()
        {
            Action callback = pendingCompletion;
            pendingCompletion = null;
            callback?.Invoke();
        }

        private void ClearPendingCallback()
        {
            if (animator != null)
            {
                animator.OnAnimationCompleted -= HandleAnimatorCompleted;
            }

            if (fallbackCoroutine != null)
            {
                StopCoroutine(fallbackCoroutine);
                fallbackCoroutine = null;
            }

            pendingCompletion = null;
        }
    }
}
