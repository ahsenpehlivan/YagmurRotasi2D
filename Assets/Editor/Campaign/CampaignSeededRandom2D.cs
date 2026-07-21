using System.Collections.Generic;

/// <summary>
/// Deterministic seeded RNG for Editor-time campaign level generation. Never
/// uses UnityEngine.Random (global, order-of-calls-dependent state that other
/// Editor code running in the same session could perturb) - wraps
/// System.Random with an explicit seed, so the exact same seed always
/// produces the exact same sequence regardless of anything else that has run
/// in the Editor. Editor-only (Assembly-CSharp-Editor) - never used at
/// runtime, per Phase 8A's "generate in Editor, load baked assets at
/// runtime" architecture.
/// </summary>
public sealed class CampaignSeededRandom2D
{
    private readonly System.Random random;

    public CampaignSeededRandom2D(int seed)
    {
        random = new System.Random(seed);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        return random.Next(minInclusive, maxExclusive);
    }

    public float NextFloat01()
    {
        return (float)random.NextDouble();
    }

    public T Choose<T>(IReadOnlyList<T> items)
    {
        return items[NextInt(0, items.Count)];
    }

    /// <summary>Fisher-Yates shuffle, in place, deterministic for a given seed.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>
    /// Combines a base campaign seed, level number, generator version and a
    /// per-attempt index into one deterministic per-attempt seed - the exact
    /// same combination always reproduces the exact same seed, so a level can
    /// always be regenerated identically from its stored (baseCampaignSeed,
    /// levelNumber, generatorVersion) alone by replaying attempts 0, 1, 2...
    /// in order until the same accepted attempt is reached again.
    /// </summary>
    public static int DeriveAttemptSeed(int baseCampaignSeed, int levelNumber, string generatorVersion, int attemptIndex)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + baseCampaignSeed;
            hash = hash * 31 + levelNumber;
            hash = hash * 31 + (generatorVersion != null ? generatorVersion.GetHashCode() : 0);
            hash = hash * 31 + attemptIndex;
            return hash;
        }
    }
}
