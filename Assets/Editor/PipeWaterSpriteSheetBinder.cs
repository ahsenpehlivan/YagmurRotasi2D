using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Visual2D;

/// <summary>
/// Binds the already-imported/sliced "pipes_tileset" texture's Corner/WideStraight/
/// NarrowStraight fill-animation frame groups onto the corresponding pipe PREFAB
/// ASSETS (not scene instances - pipes are respawned from these prefabs every level
/// load, so the binding must live on the asset to survive Reload/Next Level). Tee
/// and Cross groups are only detected/validated/logged; they are never bound to
/// gameplay prefabs (deferred to a later branching-pipe phase). Never creates,
/// re-slices, renames or overwrites the sprite sheet itself - only reads its
/// existing sub-sprites via AssetDatabase.
/// </summary>
public static class PipeWaterSpriteSheetBinder
{
    private const string SheetName = "pipes_tileset";
    private const string PreferredFolder = "Assets/Art2D/FinalSprites/Pipes";
    private const string PrefabFolder = "Assets/Prefabs2D";

    private const int ExpectedTextureWidth = 192;
    private const int ExpectedTextureHeight = 160;
    private const int CellSize = 32;
    private const int FramesPerGroup = 4;
    private const int StaticColumnStartX = 128;

    // Verified atlas rows (Sprite.rect uses a bottom-left origin).
    private const int CornerRowY = 128;
    private const int TeeRowY = 96;
    private const int CrossRowY = 64;
    private const int WideStraightRowY = 32;
    private const int NarrowStraightRowY = 0;

    [MenuItem("YagmurRotasi2D/Bind Pipe Fill Sprite Sheet")]
    public static void BindMenuCommand()
    {
        TryBindAll(true);
    }

