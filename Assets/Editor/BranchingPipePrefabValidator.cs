using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Gameplay2D;
using YagmurRotasi2D.Visual2D;

/// <summary>
/// Read-only inspection of PipeTee2D.prefab/PipeCross2D.prefab. Never modifies
/// the prefab assets or any scene - loads each via PrefabUtility.LoadPrefabContents
/// purely to inspect it, then unloads without saving.
/// </summary>
public static class BranchingPipePrefabValidator
{
    [MenuItem("YagmurRotasi2D/Validate Branching Pipe Prefabs")]
    public static void ValidateMenuCommand()
    {
        bool teeOk = ValidatePrefab(BranchingPipeAssetBinder.TeePrefabPath, PipeType2D.Tee, expectedRotatable: true);
        bool crossOk = ValidatePrefab(BranchingPipeAssetBinder.CrossPrefabPath, PipeType2D.Cross, expectedRotatable: false);

        Debug.Log($"BranchingPipePrefabValidator: PipeTee2D.prefab valid={teeOk}, PipeCross2D.prefab valid={crossOk}.");
    }

    private static bool ValidatePrefab(string prefabPath, PipeType2D expectedType, bool expectedRotatable)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset == null)
        {
            Debug.LogWarning($"BranchingPipePrefabValidator: '{prefabPath}' does not exist. Run " +
                "'YagmurRotasi2D > Bind T and Cross Pipe Assets' first.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool ok = true;
        try
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine($"BranchingPipePrefabValidator: inspecting '{prefabPath}'");

            BoxCollider2D[] colliders = root.GetComponentsInChildren<BoxCollider2D>(true);
            log.AppendLine($"  Collider count (root): {colliders.Length} (expected 1)");
            ok &= colliders.Length == 1;

            PipeTile2D pipeTile = root.GetComponent<PipeTile2D>();
            if (pipeTile == null)
            {
                log.AppendLine("  PipeTile2D: MISSING");
                ok = false;
            }
            else
            {
                log.AppendLine($"  PipeType: {pipeTile.PipeType} (expected {expectedType})");
                ok &= pipeTile.PipeType == expectedType;

                log.AppendLine($"  RotationIndex: {pipeTile.RotationIndex} (expected 0)");
                ok &= pipeTile.RotationIndex == 0;

                log.AppendLine($"  IsRotatable: {pipeTile.IsRotatable} (expected {expectedRotatable})");
                ok &= pipeTile.IsRotatable == expectedRotatable;

                log.AppendLine($"  Open directions at rotationIndex 0: {string.Join(", ", pipeTile.GetOpenDirections())}");
            }

            Transform baseVisual = root.transform.Find("BaseVisual");
            Transform waterOverlay = root.transform.Find("WaterOverlay");

            SpriteRenderer[] allRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            log.AppendLine($"  SpriteRenderer count (total): {allRenderers.Length} (expected 2: BaseVisual + WaterOverlay)");
            ok &= allRenderers.Length == 2;

            // Single source of truth for the expected offset - never a second
            // hardcoded 90/180 here, always read from the same helper the
            // binder itself uses to write the prefab.
            float expectedOffsetZ = BranchingPipeAssetBinder.GetVisualRotationOffset(expectedType);

            if (baseVisual == null)
            {
                log.AppendLine("  BaseVisual: MISSING");
                ok = false;
            }
            else
            {
                SpriteRenderer baseRenderer = baseVisual.GetComponent<SpriteRenderer>();
                bool baseRotationOk = Mathf.Abs(Mathf.DeltaAngle(baseVisual.localEulerAngles.z, expectedOffsetZ)) < 0.5f;
                log.AppendLine($"  BaseVisual sprite: {(baseRenderer != null && baseRenderer.sprite != null ? baseRenderer.sprite.name : "MISSING")}");
                log.AppendLine($"  BaseVisual sortingOrder: {(baseRenderer != null ? baseRenderer.sortingOrder.ToString() : "n/a")} (expected 1)");
                log.AppendLine($"  BaseVisual Z: {baseVisual.localEulerAngles.z:0.##} (expected {expectedOffsetZ:0.##}) - {(baseRotationOk ? "PASS" : "FAIL")}");
                ok &= baseRenderer != null && baseRenderer.sprite != null && baseRenderer.sortingOrder == 1 && baseRotationOk;
            }

            if (waterOverlay == null)
            {
                log.AppendLine("  WaterOverlay: MISSING");
                ok = false;
            }
            else
            {
                SpriteRenderer overlayRenderer = waterOverlay.GetComponent<SpriteRenderer>();
                SpriteFrameAnimator2D animator = waterOverlay.GetComponent<SpriteFrameAnimator2D>();
                PipeWaterVisual2D waterVisual = waterOverlay.GetComponent<PipeWaterVisual2D>();
                bool overlayRotationOk = Mathf.Abs(Mathf.DeltaAngle(waterOverlay.localEulerAngles.z, expectedOffsetZ)) < 0.5f;
                bool baseOverlayMatch = baseVisual == null
                    || Mathf.Abs(Mathf.DeltaAngle(baseVisual.localEulerAngles.z, waterOverlay.localEulerAngles.z)) < 0.5f;

                log.AppendLine($"  WaterOverlay sortingOrder: {(overlayRenderer != null ? overlayRenderer.sortingOrder.ToString() : "n/a")} (expected 2)");
                log.AppendLine($"  WaterOverlay active by default: {waterOverlay.gameObject.activeSelf} (expected False)");
                log.AppendLine($"  WaterOverlay Z: {waterOverlay.localEulerAngles.z:0.##} (expected {expectedOffsetZ:0.##}) - {(overlayRotationOk ? "PASS" : "FAIL")}");
                log.AppendLine($"  BaseVisual/WaterOverlay Z match each other: {(baseOverlayMatch ? "PASS" : "FAIL")}");
                log.AppendLine($"  SpriteFrameAnimator2D frame count: {(animator != null ? animator.FrameCount.ToString() : "MISSING")} (expected 4)");
                log.AppendLine($"  PipeWaterVisual2D present: {waterVisual != null}");

                ok &= overlayRenderer != null && overlayRenderer.sortingOrder == 2;
                ok &= !waterOverlay.gameObject.activeSelf;
                ok &= animator != null && animator.FrameCount == 4;
                ok &= waterVisual != null;
                ok &= overlayRotationOk && baseOverlayMatch;
            }

            log.AppendLine($"  {expectedType}2D visual alignment: {(ok ? "PASS" : "FAIL")}");
            log.AppendLine($"  Expected {expectedType} visual offset: {expectedOffsetZ:0.##}");
            log.AppendLine($"  Result: {(ok ? "VALID" : "INVALID - see warnings above")}");
            Debug.Log(log.ToString());
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return ok;
    }
}
