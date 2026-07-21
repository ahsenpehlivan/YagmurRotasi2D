using UnityEngine;

namespace YagmurRotasi2D.Gameplay2D
{
    /// <summary>
    /// Static PlayerPrefs-backed level progress. Confirmed via project search that
    /// no prior save/progress system existed anywhere before this - this is the
    /// first and only one. All keys are project-prefixed to avoid collisions with
    /// other PlayerPrefs usage (e.g. GameAudioSettings2D's separate keys).
    /// </summary>
    public static class GameProgress2D
    {
        private const string CurrentLevelKey = "YagmurRotasi.CurrentLevel";
        private const string HighestUnlockedLevelKey = "YagmurRotasi.HighestUnlockedLevel";
        private const string LevelStarsKeyPrefix = "YagmurRotasi.LevelStars.";

        // Derived from the real production level catalog (LevelManager2D.
        // BuildLevels().Count) rather than a hardcoded number, so adding/
        // removing production levels (e.g. Phase 7F.4's Levels 4-6) never
        // requires a second, easy-to-forget count to update here. Existing
        // PlayerPrefs keys (YagmurRotasi.CurrentLevel/HighestUnlockedLevel/
        // LevelStars.1-3) and existing players' saved values are unaffected -
        // this only changes the clamp ceiling.
        private static int TotalLevels => LevelManager2D.ProductionLevelCount;

        public static int CurrentLevel => Mathf.Clamp(PlayerPrefs.GetInt(CurrentLevelKey, 1), 1, TotalLevels);

        public static int HighestUnlockedLevel => Mathf.Clamp(PlayerPrefs.GetInt(HighestUnlockedLevelKey, 1), 1, TotalLevels);

        public static void SetCurrentLevel(int levelNumber)
        {
            int clamped = Mathf.Clamp(levelNumber, 1, TotalLevels);
            PlayerPrefs.SetInt(CurrentLevelKey, clamped);
            Save();
        }

        /// <summary>Records the best star result for a completed level and bumps HighestUnlockedLevel to (levelNumber + 1), clamped so a nonexistent level past the last one is never requested.</summary>
        public static void MarkLevelCompleted(int levelNumber, int earnedStars)
        {
            int clampedLevel = Mathf.Clamp(levelNumber, 1, TotalLevels);
            int clampedStars = Mathf.Clamp(earnedStars, 0, 3);

            int existingBest = GetBestStars(clampedLevel);
            if (clampedStars > existingBest)
            {
                PlayerPrefs.SetInt(LevelStarsKeyPrefix + clampedLevel, clampedStars);
            }

            int newHighest = Mathf.Clamp(clampedLevel + 1, 1, TotalLevels);
            if (newHighest > HighestUnlockedLevel)
            {
                PlayerPrefs.SetInt(HighestUnlockedLevelKey, newHighest);
            }

            Save();
        }

        public static int GetBestStars(int levelNumber)
        {
            int clampedLevel = Mathf.Clamp(levelNumber, 1, TotalLevels);
            return PlayerPrefs.GetInt(LevelStarsKeyPrefix + clampedLevel, 0);
        }

        /// <summary>Resets only this project's level-progress keys (current level, highest unlocked, per-level stars). Never touches audio settings or any unrelated PlayerPrefs key, and never calls PlayerPrefs.DeleteAll().</summary>
        public static void ResetLevelProgress()
        {
            PlayerPrefs.SetInt(CurrentLevelKey, 1);
            PlayerPrefs.SetInt(HighestUnlockedLevelKey, 1);
            for (int level = 1; level <= TotalLevels; level++)
            {
                PlayerPrefs.DeleteKey(LevelStarsKeyPrefix + level);
            }

            Save();
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
