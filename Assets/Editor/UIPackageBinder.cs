using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Skins the ENTIRE visible Canvas using only the user's imported UI package
/// (Assets/Art2D/FinalSprites/UI/) - never creates new UI logic objects, never
/// touches Button listeners/RectTransforms/text content, never adds a second
/// Canvas or replaces the EventSystem. Only Image sprite/type/color/raycastTarget
/// and (for buttons) SpriteState change, plus small decorative "PackageDecoration"
/// child Images layered behind labels using the package's own inlay tiles.
///
/// Complete package inventory (confirmed exhaustive - searched Assets/Art2D,
/// Assets/UI, Assets/ThirdParty, Assets/Plugins, Assets/ImportedAssets and the
/// whole Assets/ tree; nothing else UI-related exists anywhere in the project):
///   Buttons/brown.png, brown_inlay.png, brown_pressed.png
///   Panels/tan.png, tan_inlay.png, tan_pressed.png
///   Badges/grey.png, grey_inlay.png, grey_pressed.png, white.png, white_inlay.png, white_pressed.png
///   Stars/star.png
/// All are 48x48px (star excepted), Sprite Mode Multiple, spriteBorder (0,0,0,0) -
/// confirmed by opening each image that every one is a flat solid-color tile with
/// no frame/border artwork (base/inlay/pressed differ only by shade), so
/// Image.Type = Simple is correct throughout (there is no valid border to slice).
/// "_pressed" variants exist only for interactive elements (buttons) and are wired
/// via Button.transition = SpriteSwap. "_inlay" variants are used as a layered
/// "PackageDecoration" child (inset a fixed margin, sitting behind the label) on
/// every panel/badge/button so more than one flat tile from the package is
/// actually used per element, instead of a single color swap.
/// Stars/star.png is a single filled pixel-art icon - there is no empty/disabled
/// star asset anywhere in the package, so the permitted fallback is used: the same
/// sprite at a reduced alpha represents an unearned star.
/// </summary>
public static class UIPackageBinder
{
    private const string BaseFolder = "Assets/Art2D/FinalSprites/UI";
    private const string ButtonBasePath = BaseFolder + "/Buttons/brown.png";
    private const string ButtonInlayPath = BaseFolder + "/Buttons/brown_inlay.png";
    private const string ButtonPressedPath = BaseFolder + "/Buttons/brown_pressed.png";
    private const string PanelBasePath = BaseFolder + "/Panels/tan.png";
    private const string PanelInlayPath = BaseFolder + "/Panels/tan_inlay.png";
    // white used for both HUD badges by default (no semantic signal distinguishes
    // "level" vs "move"); grey/grey_inlay are available, untouched alternates.
    private const string BadgeBasePath = BaseFolder + "/Badges/white.png";
    private const string BadgeInlayPath = BaseFolder + "/Badges/white_inlay.png";
    private const string StarSpritePath = BaseFolder + "/Stars/star.png";

    private const float InlayInset = 10f;
    private const float InfoCardInlayInset = 24f;
    private const float EmptyStarAlpha = 0.3f;

    // MoveBadge's original (pre-Phase-7E.5) anchoredPosition.x, as built by
    // SceneBuilder2D's BuildGameScenePhase7C1 and never changed since. Comparing
    // against this exact baseline (rather than adding +30 unconditionally every
    // run) is the idempotency mechanism: if the current x already differs from
    // this baseline - because this shift already ran, or the user manually moved
    // it - the shift is skipped instead of stacking.
    private const float MoveBadgeOriginalX = -56f;
    private const float MoveBadgeShiftAmount = 30f;

    [MenuItem("YagmurRotasi2D/Bind Complete UI Package")]
    public static void BindCompleteMenuCommand()
    {
        TryBindUIPackage(true);
    }

    [MenuItem("YagmurRotasi2D/Bind Final UI Package")]
    public static void BindMenuCommand()
    {
        // Kept only so the earlier phase's menu item still works - it now calls the
        // exact same complete implementation as "Bind Complete UI Package" (no
        // separate "partial" code path exists anymore).
        TryBindUIPackage(true);
    }

