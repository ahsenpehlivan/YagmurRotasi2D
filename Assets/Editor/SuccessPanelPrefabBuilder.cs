using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Builds/updates the dedicated, self-contained success panel prefab
/// (Assets/Prefabs2D/UI/SuccessPanel2D.prefab) entirely out of the user's
/// imported UI package (Assets/Art2D/FinalSprites/UI/) and the SHPinscher-
/// Regular11 font. This replaces the earlier approach of patching the old
/// (unreliable) InfoPanel scene hierarchy - the whole visual tree here is a real
/// prefab asset, built/edited via PrefabUtility so it persists independently of
/// any scene's serialization quirks.
///
/// Idempotent: if the prefab asset already exists, its contents are loaded via
/// PrefabUtility.LoadPrefabContents and every object is found-or-created by name
/// (same pattern used throughout this project's other binders), so re-running
/// never duplicates anything - it just re-applies sprites/fonts/wiring.
/// </summary>
public static class SuccessPanelPrefabBuilder
{
    public const string PrefabFolder = "Assets/Prefabs2D/UI";
    public const string PrefabPath = PrefabFolder + "/SuccessPanel2D.prefab";

    private const string UiFolder = "Assets/Art2D/FinalSprites/UI";
    private const string FontPath = "Assets/SHPinscher-Regular11/SHPinscher-Regular.otf";

    // Layout constants (reference: a 1080-wide Canvas, same absolute-pixel
    // convention already used by every other Canvas element in this project).
    // MainPanel's suggested 950 height didn't quite fit title(130)+body(420)+
    // star tray(170)+button(130)+all requested gaps/margins, so BodyPanel's
    // height was reduced from the suggested 420 to 380 to make the stack fit
    // with a clean 20px bottom margin - the only deliberate deviation from the
    // spec's suggested sizes, needed purely for arithmetic consistency.
    private const float MainPanelWidth = 780f;
    // Phase 9B: grown from 950 to fit the new StatsText row (score/moves)
    // inserted between StarTray and the button row.
    private const float MainPanelHeight = 1020f;
    private const float MainPanelInset = 28f;

    private const float TitleBadgeWidth = 560f;
    private const float TitleBadgeHeight = 130f;
    private const float TitleBadgeInset = 10f;
    private const float TitleTopMargin = 40f;

    private const float BodyPanelWidth = 700f;
    private const float BodyPanelHeight = 380f;
    private const float BodyPanelInset = 14f;
    private const float BodyGapAboveMargin = 30f;

    private const float StarTrayWidth = 520f;
    private const float StarTrayHeight = 170f;
    private const float StarTrayInset = 12f;
    private const float StarTrayGapAboveMargin = 20f;

    private const float StarRowWidth = 420f;
    private const float StarRowHeight = 120f;
    private const float StarRowSpacing = 24f;
    private const float StarSlotSize = 110f;
    private const float StarBackgroundSize = 96f;
    private const float StarSize = 82f;
    private const float StarUnearnedAlpha = 0.3f;
    private const float StarBackgroundAlpha = 0.45f;

    // Phase 9B: narrowed again (Phase 9A's 340f pair -> 230f triple) so
    // NextLevelButton, RetryButton and ReturnToLevelSelectButton fit side by
    // side in one row within MainPanelWidth (780f).
    private const float ButtonWidth = 230f;
    private const float ButtonHeight = 130f;
    private const float ButtonInset = 10f;
    private const float ButtonGapAboveMargin = 30f;
    private const float ButtonTripleOffsetX = 260f;

    private const float StatsTextHeight = 50f;
    private const float StatsGapAboveMargin = 16f;

    // Phase 7E.7: BodyText readability - Best Fit between these two sizes.
    private const int BodyTextMinSize = 32;
    private const int BodyTextMaxSize = 40;
    private const float BodyTextLineSpacing = 1.1f;

    [MenuItem("YagmurRotasi2D/Build Dedicated Success Panel Prefab")]
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

