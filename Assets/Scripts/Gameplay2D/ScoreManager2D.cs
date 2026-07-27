using System;
using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;

namespace YagmurRotasi2D.Gameplay2D
{
    /// <summary>
    /// The single source of truth for move-based live star rating, alongside
    /// the pre-existing move-count/score bookkeeping. CurrentStarRating is
    /// always kept in lockstep with MoveCount (recomputed on every
    /// RegisterMove() and on every ResetState()) so every UI element that
    /// reads it - the top move badge, the lower-right move counter, the
    /// lower-right live star preview, and the final success save - is always
    /// reading the exact same number, never a locally-cached copy.
    /// </summary>
    public class ScoreManager2D : MonoBehaviour
    {
        private const int BaseScore = 100;
        private const int PathLengthMultiplier = 10;
        private const int PerfectBonus = 50;
        private const int MoveBonusCap = 10;
        private const int MoveBonusMultiplier = 5;
        private const int WrongPenaltyMultiplier = 10;

        // Default per-level star threshold formula (see CalculateAutomaticStarLimits):
        // threeStarExtra = Clamp(CeilToInt(optimalMoves * 0.25), 2, 8)
        // twoStarExtra   = Clamp(CeilToInt(optimalMoves * 0.50), 4, 15)
        private const float ThreeStarExtraRatio = 0.25f;
        private const float TwoStarExtraRatio = 0.5f;
        private const int ThreeStarExtraMin = 2;
        private const int ThreeStarExtraMax = 8;
        private const int TwoStarExtraMin = 4;
        private const int TwoStarExtraMax = 15;

        public int MoveCount { get; private set; }
        public int WrongAttemptCount { get; private set; }
        public int CurrentScore { get; private set; }

        /// <summary>The move count at/below which a completion earns 3 stars for the currently-loaded level. Recomputed by ResetState() from that level's own data - never a single global constant.</summary>
        public int ThreeStarMoveLimit { get; private set; }

        /// <summary>The move count at/below which a completion earns at least 2 stars for the currently-loaded level (moves beyond this always earn exactly 1).</summary>
        public int TwoStarMoveLimit { get; private set; }

        /// <summary>
        /// The live star rating for the CURRENT move count - 3 at level start,
        /// stepping down to 2 then 1 as MoveCount crosses ThreeStarMoveLimit/
        /// TwoStarMoveLimit. This is also the exact value the lower-right star
        /// preview displays and the exact value saved at successful completion
        /// (no second star formula exists anywhere else).
        /// </summary>
        public int CurrentStarRating { get; private set; }

        /// <summary>
        /// Raised whenever MoveCount or CurrentStarRating changes (every
        /// accepted move, and every ResetState() on level load/reload). Both
        /// the top and lower-right move counters and the live star preview all
        /// update from this single event/state - never from their own local
        /// count.
        /// </summary>
        public event Action OnMoveCountChanged;

        /// <summary>
        /// Resets per-level state and recomputes this level's star thresholds
        /// from its own optimalMoves (see CalculateOptimalMoves) - never a
        /// single fixed threshold shared by every level. Manual overrides are
        /// used only when useManualStarLimits is true AND they pass validation
        /// (three-star limit &gt;= optimalMoves, two-star limit &gt; three-star
        /// limit); otherwise the automatic formula is used and one warning is
        /// logged. Never touches GameProgress2D/saved campaign progress.
        /// </summary>
        public void ResetState(int optimalMoves, bool useManualStarLimits, int manualThreeStarMoveLimit, int manualTwoStarMoveLimit)
        {
            MoveCount = 0;
            WrongAttemptCount = 0;
            CurrentScore = 0;

            (ThreeStarMoveLimit, TwoStarMoveLimit, _) = ResolveStarLimits(
                optimalMoves, useManualStarLimits, manualThreeStarMoveLimit, manualTwoStarMoveLimit);

            RecalculateStarRating();
            OnMoveCountChanged?.Invoke();
        }

        /// <summary>Call only for player tap/click rotations, never for level-setup rotation.</summary>
        public void RegisterMove()
        {
            MoveCount++;
            RecalculateStarRating();
            OnMoveCountChanged?.Invoke();
        }

        public void RegisterWrongAttempt()
        {
            WrongAttemptCount++;
        }

        private void RecalculateStarRating()
        {
            if (MoveCount <= ThreeStarMoveLimit)
            {
                CurrentStarRating = 3;
            }
            else if (MoveCount <= TwoStarMoveLimit)
            {
                CurrentStarRating = 2;
            }
            else
            {
                CurrentStarRating = 1;
            }
        }

        /// <summary>
        /// Computes and stores the final score for a successful route.
        /// finalScore = 100 + (pathLength * 10) + perfectBonus + moveBonus - wrongPenalty, clamped to >= 0.
        /// Stars are NOT derived from this score - CurrentStarRating (move-based,
        /// already live-updated by every RegisterMove()) is the only star
        /// source; this method exists purely for the numeric score display.
        /// </summary>
        public int CalculateSuccess(int pathLength)
        {
            int perfectBonus = WrongAttemptCount == 0 ? PerfectBonus : 0;
            int moveBonus = Mathf.Max(0, MoveBonusCap - MoveCount) * MoveBonusMultiplier;
            int wrongPenalty = WrongAttemptCount * WrongPenaltyMultiplier;

            int score = BaseScore + (pathLength * PathLengthMultiplier) + perfectBonus + moveBonus - wrongPenalty;
            CurrentScore = Mathf.Max(0, score);

            return CurrentScore;
        }

