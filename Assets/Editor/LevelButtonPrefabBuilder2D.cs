using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Builds/updates the dynamically-instantiated campaign level card prefab
/// (Assets/Prefabs2D/UI/LevelButton2D.prefab) - LevelSelectController2D
/// instantiates 100 copies of this at runtime, one per CampaignLevelCatalog2D
/// entry; there is no manually-authored button per level anywhere. Built
/// entirely out of the existing shared UI package (Assets/Art2D/FinalSprites/UI/)
/// plus the SHPinscher-Regular11 font, matching SuccessPanelPrefabBuilder2D/
/// InGameMenuPrefabBuilder's exact idempotent find-or-create pattern. Unlike
/// those two, this card uses Selectable.Transition.ColorTint (not SpriteSwap) -
/// a deliberate choice so hover AND keyboard/gamepad "selected" both get a
/// clearly distinct tint (Phase 9A Part B/G - "strong hover/selected feedback
/// for web mouse users").
/// </summary>
public static class LevelButtonPrefabBuilder2D
{
    public const string PrefabFolder = "Assets/Prefabs2D/UI";
    public const string PrefabPath = PrefabFolder + "/LevelButton2D.prefab";

    private const string UiFolder = "Assets/Art2D/FinalSprites/UI";
    private const string FontPath = "Assets/SHPinscher-Regular11/SHPinscher-Regular.otf";

    private const float CardWidth = 190f;
    private const float CardHeight = 210f;
    private const float CardInset = 6f;

    private static readonly Color CardTint = new Color(0.80f, 0.90f, 0.98f, 1f); // pastel sky blue
    private static readonly Color NumberColor = new Color(0.16f, 0.24f, 0.38f, 1f); // dark blue
    private static readonly Color GridSizeColor = new Color(0.35f, 0.40f, 0.48f, 1f);
    private static readonly Color DevInfoColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    private static readonly Color CompletedTint = new Color(1f, 0.82f, 0.25f, 1f); // gold
    private const float StarUnearnedAlpha = 0.3f;

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

        Font shPinscherFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        Sprite cardBase = LoadFirstSprite(UiFolder + "/Badges/white.png");
        Sprite cardInlay = LoadFirstSprite(UiFolder + "/Badges/white_inlay.png");
        Sprite starSprite = LoadFirstSprite(UiFolder + "/Stars/star.png");

        if (shPinscherFont == null || cardBase == null || starSprite == null)
        {
            Debug.LogError("LevelButtonPrefabBuilder2D: one or more required assets could not be loaded (see warnings above). Aborting.");
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

            GameObject cardInsetGO = FindOrCreateChild(prefabRoot.transform, "CardInset");
            StretchFill(RectOf(cardInsetGO), CardInset);
            SetupImage(cardInsetGO, cardInlay, false, Color.white);
            cardInsetGO.transform.SetAsFirstSibling();

            GameObject numberGO = FindOrCreateChild(prefabRoot.transform, "LevelNumberText");
            SetAnchoredRect(RectOf(numberGO), TopStretch0, TopStretch1, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-16f, 74f));
            Text numberText = SetupText(numberGO, "1", shPinscherFont, 56, NumberColor, TextAnchor.MiddleCenter, false);

            GameObject gridSizeGO = FindOrCreateChild(prefabRoot.transform, "GridSizeText");
            SetAnchoredRect(RectOf(gridSizeGO), TopStretch0, TopStretch1, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(-16f, 28f));
            Text gridSizeText = SetupText(gridSizeGO, "5×5", shPinscherFont, 24, GridSizeColor, TextAnchor.MiddleCenter, false);

            GameObject starRow = FindOrCreateChild(prefabRoot.transform, "StarRow");
            SetAnchoredRect(RectOf(starRow), Center, Center, Center, new Vector2(0f, -76f), new Vector2(120f, 30f));
            var layout = starRow.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = starRow.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
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
                RectOf(starGO).sizeDelta = new Vector2(22f, 22f);
                Image starImage = SetupImage(starGO, starSprite, false, new Color(1f, 1f, 1f, StarUnearnedAlpha));
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
            SetupText(lockedTextGO, "Kilitli", shPinscherFont, 30, Color.white, TextAnchor.MiddleCenter, true);
            lockedOverlay.SetActive(false);

            GameObject devInfoGO = FindOrCreateChild(prefabRoot.transform, "DevInfoText");
            SetAnchoredRect(RectOf(devInfoGO), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(-8f, 22f));
            Text devInfoText = SetupText(devInfoGO, "", shPinscherFont, 13, DevInfoColor, TextAnchor.LowerCenter, true,
                bestFit: true, minSize: 9, maxSize: 13);
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
            so.FindProperty("earnedStarAlpha").floatValue = 1f;
            so.FindProperty("unearnedStarAlpha").floatValue = StarUnearnedAlpha;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            success = true;

            if (logDetails)
            {
                Debug.Log($"LevelButtonPrefabBuilder2D: {(prefabExists ? "updated" : "created")} '{PrefabPath}'.\n" +
                    "Hierarchy: LevelButton2D(Button,ColorTint) > CardInset, LevelNumberText, GridSizeText, StarRow > Star_0..2, " +
                    "CompletedBadge (inactive), LockedOverlay (inactive) > LockedText, DevInfoText (inactive, Editor/DevelopmentBuild only).");
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
            Debug.LogWarning($"LevelButtonPrefabBuilder2D: no sprite found at '{texturePath}'.");
        }
        return sprite;
    }
}
