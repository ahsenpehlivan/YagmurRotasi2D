using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Binds the user's custom grid-cell artwork onto GridCell2D.prefab's
/// SpriteRenderer. Purely visual - never touches BoardManager2D coordinates, grid
/// width/height, FlowSolver2D, PipeTile2D, Source/Target positions, input
/// raycasting, scoring or move counting.
///
/// GridCell2D instances are created at RUNTIME by BoardManager2D.BuildGrid()
/// (called from LevelManager2D.Start()) - they are never baked into the saved
/// scene. Editing this one prefab ASSET is therefore sufficient: every instance
/// spawned at Play time automatically picks up the new sprite. No scene hierarchy
/// changes are made or needed for this integration.
///
/// Mode detected for the current asset (Assets/Art2D/FinalSprites/Grid/grid.png):
/// MODE 1 - single grid-cell sprite. Confirmed by visually opening the image during
/// implementation - it is one bordered square cell tile (pixel-art wood frame with
/// a plain center), not a subdivided 5x5 board image. Only one sliced sub-sprite
/// exists in the file, which is also consistent with a single repeatable tile.
/// If a future asset under the same path turns out to be a whole-board image or a
/// multi-sprite sheet, this binder deliberately refuses to guess (see the
/// ambiguity checks below) rather than silently reinterpreting it.
/// </summary>
public static class GridVisualBinder
{
    private const string GridFolder = "Assets/Art2D/FinalSprites/Grid";
    private const string PrefabFolder = "Assets/Prefabs2D";
    private const string GridCellPrefabName = "GridCell2D";

    [MenuItem("YagmurRotasi2D/Bind Custom Grid Visual")]
    public static void BindMenuCommand()
    {
        TryBindGridVisual(true);
    }

    /// <summary>Locates the custom grid texture, validates it is unambiguous (exactly one sliced sprite), corrects only non-destructive import settings, and binds it onto GridCell2D.prefab. Returns false (with a logged reason) without touching anything if the asset is missing or ambiguous.</summary>
    public static bool TryBindGridVisual(bool logDetails)
    {
        string texturePath = FindGridTexturePath();
        if (string.IsNullOrEmpty(texturePath))
        {
            Debug.LogWarning($"GridVisualBinder: no texture asset found under '{GridFolder}' (or elsewhere under Assets/Art2D). " +
                "Keeping the current grid-cell sprite.");
            return false;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogWarning($"GridVisualBinder: '{texturePath}' has no sliced Sprite sub-assets " +
                "(check Sprite Mode = Multiple/Single and that slicing has been applied). Keeping the current grid-cell sprite.");
            return false;
        }

        if (sprites.Length > 1)
        {
            Debug.LogWarning($"GridVisualBinder: '{texturePath}' contains {sprites.Length} sliced sprites - ambiguous which one " +
                "represents the grid cell/board, so nothing was bound. Candidates: " +
                string.Join(", ", sprites.Select(s => s.name)) +
                ". Assign the correct one manually on GridCell2D.prefab's SpriteRenderer, or slice down to a single sprite.");
            return false;
        }

        Sprite gridSprite = sprites[0];

        if (logDetails)
        {
            Debug.Log("GridVisualBinder: grid artwork inspected.\n" +
                $"  Path: {texturePath}\n" +
                $"  Texture size: {(texture != null ? $"{texture.width}x{texture.height}" : "unknown")}\n" +
                $"  Sliced sprite count: {sprites.Length}\n" +
                $"  Selected sprite: {gridSprite.name} @ rect ({gridSprite.rect.x},{gridSprite.rect.y}) {gridSprite.rect.width}x{gridSprite.rect.height}\n" +
                "  Detected mode: MODE 1 - single grid-cell sprite (one bordered tile, repeated per cell)");
        }

        float rectWidthBeforeReimport = gridSprite.rect.width;
        EnsureGridTextureImportQuality(texturePath, rectWidthBeforeReimport);

        // Reload after a possible reimport so the Sprite reference is current.
        gridSprite = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().FirstOrDefault();
        if (gridSprite == null)
        {
            Debug.LogWarning($"GridVisualBinder: lost the sprite reference after reimporting '{texturePath}'. Aborting bind.");
            return false;
        }

        string prefabPath = PrefabFolder + "/" + GridCellPrefabName + ".prefab";
        bool bound = BindGridCellPrefab(prefabPath, gridSprite, logDetails);

        if (bound)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        return bound;
    }

    /// <summary>Edits the GridCell2D PREFAB ASSET only (not any scene instance - there are none in edit mode). Changes only the sprite/color; Transform, sorting order and collider behavior (none exists) are left exactly as they already are.</summary>
    private static bool BindGridCellPrefab(string prefabPath, Sprite gridSprite, bool logDetails)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogWarning($"GridVisualBinder: prefab not found at '{prefabPath}'.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool success = false;
        try
        {
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"GridVisualBinder: '{prefabPath}' has no SpriteRenderer on its root GameObject. Skipping bind.");
                return false;
            }

            renderer.sprite = gridSprite;
            renderer.color = Color.white;

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            success = true;

            if (logDetails)
            {
                Debug.Log($"GridVisualBinder: bound '{gridSprite.name}' onto '{prefabPath}' " +
                    $"(sortingOrder={renderer.sortingOrder}, Transform/collider unchanged). " +
                    "Every GridCell2D instance BoardManager2D.BuildGrid() spawns at Play time will use this sprite automatically.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return success;
    }

    /// <summary>
    /// Pixel-art import profile (Point filter, Uncompressed, no mipmaps, Clamp wrap,
    /// Full Rect mesh) - matches the wood-tile pixel art style of grid.png. Pixels
    /// Per Unit is set so the sliced rect's WIDTH maps to exactly 1 world unit
    /// (BoardManager2D.CellSize = 1). The rect is 382x387px - only ~1.3% taller than
    /// wide - so this keeps the sprite at its native aspect ratio (SpriteRenderer
    /// never stretches X/Y independently in Simple draw mode) with a negligible
    /// sub-2% vertical overlap between neighboring cells instead of a gap. Only
    /// reimports if something actually changed; never touches sprite rects, names,
    /// pivots or slicing.
    /// </summary>
    private static void EnsureGridTextureImportQuality(string texturePath, float desiredPixelsPerUnit)
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
        Apply("spritePixelsPerUnit", importer.spritePixelsPerUnit, desiredPixelsPerUnit, v => importer.spritePixelsPerUnit = v);
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

        if (changed)
        {
            Debug.Log($"GridVisualBinder: corrected non-destructive texture import settings on '{texturePath}'.\n" +
                $"  Before: {string.Join(", ", before)}\n" +
                $"  After:  {string.Join(", ", after)}");
            importer.SaveAndReimport();
        }
    }

    private static string FindGridTexturePath()
    {
        if (AssetDatabase.IsValidFolder(GridFolder))
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { GridFolder });
            string path = guids.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }
        }

        // Fallback: search other Art2D folders for anything grid-named.
        string[] fallbackGuids = AssetDatabase.FindAssets("grid t:Texture2D", new[] { "Assets/Art2D" });
        return fallbackGuids.Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => !p.Contains("/Placeholder/"));
    }
}
