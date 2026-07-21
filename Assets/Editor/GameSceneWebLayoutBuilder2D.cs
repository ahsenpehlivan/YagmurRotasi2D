using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.Gameplay2D;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Phase 9B: converts GameScene2D's HUD from its portrait top/bottom-strip
/// layout into a landscape top-bar + board-area + control-panel shell, and
/// wires the board-fitting mechanism (GameplayBoardFitter2D) that keeps the
/// EXISTING pipe/world coordinate system (BoardManager2D.GridToWorld, cell
/// sizing, Source/Target scaling) completely untouched while making the
/// board render at whatever size/position fits the current browser viewport.
///
/// STRICT PRESERVATION (never touched by this builder): campaign level
/// assets, FlowSolver2D, BoardManager2D/LevelManager2D internal math,
/// CampaignSession2D, pipe/Source/Target/grid-cell prefabs and their
/// individual Transforms, flower/duck/cloud/rain FX objects, existing Button
/// listener wiring (UIManager2D.Awake already owns that) - this only moves/
/// resizes CONTAINER-level RectTransforms and reparents two whole-board
/// root GameObjects as a single unit (see ReparentBoardUnderFitContainer).
///
/// Board-fitting approach (Option A - one calculated world-space container,
/// chosen over fitting the camera to a viewport rect): the Phase 9B audit
/// found BoardManager2D (whose transform.position GridToWorld() reads) and
/// BoardRoot (the actual parent of GridCells/Pipes/SourceTarget) were two
/// SEPARATE root GameObjects that only coincided at world origin by
/// coincidence. This builder reparents BOTH under one new "BoardFitContainer"
/// wrapper so GameplayBoardFitter2D's single uniform scale+offset always
/// keeps GridToWorld()'s math and the actual rendered board in perfect
/// lockstep - see GameplayBoardFitter2D's own doc comment for the full
/// reasoning. Fitting the CAMERA instead was rejected: it would also move/
/// resize the Screen Space Overlay HUD's world-space raycasting assumptions
/// for no benefit, and would require a second camera or viewport-rect
/// juggling to keep the HUD full-screen while only the board's camera framing
/// changed - strictly more moving parts for the identical visual result.
/// </summary>
public static class GameSceneWebLayoutBuilder2D
{
    private const string ScenePath = "Assets/Scenes/GameScene2D.unity";

    private const float TopBarHeight = 84f;
    private const float ContentPadding = 40f;
    private const float BoardAreaWidthFraction = 0.72f; // of the content area (usable width minus padding), left side
    private const float ControlPanelMinWidth = 380f;
    private const float ControlPanelMaxWidth = 520f;

    // Menu entry point lives on WebLayoutAggregatorCommands2D
    // ("YagmurRotasi2D/Phase 9/Build Web Gameplay Layout") since that command
    // also rebuilds the SuccessPanel2D/InGameMenu2D prefabs first - GameScene2D's
    // own UIManager2D fields depend on those prefabs already being current.

    public static bool TryBuildLayout(bool logDetails)
    {
        bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        if (!sceneExists)
        {
            Debug.LogError($"GameSceneWebLayoutBuilder2D: '{ScenePath}' does not exist. Nothing to convert.");
            return false;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var modified = new System.Collections.Generic.List<string>();

        // ---------------- Camera: clean landscape framing ----------------
        GameObject cameraGO = GameObject.Find("Main Camera");
        if (cameraGO == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'Main Camera' not found. Aborting - refusing to create a second camera.");
            return false;
        }
        Camera camera = cameraGO.GetComponent<Camera>();
        // The old -0.9 Y offset was tuned to frame the portrait board+HUD
        // arrangement - meaningless now that GameplayBoardFitter2D positions
        // the board explicitly regardless of camera framing. Reset to a
        // clean centered landscape default; orthographicSize is left as-is
        // (the fitter adapts to whatever it is - see its own doc comment).
        cameraGO.transform.position = new Vector3(0f, 0f, cameraGO.transform.position.z);
        modified.Add("Main Camera (position reset to (0,0,z) - Phase 9A's portrait Y-offset no longer applies)");

        // ---------------- Board: reparent under one fit container (Option A) ----------------
        Transform boardFitContainer = ReparentBoardUnderFitContainer(modified);
        if (boardFitContainer == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: could not locate 'BoardManager2D' (under GameRoot2D) and/or 'BoardRoot' - aborting board-fit setup without touching anything.");
            return false;
        }

        // ---------------- Canvas / CanvasScaler: landscape reference ----------------
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'Canvas' not found. Aborting.");
            return false;
        }
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            modified.Add("Canvas/CanvasScaler (referenceResolution -> 1920x1080)");
        }

