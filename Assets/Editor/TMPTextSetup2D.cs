using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Level Select readability fix: TextMeshPro has never been set up in this
/// project - no Essential Resources imported, no TMP_FontAsset exists
/// anywhere (confirmed by audit). This is NOT a "wrong SDF material" bug
/// (there was no SDF text at all); the actual blur cause is legacy
/// UnityEngine.UI.Text (non-SDF, rasterized bitmap glyphs) rendered under
/// CanvasScaler.ScaleWithScreenSize, which is inherently soft at any scale
/// factor other than exactly 1.0 - see LevelButtonPrefabBuilder2D/
/// LevelSelectSceneBuilder2D's own doc comments for the full report.
///
/// GetOrCreateFontAsset() generates ONE SDF TMP_FontAsset from the existing
/// SHPinscher-Regular.otf (the only font file in the project - no new/remote
/// font is introduced) using AtlasPopulationMode.Dynamic, so it renders
/// whatever glyph is actually requested (Turkish diacritics included) on
/// demand from the real source font file - this sidesteps the classic
/// "missing character square" risk of a static baked character set entirely,
/// since the source font already renders every Turkish character correctly
/// today via legacy Text (proven by existing UI strings across the project).
/// Idempotent - only ever created once, found and reused on every subsequent
/// call. Only Regular weight exists for this font family; "Bold"-styled TMP
/// text below uses TMP's own faux-bold SDF synthesis (FontStyles.Bold),
/// which needs no separate bold font file.
/// </summary>
public static class TMPTextSetup2D
{
    private const string SourceFontPath = "Assets/SHPinscher-Regular11/SHPinscher-Regular.otf";
    private const string FontAssetFolder = "Assets/Fonts";
    private const string FontAssetPath = FontAssetFolder + "/SHPinscher SDF.asset";
    private const string TmpSettingsFolder = "Assets/Resources";
    private const string TmpSettingsPath = TmpSettingsFolder + "/TMP Settings.asset";

