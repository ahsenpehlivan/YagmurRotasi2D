using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.Gameplay2D;
using YagmurRotasi2D.UI2D;
using YagmurRotasi2D.Visual2D;

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
///
/// Phase 9C fix: ReparentBoardUnderFitContainer used to look up
/// BoardManager2D ONLY as a direct child of GameRoot2D
/// (gameRoot2D.transform.Find("BoardManager2D")) - correct on the very
/// first run, but BROKEN on every run after that, since the first run
/// already moves BoardManager2D out from under GameRoot2D and into
/// BoardFitContainer. Every rerun since then has been silently aborting at
/// this exact step (Debug.LogError + return false) before it ever reached
/// the Canvas/TopHUD/ControlPanel/DevPerformanceHud code below - almost
/// certainly why the live scene had drifted out of sync with this script.
/// Fixed by using a scene-wide GameObject.Find (same as boardRootGO already
/// correctly did) instead of a parent-scoped one.
/// </summary>
public static class GameSceneWebLayoutBuilder2D
{
    private const string ScenePath = "Assets/Scenes/GameScene2D.unity";

    // internal (not private): WebLayoutValidator2D's static analysis reads
    // these directly to replicate MainGameplayArea/BoardViewport/ControlPanel's
    // real layout math without touching Screen.width/height - keeping one
    // source of truth avoids the two ever drifting out of sync.
    //
    // Phase 9D: tightened from the previous pass (TopBarHeight 84->68,
    // ContentPadding 40->24, MainAreaSpacing 28->20, ControlPanelPreferredWidth
    // 360->320) purely to hand a few more percent of the screen to
    // BoardViewport - gameplay readability takes priority over chrome
    // breathing room. ControlPanelMinWidth stays 280 (Ayarlar/Sıfırla/Suyu
    // Başlat still need room to render without wrapping).
    internal const float TopBarHeight = 68f;
    internal const float ContentPadding = 24f;
    internal const float MainAreaSpacing = 20f;
    internal const float ControlPanelMinWidth = 280f;
    internal const float ControlPanelPreferredWidth = 320f;

    // The REAL, pre-existing full-viewport gameplay background is the
    // "Background2D" prefab instance (Assets/Prefabs2D/Background2D.prefab) -
    // already present at scene root using background2.png, just with a
    // static hand-tuned m_LocalScale that doesn't adapt to wider browser
    // aspect ratios (the actual root cause of the light-blue side gaps - see
    // EnsureResponsiveBackground's own doc comment for the full audit
    // trail). A PREVIOUS pass of this file incorrectly created a second,
    // different object ("GameplayBackground", using background.png) instead
    // of finding this one - EnsureResponsiveBackground below both repairs
    // the real Background2D and removes that incorrect duplicate.
    private const string LegacyIncorrectBackgroundName = "GameplayBackground";

    // Phase 9G: TransparentTopSpacer is a genuinely empty, see-through area
    // (no background/badge/text/stars - see BuildTransparentTopSpacer) -
    // InfoCardHeight only preserves its footprint so ControlPanel's overall
    // proportions don't shift. The dark background it used to carry now
    // lives on ControlsCard instead (see BuildControlsCard).
    private const float InfoCardHeight = 280f;
    private const float ResultPanelHeight = 96f; // FeedbackMessage - fixed reserved height so an error message never pushes buttons outside the panel
    private const float StartWaterButtonHeight = 104f; // priority 1 - visually the largest control
    private const float ResetButtonHeight = 72f;        // priority 2
    private const float SettingsButtonHeight = 64f;     // priority 3

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

        ApplyManualBoardTransformCorrection(boardFitContainer, modified);
        EnsureIndependentWorldFXRoot(modified);

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

        // Full-viewport cover background - needs both the camera and
        // viewportState resolved above, so it happens here rather than
        // right after EnsureIndependentWorldFXRoot (which ran earlier,
        // before either was available).
        EnsureResponsiveBackground(camera, viewportState, modified);

        GameObject gameRoot2D = GameObject.Find("GameRoot2D");
        PipeHoverCoordinator2D hoverCoordinator = null;
        if (gameRoot2D != null)
        {
            hoverCoordinator = gameRoot2D.GetComponent<PipeHoverCoordinator2D>();
            if (hoverCoordinator == null) hoverCoordinator = gameRoot2D.AddComponent<PipeHoverCoordinator2D>();
        }

        GameplayBoardFitter2D boardFitter = boardFitContainer.gameObject.GetComponent<GameplayBoardFitter2D>();
        if (boardFitter == null) boardFitter = boardFitContainer.gameObject.AddComponent<GameplayBoardFitter2D>();

        LevelManager2D levelManagerComponent = gameRoot2D != null ? gameRoot2D.GetComponentInChildren<LevelManager2D>() : null;

        // Part A: rebuilt as a real UGUI panel (hidden by default, F3
        // toggle, raycastTarget false everywhere) - see BuildDevPerformanceHud
        // and DevPerformanceHud2D's own class doc for why the old always-on
        // OnGUI overlay was the confirmed root cause of "FPS panel covers
        // gameplay elements".
        BuildDevPerformanceHud(canvasGO, viewportState, levelManagerComponent, modified);

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

