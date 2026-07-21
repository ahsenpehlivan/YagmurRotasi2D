using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Visual2D;

/// <summary>
/// Discovers the Tee/Cross rows of the already-imported "pipes_tileset" sprite
/// sheet (the same sheet Corner/Straight already use - PipeWaterSpriteSheetBinder
/// already validates these two rows exist and are correctly sliced, but
/// deliberately never binds them) and builds/updates the PipeTee2D.prefab /
/// PipeCross2D.prefab gameplay prefabs from them: BaseVisual + WaterOverlay
/// hierarchy, BoxCollider2D, PipeTile2D, SpriteFrameAnimator2D, PipeWaterVisual2D -
/// exactly mirroring PipeCorner2D.prefab/PipeStraightWide2D.prefab. Never
/// re-slices, renames or overwrites the sprite sheet itself. Idempotent: re-running
/// updates the existing prefabs in place without adding duplicate children/
/// components. If either frame group is missing/invalid, that prefab is left
/// unbuilt and a warning is logged - no placeholder art is ever created.
/// </summary>
public static class BranchingPipeAssetBinder
{
    private const string SheetName = "pipes_tileset";
    private const string PreferredFolder = "Assets/Art2D/FinalSprites/Pipes";
    public const string PrefabFolder = "Assets/Prefabs2D";
    public const string TeePrefabPath = PrefabFolder + "/PipeTee2D.prefab";
    public const string CrossPrefabPath = PrefabFolder + "/PipeCross2D.prefab";

    private const int CellSize = 32;
    private const int FramesPerGroup = 4;
    private const int StaticColumnStartX = 128;
    private const int TeeRowY = 96;
    private const int CrossRowY = 64;

    private const float PipeColliderSize = 0.9f;

    // Fixed local art-alignment offset, chosen per pipe type. Corner/Straight
    // and Cross all share the same +90 degree offset already used by
    // PipeCorner2D.prefab/PipeStraightWide2D.prefab/PipeStraightNarrow2D.prefab
    // (BaseVisual and WaterOverlay m_LocalRotation: {x:0,y:0,z:0.7071068,w:0.7071068}).
    // Tee's row in this same tileset turned out to be drawn on a different
    // baseline: Phase 7F.4.2 Play Mode testing confirmed the Tee sprite needed
    // an additional 90-degree counterclockwise correction beyond that shared
    // offset (visible branches were rotated 90 degrees from PipeTile2D's
    // logical GetOpenDirections() - a visual bug only, the solver was already
    // reading the correct logical directions the whole time). Gameplay
    // direction logic is completely independent of this value regardless.
    internal static float GetVisualRotationOffset(PipeType2D pipeType)
    {
        return pipeType == PipeType2D.Tee ? 180f : 90f;
    }

    [MenuItem("YagmurRotasi2D/Bind T and Cross Pipe Assets")]
    public static void BindMenuCommand()
    {
        TryBindAll(true);
    }

