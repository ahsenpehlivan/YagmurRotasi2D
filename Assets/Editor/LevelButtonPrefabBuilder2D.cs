using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using YagmurRotasi2D.Audio2D;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Builds/updates the dynamically-instantiated campaign level card prefab
/// (Assets/Prefabs2D/UI/LevelButton2D.prefab) - LevelSelectController2D
/// instantiates 100 copies of this at runtime, one per CampaignLevelCatalog2D
/// entry; there is no manually-authored button per level anywhere. Built
/// entirely out of the existing shared UI package (Assets/Art2D/FinalSprites/UI/)
/// for card/star art, matching SuccessPanelPrefabBuilder2D/InGameMenuPrefabBuilder's
/// exact idempotent find-or-create pattern. This card uses
/// Selectable.Transition.ColorTint (not SpriteSwap) - a deliberate choice so
/// hover AND keyboard/gamepad "selected" both get a clearly distinct tint
/// (Phase 9A Part B/G - "strong hover/selected feedback for web mouse users").
///
/// READABILITY FIX (Level Select typography pass): all text on the card
/// (level number, grid size, locked text, dev info) is now TextMeshProUGUI
/// via TMPTextSetup2D, not legacy UI.Text. Root cause of the original
/// blur/softness: this project never had ANY TMP text anywhere - every label
/// was legacy UnityEngine.UI.Text, which rasterizes bitmap glyphs at a fixed
/// size and does not use SDF rendering, so it goes soft under
/// CanvasScaler.ScaleWithScreenSize whenever the effective scale factor
/// isn't exactly 1.0 (true at almost every non-reference resolution). TMP's
/// SDF glyphs stay sharp at any scale. This was never a "wrong SDF material"
/// bug (there was no SDF material at all) - see TMPTextSetup2D's doc comment
/// for the font asset itself (generated once from the existing
/// SHPinscher-Regular.otf, no new/remote font introduced).
/// </summary>
public static class LevelButtonPrefabBuilder2D
{
    public const string PrefabFolder = "Assets/Prefabs2D/UI";
    public const string PrefabPath = PrefabFolder + "/LevelButton2D.prefab";

    private const string UiFolder = "Assets/Art2D/FinalSprites/UI";

    private const float CardWidth = 190f;
    private const float CardHeight = 210f;
    private const float CardInset = 6f;

    // Deepened from the pre-TMP values for real contrast against the pastel
    // card (WCAG-style check: the old GridSizeColor at (0.35,0.40,0.48) on
    // this card background measured well below a readable contrast ratio -
    // "do not use low-opacity gray" per spec).
    private static readonly Color CardTint = new Color(0.80f, 0.90f, 0.98f, 1f); // pastel sky blue
    private static readonly Color NumberColor = new Color(0.08f, 0.13f, 0.24f, 1f); // near-black navy - max contrast, highest-priority text
    private static readonly Color GridSizeColor = new Color(0.16f, 0.22f, 0.34f, 1f); // dark slate blue, not gray
    private static readonly Color DevInfoColor = new Color(0.28f, 0.28f, 0.28f, 1f);
    private static readonly Color LockedTextOutline = new Color(0.05f, 0.05f, 0.06f, 0.85f);
    private static readonly Color CompletedTint = new Color(1f, 0.82f, 0.25f, 1f); // gold

    // Part E star visibility fix - was 22px/0.3 alpha (near-invisible);
    // unearned is now a muted multiply COLOR (not just low alpha), so it
    // never drops below the ~0.35 alpha floor the spec calls out.
    private const float StarSize = 30f;
    private const float StarRowSpacing = 6f;
    private static readonly Color StarEarnedColor = Color.white; // shows the sprite's own gold art at full opacity
    private static readonly Color StarUnearnedColor = new Color(0.55f, 0.52f, 0.46f, 0.9f);

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopStretch0 = new Vector2(0f, 1f);
    private static readonly Vector2 TopStretch1 = new Vector2(1f, 1f);

    [MenuItem("YagmurRotasi2D/Build Level Button Prefab")]
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

        TMP_FontAsset tmpFont = TMPTextSetup2D.GetOrCreateFontAsset();
        Sprite cardBase = LoadFirstSprite(UiFolder + "/Badges/white.png");
        Sprite cardInlay = LoadFirstSprite(UiFolder + "/Badges/white_inlay.png");
        Sprite starSprite = LoadFirstSprite(UiFolder + "/Stars/star.png");

        if (tmpFont == null || cardBase == null || starSprite == null)
        {
            Debug.LogError("LevelButtonPrefabBuilder2D: one or more required assets could not be loaded/created (see warnings above). Aborting.");
            return false;
        }

        bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        GameObject prefabRoot = prefabExists
            ? PrefabUtility.LoadPrefabContents(PrefabPath)
            : new GameObject("LevelButton2D", typeof(RectTransform));

