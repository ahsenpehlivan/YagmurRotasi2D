using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the imported "SHPinscher-Regular11" font to every visible game Text
/// component, replacing the earlier Thaleah pixel font everywhere.
///
/// Discovery: the font lives at
/// Assets/SHPinscher-Regular11/SHPinscher-Regular.otf (imported with
/// includeFontData=1, i.e. a genuine embeddable/dynamic Font asset - Unity
/// rasterizes whatever glyphs the OTF outline data actually contains, not a fixed
/// pre-baked bitmap subset). This project still has no TextMeshPro package
/// installed and no TMP usage anywhere in code, so - per the task's own
/// instructions - TMP installation/conversion is out of scope; the Unity Font
/// asset is assigned directly to the existing UnityEngine.UI.Text components.
///
/// Turkish glyph coverage is verified for real via UnityEngine.Font.HasCharacter,
/// which genuinely queries the underlying font engine (not a guess) - see the
/// logged per-character report every time this runs.
/// </summary>
public static class SHPinscherFontBinder
{
    private const string FontPath = "Assets/SHPinscher-Regular11/SHPinscher-Regular.otf";

    private static readonly char[] TurkishCharacters =
    {
        'Ç', 'ç', 'Ğ', 'ğ', 'İ', 'ı', 'Ö', 'ö', 'Ş', 'ş', 'Ü', 'ü'
    };

    [MenuItem("YagmurRotasi2D/Apply SHPinscher Font Everywhere")]
    public static void ApplyMenuCommand()
    {
        TryApplyFont(true);
    }

    public static bool TryApplyFont(bool logDetails)
    {
        Font shPinscherFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (shPinscherFont == null)
        {
            Debug.LogError($"SHPinscherFontBinder: no Font asset found at '{FontPath}'. " +
                "Nothing applied - visible text keeps its current font.");
            return false;
        }

        if (logDetails)
        {
            LogTurkishGlyphValidation(shPinscherFont);
        }

        Text[] allTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var log = new List<string>();
        int changed = 0;

        foreach (Text text in allTexts)
        {
            string path = GetHierarchyPath(text.transform);
            string previousFont = text.font != null ? text.font.name : "(none)";

            if (text.font != shPinscherFont)
            {
                text.font = shPinscherFont;
                EditorUtility.SetDirty(text);
                changed++;
            }

            log.Add($"  {path}: previousFont={previousFont}, newFont={shPinscherFont.name}, " +
                $"size={text.fontSize}, alignment={text.alignment}, bestFit={text.resizeTextForBestFit}");
        }

        if (logDetails)
        {
            Debug.Log($"SHPinscherFontBinder: applied '{shPinscherFont.name}' ('{FontPath}') to " +
                $"{allTexts.Length} Text component(s) in the active scene ({changed} changed, " +
                $"{allTexts.Length - changed} already correct - includes inactive objects like InfoPanel's texts).\n" +
                string.Join("\n", log));
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        return true;
    }

    /// <summary>Logs a real, verified per-character result (UnityEngine.Font.HasCharacter genuinely queries the font engine) rather than assuming coverage.</summary>
    private static void LogTurkishGlyphValidation(Font font)
    {
        var results = new List<string>();
        var missing = new List<char>();

        foreach (char c in TurkishCharacters)
        {
            bool has = font.HasCharacter(c);
            results.Add($"  '{c}' (U+{(int)c:X4}): {(has ? "OK" : "MISSING")}");
            if (!has) missing.Add(c);
        }

        Debug.Log("SHPinscherFontBinder: Turkish glyph validation (via Font.HasCharacter):\n" + string.Join("\n", results));

        if (missing.Count > 0)
        {
            Debug.LogWarning($"SHPinscherFontBinder: SHPinscher-Regular11 is MISSING {missing.Count} Turkish character(s): " +
                string.Join(" ", missing) + ". The font is still applied everywhere as requested - this is a content " +
                "limitation of the font file itself, not something a code change can fix. Consider a different font or " +
                "asking the font's author for extended glyph coverage if these characters must render correctly.");
        }
        else
        {
            Debug.Log("SHPinscherFontBinder: all tested Turkish characters are present in SHPinscher-Regular11.");
        }
    }

    private static string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