    /// <summary>Returns true if at least one of PipeTee2D.prefab/PipeCross2D.prefab was built/updated.</summary>
    public static bool TryBindAll(bool logDetails)
    {
        string texturePath = FindSpriteSheetAssetPath();
        if (string.IsNullOrEmpty(texturePath))
        {
            Debug.LogWarning($"BranchingPipeAssetBinder: could not find a texture asset named '{SheetName}' " +
                "anywhere under Assets/. Tee/Cross prefabs were not built.");
            return false;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        List<Sprite> allSprites = allAssets.OfType<Sprite>().ToList();

        if (allSprites.Count == 0)
        {
            Debug.LogWarning($"BranchingPipeAssetBinder: '{texturePath}' has no sliced Sprite sub-assets. Tee/Cross prefabs were not built.");
            return false;
        }

        // Only sprites that are exactly CellSize x CellSize and left of the
        // static/special column(s) belong to a fill-animation group (mirrors
        // PipeWaterSpriteSheetBinder's own grouping rule for this same sheet).
        List<Sprite> validSprites = allSprites
            .Where(s => Mathf.RoundToInt(s.rect.width) == CellSize
                     && Mathf.RoundToInt(s.rect.height) == CellSize
                     && Mathf.RoundToInt(s.rect.x) < StaticColumnStartX)
            .ToList();

        Sprite[] teeFrames = ExtractRow(validSprites, TeeRowY);
        Sprite[] crossFrames = ExtractRow(validSprites, CrossRowY);

        bool teeOk = ValidateGroup("Tee", teeFrames);
        bool crossOk = ValidateGroup("Cross", crossFrames);

        if (logDetails)
        {
            Debug.Log("BranchingPipeAssetBinder: sprite sheet inspected.\n" +
                $"  Path: {texturePath}\n" +
                $"  Texture size: {(texture != null ? $"{texture.width}x{texture.height}" : "unknown")}\n" +
                $"  Cell size: {CellSize}x{CellSize}\n" +
                $"  Tee frames ({teeFrames.Length}): {DescribeRects(teeFrames)}\n" +
                $"  Cross frames ({crossFrames.Length}): {DescribeRects(crossFrames)}\n" +
                $"  Default visual orientation: raw sheet art for each row, frame 0, with a +{GetVisualRotationOffset(PipeType2D.Corner)} degree " +
                $"BaseVisual/WaterOverlay offset for Corner/Straight/Cross and +{GetVisualRotationOffset(PipeType2D.Tee)} degrees for Tee " +
                "(see GetVisualRotationOffset) - verify visually in Play Mode.");
        }

        bool teeBuilt = false;
        bool crossBuilt = false;

        if (teeOk)
        {
            teeBuilt = BuildOrUpdatePrefab(TeePrefabPath, "PipeTee2D", PipeType2D.Tee, teeFrames, isRotatable: true);
        }
        else
        {
            Debug.LogWarning("BranchingPipeAssetBinder: Tee frame group invalid/incomplete - PipeTee2D.prefab was not built/updated. " +
                "No placeholder art was created; fix the sheet's Tee row and re-run this command.");
        }

        if (crossOk)
        {
            crossBuilt = BuildOrUpdatePrefab(CrossPrefabPath, "PipeCross2D", PipeType2D.Cross, crossFrames, isRotatable: false);
        }
        else
        {
            Debug.LogWarning("BranchingPipeAssetBinder: Cross frame group invalid/incomplete - PipeCross2D.prefab was not built/updated. " +
                "No placeholder art was created; fix the sheet's Cross row and re-run this command.");
        }

        return teeBuilt || crossBuilt;
    }

    private static bool BuildOrUpdatePrefab(string prefabPath, string rootName, PipeType2D pipeType, Sprite[] frames, bool isRotatable)
    {
        bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        GameObject root = prefabExists ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(rootName);

        try
        {
            root.name = rootName;

            BoxCollider2D collider = root.GetComponent<BoxCollider2D>();
            if (collider == null) collider = root.AddComponent<BoxCollider2D>();
            RemoveExtraComponents(root, collider);
            collider.size = new Vector2(PipeColliderSize, PipeColliderSize);
            collider.isTrigger = false;

            Transform baseVisual = FindOrCreateChild(root.transform, "BaseVisual");
            Transform waterOverlay = FindOrCreateChild(root.transform, "WaterOverlay");
            RemoveExtraChildren(root.transform, baseVisual, waterOverlay);

            // Absolute assignment (never .Rotate()/incremental) - re-running
            // this command must always land on the same fixed offset, never
            // accumulate another 90/180 degrees on top of itself.
            Quaternion visualRotationOffset = Quaternion.Euler(0f, 0f, GetVisualRotationOffset(pipeType));

            baseVisual.localPosition = Vector3.zero;
            baseVisual.localRotation = visualRotationOffset;
            baseVisual.localScale = Vector3.one;

            SpriteRenderer baseRenderer = baseVisual.GetComponent<SpriteRenderer>();
            if (baseRenderer == null) baseRenderer = baseVisual.gameObject.AddComponent<SpriteRenderer>();
            RemoveExtraComponents(baseVisual.gameObject, baseRenderer);
            baseRenderer.sprite = frames[0];
            baseRenderer.color = Color.white;
            baseRenderer.sortingOrder = 1;

            waterOverlay.localPosition = Vector3.zero;
            waterOverlay.localRotation = visualRotationOffset;
            waterOverlay.localScale = Vector3.one;
            waterOverlay.gameObject.SetActive(false);

            SpriteRenderer overlayRenderer = waterOverlay.GetComponent<SpriteRenderer>();
            if (overlayRenderer == null) overlayRenderer = waterOverlay.gameObject.AddComponent<SpriteRenderer>();
            RemoveExtraComponents(waterOverlay.gameObject, overlayRenderer);
            overlayRenderer.sprite = frames[0];
            overlayRenderer.color = Color.white;
            overlayRenderer.sortingOrder = 2;

            SpriteFrameAnimator2D animator = waterOverlay.GetComponent<SpriteFrameAnimator2D>();
            if (animator == null) animator = waterOverlay.gameObject.AddComponent<SpriteFrameAnimator2D>();
            RemoveExtraComponents(waterOverlay.gameObject, animator);

            var animatorSO = new SerializedObject(animator);
            animatorSO.FindProperty("targetRenderer").objectReferenceValue = overlayRenderer;
            SerializedProperty framesProp = animatorSO.FindProperty("frames");
            framesProp.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
            animatorSO.FindProperty("framesPerSecond").floatValue = 12f;
            animatorSO.FindProperty("loop").boolValue = false;
            animatorSO.FindProperty("playOnEnable").boolValue = false;
            animatorSO.FindProperty("hideWhenStopped").boolValue = false;
            animatorSO.FindProperty("useUnscaledTime").boolValue = false;
            animatorSO.FindProperty("holdLastFrameOnComplete").boolValue = true;
            animatorSO.ApplyModifiedPropertiesWithoutUndo();

            PipeWaterVisual2D waterVisual = waterOverlay.GetComponent<PipeWaterVisual2D>();
            if (waterVisual == null) waterVisual = waterOverlay.gameObject.AddComponent<PipeWaterVisual2D>();
            RemoveExtraComponents(waterOverlay.gameObject, waterVisual);

            var waterVisualSO = new SerializedObject(waterVisual);
            waterVisualSO.FindProperty("waterOverlay").objectReferenceValue = waterOverlay.gameObject;
            waterVisualSO.FindProperty("animator").objectReferenceValue = animator;
            waterVisualSO.FindProperty("fallbackDuration").floatValue = 0.25f;
            waterVisualSO.ApplyModifiedPropertiesWithoutUndo();

            YagmurRotasi2D.Gameplay2D.PipeTile2D pipeTile = root.GetComponent<YagmurRotasi2D.Gameplay2D.PipeTile2D>();
            if (pipeTile == null) pipeTile = root.AddComponent<YagmurRotasi2D.Gameplay2D.PipeTile2D>();
            RemoveExtraComponents(root, pipeTile);

            var pipeTileSO = new SerializedObject(pipeTile);
            pipeTileSO.FindProperty("pipeType").intValue = (int)pipeType;
            pipeTileSO.FindProperty("rotationIndex").intValue = 0;
            pipeTileSO.FindProperty("baseVisualRenderer").objectReferenceValue = baseRenderer;
            pipeTileSO.FindProperty("waterVisual").objectReferenceValue = waterVisual;
            pipeTileSO.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            Debug.Log($"BranchingPipeAssetBinder: {(prefabExists ? "updated" : "created")} '{prefabPath}' " +
                $"({frames.Length} fill frames, pipeType={pipeType}, rotatable={isRotatable}).");
            return true;
        }
        finally
        {
            if (prefabExists)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void RemoveExtraChildren(Transform root, params Transform[] keep)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (System.Array.IndexOf(keep, child) < 0)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void RemoveExtraComponents<T>(GameObject go, T keep) where T : Component
    {
        T[] all = go.GetComponents<T>();
        foreach (T comp in all)
        {
            if (comp != keep)
            {
                Object.DestroyImmediate(comp);
            }
        }
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
            Debug.LogWarning($"BranchingPipeAssetBinder: '{label}' group has {frames.Length} frame(s), expected exactly {FramesPerGroup}.");
            return false;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            int expectedX = i * CellSize;
            int actualX = Mathf.RoundToInt(frames[i].rect.x);
            if (actualX != expectedX)
            {
                Debug.LogWarning($"BranchingPipeAssetBinder: '{label}' frame {i} is at x={actualX}, expected x={expectedX}.");
                return false;
            }
        }

        return true;
    }

    private static string DescribeRects(Sprite[] frames)
    {
        return string.Join(", ", frames.Select(f => $"{f.name}@({f.rect.x},{f.rect.y})"));
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