        GameObject safeAreaRoot = GameObject.Find("SafeAreaRoot");
        if (safeAreaRoot == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'SafeAreaRoot' not found. Aborting.");
            return false;
        }

        // ---------------- Shared Phase 9B responsive services ----------------
        WebViewportState2D viewportState = canvasGO.GetComponent<WebViewportState2D>();
        if (viewportState == null) viewportState = canvasGO.AddComponent<WebViewportState2D>();

        WebFullscreenController2D fullscreenController = canvasGO.GetComponent<WebFullscreenController2D>();
        if (fullscreenController == null) fullscreenController = canvasGO.AddComponent<WebFullscreenController2D>();

        GameObject gameRoot2D = GameObject.Find("GameRoot2D");
        PipeHoverCoordinator2D hoverCoordinator = null;
        if (gameRoot2D != null)
        {
            hoverCoordinator = gameRoot2D.GetComponent<PipeHoverCoordinator2D>();
            if (hoverCoordinator == null) hoverCoordinator = gameRoot2D.AddComponent<PipeHoverCoordinator2D>();
        }

        GameplayBoardFitter2D boardFitter = boardFitContainer.gameObject.GetComponent<GameplayBoardFitter2D>();
        if (boardFitter == null) boardFitter = boardFitContainer.gameObject.AddComponent<GameplayBoardFitter2D>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevPerformanceHud2D devHud = canvasGO.GetComponent<DevPerformanceHud2D>();
        if (devHud == null) devHud = canvasGO.AddComponent<DevPerformanceHud2D>();
        var devHudSO = new SerializedObject(devHud);
        devHudSO.FindProperty("viewportState").objectReferenceValue = viewportState;
        devHudSO.FindProperty("levelManager").objectReferenceValue = gameRoot2D != null ? gameRoot2D.GetComponentInChildren<LevelManager2D>() : null;
        devHudSO.ApplyModifiedPropertiesWithoutUndo();
#endif

        // ---------------- Top bar ----------------
        GameObject topHud = FindOrCreateChild(safeAreaRoot.transform, "TopHUD");
        SetAnchoredRect(RectOf(topHud), TopLeft, new Vector2(1f, 1f), TopCenter, Vector2.zero, new Vector2(0f, TopBarHeight));

        GameObject backButtonGO = FindOrCreateChild(topHud.transform, "BackButton");
        SetAnchoredRect(RectOf(backButtonGO), TopLeft, TopLeft, TopLeft, new Vector2(ContentPadding * 0.5f, -TopBarHeight * 0.5f), new Vector2(140f, 64f));
        EnsureButtonComponent(backButtonGO, "Geri");

        GameObject levelBadge = FindOrCreateChild(topHud.transform, "LevelBadge");
        SetAnchoredRect(RectOf(levelBadge), TopLeft, TopLeft, TopLeft, new Vector2(180f, -TopBarHeight * 0.5f), new Vector2(220f, 64f));

        GameObject moveBadge = FindOrCreateChild(topHud.transform, "MoveBadge");
        SetAnchoredRect(RectOf(moveBadge), TopCenter, TopCenter, TopCenter, new Vector2(0f, -TopBarHeight * 0.5f), new Vector2(220f, 64f));