        Sprite buttonBase = LoadFirstSprite(UiFolder + "/Buttons/brown.png");
        Sprite buttonInlay = LoadFirstSprite(UiFolder + "/Buttons/brown_inlay.png");
        Sprite buttonPressed = LoadFirstSprite(UiFolder + "/Buttons/brown_pressed.png");
        Sprite panelBase = LoadFirstSprite(UiFolder + "/Panels/tan.png");
        Sprite panelInlay = LoadFirstSprite(UiFolder + "/Panels/tan_inlay.png");
        Sprite badgeWhite = LoadFirstSprite(UiFolder + "/Badges/white.png");
        Sprite badgeWhiteInlay = LoadFirstSprite(UiFolder + "/Badges/white_inlay.png");
        Sprite badgeGrey = LoadFirstSprite(UiFolder + "/Badges/grey.png");
        Sprite badgeGreyInlay = LoadFirstSprite(UiFolder + "/Badges/grey_inlay.png");
        Sprite starSprite = LoadFirstSprite(UiFolder + "/Stars/star.png");
        Font shPinscherFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        if (shPinscherFont == null)
        {
            Debug.LogError($"SuccessPanelPrefabBuilder: SHPinscher font not found at '{FontPath}'. Aborting.");
            return false;
        }

        if (buttonBase == null || panelBase == null || badgeWhite == null || badgeGrey == null || starSprite == null)
        {
            Debug.LogError("SuccessPanelPrefabBuilder: one or more required UI package sprites could not be " +
                "loaded (see warnings above for which path failed). Aborting.");
            return false;
        }

        bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        GameObject prefabRoot = prefabExists
            ? PrefabUtility.LoadPrefabContents(PrefabPath)
            : new GameObject("SuccessPanel2D", typeof(RectTransform));

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
            log.Add("ModalBlocker: plain color (15,12,10,0.65) - the only allowed non-package element, full-screen, raycastTarget=true.");

            // ---------------- PanelShadow (behind MainPanel) ----------------
            GameObject panelShadow = FindOrCreateChild(prefabRoot.transform, "PanelShadow");
            SetAnchoredRect(RectOf(panelShadow), Center, Center, Center, new Vector2(10f, -12f), new Vector2(MainPanelWidth, MainPanelHeight));
            SetupImage(panelShadow, badgeGrey, false, Color.white);
            panelShadow.transform.SetSiblingIndex(1);
            log.Add($"PanelShadow: '{badgeGrey.name}' ({UiFolder}/Badges/grey.png), offset (10,-12), raycastTarget=false.");

            // ---------------- MainPanel ----------------
            GameObject mainPanel = FindOrCreateChild(prefabRoot.transform, "MainPanel");
            SetAnchoredRect(RectOf(mainPanel), Center, Center, Center, Vector2.zero, new Vector2(MainPanelWidth, MainPanelHeight));
            SetupImage(mainPanel, panelBase, false, Color.white);
            log.Add($"MainPanel: '{panelBase.name}' ({UiFolder}/Panels/tan.png), size ({MainPanelWidth}x{MainPanelHeight}), Image.Type=Simple.");

            GameObject mainPanelInset = FindOrCreateChild(mainPanel.transform, "MainPanelInset");
            StretchFill(RectOf(mainPanelInset), MainPanelInset);
            SetupImage(mainPanelInset, panelInlay, false, Color.white);
            mainPanelInset.transform.SetAsFirstSibling();
            log.Add($"MainPanelInset: '{panelInlay?.name}' ({UiFolder}/Panels/tan_inlay.png), inset {MainPanelInset}px, behind all content.");

            // ---------------- TitleBadge ----------------
            GameObject titleBadge = FindOrCreateChild(mainPanel.transform, "TitleBadge");
            SetAnchoredRect(RectOf(titleBadge), TopCenter, TopCenter, TopCenter, new Vector2(0f, -TitleTopMargin), new Vector2(TitleBadgeWidth, TitleBadgeHeight));
            SetupImage(titleBadge, badgeWhite, false, Color.white);

            GameObject titleBadgeInset = FindOrCreateChild(titleBadge.transform, "TitleBadgeInset");
            StretchFill(RectOf(titleBadgeInset), TitleBadgeInset);
            SetupImage(titleBadgeInset, badgeWhiteInlay, false, Color.white);
            titleBadgeInset.transform.SetAsFirstSibling();

            GameObject titleTextGO = FindOrCreateChild(titleBadge.transform, "TitleText");
            StretchFill(RectOf(titleTextGO), 0f);
            Text titleText = SetupText(titleTextGO, "Tebrikler!", shPinscherFont, 64, Color.black, TextAnchor.MiddleCenter, false);
            log.Add($"TitleBadge: base='{badgeWhite.name}', inset='{badgeWhiteInlay?.name}' ({UiFolder}/Badges/white(+_inlay).png), " +
                $"TitleText uses '{shPinscherFont.name}' size 64.");

