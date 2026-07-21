using System;
using UnityEngine;

namespace YagmurRotasi2D.Visual2D
{
    /// <summary>
    /// Generic Update-based sprite flipbook player. Independent of gameplay logic;
    /// reusable for the rain-cloud loop, pipe water, flower bloom, duck walk, etc.
    /// </summary>
    public class SpriteFrameAnimator2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 10f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool hideWhenStopped = false;
        [SerializeField] private bool useUnscaledTime = false;
        [SerializeField] private bool holdLastFrameOnComplete = true;

        private int currentFrameIndex;
        private float frameTimer;
        private bool isPlaying;
        private bool completedFired;

        public bool IsPlaying => isPlaying;

        /// <summary>Number of frames currently assigned (0 if none/not yet bound).</summary>
        public int FrameCount => frames != null ? frames.Length : 0;

        /// <summary>Fired once when a non-looping animation reaches its last frame. Never fires for looping animations.</summary>
        public event Action OnAnimationCompleted;

        private void OnEnable()
        {
            if (playOnEnable)
            {
                PlayFromStart();
            }
        }

        /// <summary>Resumes/starts playback from the current frame. Use PlayFromStart() to restart from frame 0.</summary>
        public void Play()
        {
            if (!HasFrames() || targetRenderer == null)
            {
                return;
            }

            isPlaying = true;
        }

        public void PlayFromStart()
        {
            currentFrameIndex = 0;
            frameTimer = 0f;
            completedFired = false;
            ApplyCurrentFrame();
            Play();
        }

        public void Stop()
        {
            isPlaying = false;

            if (hideWhenStopped && targetRenderer != null)
            {
                targetRenderer.enabled = false;
            }
        }

        public void ResetToFirstFrame()
        {
            currentFrameIndex = 0;
            frameTimer = 0f;
            completedFired = false;
            ApplyCurrentFrame();
        }

        public void SetFrames(Sprite[] newFrames)
        {
            frames = newFrames;
            ResetToFirstFrame();
        }

        private void Update()
        {
            if (!isPlaying || !HasFrames() || targetRenderer == null || framesPerSecond <= 0f)
            {
                return;
            }

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            frameTimer += deltaTime;

            float frameDuration = 1f / framesPerSecond;
            while (frameTimer >= frameDuration && isPlaying)
            {
                frameTimer -= frameDuration;
                AdvanceFrame();
            }
        }

        private void AdvanceFrame()
        {
            int nextIndex = currentFrameIndex + 1;

            if (nextIndex >= frames.Length)
            {
                if (loop)
                {
                    currentFrameIndex = 0;
                    ApplyCurrentFrame();
                }
                else if (!completedFired)
                {
                    isPlaying = false;
                    completedFired = true;

                    if (!holdLastFrameOnComplete)
                    {
                        currentFrameIndex = 0;
                        ApplyCurrentFrame();
                    }

                    if (hideWhenStopped && targetRenderer != null)
                    {
                        targetRenderer.enabled = false;
                    }

                    OnAnimationCompleted?.Invoke();
                }

                return;
            }

            currentFrameIndex = nextIndex;
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            if (targetRenderer == null || !HasFrames())
            {
                return;
            }

            if (hideWhenStopped)
            {
                targetRenderer.enabled = true;
            }

            targetRenderer.sprite = frames[currentFrameIndex];
        }

        private bool HasFrames()
        {
            return frames != null && frames.Length > 0;
        }
    }
}