    /// <summary>
    /// Attempts to locate pipes_tileset.png and bind its Corner/WideStraight/
    /// NarrowStraight groups onto PipeCorner2D.prefab / PipeStraightWide2D.prefab /
    /// PipeStraightNarrow2D.prefab. Returns true if at least one group was bound.
    /// Safe to call even if the prefabs or the sheet don't exist yet - logs a
    /// warning and leaves existing placeholder overlays untouched in that case.
    /// </summary>
    public static bool TryBindAll(bool logDetails)
    {
        string texturePath = FindSpriteSheetAssetPath();
        if (string.IsNullOrEmpty(texturePath))
        {
            Debug.LogWarning($"PipeWaterSpriteSheetBinder: Could not find a texture asset named '{SheetName}' " +
                "anywhere under Assets/. Keeping placeholder pipe overlays.");
            return false;
        }

        EnsurePipeTextureImportQuality(texturePath);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        List<Sprite> allSprites = allAssets.OfType<Sprite>().ToList();

        if (allSprites.Count == 0)
        {
            Debug.LogWarning($"PipeWaterSpriteSheetBinder: '{texturePath}' has no sliced Sprite sub-assets. " +
                "Check that Sprite Mode is set to Multiple and slicing has been applied. Keeping placeholder pipe overlays.");
            return false;
        }

        if (texture != null && (texture.width != ExpectedTextureWidth || texture.height != ExpectedTextureHeight))
        {
            Debug.LogWarning($"PipeWaterSpriteSheetBinder: '{texturePath}' is {texture.width}x{texture.height}, " +
                $"expected {ExpectedTextureWidth}x{ExpectedTextureHeight}. Continuing anyway using rect-based grouping.");
        }

        // Only sprites that are exactly CellSize x CellSize and left of the
        // static/special column(s) belong to a fill-animation group.
        List<Sprite> validSprites = allSprites
            .Where(s => Mathf.RoundToInt(s.rect.width) == CellSize
                     && Mathf.RoundToInt(s.rect.height) == CellSize
                     && Mathf.RoundToInt(s.rect.x) < StaticColumnStartX)
            .ToList();
        int ignoredCount = allSprites.Count - validSprites.Count;

        Sprite[] cornerFrames = ExtractRow(validSprites, CornerRowY);
        Sprite[] teeFrames = ExtractRow(validSprites, TeeRowY);
        Sprite[] crossFrames = ExtractRow(validSprites, CrossRowY);
        Sprite[] wideFrames = ExtractRow(validSprites, WideStraightRowY);
        Sprite[] narrowFrames = ExtractRow(validSprites, NarrowStraightRowY);

        bool cornerOk = ValidateGroup("Corner", cornerFrames);
        bool teeOk = ValidateGroup("Tee", teeFrames);
        bool crossOk = ValidateGroup("Cross", crossFrames);
        bool wideOk = ValidateGroup("WideStraight", wideFrames);
        bool narrowOk = ValidateGroup("NarrowStraight", narrowFrames);

        if (logDetails)
        {
            Debug.Log("PipeWaterSpriteSheetBinder: sprite sheet inspected.\n" +
                $"  Path: {texturePath}\n" +
                $"  Texture size: {(texture != null ? $"{texture.width}x{texture.height}" : "unknown")}\n" +
                $"  Cell size: {CellSize}x{CellSize}\n" +
                $"  Corner frames ({cornerFrames.Length}): {DescribeRects(cornerFrames)}\n" +
                $"  Tee frames ({teeFrames.Length}): {DescribeRects(teeFrames)}\n" +
                $"  Cross frames ({crossFrames.Length}): {DescribeRects(crossFrames)}\n" +
                $"  WideStraight frames ({wideFrames.Length}): {DescribeRects(wideFrames)}\n" +
                $"  NarrowStraight frames ({narrowFrames.Length}): {DescribeRects(narrowFrames)}\n" +
                $"  Ignored static/special sprites: {ignoredCount}");
        }

        if (teeOk)
        {
            Debug.Log($"PipeWaterSpriteSheetBinder: Tee frame group is ready ({DescribeRects(teeFrames)}) " +
                "but not bound - Tee gameplay/branching solver support is deferred to a later phase.");
        }

        if (crossOk)
        {
            Debug.Log($"PipeWaterSpriteSheetBinder: Cross frame group is ready ({DescribeRects(crossFrames)}) " +
                "but not bound - Cross gameplay/branching solver support is deferred to a later phase.");
        }

        bool anyBound = false;
        var boundPrefabs = new List<string>();

        if (cornerOk)
        {
            string path = PrefabFolder + "/PipeCorner2D.prefab";
            if (BindPrefab(path, cornerFrames))
            {
                anyBound = true;
                boundPrefabs.Add(path);
            }
        }
        else
        {
            Debug.LogWarning("PipeWaterSpriteSheetBinder: Corner frame group invalid/incomplete. Keeping placeholder overlay for PipeCorner2D.");
        }

        if (wideOk)
        {
            string path = PrefabFolder + "/PipeStraightWide2D.prefab";
            if (BindPrefab(path, wideFrames))
            {
                anyBound = true;
                boundPrefabs.Add(path);
            }
        }
        else
        {
            Debug.LogWarning("PipeWaterSpriteSheetBinder: WideStraight frame group invalid/incomplete. Keeping placeholder overlay for PipeStraightWide2D.");
        }

        if (narrowOk)
        {
            string path = PrefabFolder + "/PipeStraightNarrow2D.prefab";
            if (BindPrefab(path, narrowFrames))
            {
                anyBound = true;
                boundPrefabs.Add(path);
            }
        }
        else
        {
            Debug.LogWarning("PipeWaterSpriteSheetBinder: NarrowStraight frame group invalid/incomplete. Keeping placeholder overlay for PipeStraightNarrow2D.");
        }

        if (logDetails)
        {
            Debug.Log(boundPrefabs.Count > 0
                ? $"PipeWaterSpriteSheetBinder: prefabs bound: {string.Join(", ", boundPrefabs)}"
                : "PipeWaterSpriteSheetBinder: no prefabs were bound this run.");
        }

        return anyBound;
    }

    private static Sprite[] ExtractRow(List<Sprite> validSprites, int y)
    {
        return validSprites
            .Where(s => Mathf.RoundToInt(s.rect.y) == y)
            .OrderBy(s => Mathf.RoundToInt(s.rect.x))
            .ToArray();
    }

    private static bool ValidateGroup(string label, Sprite[] frames)
    {
        if (frames.Length != FramesPerGroup)
        {
            Debug.LogWarning($"PipeWaterSpriteSheetBinder: '{label}' group has {frames.Length} frame(s), expected exactly {FramesPerGroup}.");
            return false;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            int expectedX = i * CellSize;
            int actualX = Mathf.RoundToInt(frames[i].rect.x);
            if (actualX != expectedX)
            {
                Debug.LogWarning($"PipeWaterSpriteSheetBinder: '{label}' frame {i} is at x={actualX}, expected x={expectedX}.");
                return false;
            }
        }

        return true;
    }

    private static string DescribeRects(Sprite[] frames)
    {
        return string.Join(", ", frames.Select(f => $"{f.name}@({f.rect.x},{f.rect.y})"));
    }

    private static bool BindPrefab(string prefabPath, Sprite[] frames)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogWarning($"PipeWaterSpriteSheetBinder: Prefab not found at '{prefabPath}'. Build the scene first (which creates it), then re-run this command.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogWarning($"PipeWaterSpriteSheetBinder: Could not load prefab contents at '{prefabPath}'.");
            return false;
        }

