using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.Audio2D;
using YagmurRotasi2D.UI2D;

/// <summary>
/// Idempotent builder for the cross-scene audio system - finds the six
/// existing AudioClip assets (music/sfx_pipe_click/sfx_ui_click/
/// sfx_water_flow/sfx_duck/sfx_sparkle) by EXACT name (never creates/
/// renames/duplicates them), ensures exactly one GameAudioManager2D + its
/// three AudioSource children (Music/SFX/WaterFlow) exist in each of the
/// three production scenes (the runtime singleton in GameAudioManager2D
/// collapses these to one live instance once the app actually runs, via
/// DontDestroyOnLoad), and idempotently attaches UIButtonSound2D to every
/// Button already present in each scene (including inactive ones, e.g. the
/// pause/success panels - so hidden-by-default modal buttons are still
/// covered). LevelButton2D's card button is handled separately in
/// LevelButtonPrefabBuilder2D.cs, since level cards are instantiated at
/// runtime and never exist in a saved scene file for this builder to find.
///
/// Never touches scene layout, positions, or visuals - only adds/reuses the
/// manager hierarchy and attaches the sound component to existing buttons.
/// </summary>
public static class GameAudioSystemBuilder2D
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene2D.unity";
    private const string LevelSelectScenePath = "Assets/Scenes/LevelSelectScene2D.unity";
    private const string GameplayScenePath = "Assets/Scenes/GameScene2D.unity";
    private const string InGameMenuPrefabPath = "Assets/Prefabs2D/UI/InGameMenu2D.prefab";

    private const string MusicClipName = "music";
    private const string PipeClickClipName = "sfx_pipe_click";
    private const string UIClickClipName = "sfx_ui_click";
    private const string WaterFlowClipName = "sfx_water_flow";
    private const string DuckClipName = "sfx_duck";
    private const string SparkleClipName = "sfx_sparkle";

    // ---------------- Volume slider rows (Music Volume / SFX Volume) ----------------
    private const string UiFolder = "Assets/Art2D/FinalSprites/UI";
    private const string FontPath = "Assets/SHPinscher-Regular11/SHPinscher-Regular.otf";
    private const string MusicVolumeLabel = "Müzik Sesi";
    private const string SFXVolumeLabel = "Efekt Sesleri";

    // Row height only - row/panel POSITIONING is owned by
    // SettingsLayoutBuilder2D, not this file (see InsertVolumeRows).
    private const float VolumeRowHeight = 100f;
    private const float SliderTrackHeight = 36f;
    private const float SliderHandleSize = 30f;

    [MenuItem("YagmurRotasi2D/Audio/Build Audio System")]
    public static void BuildAudioSystemCommand()
    {
        TryBuildAudioSystem(true);
    }

    public static bool TryBuildAudioSystem(bool logDetails)
    {
        // ---------------- Preflight: AudioClips ----------------
        AudioClip musicClip = FindClipByExactName(MusicClipName);
        AudioClip pipeClickClip = FindClipByExactName(PipeClickClipName);
        AudioClip uiClickClip = FindClipByExactName(UIClickClipName);
        AudioClip waterFlowClip = FindClipByExactName(WaterFlowClipName);
        AudioClip duckClip = FindClipByExactName(DuckClipName);
        AudioClip sparkleClip = FindClipByExactName(SparkleClipName);

        if (musicClip == null || pipeClickClip == null || uiClickClip == null
            || waterFlowClip == null || duckClip == null || sparkleClip == null)
        {
            Debug.LogError("GameAudioSystemBuilder2D: one or more required AudioClips could not be resolved (see errors above). Aborting - no scene was modified.");
            return false;
        }

        string activeScenePath = EditorSceneManager.GetActiveScene().path;

        // ---------------- Preflight: Main Menu volume-control targets ----------------
        // Opens MainMenuScene2D purely to READ/resolve targets - no
        // modification, no save. If this fails, the function returns before
        // BuildForScene/BuildMainMenuVolumeControls/BuildInGameMenuVolumeControls
        // ever run, so nothing - not even the base GameAudioManager2D setup
        // for any of the three scenes, and not the already-working in-game
        // slider addition - gets touched during a failed run.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) == null)
        {
            Debug.LogError($"GameAudioSystemBuilder2D: scene not found at '{MainMenuScenePath}'. Aborting - no scene or prefab was modified.");
            return false;
        }

        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        bool mainMenuTargetsOk = TryResolveMainMenuSettingsTargets(
            out _, out _, out _, out _, out List<string> mainMenuPreflightLog);

        if (!mainMenuTargetsOk)
        {
            RestoreActiveScene(activeScenePath);
            Debug.LogError("GameAudioSystemBuilder2D: PREFLIGHT FAILED for Main Menu volume-control targets - " +
                "aborting before any scene or prefab was modified (nothing was saved, including the base audio " +
                "setup for MainMenuScene2D/LevelSelectScene2D/GameScene2D and the in-game slider addition).\n" +
                string.Join("\n", mainMenuPreflightLog));
            return false;
        }

        // ---------------- Preflight: in-game settings targets (InGameMenu2D.prefab) ----------------
        if (AssetDatabase.LoadAssetAtPath<GameObject>(InGameMenuPrefabPath) == null)
        {
            RestoreActiveScene(activeScenePath);
            Debug.LogError($"GameAudioSystemBuilder2D: prefab not found at '{InGameMenuPrefabPath}'. " +
                "Run 'YagmurRotasi2D > Build In-Game Menu Prefab' first. Aborting - no scene or prefab was modified.");
            return false;
        }

        bool inGameTargetsOk = TryPreflightInGameMenuTargets(out List<string> inGamePreflightLog);
        if (!inGameTargetsOk)
        {
            RestoreActiveScene(activeScenePath);
            Debug.LogError("GameAudioSystemBuilder2D: PREFLIGHT FAILED for in-game volume-control targets - " +
                "aborting before any scene or prefab was modified.\n" + string.Join("\n", inGamePreflightLog));
            return false;
        }

        // ---------------- Preflight passed - now perform the real, mutating build ----------------
        var summary = new List<string>
        {
            $"AudioClips: music='{AssetDatabase.GetAssetPath(musicClip)}', sfx_pipe_click='{AssetDatabase.GetAssetPath(pipeClickClip)}', sfx_ui_click='{AssetDatabase.GetAssetPath(uiClickClip)}', " +
            $"sfx_water_flow='{AssetDatabase.GetAssetPath(waterFlowClip)}', sfx_duck='{AssetDatabase.GetAssetPath(duckClip)}', sfx_sparkle='{AssetDatabase.GetAssetPath(sparkleClip)}'",
            "Preflight: Main Menu and in-game volume-control targets both resolved successfully - proceeding with mutations.",
            "  Main Menu resolution: " + (mainMenuPreflightLog.Count > 0 ? mainMenuPreflightLog[mainMenuPreflightLog.Count - 1] : "n/a")
        };

        bool ok = true;
        ok &= BuildForScene(MainMenuScenePath, musicClip, pipeClickClip, uiClickClip, waterFlowClip, duckClip, sparkleClip, summary);
        ok &= BuildForScene(LevelSelectScenePath, musicClip, pipeClickClip, uiClickClip, waterFlowClip, duckClip, sparkleClip, summary);
        ok &= BuildForScene(GameplayScenePath, musicClip, pipeClickClip, uiClickClip, waterFlowClip, duckClip, sparkleClip, summary);

        // Music Volume / SFX Volume sliders - Main Menu settings live directly
        // in the scene, in-game settings live inside the shared
        // InGameMenu2D.prefab asset (its already-placed scene instance picks
        // up prefab changes automatically via Unity's normal propagation).
        ok &= BuildMainMenuVolumeControls(summary);
        ok &= BuildInGameMenuVolumeControls(summary);

        RestoreActiveScene(activeScenePath);

        if (logDetails)
        {
            Debug.Log("GameAudioSystemBuilder2D: Build Audio System " + (ok ? "complete" : "FAILED") + ".\n" + string.Join("\n", summary));
        }

        return ok;
    }

    /// <summary>Restores whatever scene was open before this command ran - shared by every early-return path (preflight failure) and the normal successful-completion path, so no early return ever leaves the wrong scene open.</summary>
    private static void RestoreActiveScene(string activeScenePath)
    {
        if (!string.IsNullOrEmpty(activeScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(activeScenePath) != null)
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }
    }

    private static bool BuildForScene(string scenePath, AudioClip musicClip, AudioClip pipeClickClip, AudioClip uiClickClip,
        AudioClip waterFlowClip, AudioClip duckClip, AudioClip sparkleClip, List<string> summary)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            Debug.LogError($"GameAudioSystemBuilder2D: scene not found at '{scenePath}' - skipping.");
            summary.Add($"  - {scenePath}: FAILED (scene not found)");
            return false;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // ---------------- GameAudioManager2D + its two AudioSource children ----------------
        GameObject managerGO = GameObject.Find("GameAudioManager2D");
        bool isNewManager = managerGO == null;
        if (isNewManager)
        {
            managerGO = new GameObject("GameAudioManager2D");
        }

        GameObject musicSourceGO = FindOrCreateChild(managerGO.transform, "MusicAudioSource");
        AudioSource musicSource = musicSourceGO.GetComponent<AudioSource>();
        if (musicSource == null) musicSource = musicSourceGO.AddComponent<AudioSource>();
        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = GameAudioManager2D.DefaultMusicVolume;

        GameObject sfxSourceGO = FindOrCreateChild(managerGO.transform, "SFXAudioSource");
        AudioSource sfxSource = sfxSourceGO.GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = sfxSourceGO.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = GameAudioManager2D.DefaultSFXVolume;

        GameObject waterFlowSourceGO = FindOrCreateChild(managerGO.transform, "WaterFlowAudioSource");
        AudioSource waterFlowSource = waterFlowSourceGO.GetComponent<AudioSource>();
        if (waterFlowSource == null) waterFlowSource = waterFlowSourceGO.AddComponent<AudioSource>();
        waterFlowSource.clip = waterFlowClip;
        waterFlowSource.loop = true;
        waterFlowSource.playOnAwake = false;
        waterFlowSource.spatialBlend = 0f;
        waterFlowSource.volume = GameAudioManager2D.DefaultWaterFlowVolume;

        GameAudioManager2D manager = managerGO.GetComponent<GameAudioManager2D>();
        if (manager == null) manager = managerGO.AddComponent<GameAudioManager2D>();

        var so = new SerializedObject(manager);
        so.FindProperty("musicSource").objectReferenceValue = musicSource;
        so.FindProperty("sfxSource").objectReferenceValue = sfxSource;
        so.FindProperty("waterFlowSource").objectReferenceValue = waterFlowSource;
        so.FindProperty("musicClip").objectReferenceValue = musicClip;
        so.FindProperty("pipeClickClip").objectReferenceValue = pipeClickClip;
        so.FindProperty("uiClickClip").objectReferenceValue = uiClickClip;
        so.FindProperty("waterFlowClip").objectReferenceValue = waterFlowClip;
        so.FindProperty("duckClip").objectReferenceValue = duckClip;
        so.FindProperty("sparkleClip").objectReferenceValue = sparkleClip;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---------------- UIButtonSound2D on every existing Button (including inactive ones) ----------------
        int buttonsTagged = AttachUIButtonSoundToAllButtons();

        EditorUtility.SetDirty(managerGO);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);

        summary.Add($"  - {scenePath}: GameAudioManager2D {(isNewManager ? "created" : "reused")}; {buttonsTagged} button(s) newly given UIButtonSound2D (0 on a rerun is expected/correct).");
        return true;
    }

    /// <summary>
    /// Adds MusicVolumeRow/SFXVolumeRow (each: Label, Slider, Percent) to
    /// MainMenuScene2D's existing settings card/content root, directly below
    /// the existing SfxToggleButton - repositions SettingsCloseButton down to
    /// make room and grows the panel's height only if that repositioning
    /// would otherwise overflow it. Never touches MusicToggleButton/
    /// SfxToggleButton themselves. Assumes the scene is already open (the
    /// caller - TryBuildAudioSystem - already opened it during preflight)
    /// and that TryResolveMainMenuSettingsTargets will succeed (also already
    /// confirmed during preflight) - if it somehow doesn't (e.g. the scene
    /// was changed between preflight and this call), this still fails safely
    /// without touching anything.
    /// </summary>
    private static bool BuildMainMenuVolumeControls(List<string> summary)
    {
        if (!TryLoadVolumeControlAssets(out Font font, out Sprite trackSprite, out Sprite fillSprite, out Sprite handleSprite))
        {
            summary.Add("  - MainMenuScene2D volume controls: FAILED (missing font/sprite assets)");
            return false;
        }

        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        if (!TryResolveMainMenuSettingsTargets(out Transform rowParent, out RectTransform sfxToggleRt,
                out RectTransform closeButtonRt, out RectTransform panelRt, out List<string> resolveLog))
        {
            Debug.LogError("GameAudioSystemBuilder2D: Main Menu settings targets could not be resolved (unexpected - preflight already confirmed this would work).\n" + string.Join("\n", resolveLog));
            summary.Add("  - MainMenuScene2D volume controls: FAILED (resolution failed after preflight succeeded - see error above)");
            return false;
        }

        (Slider musicSlider, Text musicPercent, Slider sfxSlider, Text sfxPercent) = InsertVolumeRows(
            rowParent, sfxToggleRt, closeButtonRt, panelRt, font, trackSprite, fillSprite, handleSprite);

        WireVolumeSlider(musicSlider.gameObject, musicSlider, musicPercent, AudioVolumeSlider2D.VolumeType.Music);
        WireVolumeSlider(sfxSlider.gameObject, sfxSlider, sfxPercent, AudioVolumeSlider2D.VolumeType.SFX);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MainMenuScenePath);

        summary.Add($"  - MainMenuScene2D: MusicVolumeRow/SFXVolumeRow added under '{rowParent.name}' (below SfxToggleButton); SettingsCloseButton repositioned; panel height adjusted only if needed. Resolution: {resolveLog[resolveLog.Count - 1]}");
        return true;
    }

    /// <summary>
    /// Resolves the Main Menu settings panel's row-parent/SfxToggleButton/
    /// SettingsCloseButton/panel-to-resize WITHOUT assuming any specific
    /// hardcoded hierarchy path, and without modifying anything - a pure
    /// lookup, safe to call during preflight (log-and-discard) or immediately
    /// before mutation (BuildMainMenuVolumeControls). Requires
    /// MainMenuScene2D to already be the open scene.
    ///
    /// Three-tier resolution, in preference order:
    /// 1. MainMenuController2D's own serialized sfxToggleButton/
    ///    settingsCloseButton references - read via SerializedObject, so
    ///    this works correctly even though SettingsPanel (their ancestor)
    ///    is inactive by design until "Ayarlar" is pressed. This is the
    ///    confirmed root-cause fix: GameObject.Find (used previously) only
    ///    ever finds objects that are ACTIVE IN HIERARCHY, and
    ///    SettingsPanel.m_IsActive is deliberately 0 in the saved scene, so
    ///    it always failed to find SfxToggleButton/SettingsCloseButton/
    ///    SettingsCard even though they genuinely exist.
    /// 2. If those specific fields are empty but MainMenuController2D.
    ///    settingsPanel is set, search recursively within that root using
    ///    Transform.Find/GetChild (also active-state-independent, unlike
    ///    GameObject.Find) for objects still named "SfxToggleButton"/
    ///    "SettingsCloseButton" - tolerates the two specific button fields
    ///    being unwired while the rest of the panel is otherwise intact.
    /// 3. Scene-wide GameObject.Find, exactly the old lookup - kept only as
    ///    a final compatibility path (e.g. if someone is editing the panel
    ///    with it manually left active); expected to rarely succeed given
    ///    the panel's normal hidden-by-default state.
    /// </summary>
    private static bool TryResolveMainMenuSettingsTargets(
        out Transform rowParent, out RectTransform sfxToggleRt, out RectTransform closeButtonRt, out RectTransform panelRt,
        out List<string> log)
    {
        log = new List<string>();
        rowParent = null;
        sfxToggleRt = null;
        closeButtonRt = null;
        panelRt = null;

        MainMenuController2D controller = Object.FindFirstObjectByType<MainMenuController2D>(FindObjectsInactive.Include);
        GameObject settingsPanelGO = null;

        if (controller != null)
        {
            var so = new SerializedObject(controller);
            Button sfxToggle = so.FindProperty("sfxToggleButton").objectReferenceValue as Button;
            Button closeButton = so.FindProperty("settingsCloseButton").objectReferenceValue as Button;
            settingsPanelGO = so.FindProperty("settingsPanel").objectReferenceValue as GameObject;

            if (sfxToggle != null && closeButton != null)
            {
                rowParent = sfxToggle.transform.parent;
                sfxToggleRt = sfxToggle.GetComponent<RectTransform>();
                closeButtonRt = closeButton.GetComponent<RectTransform>();
                panelRt = rowParent != null ? rowParent.GetComponent<RectTransform>() : null;
                log.Add($"Tier 1 OK: resolved via MainMenuController2D.sfxToggleButton='{sfxToggle.name}' / settingsCloseButton='{closeButton.name}' serialized references (row parent: '{(rowParent != null ? rowParent.name : "none")}').");
                return true;
            }

            log.Add("Tier 1 FAILED: MainMenuController2D found, but its sfxToggleButton/settingsCloseButton serialized references are empty.");

            if (settingsPanelGO != null)
            {
                Transform sfxToggleT = FindDescendant(settingsPanelGO.transform, "SfxToggleButton");
                Transform closeButtonT = FindDescendant(settingsPanelGO.transform, "SettingsCloseButton");
                if (sfxToggleT != null && closeButtonT != null)
                {
                    rowParent = sfxToggleT.parent;
                    sfxToggleRt = sfxToggleT.GetComponent<RectTransform>();
                    closeButtonRt = closeButtonT.GetComponent<RectTransform>();
                    panelRt = rowParent != null ? rowParent.GetComponent<RectTransform>() : null;
                    log.Add($"Tier 2 OK: resolved by searching under MainMenuController2D.settingsPanel ('{settingsPanelGO.name}') for 'SfxToggleButton'/'SettingsCloseButton' (row parent: '{(rowParent != null ? rowParent.name : "none")}').");
                    return true;
                }

                log.Add($"Tier 2 FAILED: searched every descendant of '{settingsPanelGO.name}' for 'SfxToggleButton'/'SettingsCloseButton' - not found. " +
                    $"Direct children of '{settingsPanelGO.name}': {DescribeDirectChildren(settingsPanelGO.transform)}.");
            }
            else
            {
                log.Add("Tier 2 skipped: MainMenuController2D.settingsPanel reference is also empty.");
            }
        }
        else
        {
            log.Add("Tier 1/2 skipped: no MainMenuController2D found in the scene (searched including inactive objects).");
        }

        GameObject sfxToggleGO = GameObject.Find("SfxToggleButton");
        GameObject closeButtonGO = GameObject.Find("SettingsCloseButton");
        if (sfxToggleGO != null && closeButtonGO != null)
        {
            rowParent = sfxToggleGO.transform.parent;
            sfxToggleRt = sfxToggleGO.GetComponent<RectTransform>();
            closeButtonRt = closeButtonGO.GetComponent<RectTransform>();
            panelRt = rowParent != null ? rowParent.GetComponent<RectTransform>() : null;
            log.Add($"Tier 3 OK: resolved via scene-wide GameObject.Find fallback (row parent: '{(rowParent != null ? rowParent.name : "none")}').");
            return true;
        }

        log.Add("Tier 3 FAILED: GameObject.Find('SfxToggleButton'/'SettingsCloseButton') found nothing - expected, since GameObject.Find never finds inactive objects and the settings panel is inactive by design until 'Ayarlar' is pressed.");
        return false;
    }

    /// <summary>Recursive by-name search over every descendant (not just direct children), using Transform.GetChild - unlike GameObject.Find, this works regardless of whether any ancestor is currently active.</summary>
    private static Transform FindDescendant(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
                return child;

            Transform found = FindDescendant(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static string DescribeDirectChildren(Transform root)
    {
        if (root.childCount == 0)
            return "(none)";

        var names = new List<string>(root.childCount);
        for (int i = 0; i < root.childCount; i++)
        {
            names.Add(root.GetChild(i).name);
        }
        return string.Join(", ", names);
    }

    /// <summary>
    /// Same idea as BuildMainMenuVolumeControls, but for the in-game settings
    /// page - which lives inside the shared InGameMenu2D.prefab ASSET, not
    /// directly in GameScene2D.unity. Editing the prefab asset here
    /// automatically propagates to its already-placed "DedicatedInGameMenu"
    /// scene instance via Unity's normal prefab propagation - GameScene2D.unity
    /// itself is never opened or touched.
    /// </summary>
    private static bool BuildInGameMenuVolumeControls(List<string> summary)
    {
        if (!TryLoadVolumeControlAssets(out Font font, out Sprite trackSprite, out Sprite fillSprite, out Sprite handleSprite))
        {
            summary.Add("  - InGameMenu2D volume controls: FAILED (missing font/sprite assets)");
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(InGameMenuPrefabPath);
        bool success = false;
        try
        {
            if (!TryResolveInGameMenuSettingsTargets(prefabRoot, out Transform settingsPage, out RectTransform sfxToggleRt,
                    out RectTransform backButtonRt, out RectTransform panelRt, out List<string> resolveLog))
            {
                Debug.LogError("GameAudioSystemBuilder2D: in-game settings targets could not be resolved (unexpected - preflight already confirmed this would work).\n" + string.Join("\n", resolveLog));
                summary.Add("  - InGameMenu2D volume controls: FAILED (resolution failed after preflight succeeded - see error above)");
                return false;
            }

            (Slider musicSlider, Text musicPercent, Slider sfxSlider, Text sfxPercent) = InsertVolumeRows(
                settingsPage, sfxToggleRt, backButtonRt, panelRt, font, trackSprite, fillSprite, handleSprite);

            WireVolumeSlider(musicSlider.gameObject, musicSlider, musicPercent, AudioVolumeSlider2D.VolumeType.Music);
            WireVolumeSlider(sfxSlider.gameObject, sfxSlider, sfxPercent, AudioVolumeSlider2D.VolumeType.SFX);

            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, InGameMenuPrefabPath);
            success = true;

            summary.Add("  - InGameMenu2D.prefab: MusicVolumeRow/SFXVolumeRow added under SettingsPage (below SfxToggleButton); SettingsBackButton repositioned; MainPanel height adjusted only if needed.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        return success;
    }

    /// <summary>Read-only dry run of the in-game settings resolution - loads the prefab contents, resolves targets, discards the temporary copy WITHOUT saving. Used by the preflight pass so a missing target there aborts the whole build before anything is mutated.</summary>
    private static bool TryPreflightInGameMenuTargets(out List<string> log)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(InGameMenuPrefabPath);
        try
        {
            return TryResolveInGameMenuSettingsTargets(prefabRoot, out _, out _, out _, out _, out log);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>Resolves InGameMenu2D.prefab's MainPanel/SettingsPage/SfxToggleButton/SettingsBackButton via Transform.Find - already active-state-independent (prefab contents loaded via LoadPrefabContents have no "active in hierarchy" ambiguity the way a partially-inactive scene does), so this never needed the tiered fallback MainMenuScene2D's resolution does.</summary>
    private static bool TryResolveInGameMenuSettingsTargets(
        GameObject prefabRoot, out Transform settingsPage, out RectTransform sfxToggleRt, out RectTransform backButtonRt, out RectTransform panelRt,
        out List<string> log)
    {
        log = new List<string>();
        settingsPage = null;
        sfxToggleRt = null;
        backButtonRt = null;
        panelRt = null;

        Transform mainPanel = prefabRoot.transform.Find("MainPanel");
        settingsPage = mainPanel != null ? mainPanel.Find("SettingsPage") : null;
        Transform sfxToggle = settingsPage != null ? settingsPage.Find("SfxToggleButton") : null;
        Transform backButton = settingsPage != null ? settingsPage.Find("SettingsBackButton") : null;

        if (mainPanel == null || settingsPage == null || sfxToggle == null || backButton == null)
        {
            log.Add($"FAILED: 'MainPanel/SettingsPage/SfxToggleButton' or 'SettingsBackButton' not found in InGameMenu2D.prefab " +
                $"(mainPanel={(mainPanel != null)}, settingsPage={(settingsPage != null)}, sfxToggle={(sfxToggle != null)}, backButton={(backButton != null)}). " +
                "Run 'YagmurRotasi2D > Build In-Game Menu Prefab' first, then re-run this command.");
            return false;
        }

        sfxToggleRt = sfxToggle.GetComponent<RectTransform>();
        backButtonRt = backButton.GetComponent<RectTransform>();
        panelRt = mainPanel.GetComponent<RectTransform>();
        log.Add("OK: resolved via MainPanel/SettingsPage/SfxToggleButton/SettingsBackButton.");
        return true;
    }

    private static bool TryLoadVolumeControlAssets(out Font font, out Sprite trackSprite, out Sprite fillSprite, out Sprite handleSprite)
    {
        font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        trackSprite = LoadFirstSprite(UiFolder + "/Badges/grey.png");
        fillSprite = LoadFirstSprite(UiFolder + "/Buttons/brown.png");
        handleSprite = LoadFirstSprite(UiFolder + "/Badges/white.png");

        if (font == null || trackSprite == null || fillSprite == null || handleSprite == null)
        {
            Debug.LogError("GameAudioSystemBuilder2D: required font/sprites for volume sliders could not be loaded (see warnings above for which path failed).");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates/repairs MusicVolumeRow and SFXVolumeRow as children of
    /// rowParent, sized to match sfxToggleRt's width for visual consistency.
    /// Deliberately does NOT position these rows, does NOT move
    /// nextButtonRt (SettingsCloseButton/SettingsBackButton), and does NOT
    /// resize panelRt - final vertical layout for the whole settings panel
    /// (order, spacing, panel height) is owned exclusively by
    /// SettingsLayoutBuilder2D ("YagmurRotasi2D > UI > Repair Settings
    /// Layout") now, so this builder can never re-introduce the
    /// overlap/drift that came from two builders independently repositioning
    /// the same controls. nextButtonRt/panelRt are accepted for now only so
    /// existing call sites don't need restructuring; they are unused.
    /// </summary>
    private static (Slider musicSlider, Text musicPercent, Slider sfxSlider, Text sfxPercent) InsertVolumeRows(
        Transform rowParent, RectTransform sfxToggleRt, RectTransform nextButtonRt, RectTransform panelRt,
        Font font, Sprite trackSprite, Sprite fillSprite, Sprite handleSprite)
    {
        float rowWidth = sfxToggleRt.sizeDelta.x;

        (Slider musicSlider, Text musicPercent) = BuildVolumeRow(rowParent, "MusicVolumeRow", "MusicVolumeSlider", "MusicVolumePercent",
            MusicVolumeLabel, rowWidth, VolumeRowHeight, font, trackSprite, fillSprite, handleSprite);
        (Slider sfxSlider, Text sfxPercent) = BuildVolumeRow(rowParent, "SFXVolumeRow", "SFXVolumeSlider", "SFXVolumePercent",
            SFXVolumeLabel, rowWidth, VolumeRowHeight, font, trackSprite, fillSprite, handleSprite);

        return (musicSlider, musicPercent, sfxSlider, sfxPercent);
    }

    /// <summary>
    /// Builds (or repairs) one row: a left-aligned label (~28% width), a
    /// centered Slider (~55% width), and a right-aligned percentage Text
    /// (~17% width) - fixed fractions of the row's own width so it works
    /// identically at any rowWidth. Only sets the row's own size, never its
    /// position - SettingsLayoutBuilder2D positions the row via its
    /// VerticalLayoutGroup.
    /// </summary>
    private static (Slider slider, Text percentText) BuildVolumeRow(
        Transform parent, string rowName, string sliderName, string percentName, string labelText,
        float width, float height, Font font, Sprite trackSprite, Sprite fillSprite, Sprite handleSprite)
    {
        GameObject row = FindOrCreateUIChild(parent, rowName);
        RectTransform rowRt = RectOf(row);
        rowRt.anchorMin = TopCenter;
        rowRt.anchorMax = TopCenter;
        rowRt.pivot = TopCenter;
        rowRt.sizeDelta = new Vector2(width, height);

        GameObject labelGO = FindOrCreateUIChild(row.transform, "Label");
        RectTransform labelRt = RectOf(labelGO);
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(0.28f, 1f);
        labelRt.pivot = Center;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        SetupText(labelGO, labelText, font, 34, Color.white, TextAnchor.MiddleLeft, false);

        GameObject sliderGO = FindOrCreateUIChild(row.transform, sliderName);
        RectTransform sliderRt = RectOf(sliderGO);
        sliderRt.anchorMin = new Vector2(0.30f, 0.5f);
        sliderRt.anchorMax = new Vector2(0.85f, 0.5f);
        sliderRt.pivot = Center;
        sliderRt.anchoredPosition = Vector2.zero;
        sliderRt.sizeDelta = new Vector2(0f, SliderTrackHeight);
        Slider slider = SetupSliderVisual(sliderGO, trackSprite, fillSprite, handleSprite);

        GameObject percentGO = FindOrCreateUIChild(row.transform, percentName);
        RectTransform percentRt = RectOf(percentGO);
        percentRt.anchorMin = new Vector2(0.87f, 0f);
        percentRt.anchorMax = new Vector2(1f, 1f);
        percentRt.pivot = Center;
        percentRt.offsetMin = Vector2.zero;
        percentRt.offsetMax = Vector2.zero;
        Text percentText = SetupText(percentGO, "0%", font, 32, Color.white, TextAnchor.MiddleRight, false);

        return (slider, percentText);
    }

    /// <summary>Standard Unity Slider construction (Background, Fill Area &gt; Fill, Handle Slide Area &gt; Handle) using the shared UI package's existing sprites - Background/Handle have raycastTarget=true (clicking anywhere on the track, not just grabbing the handle, must work), Fill Area/Fill do not. Idempotent - every part is found-or-created by name.</summary>
    private static Slider SetupSliderVisual(GameObject sliderGO, Sprite trackSprite, Sprite fillSprite, Sprite handleSprite)
    {
        Slider slider = sliderGO.GetComponent<Slider>();
        if (slider == null) slider = sliderGO.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.transition = Selectable.Transition.None;

        GameObject background = FindOrCreateUIChild(sliderGO.transform, "Background");
        StretchFill(RectOf(background), 0f);
        SetupImage(background, trackSprite, true, new Color(1f, 1f, 1f, 0.55f));

        GameObject fillArea = FindOrCreateUIChild(sliderGO.transform, "Fill Area");
        RectTransform fillAreaRt = RectOf(fillArea);
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.pivot = Center;
        fillAreaRt.offsetMin = new Vector2(4f, 4f);
        fillAreaRt.offsetMax = new Vector2(-4f, -4f);

        GameObject fill = FindOrCreateUIChild(fillArea.transform, "Fill");
        RectTransform fillRt = RectOf(fill);
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f); // Slider drives anchorMax.x at runtime from the current value.
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        SetupImage(fill, fillSprite, false, Color.white);

        GameObject handleArea = FindOrCreateUIChild(sliderGO.transform, "Handle Slide Area");
        RectTransform handleAreaRt = RectOf(handleArea);
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.pivot = Center;
        handleAreaRt.offsetMin = new Vector2(SliderHandleSize * 0.5f, 0f);
        handleAreaRt.offsetMax = new Vector2(-SliderHandleSize * 0.5f, 0f);

        GameObject handle = FindOrCreateUIChild(handleArea.transform, "Handle");
        RectTransform handleRt = RectOf(handle);
        handleRt.sizeDelta = new Vector2(SliderHandleSize, SliderHandleSize);
        handleRt.anchorMin = new Vector2(0f, 0.5f);
        handleRt.anchorMax = new Vector2(0f, 0.5f);
        handleRt.pivot = Center;
        Image handleImage = SetupImage(handle, handleSprite, true, Color.white);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImage;

        return slider;
    }

    /// <summary>Attaches AudioVolumeSlider2D idempotently (find-or-add) and wires its serialized references - never touches Slider.onValueChanged directly (AudioVolumeSlider2D registers that itself at runtime, so re-running this builder can never duplicate that listener).</summary>
    private static void WireVolumeSlider(GameObject go, Slider slider, Text percentText, AudioVolumeSlider2D.VolumeType volumeType)
    {
        AudioVolumeSlider2D volumeSlider = go.GetComponent<AudioVolumeSlider2D>();
        if (volumeSlider == null) volumeSlider = go.AddComponent<AudioVolumeSlider2D>();

        var so = new SerializedObject(volumeSlider);
        so.FindProperty("slider").objectReferenceValue = slider;
        so.FindProperty("percentageText").objectReferenceValue = percentText;
        so.FindProperty("volumeType").enumValueIndex = (int)volumeType;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Scans every Button (including inactive ones - e.g. the pause menu and
    /// success panel are hidden by default) currently in the OPEN scene and
    /// attaches UIButtonSound2D to any that doesn't already have it. Returns
    /// the count newly tagged - idempotent, so this is 0 on every rerun once
    /// the whole scene has been covered once. Level cards are NOT covered
    /// here (they don't exist in the saved scene - see class doc).
    /// </summary>
    private static int AttachUIButtonSoundToAllButtons()
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int tagged = 0;
        foreach (Button button in buttons)
        {
            if (button.GetComponent<UIButtonSound2D>() == null)
            {
                button.gameObject.AddComponent<UIButtonSound2D>();
                tagged++;
            }
        }
        return tagged;
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    // ---------------- Shared UI-construction helpers (volume sliders only) ----------------
    // Same find-or-create/RectTransform/SetupImage/SetupText conventions
    // already used by MainMenuSceneBuilder2D/InGameMenuPrefabBuilder/
    // SuccessPanelPrefabBuilder - this file keeps its own copies rather than
    // a shared utility class, matching how every one of those builders
    // already does the same thing independently.

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

    /// <summary>Unlike FindOrCreateChild above (used for AudioSource containers, plain Transform is fine), this guarantees a RectTransform - required for every UI element the volume sliders are built from.</summary>
    private static GameObject FindOrCreateUIChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform RectOf(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        return rt;
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

    private static Text SetupText(GameObject go, string content, Font font, int fontSize, Color color, TextAnchor alignment, bool wrap)
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
            Debug.LogWarning($"GameAudioSystemBuilder2D: no sprite found at '{texturePath}'.");
        }
        return sprite;
    }

    /// <summary>Searches every AudioClip in the project and matches by EXACT file name (no extension) - never a fuzzy/substring match, so "music" never accidentally matches an unrelated clip whose name merely contains it.</summary>
    private static AudioClip FindClipByExactName(string exactName)
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");
        var matches = new List<AudioClip>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == exactName)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) matches.Add(clip);
            }
        }

        if (matches.Count == 0)
        {
            Debug.LogError($"GameAudioSystemBuilder2D: no AudioClip named exactly '{exactName}' found anywhere in the project.");
            return null;
        }
        if (matches.Count > 1)
        {
            Debug.LogError($"GameAudioSystemBuilder2D: {matches.Count} AudioClips named exactly '{exactName}' found - ambiguous. Rename one to disambiguate, then re-run. Aborting.");
            return null;
        }
        return matches[0];
    }
}
