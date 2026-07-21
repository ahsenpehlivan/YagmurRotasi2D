using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Builds/updates the dedicated, self-contained in-game menu prefab
/// (Assets/Prefabs2D/UI/InGameMenu2D.prefab), entirely out of the shared UI
/// package (Assets/Art2D/FinalSprites/UI/) and SHPinscher-Regular11 - the same
/// approach used for SuccessPanel2D (Phase 7E.6), never patching an existing
/// scene hierarchy in place. Idempotent: if the prefab already exists, its
/// contents are loaded via PrefabUtility.LoadPrefabContents and every object is
/// found-or-created by name, so re-running never duplicates anything.
/// </summary>
public static class InGameMenuPrefabBuilder
{
    public const string PrefabFolder = "Assets/Prefabs2D/UI";
    public const string PrefabPath = PrefabFolder + "/InGameMenu2D.prefab";

    private const string UiFolder = "Assets/Art2D/FinalSprites/UI";
    private const string FontPath = "Assets/SHPinscher-Regular11/SHPinscher-Regular.otf";

    private const float MainPanelWidth = 700f;
    private const float MainPanelHeight = 1000f;
    private const float MainPanelInset = 28f;

    private const float TitleBadgeWidth = 480f;
    private const float TitleBadgeHeight = 120f;
    private const float TitleBadgeInset = 10f;
    private const float TitleTopMargin = 40f;

    private const float ButtonWidth = 500f;
    private const float ButtonHeight = 120f;
    private const float ButtonInset = 10f;
    private const float FirstButtonGap = 30f;
    private const float ButtonGap = 24f;

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

    [MenuItem("YagmurRotasi2D/Build In-Game Menu Prefab")]
    public static void BuildMenuCommand()
    {
        TryBuildPrefab(true);
    }

