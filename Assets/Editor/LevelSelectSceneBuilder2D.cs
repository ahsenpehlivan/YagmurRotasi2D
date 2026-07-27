using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Phase 9A: builds/updates the new LevelSelectScene2D - "Bölüm Seç" title,
/// Back button, and a ScrollRect + GridLayoutGroup that LevelSelectController2D
/// populates at RUNTIME with one LevelButton2D card per CampaignLevelCatalog2D
/// entry (never 100 manually-authored scene buttons). Built entirely out of the
/// existing shared UI package (Assets/Art2D/FinalSprites/UI/) and the
/// SHPinscher-Regular11 font, following the exact same idempotent
/// find-or-create pattern as MainMenuSceneBuilder2D - never touches
/// MainMenuScene2D or GameScene2D's own hierarchy.
/// </summary>
public static class LevelSelectSceneBuilder2D
{
    private const string ScenePath = "Assets/Scenes/LevelSelectScene2D.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene2D.unity";
    private const string GameplayScenePath = "Assets/Scenes/GameScene2D.unity";

    private const string UiFolder = "Assets/Art2D/FinalSprites/UI";
    private const string FontPath = "Assets/SHPinscher-Regular11/SHPinscher-Regular.otf";
    private const string LevelButtonPrefabPath = LevelButtonPrefabBuilder2D.PrefabPath;
    private const string BackgroundPath = "Assets/Art2D/FinalSprites/Background/background.png";

    // Phase 9B: shorter than the Phase 9A portrait value (190) - a landscape
    // 1080-tall viewport has much less vertical room to spare on chrome.
    private const float TitleBarHeight = 140f;
    private const float TitleBadgeWidth = 480f;
    private const float TitleBadgeHeight = 100f;
    private const float TitleBadgeInset = 10f;

    private const float BackButtonWidth = 170f;
    private const float BackButtonHeight = 84f;
    private const float FullscreenButtonWidth = 170f;
    private const float FullscreenButtonHeight = 84f;

    private const float ScrollbarWidth = 28f;

    // Percentage-anchored margins give a naturally centered, bounded-width
    // content area (Part B: "centered maximum-width content area") without a
    // fixed pixel cap that could clip on a narrower-than-expected screen -
    // paired with ResponsiveLevelGrid2D's own per-card min/max width and
    // column-count clamps, which do the actual "don't stretch absurdly on a
    // very wide screen" work.
    private const float ScrollAreaHorizontalMarginFraction = 0.04f;
    private const float ScrollAreaBottomMargin = 24f;
    private const float ScrollAreaTopMargin = TitleBarHeight + 16f;

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    [MenuItem("YagmurRotasi2D/Build Phase 9A Level Select Scene")]
    public static void BuildSceneCommand()
    {
        TryBuildScene(true);
    }

    // Part D readability fix - Back/Fullscreen button labels.
    private const float BackLabelFontSize = 28f;
    private const float FullscreenLabelFontSize = 26f;
    private static readonly Color ButtonLabelOutline = new Color(0.20f, 0.11f, 0.05f, 0.85f); // dark brown, matches the brown.png button art

    public static bool TryBuildScene(bool logDetails)
    {
        // "Bölüm Seç" title stays legacy Text via shPinscherFont, byte-for-byte
        // unchanged per spec ("must remain visually unchanged") - only
        // Back/Fullscreen button labels below migrate to TMP.
        Font shPinscherFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        TMP_FontAsset tmpFont = TMPTextSetup2D.GetOrCreateFontAsset();
        Sprite panelBase = LoadFirstSprite(UiFolder + "/Panels/tan.png");
        Sprite badgeWhite = LoadFirstSprite(UiFolder + "/Badges/white.png");
        Sprite badgeWhiteInlay = LoadFirstSprite(UiFolder + "/Badges/white_inlay.png");
        Sprite buttonBase = LoadFirstSprite(UiFolder + "/Buttons/brown.png");
        Sprite buttonInlay = LoadFirstSprite(UiFolder + "/Buttons/brown_inlay.png");
        Sprite buttonPressed = LoadFirstSprite(UiFolder + "/Buttons/brown_pressed.png");
        Sprite backgroundSprite = LoadFirstSprite(BackgroundPath);
        GameObject levelButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelButtonPrefabPath);

