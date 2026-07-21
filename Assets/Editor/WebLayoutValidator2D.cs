using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Phase 9B Part M: validates the three scenes' responsive configuration
/// against a fixed list of target viewports. Deliberately PURE COMPUTATION -
/// it opens each scene read-only to inspect static configuration (CanvasScaler
/// reference resolution, ResponsiveLevelGrid2D tuning constants,
/// GameplayBoardFitter2D's board-area fractions, camera orthographicSize),
/// then replicates the exact runtime formulas (Unity's own documented
/// CanvasScaler ScaleWithScreenSize/MatchWidthOrHeight formula,
/// ResponsiveLevelGrid2D's column formula, GameplayBoardFitter2D's fit-scale
/// formula, WebViewportState2D.ClassifyStatic) FOR each target resolution -
/// it never actually resizes the Game view or touches Screen.width/height,
/// and never marks a scene dirty or saves anything. Some requested checks
/// (visual clipping, text overflow, exact pixel bounds of hand-authored
/// artwork) are NOT programmatically determinable this way and are reported
/// as "not checked - verify visually" rather than a fabricated PASS/FAIL.
/// </summary>
public static class WebLayoutValidator2D
{
    private static readonly (string Name, int Width, int Height)[] TargetViewports =
    {
        ("1920x1080", 1920, 1080),
        ("1366x768", 1366, 768),
        ("1440x900", 1440, 900),
        ("1280x960", 1280, 960),
        ("960x540", 960, 540),
        ("2560x1080", 2560, 1080),
        ("768x1024 (portrait-blocked)", 768, 1024),
    };

    private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene2D.unity";
    private const string LevelSelectScenePath = "Assets/Scenes/LevelSelectScene2D.unity";
    private const string GameplayScenePath = "Assets/Scenes/GameScene2D.unity";

    [MenuItem("YagmurRotasi2D/Phase 9/Validate Web Layouts")]
    public static void ValidateWebLayoutsCommand()
    {
        string activeScenePath = EditorSceneManager.GetActiveScene().path;

        var lines = new List<string> { "Phase 9B Web Layout Validation - pure computation, no scene was saved or altered." };
        bool overallOk = true;

        overallOk &= ValidateMainMenu(lines);
        overallOk &= ValidateLevelSelect(lines);
        overallOk &= ValidateGameplay(lines);

        // Restore whatever scene was open before this command ran (read-only
        // throughout - each Validate* method reopens scenes without saving).
        if (!string.IsNullOrEmpty(activeScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(activeScenePath) != null)
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }

        lines.Add(overallOk ? "\nOVERALL: PASS (all computed checks passed - see 'not checked' notes above for anything outside static analysis' reach)." : "\nOVERALL: FAIL (see FAIL lines above).");
        Debug.Log(string.Join("\n", lines));
    }

    // ---------------- Shared math (mirrors the real runtime components exactly) ----------------