        /// <summary>
        /// The theoretical minimum number of accepted (clockwise, +1
        /// rotationIndex mod 4 - PipeTile2D.RotatePipe's real behavior) player
        /// rotations needed to bring every rotatable pipe from its
        /// startRotationIndex to a rotationIndex whose logical port mask
        /// matches the level's own intended solvedRotationIndex
        /// (PipeTile2D.GetPortMask - the same canonical data
        /// LevelManager2D.ValidateCurrentMatchesSolved already trusts).
        /// Straight's two-fold rotational symmetry (0≈2, 1≈3) and Cross's
        /// single always-open state fall out of this for free - both are
        /// found by checking EVERY rotationIndex's real port mask against the
        /// solved mask (never a hardcoded per-type symmetry table), so a
        /// pipe's minimum move count is always the shortest clockwise
        /// distance to ANY rotationIndex that is logically equivalent to the
        /// intended solution, not just the literal solvedRotationIndex.
        /// Cross itself is skipped entirely (never rotatable, contributes 0).
        /// Source/Target are never part of the pipes list, so they
        /// automatically contribute 0 too. This never searches for an
        /// alternate route through the puzzle - only each pipe's own shortest
        /// path to its own intended orientation.
        /// </summary>
        public static int CalculateOptimalMoves(IReadOnlyList<PipeSpawnData2D> pipes)
        {
            if (pipes == null)
                return 0;

            int total = 0;
            for (int i = 0; i < pipes.Count; i++)
            {
                PipeSpawnData2D spawn = pipes[i];
                if (spawn.pipeType == PipeType2D.Cross)
                    continue;

                int solvedMask = PipeTile2D.GetPortMask(spawn.pipeType, spawn.solvedRotationIndex);
                int bestSteps = int.MaxValue;

                for (int targetIndex = 0; targetIndex < 4; targetIndex++)
                {
                    if (PipeTile2D.GetPortMask(spawn.pipeType, targetIndex) != solvedMask)
                        continue;

                    int steps = ((targetIndex - spawn.startRotationIndex) % 4 + 4) % 4;
                    if (steps < bestSteps)
                    {
                        bestSteps = steps;
                    }
                }

                // Every rotationIndex trivially matches its own port mask, so
                // bestSteps always finds at least one candidate (itself) -
                // int.MaxValue can never survive to here for real data.
                total += bestSteps == int.MaxValue ? 0 : bestSteps;
            }

            return total;
        }

        /// <summary>
        /// Default per-level threshold formula (no manual override):
        /// threeStarExtra = Clamp(CeilToInt(optimalMoves * 0.25), 2, 8)
        /// twoStarExtra   = Clamp(CeilToInt(optimalMoves * 0.50), 4, 15)
        /// threeStarMoveLimit = Max(3, optimalMoves + threeStarExtra)
        /// twoStarMoveLimit   = Max(threeStarMoveLimit + 1, threeStarMoveLimit + twoStarExtra)
        /// </summary>
        public static (int threeStarMoveLimit, int twoStarMoveLimit) CalculateAutomaticStarLimits(int optimalMoves)
        {
            int clampedOptimal = Mathf.Max(0, optimalMoves);

            int threeStarExtra = Mathf.Clamp(Mathf.CeilToInt(clampedOptimal * ThreeStarExtraRatio), ThreeStarExtraMin, ThreeStarExtraMax);
            int threeStarMoveLimit = Mathf.Max(3, clampedOptimal + threeStarExtra);

            int twoStarExtra = Mathf.Clamp(Mathf.CeilToInt(clampedOptimal * TwoStarExtraRatio), TwoStarExtraMin, TwoStarExtraMax);
            int twoStarMoveLimit = Mathf.Max(threeStarMoveLimit + 1, threeStarMoveLimit + twoStarExtra);

            return (threeStarMoveLimit, twoStarMoveLimit);
        }

        /// <summary>
        /// Picks automatic vs. manual star limits for a level. A manual
        /// override is only honored when useManualStarLimits is true AND it
        /// is valid (three-star limit &gt;= optimalMoves, two-star limit &gt;
        /// three-star limit) - an invalid manual override silently falls back
        /// to the automatic result, plus one clear development-only warning
        /// (never a runtime/WebGL console message). usedManualLimits reports
        /// which one was actually applied (used by the Editor difficulty
        /// audit tool to report automatic-vs-manual per level).
        /// </summary>
        public static (int threeStarMoveLimit, int twoStarMoveLimit, bool usedManualLimits) ResolveStarLimits(
            int optimalMoves, bool useManualStarLimits, int manualThreeStarMoveLimit, int manualTwoStarMoveLimit)
        {
            (int autoThree, int autoTwo) = CalculateAutomaticStarLimits(optimalMoves);

            if (!useManualStarLimits)
                return (autoThree, autoTwo, false);

            bool manualIsValid = manualThreeStarMoveLimit >= optimalMoves && manualTwoStarMoveLimit > manualThreeStarMoveLimit;
            if (!manualIsValid)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"ScoreManager2D: manual star limits (three={manualThreeStarMoveLimit}, two={manualTwoStarMoveLimit}) " +
                    $"are invalid for optimalMoves={optimalMoves} - falling back to automatic limits (three={autoThree}, two={autoTwo}).");
#endif
                return (autoThree, autoTwo, false);
            }

            return (manualThreeStarMoveLimit, manualTwoStarMoveLimit, true);
        }
    }
}