    public static bool TryBuildPrefab(bool logDetails)
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs2D", "UI");
        }

        Font shPinscherFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        Sprite panelBase = LoadFirstSprite(UiFolder + "/Panels/tan.png");
        Sprite panelInlay = LoadFirstSprite(UiFolder + "/Panels/tan_inlay.png");
        Sprite badgeWhite = LoadFirstSprite(UiFolder + "/Badges/white.png");
        Sprite badgeWhiteInlay = LoadFirstSprite(UiFolder + "/Badges/white_inlay.png");
        Sprite badgeGrey = LoadFirstSprite(UiFolder + "/Badges/grey.png");
        Sprite buttonBase = LoadFirstSprite(UiFolder + "/Buttons/brown.png");
        Sprite buttonInlay = LoadFirstSprite(UiFolder + "/Buttons/brown_inlay.png");
        Sprite buttonPressed = LoadFirstSprite(UiFolder + "/Buttons/brown_pressed.png");

        if (shPinscherFont == null || panelBase == null || badgeWhite == null || badgeGrey == null || buttonBase == null)
        {
            Debug.LogError("InGameMenuPrefabBuilder: one or more required assets could not be loaded (see warnings above). Aborting.");
            return false;
        }

        bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        GameObject prefabRoot = prefabExists
            ? PrefabUtility.LoadPrefabContents(PrefabPath)
            : new GameObject("InGameMenu2D", typeof(RectTransform));

        var log = new List<string>();
        bool success = false;

        try
        {
            StretchFill(RectOf(prefabRoot), 0f);

            var canvasGroup = prefabRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = prefabRoot.AddComponent<CanvasGroup>();

            // ---------------- ModalBlocker ----------------
            GameObject modalBlocker = FindOrCreateChild(prefabRoot.transform, "ModalBlocker");
            StretchFill(RectOf(modalBlocker), 0f);
            SetupImage(modalBlocker, null, true, new Color(15f / 255f, 12f / 255f, 10f / 255f, 0.65f));
            log.Add("ModalBlocker: plain color (15,12,10,0.65), full-screen, raycastTarget=true.");

            // ---------------- PanelShadow ----------------
            GameObject panelShadow = FindOrCreateChild(prefabRoot.transform, "PanelShadow");
            SetAnchoredRect(RectOf(panelShadow), Center, Center, Center, new Vector2(10f, -12f), new Vector2(MainPanelWidth, MainPanelHeight));
            SetupImage(panelShadow, badgeGrey, false, Color.white);
            panelShadow.transform.SetSiblingIndex(1);
            log.Add($"PanelShadow: '{badgeGrey.name}' ({UiFolder}/Badges/grey.png), offset (10,-12), raycastTarget=false.");

            // ---------------- MainPanel ----------------
            GameObject mainPanel = FindOrCreateChild(prefabRoot.transform, "MainPanel");
            SetAnchoredRect(RectOf(mainPanel), Center, Center, Center, Vector2.zero, new Vector2(MainPanelWidth, MainPanelHeight));
            SetupImage(mainPanel, panelBase, false, Color.white);
            log.Add($"MainPanel: '{panelBase.name}' ({UiFolder}/Panels/tan.png), size ({MainPanelWidth}x{MainPanelHeight}).");

            GameObject mainPanelInset = FindOrCreateChild(mainPanel.transform, "MainPanelInset");
            StretchFill(RectOf(mainPanelInset), MainPanelInset);
            SetupImage(mainPanelInset, panelInlay, false, Color.white);
            mainPanelInset.transform.SetAsFirstSibling();

            // ---------------- MainMenuPage ----------------
            GameObject mainMenuPage = FindOrCreateChild(mainPanel.transform, "MainMenuPage");
            StretchFill(RectOf(mainMenuPage), 0f);

            GameObject titleBadge = FindOrCreateChild(mainMenuPage.transform, "TitleBadge");
            SetAnchoredRect(RectOf(titleBadge), TopCenter, TopCenter, TopCenter, new Vector2(0f, -TitleTopMargin), new Vector2(TitleBadgeWidth, TitleBadgeHeight));
            SetupImage(titleBadge, badgeWhite, false, Color.white);
            GameObject titleBadgeInset = FindOrCreateChild(titleBadge.transform, "TitleBadgeInset");
            StretchFill(RectOf(titleBadgeInset), TitleBadgeInset);
            SetupImage(titleBadgeInset, badgeWhiteInlay, false, Color.white);
            titleBadgeInset.transform.SetAsFirstSibling();
            GameObject titleTextGO = FindOrCreateChild(titleBadge.transform, "TitleText");
            StretchFill(RectOf(titleTextGO), 0f);
            SetupText(titleTextGO, "Menü", shPinscherFont, 56, Color.black, TextAnchor.MiddleCenter, false);

            float y1 = -(TitleTopMargin + TitleBadgeHeight + FirstButtonGap);
            float y2 = y1 - (ButtonHeight + ButtonGap);
            float y3 = y2 - (ButtonHeight + ButtonGap);
            float y4 = y3 - (ButtonHeight + ButtonGap);

            Button continueButton = SetupMenuButton(mainMenuPage.transform, "ContinueButton", buttonBase, buttonInlay, buttonPressed,
                shPinscherFont, "Devam Et", y1);
            Button restartButton = SetupMenuButton(mainMenuPage.transform, "RestartButton", buttonBase, buttonInlay, buttonPressed,
                shPinscherFont, "Bölümü Yeniden Başlat", y2);
            Button settingsButton = SetupMenuButton(mainMenuPage.transform, "SettingsButton", buttonBase, buttonInlay, buttonPressed,
                shPinscherFont, "Ayarlar", y3);
            Button mainMenuButton = SetupMenuButton(mainMenuPage.transform, "MainMenuButton", buttonBase, buttonInlay, buttonPressed,
                shPinscherFont, "Ana Menüye Dön", y4);

            log.Add("MainMenuPage: TitleBadge='Menü', buttons=ContinueButton/RestartButton/SettingsButton/MainMenuButton " +
                $"(base='{buttonBase.name}', inset='{buttonInlay?.name}', pressed='{buttonPressed?.name}').");

            // ---------------- SettingsPage ----------------
            GameObject settingsPage = FindOrCreateChild(mainPanel.transform, "SettingsPage");
            StretchFill(RectOf(settingsPage), 0f);

            GameObject settingsTitleBadge = FindOrCreateChild(settingsPage.transform, "SettingsTitleBadge");
            SetAnchoredRect(RectOf(settingsTitleBadge), TopCenter, TopCenter, TopCenter, new Vector2(0f, -TitleTopMargin), new Vector2(TitleBadgeWidth, TitleBadgeHeight));
            SetupImage(settingsTitleBadge, badgeWhite, false, Color.white);
            GameObject settingsTitleInset = FindOrCreateChild(settingsTitleBadge.transform, "SettingsTitleInset");
            StretchFill(RectOf(settingsTitleInset), TitleBadgeInset);
            SetupImage(settingsTitleInset, badgeWhiteInlay, false, Color.white);
            settingsTitleInset.transform.SetAsFirstSibling();
            GameObject settingsTitleTextGO = FindOrCreateChild(settingsTitleBadge.transform, "SettingsTitleText");
            StretchFill(RectOf(settingsTitleTextGO), 0f);
            SetupText(settingsTitleTextGO, "Ayarlar", shPinscherFont, 56, Color.black, TextAnchor.MiddleCenter, false);

            Button musicToggleButton = SetupMenuButton(settingsPage.transform, "MusicToggleButton", buttonBase, buttonInlay, buttonPressed,
                shPinscherFont, "Müzik: Açık", y1);
            Text musicToggleText = musicToggleButton.transform.Find("Label").GetComponent<Text>();

            Button sfxToggleButton = SetupMenuButton(settingsPage.transform, "SfxToggleButton", buttonBase, buttonInlay, buttonPressed,
                shPinscherFont, "Ses Efektleri: Açık", y2);
            Text sfxToggleText = sfxToggleButton.transform.Find("Label").GetComponent<Text>();

            Button settingsBackButton = SetupMenuButton(settingsPage.transform, "SettingsBackButton", buttonBase, buttonInlay, buttonPressed,
                shPinscherFont, "Geri", y3);

            log.Add("SettingsPage: SettingsTitleBadge='Ayarlar', buttons=MusicToggleButton/SfxToggleButton/SettingsBackButton.");

            mainMenuPage.SetActive(true);
            settingsPage.SetActive(false);

            // ---------------- InGameMenuView2D wiring ----------------
            var view = prefabRoot.GetComponent<InGameMenuView2D>();
            if (view == null) view = prefabRoot.AddComponent<InGameMenuView2D>();

            var viewSO = new SerializedObject(view);
            viewSO.FindProperty("root").objectReferenceValue = prefabRoot;
            viewSO.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            viewSO.FindProperty("continueButton").objectReferenceValue = continueButton;
            viewSO.FindProperty("restartButton").objectReferenceValue = restartButton;
            viewSO.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            viewSO.FindProperty("mainMenuButton").objectReferenceValue = mainMenuButton;
            viewSO.FindProperty("mainMenuPage").objectReferenceValue = mainMenuPage;
            viewSO.FindProperty("settingsPage").objectReferenceValue = settingsPage;
            viewSO.FindProperty("musicToggleButton").objectReferenceValue = musicToggleButton;
            viewSO.FindProperty("sfxToggleButton").objectReferenceValue = sfxToggleButton;
            viewSO.FindProperty("musicToggleText").objectReferenceValue = musicToggleText;
            viewSO.FindProperty("sfxToggleText").objectReferenceValue = sfxToggleText;
            viewSO.FindProperty("settingsBackButton").objectReferenceValue = settingsBackButton;
            viewSO.ApplyModifiedPropertiesWithoutUndo();

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            success = true;

            if (logDetails)
            {
                Debug.Log($"InGameMenuPrefabBuilder: {(prefabExists ? "updated" : "created")} '{PrefabPath}'.\n" +
                    "Hierarchy: InGameMenu2D > ModalBlocker, PanelShadow, MainPanel > MainPanelInset, " +
                    "MainMenuPage > TitleBadge(+inset+text), ContinueButton, RestartButton, SettingsButton, MainMenuButton; " +
                    "SettingsPage > SettingsTitleBadge(+inset+text), MusicToggleButton, SfxToggleButton, SettingsBackButton.\n" +
                    string.Join("\n", log));
            }
        }
        finally
        {
            if (prefabExists)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            else
            {
                Object.DestroyImmediate(prefabRoot);
            }
        }

        return success;
    }

    [MenuItem("YagmurRotasi2D/Install In-Game Menu")]
    public static void InstallMenuCommand()
    {
        TryInstallIntoScene(true);
    }

    /// <summary>Instantiates exactly one InGameMenu2D.prefab under SafeAreaRoot/InGameMenuHost, wires it into UIManager2D.inGameMenu, hides it, and reuses (never recreates/moves) the existing top MenuButton. Idempotent - finds "DedicatedInGameMenu" by name and reuses it if it already exists.</summary>
    public static bool TryInstallIntoScene(bool logDetails)
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"InGameMenuPrefabBuilder: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return false;
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"InGameMenuPrefabBuilder: prefab not found at '{PrefabPath}'. " +
                "Run 'YagmurRotasi2D > Build In-Game Menu Prefab' first.");
            return false;
        }

        GameObject safeAreaRoot = GameObject.Find("SafeAreaRoot");
        if (safeAreaRoot == null)
        {
            Debug.LogError("InGameMenuPrefabBuilder: 'SafeAreaRoot' not found in the active scene.");
            return false;
        }

        GameObject host = FindOrCreateChild(safeAreaRoot.transform, "InGameMenuHost");
        StretchFill(RectOf(host), 0f);
        // Rendered above everything else already in SafeAreaRoot (TopHUD, StatusArea,
        // BottomControls, InfoPanel, SuccessPanelHost) so its ModalBlocker correctly
        // intercepts clicks meant for gameplay/HUD buttons underneath.
        host.transform.SetAsLastSibling();

        GameObject existingInstance = GameObject.Find("DedicatedInGameMenu");
        GameObject instance;
        bool isNew = existingInstance == null;

        if (isNew)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, host.transform);
            instance.name = "DedicatedInGameMenu";
        }
        else
        {
            instance = existingInstance;
            if (instance.transform.parent != host.transform)
            {
                instance.transform.SetParent(host.transform, false);
            }
        }

        StretchFill(RectOf(instance), 0f);

        InGameMenuView2D view = instance.GetComponent<InGameMenuView2D>();
        if (view == null)
        {
            Debug.LogError("InGameMenuPrefabBuilder: 'DedicatedInGameMenu' has no InGameMenuView2D component - " +
                "the prefab may be out of date. Re-run 'Build In-Game Menu Prefab' first.");
            return false;
        }

        // Hidden initially - uses the component's own Hide() so root activation
        // and CanvasGroup state stay consistent with how it will be hidden at runtime.
        view.Hide();

        GameObject menuButtonGO = GameObject.Find("MenuButton");
        UIManager2D uiManager = Object.FindFirstObjectByType<UIManager2D>();
        bool wired = false;
        if (uiManager != null)
        {
            var so = new SerializedObject(uiManager);
            so.FindProperty("inGameMenu").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(uiManager);
            wired = true;
        }
        else if (logDetails)
        {
            Debug.LogWarning("InGameMenuPrefabBuilder: no UIManager2D found in the active scene; inGameMenu was not wired.");
        }

        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        if (logDetails)
        {
            Debug.Log("InGameMenuPrefabBuilder: install complete.\n" +
                $"  Instance: {(isNew ? "created" : "reused")} 'SafeAreaRoot/InGameMenuHost/DedicatedInGameMenu'\n" +
                $"  Existing MenuButton found: {menuButtonGO != null} (Transform not touched - listener wiring lives in UIManager2D.Awake())\n" +
                $"  UIManager2D.inGameMenu wired: {wired}\n" +
                "  Hidden initially (CanvasGroup alpha=0, interactable=false, blocksRaycasts=false, root inactive).");
        }

        return true;
    }

    private static Button SetupMenuButton(Transform parent, string name, Sprite baseSprite, Sprite inlaySprite, Sprite pressedSprite,
        Font font, string label, float y)
    {
        GameObject go = FindOrCreateChild(parent, name);
        SetAnchoredRect(RectOf(go), TopCenter, TopCenter, TopCenter, new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));

        Image image = SetupImage(go, baseSprite, true, Color.white);
        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        button.targetGraphic = image;

        if (pressedSprite != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.pressedSprite = pressedSprite;
            state.highlightedSprite = baseSprite;
            state.selectedSprite = baseSprite;
            state.disabledSprite = baseSprite;
            button.spriteState = state;
        }

        if (inlaySprite != null)
        {
            GameObject inset = FindOrCreateChild(go.transform, "ButtonInset");
            StretchFill(RectOf(inset), ButtonInset);
            SetupImage(inset, inlaySprite, false, Color.white);
            inset.transform.SetAsFirstSibling();
        }

        GameObject labelGO = FindOrCreateChild(go.transform, "Label");
        StretchFill(RectOf(labelGO), 0f);
        SetupText(labelGO, label, font, 34, Color.white, TextAnchor.MiddleCenter, true,
            bestFit: true, minSize: 24, maxSize: 34);

        return button;
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
            Debug.LogWarning($"InGameMenuPrefabBuilder: no sprite found at '{texturePath}'.");
        }
        return sprite;
    }
}
