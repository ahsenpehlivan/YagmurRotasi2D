using UnityEngine;

/// <summary>
/// Per-level generation parameters, keyed by level number, implementing the
/// Phase 8 Fast-Track campaign difficulty plan: Levels 1-10 = 5x5 (7-10 are
/// generated, 1-6 are hand-authored), 11-25 = 6x6, 26-45 = 7x7, 46-65 = 8x8,
/// 66-85 = 9x9, 86-100 = 10x10, every tenth level using the upper end of its
/// tier's range. Levels 1-6 are hand-authored and excluded from generation
/// entirely - this table is never called for them.
/// </summary>
public static class CampaignDifficultyProfiles2D
{
    public sealed class Profile
    {
        public int GridWidth;
        public int GridHeight;
        public int MinActivePipes;

        /// <summary>Phase 8A.2 Part D: the density the generator actively grows toward - ear/detour insertion stops once reached (subject to MaxActivePipes), rather than stopping at whatever a fixed small attempt count happened to produce.</summary>
        public int PreferredActivePipes;
        public int MaxActivePipes;
        public int BranchAttempts;
        public int MinMinimumTaps;
        public int MaxMinimumTaps;
        public int DifficultyTier;
        public int ChapterNumber;

        /// <summary>Phase 8A.2 Part L: every 10th level - must contain a real Tee junction and a genuine reconnecting branch/cycle, not just a long snake, once it reaches its density target.</summary>
        public bool IsChallengeLevel;
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
        if (levelNumber <= 45)
        {
            return Tier(7, 7, 16, 30, 7, 12, 20, tier: 3, chapter: 3, isChallengeLevel);
        }
        if (levelNumber <= 65)
        {
            return Tier(8, 8, 22, 40, 9, 18, 28, tier: 4, chapter: 4, isChallengeLevel);
        }
        if (levelNumber <= 85)
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
            ChapterNumber = chapter,
            IsChallengeLevel = isChallengeLevel
        };

        if (isChallengeLevel)
        {
            // Every tenth level uses the upper end of its tier's range (Part O).
            profile.MinMinimumTaps = Mathf.RoundToInt((minTaps + maxTaps) / 2f);
            profile.BranchAttempts += 2;
            profile.MaxActivePipes = maxPipes;
            profile.MinActivePipes = Mathf.RoundToInt((minPipes + maxPipes) / 2f);
        }

        // Part D: preferred sits a modest amount above the minimum (never
        // above max) - close enough to min that it stays reliably reachable,
        // far enough above it that density-growth has real headroom instead
        // of stopping the instant the floor is technically cleared.
        profile.PreferredActivePipes = Mathf.Min(profile.MaxActivePipes, profile.MinActivePipes + 2);

        return profile;
    }
}
