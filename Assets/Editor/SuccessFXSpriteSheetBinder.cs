using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YagmurRotasi2D.Visual2D;

/// <summary>
/// Binds the already-imported/sliced flower bloom and duck walk sprite sheets onto
/// the SuccessFXController2D instance living in the currently open GameScene2D
/// (BoardRoot/SuccessFXZone). Operates on scene objects, not prefab assets - flower
/// and duck instances are placed directly in the scene (see
/// "Install Phase 7E Flower Duck FX" / "Install Phase 7E1 All Flowers Preserve
/// Clouds"), not spawned from prefabs per level load. Flowers get a unique sheet
/// each (1-to-1 by index); ducks may share one sheet across several instances.
/// Never renames, re-slices, overwrites or deletes any imported art. Contains no
/// rain-cloud-related code whatsoever.
/// </summary>
public static class SuccessFXSpriteSheetBinder
{
    private const string DuckFolder = "Assets/Art2D/FinalSprites/DucksSpriteSheet";
    private const string DuckSheetName = "ducksSpriteSheet";

    private const string FlowerFolder = "Assets/Art2D/FinalSprites/Flowers";

    [MenuItem("YagmurRotasi2D/Bind Flower and Duck Sprite Sheets")]
    public static void BindMenuCommand()
    {
        TryBindAll(true);
    }

    public static bool TryBindAll(bool logDetails)
    {
        SuccessFXController2D controller = Object.FindFirstObjectByType<SuccessFXController2D>();
        if (controller == null)
        {
            Debug.LogWarning("SuccessFXSpriteSheetBinder: No SuccessFXController2D found in the active scene. " +
                "Run 'YagmurRotasi2D > Install Phase 7E Flower Duck FX' first.");
            return false;
        }

        bool flowerBound = BindFlowersOnly(controller, logDetails);
        bool duckBound = BindDucksOnly(controller, logDetails);

        // Re-apply the default pre-success visual state now that real frames exist,
        // so the Scene view immediately shows flower frame 0 / hidden ducks.
        controller.PrepareInitialState();

        EditorUtility.SetDirty(controller);
        Scene activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        return flowerBound || duckBound;
    }

