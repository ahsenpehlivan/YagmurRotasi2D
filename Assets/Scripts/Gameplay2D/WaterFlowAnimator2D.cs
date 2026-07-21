using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Visual2D;

namespace YagmurRotasi2D.Gameplay2D
{
    /// <summary>
    /// Drives the successful-route feedback sequence: locks input, plays each BFS
    /// wave of pipe fill animations together (every pipe in a wave starts at once,
    /// waiting for the whole wave to complete with a timeout safety net, before
    /// moving to the next wave - supports branching Tee/Cross networks, not just a
    /// single linear path), then hands off to SuccessFXController2D for the 5-second
    /// flower bloom / duck walk presentation before reporting completion. No moving
    /// water-drop object is used - the pipe fill animations and the continuously-
    /// looping rain-cloud are the only water visuals. The old TargetFX2D placeholder
    /// circles are no longer part of this path.
    /// </summary>
    public class WaterFlowAnimator2D : MonoBehaviour
    {
        [Tooltip("Safety cap on how long to wait for a single pipe's fill animation to complete before giving up and continuing anyway.")]
        public float pipeAnimationTimeout = 1.5f;

        private Coroutine activeCoroutine;
        private int animationRunId;

        /// <summary>Fired with an intermediate status message (currently just "Su akıyor...").</summary>
        public event Action<string> OnStageTextChanged;

        /// <summary>Fired once every pipe has filled and the success FX presentation has finished.</summary>
        public event Action OnAnimationComplete;

        /// <summary>flowWaves groups reachable pipes by BFS distance from Source, each step carrying the real entry side water arrives from (see FlowSolveResult2D.FlowSteps) - every pipe in a wave starts filling together, and the next wave only begins once the current one finishes or times out.</summary>
        public void PlaySuccess(IReadOnlyList<IReadOnlyList<PipeFlowStep2D>> flowWaves, SuccessFXController2D successFX)
        {
            CancelActiveAnimation();
            animationRunId++;
            activeCoroutine = StartCoroutine(RunSequence(flowWaves, successFX, animationRunId));
        }

        /// <summary>Stops any running sequence and unlocks input. Safe to call even if nothing is running.</summary>
        public void CancelActiveAnimation()
        {
            // Invalidates any in-flight pipe-completion callback from a stopped
            // sequence (defense in depth alongside StopCoroutine below).
            animationRunId++;

            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }

            GameState2D.IsInputLocked = false;
        }

        private IEnumerator RunSequence(IReadOnlyList<IReadOnlyList<PipeFlowStep2D>> flowWaves, SuccessFXController2D successFX, int runId)
        {
            GameState2D.IsInputLocked = true;

            OnStageTextChanged?.Invoke("Su akıyor...");

            foreach (IReadOnlyList<PipeFlowStep2D> wave in flowWaves)
            {
                yield return PlayWaveAndWait(wave, runId);
            }

            // The info panel must only open after the full 6-second flower/duck
            // success presentation settles, so we wait for its completion callback here.
            if (successFX != null)
            {
                bool successFXDone = false;
                successFX.PlaySuccessFX(() =>
                {
                    if (runId == animationRunId)
                    {
                        successFXDone = true;
                    }
                });

                yield return new WaitUntil(() => successFXDone);
            }

            GameState2D.IsInputLocked = false;
            activeCoroutine = null;

            OnAnimationComplete?.Invoke();
        }

        /// <summary>Starts every pipe fill animation in the wave together (each oriented to its own real entry side) and waits for all of them to complete, up to pipeAnimationTimeout for the whole wave.</summary>
        private IEnumerator PlayWaveAndWait(IReadOnlyList<PipeFlowStep2D> wave, int runId)
        {
            if (wave == null || wave.Count == 0)
            {
                yield break;
            }

            int pendingCount = wave.Count;
            foreach (PipeFlowStep2D step in wave)
            {
                step.Pipe.PlayWaterFlowVisual(step.PrimaryEntrySide, () =>
                {
                    if (runId == animationRunId)
                    {
                        pendingCount--;
                    }
                });
            }

            float elapsed = 0f;
            while (pendingCount > 0 && elapsed < pipeAnimationTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (pendingCount > 0)
            {
                Debug.LogWarning($"WaterFlowAnimator2D: a wave of {wave.Count} pipe fill animation(s) timed out after " +
                    $"{pipeAnimationTimeout:0.##}s ({pendingCount} still pending); continuing to the next wave.");
            }
        }
    }
}