        // ---------------- Main gameplay area (Part C/D): a real HorizontalLayoutGroup below TopHUD ----------------
        // Replaces the old BoardViewportArea/ControlPanel pair (two
        // INDEPENDENTLY anchored siblings, both hand-tuned to the same
        // BoardAreaWidthFraction constant with zero enforcement tying them
        // together) with one real layout container. This is also what makes
        // ControlPanel's LayoutElement min/preferredWidth actually take
        // effect - it was inert before since nothing was a parent
        // LayoutGroup consumer of it (Part B audit finding).
        GameObject mainGameplayArea = FindOrCreateChild(safeAreaRoot.transform, "MainGameplayArea");
        RectTransform mainAreaRt = RectOf(mainGameplayArea);
        mainAreaRt.anchorMin = Vector2.zero;
        mainAreaRt.anchorMax = Vector2.one;
        mainAreaRt.pivot = Center;
        mainAreaRt.offsetMin = new Vector2(ContentPadding, ContentPadding);
        mainAreaRt.offsetMax = new Vector2(-ContentPadding, -(TopBarHeight + ContentPadding));

        var mainAreaLayout = mainGameplayArea.GetComponent<HorizontalLayoutGroup>();
        if (mainAreaLayout == null) mainAreaLayout = mainGameplayArea.AddComponent<HorizontalLayoutGroup>();
        mainAreaLayout.spacing = MainAreaSpacing;
        mainAreaLayout.childAlignment = TextAnchor.MiddleCenter;
        mainAreaLayout.childControlWidth = true;
        mainAreaLayout.childControlHeight = true;
        mainAreaLayout.childForceExpandWidth = false;
        mainAreaLayout.childForceExpandHeight = true;
        mainAreaLayout.padding = new RectOffset(0, 0, 0, 0);

        modified.Add("MainGameplayArea (new HorizontalLayoutGroup below TopHUD, replaces two independently-anchored siblings)");

        // ---------------- Board viewport (visual placeholder only - the actual board is world-space; GameplayBoardFitter2D now reads THIS RectTransform's real screen rect every recompute, see Part E) ----------------
        GameObject boardViewport = FindOrCreateChild(mainGameplayArea.transform, "BoardViewport");
        boardViewport.transform.SetSiblingIndex(0);
        RectTransform boardViewportRt = RectOf(boardViewport);
        var boardViewportImage = boardViewport.GetComponent<Image>();
        if (boardViewportImage != null) Object.DestroyImmediate(boardViewportImage); // purely a layout marker, never rendered

        var boardViewportLayoutElement = boardViewport.GetComponent<LayoutElement>();
        if (boardViewportLayoutElement == null) boardViewportLayoutElement = boardViewport.AddComponent<LayoutElement>();
        boardViewportLayoutElement.flexibleWidth = 1f; // fills whatever width ControlPanel doesn't claim
        boardViewportLayoutElement.minWidth = 320f;

        // ---------------- Control panel (right side) ----------------
        GameObject controlPanel = FindOrCreateChild(mainGameplayArea.transform, "ControlPanel");
        controlPanel.transform.SetSiblingIndex(1);

        // Phase 9G fix: ControlPanel ROOT must never carry a visible
        // background - it used to own the near-opaque dark Image directly,
        // which sat BEHIND the now-empty TransparentTopSpacer too, so that
        // "transparent" 280px area still rendered as a solid dark block.
        // Idempotent cleanup: destroys a stale Image left by any previous
        // run instead of just leaving it there unused.
        var staleControlPanelBg = controlPanel.GetComponent<Image>();
        if (staleControlPanelBg != null) Object.DestroyImmediate(staleControlPanelBg);

        var controlPanelFitter = controlPanel.GetComponent<LayoutElement>();
        if (controlPanelFitter == null) controlPanelFitter = controlPanel.AddComponent<LayoutElement>();
        controlPanelFitter.minWidth = ControlPanelMinWidth;
        controlPanelFitter.preferredWidth = ControlPanelPreferredWidth;
        controlPanelFitter.flexibleWidth = 0f; // never grows past preferredWidth even with leftover space - keeps ControlPanel inside the required 280-420 band

        var controlPanelLayout = controlPanel.GetComponent<VerticalLayoutGroup>();
        if (controlPanelLayout == null) controlPanelLayout = controlPanel.AddComponent<VerticalLayoutGroup>();
        controlPanelLayout.spacing = 14f;
        controlPanelLayout.childAlignment = TextAnchor.UpperCenter;
        controlPanelLayout.childControlWidth = true;
        controlPanelLayout.childControlHeight = true; // FIX (Part B audit): was false, which silently made every child LayoutElement's min/preferredHeight inert - heights only ever happened to look right because ReparentIntoControlPanel also hand-set sizeDelta.y directly, a second, conflicting source of truth for the same value
        controlPanelLayout.childForceExpandWidth = true;
        controlPanelLayout.childForceExpandHeight = false;
        controlPanelLayout.padding = new RectOffset(18, 18, 18, 18);

        // Existing elements reparented into ControlsCard's vertical stack
        // (not directly into ControlPanel anymore - see BuildControlsCard),
        // in priority order (Suyu Başlat is the primary action - see class
        // doc "Primary button hierarchy"). ResultPanel doubles as the
        // FeedbackMessage slot - its existing text is already driven by
        // UIManager2D.HandleStageTextChanged/ReadyMessage/SuccessMessage/
        // FailMessage/OrientationMismatchMessage, so no new text element is
        // needed for that requirement.
        GameObject resultPanel = GameObject.Find("ResultPanel");
        GameObject startWaterButtonGO = GameObject.Find("StartWaterButton");
        GameObject reloadButtonGO = GameObject.Find("ReloadButton");

        // Relabeled to match the required mockup exactly - StartWaterButton's
        // own text was already correct, ReloadButton's said "Yeniden Dene".
        SetTextIfFound("StartWaterButtonText", "Suyu Başlat");
        SetTextIfFound("ReloadButtonText", "Sıfırla");

        GameObject transparentTopSpacer = BuildTransparentTopSpacer(controlPanel.transform);
        GameObject controlsCard = BuildControlsCard(controlPanel.transform);

