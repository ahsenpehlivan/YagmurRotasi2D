using System;
using System.Security.Cryptography;
using System.Text;
using YagmurRotasi2D.Campaign2D;

/// <summary>
/// Stable content hash for a campaign level - level number, dimensions,
/// Source/Target, ordered pipe data (position/type/start/solved rotations),
/// generator version and seed. Never includes a Unity object instance ID or
/// anything else unstable across domain reloads/re-imports. Regenerating the
/// same level from the same (levelNumber, generatorVersion, deterministicSeed)
/// must always produce the same hash - used by batch validation to detect
/// drift between a saved asset and what the generator would currently
/// produce for its own stored seed.
/// </summary>
public static class CampaignContentHash2D
{
    public static string ComputeHash(CampaignLevelDefinition2D definition)
    {
        var sb = new StringBuilder();
        sb.Append(definition.levelNumber).Append('|');
        sb.Append(definition.gridWidth).Append('x').Append(definition.gridHeight).Append('|');
        sb.Append(definition.sourceCell).Append('>').Append(definition.sourceOutputDirection).Append('|');
        sb.Append(definition.targetCell).Append('<').Append(definition.targetEntryDirection).Append('|');
        sb.Append(definition.generatorVersion).Append('|');
        sb.Append(definition.deterministicSeed).Append('|');

        // Pipe order is itself part of the deterministic output (the
        // generator always emits pipes in the same order for the same seed/
        // version), so no extra sort is applied - two lists with the same
        // pipes in a different order are intentionally treated as different
        // content (they came from different generator runs).
        foreach (var pipe in definition.pipes)
        {
            sb.Append(pipe.gridPos).Append(':').Append(pipe.pipeType)
              .Append(':').Append(pipe.startRotationIndex)
              .Append(':').Append(pipe.solvedRotationIndex).Append(';');
        }

        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hashBytes = sha.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }
    }
}