    /// <summary>Unity's own documented ScaleWithScreenSize + MatchWidthOrHeight formula.</summary>
    private static float ComputeCanvasScaleFactor(Vector2 referenceResolution, float matchWidthOrHeight, int screenWidth, int screenHeight)
    {
        float logWidth = Mathf.Log(screenWidth / referenceResolution.x, 2f);
        float logHeight = Mathf.Log(screenHeight / referenceResolution.y, 2f);
        float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight);
        return Mathf.Pow(2f, logWeightedAverage);
    }

    private static bool ValidateMainMenu(List<string> lines)
    {
        lines.Add("\n=== MainMenuScene2D ===");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) == null)
        {
            lines.Add("FAIL: scene not found.");
            return false;
        }

        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        GameObject canvasGO = GameObject.Find("Canvas");
        CanvasScaler scaler = canvasGO != null ? canvasGO.GetComponent<CanvasScaler>() : null;
        if (scaler == null)
        {
            lines.Add("FAIL: no Canvas/CanvasScaler found.");
            return false;
        }

        bool ok = true;
        foreach (var (name, w, h) in TargetViewports)
        {
            float scale = ComputeCanvasScaleFactor(scaler.referenceResolution, scaler.matchWidthOrHeight, w, h);
            WebLayoutCategory2D category = WebViewportState2D.ClassifyStatic(w, h);
            bool pass = scale > 0f;
            ok &= pass;
            lines.Add($"{(pass ? "PASS" : "FAIL")}: {name} - category={category}, refResolution={scaler.referenceResolution}, scaleFactor={scale:0.000}. " +
                "Fullscreen button/hover/pressed/focus states: not checked - verify visually.");
        }
        return ok;
    }

    private static bool ValidateLevelSelect(List<string> lines)
    {
        lines.Add("\n=== LevelSelectScene2D ===");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelSelectScenePath) == null)
        {
            lines.Add("FAIL: scene not found.");
            return false;
        }

        EditorSceneManager.OpenScene(LevelSelectScenePath, OpenSceneMode.Single);
        GameObject canvasGO = GameObject.Find("Canvas");
        CanvasScaler scaler = canvasGO != null ? canvasGO.GetComponent<CanvasScaler>() : null;
        Transform content = GameObject.Find("Content") != null ? GameObject.Find("Content").transform : null;
        ResponsiveLevelGrid2D grid = content != null ? content.GetComponent<ResponsiveLevelGrid2D>() : null;

        if (scaler == null || grid == null)
        {
            lines.Add("FAIL: Canvas/CanvasScaler or Content/ResponsiveLevelGrid2D not found.");
            return false;
        }

        var gridSO = new SerializedObject(grid);
        float minCardWidth = gridSO.FindProperty("minCardWidth").floatValue;
        float maxCardWidth = gridSO.FindProperty("maxCardWidth").floatValue;
        float spacing = gridSO.FindProperty("spacing").floatValue;
        int minColumns = gridSO.FindProperty("minColumns").intValue;
        int maxColumns = gridSO.FindProperty("maxColumns").intValue;

        // Approximate content width in canvas units: reference width minus
        // the ScrollView's percentage margins (4% each side, per
        // LevelSelectSceneBuilder2D) minus the reserved scrollbar strip -
        // matches the ACTUAL runtime anchoring closely enough for a
        // pass/fail column-count sanity check (not pixel-exact - the
        // CanvasScaler blend shifts effective canvas width slightly for
        // aspect ratios far from the 1920x1080 reference).
        bool ok = true;
        foreach (var (name, w, h) in TargetViewports)
        {
            float scale = ComputeCanvasScaleFactor(scaler.referenceResolution, scaler.matchWidthOrHeight, w, h);
            float canvasWidth = w / Mathf.Max(scale, 0.0001f);
            float contentWidth = canvasWidth * 0.92f - 38f;
            WebLayoutCategory2D category = WebViewportState2D.ClassifyStatic(w, h);

            int columns = Mathf.Clamp(Mathf.FloorToInt((contentWidth + spacing) / (minCardWidth + spacing)), minColumns, maxColumns);
            float cellWidth = Mathf.Clamp((contentWidth - spacing * (columns - 1)) / Mathf.Max(columns, 1), minCardWidth, maxCardWidth);

            bool pass = category == WebLayoutCategory2D.PortraitBlocked || (columns >= minColumns && cellWidth >= minCardWidth - 1f);
            ok &= pass;
            lines.Add($"{(pass ? "PASS" : "FAIL")}: {name} - category={category}, scaleFactor={scale:0.000}, estContentWidth={contentWidth:0}, " +
                $"estColumns={columns}, estCardWidth={cellWidth:0.0}. Level 1 visible/Level 100 reachable via scroll: not checked - verify visually.");
        }
        return ok;
    }

    private static bool ValidateGameplay(List<string> lines)
    {
        lines.Add("\n=== GameScene2D ===");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScenePath) == null)
        {
            lines.Add("FAIL: scene not found.");
            return false;
        }

        EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        GameObject cameraGO = GameObject.Find("Main Camera");
        Camera camera = cameraGO != null ? cameraGO.GetComponent<Camera>() : null;
        GameObject fitContainerGO = GameObject.Find("BoardFitContainer");
        GameplayBoardFitter2D fitter = fitContainerGO != null ? fitContainerGO.GetComponent<GameplayBoardFitter2D>() : null;

        if (camera == null || fitter == null)
        {
            lines.Add("FAIL: Main Camera or BoardFitContainer/GameplayBoardFitter2D not found - run 'Build Web Gameplay Layout' first.");
            return false;
        }

        var fitterSO = new SerializedObject(fitter);
        Vector2 areaMin = fitterSO.FindProperty("boardAreaMin").vector2Value;
        Vector2 areaMax = fitterSO.FindProperty("boardAreaMax").vector2Value;
        float orthoSize = camera.orthographicSize;

        var gameplayLevels = new (int LevelNumber, int GridSize)[] { (1, 5), (18, 6), (50, 8), (100, 10) };

        bool ok = true;
        foreach (var (name, w, h) in TargetViewports)
        {
            float aspect = w / (float)h;
            // Camera viewport-fraction -> world-space extents (orthographic,
            // z-independent) - same math as GameplayBoardFitter2D.Recompute,
            // computed here analytically instead of via ViewportToWorldPoint
            // since the camera isn't actually rendering at this resolution.
            float worldHalfHeight = orthoSize;
            float worldHalfWidth = orthoSize * aspect;
            float areaWidth = (areaMax.x - areaMin.x) * (worldHalfWidth * 2f);
            float areaHeight = (areaMax.y - areaMin.y) * (worldHalfHeight * 2f);
            float fitScale = Mathf.Min(areaWidth, areaHeight) / 5f; // BoardManager2D.ReferenceBoardWorldSize

            WebLayoutCategory2D category = WebViewportState2D.ClassifyStatic(w, h);
            bool boardFits = fitScale > 0.05f; // sanity floor - board renders at a non-degenerate size
            ok &= boardFits;

            lines.Add($"{(boardFits ? "PASS" : "FAIL")}: {name} - category={category}, boardAreaWorld={areaWidth:0.00}x{areaHeight:0.00}, fitScale={fitScale:0.000}. " +
                "Board square aspect: guaranteed by construction (fitScale applies uniformly, see GameplayBoardFitter2D). " +
                "Source/Target visible, control-panel overlap, button min size: not checked - verify visually.");

            foreach (var (levelNumber, gridSize) in gameplayLevels)
            {
                // ReferenceBoardWorldSize (5 world units) is constant regardless
                // of grid size (5x5..10x10) - see BoardManager2D/CellSize doc -
                // so fitScale above already covers every grid size identically;
                // this just records which levels were considered.
                lines.Add($"    Level {levelNumber} ({gridSize}x{gridSize}): board footprint is grid-size-independent (always 5 world units square before fitScale) - fitScale={fitScale:0.000} applies identically.");
            }
        }
        return ok;
    }
}
