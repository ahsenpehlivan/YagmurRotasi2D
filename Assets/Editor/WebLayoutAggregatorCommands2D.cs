using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 9B Part L: runs every Web layout builder in dependency order behind
/// one menu command, and a separate command that also configures WebGL
/// PlayerSettings/Build Settings for a development test build. Every
/// individual builder called here is independently idempotent (find-or-
/// create by name, never duplicates EventSystems/Cameras/Canvases) - running
/// this command twice in a row produces the same result as running it once.
/// Does NOT invoke BuildPipeline.BuildPlayer - it only prepares scenes,
/// prefabs and PlayerSettings; the actual Web build is a separate, explicit
/// step the developer runs from Build Profiles / File > Build Settings.
/// </summary>
public static class WebLayoutAggregatorCommands2D
{
    [MenuItem("YagmurRotasi2D/Phase 9/Build Web Main Menu Layout")]
    public static void BuildWebMainMenuLayoutCommand() => MainMenuSceneBuilder2D.TryBuildMainMenu(true);

    [MenuItem("YagmurRotasi2D/Phase 9/Build Web Level Select Layout")]
    public static void BuildWebLevelSelectLayoutCommand()
    {
        if (!LevelButtonPrefabBuilder2D.TryBuildPrefab(true))
        {
            Debug.LogError("WebLayoutAggregatorCommands2D: LevelButton2D prefab build failed - aborting before Level Select scene build.");
            return;
        }
        LevelSelectSceneBuilder2D.TryBuildScene(true);
    }

    [MenuItem("YagmurRotasi2D/Phase 9/Build Web Gameplay Layout")]
    public static void BuildWebGameplayLayoutCommand()
    {
        SuccessPanelPrefabBuilder.TryBuildPrefab(true);
        InGameMenuPrefabBuilder.TryBuildPrefab(true);
        GameSceneWebLayoutBuilder2D.TryBuildLayout(true);
    }

    /// <summary>
    /// Runs every builder in dependency order: LevelButton2D prefab (Level
    /// Select needs it) -> Main Menu scene -> Level Select scene ->
    /// SuccessPanel2D/InGameMenu2D prefabs (rebuilding these updates their
    /// already-instantiated scene copies automatically via Unity's normal
    /// prefab propagation - no separate "Install" step needed, those
    /// commands only matter the FIRST time a prefab instance is placed) ->
    /// GameScene2D layout -> Build Settings scene order.
    /// </summary>
    [MenuItem("YagmurRotasi2D/Phase 9/Build All Web Layouts")]
    public static bool BuildAllWebLayoutsCommand()
    {
        var results = new List<(string Step, bool Ok)>();

        results.Add(("LevelButton2D prefab", LevelButtonPrefabBuilder2D.TryBuildPrefab(false)));
        results.Add(("MainMenuScene2D layout", MainMenuSceneBuilder2D.TryBuildMainMenu(false)));
        results.Add(("LevelSelectScene2D layout", LevelSelectSceneBuilder2D.TryBuildScene(false)));
        results.Add(("SuccessPanel2D prefab", SuccessPanelPrefabBuilder.TryBuildPrefab(false)));
        results.Add(("InGameMenu2D prefab", InGameMenuPrefabBuilder.TryBuildPrefab(false)));
        results.Add(("GameScene2D web layout", GameSceneWebLayoutBuilder2D.TryBuildLayout(false)));

        WebBuildConfig2D.EnsureSceneOrder(false);
        results.Add(("Build Settings scene order", true));

        bool allOk = true;
        var lines = new List<string> { "WebLayoutAggregatorCommands2D: Build All Web Layouts summary:" };
        foreach (var (step, ok) in results)
        {
            allOk &= ok;
            lines.Add($"  [{(ok ? "OK" : "FAILED")}] {step}");
        }

        if (allOk)
        {
            Debug.Log(string.Join("\n", lines));
        }
        else
        {
            Debug.LogError(string.Join("\n", lines) + "\nOne or more steps failed - see individual error logs above for details. Earlier successful steps were NOT rolled back (each builder already saved its own scene/prefab independently).");
        }

        return allOk;
    }

    /// <summary>Builds all layouts, configures WebGL PlayerSettings for a development test build, and reports readiness - does not invoke BuildPipeline.BuildPlayer itself (see class doc).</summary>
    [MenuItem("YagmurRotasi2D/Phase 9/Build Web Development Version")]
    public static void BuildWebDevelopmentVersionCommand()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogError("WebLayoutAggregatorCommands2D: Editor is still compiling scripts - wait for compilation to finish before running this command.");
            return;
        }

        bool layoutsOk = BuildAllWebLayoutsCommand();
        if (!layoutsOk)
        {
            Debug.LogError("WebLayoutAggregatorCommands2D: aborting development version prep - one or more layout builders failed (see log above).");
            return;
        }

        WebBuildConfig2D.ConfigureWebPlayerSettingsCommand();

        Debug.Log("WebLayoutAggregatorCommands2D: Web development version prep complete. " +
            "Scenes/prefabs/PlayerSettings are ready. To actually build: File > Build Profiles (or Build Settings) > select WebGL > " +
            "check 'Development Build' > Build. This command does not invoke the build itself.");
    }
}
