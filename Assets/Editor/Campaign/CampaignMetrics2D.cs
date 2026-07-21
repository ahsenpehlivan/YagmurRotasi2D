using UnityEngine;

/// <summary>
/// Difficulty metrics (Part N) and score-threshold generation (Part Q) for a
/// campaign level. Score thresholds reuse ScoreManager2D's own existing
/// formula (never redesigned) - see ScoreManager2D.CalculateSuccess - so
/// generated levels award stars exactly the same way Levels 1-6 always have.
/// </summary>
public static class CampaignMetrics2D
{
    public sealed class LevelMetrics
    {
        public int GridWidth;
        public int GridHeight;
        public int ActivePipeCount;
        public int StraightCount;
        public int CornerCount;
        public int TeeCount;
        public int CrossCount;
        public int BranchCount;
        public int CycleCount;
        public int ShortestSourceToTargetDistance;
        public int ReachableTileCount;
        public int FlowWaveCount;
        public int MinimumRequiredTaps;
        public int SolutionCount;
        public int GenerationAttempts;
        public float GenerationTimeSeconds;
        public int Seed;
        public float DifficultyScore;
    }

    /// <summary>Weighted combination of several metrics - deliberately not grid-size-alone (Part N explicitly requires this).</summary>
    public static float ComputeDifficultyScore(LevelMetrics m)
    {
        float score = 0f;
        score += m.GridWidth * m.GridHeight * 0.5f;
        score += m.ActivePipeCount * 1.2f;
        score += m.TeeCount * 3f;
        score += m.CrossCount * 5f;
        score += m.BranchCount * 2f;
        score += m.CycleCount * 6f;
        score += m.MinimumRequiredTaps * 1.5f;
        score += m.FlowWaveCount * 1f;
        return score;
    }

    /// <summary>
    /// Mirrors ScoreManager2D.CalculateSuccess's exact formula for the
    /// "optimal play" case (MoveCount == minimumRequiredTaps, zero wrong
    /// attempts) to derive thresholds a near-optimal player can actually
    /// reach for 3 stars, with a lower bar for 2 stars - completion always
    /// earns at least 1 star regardless (ScoreManager2D's own existing
    /// guarantee, unchanged).
    /// </summary>
    public static (int twoStarScore, int threeStarScore) ComputeScoreThresholds(int minimumRequiredTaps, int activePipeCount)
    {
        const int baseScore = 100;
        const int pathLengthMultiplier = 10;
        const int perfectBonus = 50;
        const int moveBonusCap = 10;
        const int moveBonusMultiplier = 5;

        int moveBonus = Mathf.Max(0, moveBonusCap - minimumRequiredTaps) * moveBonusMultiplier;
        int optimalScore = baseScore + activePipeCount * pathLengthMultiplier + perfectBonus + moveBonus;

        int threeStarScore = Mathf.RoundToInt(optimalScore * 0.9f);
        int twoStarScore = Mathf.RoundToInt(optimalScore * 0.65f);

        return (twoStarScore, threeStarScore);
    }
}
