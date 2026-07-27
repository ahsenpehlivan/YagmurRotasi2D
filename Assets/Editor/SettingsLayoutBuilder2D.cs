using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Sole authority for the settings panel's INTERNAL vertical layout, in both
/// MainMenuScene2D's SettingsCard and InGameMenu2D.prefab's SettingsPage.
///
/// Root cause this replaces: MainMenuSceneBuilder2D hardcodes
/// SettingsCloseButton at a fixed Y (originally correct, back when no volume
/// rows existed below SfxToggleButton), and InGameMenuPrefabBuilder does the
/// same for SettingsBackButton. GameAudioSystemBuilder2D later inserted
/// MusicVolumeRow/SFXVolumeRow by reading SfxToggleButton's OWN position and
/// pushing the close/back button further down - but whichever builder ran
/// LAST always won, so re-running the base scene/prefab builder after the
/// audio builder silently snapped the close/back button back to its stale
/// pre-volume-row position, overlapping the rows. Two builders independently
/// owning the same controls' Y position is what caused the drift - not a
/// one-time mistake that can be patched with better math in either builder.
///
/// Fix: exactly one deterministic layout pass, driven entirely by a real
/// VerticalLayoutGroup on a dedicated "SettingsContent" child (never
/// scattered anchoredPosition math, never computed from another control's
/// CURRENT position). Every control is reparented into SettingsContent in
/// the required visual order and given its own fixed height;
/// VerticalLayoutGroup owns spacing/order from there, so re-running this any
/// number of times converges on the identical result - it never reads a
/// previous run's output as input.
///
/// GameAudioSystemBuilder2D still creates/wires the volume rows themselves
/// (Slider, AudioVolumeSlider2D, Label/Percent text) but no longer positions
/// anything - see InsertVolumeRows/BuildVolumeRow there. This builder never
/// creates a volume row itself - if one is missing, that means "Build Audio
/// System" hasn't been run yet, and this command aborts with a clear message
/// rather than trying to fabricate one out of scope.
/// </summary>
public static class SettingsLayoutBuilder2D
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene2D.unity";
    private const string InGameMenuPrefabPath = "Assets/Prefabs2D/UI/InGameMenu2D.prefab";

    private const float MainMenuRowSpacing = 16f;
    private const float MainMenuPaddingTop = 20f;
    private const float MainMenuPaddingBottom = 20f;
    private const float MainMenuBadgeHeight = 110f;
    private const float MainMenuToggleHeight = 110f;
    private const float MainMenuVolumeRowHeight = 100f;
    private const float MainMenuCloseHeight = 110f;

    private const float InGameRowSpacing = 24f;
    private const float InGamePaddingTop = 40f;
    private const float InGamePaddingBottom = 40f;
    private const float InGameBadgeHeight = 120f;
    private const float InGameToggleHeight = 120f;
    private const float InGameVolumeRowHeight = 100f;
    private const float InGameBackHeight = 120f;

    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

    [MenuItem("YagmurRotasi2D/UI/Repair Settings Layout")]
    public static void RepairSettingsLayout()
    {
        Scene originalActiveScene = EditorSceneManager.GetActiveScene();
        string originalActivePath = originalActiveScene.path;

        bool mainMenuOk = RepairMainMenuLayout(out bool mainMenuChanged);
        bool inGameOk = RepairInGameMenuLayout(out bool inGameChanged);

        if (!string.IsNullOrEmpty(originalActivePath) && originalActivePath != EditorSceneManager.GetActiveScene().path)
        {
            EditorSceneManager.OpenScene(originalActivePath, OpenSceneMode.Single);
        }

        Debug.Log("SettingsLayoutBuilder2D: done.\n" +
            $"  MainMenuScene2D: {(mainMenuOk ? (mainMenuChanged ? "repaired" : "already correct - no changes") : "FAILED - see errors above")}\n" +
            $"  InGameMenu2D.prefab: {(inGameOk ? (inGameChanged ? "repaired" : "already correct - no changes") : "FAILED - see errors above")}");
    }

    private static bool RepairMainMenuLayout(out bool changed)
    {
        changed = false;
        Scene scene = EditorSceneManager.GetActiveScene().path == MainMenuScenePath
            ? EditorSceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        MainMenuController2D controller = Object.FindFirstObjectByType<MainMenuController2D>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogError("SettingsLayoutBuilder2D: MainMenuScene2D has no MainMenuController2D - cannot repair.");
            return false;
        }

        var controllerSO = new SerializedObject(controller);
        Button musicToggle = controllerSO.FindProperty("musicToggleButton").objectReferenceValue as Button;
        Button sfxToggle = controllerSO.FindProperty("sfxToggleButton").objectReferenceValue as Button;
        Button closeButton = controllerSO.FindProperty("settingsCloseButton").objectReferenceValue as Button;

        if (musicToggle == null || sfxToggle == null || closeButton == null)
        {
            Debug.LogError("SettingsLayoutBuilder2D: MainMenuController2D.musicToggleButton/sfxToggleButton/settingsCloseButton are not all wired - cannot repair.");
            return false;
        }

        Transform settingsCard = musicToggle.transform.parent;
        if (settingsCard == null || sfxToggle.transform.parent != settingsCard || closeButton.transform.parent != settingsCard)
        {
            Debug.LogError("SettingsLayoutBuilder2D: MusicToggleButton/SfxToggleButton/SettingsCloseButton do not share a common parent (SettingsCard) - cannot repair.");
            return false;
        }

        Transform settingsTitleBadge = FindDescendant(settingsCard, "SettingsTitleBadge");
        Transform musicVolumeRow = FindDescendant(settingsCard, "MusicVolumeRow");
        Transform sfxVolumeRow = FindDescendant(settingsCard, "SFXVolumeRow");

        if (settingsTitleBadge == null)
        {
            Debug.LogError("SettingsLayoutBuilder2D: 'SettingsTitleBadge' not found under SettingsCard - cannot repair.");
            return false;
        }
        if (musicVolumeRow == null || sfxVolumeRow == null)
        {
            Debug.LogError("SettingsLayoutBuilder2D: MusicVolumeRow/SFXVolumeRow not found under SettingsCard. Run " +
                "'YagmurRotasi2D > Audio > Build Audio System' first, then re-run this command.");
            return false;
        }

        RectTransform cardRt = settingsCard.GetComponent<RectTransform>();
        RectTransform content = FindOrCreateContent(settingsCard, "SettingsContent", ref changed);
        changed |= ConfigureVerticalLayout(content, MainMenuRowSpacing, MainMenuPaddingTop, MainMenuPaddingBottom);

        var ordered = new (Transform t, float height)[]
        {
            (settingsTitleBadge, MainMenuBadgeHeight),
            (musicToggle.transform, MainMenuToggleHeight),
            (musicVolumeRow, MainMenuVolumeRowHeight),
            (sfxToggle.transform, MainMenuToggleHeight),
            (sfxVolumeRow, MainMenuVolumeRowHeight),
            (closeButton.transform, MainMenuCloseHeight),
        };

        changed |= ApplyOrderedStack(content, ordered);

        float requiredHeight = MainMenuPaddingTop + MainMenuPaddingBottom
            + MainMenuBadgeHeight + MainMenuToggleHeight * 2f + MainMenuVolumeRowHeight * 2f + MainMenuCloseHeight
            + MainMenuRowSpacing * (ordered.Length - 1);

        if (cardRt != null && requiredHeight > cardRt.sizeDelta.y + 0.5f)
        {
            Vector2 size = cardRt.sizeDelta;
            size.y = requiredHeight;
            cardRt.sizeDelta = size;
            changed = true;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        return true;
    }

    private static bool RepairInGameMenuLayout(out bool changed)
    {
        changed = false;
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(InGameMenuPrefabPath);
        try
        {
            Transform mainPanel = prefabRoot.transform.Find("MainPanel");
            Transform settingsPage = mainPanel != null ? mainPanel.Find("SettingsPage") : null;
            if (settingsPage == null)
            {
                Debug.LogError("SettingsLayoutBuilder2D: InGameMenu2D.prefab has no MainPanel/SettingsPage - cannot repair.");
                return false;
            }

            Transform settingsTitleBadge = settingsPage.Find("SettingsTitleBadge");
            Transform musicToggle = settingsPage.Find("MusicToggleButton");
            Transform sfxToggle = settingsPage.Find("SfxToggleButton");
            Transform backButton = settingsPage.Find("SettingsBackButton");
            Transform musicVolumeRow = settingsPage.Find("MusicVolumeRow");
            Transform sfxVolumeRow = settingsPage.Find("SFXVolumeRow");

            if (settingsTitleBadge == null || musicToggle == null || sfxToggle == null || backButton == null)
            {
                Debug.LogError("SettingsLayoutBuilder2D: InGameMenu2D.prefab's SettingsPage is missing a required control - cannot repair.");
                return false;
            }
            if (musicVolumeRow == null || sfxVolumeRow == null)
            {
                Debug.LogError("SettingsLayoutBuilder2D: MusicVolumeRow/SFXVolumeRow not found under SettingsPage. Run " +
                    "'YagmurRotasi2D > Audio > Build Audio System' first, then re-run this command.");
                return false;
            }

            RectTransform content = FindOrCreateContent(settingsPage, "SettingsContent", ref changed);
            changed |= ConfigureVerticalLayout(content, InGameRowSpacing, InGamePaddingTop, InGamePaddingBottom);

            var ordered = new (Transform t, float height)[]
            {
                (settingsTitleBadge, InGameBadgeHeight),
                (musicToggle, InGameToggleHeight),
                (musicVolumeRow, InGameVolumeRowHeight),
                (sfxToggle, InGameToggleHeight),
                (sfxVolumeRow, InGameVolumeRowHeight),
                (backButton, InGameBackHeight),
            };

            changed |= ApplyOrderedStack(content, ordered);

            float requiredHeight = InGamePaddingTop + InGamePaddingBottom
                + InGameBadgeHeight + InGameToggleHeight * 2f + InGameVolumeRowHeight * 2f + InGameBackHeight
                + InGameRowSpacing * (ordered.Length - 1);

            RectTransform pageRt = settingsPage.GetComponent<RectTransform>();
            if (pageRt != null && requiredHeight > pageRt.rect.height + 0.5f)
            {
                // SettingsPage stretch-fills MainPanel (shared with
                // MainMenuPage) - never resized here. If content genuinely
                // doesn't fit, that's a real design overflow to fix by hand,
                // not something safe to auto-grow (growing MainPanel would
                // also resize the unrelated main page).
                Debug.LogWarning($"SettingsLayoutBuilder2D: InGameMenu2D SettingsPage content requires {requiredHeight}px but SettingsPage is only {pageRt.rect.height}px tall - content may clip. MainPanel was NOT resized (shared with MainMenuPage).");
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            if (changed)
            {
                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, InGameMenuPrefabPath);
            }

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>Find-or-create the dedicated vertical content root, stretched to fill its parent. Never touches sibling order of other existing children (e.g. the card's background inset stays wherever it already is).</summary>
    private static RectTransform FindOrCreateContent(Transform parent, string name, ref bool changed)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            changed = true;
        }
        else
        {
            go = existing.gameObject;
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.one || rt.offsetMin != Vector2.zero || rt.offsetMax != Vector2.zero)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            changed = true;
        }

        return rt;
    }

    /// <summary>Sets the deterministic VerticalLayoutGroup configuration; returns true only if a value actually differed from what was already there.</summary>
    private static bool ConfigureVerticalLayout(RectTransform content, float spacing, float paddingTop, float paddingBottom)
    {
        bool changed = false;
        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            changed = true;
        }

        changed |= AssignIfDifferent(vlg.childAlignment, TextAnchor.UpperCenter, v => vlg.childAlignment = v);
        changed |= AssignIfDifferent(vlg.childControlWidth, false, v => vlg.childControlWidth = v);
        changed |= AssignIfDifferent(vlg.childControlHeight, false, v => vlg.childControlHeight = v);
        changed |= AssignIfDifferent(vlg.childForceExpandWidth, false, v => vlg.childForceExpandWidth = v);
        changed |= AssignIfDifferent(vlg.childForceExpandHeight, false, v => vlg.childForceExpandHeight = v);
        changed |= AssignIfDifferent(vlg.childScaleWidth, false, v => vlg.childScaleWidth = v);
        changed |= AssignIfDifferent(vlg.childScaleHeight, false, v => vlg.childScaleHeight = v);
        changed |= AssignIfDifferent(vlg.spacing, spacing, v => vlg.spacing = v);

        RectOffset padding = vlg.padding;
        int top = Mathf.RoundToInt(paddingTop);
        int bottom = Mathf.RoundToInt(paddingBottom);
        if (padding.top != top || padding.bottom != bottom || padding.left != 0 || padding.right != 0)
        {
            vlg.padding = new RectOffset(0, 0, top, bottom);
            changed = true;
        }

        return changed;
    }

    private static bool AssignIfDifferent<T>(T currentValue, T newValue, System.Action<T> setter)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(currentValue, newValue)) return false;
        setter(newValue);
        return true;
    }

    /// <summary>Reparents every control into content in the given order (SetSiblingIndex guarantees exact order regardless of prior order) and sets each one's own fixed height. Never sets Y position directly - VerticalLayoutGroup owns that. Returns true if anything actually changed.</summary>
    private static bool ApplyOrderedStack(RectTransform content, (Transform t, float height)[] ordered)
    {
        bool changed = false;

        for (int i = 0; i < ordered.Length; i++)
        {
            Transform t = ordered[i].t;
            float height = ordered[i].height;

            if (t.parent != content)
            {
                t.SetParent(content, false);
                changed = true;
            }
            if (t.GetSiblingIndex() != i)
            {
                t.SetSiblingIndex(i);
                changed = true;
            }

            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (rt.anchorMin != TopCenter || rt.anchorMax != TopCenter || rt.pivot != TopCenter)
                {
                    rt.anchorMin = TopCenter;
                    rt.anchorMax = TopCenter;
                    rt.pivot = TopCenter;
                    changed = true;
                }

                if (!Mathf.Approximately(rt.sizeDelta.y, height))
                {
                    Vector2 size = rt.sizeDelta;
                    size.y = height;
                    rt.sizeDelta = size;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name) return child;

            Transform found = FindDescendant(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