        // Scene-wide Find (not scoped to controlsCard) - a previous run
        // created SettingsButton directly under ControlPanel; finding it by
        // name first and letting ReparentIntoControlPanel below move it
        // (SetParent, not just SetSiblingIndex) avoids creating a duplicate
        // while the old one is left orphaned under ControlPanel.
        GameObject settingsButtonGO = GameObject.Find("SettingsButton");
        if (settingsButtonGO == null) settingsButtonGO = FindOrCreateChild(controlsCard.transform, "SettingsButton");
        Button settingsButton = EnsureButtonComponent(settingsButtonGO, "Ayarlar");

        transparentTopSpacer.transform.SetSiblingIndex(0);
        controlsCard.transform.SetSiblingIndex(1);
        ReparentIntoControlPanel(resultPanel, controlsCard.transform, 0, ResultPanelHeight, ResultPanelHeight);
        ReparentIntoControlPanel(startWaterButtonGO, controlsCard.transform, 1, StartWaterButtonHeight, StartWaterButtonHeight);
        ReparentIntoControlPanel(reloadButtonGO, controlsCard.transform, 2, ResetButtonHeight, ResetButtonHeight);
        ReparentIntoControlPanel(settingsButtonGO, controlsCard.transform, 3, SettingsButtonHeight, SettingsButtonHeight);

        modified.Add($"ControlPanel (Phase 9G: background moved off ControlPanel root onto new ControlsCard - TransparentTopSpacer ({InfoCardHeight}px) is now genuinely see-through; FeedbackMessage, StartWaterButton, ResetButton, SettingsButton reparented into ControlsCard, unchanged priority order)");

        // ---------------- Part G required sibling order fix ----------------
        // Part B audit finding: SafeAreaRoot's actual m_Children order had
        // TopHUD/BoardViewportArea/ControlPanel (normal gameplay) sitting
        // AFTER SuccessPanelHost/InGameMenuHost - meaning the gameplay HUD
        // rendered ON TOP of the success/pause modals, and since
        // ControlPanel's buttons have raycastTarget=true, they could
        // intercept clicks meant for a modal wherever they visually
        // overlapped it. Forced back to the required order here: normal
        // gameplay first, then the pause/success panels, then the landscape
        // overlay (already forced last below) - DevPerformanceHud is parented
        // outside SafeAreaRoot entirely (see BuildDevPerformanceHud) so it is
        // always topmost regardless of this ordering.
        topHud.transform.SetAsFirstSibling();
        mainGameplayArea.transform.SetSiblingIndex(1);
        modified.Add("SafeAreaRoot sibling order fixed (Part G): normal gameplay (TopHUD, MainGameplayArea) now renders BEFORE SuccessPanelHost/InGameMenuHost instead of on top of them");

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
        fitterSO.FindProperty("levelManager").objectReferenceValue = levelManagerComponent;
        // Part E: reads BoardViewport's real screen rect every recompute
        // instead of a hand-tuned camera-viewport fraction pair - see
        // GameplayBoardFitter2D's own class doc for the full reasoning.
        fitterSO.FindProperty("boardViewport").objectReferenceValue = boardViewportRt;
        fitterSO.ApplyModifiedPropertiesWithoutUndo();

        modified.Add("GameplayBoardFitter2D (on BoardFitContainer, wired to Main Camera/WebViewportState2D/LevelManager2D/BoardViewport)");

        // ---------------- UIManager2D wiring (new fields only - existing button listeners untouched, UIManager2D.Awake still owns those) ----------------
        UIManager2D uiManager = Object.FindFirstObjectByType<UIManager2D>();
        if (uiManager != null)
        {
            var uiSO = new SerializedObject(uiManager);
            SerializedProperty fullscreenButtonProp = uiSO.FindProperty("fullscreenButton");
            if (fullscreenButtonProp != null) fullscreenButtonProp.objectReferenceValue = fullscreenButtonGO.GetComponent<Button>();
            SerializedProperty fullscreenControllerProp = uiSO.FindProperty("fullscreenController");
            if (fullscreenControllerProp != null) fullscreenControllerProp.objectReferenceValue = fullscreenController;

            SerializedProperty settingsButtonProp = uiSO.FindProperty("settingsButton");
            if (settingsButtonProp != null) settingsButtonProp.objectReferenceValue = settingsButton;

            // Level-info repair: controlPanelLevelText/controlPanelGridSizeText/
            // boardManagerForDisplay are NOT dead fields - "ControlPanel/
            // LevelDetails" (LevelNameLine + GridSizeLine) is a live, visible,
            // direct child of the real ControlPanel (sibling to StatsArea,
            // TransparentTopSpacer and ControlsCard - a completely different
            // subtree from TransparentTopSpacer, so BuildTransparentTopSpacer's
            // cleanup never touches it) that every previous builder run wrongly
            // nulled out here on the same mistaken "no live scene object to
            // wire" assumption StatsArea was already fixed from - this is the
            // confirmed root cause of the lower-right level label being stuck
            // on its authored placeholder text ("Bölüm 1"/"5×5") forever,
            // since UIManager2D.HandleLevelLoaded's `if (controlPanelLevelText
            // != null)` guard always skipped the real update. Repairs the
            // reference instead of recreating or repositioning anything.
            WireControlPanelLevelDetails(controlPanel.transform, uiSO, modified);

            // Move/star scoring repair: controlPanelStatsText/starImages are
            // NOT dead fields - "ControlPanel/StatsArea" (MoveCountLine +
            // StarRow/Star_0/Star_1/Star_2) is a live, visible, direct child
            // of the real ControlPanel (a completely different subtree from
            // TransparentTopSpacer, so BuildTransparentTopSpacer's cleanup
            // above never touches it) that a previous builder run wrongly
            // nulled out here on the mistaken assumption it no longer
            // existed - this is the confirmed root cause of the lower-right
            // move counter/star icons never updating. Repairs the reference
            // instead of recreating or repositioning anything.
            WireControlPanelStatsAndStars(controlPanel.transform, uiSO, modified);

            uiSO.ApplyModifiedPropertiesWithoutUndo();
            modified.Add("UIManager2D (fullscreenButton/fullscreenController/settingsButton wired; TransparentTopSpacer-related text fields cleared)");
        }

