using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Editor-only report mapping each Tee rotationIndex to its logical open
/// directions (real production PipeTile2D.GetOpenDirections(), never a
/// re-typed copy) alongside PipeTee2D.prefab's current BaseVisual/
/// WaterOverlay local rotation. Inspects the prefab via
/// PrefabUtility.LoadPrefabContents and never saves - both the scene and the
/// prefab asset are left completely untouched. This can only confirm the
/// logical direction tables and the stored rotation VALUE are consistent
/// with what the code expects - it cannot see rendered pixels, so a genuine
/// visual confirmation still requires actually looking at the sprite in
/// Play Mode.
/// </summary>
public static class TeeVisualOrientationValidator
{
    [MenuItem("YagmurRotasi2D/Validate Tee Visual Orientation")]
    public static void ValidateMenuCommand()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(BranchingPipeAssetBinder.TeePrefabPath);
        if (asset == null)
        {
            Debug.LogWarning($"TeeVisualOrientationValidator: '{BranchingPipeAssetBinder.TeePrefabPath}' does not exist. Run " +
                "'YagmurRotasi2D > Bind T and Cross Pipe Assets' first.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(BranchingPipeAssetBinder.TeePrefabPath);
        try
        {
            PipeTile2D pipeTile = root.GetComponent<PipeTile2D>();
            Transform baseVisual = root.transform.Find("BaseVisual");
            Transform waterOverlay = root.transform.Find("WaterOverlay");

            if (pipeTile == null || baseVisual == null || waterOverlay == null)
            {
                Debug.LogError("TeeVisualOrientationValidator: PipeTee2D.prefab is missing PipeTile2D/BaseVisual/WaterOverlay - cannot report.");
                return;
            }

            float expectedOffsetZ = BranchingPipeAssetBinder.GetVisualRotationOffset(PipeType2D.Tee);
            bool baseOk = Mathf.Abs(Mathf.DeltaAngle(baseVisual.localEulerAngles.z, expectedOffsetZ)) < 0.5f;
            bool overlayOk = Mathf.Abs(Mathf.DeltaAngle(waterOverlay.localEulerAngles.z, expectedOffsetZ)) < 0.5f;

            var log = new System.Text.StringBuilder();
            log.AppendLine("TeeVisualOrientationValidator: logical vs. stored-visual mapping for PipeTee2D.prefab.");
            log.AppendLine($"  BaseVisual local Z (fixed art-alignment offset, same for every rotationIndex): " +
                $"{baseVisual.localEulerAngles.z:0.##} (expected {expectedOffsetZ:0.##}) - {(baseOk ? "PASS" : "FAIL")}");
            log.AppendLine($"  WaterOverlay local Z (fixed art-alignment offset, same for every rotationIndex): " +
                $"{waterOverlay.localEulerAngles.z:0.##} (expected {expectedOffsetZ:0.##}) - {(overlayOk ? "PASS" : "FAIL")}");
            log.AppendLine();
            log.AppendLine("  Per-rotationIndex logical mapping (from the real PipeTile2D.GetOpenDirections()):");

            for (int r = 0; r < 4; r++)
            {
                pipeTile.Initialize(PipeType2D.Tee, r, Vector2Int.zero);
                Direction2D[] openings = pipeTile.GetOpenDirections();
                log.AppendLine($"    rotationIndex {r}: logical open = [{string.Join(", ", openings)}], " +
                    $"expected visually open = [{string.Join(", ", openings)}] " +
                    $"(root Z after ApplyRotation = {root.transform.localEulerAngles.z:0.##})");
            }

            log.AppendLine();
            log.AppendLine("  Reference (unchanged by this phase - Tee logical mapping):");
            log.AppendLine("    rotationIndex 0 -> Up + Left + Right, closed Down");
            log.AppendLine("    rotationIndex 1 -> Up + Right + Down, closed Left");
            log.AppendLine("    rotationIndex 2 -> Right + Down + Left, closed Up");
            log.AppendLine("    rotationIndex 3 -> Down + Left + Up, closed Right");
            log.AppendLine();
            log.AppendLine("  This report only confirms the logical direction tables and the stored BaseVisual/" +
                "WaterOverlay rotation VALUE - it cannot see rendered pixels. Final visual confirmation still requires " +
                "actually looking at the sprite in Play Mode (rotate a Tee through all four states and compare the " +
                "visible branches against the logical mapping above).");

            Debug.Log(log.ToString());
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