        GameObject fullscreenButtonGO = FindOrCreateChild(topHud.transform, "FullscreenButton");
        SetAnchoredRect(RectOf(fullscreenButtonGO), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-ContentPadding * 0.5f - 150f, -TopBarHeight * 0.5f), new Vector2(140f, 64f));
        EnsureButtonComponent(fullscreenButtonGO, "Tam Ekran");

        GameObject menuButtonGO = FindOrCreateChild(topHud.transform, "MenuButton");
        SetAnchoredRect(RectOf(menuButtonGO), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-ContentPadding * 0.5f, -TopBarHeight * 0.5f), new Vector2(64f, 64f));

        modified.Add("TopHUD (BackButton, LevelBadge, MoveBadge, FullscreenButton, MenuButton repositioned into a full-width top bar)");

        // ---------------- Board area (visual placeholder only - the actual board is world-space, see GameplayBoardFitter2D) ----------------
        // BoardViewportArea's rect fraction MUST stay consistent with
        // GameplayBoardFitter2D.boardAreaMin/Max below it (both derived from
        // the same ContentPadding/TopBarHeight/BoardAreaWidthFraction
        // constants) - this GameObject itself renders nothing, it exists only
        // as an Editor-visible reference frame for where the fitter is
        // targeting, useful when tuning in Scene view.
        GameObject boardArea = FindOrCreateChild(safeAreaRoot.transform, "BoardViewportArea");
        RectTransform boardAreaRt = RectOf(boardArea);
        boardAreaRt.anchorMin = new Vector2(0f, 0f);
        boardAreaRt.anchorMax = new Vector2(BoardAreaWidthFraction, 1f);
        boardAreaRt.pivot = Center;
        boardAreaRt.offsetMin = new Vector2(ContentPadding, ContentPadding);
        boardAreaRt.offsetMax = new Vector2(-ContentPadding * 0.5f, -(TopBarHeight + ContentPadding));
        var boardAreaImage = boardArea.GetComponent<Image>();
        if (boardAreaImage != null) Object.DestroyImmediate(boardAreaImage); // purely a layout marker, never rendered

        // ---------------- Control panel (right side) ----------------
        GameObject controlPanel = FindOrCreateChild(safeAreaRoot.transform, "ControlPanel");
        RectTransform controlPanelRt = RectOf(controlPanel);
        controlPanelRt.anchorMin = new Vector2(BoardAreaWidthFraction, 0f);
        controlPanelRt.anchorMax = new Vector2(1f, 1f);
        controlPanelRt.pivot = Center;
        controlPanelRt.offsetMin = new Vector2(ContentPadding * 0.5f, ContentPadding);
        controlPanelRt.offsetMax = new Vector2(-ContentPadding, -(TopBarHeight + ContentPadding));

        var controlPanelLayout = controlPanel.GetComponent<VerticalLayoutGroup>();
        if (controlPanelLayout == null) controlPanelLayout = controlPanel.AddComponent<VerticalLayoutGroup>();
        controlPanelLayout.spacing = 16f;
        controlPanelLayout.childAlignment = TextAnchor.UpperCenter;
        controlPanelLayout.childControlWidth = true;
        controlPanelLayout.childControlHeight = false;
        controlPanelLayout.childForceExpandWidth = true;
        controlPanelLayout.childForceExpandHeight = false;
        controlPanelLayout.padding = new RectOffset(8, 8, 8, 8);

        var controlPanelFitter = controlPanel.GetComponent<LayoutElement>();
        if (controlPanelFitter == null) controlPanelFitter = controlPanel.AddComponent<LayoutElement>();
        controlPanelFitter.minWidth = ControlPanelMinWidth;
        controlPanelFitter.preferredWidth = ControlPanelMaxWidth;

        // Existing elements reparented into the control panel's vertical
        // stack, in priority order (Suyu Başlat is the primary action - see
        // class doc "Primary button hierarchy"). ResultPanel (from
        // StatusArea) doubles as the "concise educational objective/hint
        // area" slot - its existing text is already driven by
        // UIManager2D.HandleStageTextChanged/ReadyMessage/SuccessMessage/
        // FailMessage/OrientationMismatchMessage, so no new text element is
        // needed for that requirement.
        GameObject resultPanel = GameObject.Find("ResultPanel");
        GameObject startWaterButtonGO = GameObject.Find("StartWaterButton");
        GameObject reloadButtonGO = GameObject.Find("ReloadButton");

        ReparentIntoControlPanel(resultPanel, controlPanel.transform, 0, new Vector2(0f, 100f));
        ReparentIntoControlPanel(startWaterButtonGO, controlPanel.transform, 1, new Vector2(0f, 110f));
        ReparentIntoControlPanel(reloadButtonGO, controlPanel.transform, 2, new Vector2(0f, 80f));

        modified.Add("ControlPanel (new right-side VerticalLayoutGroup: ResultPanel/hint, StartWaterButton, ReloadButton reparented in)");

        // ---------------- Landscape-blocked overlay ----------------
        GameObject overlayRoot = FindOrCreateChild(safeAreaRoot.transform, "LandscapeRequirementOverlay");
        StretchFill(RectOf(overlayRoot), 0f);
        overlayRoot.transform.SetAsLastSibling();

        GameObject overlayBlocker = FindOrCreateChild(overlayRoot.transform, "OverlayBlocker");
        StretchFill(RectOf(overlayBlocker), 0f);
        EnsureImage(overlayBlocker, new Color(0.06f, 0.08f, 0.10f, 0.92f), true);

        GameObject overlayTextGO = FindOrCreateChild(overlayRoot.transform, "OverlayText");
        SetAnchoredRect(RectOf(overlayTextGO), Center, Center, Center, Vector2.zero, new Vector2(900f, 200f));
        Text overlayText = EnsureText(overlayTextGO, "En iyi deneyim için ekranı yatay kullanın.");

        LandscapeRequirementOverlay2D overlay = overlayRoot.GetComponent<LandscapeRequirementOverlay2D>();
        if (overlay == null) overlay = overlayRoot.AddComponent<LandscapeRequirementOverlay2D>();

        var overlaySO = new SerializedObject(overlay);
        overlaySO.FindProperty("viewportState").objectReferenceValue = viewportState;
        overlaySO.FindProperty("root").objectReferenceValue = overlayRoot;
        overlaySO.FindProperty("fullscreenController").objectReferenceValue = fullscreenController;
        overlaySO.ApplyModifiedPropertiesWithoutUndo();
        overlayRoot.SetActive(false); // Refresh() at runtime immediately corrects this - starts hidden so it never flashes visible on a valid landscape load.

        modified.Add("LandscapeRequirementOverlay (new, topmost, wired to WebViewportState2D)");

        // ---------------- GameplayBoardFitter2D wiring ----------------
        var fitterSO = new SerializedObject(boardFitter);
        fitterSO.FindProperty("targetCamera").objectReferenceValue = camera;
        fitterSO.FindProperty("boardFitContainer").objectReferenceValue = boardFitContainer;
        fitterSO.FindProperty("viewportState").objectReferenceValue = viewportState;
        LevelManager2D levelManagerComponent = gameRoot2D != null ? gameRoot2D.GetComponentInChildren<LevelManager2D>() : null;
        fitterSO.FindProperty("levelManager").objectReferenceValue = levelManagerComponent;
        // boardAreaMin/Max mirror BoardViewportArea's own anchor fractions
        // above, converted to camera-viewport space (same 0..1 convention).
        fitterSO.FindProperty("boardAreaMin").vector2Value = new Vector2(0.02f, 0.05f);
        fitterSO.FindProperty("boardAreaMax").vector2Value = new Vector2(BoardAreaWidthFraction - 0.01f, 1f - (TopBarHeight / 1080f) - 0.02f);
        fitterSO.ApplyModifiedPropertiesWithoutUndo();

        modified.Add("GameplayBoardFitter2D (on BoardFitContainer, wired to Main Camera/WebViewportState2D/LevelManager2D)");

        // ---------------- UIManager2D wiring (new fields only - existing button listeners untouched, UIManager2D.Awake still owns those) ----------------
        UIManager2D uiManager = Object.FindFirstObjectByType<UIManager2D>();
        if (uiManager != null)
        {
            var uiSO = new SerializedObject(uiManager);
            SerializedProperty fullscreenButtonProp = uiSO.FindProperty("fullscreenButton");
            if (fullscreenButtonProp != null) fullscreenButtonProp.objectReferenceValue = fullscreenButtonGO.GetComponent<Button>();
            SerializedProperty fullscreenControllerProp = uiSO.FindProperty("fullscreenController");
            if (fullscreenControllerProp != null) fullscreenControllerProp.objectReferenceValue = fullscreenController;
            uiSO.ApplyModifiedPropertiesWithoutUndo();
            modified.Add("UIManager2D (fullscreenButton/fullscreenController wired)");
        }

        // A direct top-bar Back button that skips the pause menu entirely -
        // UIManager2D doesn't currently expose a field for this, so it's
        // wired to the SAME method the pause menu's own return button already
        // uses via a lightweight forwarding component instead of adding a new
        // serialized field to a script this builder shouldn't need to modify
        // just for one more button reference.
        GameSceneBackButtonForwarder2D forwarder = backButtonGO.GetComponent<GameSceneBackButtonForwarder2D>();
        if (forwarder == null) forwarder = backButtonGO.AddComponent<GameSceneBackButtonForwarder2D>();
        Button backButton = backButtonGO.GetComponent<Button>();
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(forwarder.GoToLevelSelect);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        WebBuildConfig2D.EnsureSceneOrder(logDetails);

        if (logDetails)
        {
            Debug.Log("GameSceneWebLayoutBuilder2D: Web gameplay layout build complete.\n" + string.Join("\n", modified.Select(m => "  - " + m)));
        }

        return true;
    }

    /// <summary>
    /// The one structural hierarchy change this phase makes to GameScene2D:
    /// creates "BoardFitContainer" (a new root GameObject at world origin)
    /// and reparents the EXISTING "BoardManager2D" GameObject (currently a
    /// child of GameRoot2D) and the EXISTING "BoardRoot" GameObject (currently
    /// its own scene root) underneath it, both with worldPositionStays=true
    /// so nothing visually jumps - both were already at world (0,0,0)/scale 1,
    /// so their new localPosition/localScale relative to BoardFitContainer end
    /// up (0,0,0)/(1,1,1) exactly. Every other GameRoot2D child (LevelManager2D,
    /// FlowSolver2D, ScoreManager2D, WaterFlowAnimator2D) is untouched - only
    /// BoardManager2D's OWN Transform moves, and only because GridToWorld()
    /// reads it (see class doc for the full reasoning). Idempotent - if
    /// BoardFitContainer already exists (a prior run), reuses it and only
    /// re-parents anything that isn't already under it.
    /// </summary>
    private static Transform ReparentBoardUnderFitContainer(System.Collections.Generic.List<string> modified)
    {
        GameObject gameRoot2D = GameObject.Find("GameRoot2D");
        Transform boardManagerTransform = gameRoot2D != null ? gameRoot2D.transform.Find("BoardManager2D") : null;
        GameObject boardRootGO = GameObject.Find("BoardRoot");

        if (boardManagerTransform == null || boardRootGO == null)
        {
            return null;
        }

        GameObject fitContainerGO = GameObject.Find("BoardFitContainer");
        bool isNew = fitContainerGO == null;
        if (isNew)
        {
            fitContainerGO = new GameObject("BoardFitContainer");
            fitContainerGO.transform.position = Vector3.zero;
            fitContainerGO.transform.localScale = Vector3.one;
        }

        bool reparented = false;
        if (boardManagerTransform.parent != fitContainerGO.transform)
        {
            boardManagerTransform.SetParent(fitContainerGO.transform, worldPositionStays: true);
            reparented = true;
        }
        if (boardRootGO.transform.parent != fitContainerGO.transform)
        {
            boardRootGO.transform.SetParent(fitContainerGO.transform, worldPositionStays: true);
            reparented = true;
        }

        if (isNew || reparented)
        {
            modified.Add("BoardFitContainer (new wrapper - now the shared parent of BoardManager2D and BoardRoot, see GameplayBoardFitter2D)");
        }

        return fitContainerGO.transform;
    }

    private static void ReparentIntoControlPanel(GameObject go, Transform controlPanel, int siblingIndex, Vector2 minSize)
    {
        if (go == null) return;

        go.transform.SetParent(controlPanel, false);
        go.transform.SetSiblingIndex(siblingIndex);

        var layoutElement = go.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.minHeight = minSize.y;
        layoutElement.preferredHeight = minSize.y;

        RectTransform rt = RectOf(go);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, minSize.y);
    }

    /// <summary>Idempotent find-or-create button setup using the shared UI kit (Buttons/brown.png(+_inlay/_pressed)) - same visual convention as every other button in this project (MainMenuSceneBuilder2D/InGameMenuPrefabBuilder/etc.), so new top-bar buttons don't look like unstyled placeholders next to the existing HUD chrome.</summary>
    private static Button EnsureButtonComponent(GameObject go, string label)
    {
        Sprite buttonBase = LoadFirstSprite("Assets/Art2D/FinalSprites/UI/Buttons/brown.png");
        Sprite buttonInlay = LoadFirstSprite("Assets/Art2D/FinalSprites/UI/Buttons/brown_inlay.png");
        Sprite buttonPressed = LoadFirstSprite("Assets/Art2D/FinalSprites/UI/Buttons/brown_pressed.png");

        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = buttonBase;
        image.type = buttonBase != null && buttonBase.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        button.targetGraphic = image;
        if (buttonPressed != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.pressedSprite = buttonPressed;
            state.highlightedSprite = buttonBase;
            state.selectedSprite = buttonBase;
            state.disabledSprite = buttonBase;
            button.spriteState = state;
        }

        if (buttonInlay != null)
        {
            GameObject inset = FindOrCreateChild(go.transform, "ButtonInset");
            StretchFill(RectOf(inset), 6f);
            EnsureImage(inset, Color.white, false).sprite = buttonInlay;
            inset.transform.SetAsFirstSibling();
        }

        GameObject labelGO = FindOrCreateChild(go.transform, "Label");
        StretchFill(RectOf(labelGO), 4f);
        Text labelText = EnsureText(labelGO, label);
        labelText.fontSize = 26;
        labelText.horizontalOverflow = HorizontalWrapMode.Wrap;

        return button;
    }

    private static Sprite LoadFirstSprite(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static Image EnsureImage(GameObject go, Color color, bool raycastTarget)
    {
        var image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static Text EnsureText(GameObject go, string content)
    {
        var text = go.GetComponent<Text>();
        if (text == null) text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = 44;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/SHPinscher-Regular11/SHPinscher-Regular.otf");
        if (font != null) text.font = font;
        return text;
    }

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    private static RectTransform RectOf(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        return rt;
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetAnchoredRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    private static void StretchFill(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = Center;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
