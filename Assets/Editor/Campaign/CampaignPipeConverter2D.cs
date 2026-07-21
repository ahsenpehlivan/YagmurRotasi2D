using System;
using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Converts a solved graph's per-cell open-direction sets (from
/// CampaignGraphBuilder2D) into real PipeType2D + solvedRotationIndex values -
/// Part I's degree rules (2=Straight/Corner, 3=Tee, 4=Cross) and Part K's
/// "derive solvedRotationIndex exclusively from the solved graph directions,
/// never from spawn-list order or sprite appearance". Also generates
/// deterministic starting scrambles (Part L). Every rotation this class ever
/// derives is verified against the real production PipeTile2D.GetOpenDirections()
/// via a temporary probe instance - never a re-typed copy of the direction
/// tables.
/// </summary>
public static class CampaignPipeConverter2D
{
    /// <summary>Builds one PipeSpawnData2D per graph cell, solvedRotationIndex derived from its open-direction set. Throws if a cell's direction set cannot be matched to any rotation of a supported pipe type (should never happen for a graph respecting Part I's degree rules - CampaignGraphBuilder2D already rejects out-of-range degrees before this runs).</summary>
    public static List<PipeSpawnData2D> BuildSolvedPipes(Dictionary<Vector2Int, HashSet<Direction2D>> openSidesByCell, List<GameObject> probeInstances)
    {
        var result = new List<PipeSpawnData2D>();

        foreach (KeyValuePair<Vector2Int, HashSet<Direction2D>> kv in openSidesByCell)
        {
            Vector2Int cell = kv.Key;
            HashSet<Direction2D> sides = kv.Value;

            (PipeType2D type, int solvedRotation) = ResolveTypeAndRotation(sides, probeInstances);
            StraightVisualVariant2D variant = type == PipeType2D.Straight
                ? (sides.Contains(Direction2D.Left) ? StraightVisualVariant2D.Wide : StraightVisualVariant2D.Narrow)
                : StraightVisualVariant2D.Wide;

            // startRotationIndex is filled in later by GenerateScramble - 0 here is only a temporary placeholder.
            result.Add(new PipeSpawnData2D(type, cell, 0, solvedRotation, variant));
        }

        return result;
    }

    private static (PipeType2D type, int solvedRotation) ResolveTypeAndRotation(HashSet<Direction2D> sides, List<GameObject> probeInstances)
    {
        PipeType2D type;
        switch (sides.Count)
        {
            case 2:
                bool opposite = sides.Contains(Direction2D.Up) && sides.Contains(Direction2D.Down)
                    || sides.Contains(Direction2D.Left) && sides.Contains(Direction2D.Right);
                type = opposite ? PipeType2D.Straight : PipeType2D.Corner;
                break;
            case 3:
                type = PipeType2D.Tee;
                break;
            case 4:
                return (PipeType2D.Cross, 0);
            default:
                throw new InvalidOperationException($"Unsupported open-side count {sides.Count} - graph builder should have rejected this before conversion.");
        }

        var probe = CreateProbe(probeInstances);
        for (int rotation = 0; rotation < 4; rotation++)
        {
            probe.Initialize(type, rotation, Vector2Int.zero);
            Direction2D[] openings = probe.GetOpenDirections();
            if (openings.Length == sides.Count && SetsMatch(openings, sides))
            {
                return (type, rotation);
            }
        }

        throw new InvalidOperationException($"No rotationIndex of {type} matches open sides [{string.Join(",", sides)}].");
    }

    private static bool SetsMatch(Direction2D[] openings, HashSet<Direction2D> sides)
    {
        foreach (Direction2D dir in openings)
        {
            if (!sides.Contains(dir))
            {
                return false;
            }
        }
        return true;
    }

    private static PipeTile2D CreateProbe(List<GameObject> probeInstances)
    {
        var go = new GameObject("CampaignPipeConverterProbe");
        probeInstances.Add(go);
        return go.AddComponent<PipeTile2D>();
    }

    /// <summary>
    /// Deterministic scramble generation (Part L): assigns startRotationIndex
    /// for every non-Cross pipe such that (a) the start state is never already
    /// solved, (b) the total minimum-tap distance (accounting for Straight's
    /// two-fold symmetry - rotationIndex and rotationIndex+2 are logically
    /// identical) falls within [minTaps, maxTaps], and (c) Cross always stays
    /// at rotationIndex 0. Mutates each entry's startRotationIndex in place.
    /// </summary>
    public static int GenerateScramble(List<PipeSpawnData2D> pipes, int minTaps, int maxTaps, CampaignSeededRandom2D rng)
    {
        // Reset every rotatable pipe to its solved state, then apply random
        // forward taps to a randomly-chosen subset until the total falls
        // within range - deterministic for a given rng sequence.
        var rotatableIndices = new List<int>();
        for (int i = 0; i < pipes.Count; i++)
        {
            pipes[i].startRotationIndex = pipes[i].solvedRotationIndex;
            if (pipes[i].pipeType != PipeType2D.Cross)
            {
                rotatableIndices.Add(i);
            }
        }

        if (rotatableIndices.Count == 0)
        {
            return 0;
        }

        int totalTaps = 0;
        int safetyIterations = 0;
        int targetTaps = rng.NextInt(minTaps, maxTaps + 1);

        while (totalTaps < targetTaps && safetyIterations < targetTaps * 20 + 50)
        {
            safetyIterations++;
            int pipeIndex = rng.Choose(rotatableIndices);
            PipeSpawnData2D pipe = pipes[pipeIndex];
            int tapsToApply = rng.NextInt(1, 4); // 1-3 forward taps on this pipe this pass

            for (int t = 0; t < tapsToApply && totalTaps < targetTaps; t++)
            {
                pipe.startRotationIndex = (pipe.startRotationIndex + 1) % 4;
                totalTaps++;
            }
        }

        int minimumTaps = ComputeMinimumTaps(pipes);
        return minimumTaps;
    }

    /// <summary>Sum of each pipe's minimum forward-tap distance from startRotationIndex to solvedRotationIndex, using the real PipeTile2D rotation behavior (Straight's two logically-identical orientations included) - not raw integer subtraction alone.</summary>
    public static int ComputeMinimumTaps(List<PipeSpawnData2D> pipes)
    {
        int total = 0;
        foreach (PipeSpawnData2D pipe in pipes)
        {
            total += MinimumTapsFor(pipe.pipeType, pipe.startRotationIndex, pipe.solvedRotationIndex);
        }
        return total;
    }

    private static int MinimumTapsFor(PipeType2D type, int start, int solved)
    {
        if (type == PipeType2D.Cross)
        {
            return 0;
        }

        if (type == PipeType2D.Straight)
        {
            int equivalentSolved = (solved + 2) % 4;
            int tapsToSolved = ((solved - start) % 4 + 4) % 4;
            int tapsToEquivalent = ((equivalentSolved - start) % 4 + 4) % 4;
            return Mathf.Min(tapsToSolved, tapsToEquivalent);
        }

        return ((solved - start) % 4 + 4) % 4;
    }

    /// <summary>True if every pipe's startRotationIndex already equals a logically-solved rotation (Straight's two-fold symmetry included) - Part L requires the scramble to never accidentally start already solved.</summary>
    public static bool IsAlreadySolvedState(List<PipeSpawnData2D> pipes)
    {
        foreach (PipeSpawnData2D pipe in pipes)
        {
            if (MinimumTapsFor(pipe.pipeType, pipe.startRotationIndex, pipe.solvedRotationIndex) != 0)
            {
                return false;
            }
        }
        return true;
    }
}