        if (shPinscherFont == null || tmpFont == null || badgeWhite == null || buttonBase == null || backgroundSprite == null)
        {
            Debug.LogError("LevelSelectSceneBuilder2D: one or more required UI package assets could not be loaded/created (see warnings above). Aborting.");
            return false;
        }

        if (levelButtonPrefab == null)
        {
            Debug.LogError($"LevelSelectSceneBuilder2D: '{LevelButtonPrefabPath}' not found. Run " +
                "'YagmurRotasi2D > Build Level Button Prefab' first, then re-run this command.");
            return false;
        }

        bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        Scene scene = sceneExists
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------------- Main Camera ----------------
        GameObject cameraGO = GameObject.Find("Main Camera");
        if (cameraGO == null)
        {
            cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cameraGO.AddComponent<AudioListener>();
        }

        // ---------------- EventSystem (exactly one) ----------------
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<InputSystemUIInputModule>();
        }

        // ---------------- Canvas ----------------
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("Canvas");
        }
        var canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Phase 9B: landscape-first primary design resolution (was 1080x1920 portrait).
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGO.GetComponent<GraphicRaycaster>() == null) canvasGO.AddComponent<GraphicRaycaster>();

        // ---------------- SafeAreaRoot ----------------
        GameObject safeAreaRoot = FindOrCreateChild(canvasGO.transform, "SafeAreaRoot");
        StretchFill(RectOf(safeAreaRoot), 0f);
        if (safeAreaRoot.GetComponent<SafeAreaFitter2D>() == null) safeAreaRoot.AddComponent<SafeAreaFitter2D>();

        // ---------------- Shared Phase 9B responsive services ----------------
        WebViewportState2D viewportState = canvasGO.GetComponent<WebViewportState2D>();
        if (viewportState == null) viewportState = canvasGO.AddComponent<WebViewportState2D>();
        WebFullscreenController2D fullscreenController = canvasGO.GetComponent<WebFullscreenController2D>();
        if (fullscreenController == null) fullscreenController = canvasGO.AddComponent<WebFullscreenController2D>();

        // ---------------- Background ----------------
        // Same cover-fit approach as MainMenuSceneBuilder2D's background:
        // AspectRatioFitter.EnvelopeParent scales the RectTransform to fully
        // cover the stretched parent while preserving the source image's
        // aspect ratio (crops overflow instead of distorting/letterboxing).
        GameObject background = FindOrCreateChild(safeAreaRoot.transform, "LevelSelectBackground");
        StretchFill(RectOf(background), 0f);
        background.transform.SetAsFirstSibling();
        Image backgroundImage = SetupImage(background, backgroundSprite, false, Color.white);
        backgroundImage.preserveAspect = false;
        var backgroundFitter = background.GetComponent<AspectRatioFitter>();
        if (backgroundFitter == null) backgroundFitter = background.AddComponent<AspectRatioFitter>();
        backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        backgroundFitter.aspectRatio = backgroundSprite.rect.width / backgroundSprite.rect.height;

        // ---------------- TitleBar ----------------
        GameObject titleBar = FindOrCreateChild(safeAreaRoot.transform, "TitleBar");
        SetAnchoredRect(RectOf(titleBar), TopLeft, new Vector2(1f, 1f), TopCenter, Vector2.zero, new Vector2(0f, TitleBarHeight));

        GameObject titleBadge = FindOrCreateChild(titleBar.transform, "TitleBadge");
        SetAnchoredRect(RectOf(titleBadge), TopCenter, TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(TitleBadgeWidth, TitleBadgeHeight));
        SetupImage(titleBadge, badgeWhite, false, Color.white);
        GameObject titleBadgeInset = FindOrCreateChild(titleBadge.transform, "TitleBadgeInset");
        StretchFill(RectOf(titleBadgeInset), TitleBadgeInset);
        SetupImage(titleBadgeInset, badgeWhiteInlay, false, Color.white);
        titleBadgeInset.transform.SetAsFirstSibling();
        GameObject titleTextGO = FindOrCreateChild(titleBadge.transform, "TitleText");
        StretchFill(RectOf(titleTextGO), 0f);
        SetupText(titleTextGO, "Bölüm Seç", shPinscherFont, 56, Color.black, TextAnchor.MiddleCenter, false);

        GameObject backButtonGO = FindOrCreateChild(titleBar.transform, "BackButton");
        SetAnchoredRect(RectOf(backButtonGO), TopLeft, TopLeft, TopLeft, new Vector2(24f, -30f), new Vector2(BackButtonWidth, BackButtonHeight));
        Image backImage = SetupImage(backButtonGO, buttonBase, true, Color.white);
        Button backButton = backButtonGO.GetComponent<Button>();
        if (backButton == null) backButton = backButtonGO.AddComponent<Button>();
        backButton.targetGraphic = backImage;
        if (buttonPressed != null)
        {
            backButton.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = backButton.spriteState;
            state.pressedSprite = buttonPressed;
            state.highlightedSprite = buttonBase;
            state.selectedSprite = buttonBase;
            state.disabledSprite = buttonBase;
            backButton.spriteState = state;
        }
        if (buttonInlay != null)
        {
            GameObject backInset = FindOrCreateChild(backButtonGO.transform, "ButtonInset");
            StretchFill(RectOf(backInset), 8f);
            SetupImage(backInset, buttonInlay, false, Color.white);
            backInset.transform.SetAsFirstSibling();
        }
        GameObject backLabelGO = FindOrCreateChild(backButtonGO.transform, "Label");
        StretchFill(RectOf(backLabelGO), 0f);
        TMPTextSetup2D.SetupTMPText(backLabelGO, "Geri", tmpFont, BackLabelFontSize, Color.white,
            TextAlignmentOptions.Center, wrap: false, bold: true,
            outlineWidth: 0.14f, outlineColor: ButtonLabelOutline);

        // ---------------- Fullscreen button (Phase 9B) ----------------
        GameObject fullscreenButtonGO = FindOrCreateChild(titleBar.transform, "FullscreenButton");
        SetAnchoredRect(RectOf(fullscreenButtonGO), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -30f), new Vector2(FullscreenButtonWidth, FullscreenButtonHeight));
        Image fullscreenImage = SetupImage(fullscreenButtonGO, buttonBase, true, Color.white);
        Button fullscreenButton = fullscreenButtonGO.GetComponent<Button>();
        if (fullscreenButton == null) fullscreenButton = fullscreenButtonGO.AddComponent<Button>();
        fullscreenButton.targetGraphic = fullscreenImage;
        if (buttonPressed != null)
        {
            fullscreenButton.transition = Selectable.Transition.SpriteSwap;
            SpriteState fsState = fullscreenButton.spriteState;
            fsState.pressedSprite = buttonPressed;
            fsState.highlightedSprite = buttonBase;
            fsState.selectedSprite = buttonBase;
            fsState.disabledSprite = buttonBase;
            fullscreenButton.spriteState = fsState;
        }
        if (buttonInlay != null)
        {
            GameObject fsInset = FindOrCreateChild(fullscreenButtonGO.transform, "ButtonInset");
            StretchFill(RectOf(fsInset), 8f);
            SetupImage(fsInset, buttonInlay, false, Color.white);
            fsInset.transform.SetAsFirstSibling();
        }
        GameObject fsLabelGO = FindOrCreateChild(fullscreenButtonGO.transform, "Label");
        StretchFill(RectOf(fsLabelGO), 0f);
        TMPTextSetup2D.SetupTMPText(fsLabelGO, "Tam Ekran", tmpFont, FullscreenLabelFontSize, Color.white,
            TextAlignmentOptions.Center, wrap: true, bold: true, autoSize: true, autoSizeMin: 18f, autoSizeMax: FullscreenLabelFontSize,
            outlineWidth: 0.14f, outlineColor: ButtonLabelOutline);
        // Editor-time onClick.AddListener() is never persisted into the saved
        // scene (same root cause the Back button had) - a self-wiring runtime
        // component is added instead, which IS persisted.
        WebFullscreenButtonForwarder2D fullscreenForwarder = fullscreenButton.GetComponent<WebFullscreenButtonForwarder2D>();
        if (fullscreenForwarder == null) fullscreenForwarder = fullscreenButton.gameObject.AddComponent<WebFullscreenButtonForwarder2D>();
        SerializedObject fullscreenForwarderSO = new SerializedObject(fullscreenForwarder);
        fullscreenForwarderSO.FindProperty("fullscreenController").objectReferenceValue = fullscreenController;
        fullscreenForwarderSO.ApplyModifiedPropertiesWithoutUndo();

        // ---------------- ScrollView ----------------
        GameObject scrollViewGO = FindOrCreateChild(safeAreaRoot.transform, "ScrollView");
        RectTransform scrollRt = RectOf(scrollViewGO);
        scrollRt.anchorMin = new Vector2(ScrollAreaHorizontalMarginFraction, 0f);
        scrollRt.anchorMax = new Vector2(1f - ScrollAreaHorizontalMarginFraction, 1f);
        scrollRt.pivot = Center;
        scrollRt.offsetMin = new Vector2(0f, ScrollAreaBottomMargin);
        scrollRt.offsetMax = new Vector2(0f, -ScrollAreaTopMargin);

        ScrollRect scrollRect = scrollViewGO.GetComponent<ScrollRect>();
        if (scrollRect == null) scrollRect = scrollViewGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.12f;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 30f;

        // Viewport's right edge is inset by the scrollbar's reserved strip
        // (Part D: "account for scrollbar width and content padding") - so
        // ResponsiveLevelGrid2D's width-based column calculation, which reads
        // Content's stretched width, already correctly excludes the
        // scrollbar area without needing any scrollbar-aware code of its own.
        GameObject viewportGO = FindOrCreateChild(scrollViewGO.transform, "Viewport");
        RectTransform viewportRt = RectOf(viewportGO);
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.pivot = Center;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = new Vector2(-(ScrollbarWidth + 10f), 0f);
        SetupImage(viewportGO, null, true, new Color(1f, 1f, 1f, 0f));
        if (viewportGO.GetComponent<RectMask2D>() == null) viewportGO.AddComponent<RectMask2D>();

        GameObject contentGO = FindOrCreateChild(viewportGO.transform, "Content");
        RectTransform contentRt = RectOf(contentGO);
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = TopCenter;
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup gridLayout = contentGO.GetComponent<GridLayoutGroup>();
        if (gridLayout == null) gridLayout = contentGO.AddComponent<GridLayoutGroup>();
        gridLayout.padding = new RectOffset(0, 0, 10, 10);
        gridLayout.cellSize = new Vector2(190f, 210f);
        gridLayout.spacing = new Vector2(20f, 20f);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;

        ContentSizeFitter sizeFitter = contentGO.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null) sizeFitter = contentGO.AddComponent<ContentSizeFitter>();
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (contentGO.GetComponent<ResponsiveLevelGrid2D>() == null) contentGO.AddComponent<ResponsiveLevelGrid2D>();

        // ---------------- Scrollbar (Part D: "scrollbar dragging") ----------------
        GameObject scrollbarGO = FindOrCreateChild(scrollViewGO.transform, "Scrollbar");
        SetAnchoredRect(RectOf(scrollbarGO), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(ScrollbarWidth, 0f));
        SetupImage(scrollbarGO, badgeWhite, false, new Color(1f, 1f, 1f, 0.5f));

        GameObject scrollbarSlideArea = FindOrCreateChild(scrollbarGO.transform, "SlidingArea");
        StretchFill(RectOf(scrollbarSlideArea), 4f);

        GameObject scrollbarHandle = FindOrCreateChild(scrollbarSlideArea.transform, "Handle");
        RectTransform handleRt = RectOf(scrollbarHandle);
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = new Vector2(1f, 0.2f);
        handleRt.sizeDelta = Vector2.zero;
        Image handleImage = SetupImage(scrollbarHandle, buttonBase, true, Color.white);

        Scrollbar scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        if (scrollbar == null) scrollbar = scrollbarGO.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.TopToBottom;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRt;

        scrollRect.viewport = RectOf(viewportGO);
        scrollRect.content = contentRt;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // ---------------- Landscape-blocked overlay ----------------
        GameObject overlayRoot = FindOrCreateChild(safeAreaRoot.transform, "LandscapeRequirementOverlay");
        StretchFill(RectOf(overlayRoot), 0f);
        overlayRoot.transform.SetAsLastSibling();

        GameObject overlayBlocker = FindOrCreateChild(overlayRoot.transform, "OverlayBlocker");
        StretchFill(RectOf(overlayBlocker), 0f);
        SetupImage(overlayBlocker, null, true, new Color(0.06f, 0.08f, 0.10f, 0.92f));

        GameObject overlayTextGO = FindOrCreateChild(overlayRoot.transform, "OverlayText");
        SetAnchoredRect(RectOf(overlayTextGO), Center, Center, Center, Vector2.zero, new Vector2(900f, 200f));
        SetupText(overlayTextGO, "En iyi deneyim için ekranı yatay kullanın.", shPinscherFont, 44, Color.white, TextAnchor.MiddleCenter, true);

        LandscapeRequirementOverlay2D overlay = overlayRoot.GetComponent<LandscapeRequirementOverlay2D>();
        if (overlay == null) overlay = overlayRoot.AddComponent<LandscapeRequirementOverlay2D>();
        var overlaySO = new SerializedObject(overlay);
        overlaySO.FindProperty("viewportState").objectReferenceValue = viewportState;
        overlaySO.FindProperty("root").objectReferenceValue = overlayRoot;
        overlaySO.FindProperty("fullscreenController").objectReferenceValue = fullscreenController;
        overlaySO.ApplyModifiedPropertiesWithoutUndo();
        overlayRoot.SetActive(false);

        // ---------------- LevelSelectController2D ----------------
        GameObject controllerGO = GameObject.Find("LevelSelectController2D");
        if (controllerGO == null)
        {
            controllerGO = new GameObject("LevelSelectController2D");
        }
        LevelSelectController2D controller = controllerGO.GetComponent<LevelSelectController2D>();
        if (controller == null) controller = controllerGO.AddComponent<LevelSelectController2D>();

        var so = new SerializedObject(controller);
        so.FindProperty("backButton").objectReferenceValue = backButton;
        so.FindProperty("content").objectReferenceValue = contentRt;
        so.FindProperty("levelButtonPrefab").objectReferenceValue = levelButtonPrefab;
        so.FindProperty("mainMenuSceneName").stringValue = "MainMenuScene2D";
        so.FindProperty("gameplaySceneName").stringValue = "GameScene2D";
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        EnsureBuildSettings(logDetails);

        if (logDetails)
        {
            Debug.Log($"LevelSelectSceneBuilder2D: {(sceneExists ? "updated" : "created")} '{ScenePath}'.\n" +
                $"  Background: '{backgroundSprite.name}' ({BackgroundPath}) - cover-fit via AspectRatioFitter.EnvelopeParent.\n" +
                "Hierarchy: Canvas(1920x1080 ref) > SafeAreaRoot > LevelSelectBackground, TitleBar > TitleBadge(+inset+text), " +
                "BackButton(+inset+Label), FullscreenButton(+inset+Label), ScrollView(ScrollRect) > Viewport(RectMask2D) > " +
                "Content(GridLayoutGroup+ContentSizeFitter+ResponsiveLevelGrid2D), Scrollbar > SlidingArea > Handle; " +
                "LandscapeRequirementOverlay (topmost); LevelSelectController2D (logic-only GameObject) wired to BackButton/Content/LevelButton2D prefab.\n" +
                $"Level cards are populated at RUNTIME by LevelSelectController2D from the campaign catalog - none exist in this scene file.");
        }

        return true;
    }

    [MenuItem("YagmurRotasi2D/Phase 9/Open Level Select Scene")]
    public static void OpenLevelSelectSceneCommand()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError($"LevelSelectSceneBuilder2D: '{ScenePath}' does not exist yet. Run " +
                "'YagmurRotasi2D > Build Phase 9A Level Select Scene' first.");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    /// <summary>Phase 9B: delegates to the single shared WebBuildConfig2D.EnsureSceneOrder - see its own doc comment for why this used to be a local, divergent copy.</summary>
    private static void EnsureBuildSettings(bool logDetails)
    {
        WebBuildConfig2D.EnsureSceneOrder(logDetails);
    }

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

    private static Image SetupImage(GameObject go, Sprite sprite, bool raycastTarget, Color color)
    {
        var image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && HasValidBorder(sprite) ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static Text SetupText(GameObject go, string content, Font font, int fontSize, Color color, TextAnchor alignment, bool wrap,
        bool bestFit = false, int minSize = 10, int maxSize = 40, float lineSpacing = 1f)
    {
        var text = go.GetComponent<Text>();
        if (text == null) text = go.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.resizeTextForBestFit = bestFit;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
        text.lineSpacing = lineSpacing;
        return text;
    }

    private static bool HasValidBorder(Sprite sprite)
    {
        Vector4 b = sprite.border;
        return b.x > 0f || b.y > 0f || b.z > 0f || b.w > 0f;
    }

    private static Sprite LoadFirstSprite(string texturePath)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().FirstOrDefault();
        if (sprite == null)
        {
            Debug.LogWarning($"LevelSelectSceneBuilder2D: no sprite found at '{texturePath}'.");
        }
        return sprite;
    }
}
