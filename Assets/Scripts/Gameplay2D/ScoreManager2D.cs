using UnityEngine;

namespace YagmurRotasi2D.Gameplay2D
{
    public class ScoreManager2D : MonoBehaviour
    {
        private const int BaseScore = 100;
        private const int PathLengthMultiplier = 10;
        private const int PerfectBonus = 50;
        private const int MoveBonusCap = 10;
        private const int MoveBonusMultiplier = 5;
        private const int WrongPenaltyMultiplier = 10;

        public int MoveCount { get; private set; }
        public int WrongAttemptCount { get; private set; }
        public int CurrentScore { get; private set; }
        public int StarCount { get; private set; }

        public void ResetState()
        {
            MoveCount = 0;
            WrongAttemptCount = 0;
            CurrentScore = 0;
            StarCount = 0;
        }

        /// <summary>Call only for player tap/click rotations, never for level-setup rotation.</summary>
        public void RegisterMove()
        {
            MoveCount++;
        }

        public void RegisterWrongAttempt()
        {
            WrongAttemptCount++;
        }

        /// <summary>
        /// Computes and stores the final score/star count for a successful route.
        /// finalScore = 100 + (pathLength * 10) + perfectBonus + moveBonus - wrongPenalty, clamped to >= 0.
        /// </summary>
        public int CalculateSuccess(int pathLength, int twoStarScore, int threeStarScore)
        {
            int perfectBonus = WrongAttemptCount == 0 ? PerfectBonus : 0;
            int moveBonus = Mathf.Max(0, MoveBonusCap - MoveCount) * MoveBonusMultiplier;
            int wrongPenalty = WrongAttemptCount * WrongPenaltyMultiplier;

            int score = BaseScore + (pathLength * PathLengthMultiplier) + perfectBonus + moveBonus - wrongPenalty;
            score = Mathf.Max(0, score);

            CurrentScore = score;
            StarCount = CalculateStars(score, twoStarScore, threeStarScore);

            return CurrentScore;
        }

        private static int CalculateStars(int score, int twoStarScore, int threeStarScore)
        {
            if (score >= threeStarScore)
                return 3;

            if (score >= twoStarScore)
                return 2;

            // Success always gives at least 1 star.
            return 1;
        }
    }
}