        // A direct top-bar Back button that skips the pause menu entirely -
        // UIManager2D doesn't currently expose a field for this, so it calls
        // the SAME method the pause menu's own return button already uses
        // (UIManager2D.ReturnToLevelSelect) via a lightweight forwarding
        // component instead of adding a new serialized field to a script
        // this builder shouldn't need to modify just for one more button
        // reference. GameSceneBackButtonForwarder2D wires its own
        // Button.onClick listener itself, in its own Awake() (idempotent,
        // same pattern as UIButtonSound2D) - a plain runtime
        // Button.onClick.AddListener() call made here from Editor tooling
        // would never actually be saved into the scene's serialized
        // Button.m_OnClick.m_PersistentCalls (confirmed the root cause of
        // the button previously doing nothing - the click sound still
        // played since UIButtonSound2D already self-wires the same way),
        // so this builder only needs to ensure the component itself exists.
        GameSceneBackButtonForwarder2D forwarder = backButtonGO.GetComponent<GameSceneBackButtonForwarder2D>();
        if (forwarder == null) forwarder = backButtonGO.AddComponent<GameSceneBackButtonForwarder2D>();
        modified.Add("BackButton (GameSceneBackButtonForwarder2D ensured - it self-wires its own onClick listener at runtime, this builder no longer touches Button.onClick directly)");

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
        // Scene-wide Find (not gameRoot2D.transform.Find) - this must keep
        // working after BoardManager2D has already been moved out from under
        // GameRoot2D and into BoardFitContainer by a prior run. See the
        // class doc "Phase 9C fix" note for why the old parent-scoped lookup
        // broke every rerun.
        GameObject boardManagerGO = GameObject.Find("BoardManager2D");
        Transform boardManagerTransform = boardManagerGO != null ? boardManagerGO.transform : null;
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

    // Manually verified in the Unity Editor (Phase 9H) - authoritative, not
    // derived from any formula. Applied as ABSOLUTE assignments only (never
    // *=), so rerunning this builder always converges on exactly these
    // numbers - never 0.49/0.343 compounding. BoardContainerVisualScale is
    // built from BoardManager2D.VisualPackingScale (not a separate 0.7f
    // literal) so the container scale and GridToWorld's spacing (Phase 9I
    // fix - the gap-between-cells root cause) can never drift apart again.
    private static readonly Vector3 BoardRootLocalPosition = new Vector3(-0.270000011f, -0.0799999982f, 0f);
    private static readonly Vector3 BoardContainerVisualScale = new Vector3(BoardManager2D.VisualPackingScale, BoardManager2D.VisualPackingScale, 1f);

    /// <summary>
    /// Sets BoardRoot's local offset under BoardFitContainer and GridCells/
    /// Pipes/SourceTarget's own container scale to the manually-verified
    /// values. Explicitly does NOT touch BoardFitContainer or BoardManager2D
    /// (GameplayBoardFitter2D owns BoardFitContainer's scale/position
    /// exclusively; BoardManager2D's transform must stay in lockstep with
    /// BoardRoot's SIBLING position under BoardFitContainer for GridToWorld
    /// to remain correct, so it is left at whatever the reparent step above
    /// already gave it - untouched here). Never references CloudAndRain,
    /// SuccessFXZone, or anything under them - by construction, this method
    /// cannot write to those objects.
    /// </summary>
    private static void ApplyManualBoardTransformCorrection(Transform boardFitContainer, System.Collections.Generic.List<string> modified)
    {
        Transform boardRoot = boardFitContainer.Find("BoardRoot");
        if (boardRoot == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'BoardRoot' not found under BoardFitContainer - skipping the manual transform correction.");
            return;
        }

        boardRoot.localPosition = BoardRootLocalPosition;

        Transform gridCells = boardRoot.Find("GridCells");
        Transform pipes = boardRoot.Find("Pipes");
        Transform sourceTarget = boardRoot.Find("SourceTarget");

        if (gridCells != null) gridCells.localScale = BoardContainerVisualScale;
        if (pipes != null) pipes.localScale = BoardContainerVisualScale;
        if (sourceTarget != null) sourceTarget.localScale = BoardContainerVisualScale;

        modified.Add($"BoardRoot/GridCells/Pipes/SourceTarget (Phase 9H: manually-verified transform correction reapplied as absolute values - BoardRoot.localPosition={BoardRootLocalPosition}, containers.localScale={BoardContainerVisualScale}; CloudAndRain/SuccessFXZone untouched)");
    }

