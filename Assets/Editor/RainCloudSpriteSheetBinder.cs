using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YagmurRotasi2D.Visual2D;

/// <summary>
/// Binds the already-imported/sliced "RainCloudSpriteSheet" texture's Sprite
/// sub-assets onto RainLoopFX's SpriteFrameAnimator2D. Never creates, re-slices,
/// renames or overwrites the sprite sheet asset itself - only reads its existing
/// sub-sprites via AssetDatabase.
/// </summary>
public static class RainCloudSpriteSheetBinder
{
    private const string SpriteSheetName = "RainCloudSpriteSheet";
    private const string PreferredFolder = "Assets/Art2D/FinalSprites/CloudRain";
    public const string RainLoopFXScenePath = "BoardRoot/CloudAndRain/RainLoopFX";

    // Matches "RainCloudSpriteSheet_12" or "RainCloudSpriteSheet 12" (both
    // separators are accepted; Unity's own default slicing uses an underscore).
    private static readonly Regex FrameNamePattern = new Regex(@"^" + SpriteSheetName + @"[ _](\d+)$");

    [MenuItem("YagmurRotasi2D/Bind Rain Cloud Sprite Sheet")]
    public static void BindMenuCommand()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogWarning($"RainCloudSpriteSheetBinder: GameScene2D is not the active open scene " +
                $"(active scene: '{activeScene.name}'). Open GameScene2D and try again.");
            return;
        }

        GameObject rainLoopFX = FindInScene(activeScene, RainLoopFXScenePath);
        if (rainLoopFX == null)
        {
            Debug.LogWarning($"RainCloudSpriteSheetBinder: Could not find '{RainLoopFXScenePath}' in GameScene2D.");
            return;
        }

        if (!TryLoadSortedFrames(out Sprite[] frames, out string texturePath, out string firstName, out string lastName, out string failureReason))
        {
            Debug.LogWarning($"RainCloudSpriteSheetBinder: {failureReason}");
            return;
        }

        ApplyToRainLoopFX(rainLoopFX, frames);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("RainCloudSpriteSheetBinder: bind successful.\n" +
            $"  Sprite sheet: {texturePath}\n" +
            $"  Frames found: {frames.Length}\n" +
            $"  First frame: {firstName}\n" +
            $"  Last frame: {lastName}\n" +
            $"  RainLoopFX path: {RainLoopFXScenePath}");
    }

    /// <summary>
    /// Used by the scene builder, which already holds a direct reference to the
    /// freshly-created RainLoopFX object (so it doesn't need a scene-hierarchy
    /// search). Returns true if binding succeeded; false (with a warning logged)
    /// if the sprite sheet isn't available, in which case the caller should keep
    /// using its placeholder single-frame visual.
    /// </summary>
    public static bool TryBindToRainLoopFX(GameObject rainLoopFX)
    {
        if (!TryLoadSortedFrames(out Sprite[] frames, out string texturePath, out string firstName, out string lastName, out string failureReason))
        {
            Debug.LogWarning($"RainCloudSpriteSheetBinder: {failureReason} Keeping the placeholder RainLoopFX visual.");
            return false;
        }

        ApplyToRainLoopFX(rainLoopFX, frames);

        Debug.Log("RainCloudSpriteSheetBinder: bound during scene build.\n" +
            $"  Sprite sheet: {texturePath}\n" +
            $"  Frames found: {frames.Length}\n" +
            $"  First frame: {firstName}\n" +
            $"  Last frame: {lastName}");

        return true;
    }

    private static void ApplyToRainLoopFX(GameObject rainLoopFX, Sprite[] frames)
    {
        SpriteRenderer targetRenderer = rainLoopFX.GetComponent<SpriteRenderer>();
        if (targetRenderer == null)
        {
            targetRenderer = rainLoopFX.AddComponent<SpriteRenderer>();
        }

        SpriteFrameAnimator2D animator = rainLoopFX.GetComponent<SpriteFrameAnimator2D>();
        if (animator == null)
        {
            animator = rainLoopFX.AddComponent<SpriteFrameAnimator2D>();
        }

        var so = new SerializedObject(animator);
        so.FindProperty("targetRenderer").objectReferenceValue = targetRenderer;

        SerializedProperty framesProp = so.FindProperty("frames");
        framesProp.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        so.FindProperty("framesPerSecond").floatValue = 10f;
        so.FindProperty("loop").boolValue = true;
        so.FindProperty("playOnEnable").boolValue = true;
        so.FindProperty("hideWhenStopped").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        targetRenderer.sprite = frames[0];

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(rainLoopFX);
    }

    private static bool TryLoadSortedFrames(
        out Sprite[] frames, out string texturePath, out string firstName, out string lastName, out string failureReason)
    {
        frames = null;
        firstName = null;
        lastName = null;

        texturePath = FindSpriteSheetAssetPath();
        if (string.IsNullOrEmpty(texturePath))
        {
            failureReason = $"Could not find a texture asset named '{SpriteSheetName}' anywhere under Assets/.";
            return false;
        }

        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        var allSprites = allAssets.OfType<Sprite>().ToList();

        if (allSprites.Count == 0)
        {
            failureReason = $"'{texturePath}' has no sliced Sprite sub-assets. " +
                "Check that Sprite Mode is set to Multiple and slicing has been applied in the Sprite Editor.";
            return false;
        }

        var matched = new List<(int index, Sprite sprite)>();
        foreach (Sprite sprite in allSprites)
        {
            Match match = FrameNamePattern.Match(sprite.name);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
            {
                matched.Add((index, sprite));
            }
        }

        if (matched.Count == 0)
        {
            failureReason = $"'{texturePath}' has {allSprites.Count} sliced sprite(s), but none match the expected " +
                $"'{SpriteSheetName}_<number>' naming pattern with a valid numeric suffix.";
            return false;
        }

        matched.Sort((a, b) => a.index.CompareTo(b.index));
        frames = matched.Select(m => m.sprite).ToArray();
        firstName = matched[0].sprite.name;
        lastName = matched[matched.Count - 1].sprite.name;
        failureReason = null;
        return true;
    }

    private static string FindSpriteSheetAssetPath()
    {
        string preferredPath = PreferredFolder + "/" + SpriteSheetName + ".png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(preferredPath) != null)
        {
            return preferredPath;
        }

        string[] guids = AssetDatabase.FindAssets(SpriteSheetName);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == SpriteSheetName)
            {
                return path;
            }
        }

        return null;
    }

    private static GameObject FindInScene(Scene scene, string hierarchyPath)
    {
        string[] segments = hierarchyPath.Split('/');
        if (segments.Length == 0)
        {
            return null;
        }

        GameObject rootMatch = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == segments[0])
            {
                rootMatch = root;
                break;
            }
        }

        if (rootMatch == null)
        {
            return null;
        }

        if (segments.Length == 1)
        {
            return rootMatch;
        }

        string relativePath = string.Join("/", segments, 1, segments.Length - 1);
        Transform found = rootMatch.transform.Find(relativePath);
        return found != null ? found.gameObject : null;
    }
}
