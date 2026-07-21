using System.Text;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Gameplay2D;
using YagmurRotasi2D.Visual2D;

/// <summary>
/// Editor-only diagnostic (Assets/Editor - never compiled into a build):
/// instantiates a temporary, unsaved copy of each real pipe prefab, cycles it
/// through every rotationIndex/entry-side combination it can actually occur
/// in during gameplay, and logs the exact correction PipeFlowVisualProfile2D
/// would apply. Never modifies GameScene2D or saved progress - every
/// temporary object is destroyed immediately after use.
/// </summary>
public static class PipeFlowDirectionPreviewer
{
    [MenuItem("YagmurRotasi2D/Debug/Preview Pipe Flow Directions")]
    public static void PreviewMenuCommand()
    {
        var log = new StringBuilder();
        log.AppendLine("PipeFlowDirectionPreviewer: previewing every pipe type/rotation/entry combination.");

        PreviewStraight(log);
        PreviewCorner(log);
        PreviewTee(log);
        PreviewCross(log);

        Debug.Log(log.ToString());
    }

    private const string StraightWidePrefabPath = BranchingPipeAssetBinder.PrefabFolder + "/PipeStraightWide2D.prefab";
    private const string CornerPrefabPath = BranchingPipeAssetBinder.PrefabFolder + "/PipeCorner2D.prefab";

    private static void PreviewStraight(StringBuilder log)
    {
        log.AppendLine("-- Straight --");
        Preview(log, StraightWidePrefabPath, PipeType2D.Straight, 0, new[] { Direction2D.Left, Direction2D.Right });
        Preview(log, StraightWidePrefabPath, PipeType2D.Straight, 1, new[] { Direction2D.Up, Direction2D.Down });
    }

    private static void PreviewCorner(StringBuilder log)
    {
        log.AppendLine("-- Corner --");
        string path = CornerPrefabPath;
        Direction2D[][] table =
        {
            new[] { Direction2D.Up, Direction2D.Right },
            new[] { Direction2D.Right, Direction2D.Down },
            new[] { Direction2D.Down, Direction2D.Left },
            new[] { Direction2D.Left, Direction2D.Up }
        };
        for (int r = 0; r < 4; r++)
        {
            Preview(log, path, PipeType2D.Corner, r, table[r]);
        }
    }

    private static void PreviewTee(StringBuilder log)
    {
        log.AppendLine("-- Tee --");
        Direction2D[][] table =
        {
            new[] { Direction2D.Up, Direction2D.Left, Direction2D.Right },
            new[] { Direction2D.Up, Direction2D.Right, Direction2D.Down },
            new[] { Direction2D.Right, Direction2D.Down, Direction2D.Left },
            new[] { Direction2D.Down, Direction2D.Left, Direction2D.Up }
        };
        for (int r = 0; r < 4; r++)
        {
            Preview(log, BranchingPipeAssetBinder.TeePrefabPath, PipeType2D.Tee, r, table[r]);
        }
    }

    private static void PreviewCross(StringBuilder log)
    {
        log.AppendLine("-- Cross --");
        Preview(log, BranchingPipeAssetBinder.CrossPrefabPath, PipeType2D.Cross, 0,
            new[] { Direction2D.Up, Direction2D.Right, Direction2D.Down, Direction2D.Left });
    }

    private static void Preview(StringBuilder log, string prefabPath, PipeType2D pipeType, int rotationIndex, Direction2D[] worldEntrySides)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            log.AppendLine($"  ({pipeType} prefab not found at '{prefabPath}' - run 'Bind T and Cross Pipe Assets' / build the scene first.)");
            return;
        }

        GameObject instance = Object.Instantiate(prefabAsset);
        try
        {
            var pipeTile = instance.GetComponent<PipeTile2D>();
            Transform baseVisual = instance.transform.Find("BaseVisual");
            Transform waterOverlay = instance.transform.Find("WaterOverlay");

            if (pipeTile == null || baseVisual == null || waterOverlay == null)
            {
                log.AppendLine($"  ({prefabPath} is missing PipeTile2D/BaseVisual/WaterOverlay.)");
                return;
            }

            pipeTile.Initialize(pipeType, rotationIndex, Vector2Int.zero);

            foreach (Direction2D worldEntry in worldEntrySides)
            {
                Direction2D localEntry = worldEntry.ToLocalDirection(rotationIndex);
                PipeFlowVisualProfile2D.ResolveCorrection(pipeType, localEntry, out float extraRotationZ, out bool flipX);

                log.AppendLine($"  rotationIndex={rotationIndex} worldEntry={worldEntry} localEntry={localEntry} " +
                    $"-> extraRotationZ={extraRotationZ:0.##} flipX={flipX} " +
                    $"(BaseVisual Z={baseVisual.localEulerAngles.z:0.##}, WaterOverlay base Z={waterOverlay.localEulerAngles.z:0.##})");
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}
