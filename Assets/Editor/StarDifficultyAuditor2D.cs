using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Read-only report over every campaign level's move-based star thresholds
/// (ScoreManager2D.CalculateOptimalMoves/CalculateAutomaticStarLimits/
/// ResolveStarLimits - the exact same methods LevelManager2D.LoadLevel calls
/// at runtime, never a second/duplicated formula). Never modifies or saves
/// any level asset - purely informational, for manually tuning outlier
/// levels afterward.
/// </summary>
public static class StarDifficultyAuditor2D
{
    /// <summary>"Unusually large" threshold-jump flag between neighboring levels' threeStarMoveLimit - a plain fixed constant, not a percentage, so it stays meaningful across both small (5x5) and large (10x10) levels. Expected to legitimately fire at campaign difficulty-tier grid-size boundaries (e.g. Level 10->11) - those are still worth a human look, not false positives to suppress.</summary>
    private const int LargeNeighborJumpThreshold = 10;

    /// <summary>A manual override is flagged as "drastically different" from the automatic result if it differs by more than this many moves, or more than half the automatic value - whichever is larger.</summary>
    private const int ManualDivergenceMinDelta = 4;

    private class LevelEntry
    {
        public int LevelNumber;
        public LevelData2D Data;
    }

    [MenuItem("YagmurRotasi2D/Progress/Audit Star Difficulty")]
    public static void Audit()
    {
        List<LevelEntry> entries = LoadLevels();
        var lines = new List<string>
        {
            "YagmurRotasi2D Star Difficulty Audit",
            $"Levels found: {entries.Count}",
            ""
        };

        int errorCount = 0;
        int warningCount = 0;
        int minOptimal = int.MaxValue, maxOptimal = int.MinValue;
        int minThree = int.MaxValue, maxThree = int.MinValue;
        int minTwo = int.MaxValue, maxTwo = int.MinValue;
        int previousThreeStarLimit = -1;

        foreach (LevelEntry entry in entries)
        {
            if (entry.Data == null)
            {
                lines.Add($"Level {entry.LevelNumber} [ERROR]: missing level data (null catalog slot).");
                errorCount++;
                continue;
            }

            LevelData2D data = entry.Data;
            var levelErrors = new List<string>();
            var levelWarnings = new List<string>();

            int rotatableCount = 0;
            if (data.pipes != null)
            {
                foreach (PipeSpawnData2D pipe in data.pipes)
                {
                    if (pipe.pipeType == PipeType2D.Cross)
                        continue;

                    rotatableCount++;
                    if (pipe.startRotationIndex < 0 || pipe.startRotationIndex > 3
                        || pipe.solvedRotationIndex < 0 || pipe.solvedRotationIndex > 3)
                    {
                        levelErrors.Add($"pipe at {pipe.gridPos} has invalid rotation data (start={pipe.startRotationIndex}, solved={pipe.solvedRotationIndex})");
                    }
                }
            }

            int optimalMoves = ScoreManager2D.CalculateOptimalMoves(data.pipes);
            (int threeStarLimit, int twoStarLimit, bool usedManual) = ScoreManager2D.ResolveStarLimits(
                optimalMoves, data.useManualStarLimits, data.manualThreeStarMoveLimit, data.manualTwoStarMoveLimit);

            if (optimalMoves < 0)
                levelErrors.Add($"optimalMoves is negative ({optimalMoves})");
            if (threeStarLimit < optimalMoves)
                levelErrors.Add($"threeStarMoveLimit ({threeStarLimit}) < optimalMoves ({optimalMoves})");
            if (twoStarLimit <= threeStarLimit)
                levelErrors.Add($"twoStarMoveLimit ({twoStarLimit}) <= threeStarMoveLimit ({threeStarLimit})");

            if (optimalMoves == 0)
                levelWarnings.Add("optimalMoves is 0 (already solved at spawn) - fine for a deliberate tutorial level, otherwise review.");

            if (data.useManualStarLimits && usedManual)
            {
                (int autoThree, int autoTwo) = ScoreManager2D.CalculateAutomaticStarLimits(optimalMoves);
                int threeDelta = Mathf.Abs(data.manualThreeStarMoveLimit - autoThree);
                int twoDelta = Mathf.Abs(data.manualTwoStarMoveLimit - autoTwo);
                int threeAllowed = Mathf.Max(ManualDivergenceMinDelta, autoThree / 2);
                int twoAllowed = Mathf.Max(ManualDivergenceMinDelta, autoTwo / 2);

                if (threeDelta > threeAllowed || twoDelta > twoAllowed)
                {
                    levelWarnings.Add($"manual limits (three={data.manualThreeStarMoveLimit}, two={data.manualTwoStarMoveLimit}) " +
                        $"differ drastically from automatic (three={autoThree}, two={autoTwo})");
                }
            }
            // Note: an invalid manual override (useManualStarLimits=true but
            // usedManual=false) already logged its own warning from inside
            // ScoreManager2D.ResolveStarLimits above - not duplicated here.

            if (previousThreeStarLimit >= 0 && Mathf.Abs(threeStarLimit - previousThreeStarLimit) > LargeNeighborJumpThreshold)
            {
                levelWarnings.Add($"threeStarMoveLimit jumped by {Mathf.Abs(threeStarLimit - previousThreeStarLimit)} " +
                    $"from the previous level (previous={previousThreeStarLimit}, current={threeStarLimit})");
            }
            previousThreeStarLimit = threeStarLimit;

            minOptimal = Mathf.Min(minOptimal, optimalMoves);
            maxOptimal = Mathf.Max(maxOptimal, optimalMoves);
            minThree = Mathf.Min(minThree, threeStarLimit);
            maxThree = Mathf.Max(maxThree, threeStarLimit);
            minTwo = Mathf.Min(minTwo, twoStarLimit);
            maxTwo = Mathf.Max(maxTwo, twoStarLimit);

            string status = levelErrors.Count > 0 ? "ERROR" : (levelWarnings.Count > 0 ? "WARNING" : "OK");
            string limitsSource = !data.useManualStarLimits ? "automatic" : (usedManual ? "manual" : "manual-invalid->automatic");

            lines.Add($"Level {entry.LevelNumber} [{status}]: {data.gridWidth}x{data.gridHeight}, rotatablePipes={rotatableCount}, " +
                $"optimalMoves={optimalMoves}, threeStarMoveLimit={threeStarLimit}, twoStarMoveLimit={twoStarLimit}, limits={limitsSource}");

            foreach (string error in levelErrors)
            {
                lines.Add($"    ERROR: {error}");
                errorCount++;
            }
            foreach (string warning in levelWarnings)
            {
                lines.Add($"    WARNING: {warning}");
                warningCount++;
            }
        }

        lines.Add("");
        lines.Add("=== Summary ===");
        lines.Add($"optimalMoves: min={FormatMinMax(minOptimal)}, max={FormatMinMax(maxOptimal)}");
        lines.Add($"threeStarMoveLimit: min={FormatMinMax(minThree)}, max={FormatMinMax(maxThree)}");
        lines.Add($"twoStarMoveLimit: min={FormatMinMax(minTwo)}, max={FormatMinMax(maxTwo)}");
        lines.Add($"Errors: {errorCount}, Warnings: {warningCount}");

        Debug.Log(string.Join("\n", lines));
    }

