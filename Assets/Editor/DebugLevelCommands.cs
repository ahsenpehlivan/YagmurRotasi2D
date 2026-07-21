using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Editor-only shortcuts (Assets/Editor - never compiled into a build) for
/// jumping straight to a specific level's saved progress during development,
/// without replaying every earlier level. Only touches GameProgress2D's own
/// level-progress PlayerPrefs keys (CurrentLevel/HighestUnlockedLevel) via its
/// existing public API - never audio settings, never any scene object, never
/// a level's saved star count.
/// </summary>
public static class DebugLevelCommands
{
    [MenuItem("YagmurRotasi2D/Debug/Set Current Level 4")]
    public static void SetCurrentLevel4()
    {
        SetCurrentLevel(4);
    }

    [MenuItem("YagmurRotasi2D/Debug/Set Current Level 5")]
    public static void SetCurrentLevel5()
    {
        SetCurrentLevel(5);
    }

    [MenuItem("YagmurRotasi2D/Debug/Set Current Level 6")]
    public static void SetCurrentLevel6()
    {
        SetCurrentLevel(6);
    }

    /// <summary>Internal (not private) so CampaignDebugLevelWindow (Phase 8A's generic Levels 7-20 selector) can reuse the exact same real unlock-chain logic instead of a second copy.</summary>
    internal static void SetCurrentLevel(int levelNumber)
    {
        int beforeCurrent = GameProgress2D.CurrentLevel;
        int beforeHighest = GameProgress2D.HighestUnlockedLevel;

        GameProgress2D.SetCurrentLevel(levelNumber);

        // SetCurrentLevel only moves the "resume here" pointer - it does not
        // unlock the level. Re-marking the PREVIOUS level "completed" with its
        // own existing best-star result (a no-op on that level's stars, since
        // MarkLevelCompleted only ever raises a stored value) reuses the real
        // production unlock path instead of poking HighestUnlockedLevel's
        // PlayerPrefs key directly, so the target level is actually reachable
        // through MainMenuScene2D too.
        if (GameProgress2D.HighestUnlockedLevel < levelNumber)
        {
            int previousLevel = levelNumber - 1;
            GameProgress2D.MarkLevelCompleted(previousLevel, GameProgress2D.GetBestStars(previousLevel));
        }

        Debug.Log($"DebugLevelCommands: CurrentLevel {beforeCurrent} -> {GameProgress2D.CurrentLevel}, " +
            $"HighestUnlockedLevel {beforeHighest} -> {GameProgress2D.HighestUnlockedLevel}. " +
            "Saved stars and audio settings were not touched, and no scene was opened or modified. " +
            "Open MainMenuScene2D and press Oyuna Başla, or open GameScene2D directly, to play this level.");
    }
}
