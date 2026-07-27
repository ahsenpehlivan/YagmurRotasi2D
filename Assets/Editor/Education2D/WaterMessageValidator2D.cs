using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Education2D;

/// <summary>
/// Read-only Editor tooling over YagmurRotasi2D.Education2D.WaterMessageCatalog2D
/// - validates the 100 hand-authored educational water-awareness messages and
/// offers a plain-text preview. Never modifies or rewrites any message; both
/// commands are Editor-only (Assets/Editor/) and never compiled into a build.
/// </summary>
public static class WaterMessageValidator2D
{
    private const int RequiredLevelCount = 100;
    private const int ReasonableMaxLength = 130;

    // Flags a message as a WARNING if it contains a digit (litres/percentages/
    // rankings/etc. - the content rules explicitly forbid unverifiable numeric
    // claims) or common statistic-flavored Turkish words, without trying to
    // parse whether a specific claim is "true" - any of these is worth a
    // human re-read.
    private static readonly Regex SuspiciousNumberPattern = new Regex(@"\d", RegexOptions.Compiled);
    private static readonly string[] SuspiciousWords =
    {
        "yüzde", "%", "litre", "ton", "milyon", "milyar", "sırasında dünyada", "dünyada birinci", "dünya sıralaması"
    };

    [MenuItem("YagmurRotasi2D/Education/Validate Water Messages")]
    public static void Validate()
    {
        IReadOnlyList<WaterMessageEntry2D> entries = WaterMessageCatalog2D.AllEntries;
        var lines = new List<string>
        {
            "YagmurRotasi2D Water Message Validation",
            $"Entries found: {entries.Count} (required: {RequiredLevelCount})",
            ""
        };

        int errorCount = 0;
        int warningCount = 0;

        // ---------------- Structural checks ----------------
        if (entries.Count != RequiredLevelCount)
        {
            lines.Add($"ERROR: expected exactly {RequiredLevelCount} entries, found {entries.Count}.");
            errorCount++;
        }

        var seenLevels = new HashSet<int>();
        var duplicateLevels = new List<int>();
        foreach (WaterMessageEntry2D entry in entries)
        {
            if (!seenLevels.Add(entry.levelNumber))
            {
                duplicateLevels.Add(entry.levelNumber);
            }
        }
        if (duplicateLevels.Count > 0)
        {
            lines.Add($"ERROR: duplicate level number(s): {string.Join(", ", duplicateLevels)}.");
            errorCount += duplicateLevels.Count;
        }

        var missingLevels = new List<int>();
        for (int level = 1; level <= RequiredLevelCount; level++)
        {
            if (!seenLevels.Contains(level))
            {
                missingLevels.Add(level);
            }
        }
        if (missingLevels.Count > 0)
        {
            lines.Add($"ERROR: missing level(s) 1-{RequiredLevelCount}: {string.Join(", ", missingLevels)}.");
            errorCount += missingLevels.Count;
        }

        // ---------------- Per-entry checks ----------------
        var messageCounts = new Dictionary<string, List<int>>();
        int shortestLength = int.MaxValue;
        int longestLength = int.MinValue;
        string shortestMessage = null;
        string longestMessage = null;
        int shortestLevel = -1;
        int longestLevel = -1;
        var typeCounts = new Dictionary<WaterMessageType2D, int>();

        foreach (WaterMessageEntry2D entry in entries)
        {
            if (!Enum.IsDefined(typeof(WaterMessageType2D), entry.type))
            {
                lines.Add($"ERROR: Level {entry.levelNumber} has an invalid message type ({entry.type}).");
                errorCount++;
            }
            else
            {
                typeCounts[entry.type] = typeCounts.TryGetValue(entry.type, out int c) ? c + 1 : 1;
            }

            if (string.IsNullOrWhiteSpace(entry.message))
            {
                lines.Add($"ERROR: Level {entry.levelNumber} has an empty message.");
                errorCount++;
                continue;
            }

            string trimmed = entry.message.Trim();

            if (!messageCounts.TryGetValue(trimmed, out List<int> levelsForMessage))
            {
                levelsForMessage = new List<int>();
                messageCounts[trimmed] = levelsForMessage;
            }
            levelsForMessage.Add(entry.levelNumber);

            int length = trimmed.Length;
            if (length < shortestLength)
            {
                shortestLength = length;
                shortestMessage = trimmed;
                shortestLevel = entry.levelNumber;
            }
            if (length > longestLength)
            {
                longestLength = length;
                longestMessage = trimmed;
                longestLevel = entry.levelNumber;
            }

            if (length > ReasonableMaxLength)
            {
                lines.Add($"WARNING: Level {entry.levelNumber} message is {length} characters (> {ReasonableMaxLength}) - consider shortening for 2-3 lines on screen.");
                warningCount++;
            }

            if (ContainsSuspiciousNumericClaim(trimmed))
            {
                lines.Add($"WARNING: Level {entry.levelNumber} message contains a number/statistic-flavored word - content rules ask to avoid unverifiable numeric claims. Message: \"{trimmed}\"");
                warningCount++;
            }
        }

        int duplicateMessageCount = 0;
        foreach (var kvp in messageCounts)
        {
            if (kvp.Value.Count > 1)
            {
                duplicateMessageCount++;
                lines.Add($"ERROR: message duplicated across levels {string.Join(", ", kvp.Value)}: \"{kvp.Key}\"");
                errorCount++;
            }
        }

        // ---------------- Level 100 exact-message check ----------------
        WaterMessageEntry2D level100 = entries.FirstOrDefault(e => e.levelNumber == 100);
        if (level100 == null)
        {
            lines.Add("ERROR: Level 100 entry not found - cannot verify the required final message.");
            errorCount++;
        }
        else if (level100.message != WaterMessageCatalog2D.FinalLevelMessage)
        {
            lines.Add($"ERROR: Level 100 message does not exactly match the required final message.\n" +
                $"    Expected: \"{WaterMessageCatalog2D.FinalLevelMessage}\"\n" +
                $"    Found:    \"{level100.message}\"");
            errorCount++;
        }

        // ---------------- Summary ----------------
        lines.Add("");
        lines.Add("=== Summary ===");
        lines.Add("Count by type: " + string.Join(", ", Enum.GetValues(typeof(WaterMessageType2D))
            .Cast<WaterMessageType2D>()
            .Select(t => $"{t}={(typeCounts.TryGetValue(t, out int c) ? c : 0)}")));
        lines.Add(shortestMessage != null
            ? $"Shortest message: {shortestLength} chars (Level {shortestLevel}): \"{shortestMessage}\""
            : "Shortest message: n/a");
        lines.Add(longestMessage != null
            ? $"Longest message: {longestLength} chars (Level {longestLevel}): \"{longestMessage}\""
            : "Longest message: n/a");
        lines.Add($"Duplicate message count: {duplicateMessageCount}");
        lines.Add($"Errors: {errorCount}, Warnings: {warningCount}");
        lines.Add(errorCount == 0 ? "Result: PASS (no errors)" : "Result: FAIL (see errors above)");

        string report = string.Join("\n", lines);
        if (errorCount > 0)
        {
            Debug.LogError(report);
        }
        else if (warningCount > 0)
        {
            Debug.LogWarning(report);
        }
        else
        {
            Debug.Log(report);
        }
    }

    [MenuItem("YagmurRotasi2D/Education/Print All Water Messages")]
    public static void PrintAll()
    {
        var lines = new List<string> { "YagmurRotasi2D - All Water Messages" };
        foreach (WaterMessageEntry2D entry in WaterMessageCatalog2D.AllEntries)
        {
            lines.Add($"Level {entry.levelNumber} [{entry.type}]: {entry.message}");
        }
        Debug.Log(string.Join("\n", lines));
    }

    private static bool ContainsSuspiciousNumericClaim(string message)
    {
        if (SuspiciousNumberPattern.IsMatch(message))
        {
            return true;
        }

        string lower = message.ToLowerInvariant();
        foreach (string word in SuspiciousWords)
        {
            if (lower.Contains(word))
            {
                return true;
            }
        }

        return false;
    }
}
