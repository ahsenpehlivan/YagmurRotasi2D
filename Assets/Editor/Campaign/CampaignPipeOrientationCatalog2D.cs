using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Canonical logical orientations per PipeType2D (Phase 8A.3 Part B) - each
/// orientation is a 4-bit port mask (bit i = 1&lt;&lt;(int)Direction2D, i.e. Up=1,
/// Right=2, Down=4, Left=8), built ONCE per type by probing the real
/// PipeTile2D.GetOpenDirections() for rotationIndex 0-3 and deduplicating
/// identical masks - never a re-typed direction table, so this can never
/// silently drift from the production rotation tables. Straight naturally
/// collapses rotationIndex {0,2} and {1,3} into the same 2 masks; Cross
/// collapses all 4 rotations into its single always-open mask; Corner and
/// Tee each keep all 4 distinct masks. Editor-only (Assembly-CSharp-Editor) -
/// never used at runtime.
/// </summary>
public static class CampaignPipeOrientationCatalog2D
{
    private static readonly Dictionary<PipeType2D, List<int>> masksByType = new Dictionary<PipeType2D, List<int>>();
    private static readonly Dictionary<PipeType2D, Dictionary<int, int>> rotationByTypeAndMask = new Dictionary<PipeType2D, Dictionary<int, int>>();
    private static readonly Dictionary<PipeType2D, Dictionary<int, int>> canonicalIndexByTypeAndMask = new Dictionary<PipeType2D, Dictionary<int, int>>();

    /// <summary>Every type has at most 4 canonical masks (Cross=1, Straight=2, Corner=4, Tee=4) - used as the fixed bit budget for the exact memoization key (Phase 8A.4): 4 bits per variable is always enough regardless of type.</summary>
    public const int MaxCanonicalMasksPerType = 4;

    public static IReadOnlyList<int> CanonicalMasks(PipeType2D type)
    {
        EnsureBuilt(type);
        return masksByType[type];
    }

    /// <summary>The first rotationIndex (0-3) that produces this exact mask for this type - used only when a canonical mask needs to become a concrete PipeTile2D.Initialize() call (e.g. FlowSolver2D validation of a candidate assignment).</summary>
    public static int RotationForMask(PipeType2D type, int mask)
    {
        EnsureBuilt(type);
        return rotationByTypeAndMask[type][mask];
    }

    /// <summary>This mask's position (0..3) within CanonicalMasks(type)'s own stable list - a per-type-relative index, NOT the raw port-mask value. Used to pack a pipe's remaining domain into exactly MaxCanonicalMasksPerType bits for an exact (never hashed/lossy) memoization key.</summary>
    public static int CanonicalIndexOf(PipeType2D type, int mask)
    {
        EnsureBuilt(type);
        return canonicalIndexByTypeAndMask[type][mask];
    }

    public static int ToMask(Direction2D[] directions)
    {
        int mask = 0;
        foreach (Direction2D d in directions)
        {
            mask |= 1 << (int)d;
        }
        return mask;
    }

    public static bool HasDirection(int mask, Direction2D d)
    {
        return (mask & (1 << (int)d)) != 0;
    }

    private static void EnsureBuilt(PipeType2D type)
    {
        if (masksByType.ContainsKey(type))
        {
            return;
        }

        var masks = new List<int>();
        var rotationForMask = new Dictionary<int, int>();

        var probeGo = new GameObject("CampaignPipeOrientationProbe");
        try
        {
            var probe = probeGo.AddComponent<PipeTile2D>();
            for (int rotation = 0; rotation < 4; rotation++)
            {
                probe.Initialize(type, rotation, Vector2Int.zero);
                int mask = ToMask(probe.GetOpenDirections());
                if (!rotationForMask.ContainsKey(mask))
                {
                    rotationForMask[mask] = rotation;
                    masks.Add(mask);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(probeGo);
        }

        masksByType[type] = masks;
        rotationByTypeAndMask[type] = rotationForMask;

        var canonicalIndexForMask = new Dictionary<int, int>();
        for (int index = 0; index < masks.Count; index++)
        {
            canonicalIndexForMask[masks[index]] = index;
        }
        canonicalIndexByTypeAndMask[type] = canonicalIndexForMask;
    }
}
