namespace YagmurRotasi2D.Gameplay2D
{
    /// <summary>
    /// Phase 9A: in-memory-only navigation intent - "which campaign level did
    /// the player just pick in LevelSelectScene2D" - deliberately NOT
    /// PlayerPrefs-backed and NOT mixed with GameProgress2D's permanent saved
    /// progress. A plain static field survives a SceneManager.LoadScene call
    /// within the same play session (statics aren't tied to any GameObject, so
    /// there is nothing for a scene unload to destroy) without needing a
    /// DontDestroyOnLoad object - the safe lightweight method for this kind of
    /// short-lived, single-session value. Reset naturally on domain reload
    /// (Editor Play Mode stop/start, or app relaunch) - it is not meant to
    /// persist beyond one continuous play session.
    /// </summary>
    public static class CampaignSession2D
    {
        private static int? selectedLevelNumber;

        /// <summary>True once SetSelectedLevel has been called and not yet cleared.</summary>
        public static bool HasSelectedLevel => selectedLevelNumber.HasValue;

        /// <summary>The last level number set via SetSelectedLevel. Only meaningful when HasSelectedLevel is true - callers must check that first (this returns 1 otherwise, never an exception).</summary>
        public static int SelectedLevelNumber => selectedLevelNumber ?? 1;

        public static void SetSelectedLevel(int levelNumber)
        {
            selectedLevelNumber = levelNumber;
        }

        public static void ClearSelectedLevel()
        {
            selectedLevelNumber = null;
        }
    }
}
