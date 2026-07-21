namespace YagmurRotasi2D.Gameplay2D
{
    /// <summary>Shared gameplay flags. Currently only tracks whether pipe input should be ignored (e.g. during water animation).</summary>
    public static class GameState2D
    {
        public static bool IsInputLocked { get; set; }
    }
}
