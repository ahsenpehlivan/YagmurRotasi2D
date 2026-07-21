using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YagmurRotasi2D.Visual2D;

/// <summary>
/// Repairs and rebinds the 8 flower success animations. Root cause found while
/// implementing Phase 7E.3: SuccessFXController2D.flowerAnimators in the saved
/// GameScene2D had all 8 slots pointing at {fileID: 0} (null) - most likely from a
/// prior manual scene edit that recreated/reparented the flower components without
/// the array following along. With that array empty, PrepareInitialState/
/// PlaySuccessFX/ResetFX all silently skipped every flower (their "if (animator ==
/// null) continue;" guards did exactly what they were designed to do - skip a
/// missing reference - which is correct behavior for a genuinely-missing flower,
/// but meant NONE of the 8 were ever reset or played). Each flower's SpriteRenderer
/// was consequently left showing whatever sprite a previous Play session had
/// reached (frame 4, fully bloomed) with nothing left to ever move it back to
/// frame 0 or animate it again.
///
/// This tool fixes the root cause (rediscovers Flower_0..Flower_7 by name under
/// FlowerFXRoot and rewrites the array from that discovery, so it no longer
/// depends on possibly-stale serialized references) and repairs the visible
/// symptom (rebinds each flower's unique sheet and forces its SpriteRenderer back
/// to frame 0). Never touches Transform, ducks, UI, markers, clouds, background or
/// grid.
/// </summary>
public static class FlowerAnimationRepair
{
    [MenuItem("YagmurRotasi2D/Repair and Rebind Flower Animations")]
    public static void RepairMenuCommand()
    {
        TryRepairFlowers(true);
    }

    public static bool TryRepairFlowers(bool logDetails)
    {
        SuccessFXController2D controller = Object.FindFirstObjectByType<SuccessFXController2D>();
        if (controller == null)
        {
            Debug.LogWarning("FlowerAnimationRepair: no SuccessFXController2D found in the active scene. " +
                "Run 'YagmurRotasi2D > Install Phase 7E1 All Flowers Preserve Clouds' first.");
            return false;
        }

        var controllerSO = new SerializedObject(controller);
        SerializedProperty flowerRootProp = controllerSO.FindProperty("flowerRoot");
        GameObject flowerFXRoot = flowerRootProp.objectReferenceValue as GameObject;
        if (flowerFXRoot == null)
        {
            Debug.LogWarning("FlowerAnimationRepair: SuccessFXController2D.flowerRoot is not assigned - cannot discover Flower_N children. Aborting.");
            return false;
        }

        // Rediscover Flower_0..Flower_7 by NAME rather than trusting the existing
        // (possibly broken) flowerAnimators array. This is the actual fix for the
        // root cause: the array is rebuilt fresh from what's really in the
        // hierarchy right now, so a future manual edit that breaks the array again
        // can always be repaired the same way.
        var discovered = new List<SpriteFrameAnimator2D>();
        var missing = new List<string>();
        int index = 0;
        while (true)
        {
            Transform child = flowerFXRoot.transform.Find($"Flower_{index}");
            if (child == null)
            {
                // Stop at the first gap - Flower_0..Flower_7 are expected to be contiguous.
                break;
            }

            var animator = child.GetComponent<SpriteFrameAnimator2D>();
            if (animator == null)
            {
                missing.Add($"Flower_{index} (no SpriteFrameAnimator2D component)");
            }
            else
            {
                discovered.Add(animator);
            }

            index++;
        }

        if (discovered.Count == 0)
        {
            Debug.LogWarning($"FlowerAnimationRepair: found no Flower_N children with a SpriteFrameAnimator2D under " +
                $"'{flowerFXRoot.name}'. Aborting - nothing to repair.");
            return false;
        }

        if (logDetails)
        {
            Debug.Log($"FlowerAnimationRepair: rediscovered {discovered.Count} flower animator(s) by name under " +
                $"'{flowerFXRoot.name}' (previous flowerAnimators array size was {controllerSO.FindProperty("flowerAnimators").arraySize}).");
            if (missing.Count > 0)
            {
                Debug.LogWarning("FlowerAnimationRepair: missing components:\n" + string.Join("\n", missing));
            }
        }

        // Rewrite the array from the fresh discovery.
        SerializedProperty flowerAnimatorsProp = controllerSO.FindProperty("flowerAnimators");
        flowerAnimatorsProp.arraySize = discovered.Count;
        for (int i = 0; i < discovered.Count; i++)
        {
            flowerAnimatorsProp.GetArrayElementAtIndex(i).objectReferenceValue = discovered[i];
        }
        controllerSO.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        // Re-bind each flower's unique sheet (1-to-1 by index) using the same logic
        // as the general binder - but flowers only, never touching duckAnimators.
        bool bound = SuccessFXSpriteSheetBinder.BindFlowersOnly(controller, logDetails);

        // Force every rediscovered flower back to frame 0 right now, directly -
        // this is the actual visible repair, independent of Awake()/Play Mode ever
        // running again. Ducks are never referenced here.
        var perFlowerLog = new List<string>();
        foreach (SpriteFrameAnimator2D animator in discovered)
        {
            if (animator == null) continue;

            animator.Stop();
            animator.ResetToFirstFrame();
            EditorUtility.SetDirty(animator);

            var animatorSO = new SerializedObject(animator);
            int frameCount = animatorSO.FindProperty("frames").arraySize;
            string firstFrameName = frameCount > 0
                ? (animatorSO.FindProperty("frames").GetArrayElementAtIndex(0).objectReferenceValue as Sprite)?.name
                : "(none)";
            float fps = animatorSO.FindProperty("framesPerSecond").floatValue;
            bool loop = animatorSO.FindProperty("loop").boolValue;
            bool playOnEnable = animatorSO.FindProperty("playOnEnable").boolValue;
            bool holdLast = animatorSO.FindProperty("holdLastFrameOnComplete").boolValue;

            perFlowerLog.Add($"  {animator.name}: frames={frameCount}, initialSprite={firstFrameName}, " +
                $"fps={fps}, loop={loop}, playOnEnable={playOnEnable}, holdLast={holdLast}");
        }

        if (logDetails)
        {
            Debug.Log("FlowerAnimationRepair: per-flower state after repair:\n" + string.Join("\n", perFlowerLog));
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        return bound;
    }
}
