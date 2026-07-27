using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YagmurRotasi2D.Audio2D;
using YagmurRotasi2D.UI2D;

/// <summary>
/// A narrow, non-destructive repair command for the "Tam Ekran" buttons -
/// the safe alternative to rerunning MainMenuSceneBuilder2D/
/// GameSceneWebLayoutBuilder2D just to fix fullscreen wiring (a full builder
/// rerun previously wiped LevelSelectScene2D's background because one line
/// in that builder unconditionally reset it - see LevelSelectSceneBuilder2D's
/// background block, now fixed at the source instead).
///
/// Scope is deliberately MainMenuScene2D and GameScene2D only:
/// - MainMenuScene2D's FullscreenButton needs Button + UIButtonSound2D +
///   WebFullscreenButtonForwarder2D (the same runtime self-wiring pattern
///   GameSceneBackButtonForwarder2D established - an Editor-time
///   Button.onClick.AddListener() is never persisted into the saved scene).
/// - GameScene2D already wires its fullscreen button through a DIFFERENT,
///   already-correct mechanism (UIManager2D.Awake() registers the listener
///   at runtime) - this command only verifies that wiring isn't null and
///   repairs the reference if it genuinely is; it must NEVER add
///   WebFullscreenButtonForwarder2D there, since that would register a
///   second onClick listener alongside UIManager2D's and double-fire.
/// - LevelSelectScene2D is intentionally NOT touched here - its fullscreen
///   wiring is repaired by the hardened "Build Phase 9A Level Select Scene"
///   command itself (LevelSelectSceneBuilder2D), so there is exactly one
///   authoritative command for that scene, not two.
///
/// Never rebuilds layout, never touches backgrounds/panels/anchors, saves a
/// scene only if something was actually missing and got repaired.
/// </summary>
public static class WebFullscreenRepairBuilder2D
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene2D.unity";
    private const string GameScenePath = "Assets/Scenes/GameScene2D.unity";

    [MenuItem("YagmurRotasi2D/Web/Repair Fullscreen Buttons")]
    public static void RepairFullscreenButtons()
    {
        Scene originalActiveScene = EditorSceneManager.GetActiveScene();

        bool mainMenuOk = RepairMainMenu();
        bool gameSceneOk = RepairGameScene();

        // Restore whatever the user had open before this command ran, so it
        // never leaves the Editor sitting on a different scene than before.
        if (!string.IsNullOrEmpty(originalActiveScene.path) && originalActiveScene.path != EditorSceneManager.GetActiveScene().path)
        {
            EditorSceneManager.OpenScene(originalActiveScene.path, OpenSceneMode.Single);
        }

        Debug.Log($"WebFullscreenRepairBuilder2D: done. MainMenuScene2D: {(mainMenuOk ? "OK" : "FAILED - see errors above")}, " +
            $"GameScene2D: {(gameSceneOk ? "OK" : "FAILED - see errors above")}. LevelSelectScene2D was not touched - use " +
            "'YagmurRotasi2D > Build Phase 9A Level Select Scene' for that scene's fullscreen wiring.");
    }

    private static bool RepairMainMenu()
    {
        Scene scene = OpenScene(MainMenuScenePath);
        if (!scene.IsValid())
        {
            Debug.LogError($"WebFullscreenRepairBuilder2D: could not open '{MainMenuScenePath}'.");
            return false;
        }

        WebFullscreenController2D controller = Object.FindFirstObjectByType<WebFullscreenController2D>();
        if (controller == null)
        {
            Debug.LogError("WebFullscreenRepairBuilder2D: MainMenuScene2D has no WebFullscreenController2D in the scene - cannot repair.");
            return false;
        }

        GameObject buttonGO = FindInactiveAware(scene, "FullscreenButton");
        if (buttonGO == null)
        {
            Debug.LogError("WebFullscreenRepairBuilder2D: MainMenuScene2D has no 'FullscreenButton' object - cannot repair.");
            return false;
        }

        bool changed = false;

        Button button = buttonGO.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("WebFullscreenRepairBuilder2D: MainMenuScene2D's FullscreenButton has no Button component - cannot repair.");
            return false;
        }

        if (buttonGO.GetComponent<UIButtonSound2D>() == null)
        {
            buttonGO.AddComponent<UIButtonSound2D>();
            changed = true;
        }

        WebFullscreenButtonForwarder2D forwarder = buttonGO.GetComponent<WebFullscreenButtonForwarder2D>();
        if (forwarder == null)
        {
            forwarder = buttonGO.AddComponent<WebFullscreenButtonForwarder2D>();
            changed = true;
        }

        SerializedObject forwarderSO = new SerializedObject(forwarder);
        SerializedProperty controllerProp = forwarderSO.FindProperty("fullscreenController");
        if (controllerProp.objectReferenceValue != controller)
        {
            controllerProp.objectReferenceValue = controller;
            forwarderSO.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            Debug.Log("WebFullscreenRepairBuilder2D: MainMenuScene2D FullscreenButton repaired.");
        }
        else
        {
            Debug.Log("WebFullscreenRepairBuilder2D: MainMenuScene2D FullscreenButton already correct - no changes.");
        }

        return true;
    }

    private static bool RepairGameScene()
    {
        Scene scene = OpenScene(GameScenePath);
        if (!scene.IsValid())
        {
            Debug.LogError($"WebFullscreenRepairBuilder2D: could not open '{GameScenePath}'.");
            return false;
        }

        UIManager2D uiManager = Object.FindFirstObjectByType<UIManager2D>();
        if (uiManager == null)
        {
            Debug.LogError("WebFullscreenRepairBuilder2D: GameScene2D has no UIManager2D - cannot verify fullscreen wiring.");
            return false;
        }

        WebFullscreenController2D controller = Object.FindFirstObjectByType<WebFullscreenController2D>();
        if (controller == null)
        {
            Debug.LogError("WebFullscreenRepairBuilder2D: GameScene2D has no WebFullscreenController2D - cannot repair.");
            return false;
        }

        SerializedObject uiSO = new SerializedObject(uiManager);
        SerializedProperty fullscreenButtonProp = uiSO.FindProperty("fullscreenButton");
        SerializedProperty fullscreenControllerProp = uiSO.FindProperty("fullscreenController");

        bool changed = false;

        if (fullscreenControllerProp.objectReferenceValue == null)
        {
            fullscreenControllerProp.objectReferenceValue = controller;
            changed = true;
        }

        if (fullscreenButtonProp.objectReferenceValue == null)
        {
            GameObject buttonGO = FindInactiveAware(scene, "FullscreenButton");
            if (buttonGO == null)
            {
                Debug.LogError("WebFullscreenRepairBuilder2D: GameScene2D's UIManager2D.fullscreenButton is missing and no 'FullscreenButton' object was found - cannot repair.");
                return false;
            }

            Button button = buttonGO.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("WebFullscreenRepairBuilder2D: GameScene2D's FullscreenButton has no Button component - cannot repair.");
                return false;
            }

            fullscreenButtonProp.objectReferenceValue = button;
            changed = true;
        }

        if (changed)
        {
            uiSO.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log("WebFullscreenRepairBuilder2D: GameScene2D fullscreen wiring repaired.");
        }
        else
        {
            Debug.Log("WebFullscreenRepairBuilder2D: GameScene2D fullscreen wiring already correct - no changes.");
        }

        return true;
    }

    private static Scene OpenScene(string path)
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (active.path == path) return active;
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    /// <summary>
    /// GameObject.Find only matches objects active in the hierarchy - a
    /// recurring pitfall in this project's tooling. Searches every root
    /// transform's full descendant tree, inactive included, by exact name.
    /// </summary>
    private static GameObject FindInactiveAware(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static Transform FindRecursive(Transform current, string name)
    {
        if (current.name == name) return current;
        for (int i = 0; i < current.childCount; i++)
        {
            Transform result = FindRecursive(current.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
