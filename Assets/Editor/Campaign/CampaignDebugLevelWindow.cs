using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Generic "jump to any level" debug tool (Phase 8A Part T) covering the
/// pilot campaign's Levels 7-20 (and any level beyond, as the catalog grows)
/// without needing a new dedicated MenuItem method per level the way
/// DebugLevelCommands.cs's original Levels 4/5/6 shortcuts did. Reuses
/// DebugLevelCommands.SetCurrentLevel(int) - the exact same production
/// unlock-chain logic - so this is not a second, divergent implementation.
/// </summary>
public class CampaignDebugLevelWindow : EditorWindow
{
    private int levelNumber = 7;

    [MenuItem("YagmurRotasi2D/Debug/Set Current Level (Any)")]
    public static void Open()
    {
        var window = GetWindow<CampaignDebugLevelWindow>(true, "Set Current Level", true);
        window.minSize = new Vector2(280, 80);
    }

    private void OnGUI()
    {
        int totalLevels = LevelManager2D.ProductionLevelCount;

        EditorGUILayout.LabelField($"Production level count: {totalLevels}");
        levelNumber = EditorGUILayout.IntSlider("Level Number", levelNumber, 1, Mathf.Max(1, totalLevels));

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(levelNumber < 1 || levelNumber > totalLevels))
        {
            if (GUILayout.Button("Set Current Level"))
            {
                DebugLevelCommands.SetCurrentLevel(levelNumber);
                Close();
            }
        }
    }
}
