using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Phase 8 Fast-Track: a focused single-level preview/regeneration workflow -
/// deliberately NOT a level editor. Reuses existing preview infrastructure
/// (the same BoardManager2D.SetGridSize/BuildGrid pattern CampaignGridPreview2D
/// already uses, on whichever BoardManager2D is present in the open scene) to
/// visualize a campaign level's actual board size, and reuses
/// CampaignFastTrackCommand2D's exact regeneration path for its "Regenerate
/// With Next Seed" button - never a second, potentially-drifting copy of
/// either. Only ever touches the ONE selected level's asset; every other
/// level asset is always left completely unchanged. Grid-size preview is a
/// live scene edit (like CampaignGridPreview2D) - never saves the scene.
/// </summary>
public class CampaignFastTrackPreviewWindow2D : EditorWindow
{
    private int levelNumber = 7;
    private const int MinLevel = 1;
    private const int MaxLevel = 100;

    [MenuItem("YagmurRotasi2D/Phase 8/Preview Campaign Level")]
    public static void Open()
    {
        var window = GetWindow<CampaignFastTrackPreviewWindow2D>(true, "Preview Campaign Level", true);
        window.minSize = new Vector2(420, 320);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Fast-Track Campaign Level Preview", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("< Prev", GUILayout.Width(60))) levelNumber = Mathf.Clamp(levelNumber - 1, MinLevel, MaxLevel);
        levelNumber = EditorGUILayout.IntSlider(levelNumber, MinLevel, MaxLevel);
        if (GUILayout.Button("Next >", GUILayout.Width(60))) levelNumber = Mathf.Clamp(levelNumber + 1, MinLevel, MaxLevel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        CampaignLevelCatalog2D catalog = AssetDatabase.LoadAssetAtPath<CampaignLevelCatalog2D>(CampaignAssetIO2D.CatalogAssetPath);
        CampaignLevelDefinition2D def = null;
        if (catalog != null && catalog.levels != null && levelNumber - 1 < catalog.levels.Count)
        {
            def = catalog.levels[levelNumber - 1];
        }

        if (def == null)
        {
            EditorGUILayout.HelpBox($"Level {levelNumber} has no saved asset yet (run one of the \"Generate Fast-Track Levels\" commands first).", MessageType.Info);
            return;
        }

        bool uniquenessChecked = def.solutionCount != CampaignLevelDefinition2D.SolutionCountNotChecked;

        EditorGUILayout.LabelField("Display Name", def.displayName);
        EditorGUILayout.LabelField("Grid Size", $"{def.gridWidth}x{def.gridHeight}");
        EditorGUILayout.LabelField("Active Pipes", def.pipes != null ? def.pipes.Count.ToString() : "0");
        EditorGUILayout.LabelField("Minimum Taps", def.minimumRequiredTaps.ToString());
        EditorGUILayout.LabelField("Branch / Cycle Count", $"{def.branchCount} / {def.cycleCount}");
        EditorGUILayout.LabelField("Difficulty Tier / Score", $"{def.difficultyTier} / {def.difficultyScore:0.##}");
        EditorGUILayout.LabelField("Uniqueness", uniquenessChecked ? def.solutionCount.ToString() : "NotChecked (Fast-Track)");
        EditorGUILayout.LabelField("Deterministic Seed", def.deterministicSeed.ToString());
        EditorGUILayout.LabelField("Generator Version", string.IsNullOrEmpty(def.generatorVersion) ? "(none)" : def.generatorVersion);
        EditorGUILayout.LabelField("Content Hash", string.IsNullOrEmpty(def.contentHash) ? "(none)" : def.contentHash.Substring(0, Mathf.Min(16, def.contentHash.Length)) + "...");
        EditorGUILayout.LabelField("Two/Three Star Score", $"{def.twoStarScore} / {def.threeStarScore}");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Educational Message", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(def.educationalMessage, EditorStyles.wordWrappedLabel);

        EditorGUILayout.Space();

        if (GUILayout.Button("Preview Grid Size In Open Scene"))
        {
            PreviewGridSize(def);
        }

        using (new EditorGUI.DisabledScope(def.levelNumber < 7))
        {
            if (GUILayout.Button("Regenerate This Level With Next Seed"))
            {
                if (EditorUtility.DisplayDialog("Regenerate With Next Seed",
                    $"Regenerate Level {def.levelNumber} using the next deterministic seed?\n\n" +
                    "Only this level's asset is modified - every other level asset is left unchanged.",
                    "Regenerate", "Cancel"))
                {
                    CampaignFastTrackCommand2D.RegenerateWithNextSeed(def);
                }
            }
        }

        if (def.levelNumber < 7)
        {
            EditorGUILayout.HelpBox("Levels 1-6 are hand-authored and cannot be regenerated from this window.", MessageType.None);
        }
    }

    /// <summary>Same live-scene-edit pattern as CampaignGridPreview2D.Preview(width,height) - only resizes/rebuilds the visual grid cells, never spawns pipes/Source/Target, never saves the scene.</summary>
    private static void PreviewGridSize(CampaignLevelDefinition2D def)
    {
        var board = Object.FindFirstObjectByType<BoardManager2D>();
        if (board == null)
        {
            Debug.LogError("CampaignFastTrackPreviewWindow2D: No BoardManager2D found in the currently open scene. Open GameScene2D first.");
            return;
        }

        board.SetGridSize(def.gridWidth, def.gridHeight);
        board.BuildGrid();

        Debug.Log($"CampaignFastTrackPreviewWindow2D: Previewing Level {def.levelNumber}'s {def.gridWidth}x{def.gridHeight} grid " +
            $"(cellSize={board.CellSize:0.###}) on '{board.gameObject.name}'. This is a live scene edit (grid cell GameObjects were " +
            "rebuilt) - do NOT save the scene unless you intend to keep this size. Reload the scene without saving to discard it.");
    }
}
