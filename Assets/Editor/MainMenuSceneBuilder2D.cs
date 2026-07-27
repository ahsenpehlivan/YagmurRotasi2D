using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Builds/updates the separate MainMenuScene2D using the user's imported main
/// menu artwork (Assets/Art2D/FinalSprites/MainMenu/) plus the shared UI package
/// (Assets/Art2D/FinalSprites/UI/) for the secondary Settings/Reset panels.
///
/// Asset discovery performed during implementation:
///   Background/MainMenu.png (941x1672, aspect ~0.563 - very close to the
///   1080x1920 reference canvas's 0.5625) already has the "Yağmur Rotası" game
///   title painted into it, so no separate GameTitle text is created.
///   Buttons/startGame.png, settings.png, levelReset.png (866x288 each, aspect
///   3:1) all have their Turkish labels ("Oyuna Başla", "Ayarlar",
///   "Level Sıfırla") already drawn in - no separate button labels are created
///   for these three. Only one sprite exists per button (no "_pressed"
///   variant), so Button.Transition = ColorTint with a subtle tint is used
///   instead of SpriteSwap.
///
/// Like every prior phase's tooling, this never modifies GameScene2D's visual
/// hierarchy - it only ever opens/builds MainMenuScene2D, and everything here
/// is find-or-create idempotent.
/// </summary>
public static class MainMenuSceneBuilder2D
{
    private const string ScenePath = "Assets/Scenes/MainMenuScene2D.unity";
    private const string GameplaySceneName = "GameScene2D";
    private const string GameplayScenePath = "Assets/Scenes/GameScene2D.unity";

    // Phase 9A: Oyuna Başla now opens LevelSelectScene2D instead of GameScene2D directly.
    private const string LevelSelectSceneName = "LevelSelectScene2D";

    private const string MainMenuArtFolder = "Assets/Art2D/FinalSprites/MainMenu";
    private const string BackgroundPath = MainMenuArtFolder + "/Background/MainMenu.png";
    private const string PlaySpritePath = MainMenuArtFolder + "/Buttons/startGame.png";
    private const string SettingsSpritePath = MainMenuArtFolder + "/Buttons/settings.png";
    private const string ResetSpritePath = MainMenuArtFolder + "/Buttons/levelReset.png";

    private const string UiFolder = "Assets/Art2D/FinalSprites/UI";
    private const string FontPath = "Assets/SHPinscher-Regular11/SHPinscher-Regular.otf";

    // Phase 9B.1: Play/Settings kept at native 3:1 (matches the source art's
    // 866x288 aspect exactly - 300x100 is the only point in the requested
    // 300-360 x 80-100 range that hits 3:1 with zero distortion).
    private const float MainButtonWidth = 300f;
    private const float MainButtonHeight = 100f;
    private const float PrimaryRowSpacing = 32f;

    // ResetProgressButton's source art is ALSO 866x288 (3:1), but the
    // requested 340-440 x 70-90 range is geometrically incompatible with
    // that ratio (3:1 in that height band would need ~210-270 width, well
    // below 340) - the requested pixel range is the more specific/recent
    // instruction, so it wins; (340,90) is the closest point in the
    // requested box to 3:1 (ratio 3.78 vs native 3.0), minimizing the
    // resulting stretch while still satisfying "340-440 wide, 70-90 tall,
    // slightly wider but visually secondary than Play/Settings".
    private const float ResetButtonWidth = 340f;
    private const float ResetButtonHeight = 90f;
    private const float ButtonAreaSpacing = 26f;

    // Recommended 0.25, adjustable 0.22-0.32 per spec "after inspecting the
    // title" - 0.26 chosen to sit clearly below the title without being able
    // to visually verify exact title bounds in this session; nudge within
    // the sanctioned range if it still reads too close/far in the Editor.
    private const float ButtonAreaVerticalAnchor = 0.26f;

