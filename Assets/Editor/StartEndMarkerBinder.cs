using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Binds the user's custom StartPoint/EndPoint marker art onto Source2D.prefab /
/// Target2D.prefab. Purely visual - never touches Source/Target coordinates, flow
/// validation, board registration, or level references. Source2D and Target2D
/// prefabs each already have exactly one dedicated visual SpriteRenderer on their
/// root GameObject (no existing child visual, no Collider2D) - so per the "reuse
/// instead of creating duplicates" rule, this binder swaps that root renderer's
/// sprite directly rather than adding a StartMarkerVisual/EndMarkerVisual child.
///
/// Discovery result: only Assets/Art2D/FinalSprites/BoardMarkers/StartPoint.png
/// exists. No EndPoint.png (or any end/target-marker-named asset) exists anywhere
/// under Assets/ - Target2D's current placeholder visual is therefore left
/// completely untouched, and this is reported clearly rather than guessed at.
/// </summary>
public static class StartEndMarkerBinder
{
    private const string BoardMarkersFolder = "Assets/Art2D/FinalSprites/BoardMarkers";
    private const string PrefabFolder = "Assets/Prefabs2D";

    // StartPoint.png's sliced rect is 226x349px at the default 100 PPU (2.26 x 3.49
    // world units at scale 1) - a uniform scale of 0.2 renders it at ~0.70 units
    // tall, in the middle of the requested 60%-85%-of-a-cell range, with its
    // 226:349 aspect ratio preserved exactly (uniform scale never stretches X/Y
    // independently).
    private const float DefaultMarkerScale = 0.2f;

    [MenuItem("YagmurRotasi2D/Bind Start End Marker Sprites")]
    public static void BindMenuCommand()
    {
        TryBindMarkers(true);
    }

    public static bool TryBindMarkers(bool logDetails)
    {
        bool startBound = BindMarker("StartPoint", PrefabFolder + "/Source2D.prefab", logDetails);
        bool endBound = BindMarker("EndPoint", PrefabFolder + "/Target2D.prefab", logDetails);

        if (!endBound && logDetails)
        {
            Debug.LogWarning("StartEndMarkerBinder: no 'EndPoint' asset found anywhere under Assets/ " +
                "(checked Assets/Art2D/FinalSprites/BoardMarkers/ and a wider Assets/ search). " +
                "Target2D's current placeholder visual is left completely unchanged. Add " +
                "Assets/Art2D/FinalSprites/BoardMarkers/EndPoint.png and re-run this command to bind it.");
        }

        return startBound || endBound;
    }

    private static bool BindMarker(string assetBaseName, string prefabPath, bool logDetails)
    {
        string texturePath = FindMarkerTexturePath(assetBaseName);
        if (string.IsNullOrEmpty(texturePath))
        {
            return false;
        }

        EnsureMarkerTextureImportQuality(texturePath);

        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().FirstOrDefault();
        if (sprite == null)
        {
            Debug.LogWarning($"StartEndMarkerBinder: '{texturePath}' has no sliced Sprite sub-asset " +
                "(check Sprite Mode = Multiple/Single and that slicing has been applied). Skipping.");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogWarning($"StartEndMarkerBinder: prefab not found at '{prefabPath}'.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool success = false;
        try
        {
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"StartEndMarkerBinder: '{prefabPath}' has no SpriteRenderer on its root GameObject. Skipping.");
                return false;
            }

            bool isFirstBind = renderer.sprite != sprite;
            renderer.sprite = sprite;
            renderer.color = Color.white;

            if (isFirstBind)
            {
                // Uniform scale only (never stretches X/Y differently). Applied only
                // the first time this marker's sprite actually changes; a manually
                // re-scaled marker (sprite already matching, i.e. a repeat run) is
                // left completely untouched here.
                root.transform.localScale = Vector3.one * DefaultMarkerScale;
            }

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            success = true;

            if (logDetails)
            {
                Debug.Log($"StartEndMarkerBinder: bound '{sprite.name}' onto '{prefabPath}' " +
                    $"(sortingOrder={renderer.sortingOrder} unchanged, scale {(isFirstBind ? $"set to default {DefaultMarkerScale}" : "preserved as manually edited")}). " +
                    "Source/Target coordinates, flow logic and board registration are untouched.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return success;
    }

    /// <summary>Pixel-art import profile (Point filter, Uncompressed, no mipmaps, Clamp wrap, Full Rect mesh) matching the marker art's pixel-art style. Never touches sprite rects/slicing/pivots or Pixels Per Unit (sizing is handled via the prefab's Transform scale instead, per the task's own guidance). Only reimports if something actually changed.</summary>
    private static void EnsureMarkerTextureImportQuality(string texturePath)
    {
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null) return;

        var before = new List<string>();
        var after = new List<string>();
        bool changed = false;

        void Apply<T>(string label, T current, T desired, System.Action<T> setter)
        {
            if (Equals(current, desired)) return;
            before.Add($"{label}={current}");
            setter(desired);
            after.Add($"{label}={desired}");
            changed = true;
        }

        Apply("textureType", importer.textureType, TextureImporterType.Sprite, v => importer.textureType = v);
        Apply("filterMode", importer.filterMode, FilterMode.Point, v => importer.filterMode = v);
        Apply("mipmapEnabled", importer.mipmapEnabled, false, v => importer.mipmapEnabled = v);
        Apply("textureCompression", importer.textureCompression, TextureImporterCompression.Uncompressed, v => importer.textureCompression = v);
        Apply("alphaIsTransparency", importer.alphaIsTransparency, true, v => importer.alphaIsTransparency = v);
        Apply("wrapMode", importer.wrapMode, TextureWrapMode.Clamp, v => importer.wrapMode = v);

        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        if (textureSettings.spriteMeshType != SpriteMeshType.FullRect)
        {
            before.Add($"spriteMeshType={textureSettings.spriteMeshType}");
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            after.Add($"spriteMeshType={textureSettings.spriteMeshType}");
            importer.SetTextureSettings(textureSettings);
            changed = true;
        }

        if (changed)
        {
            Debug.Log($"StartEndMarkerBinder: corrected non-destructive texture import settings on '{texturePath}'.\n" +
                $"  Before: {string.Join(", ", before)}\n" +
                $"  After:  {string.Join(", ", after)}");
            importer.SaveAndReimport();
        }
    }

    private static string FindMarkerTexturePath(string assetBaseName)
    {
        string preferredPath = BoardMarkersFolder + "/" + assetBaseName + ".png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(preferredPath) != null)
        {
            return preferredPath;
        }

        string[] guids = AssetDatabase.FindAssets(assetBaseName + " t:Texture2D");
        return guids.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
    }
}
