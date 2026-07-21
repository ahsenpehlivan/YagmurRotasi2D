using UnityEngine;

/// <summary>
/// Per-level generation parameters, keyed by level number, implementing the
/// campaign difficulty plan (Part O/P): Levels 1-10 = 5x5, 11-25 = 6x6,
/// 26-40 = 7x7, 41-60 = 8x8, 61-80 = 9x9, 81-100 = 10x10, every tenth level
/// using the upper end of its tier's range. Phase 8A only ever calls this for
/// Levels 7-20 (Levels 1-6 are hand-authored and excluded from generation
/// entirely) - the table already covers the full 1-100 range so Phase 8B can
/// reuse it unchanged for Levels 21-100.
/// </summary>
public static class CampaignDifficultyProfiles2D
{
    public sealed class Profile
    {
        public int GridWidth;
        public int GridHeight;
        public int MinActivePipes;
        public int MaxActivePipes;
        public int BranchAttempts;
        public int MinMinimumTaps;
        public int MaxMinimumTaps;
        public int DifficultyTier;
        public int ChapterNumber;
    }

    public static Profile ForLevel(int levelNumber)
    {
        bool isChallengeLevel = levelNumber % 10 == 0;

        if (levelNumber <= 10)
        {
            return Tier(5, 5, 8, 15, 3, 4, 10, tier: 1, chapter: 1, isChallengeLevel);
        }
        if (levelNumber <= 25)
        {
            return Tier(6, 6, 12, 22, 5, 8, 15, tier: 2, chapter: 2, isChallengeLevel);
        }
        if (levelNumber <= 40)
        {
            return Tier(7, 7, 16, 30, 7, 12, 20, tier: 3, chapter: 3, isChallengeLevel);
        }
        if (levelNumber <= 60)
        {
            return Tier(8, 8, 22, 40, 9, 18, 28, tier: 4, chapter: 4, isChallengeLevel);
        }
        if (levelNumber <= 80)
        {
            return Tier(9, 9, 28, 50, 11, 24, 38, tier: 5, chapter: 5, isChallengeLevel);
        }
        return Tier(10, 10, 35, 65, 13, 30, 50, tier: 6, chapter: 6, isChallengeLevel);
    }

    private static Profile Tier(int width, int height, int minPipes, int maxPipes, int branchAttempts, int minTaps, int maxTaps, int tier, int chapter, bool isChallengeLevel)
    {
        var profile = new Profile
        {
            GridWidth = width,
            GridHeight = height,
            MinActivePipes = minPipes,
            MaxActivePipes = maxPipes,
            BranchAttempts = branchAttempts,
            MinMinimumTaps = minTaps,
            MaxMinimumTaps = maxTaps,
            DifficultyTier = tier,
            ChapterNumber = chapter
        };

        if (isChallengeLevel)
        {
            // Every tenth level uses the upper end of its tier's range (Part O).
            profile.MinMinimumTaps = Mathf.RoundToInt((minTaps + maxTaps) / 2f);
            profile.BranchAttempts += 2;
            profile.MaxActivePipes = maxPipes;
            profile.MinActivePipes = Mathf.RoundToInt((minPipes + maxPipes) / 2f);
        }

        return profile;
    }
}