    private static string FormatMinMax(int value) => value == int.MaxValue || value == int.MinValue ? "n/a" : value.ToString();

    /// <summary>Prefers the saved campaign catalog (Assets/Resources/CampaignLevelCatalog2D.asset), including null slots as "missing level data" - falls back to LevelManager2D.BuildLevels() only if no catalog exists, matching LevelManager2D.ResolveLevels()'s exact same preference order.</summary>
    private static List<LevelEntry> LoadLevels()
    {
        var result = new List<LevelEntry>();
        CampaignLevelCatalog2D catalog = AssetDatabase.LoadAssetAtPath<CampaignLevelCatalog2D>(CampaignAssetIO2D.CatalogAssetPath);

        if (catalog != null && catalog.levels != null && catalog.levels.Count > 0)
        {
            for (int i = 0; i < catalog.levels.Count; i++)
            {
                CampaignLevelDefinition2D def = catalog.levels[i];
                result.Add(new LevelEntry { LevelNumber = i + 1, Data = def != null ? def.ToLevelData() : null });
            }
            return result;
        }

        List<LevelData2D> fallback = LevelManager2D.BuildLevels();
        for (int i = 0; i < fallback.Count; i++)
        {
            result.Add(new LevelEntry { LevelNumber = i + 1, Data = fallback[i] });
        }
        return result;
    }
}