        bool success = false;
        try
        {
            var rt = RectOf(prefabRoot);
            rt.sizeDelta = new Vector2(CardWidth, CardHeight);

            Image cardImage = SetupImage(prefabRoot, cardBase, true, CardTint);

            Button button = prefabRoot.GetComponent<Button>();
            if (button == null) button = prefabRoot.AddComponent<Button>();
            button.targetGraphic = cardImage;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.98f, 0.85f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = new Color(1f, 0.9f, 0.55f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            // Idempotent - level cards are instantiated at runtime from this
            // prefab, so this is the only place UIButtonSound2D can be
            // attached; a locked card's button.interactable=false already
            // means Unity never invokes onClick at all, so no extra
            // "don't play for locked cards" logic is needed here.
            if (prefabRoot.GetComponent<UIButtonSound2D>() == null) prefabRoot.AddComponent<UIButtonSound2D>();

            GameObject cardInsetGO = FindOrCreateChild(prefabRoot.transform, "CardInset");
            StretchFill(RectOf(cardInsetGO), CardInset);
            SetupImage(cardInsetGO, cardInlay, false, Color.white);
            cardInsetGO.transform.SetAsFirstSibling();

            // Part C: level number is the card's highest-priority text -
            // Bold, deepest contrast, auto-sized within a narrow band purely
            // so "100" never clips while "1".."99" render at the full 42pt.
            GameObject numberGO = FindOrCreateChild(prefabRoot.transform, "LevelNumberText");
            SetAnchoredRect(RectOf(numberGO), TopStretch0, TopStretch1, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-12f, 60f));
            TextMeshProUGUI numberText = TMPTextSetup2D.SetupTMPText(numberGO, "1", tmpFont, 42f, NumberColor,
                TextAlignmentOptions.Center, wrap: false, bold: true, autoSize: true, autoSizeMin: 32f, autoSizeMax: 42f);

            // Grid size format ("5×5".."10×10") is fixed/predictable - a
            // small bounded auto-size band is defense-in-depth only, per
            // spec ("disable Auto Size where content format is predictable"
            // + "carefully bounded Auto Size" are both explicitly allowed).
            GameObject gridSizeGO = FindOrCreateChild(prefabRoot.transform, "GridSizeText");
            SetAnchoredRect(RectOf(gridSizeGO), TopStretch0, TopStretch1, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(-12f, 30f));
            TextMeshProUGUI gridSizeText = TMPTextSetup2D.SetupTMPText(gridSizeGO, "5×5", tmpFont, 24f, GridSizeColor,
                TextAlignmentOptions.Center, wrap: false, bold: false, autoSize: true, autoSizeMin: 18f, autoSizeMax: 24f);

            // Part E: stars grown 22px -> 30px, row given more height/width
            // to match, nudged up slightly for clearance from DevInfoText's
            // bottom-anchored box (an Editor/Development-Build-only overlap
            // risk - the actual rendered glyph pixels of both no longer
            // meaningfully touch, but note the two boxes still sit close;
            // acceptable since DevInfoText never appears in a release build
            // and restructuring the full card vertical layout is out of
            // scope for a readability-only fix).
            GameObject starRow = FindOrCreateChild(prefabRoot.transform, "StarRow");
            SetAnchoredRect(RectOf(starRow), Center, Center, Center, new Vector2(0f, -68f), new Vector2(140f, 40f));
            var layout = starRow.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = starRow.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = StarRowSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var starImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject starGO = FindOrCreateChild(starRow.transform, $"Star_{i}");
                starGO.transform.SetSiblingIndex(i);
                RectOf(starGO).sizeDelta = new Vector2(StarSize, StarSize);
                Image starImage = SetupImage(starGO, starSprite, false, StarUnearnedColor);
                starImage.preserveAspect = true;
                starImages[i] = starImage;
            }

            GameObject completedBadge = FindOrCreateChild(prefabRoot.transform, "CompletedBadge");
            SetAnchoredRect(RectOf(completedBadge), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(34f, 34f));
            Image completedImage = SetupImage(completedBadge, starSprite, false, CompletedTint);
            completedImage.preserveAspect = true;
            completedBadge.SetActive(false);

            GameObject lockedOverlay = FindOrCreateChild(prefabRoot.transform, "LockedOverlay");
            StretchFill(RectOf(lockedOverlay), 0f);
            SetupImage(lockedOverlay, null, false, new Color(0.1f, 0.1f, 0.12f, 0.55f));
            GameObject lockedTextGO = FindOrCreateChild(lockedOverlay.transform, "LockedText");
            StretchFill(RectOf(lockedTextGO), 8f);
            TMPTextSetup2D.SetupTMPText(lockedTextGO, "Kilitli", tmpFont, 20f, Color.white,
                TextAlignmentOptions.Center, wrap: true, bold: true, autoSize: true, autoSizeMin: 16f, autoSizeMax: 20f,
                outlineWidth: 0.15f, outlineColor: LockedTextOutline);
            lockedOverlay.SetActive(false);