    /// <summary>Binds only the 8 unique flower sheets onto controller.flowerAnimators (1-to-1 by index). Does not touch ducks, does not call PrepareInitialState or save the scene - callers that need edit-mode visuals to update immediately should do that themselves (see FlowerAnimationRepair, which resets just the flowers it touched).</summary>
    public static bool BindFlowersOnly(SuccessFXController2D controller, bool logDetails)
    {
        var so = new SerializedObject(controller);
        SerializedProperty flowerAnimatorsProp = so.FindProperty("flowerAnimators");

        // ---------------- Flower sheet discovery: one unique variant per instance ----------------
        // Deterministic order: unsuffixed "FlowerSpriteSheet.png" first (-> Flower_0),
        // then "FlowerSpriteSheet (1).png".."(7).png" in ascending numeric order
        // (-> Flower_1..Flower_7). This is NOT alphabetical (alphabetically the
        // unsuffixed name sorts last, after "(1)".."(7)").
        List<string> flowerCandidates = FindFlowerCandidatePaths()
            .OrderBy(ParseFlowerVariantOrder)
            .ToList();

        if (logDetails)
        {
            Debug.Log($"SuccessFXSpriteSheetBinder: {flowerCandidates.Count} flower sheet candidate(s) found (in assignment order):\n" +
                string.Join("\n", flowerCandidates.Select((p, i) => $"  [{i}] {p}")));
        }

        int flowerSlotCount = flowerAnimatorsProp.arraySize;
        int flowerPairCount = Mathf.Min(flowerCandidates.Count, flowerSlotCount);

        if (flowerCandidates.Count != flowerSlotCount)
        {
            Debug.LogWarning($"SuccessFXSpriteSheetBinder: {flowerCandidates.Count} flower sheet(s) discovered but " +
                $"{flowerSlotCount} flower animator instance(s) exist in the scene. Binding the first {flowerPairCount} " +
                "1-to-1 by index; " +
                (flowerCandidates.Count > flowerSlotCount
                    ? $"{flowerCandidates.Count - flowerSlotCount} extra sheet(s) are unused."
                    : $"{flowerSlotCount - flowerCandidates.Count} flower instance(s) will keep their previous/empty frames."));
        }

        bool flowerBound = false;
        var assignmentLog = new List<string>();
        for (int i = 0; i < flowerPairCount; i++)
        {
            string flowerPath = flowerCandidates[i];
            var animatorProp = flowerAnimatorsProp.GetArrayElementAtIndex(i);
            var animator = animatorProp.objectReferenceValue as SpriteFrameAnimator2D;
            if (animator == null)
            {
                assignmentLog.Add($"  [{i}] {(animator == null ? "(no animator assigned in slot)" : animator.name)} <- {flowerPath} SKIPPED (no animator)");
                continue;
            }

            Sprite[] flowerFrames = LoadSlicedFramesSortedNumerically(flowerPath, out string firstName, out string lastName);
            if (flowerFrames.Length == 0)
            {
                Debug.LogWarning($"SuccessFXSpriteSheetBinder: '{flowerPath}' has no sliced Sprite sub-assets " +
                    "(check Sprite Mode = Multiple and that slicing has been applied). Skipping this slot.");
                continue;
            }

            BindOneAnimator(animator, flowerFrames, loop: false, holdLastFrame: true);
            flowerBound = true;
            assignmentLog.Add($"  [{i}] {animator.name} <- {flowerPath} ({flowerFrames.Length} frames: {firstName}..{lastName})");
        }

        if (logDetails)
        {
            Debug.Log("SuccessFXSpriteSheetBinder: flower assignment mapping:\n" + string.Join("\n", assignmentLog));
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return flowerBound;
    }

    /// <summary>Binds the single unambiguous duck sheet onto controller.duckAnimators (may be shared across several duck instances). Does not touch flowers, does not call PrepareInitialState or save the scene.</summary>
    public static bool BindDucksOnly(SuccessFXController2D controller, bool logDetails)
    {
        var so = new SerializedObject(controller);
        SerializedProperty duckAnimatorsProp = so.FindProperty("duckAnimators");

        // ---------------- Duck sheet discovery (unambiguous: single sheet) ----------------
        string duckPath = FindDuckSheetPath();
        bool duckBound = false;
        if (string.IsNullOrEmpty(duckPath))
        {
            Debug.LogWarning($"SuccessFXSpriteSheetBinder: No duck sprite sheet found (expected '{DuckFolder}/{DuckSheetName}.png'). " +
                "Ducks remain on their placeholder/empty state.");
        }
        else
        {
            Sprite[] duckFrames = LoadSlicedFramesSortedNumerically(duckPath, out string firstName, out string lastName);
            if (duckFrames.Length == 0)
            {
                Debug.LogWarning($"SuccessFXSpriteSheetBinder: '{duckPath}' has no sliced Sprite sub-assets " +
                    "(check Sprite Mode = Multiple and that slicing has been applied).");
            }
            else
            {
                BindAnimatorArray(duckAnimatorsProp, duckFrames, loop: true, holdLastFrame: true);
                duckBound = true;

                if (logDetails)
                {
                    Debug.Log("SuccessFXSpriteSheetBinder: duck sheet bound.\n" +
                        $"  Path: {duckPath}\n" +
                        $"  Frame count: {duckFrames.Length}\n" +
                        $"  First frame: {firstName}\n" +
                        $"  Last frame: {lastName}\n" +
                        $"  Animators bound: {duckAnimatorsProp.arraySize}");
                }
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return duckBound;
    }

    /// <summary>Binds the given frames (in order) onto every SpriteFrameAnimator2D in the array property (used for ducks, which may share one sheet across several instances), configuring loop/hold flags and the initial SpriteRenderer sprite.</summary>
    private static void BindAnimatorArray(SerializedProperty animatorsProp, Sprite[] frames, bool loop, bool holdLastFrame)
    {
        for (int i = 0; i < animatorsProp.arraySize; i++)
        {
            var animator = animatorsProp.GetArrayElementAtIndex(i).objectReferenceValue as SpriteFrameAnimator2D;
            if (animator == null)
                continue;

            BindOneAnimator(animator, frames, loop, holdLastFrame);
        }
    }

    /// <summary>Binds the given frames onto a single SpriteFrameAnimator2D (used for flowers, where each instance gets its own unique variant sheet), configuring loop/hold flags and the initial SpriteRenderer sprite.</summary>
    private static void BindOneAnimator(SpriteFrameAnimator2D animator, Sprite[] frames, bool loop, bool holdLastFrame)
    {
        var animatorSO = new SerializedObject(animator);
        SerializedProperty framesProp = animatorSO.FindProperty("frames");
        framesProp.arraySize = frames.Length;
        for (int f = 0; f < frames.Length; f++)
        {
            framesProp.GetArrayElementAtIndex(f).objectReferenceValue = frames[f];
        }

        animatorSO.FindProperty("loop").boolValue = loop;
        animatorSO.FindProperty("playOnEnable").boolValue = false;
        animatorSO.FindProperty("hideWhenStopped").boolValue = false;
        animatorSO.FindProperty("holdLastFrameOnComplete").boolValue = holdLastFrame;
        animatorSO.ApplyModifiedPropertiesWithoutUndo();

        var renderer = animatorSO.FindProperty("targetRenderer").objectReferenceValue as SpriteRenderer;
        if (renderer != null)
        {
            renderer.sprite = frames[0];
        }

        EditorUtility.SetDirty(animator);
    }

    /// <summary>Loads every sliced Sprite sub-asset at the given texture path and sorts by the numeric suffix of its name (not lexicographical), e.g. Name_2 before Name_10.</summary>
    private static Sprite[] LoadSlicedFramesSortedNumerically(string texturePath, out string firstName, out string lastName)
    {
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        Sprite[] sorted = allAssets.OfType<Sprite>()
            .Select(s => (sprite: s, index: ParseTrailingNumber(s.name)))
            .OrderBy(t => t.index)
            .Select(t => t.sprite)
            .ToArray();

        firstName = sorted.Length > 0 ? sorted[0].name : null;
        lastName = sorted.Length > 0 ? sorted[sorted.Length - 1].name : null;
        return sorted;
    }

    /// <summary>Extracts the trailing numeric suffix from a sliced sprite name, accepting both "Name_0" and "Name 0" styles. Returns int.MaxValue if no trailing number is found, so unrecognized names sort last instead of breaking the order.</summary>
    private static int ParseTrailingNumber(string spriteName)
    {
        Match match = Regex.Match(spriteName, @"[ _](\d+)$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
        {
            return value;
        }

        return int.MaxValue;
    }

    /// <summary>Deterministic flower assignment order: the unsuffixed "FlowerSpriteSheet.png" sorts first (0), then "FlowerSpriteSheet (N).png" sorts by N ascending. This is intentionally NOT alphabetical.</summary>
    private static int ParseFlowerVariantOrder(string path)
    {
        string nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(path);
        Match match = Regex.Match(nameWithoutExtension, @"\((\d+)\)$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
        {
            return value;
        }

        return 0;
    }

    private static string FindDuckSheetPath()
    {
        string preferredPath = DuckFolder + "/" + DuckSheetName + ".png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(preferredPath) != null)
        {
            return preferredPath;
        }

        string[] guids = AssetDatabase.FindAssets(DuckSheetName + " t:Texture2D");
        return guids.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
    }

    private static List<string> FindFlowerCandidatePaths()
    {
        string[] guids = AssetDatabase.FindAssets("FlowerSpriteSheet t:Texture2D", new[] { FlowerFolder });
        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
    }
}