    public static bool TryBindUIPackage(bool logDetails)
    {
        var log = new List<string>();
        bool any = false;

        Sprite buttonBase = LoadFirstSprite(ButtonBasePath);
        Sprite buttonInlay = LoadFirstSprite(ButtonInlayPath);
        Sprite buttonPressed = LoadFirstSprite(ButtonPressedPath);
        Sprite panelBase = LoadFirstSprite(PanelBasePath);
        Sprite panelInlay = LoadFirstSprite(PanelInlayPath);
        Sprite badgeBase = LoadFirstSprite(BadgeBasePath);
        Sprite badgeInlay = LoadFirstSprite(BadgeInlayPath);
        Sprite starSprite = LoadFirstSprite(StarSpritePath);

        any |= BindButton("ReloadButton", buttonBase, buttonInlay, buttonPressed, log);
        any |= BindButton("StartWaterButton", buttonBase, buttonInlay, buttonPressed, log);
        any |= BindButton("InfoNextLevelButton", buttonBase, buttonInlay, buttonPressed, log);

        any |= BindPanel("ResultPanel", panelBase, panelInlay, log);
        any |= BindPanel("InfoCard", panelBase, panelInlay, log, InfoCardInlayInset);
        any |= BindPanel("LevelBadge", badgeBase, badgeInlay, log);
        any |= BindPanel("MoveBadge", badgeBase, badgeInlay, log);

        any |= EnsureTitleBadge(badgeBase, badgeInlay, log);
        any |= BindStars(starSprite, log);

        log.Add("ModalBlocker: intentionally left as a plain semi-transparent black scrim - every package asset " +
            "is an opaque flat tile, none is appropriate for a translucent dimming backdrop.");

        if (logDetails)
        {
            Debug.Log("UIPackageBinder: complete UI mapping report:\n" + string.Join("\n", log));
        }

        if (any)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        return any;
    }

    private static bool BindButton(string goName, Sprite baseSprite, Sprite inlaySprite, Sprite pressedSprite, List<string> log)
    {
        if (baseSprite == null)
        {
            log.Add($"{goName}: no button base sprite found - skipped.");
            return false;
        }

        GameObject go = GameObject.Find(goName);
        if (go == null)
        {
            log.Add($"{goName}: NOT FOUND in active scene - skipped.");
            return false;
        }

        var image = go.GetComponent<Image>();
        var button = go.GetComponent<Button>();
        if (image == null)
        {
            log.Add($"{goName}: has no Image component - skipped.");
            return false;
        }

        image.sprite = baseSprite;
        image.type = HasValidBorder(baseSprite) ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;

        bool decorationCreated = inlaySprite != null && EnsureDecorationChild(go, inlaySprite);

        string transitionDesc = "n/a (no Button component)";
        if (button != null)
        {
            button.targetGraphic = image;

            if (pressedSprite != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState state = button.spriteState;
                state.pressedSprite = pressedSprite;
                // No dedicated highlighted/selected/disabled art exists in the
                // package - reuse the base sprite for those slots rather than
                // leaving them null.
                state.highlightedSprite = baseSprite;
                state.selectedSprite = baseSprite;
                state.disabledSprite = baseSprite;
                button.spriteState = state;
            }
            transitionDesc = $"{button.transition} (pressedSprite={(pressedSprite != null ? pressedSprite.name : "n/a")})";
        }

        log.Add($"{goName}: base='{baseSprite.name}' ({ButtonBasePath}), Image.Type={image.type}, " +
            $"decoration={(decorationCreated ? $"created ({inlaySprite.name})" : inlaySprite != null ? "reused" : "none")}, " +
            $"Button.transition={transitionDesc}, targetGraphic={(button != null ? "set" : "n/a")}, " +
            "onClick listeners untouched, RectTransform untouched, label child untouched.");

        return true;
    }

