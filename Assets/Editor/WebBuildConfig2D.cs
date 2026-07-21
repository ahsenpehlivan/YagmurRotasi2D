using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 9B Part J: single source of truth for Build Settings scene order and
/// WebGL PlayerSettings - used by every scene builder (MainMenuSceneBuilder2D/
/// LevelSelectSceneBuilder2D/GameSceneWebLayoutBuilder2D previously each had
/// their own divergent scene-order logic; the Phase 9B audit found
/// LevelSelectScene2D was missing from Build Settings entirely because of
/// this drift - consolidating avoids ever reintroducing that class of bug).
/// Also configures WebGL PlayerSettings (product name, template, compression,
/// data caching, default canvas size) - idempotent, safe to re-run.
/// </summary>
public static class WebBuildConfig2D
{
    public const string MainMenuScenePath = "Assets/Scenes/MainMenuScene2D.unity";
    public const string LevelSelectScenePath = "Assets/Scenes/LevelSelectScene2D.unity";
    public const string GameplayScenePath = "Assets/Scenes/GameScene2D.unity";

    public const string WebGLTemplateName = "YagmurRotasiWeb";
    public const string WebGLTemplateSourceFolder = "Assets/WebGLTemplates/" + WebGLTemplateName;

    /// <summary>Forces MainMenuScene2D -> LevelSelectScene2D -> GameScene2D as Build Settings indices 0/1/2, preserving every other pre-existing scene (not removed) after them.</summary>
    public static void EnsureSceneOrder(bool logDetails)
    {
        var scenes = EditorBuildSettings.scenes.ToList();

        EditorBuildSettingsScene TakeOrCreate(string path)
        {
            EditorBuildSettingsScene found = scenes.FirstOrDefault(s => s.path == path);
            if (found == null)
            {
                return new EditorBuildSettingsScene(path, true);
            }
            scenes.Remove(found);
            found.enabled = true;
            return found;
        }

        EditorBuildSettingsScene menuScene = TakeOrCreate(MainMenuScenePath);
        EditorBuildSettingsScene levelSelectScene = TakeOrCreate(LevelSelectScenePath);
        EditorBuildSettingsScene gameScene = TakeOrCreate(GameplayScenePath);

        var finalList = new List<EditorBuildSettingsScene> { menuScene, levelSelectScene, gameScene };
        finalList.AddRange(scenes);
        EditorBuildSettings.scenes = finalList.ToArray();

        if (logDetails)
        {
            Debug.Log("WebBuildConfig2D: Build Settings scene order:\n" +
                string.Join("\n", finalList.Select((s, i) => $"  [{i}] {s.path} (enabled={s.enabled})")));
        }
    }

    /// <summary>
    /// Configures WebGL PlayerSettings for a first easy-to-host production
    /// build. Compression choice: Gzip with decompression fallback enabled
    /// (Brotli gives smaller builds but requires the HOSTING SERVER to send
    /// the correct Content-Encoding/Content-Type headers for .br files - a
    /// municipality static host cannot be assumed to have that configured;
    /// Gzip is far more commonly supported out of the box, and the
    /// decompression-fallback build (a small extra .js unityweb file Unity
    /// generates automatically) means the game still loads correctly even on
    /// a server that serves the compressed files with NO special headers at
    /// all - the fallback decompresses client-side. Tradeoff: fallback adds
    /// a small amount of extra download size/CPU decompression cost on first
    /// load compared to a server correctly configured for Brotli, but it is
    /// the safer default when server configuration is unknown/unguaranteed.
    /// </summary>
    [MenuItem("YagmurRotasi2D/Phase 9/Configure Web Player Settings")]
    public static void ConfigureWebPlayerSettingsCommand()
    {
        PlayerSettings.productName = "Yağmur Rotası";

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.template = "PROJECT:" + WebGLTemplateName;

        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.defaultWebScreenWidth = 1920;
        PlayerSettings.defaultWebScreenHeight = 1080;
        PlayerSettings.runInBackground = false;

        EnsureSceneOrder(true);

        bool templateExists = Directory.Exists(WebGLTemplateSourceFolder) && File.Exists(Path.Combine(WebGLTemplateSourceFolder, "index.html"));

        Debug.Log("WebBuildConfig2D: WebGL PlayerSettings configured.\n" +
            $"  productName = 'Yağmur Rotası'\n" +
            $"  compressionFormat = Gzip (decompressionFallback = true - see ConfigureWebPlayerSettingsCommand's doc comment for the tradeoff)\n" +
            $"  dataCaching = true\n" +
            $"  template = 'PROJECT:{WebGLTemplateName}' (found on disk: {templateExists})\n" +
            $"  defaultScreenWidth/Height (Web) = 1920x1080\n" +
            $"  runInBackground = false\n" +
            (templateExists ? "" : $"  WARNING: template files not found at '{WebGLTemplateSourceFolder}' - the template dropdown in Project Settings > Player > Resolution and Presentation may not show it until Unity re-scans Assets/WebGLTemplates/."));
    }
}