    /// <summary>Idempotent - returns the existing font asset if already created, otherwise generates it once from SHPinscher-Regular.otf.</summary>
    public static TMP_FontAsset GetOrCreateFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null)
        {
            return existing;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"TMPTextSetup2D: source font not found at '{SourceFontPath}'. Cannot create the SDF font asset.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(FontAssetFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Fonts");
        }

        // Dynamic atlas: starts near-empty and adds glyphs on demand at
        // runtime/edit-time as text actually requests them - correct for
        // Turkish text (ğĞüÜşŞıİöÖçÇ) without needing to hand-pick a static
        // character set, and never shows a "missing glyph" square as long as
        // the source .otf itself contains the glyph (it does - see class doc).
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            samplingPointSize: 90,
            atlasPadding: 5,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 1024,
            atlasHeight: 1024,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError("TMPTextSetup2D: TMP_FontAsset.CreateFontAsset returned null - aborting font asset creation.");
            return null;
        }

        fontAsset.name = "SHPinscher SDF";
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        if (fontAsset.material != null && AssetDatabase.GetAssetPath(fontAsset.material) != FontAssetPath)
        {
            fontAsset.material.name = "SHPinscher SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture != null && AssetDatabase.GetAssetPath(atlasTexture) != FontAssetPath)
                {
                    atlasTexture.name = "SHPinscher SDF Atlas";
                    AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                }
            }
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath);

        EnsureTmpSettings(fontAsset);

        Debug.Log($"TMPTextSetup2D: created '{FontAssetPath}' from '{SourceFontPath}' (Dynamic SDF atlas, 1024x1024, sampling 90).");
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    /// <summary>
    /// Defensive - creates a minimal project-wide TMP Settings asset (at the
    /// fixed Resources path TMP itself expects) pointing its default font at
    /// our new asset, so TMP internals never hit a null-default-font code
    /// path. Every TextMeshProUGUI this project creates explicitly assigns
    /// its own font anyway (never relies on this fallback) - this exists
    /// purely as a safety net, and is skipped entirely if a settings asset
    /// already exists (never overwrites a manually configured one).
    /// </summary>
    private static void EnsureTmpSettings(TMP_FontAsset fontAsset)
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null)
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(TmpSettingsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        var settings = ScriptableObject.CreateInstance<TMP_Settings>();
        SerializedObject so = new SerializedObject(settings);
        SerializedProperty defaultFontProp = so.FindProperty("m_defaultFontAsset");
        if (defaultFontProp != null)
        {
            defaultFontProp.objectReferenceValue = fontAsset;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(settings, TmpSettingsPath);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Shared idempotent TMP text setup - find-or-create the TextMeshProUGUI
    /// component and (re)apply every value unconditionally on every call, the
    /// same "always overwrite, never accumulate" convention every other
    /// builder helper in this project already follows (SetupText/SetupImage)
    /// - safe to call every time the builder runs without ever multiplying a
    /// font size or outline width. Face dilate/softness and shadow are
    /// deliberately never touched here (left at the auto-created material's
    /// defaults - dilate 0) per the "avoid excessive softness" requirement;
    /// outlineWidth/outlineColor go through TMP_Text's own instanced-material
    /// properties (auto-instances a per-object material copy, never mutates
    /// the shared font asset material other text elements also use).
    /// </summary>
    public static TextMeshProUGUI SetupTMPText(
        GameObject go, string content, TMP_FontAsset font, float fontSize, Color color,
        TextAlignmentOptions alignment, bool wrap, bool bold = false,
        bool autoSize = false, float autoSizeMin = 10f, float autoSizeMax = 40f,
        float outlineWidth = 0f, Color outlineColor = default)
    {
        // Migration cleanup: a GameObject rebuilt from an older (pre-TMP)
        // idempotent run may still carry a legacy Text component - having
        // both a Text AND a TextMeshProUGUI on the same object would render
        // two overlapping copies of the label. Remove the stale one first.
        Text legacyText = go.GetComponent<Text>();
        if (legacyText != null)
        {
            Object.DestroyImmediate(legacyText, allowDestroyingAssets: true);
        }

        var text = go.GetComponent<TextMeshProUGUI>();
        if (text == null) text = go.AddComponent<TextMeshProUGUI>();

        if (font == null)
        {
            Debug.LogError($"TMPTextSetup2D: SetupTMPText called with a null TMP_FontAsset for '{go.name}' - skipping font/material setup for this element to avoid an UnassignedReferenceException.");
            return text;
        }

        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = wrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;

        text.enableAutoSizing = autoSize;
        if (autoSize)
        {
            text.fontSizeMin = autoSizeMin;
            text.fontSizeMax = autoSizeMax;
        }

        if (outlineWidth > 0f)
        {
            // TMP_Text.outlineWidth's setter (SetOutlineThickness) and the
            // fontMaterial getter both assume a valid m_sharedMaterial
            // already exists. On a REBUILD of an existing prefab, a text
            // object can be found already parented under a GameObject an
            // earlier run left inactive (e.g. LockedText under
            // LockedOverlay, deactivated at the end of the previous build) -
            // that component has never been enabled, so TMP's internal
            // shared-material bookkeeping was never populated, and reading
            // .fontMaterial in that state throws
            // "UnassignedReferenceException: m_sharedMaterial ... has not
            // been assigned" instead of lazily creating it. Assigning
            // fontSharedMaterial directly (a plain field write, not a lazy
            // getter) first sidesteps that entirely, and repairs any
            // existing/broken component the exact same way new ones are set
            // up - safe to call every time, never creates a new Material
            // asset (only reassigns the existing shared reference).
            EnsureFontSharedMaterial(text, font, go.name);

            if (text.fontSharedMaterial != null)
            {
                _ = text.fontMaterial;
                text.outlineWidth = outlineWidth;
                text.outlineColor = outlineColor;
            }
        }

        return text;
    }

    /// <summary>
    /// Idempotent repair step for both newly added and previously-broken TMP
    /// components: assigns fontSharedMaterial straight from the font asset's
    /// own material only when missing. Never instantiates a new Material -
    /// re-running the builder can never accumulate duplicate material
    /// assets/instances from this method.
    /// </summary>
    private static void EnsureFontSharedMaterial(TextMeshProUGUI text, TMP_FontAsset font, string goName)
    {
        if (text.fontSharedMaterial != null)
            return;

        if (font.material == null)
        {
            Debug.LogError($"TMPTextSetup2D: font asset '{font.name}' has no material assigned - cannot configure outline for '{goName}'. Skipping material-dependent setup safely.");
            return;
        }

        text.fontSharedMaterial = font.material;
    }
}