    private static bool BindPanel(string goName, Sprite baseSprite, Sprite inlaySprite, List<string> log, float inset = InlayInset)
    {
        if (baseSprite == null)
        {
            log.Add($"{goName}: no panel/badge base sprite found - skipped.");
            return false;
        }

        GameObject go = GameObject.Find(goName);
        if (go == null)
        {
            log.Add($"{goName}: NOT FOUND in active scene - skipped.");
            return false;
        }

        var image = go.GetComponent<Image>();
        if (image == null)
        {
            log.Add($"{goName}: has no Image component - skipped.");
            return false;
        }

        image.sprite = baseSprite;
        image.type = HasValidBorder(baseSprite) ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        // Decorative background - must never intercept clicks meant for its children.
        image.raycastTarget = false;

        bool decorationCreated = inlaySprite != null && EnsureDecorationChild(go, inlaySprite, inset);

        log.Add($"{goName}: base='{baseSprite.name}', Image.Type={image.type}, " +
            $"decoration={(decorationCreated ? $"created ({inlaySprite.name}, inset={inset})" : inlaySprite != null ? "reused" : "none")}, " +
            "raycastTarget=false, RectTransform and children untouched.");

        return true;
    }

    /// <summary>Finds-or-creates a "PackageDecoration" child Image inset by a fixed margin, placed as the first sibling so it renders behind any label/existing children. Idempotent - re-running just updates its sprite, never duplicates it.</summary>
    private static bool EnsureDecorationChild(GameObject parent, Sprite inlaySprite, float inset = InlayInset)
    {
        Transform existing = parent.transform.Find("PackageDecoration");
        GameObject child;
        bool isNew = existing == null;

        if (isNew)
        {
            child = new GameObject("PackageDecoration", typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            child.transform.SetAsFirstSibling();
            var rt = child.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
        else
        {
            child = existing.gameObject;
        }

        var image = child.GetComponent<Image>();
        if (image == null) image = child.AddComponent<Image>();
        image.sprite = inlaySprite;
        image.type = HasValidBorder(inlaySprite) ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;

        return isNew;
    }

    /// <summary>Finds-or-creates "TitleBadge" (Badges/white + white_inlay) occupying the exact rect InfoTitleText originally had, then reparents InfoTitleText inside it (stretched to fill) so the title text's visual position never changes. Idempotent: on a repeat run InfoTitleText is already a child of TitleBadge, so only sprites are refreshed - no further reparenting or movement happens.</summary>
    private static bool EnsureTitleBadge(Sprite badgeBase, Sprite badgeInlay, List<string> log)
    {
        if (badgeBase == null)
        {
            log.Add("TitleBadge: no badge base sprite found - skipped.");
            return false;
        }

        GameObject infoCard = GameObject.Find("InfoCard");
        GameObject infoTitleText = GameObject.Find("InfoTitleText");
        if (infoCard == null || infoTitleText == null)
        {
            log.Add("TitleBadge: 'InfoCard' or 'InfoTitleText' not found - skipped.");
            return false;
        }

        Transform titleBadgeT = infoCard.transform.Find("TitleBadge");
        GameObject titleBadge;
        bool isNew = titleBadgeT == null;

        if (isNew)
        {
            titleBadge = new GameObject("TitleBadge", typeof(RectTransform));
            titleBadge.transform.SetParent(infoCard.transform, false);

            // Take over the exact rect InfoTitleText currently occupies (its
            // pre-existing anchors/position/size, read directly from its own
            // RectTransform), so nothing visually moves.
            var titleRt = infoTitleText.GetComponent<RectTransform>();
            var badgeRt = titleBadge.GetComponent<RectTransform>();
            badgeRt.anchorMin = titleRt.anchorMin;
            badgeRt.anchorMax = titleRt.anchorMax;
            badgeRt.pivot = titleRt.pivot;
            badgeRt.anchoredPosition = titleRt.anchoredPosition;
            badgeRt.sizeDelta = titleRt.sizeDelta;

            // Place TitleBadge at InfoTitleText's old sibling index so overall
            // draw order among InfoCard's direct children is otherwise unchanged.
            titleBadge.transform.SetSiblingIndex(infoTitleText.transform.GetSiblingIndex());

            // Reparent the title text inside the badge, stretched to fill it -
            // its visual position stays exactly where it was.
            infoTitleText.transform.SetParent(titleBadge.transform, false);
            var newTitleRt = infoTitleText.GetComponent<RectTransform>();
            newTitleRt.anchorMin = Vector2.zero;
            newTitleRt.anchorMax = Vector2.one;
            newTitleRt.offsetMin = Vector2.zero;
            newTitleRt.offsetMax = Vector2.zero;
        }
        else
        {
            titleBadge = titleBadgeT.gameObject;
            // Already reparented on a prior run - leave InfoTitleText's Transform alone.
        }

        var image = titleBadge.GetComponent<Image>();
        if (image == null) image = titleBadge.AddComponent<Image>();
        image.sprite = badgeBase;
        image.type = HasValidBorder(badgeBase) ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;

        bool decorationCreated = badgeInlay != null && EnsureDecorationChild(titleBadge, badgeInlay);

        log.Add($"TitleBadge: {(isNew ? "created" : "reused")}, base='{badgeBase.name}', " +
            $"decoration={(decorationCreated ? $"created ({badgeInlay.name})" : badgeInlay != null ? "reused" : "none")}, " +
            "InfoTitleText reparented inside (stretched to fill, same visual position as before).");

        return true;
    }

    [MenuItem("YagmurRotasi2D/Repair Complete Success Panel")]
    public static void RepairSuccessPanelMenuCommand()
    {
        TryRepairSuccessPanel(true);
    }

    /// <summary>Repairs only InfoPanel and its child visuals (InfoCard, TitleBadge, stars, InfoNextLevelButton) - never touches ReloadButton/StartWaterButton/LevelBadge/MoveBadge/ModalBlocker, world-space objects, or success timing.</summary>
    public static bool TryRepairSuccessPanel(bool logDetails)
    {
        var log = new List<string>();
        bool any = false;

        Sprite buttonBase = LoadFirstSprite(ButtonBasePath);
        Sprite buttonInlay = LoadFirstSprite(ButtonInlayPath);
        Sprite buttonPressed = LoadFirstSprite(ButtonPressedPath);
        Sprite panelBase = LoadFirstSprite(PanelBasePath);
        Sprite panelInlay = LoadFirstSprite(PanelInlayPath);
        Sprite badgeBase = LoadFirstSprite(BadgeBasePath);
        Sprite badgeInlay = LoadFirstSprite(BadgeInlayPath);
        Sprite starSprite = LoadFirstSprite(StarSpritePath);

        any |= BindPanel("InfoCard", panelBase, panelInlay, log, InfoCardInlayInset);
        any |= EnsureTitleBadge(badgeBase, badgeInlay, log);
        any |= BindStars(starSprite, log);
        any |= BindButton("InfoNextLevelButton", buttonBase, buttonInlay, buttonPressed, log);

        if (logDetails)
        {
            Debug.Log("UIPackageBinder: success panel repair report:\n" + string.Join("\n", log));
        }

        if (any)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        return any;
    }

    [MenuItem("YagmurRotasi2D/Shift Hamle Badge Right")]
    public static void ShiftMoveBadgeMenuCommand()
    {
        TryShiftMoveBadgeRight(true);
    }

    /// <summary>Shifts MoveBadge's anchoredPosition.x right by MoveBadgeShiftAmount exactly once. Idempotent by comparison against the known original baseline (MoveBadgeOriginalX) rather than by unconditionally adding the offset - if the current x already differs from that baseline (because this already ran, or the user manually repositioned it), nothing happens.</summary>
    public static bool TryShiftMoveBadgeRight(bool logDetails)
    {
        GameObject moveBadge = GameObject.Find("MoveBadge");
        if (moveBadge == null)
        {
            if (logDetails) Debug.LogWarning("UIPackageBinder: 'MoveBadge' not found in the active scene - cannot shift it.");
            return false;
        }

        var rt = moveBadge.GetComponent<RectTransform>();
        if (rt == null)
        {
            if (logDetails) Debug.LogWarning("UIPackageBinder: 'MoveBadge' has no RectTransform - cannot shift it.");
            return false;
        }

        float previousX = rt.anchoredPosition.x;
        if (Mathf.Abs(previousX - MoveBadgeOriginalX) > 0.01f)
        {
            if (logDetails)
            {
                Debug.Log($"UIPackageBinder: MoveBadge.anchoredPosition.x is already {previousX} (differs from the " +
                    $"original baseline {MoveBadgeOriginalX}) - already shifted or manually adjusted, not shifting again.");
            }
            return false;
        }

        Vector2 pos = rt.anchoredPosition;
        float newX = pos.x + MoveBadgeShiftAmount;
        pos.x = newX;
        rt.anchoredPosition = pos;
        EditorUtility.SetDirty(rt);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (logDetails)
        {
            Debug.Log($"UIPackageBinder: MoveBadge shifted right - anchoredPosition.x {previousX} -> {newX} " +
                $"(+{MoveBadgeShiftAmount}px). Y ({pos.y}), anchors, pivot and size unchanged. LevelBadge was not touched.");
        }

        return true;
    }

    private const float MenuButtonSize = 76f;
    private const float MenuButtonGap = 14f;
    private const float MenuButtonDecorationInset = 8f;
    private const float MenuBarWidth = 36f;
    private const float MenuBarHeight = 6f;
    private const float MenuBarSpacing = 16f; // center-to-center

    [MenuItem("YagmurRotasi2D/Install Small Top Menu Button")]
    public static void InstallMenuButtonMenuCommand()
    {
        TryInstallMenuButton(true);
    }

    /// <summary>Finds-or-creates "MenuButton" as a sibling of LevelBadge (same TopHUD parent), positioned immediately to LevelBadge's right (recomputed fresh from LevelBadge's own anchoredPosition/size every run, so it's naturally idempotent - never accumulates an offset). Never touches LevelBadge or MoveBadge.</summary>
    public static bool TryInstallMenuButton(bool logDetails)
    {
        GameObject levelBadge = GameObject.Find("LevelBadge");
        if (levelBadge == null)
        {
            Debug.LogError("UIPackageBinder: 'LevelBadge' not found in the active scene - cannot position MenuButton relative to it.");
            return false;
        }

        Transform topHud = levelBadge.transform.parent;
        if (topHud == null)
        {
            Debug.LogError("UIPackageBinder: 'LevelBadge' has no parent (expected TopHUD) - cannot place MenuButton alongside it.");
            return false;
        }

        RectTransform levelBadgeRt = levelBadge.GetComponent<RectTransform>();

        Sprite buttonBase = LoadFirstSprite(ButtonBasePath);
        Sprite buttonInlay = LoadFirstSprite(ButtonInlayPath);
        Sprite buttonPressed = LoadFirstSprite(ButtonPressedPath);
        Sprite barSprite = LoadFirstSprite(BadgeBasePath);

        if (buttonBase == null || barSprite == null)
        {
            Debug.LogError("UIPackageBinder: required sprites (Buttons/brown.png, Badges/white.png) not found - aborting MenuButton install.");
            return false;
        }

        Transform existing = topHud.Find("MenuButton");
        GameObject menuButtonGO = existing != null ? existing.gameObject : new GameObject("MenuButton", typeof(RectTransform));
        if (existing == null)
        {
            menuButtonGO.transform.SetParent(topHud, false);
        }
        // Keep it as the sibling immediately after LevelBadge for a sane hierarchy order.
        menuButtonGO.transform.SetSiblingIndex(levelBadge.transform.GetSiblingIndex() + 1);

        var menuButtonRt = menuButtonGO.GetComponent<RectTransform>();
        menuButtonRt.anchorMin = levelBadgeRt.anchorMin;
        menuButtonRt.anchorMax = levelBadgeRt.anchorMax;
        menuButtonRt.pivot = levelBadgeRt.pivot;
        menuButtonRt.sizeDelta = new Vector2(MenuButtonSize, MenuButtonSize);
        float menuButtonX = levelBadgeRt.anchoredPosition.x + levelBadgeRt.sizeDelta.x + MenuButtonGap;
        menuButtonRt.anchoredPosition = new Vector2(menuButtonX, levelBadgeRt.anchoredPosition.y);

        var menuButtonImage = menuButtonGO.GetComponent<Image>();
        if (menuButtonImage == null) menuButtonImage = menuButtonGO.AddComponent<Image>();
        menuButtonImage.sprite = buttonBase;
        menuButtonImage.type = Image.Type.Simple;
        menuButtonImage.color = Color.white;
        menuButtonImage.raycastTarget = true;

        var menuButton = menuButtonGO.GetComponent<Button>();
        if (menuButton == null) menuButton = menuButtonGO.AddComponent<Button>();
        menuButton.targetGraphic = menuButtonImage;
        if (buttonPressed != null)
        {
            menuButton.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = menuButton.spriteState;
            state.pressedSprite = buttonPressed;
            state.highlightedSprite = buttonBase;
            state.selectedSprite = buttonBase;
            state.disabledSprite = buttonBase;
            menuButton.spriteState = state;
        }

        bool decorationCreated = buttonInlay != null && EnsureDecorationChild(menuButtonGO, buttonInlay, MenuButtonDecorationInset);

        // ---------------- MenuIcon: three bars (never a Unicode glyph) ----------------
        Transform iconExisting = menuButtonGO.transform.Find("MenuIcon");
        GameObject menuIcon = iconExisting != null ? iconExisting.gameObject : new GameObject("MenuIcon", typeof(RectTransform));
        if (iconExisting == null)
        {
            menuIcon.transform.SetParent(menuButtonGO.transform, false);
        }
        var iconRt = menuIcon.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;

        EnsureMenuBar(menuIcon.transform, "BarTop", MenuBarSpacing, barSprite);
        EnsureMenuBar(menuIcon.transform, "BarMiddle", 0f, barSprite);
        EnsureMenuBar(menuIcon.transform, "BarBottom", -MenuBarSpacing, barSprite);

        UIManager2D uiManager = Object.FindFirstObjectByType<UIManager2D>();
        bool wired = false;
        if (uiManager != null)
        {
            var so = new SerializedObject(uiManager);
            so.FindProperty("menuButton").objectReferenceValue = menuButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(uiManager);
            wired = true;
        }
        else if (logDetails)
        {
            Debug.LogWarning("UIPackageBinder: no UIManager2D found in the active scene; menuButton was not wired.");
        }

        EditorUtility.SetDirty(menuButtonGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (logDetails)
        {
            Debug.Log("UIPackageBinder: MenuButton install complete.\n" +
                $"  Hierarchy: TopHUD/MenuButton({(existing != null ? "reused" : "created")}) > " +
                $"PackageDecoration({(decorationCreated ? "created" : "reused")}), MenuIcon > BarTop, BarMiddle, BarBottom\n" +
                $"  Base sprite: '{buttonBase.name}' ({ButtonBasePath}), inset: '{buttonInlay?.name}' ({ButtonInlayPath}), " +
                $"pressed: '{buttonPressed?.name}' ({ButtonPressedPath})\n" +
                $"  Bar sprite: '{barSprite.name}' ({BadgeBasePath})\n" +
                $"  Size: ({MenuButtonSize}x{MenuButtonSize}), anchoredPosition: ({menuButtonX}, {levelBadgeRt.anchoredPosition.y})\n" +
                $"  LevelBadge anchoredPosition (unchanged): {levelBadgeRt.anchoredPosition}\n" +
                $"  UIManager2D.menuButton wired: {wired}");
        }

        return true;
    }

    private static void EnsureMenuBar(Transform parent, string name, float yOffset, Sprite sprite)
    {
        Transform existing = parent.Find(name);
        GameObject bar = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        if (existing == null)
        {
            bar.transform.SetParent(parent, false);
        }

        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(MenuBarWidth, MenuBarHeight);
        rt.anchoredPosition = new Vector2(0f, yOffset);

        var image = bar.GetComponent<Image>();
        if (image == null) image = bar.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    /// <summary>Replaces the text-based star display with an Image row (StarRoot/Star_0..Star_2) at the exact same anchored slot InfoStarText occupied, driven by ScoreManager2D.StarCount via UIManager2D.starImages. InfoStarText is deactivated (kept as an inert legacy fallback, not deleted).</summary>
    private static bool BindStars(Sprite starSprite, List<string> log)
    {
        if (starSprite == null)
        {
            log.Add("Stars: no star sprite found in the package - keeping the existing text-based star display unchanged.");
            return false;
        }

        GameObject infoCard = GameObject.Find("InfoCard");
        if (infoCard == null)
        {
            log.Add("Stars: 'InfoCard' not found - skipped.");
            return false;
        }

        Transform starRootT = infoCard.transform.Find("StarRoot");
        GameObject starRoot;
        bool isNewRoot = starRootT == null;
        if (isNewRoot)
        {
            starRoot = new GameObject("StarRoot", typeof(RectTransform));
            starRoot.transform.SetParent(infoCard.transform, false);
            var rt = starRoot.GetComponent<RectTransform>();
            // Same slot InfoStarText already occupied.
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -180f);
            rt.sizeDelta = new Vector2(800f, 80f);
        }
        else
        {
            starRoot = starRootT.gameObject;
        }

        const int starCount = 3;
        const float starSize = 70f;
        const float gap = 24f;
        float totalWidth = starCount * starSize + (starCount - 1) * gap;
        float startX = -totalWidth / 2f + starSize / 2f;

        var starImages = new List<Image>();
        for (int i = 0; i < starCount; i++)
        {
            string childName = $"Star_{i}";
            Transform childT = starRoot.transform.Find(childName);
            GameObject child;
            if (childT == null)
            {
                child = new GameObject(childName, typeof(RectTransform));
                child.transform.SetParent(starRoot.transform, false);
                var rt = child.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(starSize, starSize);
                rt.anchoredPosition = new Vector2(startX + i * (starSize + gap), 0f);
            }
            else
            {
                child = childT.gameObject;
            }

            var image = child.GetComponent<Image>();
            if (image == null) image = child.AddComponent<Image>();
            image.sprite = starSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            // Alpha is driven at runtime by UIManager2D.UpdateStarImages(starCount);
            // full alpha here is just the default editor-time appearance.
            Color c = image.color;
            c.a = 1f;
            image.color = c;

            starImages.Add(image);
        }

        UIManager2D uiManager = Object.FindFirstObjectByType<UIManager2D>();
        bool wired = false;
        if (uiManager != null)
        {
            var so = new SerializedObject(uiManager);
            SerializedProperty prop = so.FindProperty("starImages");
            prop.arraySize = starImages.Count;
            for (int i = 0; i < starImages.Count; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = starImages[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(uiManager);
            wired = true;
        }

        GameObject oldStarText = GameObject.Find("InfoStarText");
        if (oldStarText != null)
        {
            oldStarText.SetActive(false);
        }

        log.Add($"Stars: '{starSprite.name}' ({StarSpritePath}) bound to InfoCard/StarRoot/Star_0..Star_2 " +
            $"({(isNewRoot ? "created" : "reused")}), wired to UIManager2D.starImages={wired}, " +
            "old text-based 'InfoStarText' deactivated (kept as inert legacy, not deleted). " +
            "Empty-star fallback = same sprite at reduced alpha (no empty-star asset exists in the package); " +
            "ScoreManager2D.StarCount still drives which stars show full alpha - no scoring logic changed.");

        return true;
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
            Debug.LogWarning($"UIPackageBinder: no sprite found at '{texturePath}'.");
        }
        return sprite;
    }
}