    [MenuItem("YagmurRotasi2D/Build Phase 7E8 Main Menu")]
    public static void BuildMenuCommand()
    {
        TryBuildMainMenu(true);
    }

    public static bool TryBuildMainMenu(bool logDetails)
    {
        Font shPinscherFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        Sprite backgroundSprite = LoadFirstSprite(BackgroundPath);
        Sprite playSprite = LoadFirstSprite(PlaySpritePath);
        Sprite settingsSprite = LoadFirstSprite(SettingsSpritePath);
        Sprite resetSprite = LoadFirstSprite(ResetSpritePath);
        Sprite panelBase = LoadFirstSprite(UiFolder + "/Panels/tan.png");
        Sprite panelInlay = LoadFirstSprite(UiFolder + "/Panels/tan_inlay.png");
        Sprite badgeBase = LoadFirstSprite(UiFolder + "/Badges/white.png");
        Sprite badgeInlay = LoadFirstSprite(UiFolder + "/Badges/white_inlay.png");
        Sprite buttonBase = LoadFirstSprite(UiFolder + "/Buttons/brown.png");
        Sprite buttonInlay = LoadFirstSprite(UiFolder + "/Buttons/brown_inlay.png");

        if (shPinscherFont == null || backgroundSprite == null || playSprite == null || settingsSprite == null || resetSprite == null)
        {
            Debug.LogError("MainMenuSceneBuilder2D: one or more required assets could not be loaded (see warnings above). Aborting.");
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

        // ---------------- MainMenuBackground ----------------
        // Phase 9B: the existing MainMenu.png source art is portrait
        // (941x1672) - there is no landscape-specific background asset in
        // the project yet. AspectRatioFitter.EnvelopeParent gives true
        // "cover" behavior (fills the viewport, preserves aspect, crops
        // overflow) instead of the old disproportionate stretch - the least
        // distorted option available with the CURRENT asset. Revisit once a
        // landscape-native background exists.
        GameObject background = FindOrCreateChild(safeAreaRoot.transform, "MainMenuBackground");
        StretchFill(RectOf(background), 0f);
        background.transform.SetAsFirstSibling();
        Image backgroundImage = SetupImage(background, backgroundSprite, false, Color.white);
        backgroundImage.preserveAspect = false;
        var backgroundFitter = background.GetComponent<AspectRatioFitter>();
        if (backgroundFitter == null) backgroundFitter = background.AddComponent<AspectRatioFitter>();
        backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        backgroundFitter.aspectRatio = backgroundSprite.rect.width / backgroundSprite.rect.height;

        // Subtle dark readability overlay so menu text/cards stay legible
        // over whichever part of the (now cropped) background shows through.
        GameObject readabilityOverlay = FindOrCreateChild(safeAreaRoot.transform, "ReadabilityOverlay");
        StretchFill(RectOf(readabilityOverlay), 0f);
        SetupImage(readabilityOverlay, null, false, new Color(0f, 0f, 0f, 0.22f));
        readabilityOverlay.transform.SetSiblingIndex(1);

        // ---------------- MainMenuContent / MainMenuButtonArea (Phase 9B.1: title-safe lower-half layout) ----------------
        GameObject content = FindOrCreateChild(safeAreaRoot.transform, "MainMenuContent");
        StretchFill(RectOf(content), 0f);

        float primaryRowWidth = MainButtonWidth * 2f + PrimaryRowSpacing;
        float buttonAreaWidth = Mathf.Max(primaryRowWidth, ResetButtonWidth) + 20f;
        float buttonAreaHeight = MainButtonHeight + ButtonAreaSpacing + ResetButtonHeight + 20f;

        GameObject buttonArea = FindOrCreateChild(content.transform, "MainMenuButtonArea");
        RectTransform buttonAreaRt = RectOf(buttonArea);
        buttonAreaRt.anchorMin = new Vector2(0.5f, ButtonAreaVerticalAnchor);
        buttonAreaRt.anchorMax = new Vector2(0.5f, ButtonAreaVerticalAnchor);
        buttonAreaRt.pivot = Center;
        buttonAreaRt.anchoredPosition = Vector2.zero;
        buttonAreaRt.sizeDelta = new Vector2(buttonAreaWidth, buttonAreaHeight);

        var buttonAreaLayout = buttonArea.GetComponent<VerticalLayoutGroup>();
        if (buttonAreaLayout == null) buttonAreaLayout = buttonArea.AddComponent<VerticalLayoutGroup>();
        buttonAreaLayout.spacing = ButtonAreaSpacing;
        buttonAreaLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonAreaLayout.childControlWidth = false;
        buttonAreaLayout.childControlHeight = false;
        buttonAreaLayout.childForceExpandWidth = false;
        buttonAreaLayout.childForceExpandHeight = false;

        GameObject primaryRow = FindOrCreateChild(buttonArea.transform, "PrimaryButtonRow");
        primaryRow.transform.SetSiblingIndex(0);
        RectOf(primaryRow).sizeDelta = new Vector2(primaryRowWidth, MainButtonHeight);

        var primaryRowLayout = primaryRow.GetComponent<HorizontalLayoutGroup>();
        if (primaryRowLayout == null) primaryRowLayout = primaryRow.AddComponent<HorizontalLayoutGroup>();
        primaryRowLayout.spacing = PrimaryRowSpacing;
        primaryRowLayout.childAlignment = TextAnchor.MiddleCenter;
        primaryRowLayout.childControlWidth = false;
        primaryRowLayout.childControlHeight = false;
        primaryRowLayout.childForceExpandWidth = false;
        primaryRowLayout.childForceExpandHeight = false;

        // FindOrCreateAnywhere locates each button by name WHEREVER it
        // currently sits in the scene (GameObject.Find searches the whole
        // scene, not just direct children) before reparenting it into the
        // new structure - this is what makes the rebuild idempotent and
        // reuse-safe regardless of prior hierarchy shape (a stale
        // Phase 9B "MainButtonRoot" layout, or an already-correct one from a
        // prior run of THIS command). Button.onClick listeners are runtime-
        // only (MainMenuController2D.Awake adds them fresh each play session
        // from its serialized field references, re-wired below) - nothing
        // about reparenting a GameObject touches that.
        Button playButton = FindOrCreateAnywhere("PlayButton", primaryRow.transform);
        ConfigureMainButton(playButton.gameObject, playSprite, new Vector2(MainButtonWidth, MainButtonHeight));
        playButton.transform.SetSiblingIndex(0);

        Button settingsButton = FindOrCreateAnywhere("SettingsButton", primaryRow.transform);
        ConfigureMainButton(settingsButton.gameObject, settingsSprite, new Vector2(MainButtonWidth, MainButtonHeight));
        settingsButton.transform.SetSiblingIndex(1);

        Button resetProgressButton = FindOrCreateAnywhere("ResetProgressButton", buttonArea.transform);
        ConfigureMainButton(resetProgressButton.gameObject, resetSprite, new Vector2(ResetButtonWidth, ResetButtonHeight));
        resetProgressButton.transform.SetSiblingIndex(1);

        // Cleanup: the old Phase 9B "MainButtonRoot" wrapper was this
        // builder's own creation (never manually authored) - safe to remove
        // once empty (its former children were just reparented above). No-op
        // on every subsequent idempotent run once it's gone.
        Transform staleButtonRoot = content.transform.Find("MainButtonRoot");
        if (staleButtonRoot != null && staleButtonRoot.childCount == 0)
        {
            Object.DestroyImmediate(staleButtonRoot.gameObject);
        }

        // ---------------- Fullscreen button (Phase 9B) ----------------
        Button fullscreenButton = SetupSimpleButton(safeAreaRoot.transform, "FullscreenButton", buttonBase, buttonInlay,
            shPinscherFont, "Tam Ekran", new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(180f, 76f));
        // Editor-time onClick.AddListener() is never persisted into the saved
        // scene (same root cause the Back button had) - a self-wiring runtime
        // component is added instead, which IS persisted.
        WebFullscreenButtonForwarder2D fullscreenForwarder = fullscreenButton.GetComponent<WebFullscreenButtonForwarder2D>();
        if (fullscreenForwarder == null) fullscreenForwarder = fullscreenButton.gameObject.AddComponent<WebFullscreenButtonForwarder2D>();
        SerializedObject fullscreenForwarderSO = new SerializedObject(fullscreenForwarder);
        fullscreenForwarderSO.FindProperty("fullscreenController").objectReferenceValue = fullscreenController;
        fullscreenForwarderSO.ApplyModifiedPropertiesWithoutUndo();

        // ---------------- SettingsPanel ----------------
        GameObject settingsPanel = FindOrCreateChild(safeAreaRoot.transform, "SettingsPanel");
        StretchFill(RectOf(settingsPanel), 0f);

        GameObject settingsBlocker = FindOrCreateChild(settingsPanel.transform, "SettingsBlocker");
        StretchFill(RectOf(settingsBlocker), 0f);
        SetupImage(settingsBlocker, null, true, new Color(0f, 0f, 0f, 0.55f));
        settingsBlocker.transform.SetAsFirstSibling();

        GameObject settingsCard = FindOrCreateChild(settingsPanel.transform, "SettingsCard");
        SetAnchoredRect(RectOf(settingsCard), Center, Center, Center, Vector2.zero, new Vector2(760f, 760f));
        SetupImage(settingsCard, panelBase, false, Color.white);

        GameObject settingsCardInset = FindOrCreateChild(settingsCard.transform, "SettingsCardInset");
        StretchFill(RectOf(settingsCardInset), 24f);
        SetupImage(settingsCardInset, panelInlay, false, Color.white);
        settingsCardInset.transform.SetAsFirstSibling();

        GameObject settingsTitleBadge = FindOrCreateChild(settingsCard.transform, "SettingsTitleBadge");
        SetAnchoredRect(RectOf(settingsTitleBadge), TopCenter, TopCenter, TopCenter, new Vector2(0f, -36f), new Vector2(480f, 110f));
        SetupImage(settingsTitleBadge, badgeBase, false, Color.white);
        GameObject settingsTitleBadgeInset = FindOrCreateChild(settingsTitleBadge.transform, "PackageDecoration");
        StretchFill(RectOf(settingsTitleBadgeInset), 8f);
        SetupImage(settingsTitleBadgeInset, badgeInlay, false, Color.white);
        settingsTitleBadgeInset.transform.SetAsFirstSibling();
        GameObject settingsTitleGO = FindOrCreateChild(settingsTitleBadge.transform, "SettingsTitle");
        StretchFill(RectOf(settingsTitleGO), 0f);
        SetupText(settingsTitleGO, "Ayarlar", shPinscherFont, 52, Color.black, TextAnchor.MiddleCenter, false);

        Button musicToggleButton = SetupSettingsToggleButton(settingsCard.transform, "MusicToggleButton", buttonBase, buttonInlay,
            shPinscherFont, -190f, out Text musicToggleText);
        Button sfxToggleButton = SetupSettingsToggleButton(settingsCard.transform, "SfxToggleButton", buttonBase, buttonInlay,
            shPinscherFont, -320f, out Text sfxToggleText);
        // Sensible default label text for Edit-mode/Prefab preview - MainMenuController2D.RefreshToggleTexts()
        // corrects these immediately at runtime from the real saved GameAudioSettings2D values.
        musicToggleText.text = "Müzik: Açık";
        sfxToggleText.text = "Ses Efektleri: Açık";

        Button settingsCloseButton = SetupSimpleButton(settingsCard.transform, "SettingsCloseButton", buttonBase, buttonInlay,
            shPinscherFont, "Kapat", TopCenter, new Vector2(0f, -470f), new Vector2(320f, 110f));

        // ---------------- ResetConfirmationPanel ----------------
        GameObject resetPanel = FindOrCreateChild(safeAreaRoot.transform, "ResetConfirmationPanel");
        StretchFill(RectOf(resetPanel), 0f);

        GameObject resetBlocker = FindOrCreateChild(resetPanel.transform, "ModalBlocker");
        StretchFill(RectOf(resetBlocker), 0f);
        SetupImage(resetBlocker, null, true, new Color(0f, 0f, 0f, 0.55f));
        resetBlocker.transform.SetAsFirstSibling();

        GameObject confirmationCard = FindOrCreateChild(resetPanel.transform, "ConfirmationCard");
        SetAnchoredRect(RectOf(confirmationCard), Center, Center, Center, Vector2.zero, new Vector2(800f, 720f));
        SetupImage(confirmationCard, panelBase, false, Color.white);

        GameObject confirmationCardInset = FindOrCreateChild(confirmationCard.transform, "ConfirmationCardInset");
        StretchFill(RectOf(confirmationCardInset), 24f);
        SetupImage(confirmationCardInset, panelInlay, false, Color.white);
        confirmationCardInset.transform.SetAsFirstSibling();

        GameObject confirmationTitleBadge = FindOrCreateChild(confirmationCard.transform, "ConfirmationTitleBadge");
        SetAnchoredRect(RectOf(confirmationTitleBadge), TopCenter, TopCenter, TopCenter, new Vector2(0f, -36f), new Vector2(620f, 110f));
        SetupImage(confirmationTitleBadge, badgeBase, false, Color.white);
        GameObject confirmationTitleBadgeInset = FindOrCreateChild(confirmationTitleBadge.transform, "PackageDecoration");
        StretchFill(RectOf(confirmationTitleBadgeInset), 8f);
        SetupImage(confirmationTitleBadgeInset, badgeInlay, false, Color.white);
        confirmationTitleBadgeInset.transform.SetAsFirstSibling();
        GameObject confirmationTitleGO = FindOrCreateChild(confirmationTitleBadge.transform, "ConfirmationTitle");
        StretchFill(RectOf(confirmationTitleGO), 0f);
        SetupText(confirmationTitleGO, "İlerlemeyi Sıfırla", shPinscherFont, 44, Color.black, TextAnchor.MiddleCenter, true);

        GameObject confirmationTextGO = FindOrCreateChild(confirmationCard.transform, "ConfirmationText");
        SetAnchoredRect(RectOf(confirmationTextGO), TopCenter, TopCenter, TopCenter, new Vector2(0f, -180f), new Vector2(680f, 260f));
        SetupText(confirmationTextGO, "Tüm bölüm ilerlemesi ve kazanılan yıldızlar silinsin mi?", shPinscherFont, 36,
            Color.black, TextAnchor.MiddleCenter, true, bestFit: true, minSize: 28, maxSize: 36, lineSpacing: 1.1f);

        Button confirmResetButton = SetupSimpleButton(confirmationCard.transform, "ConfirmResetButton", buttonBase, buttonInlay,
            shPinscherFont, "Evet", TopCenter, new Vector2(-160f, -480f), new Vector2(300f, 110f));
        Button cancelResetButton = SetupSimpleButton(confirmationCard.transform, "CancelResetButton", buttonBase, buttonInlay,
            shPinscherFont, "Vazgeç", TopCenter, new Vector2(160f, -480f), new Vector2(300f, 110f));

        settingsPanel.SetActive(false);
        resetPanel.SetActive(false);

        // ---------------- MainMenuController2D ----------------
        GameObject controllerGO = GameObject.Find("MainMenuController2D");
        if (controllerGO == null)
        {
            controllerGO = new GameObject("MainMenuController2D");
        }
        MainMenuController2D controller = controllerGO.GetComponent<MainMenuController2D>();
        if (controller == null) controller = controllerGO.AddComponent<MainMenuController2D>();

        var so = new SerializedObject(controller);
        so.FindProperty("playButton").objectReferenceValue = playButton;
        so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        so.FindProperty("resetProgressButton").objectReferenceValue = resetProgressButton;
        so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
        so.FindProperty("resetConfirmationPanel").objectReferenceValue = resetPanel;
        so.FindProperty("musicToggleButton").objectReferenceValue = musicToggleButton;
        so.FindProperty("sfxToggleButton").objectReferenceValue = sfxToggleButton;
        so.FindProperty("musicToggleText").objectReferenceValue = musicToggleText;
        so.FindProperty("sfxToggleText").objectReferenceValue = sfxToggleText;
        so.FindProperty("settingsCloseButton").objectReferenceValue = settingsCloseButton;
        so.FindProperty("confirmResetButton").objectReferenceValue = confirmResetButton;
        so.FindProperty("cancelResetButton").objectReferenceValue = cancelResetButton;
        so.FindProperty("levelSelectSceneName").stringValue = LevelSelectSceneName;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        EnsureBuildSettings(logDetails);

        if (logDetails)
        {
            Debug.Log($"MainMenuSceneBuilder2D: {(sceneExists ? "updated" : "created")} '{ScenePath}'.\n" +
                $"  Background: '{backgroundSprite.name}' ({BackgroundPath}) - title already embedded, no separate GameTitle created.\n" +
                $"  PlayButton: '{playSprite.name}' ({PlaySpritePath}) - label embedded.\n" +
                $"  SettingsButton: '{settingsSprite.name}' ({SettingsSpritePath}) - label embedded.\n" +
                $"  ResetProgressButton: '{resetSprite.name}' ({ResetSpritePath}) - label embedded.\n" +
                $"  Font: '{shPinscherFont.name}' ({FontPath}) used for all created text.\n" +
                "  Hierarchy: Canvas > SafeAreaRoot > MainMenuBackground, ReadabilityOverlay, MainMenuContent > " +
                "MainMenuButtonArea (VerticalLayoutGroup, anchor 0.5/" + ButtonAreaVerticalAnchor + ") > " +
                "PrimaryButtonRow (HorizontalLayoutGroup) > PlayButton/SettingsButton, ResetProgressButton; " +
                "FullscreenButton; SettingsPanel > SettingsBlocker, SettingsCard > " +
                "SettingsCardInset, SettingsTitleBadge, MusicToggleButton, SfxToggleButton, SettingsCloseButton; " +
                "ResetConfirmationPanel > ModalBlocker, ConfirmationCard > ConfirmationCardInset, " +
                "ConfirmationTitleBadge, ConfirmationText, ConfirmResetButton, CancelResetButton.");
        }

        return true;
    }

    [MenuItem("YagmurRotasi2D/Rebind Main Menu Assets")]
    public static void RebindMenuCommand()
    {
        TryRebindAssets(true);
    }

    /// <summary>Updates only MainMenuBackground/PlayButton/SettingsButton/ResetProgressButton's sprites in the already-open MainMenuScene2D. Preserves listeners and any manually-adjusted RectTransform - never recreates the scene, never touches GameScene2D.</summary>
    public static bool TryRebindAssets(bool logDetails)
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "MainMenuScene2D")
        {
            Debug.LogError($"MainMenuSceneBuilder2D: active scene is '{activeScene.name}', not 'MainMenuScene2D'. " +
                "Open Assets/Scenes/MainMenuScene2D.unity first, then run this command again.");
            return false;
        }