            // ---------------- BodyPanel ----------------
            float bodyTop = -(TitleTopMargin + TitleBadgeHeight + BodyGapAboveMargin);
            GameObject bodyPanel = FindOrCreateChild(mainPanel.transform, "BodyPanel");
            SetAnchoredRect(RectOf(bodyPanel), TopCenter, TopCenter, TopCenter, new Vector2(0f, bodyTop), new Vector2(BodyPanelWidth, BodyPanelHeight));
            SetupImage(bodyPanel, badgeGrey, false, Color.white);

            GameObject bodyPanelInset = FindOrCreateChild(bodyPanel.transform, "BodyPanelInset");
            StretchFill(RectOf(bodyPanelInset), BodyPanelInset);
            SetupImage(bodyPanelInset, badgeGreyInlay, false, Color.white);
            bodyPanelInset.transform.SetAsFirstSibling();

            GameObject bodyTextGO = FindOrCreateChild(bodyPanel.transform, "BodyText");
            StretchFill(RectOf(bodyTextGO), BodyPanelInset + 12f);
            Text bodyText = SetupText(bodyTextGO, "", shPinscherFont, BodyTextMaxSize, Color.black, TextAnchor.MiddleCenter, true,
                bestFit: true, minSize: BodyTextMinSize, maxSize: BodyTextMaxSize, lineSpacing: BodyTextLineSpacing);
            log.Add($"BodyPanel: base='{badgeGrey.name}', inset='{badgeGreyInlay?.name}' ({UiFolder}/Badges/grey(+_inlay).png), " +
                $"BodyText uses '{shPinscherFont.name}' size {BodyTextMaxSize} (Best Fit {BodyTextMinSize}-{BodyTextMaxSize}), " +
                $"lineSpacing={BodyTextLineSpacing}, alignment=MiddleCenter, wrap=true.");

            // ---------------- StarTray ----------------
            float starTrayTop = bodyTop - (BodyPanelHeight + StarTrayGapAboveMargin);
            GameObject starTray = FindOrCreateChild(mainPanel.transform, "StarTray");
            SetAnchoredRect(RectOf(starTray), TopCenter, TopCenter, TopCenter, new Vector2(0f, starTrayTop), new Vector2(StarTrayWidth, StarTrayHeight));
            SetupImage(starTray, badgeWhite, false, Color.white);

            GameObject starTrayInset = FindOrCreateChild(starTray.transform, "StarTrayInset");
            StretchFill(RectOf(starTrayInset), StarTrayInset);
            SetupImage(starTrayInset, badgeWhiteInlay, false, Color.white);
            starTrayInset.transform.SetAsFirstSibling();

            GameObject starRow = FindOrCreateChild(starTray.transform, "StarRow");
            SetAnchoredRect(RectOf(starRow), Center, Center, Center, Vector2.zero, new Vector2(StarRowWidth, StarRowHeight));
            var layout = starRow.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = starRow.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = StarRowSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var starImages = new List<Image>();
            var starLog = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                GameObject slot = FindOrCreateChild(starRow.transform, $"StarSlot_{i}");
                slot.transform.SetSiblingIndex(i);
                var slotRt = RectOf(slot);
                slotRt.sizeDelta = new Vector2(StarSlotSize, StarSlotSize);

                GameObject background = FindOrCreateChild(slot.transform, "StarBackground");
                SetAnchoredRect(RectOf(background), Center, Center, Center, Vector2.zero, new Vector2(StarBackgroundSize, StarBackgroundSize));
                Image bgImage = SetupImage(background, badgeGreyInlay, false, new Color(1f, 1f, 1f, StarBackgroundAlpha));
                bgImage.preserveAspect = true;
                background.transform.SetSiblingIndex(0);

                GameObject starGO = FindOrCreateChild(slot.transform, $"Star_{i}");
                SetAnchoredRect(RectOf(starGO), Center, Center, Center, Vector2.zero, new Vector2(StarSize, StarSize));
                Image starImage = SetupImage(starGO, starSprite, false, new Color(1f, 1f, 1f, StarUnearnedAlpha));
                starImage.preserveAspect = true;
                starGO.transform.SetSiblingIndex(1);