        bool success = false;
        try
        {
            // Base sprite now lives on the BaseVisual child (which carries the fixed
            // +90 degree art-alignment offset) - the root itself has no SpriteRenderer.
            Transform baseVisualTransform = root.transform.Find("BaseVisual");
            Transform overlayTransform = root.transform.Find("WaterOverlay");
            SpriteRenderer baseRenderer = baseVisualTransform != null ? baseVisualTransform.GetComponent<SpriteRenderer>() : null;
            SpriteRenderer overlayRenderer = overlayTransform != null ? overlayTransform.GetComponent<SpriteRenderer>() : null;
            SpriteFrameAnimator2D overlayAnimator = overlayTransform != null ? overlayTransform.GetComponent<SpriteFrameAnimator2D>() : null;

            if (baseVisualTransform == null || overlayTransform == null || baseRenderer == null || overlayRenderer == null || overlayAnimator == null)
            {
                Debug.LogWarning($"PipeWaterSpriteSheetBinder: '{prefabPath}' is missing BaseVisual/WaterOverlay/SpriteRenderer/SpriteFrameAnimator2D. Skipping bind.");
                return false;
            }

            baseRenderer.sprite = frames[0];

            var so = new SerializedObject(overlayAnimator);
            SerializedProperty framesProp = so.FindProperty("frames");
            framesProp.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
            so.FindProperty("framesPerSecond").floatValue = 12f;
            so.FindProperty("loop").boolValue = false;
            so.FindProperty("playOnEnable").boolValue = false;
            so.FindProperty("holdLastFrameOnComplete").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Real art replaces the placeholder blue tint with the frame's true colors.
            overlayRenderer.sprite = frames[0];
            overlayRenderer.color = Color.white;

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            success = true;

            Debug.Log($"PipeWaterSpriteSheetBinder: bound {frames.Length} frames to '{prefabPath}'.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return success;
    }

    /// <summary>
    /// Corrects only non-destructive texture-import quality settings (filter mode,
    /// pixels-per-unit, compression, mesh type, wrap mode, max size) so the pixel art
    /// renders crisply at the correct 1-unit-per-cell size. Never touches sprite
    /// rects/names/pivots/slicing. Only reimports if something actually changed.
    /// </summary>
    private static void EnsurePipeTextureImportQuality(string texturePath)
    {
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        var before = new List<string>();
        var after = new List<string>();
        bool changed = false;

        void Apply<T>(string label, T current, T desired, System.Action<T> setter)
        {
            if (Equals(current, desired))
            {
                return;
            }

            before.Add($"{label}={current}");
            setter(desired);
            after.Add($"{label}={desired}");
            changed = true;
        }

        Apply("textureType", importer.textureType, TextureImporterType.Sprite, v => importer.textureType = v);
        Apply("spriteImportMode", importer.spriteImportMode, SpriteImportMode.Multiple, v => importer.spriteImportMode = v);
        Apply("spritePixelsPerUnit", importer.spritePixelsPerUnit, (float)CellSize, v => importer.spritePixelsPerUnit = v);
        Apply("filterMode", importer.filterMode, FilterMode.Point, v => importer.filterMode = v);
        Apply("mipmapEnabled", importer.mipmapEnabled, false, v => importer.mipmapEnabled = v);
        Apply("textureCompression", importer.textureCompression, TextureImporterCompression.Uncompressed, v => importer.textureCompression = v);
        Apply("alphaIsTransparency", importer.alphaIsTransparency, true, v => importer.alphaIsTransparency = v);
        Apply("wrapMode", importer.wrapMode, TextureWrapMode.Clamp, v => importer.wrapMode = v);

        // spriteMeshType lives on TextureImporterSettings, not as a direct
        // TextureImporter property.
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

        if (importer.maxTextureSize < 256)
        {
            before.Add($"maxTextureSize={importer.maxTextureSize}");
            importer.maxTextureSize = 256;
            after.Add($"maxTextureSize={importer.maxTextureSize}");
            changed = true;
        }

        if (changed)
        {
            Debug.Log($"PipeWaterSpriteSheetBinder: corrected non-destructive texture import settings on '{texturePath}'.\n" +
                $"  Before: {string.Join(", ", before)}\n" +
                $"  After:  {string.Join(", ", after)}");
            importer.SaveAndReimport();
        }
    }

    private static string FindSpriteSheetAssetPath()
    {
        string preferredPath = PreferredFolder + "/" + SheetName + ".png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(preferredPath) != null)
        {
            return preferredPath;
        }

        string[] guids = AssetDatabase.FindAssets(SheetName);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == SheetName)
            {
                return path;
            }
        }

        return null;
    }
}