        Sprite backgroundSprite = LoadFirstSprite(BackgroundPath);
        Sprite playSprite = LoadFirstSprite(PlaySpritePath);
        Sprite settingsSprite = LoadFirstSprite(SettingsSpritePath);
        Sprite resetSprite = LoadFirstSprite(ResetSpritePath);

        bool any = false;
        any |= RebindSprite("MainMenuBackground", backgroundSprite);
        any |= RebindSprite("PlayButton", playSprite);
        any |= RebindSprite("SettingsButton", settingsSprite);
        any |= RebindSprite("ResetProgressButton", resetSprite);

        if (any)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        if (logDetails)
        {
            Debug.Log($"MainMenuSceneBuilder2D: asset rebind complete (any changed: {any}). " +
                "RectTransforms and Button listeners were not touched.");
        }

        return any;
    }

    private static bool RebindSprite(string goName, Sprite sprite)
    {
        if (sprite == null) return false;
        GameObject go = GameObject.Find(goName);
        if (go == null) return false;
        var image = go.GetComponent<Image>();
        if (image == null) return false;
        image.sprite = sprite;
        EditorUtility.SetDirty(image);
        return true;
    }

    /// <summary>Phase 9B: delegates to the single shared WebBuildConfig2D.EnsureSceneOrder (was a divergent local copy that only knew about 2 of the 3 scenes - the Phase 9B audit found this drift had left LevelSelectScene2D out of Build Settings entirely).</summary>
    private static void EnsureBuildSettings(bool logDetails)
    {
        WebBuildConfig2D.EnsureSceneOrder(logDetails);
    }

    /// <summary>Finds a GameObject by name ANYWHERE in the currently open scene (not just direct children of a given parent, unlike FindOrCreateChild) and reparents it under newParent - the key to safely restructuring a hierarchy across builder versions without ever duplicating an existing button. Creates a fresh GameObject only if truly none exists yet (brand-new scene case).</summary>
    private static Button FindOrCreateAnywhere(string name, Transform newParent)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform));
        }

        if (go.transform.parent != newParent)
        {
            go.transform.SetParent(newParent, false);
        }

        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        return button;
    }

    /// <summary>Applies size/sprite/ColorTint button styling in place - never touches the GameObject's parent/sibling position (callers control that separately), so this is safe to re-run on an already-correctly-parented button.</summary>
    private static void ConfigureMainButton(GameObject go, Sprite sprite, Vector2 size)
    {
        RectOf(go).sizeDelta = size;

        Image image = SetupImage(go, sprite, true, Color.white);
        image.preserveAspect = false; // sizes above are chosen to match (or minimize distortion from) the sprite's own native aspect - see the size constants' doc comments.

        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        button.colors = colors;
    }

    private static Button SetupSimpleButton(Transform parent, string name, Sprite baseSprite, Sprite inlaySprite, Font font,
        string label, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = FindOrCreateChild(parent, name);
        SetAnchoredRect(RectOf(go), anchor, anchor, anchor, anchoredPos, size);

        Image image = SetupImage(go, baseSprite, true, Color.white);
        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        button.targetGraphic = image;

        if (inlaySprite != null)
        {
            GameObject inset = FindOrCreateChild(go.transform, "PackageDecoration");
            StretchFill(RectOf(inset), 8f);
            SetupImage(inset, inlaySprite, false, Color.white);
            inset.transform.SetAsFirstSibling();
        }

        GameObject labelGO = FindOrCreateChild(go.transform, "Label");
        StretchFill(RectOf(labelGO), 0f);
        SetupText(labelGO, label, font, 40, Color.white, TextAnchor.MiddleCenter, false);

        return button;
    }

    private static Button SetupSettingsToggleButton(Transform parent, string name, Sprite baseSprite, Sprite inlaySprite,
        Font font, float y, out Text label)
    {
        Button button = SetupSimpleButton(parent, name, baseSprite, inlaySprite, font, "", TopCenter, new Vector2(0f, y), new Vector2(620f, 110f));
        label = button.transform.Find("Label").GetComponent<Text>();
        return button;
    }

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

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
            Debug.LogWarning($"MainMenuSceneBuilder2D: no sprite found at '{texturePath}'.");
        }
        return sprite;
    }
}