                // Guaranteed-visible: explicitly active/enabled regardless of prior state.
                starGO.SetActive(true);
                starImage.enabled = true;

                starImages.Add(starImage);
                starLog.Add($"  Star_{i}: size ({StarSize}x{StarSize}), active={starGO.activeSelf}, enabled={starImage.enabled}, " +
                    $"sprite='{starSprite.name}', initial alpha={StarUnearnedAlpha}");
            }

            log.Add($"StarTray: base='{badgeWhite.name}', inset='{badgeWhiteInlay?.name}' ({UiFolder}/Badges/white(+_inlay).png).\n" +
                $"StarRow: HorizontalLayoutGroup spacing={StarRowSpacing}, MiddleCenter, controlChildSize=false, forceExpand=false.\n" +
                string.Join("\n", starLog) +
                $"\n  Star sprite path: {UiFolder}/Stars/star.png");

            // ---------------- StatsText (Phase 9B: score/moves - no time field, this project has no timer system) ----------------
            float statsTop = starTrayTop - (StarTrayHeight + StatsGapAboveMargin);
            GameObject statsTextGO = FindOrCreateChild(mainPanel.transform, "StatsText");
            SetAnchoredRect(RectOf(statsTextGO), TopCenter, TopCenter, TopCenter, new Vector2(0f, statsTop), new Vector2(BodyPanelWidth, StatsTextHeight));
            Text statsText = SetupText(statsTextGO, "Skor: 0   Hamle: 0", shPinscherFont, 32, Color.black, TextAnchor.MiddleCenter, false);
            log.Add($"StatsText: plain Text (no background panel), '{shPinscherFont.name}' size 32 - shows score/move count set by UIManager2D.HandleAnimationComplete.");

            // ---------------- NextLevelButton / RetryButton / ReturnToLevelSelectButton (Phase 9B: one row of three) ----------------
            float buttonTop = statsTop - (StatsTextHeight + ButtonGapAboveMargin);

            GameObject nextLevelButtonGO = FindOrCreateChild(mainPanel.transform, "NextLevelButton");
            SetAnchoredRect(RectOf(nextLevelButtonGO), TopCenter, TopCenter, TopCenter, new Vector2(-ButtonTripleOffsetX, buttonTop), new Vector2(ButtonWidth, ButtonHeight));
            Image buttonImage = SetupImage(nextLevelButtonGO, buttonBase, true, Color.white);

            Button button = nextLevelButtonGO.GetComponent<Button>();
            if (button == null) button = nextLevelButtonGO.AddComponent<Button>();
            button.targetGraphic = buttonImage;
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

            GameObject buttonInset = FindOrCreateChild(nextLevelButtonGO.transform, "ButtonInset");
            StretchFill(RectOf(buttonInset), ButtonInset);
            SetupImage(buttonInset, buttonInlay, false, Color.white);
            buttonInset.transform.SetAsFirstSibling();

            GameObject labelGO = FindOrCreateChild(nextLevelButtonGO.transform, "Label");
            StretchFill(RectOf(labelGO), 0f);
            Text label = SetupText(labelGO, "Sonraki Bölüm", shPinscherFont, 28, Color.white, TextAnchor.MiddleCenter, true,
                bestFit: true, minSize: 16, maxSize: 28);
            log.Add($"NextLevelButton: base='{buttonBase.name}', inset='{buttonInlay?.name}', pressed='{buttonPressed?.name}' " +
                $"({UiFolder}/Buttons/brown(+_inlay/_pressed).png), transition={button.transition}, Label uses '{shPinscherFont.name}' Best Fit 16-28.");

            // Phase 9B: center button - "Tekrar Oyna" replays the just-completed level (same action as the pause menu's Restart).
            GameObject retryButtonGO = FindOrCreateChild(mainPanel.transform, "RetryButton");
            SetAnchoredRect(RectOf(retryButtonGO), TopCenter, TopCenter, TopCenter, new Vector2(0f, buttonTop), new Vector2(ButtonWidth, ButtonHeight));
            Image retryButtonImage = SetupImage(retryButtonGO, buttonBase, true, Color.white);

            Button retryButton = retryButtonGO.GetComponent<Button>();
            if (retryButton == null) retryButton = retryButtonGO.AddComponent<Button>();
            retryButton.targetGraphic = retryButtonImage;
            if (buttonPressed != null)
            {
                retryButton.transition = Selectable.Transition.SpriteSwap;
                SpriteState retryState = retryButton.spriteState;
                retryState.pressedSprite = buttonPressed;
                retryState.highlightedSprite = buttonBase;
                retryState.selectedSprite = buttonBase;
                retryState.disabledSprite = buttonBase;
                retryButton.spriteState = retryState;
            }

            GameObject retryButtonInset = FindOrCreateChild(retryButtonGO.transform, "ButtonInset");
            StretchFill(RectOf(retryButtonInset), ButtonInset);
            SetupImage(retryButtonInset, buttonInlay, false, Color.white);
            retryButtonInset.transform.SetAsFirstSibling();

            GameObject retryLabelGO = FindOrCreateChild(retryButtonGO.transform, "Label");
            StretchFill(RectOf(retryLabelGO), 0f);
            Text retryLabel = SetupText(retryLabelGO, "Tekrar Oyna", shPinscherFont, 28, Color.white, TextAnchor.MiddleCenter, true,
                bestFit: true, minSize: 16, maxSize: 28);
            log.Add($"RetryButton: base='{buttonBase.name}', inset='{buttonInlay?.name}', pressed='{buttonPressed?.name}' " +
                $"({UiFolder}/Buttons/brown(+_inlay/_pressed).png), transition={retryButton.transition}, Label uses '{shPinscherFont.name}' Best Fit 16-28.");

            // Phase 9A: third button, hidden entirely (not just disabled) on
            // the last campaign level - see SuccessPanelView2D.Show(hasNextLevel).
            GameObject returnButtonGO = FindOrCreateChild(mainPanel.transform, "ReturnToLevelSelectButton");
            SetAnchoredRect(RectOf(returnButtonGO), TopCenter, TopCenter, TopCenter, new Vector2(ButtonTripleOffsetX, buttonTop), new Vector2(ButtonWidth, ButtonHeight));
            Image returnButtonImage = SetupImage(returnButtonGO, buttonBase, true, Color.white);

            Button returnButton = returnButtonGO.GetComponent<Button>();
            if (returnButton == null) returnButton = returnButtonGO.AddComponent<Button>();
            returnButton.targetGraphic = returnButtonImage;
            if (buttonPressed != null)
            {
                returnButton.transition = Selectable.Transition.SpriteSwap;
                SpriteState returnState = returnButton.spriteState;
                returnState.pressedSprite = buttonPressed;
                returnState.highlightedSprite = buttonBase;
                returnState.selectedSprite = buttonBase;
                returnState.disabledSprite = buttonBase;
                returnButton.spriteState = returnState;
            }

            GameObject returnButtonInset = FindOrCreateChild(returnButtonGO.transform, "ButtonInset");
            StretchFill(RectOf(returnButtonInset), ButtonInset);
            SetupImage(returnButtonInset, buttonInlay, false, Color.white);
            returnButtonInset.transform.SetAsFirstSibling();

            GameObject returnLabelGO = FindOrCreateChild(returnButtonGO.transform, "Label");
            StretchFill(RectOf(returnLabelGO), 0f);
            Text returnLabel = SetupText(returnLabelGO, "Bölüm Seçimine Dön", shPinscherFont, 24, Color.white, TextAnchor.MiddleCenter, true,
                bestFit: true, minSize: 14, maxSize: 24);
            log.Add($"ReturnToLevelSelectButton: base='{buttonBase.name}', inset='{buttonInlay?.name}', pressed='{buttonPressed?.name}' " +
                $"({UiFolder}/Buttons/brown(+_inlay/_pressed).png), transition={returnButton.transition}, Label uses '{shPinscherFont.name}' Best Fit 16-28.");

            // ---------------- SuccessPanelView2D wiring ----------------
            var view = prefabRoot.GetComponent<SuccessPanelView2D>();
            if (view == null) view = prefabRoot.AddComponent<SuccessPanelView2D>();

            var viewSO = new SerializedObject(view);
            viewSO.FindProperty("root").objectReferenceValue = prefabRoot;
            viewSO.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            viewSO.FindProperty("titleText").objectReferenceValue = titleText;
            viewSO.FindProperty("bodyText").objectReferenceValue = bodyText;
            viewSO.FindProperty("statsText").objectReferenceValue = statsText;
            SerializedProperty starImagesProp = viewSO.FindProperty("starImages");
            starImagesProp.arraySize = starImages.Count;
            for (int i = 0; i < starImages.Count; i++)
            {
                starImagesProp.GetArrayElementAtIndex(i).objectReferenceValue = starImages[i];
            }
            viewSO.FindProperty("nextLevelButton").objectReferenceValue = button;
            viewSO.FindProperty("retryButton").objectReferenceValue = retryButton;
            viewSO.FindProperty("returnToLevelSelectButton").objectReferenceValue = returnButton;
            viewSO.FindProperty("earnedStarAlpha").floatValue = 1f;
            viewSO.FindProperty("unearnedStarAlpha").floatValue = StarUnearnedAlpha;
            viewSO.ApplyModifiedPropertiesWithoutUndo();

            // Prefab is authored in its default (hidden) state - CanvasGroup
            // invisible/non-interactive, root inactive is NOT set here (the
            // scene installer controls root activation for the live instance;
            // the prefab ASSET itself stays "active" so Prefab Mode inspection
            // shows the complete design per Part L).
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            success = true;

            if (logDetails)
            {
                Debug.Log($"SuccessPanelPrefabBuilder: {(prefabExists ? "updated" : "created")} '{PrefabPath}'.\n" +
                    "Hierarchy: SuccessPanel2D > ModalBlocker, PanelShadow, MainPanel > MainPanelInset, TitleBadge > " +
                    "TitleBadgeInset, TitleText; BodyPanel > BodyPanelInset, BodyText; StarTray > StarTrayInset, " +
                    "StarRow > StarSlot_0..2 > StarBackground, Star_N; StatsText (Phase 9B - score/moves); " +
                    "NextLevelButton, RetryButton, ReturnToLevelSelectButton (Phase 9B - one row of three) each > ButtonInset, Label.\n" +
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

    [MenuItem("YagmurRotasi2D/Increase Success Info Text")]
    public static void IncreaseBodyTextMenuCommand()
    {
        TryUpdateBodyTextReadability(true);
    }

    /// <summary>Updates only SuccessPanel2D/MainPanel/BodyPanel/BodyText's font settings for readability (font size, Best Fit range, line spacing, alignment). Touches nothing else in the prefab - title, stars, button, backgrounds and every RectTransform/sprite are left exactly as they are.</summary>
    public static bool TryUpdateBodyTextReadability(bool logDetails)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError($"SuccessPanelPrefabBuilder: prefab not found at '{PrefabPath}'. " +
                "Run 'YagmurRotasi2D > Build Dedicated Success Panel Prefab' first.");
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool success = false;

        try
        {
            Transform bodyTextT = prefabRoot.transform.Find("MainPanel/BodyPanel/BodyText");
            if (bodyTextT == null)
            {
                Debug.LogError("SuccessPanelPrefabBuilder: 'MainPanel/BodyPanel/BodyText' not found in the prefab. " +
                    "Run 'Build Dedicated Success Panel Prefab' first to (re)create the full hierarchy.");
                return false;
            }

            Text bodyText = bodyTextT.GetComponent<Text>();
            if (bodyText == null)
            {
                Debug.LogError("SuccessPanelPrefabBuilder: 'BodyText' has no Text component.");
                return false;
            }

            string before = $"fontSize={bodyText.fontSize}, bestFit={bodyText.resizeTextForBestFit}, " +
                $"minSize={bodyText.resizeTextMinSize}, maxSize={bodyText.resizeTextMaxSize}, " +
                $"lineSpacing={bodyText.lineSpacing}, alignment={bodyText.alignment}, wrap={bodyText.horizontalOverflow}";

            bodyText.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath) ?? bodyText.font;
            bodyText.fontSize = BodyTextMaxSize;
            bodyText.resizeTextForBestFit = true;
            bodyText.resizeTextMinSize = BodyTextMinSize;
            bodyText.resizeTextMaxSize = BodyTextMaxSize;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.alignment = TextAnchor.MiddleCenter;
            bodyText.lineSpacing = BodyTextLineSpacing;

            string after = $"fontSize={bodyText.fontSize}, bestFit={bodyText.resizeTextForBestFit}, " +
                $"minSize={bodyText.resizeTextMinSize}, maxSize={bodyText.resizeTextMaxSize}, " +
                $"lineSpacing={bodyText.lineSpacing}, alignment={bodyText.alignment}, wrap={bodyText.horizontalOverflow}";

            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            success = true;

            if (logDetails)
            {
                Debug.Log("SuccessPanelPrefabBuilder: BodyText readability updated.\n" +
                    $"  Before: {before}\n  After:  {after}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        return success;
    }

    [MenuItem("YagmurRotasi2D/Install Dedicated Success Panel")]
    public static void InstallMenuCommand()
    {
        TryInstallIntoScene(true);
    }

    /// <summary>Instantiates exactly one SuccessPanel2D.prefab under SafeAreaRoot/SuccessPanelHost, wires it into UIManager2D.dedicatedSuccessPanel, hides it, and disables the old InfoPanel. Idempotent - finds "DedicatedSuccessPanel" by name and reuses it if it already exists, never creating a second instance.</summary>
    public static bool TryInstallIntoScene(bool logDetails)
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "GameScene2D")
        {
            Debug.LogError($"SuccessPanelPrefabBuilder: active scene is '{activeScene.name}', not 'GameScene2D'. " +
                "Open Assets/Scenes/GameScene2D.unity first, then run this command again.");
            return false;
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"SuccessPanelPrefabBuilder: prefab not found at '{PrefabPath}'. " +
                "Run 'YagmurRotasi2D > Build Dedicated Success Panel Prefab' first.");
            return false;
        }

        GameObject safeAreaRoot = GameObject.Find("SafeAreaRoot");
        if (safeAreaRoot == null)
        {
            Debug.LogError("SuccessPanelPrefabBuilder: 'SafeAreaRoot' not found in the active scene.");
            return false;
        }

        GameObject host = FindOrCreateChild(safeAreaRoot.transform, "SuccessPanelHost");
        StretchFill(RectOf(host), 0f);

        GameObject existingInstance = GameObject.Find("DedicatedSuccessPanel");
        GameObject instance;
        bool isNew = existingInstance == null;

        if (isNew)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, host.transform);
            instance.name = "DedicatedSuccessPanel";
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

        SuccessPanelView2D view = instance.GetComponent<SuccessPanelView2D>();
        if (view == null)
        {
            Debug.LogError("SuccessPanelPrefabBuilder: 'DedicatedSuccessPanel' has no SuccessPanelView2D component - " +
                "the prefab may be out of date. Re-run 'Build Dedicated Success Panel Prefab' first.");
            return false;
        }

        // Hidden initially - uses the component's own Hide() so root activation and
        // CanvasGroup state stay consistent with how it will be hidden at runtime.
        view.Hide();

        UIManager2D uiManager = Object.FindFirstObjectByType<UIManager2D>();
        bool wired = false;
        if (uiManager != null)
        {
            var so = new SerializedObject(uiManager);
            so.FindProperty("dedicatedSuccessPanel").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(uiManager);
            wired = true;
        }
        else if (logDetails)
        {
            Debug.LogWarning("SuccessPanelPrefabBuilder: no UIManager2D found in the active scene; dedicatedSuccessPanel was not wired.");
        }

        GameObject oldInfoPanel = GameObject.Find("InfoPanel");
        if (oldInfoPanel != null)
        {
            oldInfoPanel.SetActive(false);
        }

        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        if (logDetails)
        {
            Debug.Log($"SuccessPanelPrefabBuilder: install complete.\n" +
                $"  Instance: {(isNew ? "created" : "reused")} 'SafeAreaRoot/SuccessPanelHost/DedicatedSuccessPanel'\n" +
                $"  UIManager2D.dedicatedSuccessPanel wired: {wired}\n" +
                $"  Old 'InfoPanel' disabled: {oldInfoPanel != null}\n" +
                "  Hidden initially (CanvasGroup alpha=0, interactable=false, blocksRaycasts=false, root inactive).");
        }

        return true;
    }

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

    private static RectTransform RectOf(GameObject go) => go.GetComponent<RectTransform>();

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
            Debug.LogWarning($"SuccessPanelPrefabBuilder: no sprite found at '{texturePath}'.");
        }
        return sprite;
    }
}
