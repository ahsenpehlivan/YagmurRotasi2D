using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;

/// <summary>
/// Phase 8 Fast-Track: validates the full Levels 1-100 campaign range using
/// the exact same structural checks CampaignValidationCommands2D.ValidateOneLevel
/// already applies (asset exists, correct grid size, solved state valid via
/// the real unchanged FlowSolver2D, Target reached, no leak, all pipes
/// Source-reachable, start state unsolved, duplicate coordinates, active
/// pipe count, minimum taps, deterministic content hash) - never a second,
/// potentially-drifting copy of that logic. Exact uniqueness is deliberately
/// NOT re-run here (that remains CampaignValidationCommands2D's "Validate
/// Unique Solutions" diagnostic command) - this command only reports
/// whichever uniqueness status is already stored on the asset
/// (a real solution count for exact-uniqueness-checked levels, or
/// CampaignLevelDefinition2D.SolutionCountNotChecked for Fast-Track levels)
/// and never fails validation because of it.
/// </summary>
public static class CampaignFastTrackValidationCommands2D
{
    private const int FirstLevel = 1;
    private const int LastLevel = 100;

    [MenuItem("YagmurRotasi2D/Phase 8/Validate Fast-Track Campaign")]
    public static bool ValidateFastTrackCampaign()
    {
        CampaignLevelCatalog2D catalog = AssetDatabase.LoadAssetAtPath<CampaignLevelCatalog2D>(CampaignAssetIO2D.CatalogAssetPath);

        var lines = new List<string>();
        var spawned = new List<GameObject>();
        var seenLevelNumbers = new HashSet<int>();

        int passCount = 0;
        int failCount = 0;
        int missingCount = 0;
        int notCheckedUniquenessCount = 0;

        try
        {
            for (int levelNumber = FirstLevel; levelNumber <= LastLevel; levelNumber++)
            {
                CampaignLevelDefinition2D def = null;
                if (catalog != null && catalog.levels != null && levelNumber - 1 < catalog.levels.Count)
                {
                    def = catalog.levels[levelNumber - 1];
                }

                if (def == null)
                {
                    lines.Add($"MISSING: Level {levelNumber} - no asset in catalog slot {levelNumber}.");
                    missingCount++;
                    continue;
                }

                var problems = new List<string>();
                CampaignValidationCommands2D.ValidateOneLevel(def, levelNumber, seenLevelNumbers, problems, spawned);

                if (problems.Count == 0)
                {
                    bool uniquenessChecked = def.solutionCount != CampaignLevelDefinition2D.SolutionCountNotChecked;
                    if (!uniquenessChecked) notCheckedUniquenessCount++;

                    string uniquenessStatus = uniquenessChecked ? def.solutionCount.ToString() : "NotChecked";
                    string hashPrefix = string.IsNullOrEmpty(def.contentHash) ? "(none)" : def.contentHash.Substring(0, System.Math.Min(12, def.contentHash.Length));

                    lines.Add($"PASS: Level {def.levelNumber} ('{def.displayName}', {def.gridWidth}x{def.gridHeight}, " +
                        $"{def.pipes.Count} pipes, minTaps={def.minimumRequiredTaps}, uniqueness={uniquenessStatus}, " +
                        $"generatorVersion={def.generatorVersion}, hash={hashPrefix}...).");
                    passCount++;
                }
                else
                {
                    lines.Add($"FAIL: Level {levelNumber} ('{def.displayName}') - " + string.Join("; ", problems));
                    failCount++;
                }
            }
        }
        finally
        {
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        bool ok = failCount == 0 && missingCount == 0;
        lines.Add($"Fast-Track campaign validation ({FirstLevel}-{LastLevel}): {passCount} PASSED, {failCount} FAILED, {missingCount} MISSING " +
            $"({notCheckedUniquenessCount} of the passed levels report uniqueness=NotChecked - this never fails validation).");

        if (ok)
        {
            Debug.Log(string.Join("\n", lines));
        }
        else
        {
            Debug.LogError(string.Join("\n", lines));
        }

        return ok;
    }
}
