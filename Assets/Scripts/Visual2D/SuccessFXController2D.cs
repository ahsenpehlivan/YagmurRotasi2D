using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YagmurRotasi2D.Audio2D;

namespace YagmurRotasi2D.Visual2D
{
    /// <summary>
    /// Drives the world-space flower bloom / duck walk success presentation shown
    /// over the grass area of Background2D. Flowers are visible at frame 0 before
    /// success; ducks are hidden. On PlaySuccessFX, both start simultaneously.
    /// Ducks stop (but stay visible) after duckAnimationDuration seconds; the full
    /// presentation completes after successDuration seconds, at which point flowers
    /// hold their bloomed frame. Independent of pipe-fill logic and of the old
    /// TargetFX2D placeholder circles.
    /// </summary>
    public class SuccessFXController2D : MonoBehaviour
    {
        [SerializeField] private GameObject flowerRoot;
        [SerializeField] private SpriteFrameAnimator2D[] flowerAnimators;
        [SerializeField] private GameObject duckRoot;
        [SerializeField] private SpriteFrameAnimator2D[] duckAnimators;
        [SerializeField] private float successDuration = 5f;
        [Tooltip("How long ducks animate before freezing on their current frame (stays visible until Reload/Next). Clamped to successDuration if set higher.")]
        [SerializeField] private float duckAnimationDuration = 4f;
        [SerializeField] private bool keepDucksVisibleAfterSuccess = true;

        private Coroutine activeCoroutine;
        private int runId;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            PrepareInitialState();
        }

        /// <summary>
        /// Returns flowerAnimators, or - if that array is empty/entirely null
        /// (e.g. broken by an external scene edit that recreated the flower
        /// components without the array following along) - auto-discovers every
        /// SpriteFrameAnimator2D under flowerRoot as a fallback, so a single bad
        /// serialized reference can never silently disable the whole flower
        /// presentation. This is a runtime safety net only; the array should still
        /// be repaired properly via "Repair and Rebind Flower Animations" so the
        /// scene doesn't rely on this fallback going forward.
        /// </summary>
        private IReadOnlyList<SpriteFrameAnimator2D> ResolvedFlowerAnimators()
        {
            if (flowerAnimators != null && flowerAnimators.Any(a => a != null))
            {
                return flowerAnimators;
            }

            if (flowerRoot != null)
            {
                SpriteFrameAnimator2D[] discovered = flowerRoot.GetComponentsInChildren<SpriteFrameAnimator2D>(true);
                if (discovered.Length > 0)
                {
                    Debug.LogWarning("SuccessFXController2D: flowerAnimators was empty/unassigned - auto-discovered " +
                        $"{discovered.Length} SpriteFrameAnimator2D(s) under flowerRoot as a fallback. Run " +
                        "'YagmurRotasi2D > Repair and Rebind Flower Animations' to persist this wiring.");
                    return discovered;
                }
            }

            return Array.Empty<SpriteFrameAnimator2D>();
        }

        /// <summary>Applies the default pre-success visual state: flowers visible at frame 0, ducks hidden.</summary>
        public void PrepareInitialState()
        {
            CancelFX();
            ApplyDefaultVisualState();
        }

        /// <summary>Starts the simultaneous flower/duck success presentation. Invokes onCompleted exactly once, after successDuration seconds, unless cancelled first.</summary>
        public void PlaySuccessFX(Action onCompleted)
        {
            CancelFX();
            runId++;
            IsPlaying = true;

            if (flowerRoot != null) flowerRoot.SetActive(true);
            foreach (SpriteFrameAnimator2D animator in ResolvedFlowerAnimators())
            {
                if (animator == null) continue;
                animator.PlayFromStart();
            }
            // flowerRoot is already active before success (flowers idle at frame
            // 0 - see ApplyDefaultVisualState), so hooking the sparkle sound to
            // "flowerRoot activated" would fire on every level load. This is the
            // one place PlayFromStart() is actually called on the flower
            // animators for a genuine success - fires exactly once per
            // PlaySuccessFX call.
            GameAudioManager2D.Instance?.PlaySparkleSuccess();

            if (duckRoot != null) duckRoot.SetActive(true);
            if (duckAnimators != null)
            {
                foreach (SpriteFrameAnimator2D animator in duckAnimators)
                {
                    if (animator == null) continue;
                    animator.PlayFromStart();
                }
            }
            // duckRoot is inactive at all other times (see ApplyDefaultVisualState),
            // so this is genuinely unique to a real success.
            GameAudioManager2D.Instance?.PlayDuckSuccess();

            activeCoroutine = StartCoroutine(RunSuccessTimer(onCompleted, runId));
        }