    /// <summary>
    /// Phase 9L root-cause fix: SuccessFXZone and CloudAndRain used to be
    /// descendants of BoardRoot (itself under BoardFitContainer) - even
    /// though nothing directly WROTE their own local transforms, they still
    /// INHERITED every runtime position/scale change GameplayBoardFitter2D
    /// and ApplyManualBoardTransformCorrection apply to BoardFitContainer/
    /// BoardRoot, which is why they visibly moved/resized in Play Mode
    /// despite no visible "culprit" line writing to them directly (the exact
    /// bug the Phase 9J/9K enforcement was chasing symptoms of, rather than
    /// fixing the actual cause).
    ///
    /// Moving them to a brand-new scene-level root ("IndependentWorldFXRoot",
    /// a sibling of BoardFitContainer, always kept at identity) removes them
    /// from that inheritance chain entirely - GameplayBoardFitter2D only ever
    /// touches BoardFitContainer, never this root, so nothing can affect
    /// these two again short of a script explicitly writing to them (nothing
    /// does - ApplyManualBoardTransformCorrection only ever touches BoardRoot/
    /// GridCells/Pipes/SourceTarget). This makes the old Phase 9J/9K
    /// CloudAndRain position/scale enforcement (formerly
    /// ApplyAuthoredWeatherTransform, removed here) unnecessary - the saved
    /// Edit Mode transform is authoritative on its own structural merits now,
    /// with nothing left that could ever overwrite it.
    ///
    /// Idempotent: SuccessFXZone/CloudAndRain are found scene-wide (not
    /// scoped to BoardRoot), so this is safe to call whether they are still
    /// under the old BoardRoot (first run) or already under
    /// IndependentWorldFXRoot (any later run) - SetParent is only called when
    /// not already correctly parented, and always with
    /// worldPositionStays=true so their CURRENT saved world position/
    /// rotation/scale is preserved exactly, never replaced with a hardcoded
    /// number. Moves SuccessFXZone as a single unit - DuckFXRoot/FlowerFXRoot
    /// and their animation children are never individually touched, they
    /// simply come along as SuccessFXZone's existing children.
    /// </summary>
    private static void EnsureIndependentWorldFXRoot(System.Collections.Generic.List<string> modified)
    {
        GameObject rootGO = GameObject.Find("IndependentWorldFXRoot");
        bool isNewRoot = rootGO == null;
        if (isNewRoot)
        {
            rootGO = new GameObject("IndependentWorldFXRoot");
        }

        // A scene-level root, always identity - a structural invariant of
        // this container itself (unlike CloudAndRain's authored values, this
        // one has no meaningful "authored" transform to preserve), safe to
        // assert unconditionally every run.
        if (rootGO.transform.parent != null)
        {
            rootGO.transform.SetParent(null, worldPositionStays: false);
        }
        rootGO.transform.localPosition = Vector3.zero;
        rootGO.transform.localRotation = Quaternion.identity;
        rootGO.transform.localScale = Vector3.one;

        bool anyReparented = isNewRoot;

        GameObject successFXZoneGO = GameObject.Find("SuccessFXZone");
        if (successFXZoneGO == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'SuccessFXZone' not found anywhere in the scene - cannot move it under IndependentWorldFXRoot.");
        }
        else if (successFXZoneGO.transform.parent != rootGO.transform)
        {
            successFXZoneGO.transform.SetParent(rootGO.transform, worldPositionStays: true);
            anyReparented = true;
        }

        GameObject cloudAndRainGO = GameObject.Find("CloudAndRain");
        if (cloudAndRainGO == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'CloudAndRain' not found anywhere in the scene - cannot move it under IndependentWorldFXRoot.");
        }
        else if (cloudAndRainGO.transform.parent != rootGO.transform)
        {
            cloudAndRainGO.transform.SetParent(rootGO.transform, worldPositionStays: true);
            anyReparented = true;
        }

        if (anyReparented)
        {
            modified.Add("IndependentWorldFXRoot (Phase 9L: new scene-level root at identity, sibling of BoardFitContainer - now the parent of SuccessFXZone and CloudAndRain, reparented with worldPositionStays=true so their visible world transform is unchanged; no longer descendants of BoardRoot/BoardFitContainer, so GameplayBoardFitter2D can never affect them again, and no runtime/builder transform enforcement is needed for them anymore)");
        }
    }

    /// <summary>
    /// Root-cause fix for the light-blue side bars visible at wide browser
    /// aspect ratios (corrected version - see class doc history): the FIRST
    /// audit wrongly concluded GameScene2D had no background object at all,
    /// because the real one is a PREFAB INSTANCE ("Background2D",
    /// Assets/Prefabs2D/Background2D.prefab) whose sprite is assigned via a
    /// per-INSTANCE override in the scene file (m_Modifications), not a
    /// direct m_Sprite value a plain scene-text grep would show next to an
    /// obviously-named object - it was missed, not absent. Deeper audit
    /// found:
    ///   - Background2D.prefab's own DEFAULT sprite reference was an
    ///     orphaned GUID (225a83ccb1fbcf04f97c30675440673f) - background.png
    ///     was re-imported at some point after the project's initial commit
    ///     and got assigned a brand-new GUID, silently breaking the
    ///     prefab's default (now fixed directly in the prefab asset, see
    ///     Background2D.prefab's own m_Sprite - it now points at
    ///     background2.png, matching what the live scene instance already
    ///     used).
    ///   - The scene's Background2D INSTANCE already carried its own
    ///     working override pointing at background2.png (guid
    ///     a2b23d46378c8cb48a4ef9823644b865) - this is the exact rainy
    ///     Tepebaşı artwork already visible in the browser.
    ///   - Its actual bug: a static, hand-tuned m_LocalScale
    ///     (1.3081671, 1.3081671) that never adapts to a wider-than-authored
    ///     browser aspect ratio - the real cause of the light-blue side
    ///     gaps. Never a missing image.
    ///
    /// A PREVIOUS pass of this file misdiagnosed this and created a second,
    /// DIFFERENT "GameplayBackground" object (using background.png, a
    /// different asset) instead of finding/fixing this real one - this
    /// method now removes that incorrect duplicate (RemoveLegacyIncorrectBackground)
    /// and instead attaches ResponsiveBackgroundCover2D to the REAL,
    /// pre-existing Background2D SpriteRenderer, replacing its static scale
    /// with the same aspect-preserving cover-fit math, recomputed on every
    /// WebViewportState2D viewport change. Background2D is already parented
    /// at scene root (never a descendant of BoardFitContainer), which
    /// already correctly keeps it independent of GameplayBoardFitter2D's
    /// board-fitting - left exactly where it is, never reparented.
    /// Idempotent - finds the existing object by name, only ever
    /// (re)assigns the cover component's references, never touches its
    /// sprite/color/sortingOrder or any gameplay object.
    /// </summary>
    private static void EnsureResponsiveBackground(Camera camera, WebViewportState2D viewportState, System.Collections.Generic.List<string> modified)
    {
        RemoveLegacyIncorrectBackground(modified);

        GameObject backgroundGO = GameObject.Find("Background2D");
        if (backgroundGO == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'Background2D' not found in the scene - the real gameplay background prefab instance is missing. Cannot set up responsive cover-fit.");
            return;
        }

        SpriteRenderer renderer = backgroundGO.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'Background2D' has no SpriteRenderer/sprite assigned - cannot set up responsive cover-fit.");
            return;
        }

