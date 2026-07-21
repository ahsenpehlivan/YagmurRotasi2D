using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Gameplay2D;
using YagmurRotasi2D.UI2D;
using YagmurRotasi2D.Visual2D;

public static class SceneBuilder2D
{
    private const string ArtFolder = "Assets/Art2D/Placeholder";
    private const string PrefabFolder = "Assets/Prefabs2D";
    private const string ScenePath = "Assets/Scenes/GameScene2D.unity";
    private const int TextureSize = 100;
    private const float PixelsPerUnit = 100f;

    [MenuItem("YagmurRotasi2D/Build Phase 1+2 Scene")]
    public static void BuildScene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScene(assets.grid, assets.straight, assets.corner, assets.source, assets.target);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 1+2 scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 3 Levels")]
    public static void BuildPhase3Scene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScenePhase3(assets.grid, assets.straight, assets.corner, assets.source, assets.target);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 3 scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 4 UI Score")]
    public static void BuildPhase4Scene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScenePhase4(assets.grid, assets.straight, assets.corner, assets.source, assets.target);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 4 scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 5 Water Animation")]
    public static void BuildPhase5Scene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScenePhase5(assets.grid, assets.straight, assets.corner, assets.source, assets.target, assets.waterDrop);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 5 scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 5B Cloud Rain Source")]
    public static void BuildPhase5BScene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScenePhase5B(assets.grid, assets.straight, assets.corner, assets.source, assets.target, assets.waterDrop, assets.cloud);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 5B scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 6 Visual FX Hooks")]
    public static void BuildPhase6Scene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScenePhase6(assets.grid, assets.straight, assets.corner, assets.source, assets.target, assets.waterDrop, assets.cloud);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 6 scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 6B Fixed Corner Route")]
    public static void BuildPhase6BScene()
    {
        var assets = BuildPlaceholderAssets();
        // Scene assembly is identical to Phase 6: Cloud2D/RainLoopFX already anchor at
        // (-2, 2), which is exactly the new fixed Source2D grid position, so no scene-
        // builder code needed to change - only LevelManager2D's level data changed.
        BuildGameScenePhase6(assets.grid, assets.straight, assets.corner, assets.source, assets.target, assets.waterDrop, assets.cloud);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 6B scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 7A Rain Cloud Sprite Sheet")]
    public static void BuildPhase7AScene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScenePhase7A(assets.grid, assets.straight, assets.corner, assets.source, assets.target, assets.waterDrop, assets.cloud);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 7A scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 7B Pipe Fill Animations")]
    public static void BuildPhase7BScene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScenePhase7B(
            assets.grid, assets.straight, assets.straightNarrow, assets.corner,
            assets.source, assets.target, assets.waterDrop, assets.cloud);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 7B scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 7B Fix Pipe Alignment")]
    public static void BuildPhase7BFixAlignmentScene()
    {
        var assets = BuildPlaceholderAssets();
        // Scene assembly is identical to "Build Phase 7B Pipe Fill Animations": the
        // actual fix (BaseVisual/WaterOverlay +90 degree art-alignment offset +
        // texture import quality correction) lives in the shared
        // CreatePipePrefab/PipeTile2D/PipeWaterSpriteSheetBinder code this reuses, so
        // no separate scene-builder method is needed.
        BuildGameScenePhase7B(
            assets.grid, assets.straight, assets.straightNarrow, assets.corner,
            assets.source, assets.target, assets.waterDrop, assets.cloud);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 7B pipe alignment fix scene build complete.");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 7B.1 Remove Water Drop")]
    public static void BuildPhase7B1Scene()
    {
        var assets = BuildPlaceholderAssets();
        // WaterDrop2D.prefab is still generated by BuildPlaceholderAssets (older
        // commands still reference it), but this method deliberately never receives
        // or wires it - WaterFlowAnimator2D no longer has a drop-related field at all.
        BuildGameScenePhase7B1(
            assets.grid, assets.straight, assets.straightNarrow, assets.corner,
            assets.source, assets.target, assets.cloud);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 7B.1 scene build complete (WaterDrop2D removed).");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 7C Polished Mobile Layout")]
    public static void BuildPhase7CScene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScenePhase7C(
            assets.grid, assets.straight, assets.straightNarrow, assets.corner,
            assets.source, assets.target, assets.cloud);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 7C scene build complete (polished mobile Canvas layout).");
    }

    [MenuItem("YagmurRotasi2D/Build Phase 7C1 Grass Success Area")]
    public static void BuildPhase7C1Scene()
    {
        var assets = BuildPlaceholderAssets();
        BuildGameScenePhase7C1(
            assets.grid, assets.straight, assets.straightNarrow, assets.corner,
            assets.source, assets.target, assets.cloud);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SceneBuilder2D: Phase 7C.1 scene build complete (grass success area added).");
    }

    private static (GameObject grid, GameObject straight, GameObject corner, GameObject source, GameObject target, GameObject waterDrop, GameObject cloud, GameObject straightNarrow) BuildPlaceholderAssets()
    {
        EnsureFolders();

        Material unlitMaterial = CreateUnlitMaterial();

        Sprite gridSprite = CreateAndImportSprite("grid_cell", CreateSquareTexture(
            new Color32(76, 175, 80, 255), new Color32(56, 142, 60, 255), 4));
        // Straight/Corner placeholders are pre-rotated -90 degrees relative to their
        // final on-screen look: CreatePipePrefab applies a fixed +90 degree offset to
        // BaseVisual/WaterOverlay (matching the real pipes_tileset artwork's actual
        // orientation), so these unrotated textures must already be vertical / Right+Down
        // to land on horizontal / Up+Right once that offset is applied.
        Sprite straightWideSprite = CreateAndImportSprite("pipe_straight_wide", CreateBarTexture(
            new Color32(128, 128, 128, 255), false, 0.4f));
        Sprite straightNarrowSprite = CreateAndImportSprite("pipe_straight_narrow", CreateBarTexture(
            new Color32(128, 128, 128, 255), false, 0.2f));
        Sprite cornerSprite = CreateAndImportSprite("pipe_corner", CreateCornerTexture(
            new Color32(128, 128, 128, 255), armPointsDown: true));
        Sprite sourceSprite = CreateAndImportSprite("source", CreateCircleTexture(
            new Color32(33, 150, 243, 255)));
        Sprite targetSprite = CreateAndImportSprite("target", CreateCircleTexture(
            new Color32(46, 125, 50, 255)));
        Sprite cloudSprite = CreateAndImportSprite("cloud", CreateCircleTexture(
            new Color32(235, 235, 240, 255)));
        Sprite flowerSprite = CreateAndImportSprite("flower_bloom", CreateCircleTexture(
            new Color32(233, 30, 99, 255)));
        Sprite duckSprite = CreateAndImportSprite("duck_walk", CreateCircleTexture(
            new Color32(255, 193, 7, 255)));

        GameObject gridPrefab = CreateGridCellPrefab(gridSprite, unlitMaterial);
        // "PipeStraightWide2D" also fills the tuple's legacy "straight" slot so every
        // pre-Phase-7B build command (which only knows one straight prefab) keeps
        // compiling unchanged.
        GameObject straightWidePrefab = CreatePipePrefab("PipeStraightWide2D", straightWideSprite, PipeType2D.Straight, unlitMaterial);
        GameObject straightNarrowPrefab = CreatePipePrefab("PipeStraightNarrow2D", straightNarrowSprite, PipeType2D.Straight, unlitMaterial);
        GameObject cornerPrefab = CreatePipePrefab("PipeCorner2D", cornerSprite, PipeType2D.Corner, unlitMaterial);
        GameObject sourcePrefab = CreateMarkerPrefab("Source2D", sourceSprite, unlitMaterial);
        // Target2D also carries the FlowerBloomFX/DuckWalkFX placeholder children +
        // TargetFX2D, so they respawn hidden with every new level automatically.
        GameObject targetPrefab = CreateTargetPrefab(targetSprite, unlitMaterial, flowerSprite, duckSprite);
        // Reuses the existing blue circle sprite (source) at a smaller scale and higher
        // sorting order so it reads as a small drop rendered above the pipes.
        GameObject waterDropPrefab = CreateMarkerPrefab("WaterDrop2D", sourceSprite, unlitMaterial, scale: 0.5f, sortingOrder: 5);
        // Simple light grey/white oval placeholder, above the grid but not above UI
        // (world-space sprites never draw over a Screen Space Overlay canvas).
        GameObject cloudPrefab = CreateMarkerPrefab("Cloud2D", cloudSprite, unlitMaterial, scale: 1.6f, sortingOrder: 2);

        return (gridPrefab, straightWidePrefab, cornerPrefab, sourcePrefab, targetPrefab, waterDropPrefab, cloudPrefab, straightNarrowPrefab);
    }

    private static void EnsureFolders()
    {
        CreateFolderIfMissing("Assets", "Scripts");
        CreateFolderIfMissing("Assets", "Art2D");
        CreateFolderIfMissing("Assets/Art2D", "Placeholder");
        CreateFolderIfMissing("Assets", "Prefabs2D");
        CreateFolderIfMissing("Assets", "Audio");
    }

    private static void CreateFolderIfMissing(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static Material CreateUnlitMaterial()
    {
        string path = ArtFolder + "/UnlitSprite2D.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
        {
            Debug.LogWarning("SceneBuilder2D: URP unlit sprite shader not found, falling back to Sprites/Default.");
            shader = Shader.Find("Sprites/Default");
        }

        Material mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ---------- Texture generation ----------

    private static Texture2D CreateSquareTexture(Color32 fill, Color32 border, int borderThickness)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                bool isBorder = x < borderThickness || x >= TextureSize - borderThickness ||
                                y < borderThickness || y >= TextureSize - borderThickness;
                pixels[y * TextureSize + x] = isBorder ? border : fill;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateBarTexture(Color32 color, bool horizontal, float thicknessRatio = 0.4f)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[TextureSize * TextureSize];
        int thickness = Mathf.RoundToInt(TextureSize * thicknessRatio);
        int start = (TextureSize - thickness) / 2;
        int end = start + thickness;
        var clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                bool filled = horizontal ? (y >= start && y < end) : (x >= start && x < end);
                pixels[y * TextureSize + x] = filled ? color : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateCornerTexture(Color32 color, bool armPointsDown = false)
    {
        // armPointsDown=false: unrotated shape is Up + Right (used directly, no BaseVisual offset).
        // armPointsDown=true: unrotated shape is Right + Down, which after the pipe
        // prefab's fixed +90 degree BaseVisual/WaterOverlay offset becomes Up + Right
        // - matching the imported pipes_tileset artwork's actual orientation.
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[TextureSize * TextureSize];
        int thickness = Mathf.RoundToInt(TextureSize * 0.4f);
        int half = thickness / 2;
        int center = TextureSize / 2;
        int start = (TextureSize - thickness) / 2;
        int end = start + thickness;
        var clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                bool verticalArm = (x >= start && x < end) &&
                    (armPointsDown ? (y <= center + half) : (y >= center - half));
                bool horizontalArm = (y >= start && y < end) && (x >= center - half);
                pixels[y * TextureSize + x] = (verticalArm || horizontalArm) ? color : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateCircleTexture(Color32 color)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[TextureSize * TextureSize];
        float radius = TextureSize * 0.4f;
        Vector2 center = new Vector2(TextureSize / 2f, TextureSize / 2f);
        var clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * TextureSize + x] = dist <= radius ? color : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Sprite CreateAndImportSprite(string name, Texture2D texture)
    {
        string pngPath = ArtFolder + "/" + name + ".png";
        File.WriteAllBytes(pngPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
    }

    // ---------- Prefab creation ----------

    private static GameObject CreateGridCellPrefab(Sprite sprite, Material material)
    {
        var go = new GameObject("GridCell2D");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = material;
        sr.sortingOrder = 0;

        string path = PrefabFolder + "/GridCell2D.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject CreatePipePrefab(string name, Sprite sprite, PipeType2D type, Material material)
    {
        var go = new GameObject(name);

        // Root: gameplay-only. Holds PipeTile2D + BoxCollider2D and is rotated
        // exclusively by rotationIndex (-rotationIndex * 90). It never carries a
        // SpriteRenderer and is never rotated to compensate for artwork orientation.
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one * 0.9f;

        // Fixed art-alignment correction: the imported pipe artwork (and our
        // placeholder textures, pre-compensated to match) is authored 90 degrees
        // offset from the gameplay rotation table. This +90 degree local rotation is
        // applied exactly once, here, to both visual children - never inside
        // PipeTile2D's rotation logic and never duplicated elsewhere.
        var baseVisualGO = new GameObject("BaseVisual");
        baseVisualGO.transform.SetParent(go.transform, false);
        baseVisualGO.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var baseSr = baseVisualGO.AddComponent<SpriteRenderer>();
        baseSr.sprite = sprite;
        baseSr.sharedMaterial = material;
        baseSr.sortingOrder = 1;

        // Water overlay: hidden by default, reuses the pipe's own sprite tinted blue
        // as a safe placeholder fallback (PipeWaterVisual2D.PlayFill falls back to
        // this look when no real 4-frame array is bound). Carries the same
        // +90 degree offset as BaseVisual so it always stays perfectly aligned with it.
        var overlayGO = new GameObject("WaterOverlay");
        overlayGO.transform.SetParent(go.transform, false);
        overlayGO.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var overlaySr = overlayGO.AddComponent<SpriteRenderer>();
        overlaySr.sprite = sprite;
        overlaySr.sharedMaterial = material;
        overlaySr.sortingOrder = 2;
        overlaySr.color = Color.blue;

        var overlayAnimator = overlayGO.AddComponent<SpriteFrameAnimator2D>();
        var overlayAnimatorSO = new SerializedObject(overlayAnimator);
        overlayAnimatorSO.FindProperty("targetRenderer").objectReferenceValue = overlaySr;
        overlayAnimatorSO.FindProperty("framesPerSecond").floatValue = 12f;
        overlayAnimatorSO.FindProperty("loop").boolValue = false;
        overlayAnimatorSO.FindProperty("playOnEnable").boolValue = false;
        overlayAnimatorSO.FindProperty("hideWhenStopped").boolValue = false;
        overlayAnimatorSO.FindProperty("holdLastFrameOnComplete").boolValue = true;
        overlayAnimatorSO.ApplyModifiedPropertiesWithoutUndo();

        var pipeWaterVisual = overlayGO.AddComponent<PipeWaterVisual2D>();
        var pipeWaterVisualSO = new SerializedObject(pipeWaterVisual);
        pipeWaterVisualSO.FindProperty("waterOverlay").objectReferenceValue = overlayGO;
        pipeWaterVisualSO.FindProperty("animator").objectReferenceValue = overlayAnimator;
        pipeWaterVisualSO.ApplyModifiedPropertiesWithoutUndo();

        overlayGO.SetActive(false);

        var pipe = go.AddComponent<PipeTile2D>();
        var so = new SerializedObject(pipe);
        so.FindProperty("pipeType").enumValueIndex = (int)type;
        so.FindProperty("baseVisualRenderer").objectReferenceValue = baseSr;
        so.FindProperty("waterVisual").objectReferenceValue = pipeWaterVisual;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = PrefabFolder + "/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject CreateTargetPrefab(Sprite sprite, Material material, Sprite flowerSprite, Sprite duckSprite)
    {
        var go = new GameObject("Target2D");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = material;
        sr.sortingOrder = 1;

        // Target success FX placeholders: hidden by default, shown once via
        // TargetFX2D.PlaySuccessFX() after the water drop arrives. Swap sprites/add
        // real animation later without touching WaterFlowAnimator2D or UIManager2D.
        var flowerGO = new GameObject("FlowerBloomFX");
        flowerGO.transform.SetParent(go.transform, false);
        var flowerSr = flowerGO.AddComponent<SpriteRenderer>();
        flowerSr.sprite = flowerSprite;
        flowerSr.sharedMaterial = material;
        flowerSr.sortingOrder = 3;
        flowerGO.transform.localPosition = new Vector3(0.35f, 0.35f, 0f);
        flowerGO.transform.localScale = Vector3.one * 0.5f;
        flowerGO.SetActive(false);

        var duckGO = new GameObject("DuckWalkFX");
        duckGO.transform.SetParent(go.transform, false);
        var duckSr = duckGO.AddComponent<SpriteRenderer>();
        duckSr.sprite = duckSprite;
        duckSr.sharedMaterial = material;
        duckSr.sortingOrder = 3;
        duckGO.transform.localPosition = new Vector3(-0.35f, -0.35f, 0f);
        duckGO.transform.localScale = Vector3.one * 0.5f;
        duckGO.SetActive(false);

        var targetFX = go.AddComponent<TargetFX2D>();
        var fxSO = new SerializedObject(targetFX);
        fxSO.FindProperty("flowerBloomFX").objectReferenceValue = flowerGO;
        fxSO.FindProperty("duckWalkFX").objectReferenceValue = duckGO;
        fxSO.ApplyModifiedPropertiesWithoutUndo();

        string path = PrefabFolder + "/Target2D.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject CreateMarkerPrefab(string name, Sprite sprite, Material material, float scale = 1f, int sortingOrder = 1)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = material;
        sr.sortingOrder = sortingOrder;
        go.transform.localScale = Vector3.one * scale;

        string path = PrefabFolder + "/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------- Scene assembly ----------

    private static void BuildGameScene(
        GameObject gridPrefab, GameObject straightPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var effectsContainer = new GameObject("Effects");
        effectsContainer.transform.SetParent(boardRoot.transform);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("pipeStraightPrefab").objectReferenceValue = straightPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject resultTextGO = CreateUIText("ResultText", canvasGO.transform, "Hazır",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -120f), new Vector2(800f, 100f));
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 56;
        resultText.color = Color.black;

        GameObject buttonGO = CreateUIButton("StartWaterButton", canvasGO.transform, "Suyu Başlat",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 160f), new Vector2(500f, 140f));
        Button startButton = buttonGO.GetComponent<Button>();

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 3 scene: same board/manager setup as Phase 1+2, plus LevelNameText,
    // ReloadButton and NextLevelButton wired into UIManager2D. Kept as its own method
    // (rather than branching BuildGameScene) so the Phase 1+2 command stays untouched.
    private static void BuildGameScenePhase3(
        GameObject gridPrefab, GameObject straightPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var effectsContainer = new GameObject("Effects");
        effectsContainer.transform.SetParent(boardRoot.transform);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("pipeStraightPrefab").objectReferenceValue = straightPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject levelNameGO = CreateUIText("LevelNameText", canvasGO.transform, "",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -50f), new Vector2(900f, 90f));
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 48;
        levelNameText.color = Color.black;

        GameObject resultTextGO = CreateUIText("ResultText", canvasGO.transform, "Hazır",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -160f), new Vector2(800f, 100f));
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 56;
        resultText.color = Color.black;

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", canvasGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-360f, 160f), new Vector2(260f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 34);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", canvasGO.transform, "Suyu Başlat",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 160f), new Vector2(420f, 140f));
        Button startButton = startButtonGO.GetComponent<Button>();

        GameObject nextButtonGO = CreateUIButton("NextLevelButton", canvasGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(360f, 160f), new Vector2(260f, 120f));
        Button nextButton = nextButtonGO.GetComponent<Button>();
        nextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(nextButtonGO, 34);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 4 scene: same board/manager setup as Phase 3, plus ScoreManager2D,
    // MoveText/ScoreText and an InfoPanel (title/stars/score/path length/description
    // + its own Next Level button) wired into UIManager2D. Kept as its own method so
    // the Phase 1+2 and Phase 3 commands stay untouched.
    private static void BuildGameScenePhase4(
        GameObject gridPrefab, GameObject straightPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);
        var scoreManagerGO = new GameObject("ScoreManager2D");
        scoreManagerGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var effectsContainer = new GameObject("Effects");
        effectsContainer.transform.SetParent(boardRoot.transform);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        ScoreManager2D scoreManager = scoreManagerGO.AddComponent<ScoreManager2D>();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        levelSO.FindProperty("pipeStraightPrefab").objectReferenceValue = straightPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject levelNameGO = CreateUIText("LevelNameText", canvasGO.transform, "",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -50f), new Vector2(900f, 90f));
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 48;
        levelNameText.color = Color.black;

        GameObject moveTextGO = CreateUIText("MoveText", canvasGO.transform, "Hamle: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(-220f, -150f), new Vector2(380f, 70f));
        Text moveText = moveTextGO.GetComponent<Text>();
        moveText.fontSize = 40;
        moveText.color = Color.black;

        GameObject scoreTextGO = CreateUIText("ScoreText", canvasGO.transform, "Puan: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(220f, -150f), new Vector2(380f, 70f));
        Text scoreText = scoreTextGO.GetComponent<Text>();
        scoreText.fontSize = 40;
        scoreText.color = Color.black;

        GameObject resultTextGO = CreateUIText("ResultText", canvasGO.transform, "Hazır",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 100f));
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 56;
        resultText.color = Color.black;

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", canvasGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-360f, 160f), new Vector2(260f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 34);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", canvasGO.transform, "Suyu Başlat",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 160f), new Vector2(420f, 140f));
        Button startButton = startButtonGO.GetComponent<Button>();

        GameObject nextButtonGO = CreateUIButton("NextLevelButton", canvasGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(360f, 160f), new Vector2(260f, 120f));
        Button nextButton = nextButtonGO.GetComponent<Button>();
        nextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(nextButtonGO, 34);

        // InfoPanel: centered card, sized/positioned to avoid the bottom button row
        // and the top texts. Hidden by default; only UIManager2D activates it.
        GameObject infoPanelGO = CreateUIPanel("InfoPanel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(900f, 1300f), new Color(1f, 1f, 1f, 0.96f));

        GameObject infoTitleGO = CreateUIText("InfoTitleText", infoPanelGO.transform, "Tebrikler!",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(800f, 90f));
        Text infoTitleText = infoTitleGO.GetComponent<Text>();
        infoTitleText.fontSize = 54;
        infoTitleText.color = Color.black;

        GameObject infoStarGO = CreateUIText("InfoStarText", infoPanelGO.transform, "Yildiz: * (1/3)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -160f), new Vector2(800f, 70f));
        Text infoStarText = infoStarGO.GetComponent<Text>();
        infoStarText.fontSize = 42;
        infoStarText.color = Color.black;

        GameObject infoScoreGO = CreateUIText("InfoScoreText", infoPanelGO.transform, "Puan: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 70f));
        Text infoScoreText = infoScoreGO.GetComponent<Text>();
        infoScoreText.fontSize = 40;
        infoScoreText.color = Color.black;

        GameObject infoPathLengthGO = CreateUIText("InfoPathLengthText", infoPanelGO.transform, "Yol Uzunluğu: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(800f, 70f));
        Text infoPathLengthText = infoPathLengthGO.GetComponent<Text>();
        infoPathLengthText.fontSize = 40;
        infoPathLengthText.color = Color.black;

        GameObject infoDescriptionGO = CreateUIText("InfoText", infoPanelGO.transform, "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -460f), new Vector2(800f, 320f));
        Text infoDescriptionText = infoDescriptionGO.GetComponent<Text>();
        infoDescriptionText.fontSize = 36;
        infoDescriptionText.color = Color.black;
        infoDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        infoDescriptionText.alignment = TextAnchor.UpperCenter;

        GameObject infoNextButtonGO = CreateUIButton("InfoNextLevelButton", infoPanelGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(420f, 110f));
        Button infoNextButton = infoNextButtonGO.GetComponent<Button>();
        infoNextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);

        infoPanelGO.SetActive(false);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.FindProperty("moveText").objectReferenceValue = moveText;
        uiSO.FindProperty("scoreText").objectReferenceValue = scoreText;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("infoPanel").objectReferenceValue = infoPanelGO;
        uiSO.FindProperty("infoTitleText").objectReferenceValue = infoTitleText;
        uiSO.FindProperty("infoStarText").objectReferenceValue = infoStarText;
        uiSO.FindProperty("infoScoreText").objectReferenceValue = infoScoreText;
        uiSO.FindProperty("infoPathLengthText").objectReferenceValue = infoPathLengthText;
        uiSO.FindProperty("infoDescriptionText").objectReferenceValue = infoDescriptionText;
        uiSO.FindProperty("infoNextLevelButton").objectReferenceValue = infoNextButton;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 5 scene: same as Phase 4, plus WaterFlowAnimator2D (parented under
    // GameRoot2D, using BoardRoot/Effects as its drop container) and the
    // WaterDrop2D prefab, wired into both WaterFlowAnimator2D and UIManager2D
    // (which now also needs a BoardManager2D reference to compute source/target
    // world positions for the animation). Kept as its own method so Phase 1+2,
    // Phase 3 and Phase 4 commands stay untouched.
    private static void BuildGameScenePhase5(
        GameObject gridPrefab, GameObject straightPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab, GameObject waterDropPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);
        var scoreManagerGO = new GameObject("ScoreManager2D");
        scoreManagerGO.transform.SetParent(gameRoot.transform);
        var waterFlowAnimatorGO = new GameObject("WaterFlowAnimator2D");
        waterFlowAnimatorGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var effectsContainer = new GameObject("Effects");
        effectsContainer.transform.SetParent(boardRoot.transform);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        ScoreManager2D scoreManager = scoreManagerGO.AddComponent<ScoreManager2D>();

        WaterFlowAnimator2D waterFlowAnimator = waterFlowAnimatorGO.AddComponent<WaterFlowAnimator2D>();
        var waterSO = new SerializedObject(waterFlowAnimator);
        waterSO.FindProperty("waterDropPrefab").objectReferenceValue = waterDropPrefab;
        waterSO.FindProperty("effectsContainer").objectReferenceValue = effectsContainer.transform;
        waterSO.ApplyModifiedPropertiesWithoutUndo();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        levelSO.FindProperty("pipeStraightPrefab").objectReferenceValue = straightPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject levelNameGO = CreateUIText("LevelNameText", canvasGO.transform, "",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -50f), new Vector2(900f, 90f));
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 48;
        levelNameText.color = Color.black;

        GameObject moveTextGO = CreateUIText("MoveText", canvasGO.transform, "Hamle: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(-220f, -150f), new Vector2(380f, 70f));
        Text moveText = moveTextGO.GetComponent<Text>();
        moveText.fontSize = 40;
        moveText.color = Color.black;

        GameObject scoreTextGO = CreateUIText("ScoreText", canvasGO.transform, "Puan: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(220f, -150f), new Vector2(380f, 70f));
        Text scoreText = scoreTextGO.GetComponent<Text>();
        scoreText.fontSize = 40;
        scoreText.color = Color.black;

        GameObject resultTextGO = CreateUIText("ResultText", canvasGO.transform, "Hazır",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 100f));
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 56;
        resultText.color = Color.black;

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", canvasGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-360f, 160f), new Vector2(260f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 34);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", canvasGO.transform, "Suyu Başlat",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 160f), new Vector2(420f, 140f));
        Button startButton = startButtonGO.GetComponent<Button>();

        GameObject nextButtonGO = CreateUIButton("NextLevelButton", canvasGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(360f, 160f), new Vector2(260f, 120f));
        Button nextButton = nextButtonGO.GetComponent<Button>();
        nextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(nextButtonGO, 34);

        // InfoPanel: centered card, sized/positioned to avoid the bottom button row
        // and the top texts. Hidden by default; only UIManager2D activates it.
        GameObject infoPanelGO = CreateUIPanel("InfoPanel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(900f, 1300f), new Color(1f, 1f, 1f, 0.96f));

        GameObject infoTitleGO = CreateUIText("InfoTitleText", infoPanelGO.transform, "Tebrikler!",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(800f, 90f));
        Text infoTitleText = infoTitleGO.GetComponent<Text>();
        infoTitleText.fontSize = 54;
        infoTitleText.color = Color.black;

        GameObject infoStarGO = CreateUIText("InfoStarText", infoPanelGO.transform, "Yildiz: * (1/3)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -160f), new Vector2(800f, 70f));
        Text infoStarText = infoStarGO.GetComponent<Text>();
        infoStarText.fontSize = 42;
        infoStarText.color = Color.black;

        GameObject infoScoreGO = CreateUIText("InfoScoreText", infoPanelGO.transform, "Puan: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 70f));
        Text infoScoreText = infoScoreGO.GetComponent<Text>();
        infoScoreText.fontSize = 40;
        infoScoreText.color = Color.black;

        GameObject infoPathLengthGO = CreateUIText("InfoPathLengthText", infoPanelGO.transform, "Yol Uzunluğu: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(800f, 70f));
        Text infoPathLengthText = infoPathLengthGO.GetComponent<Text>();
        infoPathLengthText.fontSize = 40;
        infoPathLengthText.color = Color.black;

        GameObject infoDescriptionGO = CreateUIText("InfoText", infoPanelGO.transform, "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -460f), new Vector2(800f, 320f));
        Text infoDescriptionText = infoDescriptionGO.GetComponent<Text>();
        infoDescriptionText.fontSize = 36;
        infoDescriptionText.color = Color.black;
        infoDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        infoDescriptionText.alignment = TextAnchor.UpperCenter;

        GameObject infoNextButtonGO = CreateUIButton("InfoNextLevelButton", infoPanelGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(420f, 110f));
        Button infoNextButton = infoNextButtonGO.GetComponent<Button>();
        infoNextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);

        infoPanelGO.SetActive(false);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        uiSO.FindProperty("waterFlowAnimator").objectReferenceValue = waterFlowAnimator;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.FindProperty("moveText").objectReferenceValue = moveText;
        uiSO.FindProperty("scoreText").objectReferenceValue = scoreText;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("infoPanel").objectReferenceValue = infoPanelGO;
        uiSO.FindProperty("infoTitleText").objectReferenceValue = infoTitleText;
        uiSO.FindProperty("infoStarText").objectReferenceValue = infoStarText;
        uiSO.FindProperty("infoScoreText").objectReferenceValue = infoScoreText;
        uiSO.FindProperty("infoPathLengthText").objectReferenceValue = infoPathLengthText;
        uiSO.FindProperty("infoDescriptionText").objectReferenceValue = infoDescriptionText;
        uiSO.FindProperty("infoNextLevelButton").objectReferenceValue = infoNextButton;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 5B scene: same as Phase 5, plus a single persistent Cloud2D placeholder
    // under BoardRoot/CloudAndRain, positioned near the top-left grid cell, wired as
    // WaterFlowAnimator2D.cloudDropStartPoint. Kept as its own method so Phase 1+2,
    // Phase 3, Phase 4 and Phase 5 commands stay untouched.
    private static void BuildGameScenePhase5B(
        GameObject gridPrefab, GameObject straightPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab, GameObject waterDropPrefab, GameObject cloudPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);
        var scoreManagerGO = new GameObject("ScoreManager2D");
        scoreManagerGO.transform.SetParent(gameRoot.transform);
        var waterFlowAnimatorGO = new GameObject("WaterFlowAnimator2D");
        waterFlowAnimatorGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var effectsContainer = new GameObject("Effects");
        effectsContainer.transform.SetParent(boardRoot.transform);
        var cloudAndRainContainer = new GameObject("CloudAndRain");
        cloudAndRainContainer.transform.SetParent(boardRoot.transform);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        // Cloud placeholder: fixed near the top-left grid cell (-2, 2) plus a small
        // upward offset. It is a single scene object created once here (not spawned
        // by LevelManager2D), so it naturally persists across reload/next level
        // without any extra cleanup code.
        Vector2Int cloudGridAnchor = new Vector2Int(-2, 2);
        Vector3 cloudOffset = new Vector3(0f, 0.75f, 0f);
        Vector3 cloudWorldPos = boardManager.GridToWorld(cloudGridAnchor) + cloudOffset;
        GameObject cloudInstance = Object.Instantiate(cloudPrefab, cloudWorldPos, Quaternion.identity, cloudAndRainContainer.transform);
        cloudInstance.name = "Cloud2D";

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        ScoreManager2D scoreManager = scoreManagerGO.AddComponent<ScoreManager2D>();

        WaterFlowAnimator2D waterFlowAnimator = waterFlowAnimatorGO.AddComponent<WaterFlowAnimator2D>();
        var waterSO = new SerializedObject(waterFlowAnimator);
        waterSO.FindProperty("waterDropPrefab").objectReferenceValue = waterDropPrefab;
        waterSO.FindProperty("effectsContainer").objectReferenceValue = effectsContainer.transform;
        waterSO.FindProperty("cloudDropStartPoint").objectReferenceValue = cloudInstance.transform;
        waterSO.ApplyModifiedPropertiesWithoutUndo();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        levelSO.FindProperty("pipeStraightPrefab").objectReferenceValue = straightPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject levelNameGO = CreateUIText("LevelNameText", canvasGO.transform, "",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -50f), new Vector2(900f, 90f));
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 48;
        levelNameText.color = Color.black;

        GameObject moveTextGO = CreateUIText("MoveText", canvasGO.transform, "Hamle: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(-220f, -150f), new Vector2(380f, 70f));
        Text moveText = moveTextGO.GetComponent<Text>();
        moveText.fontSize = 40;
        moveText.color = Color.black;

        GameObject scoreTextGO = CreateUIText("ScoreText", canvasGO.transform, "Puan: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(220f, -150f), new Vector2(380f, 70f));
        Text scoreText = scoreTextGO.GetComponent<Text>();
        scoreText.fontSize = 40;
        scoreText.color = Color.black;

        GameObject resultTextGO = CreateUIText("ResultText", canvasGO.transform, "Hazır",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 100f));
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 56;
        resultText.color = Color.black;

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", canvasGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-360f, 160f), new Vector2(260f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 34);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", canvasGO.transform, "Suyu Başlat",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 160f), new Vector2(420f, 140f));
        Button startButton = startButtonGO.GetComponent<Button>();

        GameObject nextButtonGO = CreateUIButton("NextLevelButton", canvasGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(360f, 160f), new Vector2(260f, 120f));
        Button nextButton = nextButtonGO.GetComponent<Button>();
        nextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(nextButtonGO, 34);

        // InfoPanel: centered card, sized/positioned to avoid the bottom button row
        // and the top texts. Hidden by default; only UIManager2D activates it.
        GameObject infoPanelGO = CreateUIPanel("InfoPanel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(900f, 1300f), new Color(1f, 1f, 1f, 0.96f));

        GameObject infoTitleGO = CreateUIText("InfoTitleText", infoPanelGO.transform, "Tebrikler!",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(800f, 90f));
        Text infoTitleText = infoTitleGO.GetComponent<Text>();
        infoTitleText.fontSize = 54;
        infoTitleText.color = Color.black;

        GameObject infoStarGO = CreateUIText("InfoStarText", infoPanelGO.transform, "Yildiz: * (1/3)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -160f), new Vector2(800f, 70f));
        Text infoStarText = infoStarGO.GetComponent<Text>();
        infoStarText.fontSize = 42;
        infoStarText.color = Color.black;

        GameObject infoScoreGO = CreateUIText("InfoScoreText", infoPanelGO.transform, "Puan: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 70f));
        Text infoScoreText = infoScoreGO.GetComponent<Text>();
        infoScoreText.fontSize = 40;
        infoScoreText.color = Color.black;

        GameObject infoPathLengthGO = CreateUIText("InfoPathLengthText", infoPanelGO.transform, "Yol Uzunluğu: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(800f, 70f));
        Text infoPathLengthText = infoPathLengthGO.GetComponent<Text>();
        infoPathLengthText.fontSize = 40;
        infoPathLengthText.color = Color.black;

        GameObject infoDescriptionGO = CreateUIText("InfoText", infoPanelGO.transform, "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -460f), new Vector2(800f, 320f));
        Text infoDescriptionText = infoDescriptionGO.GetComponent<Text>();
        infoDescriptionText.fontSize = 36;
        infoDescriptionText.color = Color.black;
        infoDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        infoDescriptionText.alignment = TextAnchor.UpperCenter;

        GameObject infoNextButtonGO = CreateUIButton("InfoNextLevelButton", infoPanelGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(420f, 110f));
        Button infoNextButton = infoNextButtonGO.GetComponent<Button>();
        infoNextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);

        infoPanelGO.SetActive(false);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        uiSO.FindProperty("waterFlowAnimator").objectReferenceValue = waterFlowAnimator;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.FindProperty("moveText").objectReferenceValue = moveText;
        uiSO.FindProperty("scoreText").objectReferenceValue = scoreText;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("infoPanel").objectReferenceValue = infoPanelGO;
        uiSO.FindProperty("infoTitleText").objectReferenceValue = infoTitleText;
        uiSO.FindProperty("infoStarText").objectReferenceValue = infoStarText;
        uiSO.FindProperty("infoScoreText").objectReferenceValue = infoScoreText;
        uiSO.FindProperty("infoPathLengthText").objectReferenceValue = infoPathLengthText;
        uiSO.FindProperty("infoDescriptionText").objectReferenceValue = infoDescriptionText;
        uiSO.FindProperty("infoNextLevelButton").objectReferenceValue = infoNextButton;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 6 scene: same as Phase 5B, plus a RainLoopFX placeholder under
    // BoardRoot/CloudAndRain (always active, independent of gameplay). The pipe water
    // overlay and target FlowerBloomFX/DuckWalkFX hooks live on the shared
    // PipeStraight2D/PipeCorner2D/Target2D prefabs (see CreatePipePrefab and
    // CreateTargetPrefab), so no extra wiring is needed for those here. Kept as its
    // own method so Phase 1+2, Phase 3, Phase 4, Phase 5 and Phase 5B commands stay
    // untouched.
    private static void BuildGameScenePhase6(
        GameObject gridPrefab, GameObject straightPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab, GameObject waterDropPrefab, GameObject cloudPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);
        var scoreManagerGO = new GameObject("ScoreManager2D");
        scoreManagerGO.transform.SetParent(gameRoot.transform);
        var waterFlowAnimatorGO = new GameObject("WaterFlowAnimator2D");
        waterFlowAnimatorGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var effectsContainer = new GameObject("Effects");
        effectsContainer.transform.SetParent(boardRoot.transform);
        var cloudAndRainContainer = new GameObject("CloudAndRain");
        cloudAndRainContainer.transform.SetParent(boardRoot.transform);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        // Cloud placeholder: fixed near the top-left grid cell (-2, 2) plus a small
        // upward offset. Created once here (not spawned by LevelManager2D), so it
        // naturally persists across reload/next level without any extra cleanup code.
        Vector2Int cloudGridAnchor = new Vector2Int(-2, 2);
        Vector3 cloudOffset = new Vector3(0f, 0.75f, 0f);
        Vector3 cloudWorldPos = boardManager.GridToWorld(cloudGridAnchor) + cloudOffset;
        GameObject cloudInstance = Object.Instantiate(cloudPrefab, cloudWorldPos, Quaternion.identity, cloudAndRainContainer.transform);
        cloudInstance.name = "Cloud2D";

        // Rain loop placeholder: a simple always-active vertical streak just below the
        // cloud. Independent of Suyu Başlat/WaterFlowAnimator2D on purpose - it is not
        // wired to any script, just a hook point ready for a real looping
        // sprite-sheet/Animator later.
        Material unlitMaterialForRain = CreateUnlitMaterial();
        Texture2D rainTexture = CreateBarTexture(new Color32(150, 200, 235, 180), false);
        Sprite rainSprite = CreateAndImportSprite("rain_loop", rainTexture);
        var rainLoopGO = new GameObject("RainLoopFX");
        rainLoopGO.transform.SetParent(cloudAndRainContainer.transform, false);
        rainLoopGO.transform.position = cloudWorldPos + new Vector3(0f, -0.4f, 0f);
        rainLoopGO.transform.localScale = Vector3.one * 0.5f;
        var rainSr = rainLoopGO.AddComponent<SpriteRenderer>();
        rainSr.sprite = rainSprite;
        rainSr.sharedMaterial = unlitMaterialForRain;
        rainSr.sortingOrder = 2;

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        ScoreManager2D scoreManager = scoreManagerGO.AddComponent<ScoreManager2D>();

        WaterFlowAnimator2D waterFlowAnimator = waterFlowAnimatorGO.AddComponent<WaterFlowAnimator2D>();
        var waterSO = new SerializedObject(waterFlowAnimator);
        waterSO.FindProperty("waterDropPrefab").objectReferenceValue = waterDropPrefab;
        waterSO.FindProperty("effectsContainer").objectReferenceValue = effectsContainer.transform;
        waterSO.FindProperty("cloudDropStartPoint").objectReferenceValue = cloudInstance.transform;
        waterSO.ApplyModifiedPropertiesWithoutUndo();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        levelSO.FindProperty("pipeStraightPrefab").objectReferenceValue = straightPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject levelNameGO = CreateUIText("LevelNameText", canvasGO.transform, "",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -50f), new Vector2(900f, 90f));
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 48;
        levelNameText.color = Color.black;

        GameObject moveTextGO = CreateUIText("MoveText", canvasGO.transform, "Hamle: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(-220f, -150f), new Vector2(380f, 70f));
        Text moveText = moveTextGO.GetComponent<Text>();
        moveText.fontSize = 40;
        moveText.color = Color.black;

        GameObject scoreTextGO = CreateUIText("ScoreText", canvasGO.transform, "Puan: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(220f, -150f), new Vector2(380f, 70f));
        Text scoreText = scoreTextGO.GetComponent<Text>();
        scoreText.fontSize = 40;
        scoreText.color = Color.black;

        GameObject resultTextGO = CreateUIText("ResultText", canvasGO.transform, "Hazır",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 100f));
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 56;
        resultText.color = Color.black;

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", canvasGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-360f, 160f), new Vector2(260f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 34);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", canvasGO.transform, "Suyu Başlat",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 160f), new Vector2(420f, 140f));
        Button startButton = startButtonGO.GetComponent<Button>();

        GameObject nextButtonGO = CreateUIButton("NextLevelButton", canvasGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(360f, 160f), new Vector2(260f, 120f));
        Button nextButton = nextButtonGO.GetComponent<Button>();
        nextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(nextButtonGO, 34);

        // InfoPanel: centered card, sized/positioned to avoid the bottom button row
        // and the top texts. Hidden by default; only UIManager2D activates it.
        GameObject infoPanelGO = CreateUIPanel("InfoPanel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(900f, 1300f), new Color(1f, 1f, 1f, 0.96f));

        GameObject infoTitleGO = CreateUIText("InfoTitleText", infoPanelGO.transform, "Tebrikler!",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(800f, 90f));
        Text infoTitleText = infoTitleGO.GetComponent<Text>();
        infoTitleText.fontSize = 54;
        infoTitleText.color = Color.black;

        GameObject infoStarGO = CreateUIText("InfoStarText", infoPanelGO.transform, "Yildiz: * (1/3)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -160f), new Vector2(800f, 70f));
        Text infoStarText = infoStarGO.GetComponent<Text>();
        infoStarText.fontSize = 42;
        infoStarText.color = Color.black;

        GameObject infoScoreGO = CreateUIText("InfoScoreText", infoPanelGO.transform, "Puan: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 70f));
        Text infoScoreText = infoScoreGO.GetComponent<Text>();
        infoScoreText.fontSize = 40;
        infoScoreText.color = Color.black;

        GameObject infoPathLengthGO = CreateUIText("InfoPathLengthText", infoPanelGO.transform, "Yol Uzunluğu: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(800f, 70f));
        Text infoPathLengthText = infoPathLengthGO.GetComponent<Text>();
        infoPathLengthText.fontSize = 40;
        infoPathLengthText.color = Color.black;

        GameObject infoDescriptionGO = CreateUIText("InfoText", infoPanelGO.transform, "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -460f), new Vector2(800f, 320f));
        Text infoDescriptionText = infoDescriptionGO.GetComponent<Text>();
        infoDescriptionText.fontSize = 36;
        infoDescriptionText.color = Color.black;
        infoDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        infoDescriptionText.alignment = TextAnchor.UpperCenter;

        GameObject infoNextButtonGO = CreateUIButton("InfoNextLevelButton", infoPanelGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(420f, 110f));
        Button infoNextButton = infoNextButtonGO.GetComponent<Button>();
        infoNextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);

        infoPanelGO.SetActive(false);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        uiSO.FindProperty("waterFlowAnimator").objectReferenceValue = waterFlowAnimator;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.FindProperty("moveText").objectReferenceValue = moveText;
        uiSO.FindProperty("scoreText").objectReferenceValue = scoreText;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("infoPanel").objectReferenceValue = infoPanelGO;
        uiSO.FindProperty("infoTitleText").objectReferenceValue = infoTitleText;
        uiSO.FindProperty("infoStarText").objectReferenceValue = infoStarText;
        uiSO.FindProperty("infoScoreText").objectReferenceValue = infoScoreText;
        uiSO.FindProperty("infoPathLengthText").objectReferenceValue = infoPathLengthText;
        uiSO.FindProperty("infoDescriptionText").objectReferenceValue = infoDescriptionText;
        uiSO.FindProperty("infoNextLevelButton").objectReferenceValue = infoNextButton;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 7A scene: identical to Phase 6/6B, except RainLoopFX now carries a
    // SpriteFrameAnimator2D (seeded with the placeholder rain sprite as a single
    // frame) and CloudAndRain carries CloudRainVisual2D. After creating those,
    // RainCloudSpriteSheetBinder.TryBindToRainLoopFX attempts to upgrade RainLoopFX
    // to the real imported RainCloudSpriteSheet frames; if that asset isn't found,
    // the placeholder single-frame setup is left in place (with a logged warning)
    // and the scene build still completes normally. Kept as its own method so
    // Phase 1+2, Phase 3, Phase 4, Phase 5, Phase 5B and Phase 6/6B commands stay
    // untouched.
    private static void BuildGameScenePhase7A(
        GameObject gridPrefab, GameObject straightPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab, GameObject waterDropPrefab, GameObject cloudPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);
        var scoreManagerGO = new GameObject("ScoreManager2D");
        scoreManagerGO.transform.SetParent(gameRoot.transform);
        var waterFlowAnimatorGO = new GameObject("WaterFlowAnimator2D");
        waterFlowAnimatorGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var effectsContainer = new GameObject("Effects");
        effectsContainer.transform.SetParent(boardRoot.transform);
        var cloudAndRainContainer = new GameObject("CloudAndRain");
        cloudAndRainContainer.transform.SetParent(boardRoot.transform);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        // Cloud placeholder: fixed near the top-left grid cell (-2, 2) plus a small
        // upward offset. Created once here (not spawned by LevelManager2D), so it
        // naturally persists across reload/next level without any extra cleanup code.
        Vector2Int cloudGridAnchor = new Vector2Int(-2, 2);
        Vector3 cloudOffset = new Vector3(0f, 0.75f, 0f);
        Vector3 cloudWorldPos = boardManager.GridToWorld(cloudGridAnchor) + cloudOffset;
        GameObject cloudInstance = Object.Instantiate(cloudPrefab, cloudWorldPos, Quaternion.identity, cloudAndRainContainer.transform);
        cloudInstance.name = "Cloud2D";

        // Rain loop: starts as the same placeholder vertical streak as Phase 6, now
        // wrapped in a SpriteFrameAnimator2D (single placeholder frame by default).
        Material unlitMaterialForRain = CreateUnlitMaterial();
        Texture2D rainTexture = CreateBarTexture(new Color32(150, 200, 235, 180), false);
        Sprite rainSprite = CreateAndImportSprite("rain_loop", rainTexture);
        var rainLoopGO = new GameObject("RainLoopFX");
        rainLoopGO.transform.SetParent(cloudAndRainContainer.transform, false);
        rainLoopGO.transform.position = cloudWorldPos + new Vector3(0f, -0.4f, 0f);
        rainLoopGO.transform.localScale = Vector3.one * 0.5f;
        var rainSr = rainLoopGO.AddComponent<SpriteRenderer>();
        rainSr.sprite = rainSprite;
        rainSr.sharedMaterial = unlitMaterialForRain;
        rainSr.sortingOrder = 3;

        var rainAnimator = rainLoopGO.AddComponent<SpriteFrameAnimator2D>();
        var rainAnimatorSO = new SerializedObject(rainAnimator);
        rainAnimatorSO.FindProperty("targetRenderer").objectReferenceValue = rainSr;
        SerializedProperty placeholderFramesProp = rainAnimatorSO.FindProperty("frames");
        placeholderFramesProp.arraySize = 1;
        placeholderFramesProp.GetArrayElementAtIndex(0).objectReferenceValue = rainSprite;
        rainAnimatorSO.FindProperty("framesPerSecond").floatValue = 10f;
        rainAnimatorSO.FindProperty("loop").boolValue = true;
        rainAnimatorSO.FindProperty("playOnEnable").boolValue = true;
        rainAnimatorSO.FindProperty("hideWhenStopped").boolValue = false;
        rainAnimatorSO.ApplyModifiedPropertiesWithoutUndo();

        // Attempt to upgrade to the real imported RainCloudSpriteSheet frames. Safe
        // no-op (with a logged warning) if the asset isn't available yet - the
        // placeholder single-frame setup above is left exactly as-is in that case.
        RainCloudSpriteSheetBinder.TryBindToRainLoopFX(rainLoopGO);

        CloudRainVisual2D cloudRainVisual = cloudAndRainContainer.AddComponent<CloudRainVisual2D>();
        var cloudRainVisualSO = new SerializedObject(cloudRainVisual);
        cloudRainVisualSO.FindProperty("cloudObject").objectReferenceValue = cloudInstance;
        cloudRainVisualSO.FindProperty("rainCloudAnimator").objectReferenceValue = rainAnimator;
        cloudRainVisualSO.FindProperty("animationIncludesCloud").boolValue = true;
        cloudRainVisualSO.ApplyModifiedPropertiesWithoutUndo();
        cloudRainVisual.ApplyVisualMode();

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        ScoreManager2D scoreManager = scoreManagerGO.AddComponent<ScoreManager2D>();

        WaterFlowAnimator2D waterFlowAnimator = waterFlowAnimatorGO.AddComponent<WaterFlowAnimator2D>();
        var waterSO = new SerializedObject(waterFlowAnimator);
        waterSO.FindProperty("waterDropPrefab").objectReferenceValue = waterDropPrefab;
        waterSO.FindProperty("effectsContainer").objectReferenceValue = effectsContainer.transform;
        waterSO.FindProperty("cloudDropStartPoint").objectReferenceValue = cloudInstance.transform;
        waterSO.ApplyModifiedPropertiesWithoutUndo();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        levelSO.FindProperty("pipeStraightPrefab").objectReferenceValue = straightPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject levelNameGO = CreateUIText("LevelNameText", canvasGO.transform, "",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -50f), new Vector2(900f, 90f));
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 48;
        levelNameText.color = Color.black;

        GameObject moveTextGO = CreateUIText("MoveText", canvasGO.transform, "Hamle: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(-220f, -150f), new Vector2(380f, 70f));
        Text moveText = moveTextGO.GetComponent<Text>();
        moveText.fontSize = 40;
        moveText.color = Color.black;

        GameObject scoreTextGO = CreateUIText("ScoreText", canvasGO.transform, "Puan: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(220f, -150f), new Vector2(380f, 70f));
        Text scoreText = scoreTextGO.GetComponent<Text>();
        scoreText.fontSize = 40;
        scoreText.color = Color.black;

        GameObject resultTextGO = CreateUIText("ResultText", canvasGO.transform, "Hazır",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 100f));
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 56;
        resultText.color = Color.black;

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", canvasGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-360f, 160f), new Vector2(260f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 34);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", canvasGO.transform, "Suyu Başlat",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 160f), new Vector2(420f, 140f));
        Button startButton = startButtonGO.GetComponent<Button>();

        GameObject nextButtonGO = CreateUIButton("NextLevelButton", canvasGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(360f, 160f), new Vector2(260f, 120f));
        Button nextButton = nextButtonGO.GetComponent<Button>();
        nextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(nextButtonGO, 34);

        // InfoPanel: centered card, sized/positioned to avoid the bottom button row
        // and the top texts. Hidden by default; only UIManager2D activates it.
        GameObject infoPanelGO = CreateUIPanel("InfoPanel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(900f, 1300f), new Color(1f, 1f, 1f, 0.96f));

        GameObject infoTitleGO = CreateUIText("InfoTitleText", infoPanelGO.transform, "Tebrikler!",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(800f, 90f));
        Text infoTitleText = infoTitleGO.GetComponent<Text>();
        infoTitleText.fontSize = 54;
        infoTitleText.color = Color.black;

        GameObject infoStarGO = CreateUIText("InfoStarText", infoPanelGO.transform, "Yildiz: * (1/3)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -160f), new Vector2(800f, 70f));
        Text infoStarText = infoStarGO.GetComponent<Text>();
        infoStarText.fontSize = 42;
        infoStarText.color = Color.black;

        GameObject infoScoreGO = CreateUIText("InfoScoreText", infoPanelGO.transform, "Puan: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 70f));
        Text infoScoreText = infoScoreGO.GetComponent<Text>();
        infoScoreText.fontSize = 40;
        infoScoreText.color = Color.black;

        GameObject infoPathLengthGO = CreateUIText("InfoPathLengthText", infoPanelGO.transform, "Yol Uzunluğu: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(800f, 70f));
        Text infoPathLengthText = infoPathLengthGO.GetComponent<Text>();
        infoPathLengthText.fontSize = 40;
        infoPathLengthText.color = Color.black;

        GameObject infoDescriptionGO = CreateUIText("InfoText", infoPanelGO.transform, "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -460f), new Vector2(800f, 320f));
        Text infoDescriptionText = infoDescriptionGO.GetComponent<Text>();
        infoDescriptionText.fontSize = 36;
        infoDescriptionText.color = Color.black;
        infoDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        infoDescriptionText.alignment = TextAnchor.UpperCenter;

        GameObject infoNextButtonGO = CreateUIButton("InfoNextLevelButton", infoPanelGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(420f, 110f));
        Button infoNextButton = infoNextButtonGO.GetComponent<Button>();
        infoNextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);

        infoPanelGO.SetActive(false);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        uiSO.FindProperty("waterFlowAnimator").objectReferenceValue = waterFlowAnimator;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.FindProperty("moveText").objectReferenceValue = moveText;
        uiSO.FindProperty("scoreText").objectReferenceValue = scoreText;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("infoPanel").objectReferenceValue = infoPanelGO;
        uiSO.FindProperty("infoTitleText").objectReferenceValue = infoTitleText;
        uiSO.FindProperty("infoStarText").objectReferenceValue = infoStarText;
        uiSO.FindProperty("infoScoreText").objectReferenceValue = infoScoreText;
        uiSO.FindProperty("infoPathLengthText").objectReferenceValue = infoPathLengthText;
        uiSO.FindProperty("infoDescriptionText").objectReferenceValue = infoDescriptionText;
        uiSO.FindProperty("infoNextLevelButton").objectReferenceValue = infoNextButton;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 7B scene: identical to Phase 7A, except LevelManager2D now wires two
    // straight prefabs (Wide/Narrow) instead of one, and PipeWaterSpriteSheetBinder
    // is invoked once after all pipe prefabs exist on disk to attempt binding the
    // real pipes_tileset.png fill-animation frames onto them (falling back to the
    // placeholder blue overlay per prefab if that asset isn't available). Kept as
    // its own method so Phase 1+2 through Phase 7A commands stay untouched.
    private static void BuildGameScenePhase7B(
        GameObject gridPrefab, GameObject straightWidePrefab, GameObject straightNarrowPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab, GameObject waterDropPrefab, GameObject cloudPrefab)
    {
        // Prefabs were already saved to disk by BuildPlaceholderAssets() above;
        // binding operates purely on those prefab assets (not the scene), which is
        // also why the standalone "Bind Pipe Fill Sprite Sheet" command never needs
        // GameScene2D to be open.
        PipeWaterSpriteSheetBinder.TryBindAll(true);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);
        var scoreManagerGO = new GameObject("ScoreManager2D");
        scoreManagerGO.transform.SetParent(gameRoot.transform);
        var waterFlowAnimatorGO = new GameObject("WaterFlowAnimator2D");
        waterFlowAnimatorGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var effectsContainer = new GameObject("Effects");
        effectsContainer.transform.SetParent(boardRoot.transform);
        var cloudAndRainContainer = new GameObject("CloudAndRain");
        cloudAndRainContainer.transform.SetParent(boardRoot.transform);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        // Cloud placeholder: fixed near the top-left grid cell (-2, 2) plus a small
        // upward offset. Created once here (not spawned by LevelManager2D), so it
        // naturally persists across reload/next level without any extra cleanup code.
        Vector2Int cloudGridAnchor = new Vector2Int(-2, 2);
        Vector3 cloudOffset = new Vector3(0f, 0.75f, 0f);
        Vector3 cloudWorldPos = boardManager.GridToWorld(cloudGridAnchor) + cloudOffset;
        GameObject cloudInstance = Object.Instantiate(cloudPrefab, cloudWorldPos, Quaternion.identity, cloudAndRainContainer.transform);
        cloudInstance.name = "Cloud2D";

        // Rain loop: starts as the same placeholder vertical streak as Phase 6, now
        // wrapped in a SpriteFrameAnimator2D (single placeholder frame by default).
        Material unlitMaterialForRain = CreateUnlitMaterial();
        Texture2D rainTexture = CreateBarTexture(new Color32(150, 200, 235, 180), false);
        Sprite rainSprite = CreateAndImportSprite("rain_loop", rainTexture);
        var rainLoopGO = new GameObject("RainLoopFX");
        rainLoopGO.transform.SetParent(cloudAndRainContainer.transform, false);
        rainLoopGO.transform.position = cloudWorldPos + new Vector3(0f, -0.4f, 0f);
        rainLoopGO.transform.localScale = Vector3.one * 0.5f;
        var rainSr = rainLoopGO.AddComponent<SpriteRenderer>();
        rainSr.sprite = rainSprite;
        rainSr.sharedMaterial = unlitMaterialForRain;
        rainSr.sortingOrder = 3;

        var rainAnimator = rainLoopGO.AddComponent<SpriteFrameAnimator2D>();
        var rainAnimatorSO = new SerializedObject(rainAnimator);
        rainAnimatorSO.FindProperty("targetRenderer").objectReferenceValue = rainSr;
        SerializedProperty placeholderFramesProp = rainAnimatorSO.FindProperty("frames");
        placeholderFramesProp.arraySize = 1;
        placeholderFramesProp.GetArrayElementAtIndex(0).objectReferenceValue = rainSprite;
        rainAnimatorSO.FindProperty("framesPerSecond").floatValue = 10f;
        rainAnimatorSO.FindProperty("loop").boolValue = true;
        rainAnimatorSO.FindProperty("playOnEnable").boolValue = true;
        rainAnimatorSO.FindProperty("hideWhenStopped").boolValue = false;
        rainAnimatorSO.ApplyModifiedPropertiesWithoutUndo();

        // Attempt to upgrade to the real imported RainCloudSpriteSheet frames. Safe
        // no-op (with a logged warning) if the asset isn't available yet - the
        // placeholder single-frame setup above is left exactly as-is in that case.
        RainCloudSpriteSheetBinder.TryBindToRainLoopFX(rainLoopGO);

        CloudRainVisual2D cloudRainVisual = cloudAndRainContainer.AddComponent<CloudRainVisual2D>();
        var cloudRainVisualSO = new SerializedObject(cloudRainVisual);
        cloudRainVisualSO.FindProperty("cloudObject").objectReferenceValue = cloudInstance;
        cloudRainVisualSO.FindProperty("rainCloudAnimator").objectReferenceValue = rainAnimator;
        cloudRainVisualSO.FindProperty("animationIncludesCloud").boolValue = true;
        cloudRainVisualSO.ApplyModifiedPropertiesWithoutUndo();
        cloudRainVisual.ApplyVisualMode();

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        ScoreManager2D scoreManager = scoreManagerGO.AddComponent<ScoreManager2D>();

        WaterFlowAnimator2D waterFlowAnimator = waterFlowAnimatorGO.AddComponent<WaterFlowAnimator2D>();
        var waterSO = new SerializedObject(waterFlowAnimator);
        waterSO.FindProperty("waterDropPrefab").objectReferenceValue = waterDropPrefab;
        waterSO.FindProperty("effectsContainer").objectReferenceValue = effectsContainer.transform;
        waterSO.FindProperty("cloudDropStartPoint").objectReferenceValue = cloudInstance.transform;
        waterSO.ApplyModifiedPropertiesWithoutUndo();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        levelSO.FindProperty("pipeStraightWidePrefab").objectReferenceValue = straightWidePrefab;
        levelSO.FindProperty("pipeStraightNarrowPrefab").objectReferenceValue = straightNarrowPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject levelNameGO = CreateUIText("LevelNameText", canvasGO.transform, "",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -50f), new Vector2(900f, 90f));
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 48;
        levelNameText.color = Color.black;

        GameObject moveTextGO = CreateUIText("MoveText", canvasGO.transform, "Hamle: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(-220f, -150f), new Vector2(380f, 70f));
        Text moveText = moveTextGO.GetComponent<Text>();
        moveText.fontSize = 40;
        moveText.color = Color.black;

        GameObject scoreTextGO = CreateUIText("ScoreText", canvasGO.transform, "Puan: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(220f, -150f), new Vector2(380f, 70f));
        Text scoreText = scoreTextGO.GetComponent<Text>();
        scoreText.fontSize = 40;
        scoreText.color = Color.black;

        GameObject resultTextGO = CreateUIText("ResultText", canvasGO.transform, "Hazır",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 100f));
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 56;
        resultText.color = Color.black;

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", canvasGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-360f, 160f), new Vector2(260f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 34);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", canvasGO.transform, "Suyu Başlat",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 160f), new Vector2(420f, 140f));
        Button startButton = startButtonGO.GetComponent<Button>();

        GameObject nextButtonGO = CreateUIButton("NextLevelButton", canvasGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(360f, 160f), new Vector2(260f, 120f));
        Button nextButton = nextButtonGO.GetComponent<Button>();
        nextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(nextButtonGO, 34);

        // InfoPanel: centered card, sized/positioned to avoid the bottom button row
        // and the top texts. Hidden by default; only UIManager2D activates it.
        GameObject infoPanelGO = CreateUIPanel("InfoPanel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(900f, 1300f), new Color(1f, 1f, 1f, 0.96f));

        GameObject infoTitleGO = CreateUIText("InfoTitleText", infoPanelGO.transform, "Tebrikler!",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(800f, 90f));
        Text infoTitleText = infoTitleGO.GetComponent<Text>();
        infoTitleText.fontSize = 54;
        infoTitleText.color = Color.black;

        GameObject infoStarGO = CreateUIText("InfoStarText", infoPanelGO.transform, "Yildiz: * (1/3)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -160f), new Vector2(800f, 70f));
        Text infoStarText = infoStarGO.GetComponent<Text>();
        infoStarText.fontSize = 42;
        infoStarText.color = Color.black;

        GameObject infoScoreGO = CreateUIText("InfoScoreText", infoPanelGO.transform, "Puan: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 70f));
        Text infoScoreText = infoScoreGO.GetComponent<Text>();
        infoScoreText.fontSize = 40;
        infoScoreText.color = Color.black;

        GameObject infoPathLengthGO = CreateUIText("InfoPathLengthText", infoPanelGO.transform, "Yol Uzunluğu: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(800f, 70f));
        Text infoPathLengthText = infoPathLengthGO.GetComponent<Text>();
        infoPathLengthText.fontSize = 40;
        infoPathLengthText.color = Color.black;

        GameObject infoDescriptionGO = CreateUIText("InfoText", infoPanelGO.transform, "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -460f), new Vector2(800f, 320f));
        Text infoDescriptionText = infoDescriptionGO.GetComponent<Text>();
        infoDescriptionText.fontSize = 36;
        infoDescriptionText.color = Color.black;
        infoDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        infoDescriptionText.alignment = TextAnchor.UpperCenter;

        GameObject infoNextButtonGO = CreateUIButton("InfoNextLevelButton", infoPanelGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(420f, 110f));
        Button infoNextButton = infoNextButtonGO.GetComponent<Button>();
        infoNextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);

        infoPanelGO.SetActive(false);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        uiSO.FindProperty("waterFlowAnimator").objectReferenceValue = waterFlowAnimator;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.FindProperty("moveText").objectReferenceValue = moveText;
        uiSO.FindProperty("scoreText").objectReferenceValue = scoreText;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("infoPanel").objectReferenceValue = infoPanelGO;
        uiSO.FindProperty("infoTitleText").objectReferenceValue = infoTitleText;
        uiSO.FindProperty("infoStarText").objectReferenceValue = infoStarText;
        uiSO.FindProperty("infoScoreText").objectReferenceValue = infoScoreText;
        uiSO.FindProperty("infoPathLengthText").objectReferenceValue = infoPathLengthText;
        uiSO.FindProperty("infoDescriptionText").objectReferenceValue = infoDescriptionText;
        uiSO.FindProperty("infoNextLevelButton").objectReferenceValue = infoNextButton;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 7B.1 scene: identical to Phase 7B, except WaterFlowAnimator2D is added
    // with no wiring at all (it no longer has any drop-related serialized fields),
    // WaterDrop2D is never instantiated/referenced, and UIManager2D is wired without
    // a BoardManager2D reference (no longer needed now that PlaySuccess doesn't take
    // world positions). Kept as its own method so Phase 1+2 through Phase 7B Fix
    // Pipe Alignment commands stay untouched.
    private static void BuildGameScenePhase7B1(
        GameObject gridPrefab, GameObject straightWidePrefab, GameObject straightNarrowPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab, GameObject cloudPrefab)
    {
        // Prefabs were already saved to disk by BuildPlaceholderAssets() above;
        // binding operates purely on those prefab assets (not the scene), which is
        // also why the standalone "Bind Pipe Fill Sprite Sheet" command never needs
        // GameScene2D to be open.
        PipeWaterSpriteSheetBinder.TryBindAll(true);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);
        var scoreManagerGO = new GameObject("ScoreManager2D");
        scoreManagerGO.transform.SetParent(gameRoot.transform);
        var waterFlowAnimatorGO = new GameObject("WaterFlowAnimator2D");
        waterFlowAnimatorGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var cloudAndRainContainer = new GameObject("CloudAndRain");
        cloudAndRainContainer.transform.SetParent(boardRoot.transform);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        // Cloud placeholder: fixed near the top-left grid cell (-2, 2) plus a small
        // upward offset. Created once here (not spawned by LevelManager2D), so it
        // naturally persists across reload/next level without any extra cleanup code.
        Vector2Int cloudGridAnchor = new Vector2Int(-2, 2);
        Vector3 cloudOffset = new Vector3(0f, 0.75f, 0f);
        Vector3 cloudWorldPos = boardManager.GridToWorld(cloudGridAnchor) + cloudOffset;
        GameObject cloudInstance = Object.Instantiate(cloudPrefab, cloudWorldPos, Quaternion.identity, cloudAndRainContainer.transform);
        cloudInstance.name = "Cloud2D";

        // Rain loop: starts as the same placeholder vertical streak as Phase 6, now
        // wrapped in a SpriteFrameAnimator2D (single placeholder frame by default).
        Material unlitMaterialForRain = CreateUnlitMaterial();
        Texture2D rainTexture = CreateBarTexture(new Color32(150, 200, 235, 180), false);
        Sprite rainSprite = CreateAndImportSprite("rain_loop", rainTexture);
        var rainLoopGO = new GameObject("RainLoopFX");
        rainLoopGO.transform.SetParent(cloudAndRainContainer.transform, false);
        rainLoopGO.transform.position = cloudWorldPos + new Vector3(0f, -0.4f, 0f);
        rainLoopGO.transform.localScale = Vector3.one * 0.5f;
        var rainSr = rainLoopGO.AddComponent<SpriteRenderer>();
        rainSr.sprite = rainSprite;
        rainSr.sharedMaterial = unlitMaterialForRain;
        rainSr.sortingOrder = 3;

        var rainAnimator = rainLoopGO.AddComponent<SpriteFrameAnimator2D>();
        var rainAnimatorSO = new SerializedObject(rainAnimator);
        rainAnimatorSO.FindProperty("targetRenderer").objectReferenceValue = rainSr;
        SerializedProperty placeholderFramesProp = rainAnimatorSO.FindProperty("frames");
        placeholderFramesProp.arraySize = 1;
        placeholderFramesProp.GetArrayElementAtIndex(0).objectReferenceValue = rainSprite;
        rainAnimatorSO.FindProperty("framesPerSecond").floatValue = 10f;
        rainAnimatorSO.FindProperty("loop").boolValue = true;
        rainAnimatorSO.FindProperty("playOnEnable").boolValue = true;
        rainAnimatorSO.FindProperty("hideWhenStopped").boolValue = false;
        rainAnimatorSO.ApplyModifiedPropertiesWithoutUndo();

        // Attempt to upgrade to the real imported RainCloudSpriteSheet frames. Safe
        // no-op (with a logged warning) if the asset isn't available yet - the
        // placeholder single-frame setup above is left exactly as-is in that case.
        RainCloudSpriteSheetBinder.TryBindToRainLoopFX(rainLoopGO);

        CloudRainVisual2D cloudRainVisual = cloudAndRainContainer.AddComponent<CloudRainVisual2D>();
        var cloudRainVisualSO = new SerializedObject(cloudRainVisual);
        cloudRainVisualSO.FindProperty("cloudObject").objectReferenceValue = cloudInstance;
        cloudRainVisualSO.FindProperty("rainCloudAnimator").objectReferenceValue = rainAnimator;
        cloudRainVisualSO.FindProperty("animationIncludesCloud").boolValue = true;
        cloudRainVisualSO.ApplyModifiedPropertiesWithoutUndo();
        cloudRainVisual.ApplyVisualMode();

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        ScoreManager2D scoreManager = scoreManagerGO.AddComponent<ScoreManager2D>();

        // No wiring needed: WaterFlowAnimator2D no longer has any drop-related
        // serialized fields. pipeAnimationTimeout/targetFXDuration keep their
        // public-field defaults (1.5s / 0.6s).
        WaterFlowAnimator2D waterFlowAnimator = waterFlowAnimatorGO.AddComponent<WaterFlowAnimator2D>();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        levelSO.FindProperty("pipeStraightWidePrefab").objectReferenceValue = straightWidePrefab;
        levelSO.FindProperty("pipeStraightNarrowPrefab").objectReferenceValue = straightNarrowPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject levelNameGO = CreateUIText("LevelNameText", canvasGO.transform, "",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -50f), new Vector2(900f, 90f));
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 48;
        levelNameText.color = Color.black;

        GameObject moveTextGO = CreateUIText("MoveText", canvasGO.transform, "Hamle: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(-220f, -150f), new Vector2(380f, 70f));
        Text moveText = moveTextGO.GetComponent<Text>();
        moveText.fontSize = 40;
        moveText.color = Color.black;

        GameObject scoreTextGO = CreateUIText("ScoreText", canvasGO.transform, "Puan: 0",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(220f, -150f), new Vector2(380f, 70f));
        Text scoreText = scoreTextGO.GetComponent<Text>();
        scoreText.fontSize = 40;
        scoreText.color = Color.black;

        GameObject resultTextGO = CreateUIText("ResultText", canvasGO.transform, "Hazır",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 100f));
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 56;
        resultText.color = Color.black;

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", canvasGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-360f, 160f), new Vector2(260f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 34);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", canvasGO.transform, "Suyu Başlat",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 160f), new Vector2(420f, 140f));
        Button startButton = startButtonGO.GetComponent<Button>();

        GameObject nextButtonGO = CreateUIButton("NextLevelButton", canvasGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(360f, 160f), new Vector2(260f, 120f));
        Button nextButton = nextButtonGO.GetComponent<Button>();
        nextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(nextButtonGO, 34);

        // InfoPanel: centered card, sized/positioned to avoid the bottom button row
        // and the top texts. Hidden by default; only UIManager2D activates it.
        GameObject infoPanelGO = CreateUIPanel("InfoPanel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(900f, 1300f), new Color(1f, 1f, 1f, 0.96f));

        GameObject infoTitleGO = CreateUIText("InfoTitleText", infoPanelGO.transform, "Tebrikler!",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(800f, 90f));
        Text infoTitleText = infoTitleGO.GetComponent<Text>();
        infoTitleText.fontSize = 54;
        infoTitleText.color = Color.black;

        GameObject infoStarGO = CreateUIText("InfoStarText", infoPanelGO.transform, "Yildiz: * (1/3)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -160f), new Vector2(800f, 70f));
        Text infoStarText = infoStarGO.GetComponent<Text>();
        infoStarText.fontSize = 42;
        infoStarText.color = Color.black;

        GameObject infoScoreGO = CreateUIText("InfoScoreText", infoPanelGO.transform, "Puan: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -240f), new Vector2(800f, 70f));
        Text infoScoreText = infoScoreGO.GetComponent<Text>();
        infoScoreText.fontSize = 40;
        infoScoreText.color = Color.black;

        GameObject infoPathLengthGO = CreateUIText("InfoPathLengthText", infoPanelGO.transform, "Yol Uzunluğu: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(800f, 70f));
        Text infoPathLengthText = infoPathLengthGO.GetComponent<Text>();
        infoPathLengthText.fontSize = 40;
        infoPathLengthText.color = Color.black;

        GameObject infoDescriptionGO = CreateUIText("InfoText", infoPanelGO.transform, "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -460f), new Vector2(800f, 320f));
        Text infoDescriptionText = infoDescriptionGO.GetComponent<Text>();
        infoDescriptionText.fontSize = 36;
        infoDescriptionText.color = Color.black;
        infoDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        infoDescriptionText.alignment = TextAnchor.UpperCenter;

        GameObject infoNextButtonGO = CreateUIButton("InfoNextLevelButton", infoPanelGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(420f, 110f));
        Button infoNextButton = infoNextButtonGO.GetComponent<Button>();
        infoNextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);

        infoPanelGO.SetActive(false);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        uiSO.FindProperty("waterFlowAnimator").objectReferenceValue = waterFlowAnimator;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.FindProperty("moveText").objectReferenceValue = moveText;
        uiSO.FindProperty("scoreText").objectReferenceValue = scoreText;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("infoPanel").objectReferenceValue = infoPanelGO;
        uiSO.FindProperty("infoTitleText").objectReferenceValue = infoTitleText;
        uiSO.FindProperty("infoStarText").objectReferenceValue = infoStarText;
        uiSO.FindProperty("infoScoreText").objectReferenceValue = infoScoreText;
        uiSO.FindProperty("infoPathLengthText").objectReferenceValue = infoPathLengthText;
        uiSO.FindProperty("infoDescriptionText").objectReferenceValue = infoDescriptionText;
        uiSO.FindProperty("infoNextLevelButton").objectReferenceValue = infoNextButton;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 7C scene: same gameplay setup as Phase 7B.1 (board, level data, rain
    // cloud, sequential pipe fill, score/star internals, target FX) but with a fully
    // rebuilt Canvas: SafeAreaRoot + SafeAreaFitter2D, a compact TopHUD (level + move
    // only, no score), a centered ResultText status panel, two-button BottomControls
    // (no persistent Next Level button), and a simplified InfoPanel (no score/path
    // length). Camera framing (orthographicSize 6, position (0,0,-10)) is left
    // unchanged: verified by hand that a smaller size would clip the 5-unit-wide grid
    // horizontally at narrow-aspect tall phones (e.g. 1080x2400, aspect 0.45 needs
    // size >= 6 for the grid alone), while the new reserved top/bottom UI strips
    // still leave several units of vertical margin around the board+cloud at size 6.
    // Kept as its own method so every earlier command stays untouched.
    private static void BuildGameScenePhase7C(
        GameObject gridPrefab, GameObject straightWidePrefab, GameObject straightNarrowPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab, GameObject cloudPrefab)
    {
        PipeWaterSpriteSheetBinder.TryBindAll(true);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);
        var scoreManagerGO = new GameObject("ScoreManager2D");
        scoreManagerGO.transform.SetParent(gameRoot.transform);
        var waterFlowAnimatorGO = new GameObject("WaterFlowAnimator2D");
        waterFlowAnimatorGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var cloudAndRainContainer = new GameObject("CloudAndRain");
        cloudAndRainContainer.transform.SetParent(boardRoot.transform);

        // Optional world-space staging area for future success FX (flower/duck).
        // Empty and inactive during this phase.
        var successFXZone = new GameObject("SuccessFXZone");
        successFXZone.transform.SetParent(boardRoot.transform);
        successFXZone.transform.position = new Vector3(2f, -3f, 0f);
        successFXZone.SetActive(false);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        // Cloud placeholder: fixed near the top-left grid cell (-2, 2) plus a small
        // upward offset. Created once here (not spawned by LevelManager2D), so it
        // naturally persists across reload/next level without any extra cleanup code.
        Vector2Int cloudGridAnchor = new Vector2Int(-2, 2);
        Vector3 cloudOffset = new Vector3(0f, 0.75f, 0f);
        Vector3 cloudWorldPos = boardManager.GridToWorld(cloudGridAnchor) + cloudOffset;
        GameObject cloudInstance = Object.Instantiate(cloudPrefab, cloudWorldPos, Quaternion.identity, cloudAndRainContainer.transform);
        cloudInstance.name = "Cloud2D";

        // Rain loop: starts as the same placeholder vertical streak as Phase 6, now
        // wrapped in a SpriteFrameAnimator2D (single placeholder frame by default).
        Material unlitMaterialForRain = CreateUnlitMaterial();
        Texture2D rainTexture = CreateBarTexture(new Color32(150, 200, 235, 180), false);
        Sprite rainSprite = CreateAndImportSprite("rain_loop", rainTexture);
        var rainLoopGO = new GameObject("RainLoopFX");
        rainLoopGO.transform.SetParent(cloudAndRainContainer.transform, false);
        rainLoopGO.transform.position = cloudWorldPos + new Vector3(0f, -0.4f, 0f);
        rainLoopGO.transform.localScale = Vector3.one * 0.5f;
        var rainSr = rainLoopGO.AddComponent<SpriteRenderer>();
        rainSr.sprite = rainSprite;
        rainSr.sharedMaterial = unlitMaterialForRain;
        rainSr.sortingOrder = 3;

        var rainAnimator = rainLoopGO.AddComponent<SpriteFrameAnimator2D>();
        var rainAnimatorSO = new SerializedObject(rainAnimator);
        rainAnimatorSO.FindProperty("targetRenderer").objectReferenceValue = rainSr;
        SerializedProperty placeholderFramesProp = rainAnimatorSO.FindProperty("frames");
        placeholderFramesProp.arraySize = 1;
        placeholderFramesProp.GetArrayElementAtIndex(0).objectReferenceValue = rainSprite;
        rainAnimatorSO.FindProperty("framesPerSecond").floatValue = 10f;
        rainAnimatorSO.FindProperty("loop").boolValue = true;
        rainAnimatorSO.FindProperty("playOnEnable").boolValue = true;
        rainAnimatorSO.FindProperty("hideWhenStopped").boolValue = false;
        rainAnimatorSO.ApplyModifiedPropertiesWithoutUndo();

        // Attempt to upgrade to the real imported RainCloudSpriteSheet frames. Safe
        // no-op (with a logged warning) if the asset isn't available yet - the
        // placeholder single-frame setup above is left exactly as-is in that case.
        RainCloudSpriteSheetBinder.TryBindToRainLoopFX(rainLoopGO);

        CloudRainVisual2D cloudRainVisual = cloudAndRainContainer.AddComponent<CloudRainVisual2D>();
        var cloudRainVisualSO = new SerializedObject(cloudRainVisual);
        cloudRainVisualSO.FindProperty("cloudObject").objectReferenceValue = cloudInstance;
        cloudRainVisualSO.FindProperty("rainCloudAnimator").objectReferenceValue = rainAnimator;
        cloudRainVisualSO.FindProperty("animationIncludesCloud").boolValue = true;
        cloudRainVisualSO.ApplyModifiedPropertiesWithoutUndo();
        cloudRainVisual.ApplyVisualMode();

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        ScoreManager2D scoreManager = scoreManagerGO.AddComponent<ScoreManager2D>();

        // No wiring needed: WaterFlowAnimator2D no longer has any drop-related
        // serialized fields. pipeAnimationTimeout/targetFXDuration keep their
        // public-field defaults (1.5s / 0.6s).
        WaterFlowAnimator2D waterFlowAnimator = waterFlowAnimatorGO.AddComponent<WaterFlowAnimator2D>();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        levelSO.FindProperty("pipeStraightWidePrefab").objectReferenceValue = straightWidePrefab;
        levelSO.FindProperty("pipeStraightNarrowPrefab").objectReferenceValue = straightNarrowPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // ---------------- Canvas ----------------
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // SafeAreaRoot: stretches to fill the canvas by default; SafeAreaFitter2D
        // pulls its anchors in to Screen.safeArea at runtime (Editor and device).
        GameObject safeAreaRootGO = CreateUIContainer("SafeAreaRoot", canvasGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        safeAreaRootGO.AddComponent<SafeAreaFitter2D>();

        // ---------------- TopHUD (Level left, Move right - no score) ----------------
        GameObject topHudGO = CreateUIContainer("TopHUD", safeAreaRootGO.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(0f, 130f));

        GameObject levelBadgeGO = CreateUIPanel("LevelBadge", topHudGO.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(56f, 0f), new Vector2(320f, 110f), new Color(1f, 1f, 1f, 0.9f));
        GameObject levelNameGO = CreateUIText("LevelNameText", levelBadgeGO.transform, "Bölüm 1",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 42;
        levelNameText.color = Color.black;

        GameObject moveBadgeGO = CreateUIPanel("MoveBadge", topHudGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-56f, 0f), new Vector2(260f, 110f), new Color(1f, 1f, 1f, 0.9f));
        GameObject moveTextGO = CreateUIText("MoveText", moveBadgeGO.transform, "Hamle: 0",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Text moveText = moveTextGO.GetComponent<Text>();
        moveText.fontSize = 42;
        moveText.color = Color.black;

        // ---------------- StatusArea (ResultText, between grid and buttons) ----------------
        GameObject statusAreaGO = CreateUIContainer("StatusArea", safeAreaRootGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        GameObject resultPanelGO = CreateUIPanel("ResultPanel", statusAreaGO.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 180f), new Vector2(-120f, 80f), new Color(1f, 1f, 1f, 0.85f));
        GameObject resultTextGO = CreateUIText("ResultText", resultPanelGO.transform, "Hazır",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 36;
        resultText.color = Color.black;

        // ---------------- BottomControls (Reload + StartWater only) ----------------
        GameObject bottomControlsGO = CreateUIContainer("BottomControls", safeAreaRootGO.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0f, 160f));

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", bottomControlsGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-307.5f, 40f), new Vector2(310f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 40);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", bottomControlsGO.transform, "Su Toplamaya Başla",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(172.5f, 40f), new Vector2(580f, 120f));
        Button startButton = startButtonGO.GetComponent<Button>();
        SetButtonLabelFontSize(startButtonGO, 40);
        EnableButtonLabelBestFit(startButtonGO, 28, 44);

        // ---------------- InfoPanel (success modal, last sibling = renders on top) ----------------
        GameObject infoPanelGO = CreateUIContainer("InfoPanel", safeAreaRootGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        GameObject modalBlockerGO = CreateUIPanel("ModalBlocker", infoPanelGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.55f));

        GameObject infoCardGO = CreateUIPanel("InfoCard", infoPanelGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(900f, 1000f), new Color(1f, 1f, 1f, 0.98f));

        GameObject infoTitleGO = CreateUIText("InfoTitleText", infoCardGO.transform, "Tebrikler!",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -70f), new Vector2(800f, 90f));
        Text infoTitleText = infoTitleGO.GetComponent<Text>();
        infoTitleText.fontSize = 56;
        infoTitleText.color = Color.black;

        GameObject infoStarGO = CreateUIText("InfoStarText", infoCardGO.transform, "Yildiz: * (1/3)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -180f), new Vector2(800f, 80f));
        Text infoStarText = infoStarGO.GetComponent<Text>();
        infoStarText.fontSize = 48;
        infoStarText.color = Color.black;

        GameObject infoDescriptionGO = CreateUIText("InfoText", infoCardGO.transform, "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(800f, 450f));
        Text infoDescriptionText = infoDescriptionGO.GetComponent<Text>();
        infoDescriptionText.fontSize = 34;
        infoDescriptionText.color = Color.black;
        infoDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        infoDescriptionText.alignment = TextAnchor.UpperCenter;

        GameObject infoNextButtonGO = CreateUIButton("InfoNextLevelButton", infoCardGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 90f), new Vector2(420f, 110f));
        Button infoNextButton = infoNextButtonGO.GetComponent<Button>();
        infoNextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(infoNextButtonGO, 42);

        infoPanelGO.SetActive(false);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        uiSO.FindProperty("waterFlowAnimator").objectReferenceValue = waterFlowAnimator;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.FindProperty("moveText").objectReferenceValue = moveText;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("infoPanel").objectReferenceValue = infoPanelGO;
        uiSO.FindProperty("infoTitleText").objectReferenceValue = infoTitleText;
        uiSO.FindProperty("infoStarText").objectReferenceValue = infoStarText;
        uiSO.FindProperty("infoDescriptionText").objectReferenceValue = infoDescriptionText;
        uiSO.FindProperty("infoNextLevelButton").objectReferenceValue = infoNextButton;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 7C.1 scene: same as Phase 7C, with two targeted additions:
    //   1) SuccessFXZone is now active and holds a visible GrassStrip placeholder
    //      (flat green rectangle) directly below the grid - the future world-space
    //      staging area for flower/duck success animations.
    //   2) Camera Y shifted from 0 to -0.9 to recenter the taller combined content
    //      (cloud + grid + grass) within the same unchanged orthographicSize (6).
    // Verified by hand: cloud/grid/grass now span world Y ~3.15 to ~-4.2 (was ~3.15
    // to ~-2.5 before the grass strip); at size 6 the visible height is still 12
    // units, and shifting the camera down by 0.9 keeps ~1.07 units of margin on both
    // the cloud side and the grass side relative to the reserved top/bottom UI
    // strips (which are unchanged from Phase 7C). Kept as its own method so every
    // earlier command, including "Build Phase 7C Polished Mobile Layout", stays
    // untouched.
    private static void BuildGameScenePhase7C1(
        GameObject gridPrefab, GameObject straightWidePrefab, GameObject straightNarrowPrefab, GameObject cornerPrefab,
        GameObject sourcePrefab, GameObject targetPrefab, GameObject cloudPrefab)
    {
        PipeWaterSpriteSheetBinder.TryBindAll(true);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, -0.9f, -10f);
        cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraGO.AddComponent<AudioListener>();

        // World-space background: instantiates the manually-configured
        // Background2D.prefab as-is (its own SpriteRenderer/sprite/sortingOrder -100
        // are never touched here - no resize, reposition, recolor or overwrite).
        // Its art already includes the grass ground, so no separate placeholder
        // rectangle is generated.
        GameObject backgroundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Background2D.prefab");
        if (backgroundPrefab != null)
        {
            PrefabUtility.InstantiatePrefab(backgroundPrefab, scene);
        }
        else
        {
            Debug.LogWarning($"SceneBuilder2D: '{PrefabFolder}/Background2D.prefab' not found; continuing without a background.");
        }

        // GameRoot2D
        var gameRoot = new GameObject("GameRoot2D");
        var boardManagerGO = new GameObject("BoardManager2D");
        boardManagerGO.transform.SetParent(gameRoot.transform);
        var levelManagerGO = new GameObject("LevelManager2D");
        levelManagerGO.transform.SetParent(gameRoot.transform);
        var flowSolverGO = new GameObject("FlowSolver2D");
        flowSolverGO.transform.SetParent(gameRoot.transform);
        var scoreManagerGO = new GameObject("ScoreManager2D");
        scoreManagerGO.transform.SetParent(gameRoot.transform);
        var waterFlowAnimatorGO = new GameObject("WaterFlowAnimator2D");
        waterFlowAnimatorGO.transform.SetParent(gameRoot.transform);

        // BoardRoot hierarchy
        var boardRoot = new GameObject("BoardRoot");
        var gridCellsContainer = new GameObject("GridCells");
        gridCellsContainer.transform.SetParent(boardRoot.transform);
        var pipesContainer = new GameObject("Pipes");
        pipesContainer.transform.SetParent(boardRoot.transform);
        var sourceTargetContainer = new GameObject("SourceTarget");
        sourceTargetContainer.transform.SetParent(boardRoot.transform);
        var cloudAndRainContainer = new GameObject("CloudAndRain");
        cloudAndRainContainer.transform.SetParent(boardRoot.transform);

        // SuccessFXZone: future parent for flower/duck success animations. Empty and
        // inactive for now - Background2D.prefab's own art already provides the
        // visible grass ground, so no placeholder rectangle is created here.
        var successFXZone = new GameObject("SuccessFXZone");
        successFXZone.transform.SetParent(boardRoot.transform);
        successFXZone.transform.position = new Vector3(0f, -3.5f, 0f);
        successFXZone.SetActive(false);

        BoardManager2D boardManager = boardManagerGO.AddComponent<BoardManager2D>();
        var boardSO = new SerializedObject(boardManager);
        boardSO.FindProperty("gridCellPrefab").objectReferenceValue = gridPrefab;
        boardSO.FindProperty("gridCellsContainer").objectReferenceValue = gridCellsContainer.transform;
        boardSO.FindProperty("width").intValue = 5;
        boardSO.FindProperty("height").intValue = 5;
        boardSO.FindProperty("cellSize").floatValue = 1f;
        boardSO.ApplyModifiedPropertiesWithoutUndo();

        // Cloud placeholder: fixed near the top-left grid cell (-2, 2) plus a small
        // upward offset. Created once here (not spawned by LevelManager2D), so it
        // naturally persists across reload/next level without any extra cleanup code.
        Vector2Int cloudGridAnchor = new Vector2Int(-2, 2);
        Vector3 cloudOffset = new Vector3(0f, 0.75f, 0f);
        Vector3 cloudWorldPos = boardManager.GridToWorld(cloudGridAnchor) + cloudOffset;
        GameObject cloudInstance = Object.Instantiate(cloudPrefab, cloudWorldPos, Quaternion.identity, cloudAndRainContainer.transform);
        cloudInstance.name = "Cloud2D";

        // Rain loop: starts as the same placeholder vertical streak as Phase 6, now
        // wrapped in a SpriteFrameAnimator2D (single placeholder frame by default).
        Material unlitMaterialForRain = CreateUnlitMaterial();
        Texture2D rainTexture = CreateBarTexture(new Color32(150, 200, 235, 180), false);
        Sprite rainSprite = CreateAndImportSprite("rain_loop", rainTexture);
        var rainLoopGO = new GameObject("RainLoopFX");
        rainLoopGO.transform.SetParent(cloudAndRainContainer.transform, false);
        rainLoopGO.transform.position = cloudWorldPos + new Vector3(0f, -0.4f, 0f);
        rainLoopGO.transform.localScale = Vector3.one * 0.5f;
        var rainSr = rainLoopGO.AddComponent<SpriteRenderer>();
        rainSr.sprite = rainSprite;
        rainSr.sharedMaterial = unlitMaterialForRain;
        rainSr.sortingOrder = 3;

        var rainAnimator = rainLoopGO.AddComponent<SpriteFrameAnimator2D>();
        var rainAnimatorSO = new SerializedObject(rainAnimator);
        rainAnimatorSO.FindProperty("targetRenderer").objectReferenceValue = rainSr;
        SerializedProperty placeholderFramesProp = rainAnimatorSO.FindProperty("frames");
        placeholderFramesProp.arraySize = 1;
        placeholderFramesProp.GetArrayElementAtIndex(0).objectReferenceValue = rainSprite;
        rainAnimatorSO.FindProperty("framesPerSecond").floatValue = 10f;
        rainAnimatorSO.FindProperty("loop").boolValue = true;
        rainAnimatorSO.FindProperty("playOnEnable").boolValue = true;
        rainAnimatorSO.FindProperty("hideWhenStopped").boolValue = false;
        rainAnimatorSO.ApplyModifiedPropertiesWithoutUndo();

        // Attempt to upgrade to the real imported RainCloudSpriteSheet frames. Safe
        // no-op (with a logged warning) if the asset isn't available yet - the
        // placeholder single-frame setup above is left exactly as-is in that case.
        RainCloudSpriteSheetBinder.TryBindToRainLoopFX(rainLoopGO);

        CloudRainVisual2D cloudRainVisual = cloudAndRainContainer.AddComponent<CloudRainVisual2D>();
        var cloudRainVisualSO = new SerializedObject(cloudRainVisual);
        cloudRainVisualSO.FindProperty("cloudObject").objectReferenceValue = cloudInstance;
        cloudRainVisualSO.FindProperty("rainCloudAnimator").objectReferenceValue = rainAnimator;
        cloudRainVisualSO.FindProperty("animationIncludesCloud").boolValue = true;
        cloudRainVisualSO.ApplyModifiedPropertiesWithoutUndo();
        cloudRainVisual.ApplyVisualMode();

        FlowSolver2D flowSolver = flowSolverGO.AddComponent<FlowSolver2D>();
        var flowSO = new SerializedObject(flowSolver);
        flowSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        flowSO.ApplyModifiedPropertiesWithoutUndo();

        ScoreManager2D scoreManager = scoreManagerGO.AddComponent<ScoreManager2D>();

        // No wiring needed: WaterFlowAnimator2D no longer has any drop-related
        // serialized fields. pipeAnimationTimeout/targetFXDuration keep their
        // public-field defaults (1.5s / 0.6s).
        WaterFlowAnimator2D waterFlowAnimator = waterFlowAnimatorGO.AddComponent<WaterFlowAnimator2D>();

        LevelManager2D levelManager = levelManagerGO.AddComponent<LevelManager2D>();
        var levelSO = new SerializedObject(levelManager);
        levelSO.FindProperty("boardManager").objectReferenceValue = boardManager;
        levelSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        levelSO.FindProperty("pipeStraightWidePrefab").objectReferenceValue = straightWidePrefab;
        levelSO.FindProperty("pipeStraightNarrowPrefab").objectReferenceValue = straightNarrowPrefab;
        levelSO.FindProperty("pipeCornerPrefab").objectReferenceValue = cornerPrefab;
        levelSO.FindProperty("sourcePrefab").objectReferenceValue = sourcePrefab;
        levelSO.FindProperty("targetPrefab").objectReferenceValue = targetPrefab;
        levelSO.FindProperty("pipesContainer").objectReferenceValue = pipesContainer.transform;
        levelSO.FindProperty("sourceTargetContainer").objectReferenceValue = sourceTargetContainer.transform;
        levelSO.ApplyModifiedPropertiesWithoutUndo();

        // ---------------- Canvas ----------------
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // SafeAreaRoot: stretches to fill the canvas by default; SafeAreaFitter2D
        // pulls its anchors in to Screen.safeArea at runtime (Editor and device).
        GameObject safeAreaRootGO = CreateUIContainer("SafeAreaRoot", canvasGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        safeAreaRootGO.AddComponent<SafeAreaFitter2D>();

        // ---------------- TopHUD (Level left, Move right - no score) ----------------
        GameObject topHudGO = CreateUIContainer("TopHUD", safeAreaRootGO.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(0f, 130f));

        GameObject levelBadgeGO = CreateUIPanel("LevelBadge", topHudGO.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(56f, 0f), new Vector2(320f, 110f), new Color(1f, 1f, 1f, 0.9f));
        GameObject levelNameGO = CreateUIText("LevelNameText", levelBadgeGO.transform, "Bölüm 1",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Text levelNameText = levelNameGO.GetComponent<Text>();
        levelNameText.fontSize = 42;
        levelNameText.color = Color.black;

        GameObject moveBadgeGO = CreateUIPanel("MoveBadge", topHudGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-56f, 0f), new Vector2(260f, 110f), new Color(1f, 1f, 1f, 0.9f));
        GameObject moveTextGO = CreateUIText("MoveText", moveBadgeGO.transform, "Hamle: 0",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Text moveText = moveTextGO.GetComponent<Text>();
        moveText.fontSize = 42;
        moveText.color = Color.black;

        // ---------------- StatusArea (ResultText, below grass, above buttons) ----------------
        GameObject statusAreaGO = CreateUIContainer("StatusArea", safeAreaRootGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        GameObject resultPanelGO = CreateUIPanel("ResultPanel", statusAreaGO.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 180f), new Vector2(-120f, 80f), new Color(1f, 1f, 1f, 0.85f));
        GameObject resultTextGO = CreateUIText("ResultText", resultPanelGO.transform, "Hazır",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Text resultText = resultTextGO.GetComponent<Text>();
        resultText.fontSize = 36;
        resultText.color = Color.black;

        // ---------------- BottomControls (Reload + StartWater only) ----------------
        GameObject bottomControlsGO = CreateUIContainer("BottomControls", safeAreaRootGO.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0f, 160f));

        GameObject reloadButtonGO = CreateUIButton("ReloadButton", bottomControlsGO.transform, "Yeniden Dene",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-307.5f, 40f), new Vector2(310f, 120f));
        Button reloadButton = reloadButtonGO.GetComponent<Button>();
        reloadButtonGO.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        SetButtonLabelFontSize(reloadButtonGO, 40);

        GameObject startButtonGO = CreateUIButton("StartWaterButton", bottomControlsGO.transform, "Su Toplamaya Başla",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(172.5f, 40f), new Vector2(580f, 120f));
        Button startButton = startButtonGO.GetComponent<Button>();
        SetButtonLabelFontSize(startButtonGO, 40);
        EnableButtonLabelBestFit(startButtonGO, 28, 44);

        // ---------------- InfoPanel (success modal, last sibling = renders on top) ----------------
        GameObject infoPanelGO = CreateUIContainer("InfoPanel", safeAreaRootGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        GameObject modalBlockerGO = CreateUIPanel("ModalBlocker", infoPanelGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.55f));

        GameObject infoCardGO = CreateUIPanel("InfoCard", infoPanelGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(900f, 1000f), new Color(1f, 1f, 1f, 0.98f));

        GameObject infoTitleGO = CreateUIText("InfoTitleText", infoCardGO.transform, "Tebrikler!",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -70f), new Vector2(800f, 90f));
        Text infoTitleText = infoTitleGO.GetComponent<Text>();
        infoTitleText.fontSize = 56;
        infoTitleText.color = Color.black;

        GameObject infoStarGO = CreateUIText("InfoStarText", infoCardGO.transform, "Yildiz: * (1/3)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -180f), new Vector2(800f, 80f));
        Text infoStarText = infoStarGO.GetComponent<Text>();
        infoStarText.fontSize = 48;
        infoStarText.color = Color.black;

        GameObject infoDescriptionGO = CreateUIText("InfoText", infoCardGO.transform, "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(800f, 450f));
        Text infoDescriptionText = infoDescriptionGO.GetComponent<Text>();
        infoDescriptionText.fontSize = 34;
        infoDescriptionText.color = Color.black;
        infoDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        infoDescriptionText.alignment = TextAnchor.UpperCenter;

        GameObject infoNextButtonGO = CreateUIButton("InfoNextLevelButton", infoCardGO.transform, "Sonraki Bölüm",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 90f), new Vector2(420f, 110f));
        Button infoNextButton = infoNextButtonGO.GetComponent<Button>();
        infoNextButtonGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f);
        SetButtonLabelFontSize(infoNextButtonGO, 42);

        infoPanelGO.SetActive(false);

        // EventSystem (New Input System module)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        UIManager2D uiManager = canvasGO.AddComponent<UIManager2D>();
        var uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("levelManager").objectReferenceValue = levelManager;
        uiSO.FindProperty("flowSolver").objectReferenceValue = flowSolver;
        uiSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        uiSO.FindProperty("waterFlowAnimator").objectReferenceValue = waterFlowAnimator;
        uiSO.FindProperty("startWaterButton").objectReferenceValue = startButton;
        uiSO.FindProperty("reloadButton").objectReferenceValue = reloadButton;
        uiSO.FindProperty("levelNameText").objectReferenceValue = levelNameText;
        uiSO.FindProperty("moveText").objectReferenceValue = moveText;
        uiSO.FindProperty("resultText").objectReferenceValue = resultText;
        uiSO.FindProperty("infoPanel").objectReferenceValue = infoPanelGO;
        uiSO.FindProperty("infoTitleText").objectReferenceValue = infoTitleText;
        uiSO.FindProperty("infoStarText").objectReferenceValue = infoStarText;
        uiSO.FindProperty("infoDescriptionText").objectReferenceValue = infoDescriptionText;
        uiSO.FindProperty("infoNextLevelButton").objectReferenceValue = infoNextButton;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Phase 7E: non-destructive installer. Unlike every BuildGameScenePhaseX method
    // above, this does NOT call EditorSceneManager.NewScene - it operates entirely
    // on whatever GameScene2D is currently open (including the user's manually
    // placed Background2D instance), only adding/finding the specific objects this
    // phase needs. Safe to run repeatedly: every step finds-or-creates by name/type
    // instead of blindly instantiating, so re-running never duplicates roots,
    // animators or listeners.
    [MenuItem("YagmurRotasi2D/Install Phase 7E Flower Duck FX")]
    public static void InstallPhase7EFlowerDuckFX()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        GameObject boardRoot = GameObject.Find("BoardRoot");
        if (boardRoot == null)
        {
            Debug.LogError("SceneBuilder2D: 'BoardRoot' not found in the active scene. " +
                "This installer only augments an existing Phase 7C/7C.1 scene - it does not build one from scratch.");
            return;
        }

        // SuccessFXZone must be ACTIVE now (flowers are visible by default before
        // success) - this differs from its empty/inactive Phase 7C.1 state.
        GameObject successFXZone = FindOrCreateChild(boardRoot.transform, "SuccessFXZone", new Vector3(0f, -3.5f, 0f));
        successFXZone.SetActive(true);

        GameObject flowerFXRoot = FindOrCreateChild(successFXZone.transform, "FlowerFXRoot", Vector3.zero);
        GameObject duckFXRoot = FindOrCreateChild(successFXZone.transform, "DuckFXRoot", Vector3.zero);

        SpriteFrameAnimator2D flowerAnimator = FindOrCreateFXInstance(
            flowerFXRoot.transform, "Flower_0", new Vector3(-1f, 0f, 0f), uniformScale: 0.4f, sortingOrder: 3,
            loop: false, holdLastFrame: true, framesPerSecond: 2f);

        SpriteFrameAnimator2D duckAnimator = FindOrCreateFXInstance(
            duckFXRoot.transform, "Duck_0", new Vector3(1f, 0f, 0f), uniformScale: 0.25f, sortingOrder: 4,
            loop: true, holdLastFrame: true, framesPerSecond: 10f);

        // Ducks are hidden by default via DuckFXRoot itself (not per-instance), so
        // additional duck instances added later inherit the same hidden default.
        flowerFXRoot.SetActive(true);
        duckFXRoot.SetActive(false);

        SuccessFXController2D controller = successFXZone.GetComponent<SuccessFXController2D>();
        if (controller == null)
        {
            controller = successFXZone.AddComponent<SuccessFXController2D>();
        }

        var controllerSO = new SerializedObject(controller);
        controllerSO.FindProperty("flowerRoot").objectReferenceValue = flowerFXRoot;
        SerializedProperty flowerAnimatorsProp = controllerSO.FindProperty("flowerAnimators");
        flowerAnimatorsProp.arraySize = 1;
        flowerAnimatorsProp.GetArrayElementAtIndex(0).objectReferenceValue = flowerAnimator;
        controllerSO.FindProperty("duckRoot").objectReferenceValue = duckFXRoot;
        SerializedProperty duckAnimatorsProp = controllerSO.FindProperty("duckAnimators");
        duckAnimatorsProp.arraySize = 1;
        duckAnimatorsProp.GetArrayElementAtIndex(0).objectReferenceValue = duckAnimator;
        controllerSO.FindProperty("successDuration").floatValue = 6f;
        controllerSO.FindProperty("keepDucksVisibleAfterSuccess").boolValue = true;
        controllerSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire UIManager2D -> successFXController (WaterFlowAnimator2D receives the
        // controller as a PlaySuccess(...) parameter from UIManager2D, so it needs
        // no direct scene reference of its own).
        UIManager2D uiManager = Object.FindFirstObjectByType<UIManager2D>();
        if (uiManager != null)
        {
            var uiSO = new SerializedObject(uiManager);
            uiSO.FindProperty("successFXController").objectReferenceValue = controller;
            uiSO.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("SceneBuilder2D: no UIManager2D found in the active scene; successFXController was not wired to it.");
        }

        // Defensive: the old TargetFX2D placeholder circles (FlowerBloomFX/
        // DuckWalkFX under Target2D) already default to inactive in the prefab and
        // are never triggered by the new success path, but force them off here too
        // in case a stale scene instance was left active from earlier testing.
        TargetFX2D[] legacyTargetFX = Object.FindObjectsByType<TargetFX2D>(FindObjectsSortMode.None);
        foreach (TargetFX2D fx in legacyTargetFX)
        {
            fx.ResetFX();
        }

        // Best-effort real sprite binding - safe no-op (with a logged warning) if the
        // flower/duck sheets aren't importable yet; leaves empty placeholder frame
        // arrays untouched in that case.
        SuccessFXSpriteSheetBinder.TryBindAll(true);

        controller.PrepareInitialState();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SceneBuilder2D: Phase 7E install complete.\n" +
            $"  SuccessFXZone: {GetHierarchyPath(successFXZone.transform)}\n" +
            $"  Flower instances: 1 (Flower_0), Duck instances: 1 (Duck_0)\n" +
            "  Adjust Flower_0/Duck_0 Transform position/scale manually in the Inspector to fit your Background2D art.");
    }

    // Phase 7E.1: expands FlowerFXRoot from 1 to up to 8 flower instances (one per
    // discovered flower variant) and wires them all into SuccessFXController2D.
    // Just like InstallPhase7EFlowerDuckFX, this is fully non-destructive: it never
    // calls EditorSceneManager.NewScene, and it contains ZERO cloud/rain-related
    // code - the user's manually repositioned/resized/added rain clouds (original +
    // two extra) are never touched, moved, resized, deleted or recreated by this
    // command. Existing flower/duck instances that already exist keep their current
    // Transform (position/scale) untouched; only newly created flower instances get
    // a default staggered position.
    [MenuItem("YagmurRotasi2D/Install Phase 7E1 All Flowers Preserve Clouds")]
    public static void InstallPhase7E1AllFlowersPreserveClouds()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        GameObject boardRoot = GameObject.Find("BoardRoot");
        if (boardRoot == null)
        {
            Debug.LogError("SceneBuilder2D: 'BoardRoot' not found in the active scene. " +
                "This installer only augments an existing Phase 7E scene - it does not build one from scratch.");
            return;
        }

        // Find-or-create only. No cloud/rain object of any kind is read, moved,
        // resized or recreated anywhere in this method.
        GameObject successFXZone = FindOrCreateChild(boardRoot.transform, "SuccessFXZone", new Vector3(0f, -3.5f, 0f));
        successFXZone.SetActive(true);

        GameObject flowerFXRoot = FindOrCreateChild(successFXZone.transform, "FlowerFXRoot", Vector3.zero);
        GameObject duckFXRoot = FindOrCreateChild(successFXZone.transform, "DuckFXRoot", Vector3.zero);
        flowerFXRoot.SetActive(true);

        // Preserve the existing duck instance exactly as-is (FindOrCreateFXInstance
        // never repositions an already-existing instance); only created if missing.
        SpriteFrameAnimator2D duckAnimator = FindOrCreateFXInstance(
            duckFXRoot.transform, "Duck_0", new Vector3(1f, 0f, 0f), uniformScale: 0.25f, sortingOrder: 4,
            loop: true, holdLastFrame: true, framesPerSecond: 10f);
        duckFXRoot.SetActive(false);

        // One flower instance per discovered flower variant (up to 8). Default
        // staggered horizontal spread with slight vertical/scale variation - only
        // applied to NEWLY created instances; existing ones keep their current
        // Transform untouched.
        const int flowerSlotCount = 8;
        var flowerAnimators = new List<SpriteFrameAnimator2D>(flowerSlotCount);
        for (int i = 0; i < flowerSlotCount; i++)
        {
            float t = flowerSlotCount > 1 ? i / (float)(flowerSlotCount - 1) : 0.5f;
            float x = Mathf.Lerp(-2.4f, 2.4f, t);
            float y = (i % 2 == 0) ? 0.15f : -0.1f;
            float scale = 0.32f + (i % 3) * 0.03f;

            SpriteFrameAnimator2D flowerAnimator = FindOrCreateFXInstance(
                flowerFXRoot.transform, $"Flower_{i}", new Vector3(x, y, 0f), uniformScale: scale, sortingOrder: 3,
                loop: false, holdLastFrame: true, framesPerSecond: 2f);
            flowerAnimators.Add(flowerAnimator);
        }

        SuccessFXController2D controller = successFXZone.GetComponent<SuccessFXController2D>();
        if (controller == null)
        {
            controller = successFXZone.AddComponent<SuccessFXController2D>();
        }

        var controllerSO = new SerializedObject(controller);
        controllerSO.FindProperty("flowerRoot").objectReferenceValue = flowerFXRoot;
        SerializedProperty flowerAnimatorsProp = controllerSO.FindProperty("flowerAnimators");
        flowerAnimatorsProp.arraySize = flowerAnimators.Count;
        for (int i = 0; i < flowerAnimators.Count; i++)
        {
            flowerAnimatorsProp.GetArrayElementAtIndex(i).objectReferenceValue = flowerAnimators[i];
        }
        controllerSO.FindProperty("duckRoot").objectReferenceValue = duckFXRoot;
        SerializedProperty duckAnimatorsProp = controllerSO.FindProperty("duckAnimators");
        duckAnimatorsProp.arraySize = 1;
        duckAnimatorsProp.GetArrayElementAtIndex(0).objectReferenceValue = duckAnimator;
        controllerSO.FindProperty("successDuration").floatValue = 6f;
        controllerSO.FindProperty("keepDucksVisibleAfterSuccess").boolValue = true;
        controllerSO.ApplyModifiedPropertiesWithoutUndo();

        UIManager2D uiManager = Object.FindFirstObjectByType<UIManager2D>();
        if (uiManager != null)
        {
            var uiSO = new SerializedObject(uiManager);
            uiSO.FindProperty("successFXController").objectReferenceValue = controller;
            uiSO.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("SceneBuilder2D: no UIManager2D found in the active scene; successFXController was not wired to it.");
        }

        // Defensive only (same as Phase 7E) - old TargetFX2D placeholder circles
        // already default to inactive and are never triggered by the new path.
        TargetFX2D[] legacyTargetFX = Object.FindObjectsByType<TargetFX2D>(FindObjectsSortMode.None);
        foreach (TargetFX2D fx in legacyTargetFX)
        {
            fx.ResetFX();
        }

        // Assigns one unique flower sheet per instance (1-to-1 by index) and binds
        // duck frames as before. Safe no-op (with a logged warning) for any slot
        // that has no corresponding sheet/instance.
        SuccessFXSpriteSheetBinder.TryBindAll(true);

        controller.PrepareInitialState();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SceneBuilder2D: Phase 7E.1 install complete.\n" +
            $"  SuccessFXZone: {GetHierarchyPath(successFXZone.transform)}\n" +
            $"  Flower instances: {flowerAnimators.Count} (Flower_0..Flower_{flowerAnimators.Count - 1}), Duck instances: 1 (Duck_0)\n" +
            "  Rain-cloud objects were not read, moved or modified by this command.\n" +
            "  Adjust any Flower_N Transform position/scale manually in the Inspector to fine-tune over your grass art.");
    }

    // Phase 7E.2: only touches two things - SuccessFXController2D's timing fields
    // and the grid-cell sprite (via GridVisualBinder). It does NOT create, find,
    // move or otherwise reference FlowerFXRoot/DuckFXRoot/flower or duck instances,
    // clouds, Background2D, or the Canvas - all of those are left completely alone,
    // exactly as Phase 7E.1 (and the user's manual edits) left them. If
    // SuccessFXController2D doesn't already exist, this command refuses to create
    // one (that would risk fighting the "preserve manual positions" rule) and asks
    // the user to run the Phase 7E1 installer first.
    [MenuItem("YagmurRotasi2D/Install Phase 7E2 Duck 4s and Grid Visual")]
    public static void InstallPhase7E2DuckDurationAndGridVisual()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        SuccessFXController2D controller = Object.FindFirstObjectByType<SuccessFXController2D>();
        if (controller == null)
        {
            Debug.LogError("SceneBuilder2D: no SuccessFXController2D found in the active scene. " +
                "Run 'YagmurRotasi2D > Install Phase 7E1 All Flowers Preserve Clouds' first - " +
                "this command only adjusts its timing fields and the grid visual, it does not create the FX system.");
            return;
        }

        var controllerSO = new SerializedObject(controller);
        controllerSO.FindProperty("successDuration").floatValue = 5f;
        controllerSO.FindProperty("duckAnimationDuration").floatValue = 4f;
        controllerSO.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        bool gridBound = GridVisualBinder.TryBindGridVisual(true);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SceneBuilder2D: Phase 7E.2 install complete.\n" +
            "  SuccessFXController2D.successDuration = 5, duckAnimationDuration = 4\n" +
            $"  Grid visual bound: {gridBound}\n" +
            "  Clouds, Background2D, flowers, ducks, Canvas and board placement were not touched by this command.");
    }

    // Phase 7E.3: combines three independent, non-destructive binders (UI package,
    // start/end markers, flower repair) into one command. Like every Phase 7E.x
    // installer, this never calls EditorSceneManager.NewScene and contains zero
    // cloud/background/board/grid-logic code - it only calls into
    // UIPackageBinder / StartEndMarkerBinder / FlowerAnimationRepair, each of which
    // is independently scoped and safe to re-run.
    [MenuItem("YagmurRotasi2D/Install Phase 7E3 UI Markers Flower Repair")]
    public static void InstallPhase7E3UIMarkersFlowerRepair()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        bool uiBound = UIPackageBinder.TryBindUIPackage(true);
        bool markersBound = StartEndMarkerBinder.TryBindMarkers(true);
        bool flowersRepaired = FlowerAnimationRepair.TryRepairFlowers(true);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SceneBuilder2D: Phase 7E.3 install complete.\n" +
            $"  UI package bound: {uiBound}\n" +
            $"  Start/End markers bound: {markersBound}\n" +
            $"  Flower animations repaired: {flowersRepaired}\n" +
            "  Clouds, Background2D, board/grid logic, level data, successDuration (5) and " +
            "duckAnimationDuration (4) were not touched by this command.");
    }

    // Phase 7E.4's installer ("Install Phase 7E4 Complete UI and PixelFont") has
    // been removed as of Phase 7E.5: its entire purpose was applying the Thaleah
    // pixel font, which the user has explicitly replaced project-wide with
    // SHPinscher-Regular11 ("do not continue using ThaleahFat_TTF... the final
    // visible UI must use SHPinscher-Regular11 only"). Keeping a menu item named
    // after PixelFont around would be actively misleading. UIPackageBinder's UI
    // skinning logic from that phase is unaffected and lives on below via Phase
    // 7E.5's installer, which does everything 7E.4 did plus more.

    // Phase 7E.5: SHPinscher font pass + complete success-panel repair + Hamle
    // badge shift. Like every Phase 7E.x installer, this never calls
    // EditorSceneManager.NewScene and contains zero cloud/background/board/grid-
    // logic/flower/duck/timing code - it only calls into SHPinscherFontBinder and
    // UIPackageBinder, each independently scoped and idempotent. Uses the full
    // TryBindUIPackage (not just TryRepairSuccessPanel) so any other UI element
    // that may be missing its decoration for unrelated historical reasons also
    // gets repaired - already-correct elements are simply no-ops.
    [MenuItem("YagmurRotasi2D/Install Phase 7E5 SHPinscher and Success Panel Repair")]
    public static void InstallPhase7E5SHPinscherAndSuccessPanelRepair()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        bool fontApplied = SHPinscherFontBinder.TryApplyFont(true);
        bool uiBound = UIPackageBinder.TryBindUIPackage(true);
        bool hamleShifted = UIPackageBinder.TryShiftMoveBadgeRight(true);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SceneBuilder2D: Phase 7E.5 install complete.\n" +
            $"  SHPinscher font applied: {fontApplied}\n" +
            $"  Success panel / UI package repaired: {uiBound}\n" +
            $"  Hamle badge shifted: {hamleShifted} (false means it was already shifted or manually adjusted)\n" +
            "  Clouds, Background2D, board/grid logic, flowers, ducks, level data, successDuration (5) and " +
            "duckAnimationDuration (4) were not touched by this command.");
    }

    // Phase 7E.6: replaces the unreliable old InfoPanel-patching approach with a
    // dedicated, self-contained SuccessPanel2D prefab (see
    // Assets/Editor/SuccessPanelPrefabBuilder.cs). Like every Phase 7E.x
    // installer, this never calls EditorSceneManager.NewScene and contains zero
    // cloud/background/board/grid-logic/flower/duck/timing code.
    [MenuItem("YagmurRotasi2D/Install Phase 7E6 Dedicated Success Panel")]
    public static void InstallPhase7E6DedicatedSuccessPanel()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        bool prefabBuilt = SuccessPanelPrefabBuilder.TryBuildPrefab(true);
        bool installed = prefabBuilt && SuccessPanelPrefabBuilder.TryInstallIntoScene(true);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SceneBuilder2D: Phase 7E.6 install complete.\n" +
            $"  SuccessPanel2D.prefab built/updated: {prefabBuilt}\n" +
            $"  Instance installed and wired: {installed}\n" +
            "  SHPinscher-Regular11, successDuration (5), duckAnimationDuration (4), clouds, Background2D, " +
            "board/grid logic, flowers and ducks were not touched by this command.");
    }

    // Phase 7E.7: success-panel BodyText readability + a small top Menu button.
    // Like every Phase 7E.x installer, this never calls EditorSceneManager.NewScene
    // and contains zero cloud/background/board/grid-logic/flower/duck/timing code
    // - it only calls into SuccessPanelPrefabBuilder and UIPackageBinder, each
    // independently scoped and idempotent.
    [MenuItem("YagmurRotasi2D/Install Phase 7E7 Readable Info and Menu Button")]
    public static void InstallPhase7E7ReadableInfoAndMenuButton()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        bool bodyTextUpdated = SuccessPanelPrefabBuilder.TryUpdateBodyTextReadability(true);
        bool menuButtonInstalled = UIPackageBinder.TryInstallMenuButton(true);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SceneBuilder2D: Phase 7E.7 install complete.\n" +
            $"  Success panel BodyText readability updated: {bodyTextUpdated}\n" +
            $"  Menu button installed: {menuButtonInstalled}\n" +
            "  Clouds, Background2D, board/grid logic, flowers, ducks, level data, successDuration (5), " +
            "duckAnimationDuration (4), LevelBadge and MoveBadge positions were not touched by this command.");
    }

    // Phase 7E.9: builds/updates the dedicated in-game menu prefab and installs
    // exactly one instance into GameScene2D, wiring it to the existing top
    // MenuButton via UIManager2D. Like every Phase 7E.x installer, this never
    // calls EditorSceneManager.NewScene and contains zero cloud/background/
    // board/grid-logic/flower/duck/timing/MainMenuScene2D code - it only calls
    // into InGameMenuPrefabBuilder, which is independently scoped and idempotent.
    [MenuItem("YagmurRotasi2D/Install Phase 7E9 In-Game Menu")]
    public static void InstallPhase7E9InGameMenu()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        bool prefabBuilt = InGameMenuPrefabBuilder.TryBuildPrefab(true);
        bool installed = prefabBuilt && InGameMenuPrefabBuilder.TryInstallIntoScene(true);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("SceneBuilder2D: Phase 7E.9 install complete.\n" +
            $"  InGameMenu2D.prefab built/updated: {prefabBuilt}\n" +
            $"  Instance installed and wired: {installed}\n" +
            "  Top MenuButton Transform, DedicatedSuccessPanel, successDuration (5), duckAnimationDuration (4), " +
            "clouds, Background2D, board/grid logic, flowers and ducks were not touched by this command.");
    }

    // Phase 7F: binds/builds the Tee and Cross pipe prefabs from the already-
    // discovered pipes_tileset rows and wires them into the scene's
    // LevelManager2D (pipeTeePrefab/pipeCrossPrefab), then runs the branching
    // solver's deterministic EditMode test suite. Like every installer here,
    // this never calls EditorSceneManager.NewScene, never touches the Canvas,
    // MainMenuScene2D, clouds, Background2D, board/grid logic, flowers, ducks,
    // successDuration/duckAnimationDuration, or Levels 1-3's data - it only
    // calls into BranchingPipeAssetBinder and BranchingSolverTestRunner, and
    // wires two new prefab reference fields on the existing LevelManager2D.
    [MenuItem("YagmurRotasi2D/Install Phase 7F Branching Pipes")]
    public static void InstallPhase7FBranchingPipes()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        bool prefabsBound = BranchingPipeAssetBinder.TryBindAll(true);

        GameObject teePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BranchingPipeAssetBinder.TeePrefabPath);
        GameObject crossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BranchingPipeAssetBinder.CrossPrefabPath);

        LevelManager2D levelManager = Object.FindFirstObjectByType<LevelManager2D>();
        bool levelManagerWired = false;
        if (levelManager != null && (teePrefab != null || crossPrefab != null))
        {
            var so = new SerializedObject(levelManager);
            if (teePrefab != null) so.FindProperty("pipeTeePrefab").objectReferenceValue = teePrefab;
            if (crossPrefab != null) so.FindProperty("pipeCrossPrefab").objectReferenceValue = crossPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            levelManagerWired = true;
        }
        else if (levelManager == null)
        {
            Debug.LogWarning("SceneBuilder2D: no LevelManager2D found in the active scene - pipeTeePrefab/pipeCrossPrefab were not wired.");
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        bool validationPassed = BranchingSolverTestRunner.RunTests();

        if (validationPassed)
        {
            Debug.Log("SceneBuilder2D: Phase 7F install complete.\n" +
                $"  PipeTee2D.prefab / PipeCross2D.prefab bound/updated: {prefabsBound}\n" +
                $"  LevelManager2D.pipeTeePrefab/pipeCrossPrefab wired: {levelManagerWired}\n" +
                "  No production level (1-3) data was changed - Tee/Cross are spawnable but not yet used by any level. " +
                "Branching solver validation: 16/16 PASSED (see BranchingSolverTestRunner log above). " +
                "Clouds, Background2D, board/grid logic, flowers, ducks, successDuration (5), duckAnimationDuration (4), " +
                "Canvas, MainMenuScene2D and the in-game menu were not touched by this command.");
        }
        else
        {
            Debug.LogError("SceneBuilder2D: Phase 7F installation created the assets, but validation failed.\n" +
                "Phase 7F is NOT complete.\n" +
                $"  PipeTee2D.prefab / PipeCross2D.prefab bound/updated: {prefabsBound}\n" +
                $"  LevelManager2D.pipeTeePrefab/pipeCrossPrefab wired: {levelManagerWired}\n" +
                "  See the [FAIL] lines above from BranchingSolverTestRunner for the exact failing case(s) and stack trace(s). " +
                "The prefabs/scene wiring created above are left in place (not rolled back) - only the solver validation itself failed.");
        }
    }

    // Phase 7F.4: Levels 4-6 (Üç Kollu Bahçe / Dört Yönlü Kavşak / Yağmur
    // Dağıtım Ağı) are pure code/data additions already appended to
    // LevelManager2D.BuildLevels() - there is no scene-layout step for the
    // level content itself. This command's job is to (re)confirm the scene's
    // Tee/Cross prefab wiring (idempotent, safe even if Phase 7F's own
    // installer was never run) and then run BOTH the full branching solver
    // validation (which now covers all 6 levels via Case03) and the
    // dedicated Levels 4-6 report. Like every installer here, this never
    // calls EditorSceneManager.NewScene and never touches MainMenuScene2D.
    [MenuItem("YagmurRotasi2D/Install Phase 7F4 Branching Production Levels")]
    public static void InstallPhase7F4BranchingProductionLevels()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SceneBuilder2D: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return;
        }

        bool prefabsBound = BranchingPipeAssetBinder.TryBindAll(true);

        GameObject teePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BranchingPipeAssetBinder.TeePrefabPath);
        GameObject crossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BranchingPipeAssetBinder.CrossPrefabPath);

        LevelManager2D levelManager = Object.FindFirstObjectByType<LevelManager2D>();
        bool levelManagerWired = false;
        if (levelManager != null && (teePrefab != null || crossPrefab != null))
        {
            var so = new SerializedObject(levelManager);
            if (teePrefab != null) so.FindProperty("pipeTeePrefab").objectReferenceValue = teePrefab;
            if (crossPrefab != null) so.FindProperty("pipeCrossPrefab").objectReferenceValue = crossPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            levelManagerWired = true;
        }
        else if (levelManager == null)
        {
            Debug.LogWarning("SceneBuilder2D: no LevelManager2D found in the active scene - pipeTeePrefab/pipeCrossPrefab were not (re-)wired.");
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        bool solverValidationPassed = BranchingSolverTestRunner.RunTests();
        bool levels456Passed = ProductionBranchingLevelsValidator.ValidateLevels456(true);

        if (solverValidationPassed && levels456Passed)
        {
            Debug.Log("SceneBuilder2D: Phase 7F.4 install complete.\n" +
                $"  Production level count: {LevelManager2D.ProductionLevelCount}\n" +
                $"  PipeTee2D.prefab / PipeCross2D.prefab bound/updated: {prefabsBound}\n" +
                $"  LevelManager2D.pipeTeePrefab/pipeCrossPrefab wired: {levelManagerWired}\n" +
                "  Levels 1-3 layouts/rotations were not changed - Levels 4-6 (Üç Kollu Bahçe / Dört Yönlü Kavşak / " +
                "Yağmur Dağıtım Ağı) were appended after Level 3. " +
                "Branching solver validation: 16/16 PASSED (now covering all 6 levels via Case03). " +
                "Dedicated Levels 4-6 report: PASSED (see ProductionBranchingLevelsValidator log above). " +
                "Clouds, Background2D, board/grid logic, flowers, ducks, successDuration (5), duckAnimationDuration (4), " +
                "Canvas, MainMenuScene2D and the in-game menu were not touched by this command.");
        }
        else
        {
            Debug.LogError("SceneBuilder2D: Phase 7F.4 validation failed - NOT marking Phase 7F.4 complete.\n" +
                $"  Solver validation 16/16: {solverValidationPassed}\n" +
                $"  Levels 4-6 dedicated validation: {levels456Passed}\n" +
                "  See the [FAIL] logs above (from BranchingSolverTestRunner and/or ProductionBranchingLevelsValidator) " +
                "for the exact failing level, tile, and leak. Prefabs/scene wiring created above are left in place " +
                "(not rolled back) - only validation failed.");
        }
    }

    /// <summary>Finds an existing named child under parent, or creates+parents a new empty GameObject at localPos if none exists. Never repositions an existing child.</summary>
    private static GameObject FindOrCreateChild(Transform parent, string name, Vector3 localPos)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        return go;
    }

    /// <summary>Finds an existing named flower/duck instance under parent, or creates one with a SpriteRenderer + configured SpriteFrameAnimator2D. Never repositions an existing instance.</summary>
    private static SpriteFrameAnimator2D FindOrCreateFXInstance(
        Transform parent, string name, Vector3 localPos, float uniformScale, int sortingOrder, bool loop, bool holdLastFrame, float framesPerSecond)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        bool isNew = existing == null;

        if (isNew)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * uniformScale;
        }
        else
        {
            go = existing.gameObject;
        }

        var renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = go.AddComponent<SpriteRenderer>();
        }
        renderer.sortingOrder = sortingOrder;

        var animator = go.GetComponent<SpriteFrameAnimator2D>();
        if (animator == null)
        {
            animator = go.AddComponent<SpriteFrameAnimator2D>();
        }

        var animatorSO = new SerializedObject(animator);
        animatorSO.FindProperty("targetRenderer").objectReferenceValue = renderer;
        animatorSO.FindProperty("loop").boolValue = loop;
        animatorSO.FindProperty("playOnEnable").boolValue = false;
        animatorSO.FindProperty("hideWhenStopped").boolValue = false;
        animatorSO.FindProperty("holdLastFrameOnComplete").boolValue = holdLastFrame;
        animatorSO.FindProperty("framesPerSecond").floatValue = framesPerSecond;
        animatorSO.ApplyModifiedPropertiesWithoutUndo();

        return animator;
    }

    private static string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static GameObject CreateUIText(
        string name, Transform parent, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 42;
        txt.color = Color.black;

        return go;
    }

    private static GameObject CreateUIButton(
        string name, Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.5f, 0.9f);

        go.AddComponent<Button>();

        GameObject textGO = CreateUIText(name + "Text", go.transform, label,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        textRt.anchoredPosition = Vector2.zero;
        Text txt = textGO.GetComponent<Text>();
        txt.color = Color.white;
        txt.fontSize = 48;

        return go;
    }

    private static GameObject CreateUIPanel(
        string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color backgroundColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var image = go.AddComponent<Image>();
        image.color = backgroundColor;

        return go;
    }

    private static void SetButtonLabelFontSize(GameObject buttonGO, int fontSize)
    {
        Text label = buttonGO.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.fontSize = fontSize;
        }
    }

    /// <summary>Lets a button's label shrink (never wrap/clip) on narrow screens instead of overflowing.</summary>
    private static void EnableButtonLabelBestFit(GameObject buttonGO, int minSize, int maxSize)
    {
        Text label = buttonGO.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = minSize;
            label.resizeTextMaxSize = maxSize;
        }
    }

    /// <summary>Plain RectTransform-only layout node (no Image) - used for logical grouping containers like SafeAreaRoot/TopHUD/BottomControls.</summary>
    private static GameObject CreateUIContainer(
        string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return go;
    }
}
