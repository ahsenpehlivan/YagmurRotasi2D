using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Editor-only visual preview of a given grid size (Phase 8A Part T) on
/// whichever BoardManager2D is present in the currently open scene (intended
/// for GameScene2D) - lets a developer eyeball how a 5x5 through 10x10 board
/// looks/fits before running the generator, without touching any saved level
/// asset. Only rebuilds the visual grid cells (BoardManager2D.SetGridSize +
/// BuildGrid) - never spawns/removes pipes, Source, or Target, and never
/// saves the scene itself. Because BuildGrid() instantiates/destroys real
/// scene GameObjects, Unity will still mark the scene as modified - this is
/// a genuine (if minor) scene edit, not a separate overlay, so the log
/// message below explicitly warns not to save unless the new size is
/// intentional. Play Mode or reloading the scene without saving discards it.
/// </summary>
public static class CampaignGridPreview2D
{
    [MenuItem("YagmurRotasi2D/Phase 8/Preview Grid Size/5x5")]
    public static void Preview5x5() => Preview(5, 5);

    [MenuItem("YagmurRotasi2D/Phase 8/Preview Grid Size/6x6")]
    public static void Preview6x6() => Preview(6, 6);

    [MenuItem("YagmurRotasi2D/Phase 8/Preview Grid Size/7x7")]
    public static void Preview7x7() => Preview(7, 7);

    [MenuItem("YagmurRotasi2D/Phase 8/Preview Grid Size/8x8")]
    public static void Preview8x8() => Preview(8, 8);

    [MenuItem("YagmurRotasi2D/Phase 8/Preview Grid Size/9x9")]
    public static void Preview9x9() => Preview(9, 9);

    [MenuItem("YagmurRotasi2D/Phase 8/Preview Grid Size/10x10")]
    public static void Preview10x10() => Preview(10, 10);

    private static void Preview(int width, int height)
    {
        var board = Object.FindFirstObjectByType<BoardManager2D>();
        if (board == null)
        {
            Debug.LogError("CampaignGridPreview2D: No BoardManager2D found in the currently open scene. Open GameScene2D first.");
            return;
        }

        board.SetGridSize(width, height);
        board.BuildGrid();

        Debug.Log($"CampaignGridPreview2D: Previewing a {width}x{height} grid (cellSize={board.CellSize:0.###}) on '{board.gameObject.name}'. " +
            "This is a live scene edit (grid cell GameObjects were rebuilt) - do NOT save the scene unless you intend to keep this size. " +
            "Reload the scene without saving, or run this preview again with the original size, to discard it.");
    }
}