        ResponsiveBackgroundCover2D cover = backgroundGO.GetComponent<ResponsiveBackgroundCover2D>();
        if (cover == null) cover = backgroundGO.AddComponent<ResponsiveBackgroundCover2D>();

        var coverSO = new SerializedObject(cover);
        coverSO.FindProperty("targetCamera").objectReferenceValue = camera;
        coverSO.FindProperty("viewportState").objectReferenceValue = viewportState;
        coverSO.FindProperty("spriteRenderer").objectReferenceValue = renderer;
        coverSO.ApplyModifiedPropertiesWithoutUndo();

        modified.Add($"Background2D (repaired, not recreated) - ResponsiveBackgroundCover2D wired to Main Camera/WebViewportState2D, reusing its existing sprite '{renderer.sprite.name}' - replaces the previous static m_LocalScale with dynamic aspect-preserving cover-fit. Position/parent/sprite/sortingOrder untouched.");
    }

    /// <summary>
    /// Deletes the "GameplayBackground" object left over from the earlier
    /// incorrect implementation (it used background.png, a different asset
    /// from the real Background2D/background2.png, and was always parented
    /// under the always-active IndependentWorldFXRoot - so a plain
    /// GameObject.Find is reliable here, unlike the Main Menu settings
    /// panel case elsewhere in this file where the target sits behind an
    /// inactive ancestor). Safe/idempotent - a no-op once already cleaned up.
    /// </summary>
    private static void RemoveLegacyIncorrectBackground(System.Collections.Generic.List<string> modified)
    {
        GameObject legacy = GameObject.Find(LegacyIncorrectBackgroundName);
        if (legacy == null)
            return;

        Object.DestroyImmediate(legacy);
        modified.Add($"Removed leftover '{LegacyIncorrectBackgroundName}' object from the earlier incorrect background implementation (wrong image, duplicate of the real Background2D).");
    }

    /// <summary>
    /// Reparents an existing gameplay object into ControlPanel's vertical
    /// stack. Deliberately does NOT set anchorMin/anchorMax/pivot/sizeDelta
    /// itself anymore (Phase 9C fix) - ControlPanel's VerticalLayoutGroup now
    /// runs with childControlWidth=true AND childControlHeight=true, so it
    /// fully owns every child's anchors/size every layout pass regardless of
    /// what's set here; the old code set both (a real "layout groups
    /// combined with conflicting anchored positions" case, Part B audit
    /// finding) - LayoutGroup always won at runtime, but the manual
    /// assignment was dead, misleading code. Only the LayoutElement's
    /// min/preferredHeight now decide the child's height, and they are
    /// actually honored now that childControlHeight is true.
    /// </summary>
    private static void ReparentIntoControlPanel(GameObject go, Transform controlPanel, int siblingIndex, float preferredHeight, float minHeight)
    {
        if (go == null) return;

        go.transform.SetParent(controlPanel, false);
        go.transform.SetSiblingIndex(siblingIndex);

        var layoutElement = go.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.minHeight = minHeight;
        layoutElement.preferredHeight = preferredHeight;
    }

    /// <summary>
    /// Repairs UIManager2D.controlPanelStatsText/starImages to point at the
    /// real, already-existing "StatsArea" child of ControlPanel (a direct
    /// child, sibling to TransparentTopSpacer/ControlsCard - never moved or
    /// resized here) instead of leaving them null/empty. StatsArea/
    /// MoveCountLine/StarRow/Star_0/Star_1/Star_2 are pre-existing scene
    /// objects (predating this builder) - this method only ever assigns
    /// SerializedProperty references, it never creates, destroys, moves or
    /// resizes anything, so it is safe to call on every rebuild. If
    /// StatsArea (or any of its expected children) is missing, the
    /// corresponding field is left at its current value and one error is
    /// logged - never a silent partial wire.
    /// </summary>
    private static void WireControlPanelStatsAndStars(Transform controlPanel, SerializedObject uiSO, System.Collections.Generic.List<string> modified)
    {
        Transform statsArea = controlPanel.Find("StatsArea");
        if (statsArea == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'StatsArea' not found under ControlPanel - controlPanelStatsText/starImages left unchanged.");
            return;
        }

        Transform moveCountLine = statsArea.Find("MoveCountLine");
        Text moveCountText = moveCountLine != null ? moveCountLine.GetComponent<Text>() : null;
        if (moveCountText == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'StatsArea/MoveCountLine' (Text) not found - controlPanelStatsText left unchanged.");
        }
        else
        {
            SerializedProperty statsTextProp = uiSO.FindProperty("controlPanelStatsText");
            if (statsTextProp != null) statsTextProp.objectReferenceValue = moveCountText;
        }

        Transform starRow = statsArea.Find("StarRow");
        if (starRow == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'StatsArea/StarRow' not found - starImages left unchanged.");
            return;
        }

        var starImages = new System.Collections.Generic.List<Image>();
        for (int i = 0; i < starRow.childCount; i++)
        {
            Image starImage = starRow.GetChild(i).GetComponent<Image>();
            if (starImage != null) starImages.Add(starImage);
        }

        if (starImages.Count == 0)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'StatsArea/StarRow' has no Image children - starImages left unchanged.");
            return;
        }

        SerializedProperty starImagesProp = uiSO.FindProperty("starImages");
        if (starImagesProp != null)
        {
            starImagesProp.arraySize = starImages.Count;
            for (int i = 0; i < starImages.Count; i++)
            {
                starImagesProp.GetArrayElementAtIndex(i).objectReferenceValue = starImages[i];
            }
        }

        modified.Add($"ControlPanel/StatsArea (repaired: controlPanelStatsText -> MoveCountLine, starImages -> {starImages.Count} StarRow icons - the confirmed stale/duplicated lower-right move counter and non-updating star preview)");
    }

    /// <summary>
    /// Repairs UIManager2D.controlPanelLevelText/controlPanelGridSizeText/
    /// boardManagerForDisplay to point at the real, already-existing
    /// "LevelDetails" child of ControlPanel (a direct child, sibling to
    /// StatsArea/TransparentTopSpacer/ControlsCard - never moved or resized
    /// here) instead of leaving them null. LevelDetails/LevelNameLine/
    /// GridSizeLine are pre-existing scene objects (predating this builder,
    /// same as StatsArea) - this method only ever assigns SerializedProperty
    /// references, it never creates, destroys, moves or resizes anything.
    /// Once wired, UIManager2D.HandleLevelLoaded (already subscribed to
    /// LevelManager2D.OnLevelLoaded - no second event subscription needed)
    /// starts actually writing "Bölüm {n}"/"{width}×{height}" into these
    /// exact objects, replacing whatever placeholder text was last authored
    /// into them.
    /// </summary>
    private static void WireControlPanelLevelDetails(Transform controlPanel, SerializedObject uiSO, System.Collections.Generic.List<string> modified)
    {
        Transform levelDetails = controlPanel.Find("LevelDetails");
        if (levelDetails == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'LevelDetails' not found under ControlPanel - controlPanelLevelText/controlPanelGridSizeText/boardManagerForDisplay left unchanged.");
            return;
        }

        Transform levelNameLine = levelDetails.Find("LevelNameLine");
        Text levelNameText = levelNameLine != null ? levelNameLine.GetComponent<Text>() : null;
        if (levelNameText == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'LevelDetails/LevelNameLine' (Text) not found - controlPanelLevelText left unchanged.");
        }
        else
        {
            SerializedProperty levelTextProp = uiSO.FindProperty("controlPanelLevelText");
            if (levelTextProp != null) levelTextProp.objectReferenceValue = levelNameText;
        }

        Transform gridSizeLine = levelDetails.Find("GridSizeLine");
        Text gridSizeText = gridSizeLine != null ? gridSizeLine.GetComponent<Text>() : null;
        if (gridSizeText == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'LevelDetails/GridSizeLine' (Text) not found - controlPanelGridSizeText left unchanged.");
        }
        else
        {
            SerializedProperty gridSizeTextProp = uiSO.FindProperty("controlPanelGridSizeText");
            if (gridSizeTextProp != null) gridSizeTextProp.objectReferenceValue = gridSizeText;
        }

        // boardManagerForDisplay is a scene-wide singleton (never hidden
        // behind an inactive panel like the Main Menu settings case), so a
        // plain GameObject.Find is reliable here.
        GameObject boardManagerGO = GameObject.Find("BoardManager2D");
        BoardManager2D boardManager = boardManagerGO != null ? boardManagerGO.GetComponent<BoardManager2D>() : null;
        if (boardManager == null)
        {
            Debug.LogError("GameSceneWebLayoutBuilder2D: 'BoardManager2D' not found - boardManagerForDisplay left unchanged.");
        }
        else
        {
            SerializedProperty boardManagerProp = uiSO.FindProperty("boardManagerForDisplay");
            if (boardManagerProp != null) boardManagerProp.objectReferenceValue = boardManager;
        }

        modified.Add("ControlPanel/LevelDetails (repaired: controlPanelLevelText -> LevelNameLine, controlPanelGridSizeText -> GridSizeLine, boardManagerForDisplay -> BoardManager2D - the confirmed stale 'Bölüm 1'/'5×5' lower-right level info)");
    }

    /// <summary>Finds an existing Text object anywhere in the open scene by name and overwrites its content - used to relabel pre-existing buttons (StartWaterButtonText/ReloadButtonText) to match the required mockup without touching their Button/listener wiring.</summary>
    private static void SetTextIfFound(string objectName, string content)
    {
        GameObject go = GameObject.Find(objectName);
        Text text = go != null ? go.GetComponent<Text>() : null;
        if (text != null)
        {
            text.text = content;
        }
    }

    /// <summary>
    /// Phase 9G: a genuinely empty, see-through spacer - no Image, no
    /// Outline, no CanvasGroup, nothing that could tint or block anything.
    /// Idempotent rename: if a previous run's "InfoCard" object still exists
    /// (pre-Phase-9G name) it is renamed in place rather than left behind as
    /// an orphaned duplicate while a fresh "TransparentTopSpacer" is created
    /// alongside it. Any leftover visual children/components from even
    /// older runs (LevelText/GridSizeText/MoveText/StarRow, Image, Outline,
    /// CanvasGroup) are explicitly destroyed.
    /// </summary>
    private static GameObject BuildTransparentTopSpacer(Transform controlPanel)
    {
        Transform existing = controlPanel.Find("TransparentTopSpacer");
        if (existing == null)
        {
            Transform legacy = controlPanel.Find("InfoCard");
            if (legacy != null)
            {
                legacy.name = "TransparentTopSpacer";
                existing = legacy;
            }
        }
        GameObject spacer = existing != null ? existing.gameObject : FindOrCreateChild(controlPanel, "TransparentTopSpacer");

        foreach (string childName in new[] { "LevelText", "GridSizeText", "MoveText", "StarRow" })
        {
            Transform child = spacer.transform.Find(childName);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        Outline outline = spacer.GetComponent<Outline>();
        if (outline != null) Object.DestroyImmediate(outline);

        Image bg = spacer.GetComponent<Image>();
        if (bg != null) Object.DestroyImmediate(bg);

        CanvasGroup canvasGroup = spacer.GetComponent<CanvasGroup>();
        if (canvasGroup != null) Object.DestroyImmediate(canvasGroup);

        var layoutEl = spacer.GetComponent<LayoutElement>();
        if (layoutEl == null) layoutEl = spacer.AddComponent<LayoutElement>();
        layoutEl.minHeight = InfoCardHeight;
        layoutEl.preferredHeight = InfoCardHeight;
        layoutEl.flexibleHeight = 0f;

        return spacer;
    }

    /// <summary>
    /// Phase 9G: owns the dark background that used to sit on ControlPanel's
    /// own root (the confirmed cause of TransparentTopSpacer still rendering
    /// as a solid dark block - it was never actually transparent, only its
    /// own content was). No explicit LayoutElement height here on purpose -
    /// with only a VerticalLayoutGroup and no competing LayoutElement, its
    /// reported preferred height is the sum of its real children (ResultPanel/
    /// StartWaterButton/ReloadButton/SettingsButton) + padding/spacing, so it
    /// wraps its contents compactly instead of claiming a fixed, possibly-too-
    /// tall region - avoiding the exact nested LayoutElement-vs-LayoutGroup
    /// max-priority ambiguity documented on the old InfoCard bug.
    /// </summary>
    private static GameObject BuildControlsCard(Transform controlPanel)
    {
        GameObject card = FindOrCreateChild(controlPanel, "ControlsCard");

        Image bg = card.GetComponent<Image>();
        if (bg == null) bg = card.AddComponent<Image>();
        bg.sprite = null;
        bg.color = new Color(0.04f, 0.05f, 0.09f, 0.9f);
        bg.raycastTarget = false; // Part G: decorative background must never block clicks

        var layout = card.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = card.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(18, 18, 18, 18);

        return card;
    }

    /// <summary>
    /// Part A: rebuilds DevPerformanceHud as a real UGUI panel - top-right
    /// anchored, hidden by default (panel.SetActive(false) here AND
    /// DevPerformanceHud2D.Awake() both enforce this independently),
    /// raycastTarget false on every visual/text piece (Part G), F3-toggled at
    /// runtime by the script itself. Parented directly under Canvas (a
    /// sibling of SafeAreaRoot, not a descendant of it) and set as the LAST
    /// sibling so it renders above everything else per Part G's required
    /// sibling order - and, critically, so it sits completely outside
    /// SafeAreaRoot/MainGameplayArea/ControlPanel's LayoutGroups and can
    /// never reserve layout space for anything.
    /// </summary>
    private static void BuildDevPerformanceHud(GameObject canvasGO, WebViewportState2D viewportState, LevelManager2D levelManagerComponent, System.Collections.Generic.List<string> modified)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameObject hudRoot = FindOrCreateChild(canvasGO.transform, "DevPerformanceHud");
        hudRoot.transform.SetAsLastSibling();
        StretchFill(RectOf(hudRoot), 0f);

        GameObject panel = FindOrCreateChild(hudRoot.transform, "Panel");
        SetAnchoredRect(RectOf(panel), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(260f, 130f));
        EnsureImage(panel, new Color(0f, 0f, 0f, 0.6f), false); // raycastTarget false - Part G: must never block the board

        GameObject statsGO = FindOrCreateChild(panel.transform, "StatsText");
        StretchFill(RectOf(statsGO), 8f);
        Text statsText = EnsureText(statsGO, "FPS: --");
        statsText.fontSize = 18;
        statsText.alignment = TextAnchor.UpperLeft;
        statsText.horizontalOverflow = HorizontalWrapMode.Overflow;
        statsText.verticalOverflow = VerticalWrapMode.Overflow;
        statsText.raycastTarget = false;

        panel.SetActive(false);

        DevPerformanceHud2D devHud = hudRoot.GetComponent<DevPerformanceHud2D>();
        if (devHud == null) devHud = hudRoot.AddComponent<DevPerformanceHud2D>();
        var devHudSO = new SerializedObject(devHud);
        devHudSO.FindProperty("viewportState").objectReferenceValue = viewportState;
        devHudSO.FindProperty("levelManager").objectReferenceValue = levelManagerComponent;
        devHudSO.FindProperty("panelRoot").objectReferenceValue = panel;
        devHudSO.FindProperty("statsText").objectReferenceValue = statsText;
        devHudSO.ApplyModifiedPropertiesWithoutUndo();

        modified.Add("DevPerformanceHud (Phase 9C: rebuilt as a real UGUI top-right panel, hidden by default, F3 toggle, raycastTarget false everywhere - was an always-on OnGUI overlay at raw screen (4,4) that covered TopHUD's BackButton/LevelBadge)");
#endif
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