        /// <summary>Cancels any running/queued sequence and resets to the default pre-success visual state. Safe to call any time (Reload, Next Level).</summary>
        public void ResetFX()
        {
            CancelFX();
            ApplyDefaultVisualState();
        }

        /// <summary>Stops the active 6-second sequence, if any, without firing its completion callback.</summary>
        public void CancelFX()
        {
            // Invalidates any in-flight callback from a stopped sequence (defense in
            // depth alongside StopCoroutine below).
            runId++;

            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }

            IsPlaying = false;
        }

        private void ApplyDefaultVisualState()
        {
            if (flowerRoot != null) flowerRoot.SetActive(true);
            foreach (SpriteFrameAnimator2D animator in ResolvedFlowerAnimators())
            {
                if (animator == null) continue;
                animator.Stop();
                animator.ResetToFirstFrame();
            }

            if (duckAnimators != null)
            {
                foreach (SpriteFrameAnimator2D animator in duckAnimators)
                {
                    if (animator == null) continue;
                    animator.Stop();
                    animator.ResetToFirstFrame();
                }
            }
            if (duckRoot != null) duckRoot.SetActive(false);
        }

        private IEnumerator RunSuccessTimer(Action onCompleted, int myRunId)
        {
            if (duckAnimationDuration > successDuration)
            {
                Debug.LogWarning($"SuccessFXController2D: duckAnimationDuration ({duckAnimationDuration:0.##}s) exceeds " +
                    $"successDuration ({successDuration:0.##}s); clamping ducks to stop at successDuration instead.");
            }
            float duckStopAt = Mathf.Clamp(duckAnimationDuration, 0f, successDuration);

            // Phase 1: wait until the duck-stop mark (e.g. 4s of a 5s total).
            yield return new WaitForSeconds(duckStopAt);

            if (myRunId != runId)
            {
                // Superseded/cancelled while waiting - do not touch state or fire completion.
                yield break;
            }

            if (duckAnimators != null)
            {
                foreach (SpriteFrameAnimator2D animator in duckAnimators)
                {
                    // Stops only the ducks here - they stay visible (duckRoot is left
                    // active) on whatever frame they were on. Flowers keep running/
                    // holding their own bloomed frame independently of this.
                    if (animator == null) continue;
                    animator.Stop();
                }
            }

            // Phase 2: wait out the remainder of successDuration before completing.
            float remaining = successDuration - duckStopAt;
            if (remaining > 0f)
            {
                yield return new WaitForSeconds(remaining);
            }

            if (myRunId != runId)
            {
                // Superseded/cancelled while waiting - do not touch state or fire completion.
                yield break;
            }

            foreach (SpriteFrameAnimator2D animator in ResolvedFlowerAnimators())
            {
                // Non-looping + holdLastFrameOnComplete means these have already
                // stopped themselves on their final frame well before successDuration;
                // this Stop() is just a safety net in case a clip runs longer.
                if (animator == null) continue;
                animator.Stop();
            }

            if (duckRoot != null) duckRoot.SetActive(keepDucksVisibleAfterSuccess);

            IsPlaying = false;
            activeCoroutine = null;

            onCompleted?.Invoke();
        }
    }
}