            GameObject devInfoGO = FindOrCreateChild(prefabRoot.transform, "DevInfoText");
            SetAnchoredRect(RectOf(devInfoGO), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(-8f, 22f));
            TextMeshProUGUI devInfoText = TMPTextSetup2D.SetupTMPText(devInfoGO, "", tmpFont, 15f, DevInfoColor,
                TextAlignmentOptions.Bottom, wrap: true, bold: false, autoSize: true, autoSizeMin: 13f, autoSizeMax: 15f);
            devInfoGO.SetActive(false);

            // ---------------- LevelButton2D wiring ----------------
            var view = prefabRoot.GetComponent<LevelButton2D>();
            if (view == null) view = prefabRoot.AddComponent<LevelButton2D>();

            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("levelNumberText").objectReferenceValue = numberText;
            so.FindProperty("gridSizeText").objectReferenceValue = gridSizeText;
            so.FindProperty("completedBadge").objectReferenceValue = completedBadge;
            SerializedProperty starImagesProp = so.FindProperty("starImages");
            starImagesProp.arraySize = starImages.Length;
            for (int i = 0; i < starImages.Length; i++)
            {
                starImagesProp.GetArrayElementAtIndex(i).objectReferenceValue = starImages[i];
            }
            so.FindProperty("lockedOverlay").objectReferenceValue = lockedOverlay;
            so.FindProperty("devInfoText").objectReferenceValue = devInfoText;
            so.FindProperty("earnedStarColor").colorValue = StarEarnedColor;
            so.FindProperty("unearnedStarColor").colorValue = StarUnearnedColor;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Guards the exact bug class that shipped a stale prefab (legacy
            // Text left wired into a TMP_Text field, silently resolving to
            // null at load): confirms every serialized TMP reference is
            // non-null, actually lives under this prefab's own hierarchy
            // (never a scene/other-asset leftover), named as expected, and
            // is really a TextMeshProUGUI - not just a variable of that
            // static type pointing at something stale.
            bool wiringOk = true;
            wiringOk &= AssertTmpTextWiring(numberText, "LevelNumberText", prefabRoot);
            wiringOk &= AssertTmpTextWiring(gridSizeText, "GridSizeText", prefabRoot);
            wiringOk &= AssertTmpTextWiring(devInfoText, "DevInfoText", prefabRoot);

            if (!wiringOk)
            {
                Debug.LogError("LevelButtonPrefabBuilder2D: TMP wiring assertion(s) failed - prefab NOT saved (see errors above).");
            }
            else
            {
                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                success = true;

                if (logDetails)
                {
                    Debug.Log($"LevelButtonPrefabBuilder2D: {(prefabExists ? "updated" : "created")} '{PrefabPath}'.\n" +
                        "Hierarchy: LevelButton2D(Button,ColorTint) > CardInset, LevelNumberText(TMP,Bold,42pt), GridSizeText(TMP,24pt), " +
                        "StarRow(30px stars) > Star_0..2, CompletedBadge (inactive), LockedOverlay (inactive) > LockedText(TMP,Bold,20pt), " +
                        "DevInfoText(TMP,15pt, inactive, Editor/DevelopmentBuild only). Font: SHPinscher SDF (generated once from " +
                        "SHPinscher-Regular.otf - see TMPTextSetup2D).");
                }
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

    /// <summary>
    /// Builder-time safety net: returns false (and logs exactly which check
    /// failed) rather than letting a stale/mismatched TMP reference reach
    /// SaveAsPrefabAsset. Every condition here mirrors a real way the pre-fix
    /// prefab was broken - null reference, an object outside this prefab's
    /// own hierarchy, a renamed/misplaced child, or (defensively, in case a
    /// future refactor changes SetupTMPText's return type) a missing
    /// TextMeshProUGUI component.
    /// </summary>
    private static bool AssertTmpTextWiring(TextMeshProUGUI text, string expectedName, GameObject prefabRoot)
    {
        if (text == null)
        {
            Debug.LogError($"LevelButtonPrefabBuilder2D: '{expectedName}' TMP reference is null.");
            return false;
        }

        if (!text.transform.IsChildOf(prefabRoot.transform))
        {
            Debug.LogError($"LevelButtonPrefabBuilder2D: '{expectedName}' TMP reference does not belong to this prefab's hierarchy.");
            return false;
        }

        if (text.gameObject.name != expectedName)
        {
            Debug.LogError($"LevelButtonPrefabBuilder2D: TMP reference GameObject is named '{text.gameObject.name}', expected '{expectedName}'.");
            return false;
        }

        if (text.GetComponent<TextMeshProUGUI>() == null)
        {
            Debug.LogError($"LevelButtonPrefabBuilder2D: '{expectedName}' has no TextMeshProUGUI component.");
            return false;
        }

        return true;
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
            Debug.LogWarning($"LevelButtonPrefabBuilder2D: no sprite found at '{texturePath}'.");
        }
        return sprite;
    }
}
