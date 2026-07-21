using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;

/// <summary>
/// Phase 8 Fast-Track: generates Levels 7-100 in transactional decade
/// batches, using CampaignLevelGenerator2D with
/// GenerationRequest.RequireExactUniqueness=false (see that field's doc
/// comment) - the expensive CampaignUniquenessSolver2D search is never
/// invoked here. Acceptance instead relies on the cheaper structural gates
/// CampaignLevelGenerator2D.TryGenerateOnce already applies for a
/// RequireExactUniqueness=false request: solved layout reaches Target, has
/// no leak, every production pipe is Source-reachable, every pipe is inside
/// bounds, no duplicate coordinates, the start layout is unsolved, and both
/// active pipe count and minimum tap count fit the level's difficulty
/// profile (CampaignDifficultyProfiles2D). Never modifies Levels 1-6, never
/// calls CampaignUniquenessSolver2D, and never touches
/// CampaignGenerateCommand2D's original pilot-batch command (which still
/// requires exact uniqueness, unchanged, and remains available as a
/// diagnostic/reference path).
/// </summary>
public static class CampaignFastTrackCommand2D
{
    /// <summary>
    /// Deliberately a distinct constant from CampaignGenerateCommand2D.PilotBaseSeed
    /// and a distinct generatorVersion (below) - Fast-Track candidates are
    /// accepted under different criteria than the exact-uniqueness pilot
    /// path, so they must never be mistaken for (or silently collide with)
    /// pilot-path output in a stored seed/hash.
    /// </summary>
    public const int FastTrackBaseSeed = 812026;

    /// <summary>
    /// Stamped into CampaignLevelDefinition2D.generatorVersion and folded
    /// into attempt-seed derivation (CampaignLevelGenerator2D.GenerationRequest.GeneratorVersionOverride).
    /// A "+r{N}" suffix is appended by RegenerateSelectedWithNextSeed to
    /// deterministically move to a different accepted candidate for one
    /// level without touching any other level's asset.
    /// </summary>
    public const string FastTrackGeneratorVersion = "FastTrack.1";

    private const int FirstGeneratedLevel = 7;
    private const int LastLevel = 100;

    private static readonly string[] EducationalMessages =
    {
        "Yağmur suyu toplanıp doğru yönlendirilirse şehirdeki yeşil alanları sulamak için kullanılabilir.",
        "Borularda sızıntı olmadan taşınan su, kurak günler için biriktirilebilir.",
        "Doğru planlanmış bir su ağı, hem parkları sular hem de taşkınları önler.",
        "Her boru parçası suyun doğru yöne akması için doğru şekilde çevrilmelidir.",
        "Toplanan yağmur suyu, şehir ağaçlarının ve çiçeklerin yaşaması için değerlidir."
    };

    private static readonly (int First, int Last)[] BatchRanges =
    {
        (7, 10), (11, 20), (21, 30), (31, 40), (41, 50),
        (51, 60), (61, 70), (71, 80), (81, 90), (91, 100)
    };

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 7-10")]
    public static bool GenerateLevels7To10() => GenerateBatch(7, 10);

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 11-20")]
    public static bool GenerateLevels11To20() => GenerateBatch(11, 20);

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 21-30")]
    public static bool GenerateLevels21To30() => GenerateBatch(21, 30);

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 31-40")]
    public static bool GenerateLevels31To40() => GenerateBatch(31, 40);

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 41-50")]
    public static bool GenerateLevels41To50() => GenerateBatch(41, 50);

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 51-60")]
    public static bool GenerateLevels51To60() => GenerateBatch(51, 60);

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 61-70")]
    public static bool GenerateLevels61To70() => GenerateBatch(61, 70);

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 71-80")]
    public static bool GenerateLevels71To80() => GenerateBatch(71, 80);

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 81-90")]
    public static bool GenerateLevels81To90() => GenerateBatch(81, 90);

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Fast-Track Levels 91-100")]
    public static bool GenerateLevels91To100() => GenerateBatch(91, 100);

    /// <summary>
    /// Runs whichever of the 10 decade batches above still has at least one
    /// missing/ungenerated level, one batch at a time - each batch is its
    /// own transaction, so a failure in one decade never blocks the others
    /// (unlike the original pilot command, this does NOT require all
    /// remaining levels to succeed in a single transaction).
    /// </summary>
    [MenuItem("YagmurRotasi2D/Phase 8/Generate All Missing Fast-Track Levels")]
    public static bool GenerateAllMissingLevels()
    {
        var batchReports = new List<string>();
        bool overallOk = true;

        foreach ((int first, int last) in BatchRanges)
        {
            CampaignLevelCatalog2D catalog = AssetDatabase.LoadAssetAtPath<CampaignLevelCatalog2D>(CampaignAssetIO2D.CatalogAssetPath);

            bool anyMissing = false;
            for (int levelNumber = first; levelNumber <= last; levelNumber++)
            {
                if (!LevelAlreadyGenerated(catalog, levelNumber))
                {
                    anyMissing = true;
                    break;
                }
            }

            if (!anyMissing)
            {
                batchReports.Add($"Levels {first}-{last}: already complete, skipped.");
                continue;
            }

            bool ok = GenerateBatch(first, last);
            batchReports.Add($"Levels {first}-{last}: {(ok ? "generated and committed." : "FAILED - left unchanged, see error above.")}");
            overallOk &= ok;
        }

        Debug.Log("CampaignFastTrackCommand2D: Generate All Missing Fast-Track Levels summary:\n" + string.Join("\n", batchReports));
        return overallOk;
    }

    private static bool LevelAlreadyGenerated(CampaignLevelCatalog2D catalog, int levelNumber)
    {
        if (catalog == null || catalog.levels == null || levelNumber - 1 >= catalog.levels.Count)
        {
            return false;
        }

        CampaignLevelDefinition2D def = catalog.levels[levelNumber - 1];
        return def != null && def.pipes != null && def.pipes.Count > 0;
    }

    /// <summary>
    /// Regenerates whichever single CampaignLevelDefinition2D asset is
    /// currently selected in the Project window, deterministically moving to
    /// the NEXT candidate for that level instead of reproducing the same one
    /// (unlike CampaignGenerateCommand2D.RegenerateSelectedLevel, which
    /// replays the exact stored seed for drift-detection). Does this by
    /// parsing a "+r{N}" regeneration-nonce suffix off the asset's currently
    /// stored generatorVersion (0 if absent) and requesting generation again
    /// with that nonce incremented - a distinct version string yields a
    /// distinct deterministic attempt-seed sequence
    /// (CampaignSeededRandom2D.DeriveAttemptSeed), so the same nonce always
    /// reproduces the same regenerated candidate, and every other level
    /// asset is left completely untouched.
    /// </summary>
    [MenuItem("YagmurRotasi2D/Phase 8/Regenerate Selected Campaign Level With Next Seed")]
    public static bool RegenerateSelectedWithNextSeed()
    {
        var selected = Selection.activeObject as CampaignLevelDefinition2D;
        if (selected == null)
        {
            Debug.LogError("CampaignFastTrackCommand2D: Select a CampaignLevelDefinition2D asset in the Project window first.");
            return false;
        }

        return RegenerateWithNextSeed(selected);
    }

    /// <summary>Internal (not private) so CampaignFastTrackPreviewWindow2D can drive the exact same regeneration path from its own "Regenerate With Next Seed" button, instead of a second, potentially-drifting copy.</summary>
    internal static bool RegenerateWithNextSeed(CampaignLevelDefinition2D selected)
    {
        if (selected.levelNumber < FirstGeneratedLevel)
        {
            Debug.LogError($"CampaignFastTrackCommand2D: Level {selected.levelNumber} is hand-authored (Levels 1-6), not generator " +
                "output - it has no generation seed to advance. Levels 1-6 are never regenerated this way.");
            return false;
        }

        int nextNonce = ParseRegenerationNonce(selected.generatorVersion) + 1;
        string overrideVersion = $"{FastTrackGeneratorVersion}+r{nextNonce}";

        CampaignLevelGenerator2D.GenerationRequest request = BuildFastTrackRequest(selected.levelNumber);
        request.GeneratorVersionOverride = overrideVersion;

        CampaignLevelGenerator2D.GenerationResult result = CampaignLevelGenerator2D.Generate(request);
        if (!result.Success)
        {
            Debug.LogError($"CampaignFastTrackCommand2D: Regeneration of Level {selected.levelNumber} (next seed, nonce {nextNonce}) " +
                $"FAILED after {result.AttemptsUsed} attempt(s) - {result.LastRejectionReason}. The stored asset was NOT modified.");
            return false;
        }

        string oldHash = selected.contentHash;
        CampaignAssetIO2D.CopyInto(selected, result.Definition);
        Object.DestroyImmediate(result.Definition);
        EditorUtility.SetDirty(selected);
        AssetDatabase.SaveAssets();

        bool identical = oldHash == selected.contentHash;
        Debug.Log($"CampaignFastTrackCommand2D: Level {selected.levelNumber} regenerated with next seed (nonce {nextNonce}, " +
            $"version={overrideVersion}) - " +
            (identical
                ? "content hash UNCHANGED (unexpected for a fresh nonce, but not an error)."
                : $"content hash CHANGED (old={oldHash.Substring(0, 12)}..., new={selected.contentHash.Substring(0, 12)}...). " +
                  "Every other level asset is untouched."));

        return true;
    }

    private static int ParseRegenerationNonce(string generatorVersion)
    {
        if (string.IsNullOrEmpty(generatorVersion))
        {
            return 0;
        }

        int markerIndex = generatorVersion.IndexOf("+r", System.StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return 0;
        }

        string tail = generatorVersion.Substring(markerIndex + 2);
        return int.TryParse(tail, out int nonce) ? nonce : 0;
    }

    /// <summary>
    /// Part H-equivalent transactional batch: every level in [first,last] is
    /// generated and validated in memory FIRST; nothing is written to disk
    /// (no asset, no catalog slot) unless every level in THIS batch succeeds.
    /// A failure discards only this batch's in-memory candidates - assets
    /// from previously committed batches, and the previous catalog, are left
    /// completely unchanged.
    /// </summary>
    private static bool GenerateBatch(int first, int last)
    {
        var summary = new List<string>();
        var results = new List<(int levelNumber, CampaignLevelGenerator2D.GenerationResult result)>();
        int firstFailedLevel = -1;

        for (int levelNumber = first; levelNumber <= last; levelNumber++)
        {
            CampaignLevelGenerator2D.GenerationRequest request = BuildFastTrackRequest(levelNumber);
            CampaignLevelGenerator2D.GenerationResult result = CampaignLevelGenerator2D.Generate(request);
            results.Add((levelNumber, result));

            if (!result.Success)
            {
                firstFailedLevel = levelNumber;
                Debug.LogError($"CampaignFastTrackCommand2D: Level {levelNumber} FAILED after {result.AttemptsUsed} attempt(s) - {result.LastRejectionReason}.");
                break;
            }

            summary.Add($"Level {levelNumber}: {result.Definition.gridWidth}x{result.Definition.gridHeight}, seed={result.AcceptedSeed}, " +
                $"attempts={result.AttemptsUsed}, pipes={result.Definition.pipes.Count}, minTaps={result.Definition.minimumRequiredTaps}, " +
                $"uniqueness=NotChecked (Fast-Track), hash={result.Definition.contentHash.Substring(0, 12)}...");
        }

        bool allSucceeded = firstFailedLevel < 0;

        if (!allSucceeded)
        {
            foreach ((int _, CampaignLevelGenerator2D.GenerationResult result) in results)
            {
                if (result.Definition != null) Object.DestroyImmediate(result.Definition);
            }

            Debug.LogError($"CampaignFastTrackCommand2D: Batch {first}-{last} ABORTED - Level {firstFailedLevel} failed. " +
                "0 levels saved; no assets in this batch were committed. The previous catalog and every other level asset are unchanged." +
                (summary.Count > 0
                    ? "\nLevels that WOULD have been saved (discarded, not committed, since the batch is transactional):\n" + string.Join("\n", summary)
                    : ""));
            return false;
        }

        var savedDefinitions = new List<(int levelNumber, CampaignLevelDefinition2D definition)>();
        foreach ((int levelNumber, CampaignLevelGenerator2D.GenerationResult result) in results)
        {
            CampaignLevelDefinition2D asset = CampaignAssetIO2D.LoadOrCreateDefinitionAsset(levelNumber);
            CampaignAssetIO2D.CopyInto(asset, result.Definition);
            Object.DestroyImmediate(result.Definition);
            EditorUtility.SetDirty(asset);
            savedDefinitions.Add((levelNumber, asset));
        }

        CampaignLevelCatalog2D catalog = CampaignAssetIO2D.LoadOrCreateCatalogAsset();
        foreach ((int levelNumber, CampaignLevelDefinition2D definition) in savedDefinitions)
        {
            CampaignAssetIO2D.AssignCatalogSlot(catalog, levelNumber, definition);
        }
        EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"CampaignFastTrackCommand2D: Batch {first}-{last} SUCCEEDED - all {last - first + 1} level(s) were saved.\n" +
            string.Join("\n", summary));

        return true;
    }

    /// <summary>Internal (not private) so CampaignFastTrackPreviewWindow2D can build the exact same request configuration these batch commands use.</summary>
    internal static CampaignLevelGenerator2D.GenerationRequest BuildFastTrackRequest(int levelNumber)
    {
        CampaignDifficultyProfiles2D.Profile profile = CampaignDifficultyProfiles2D.ForLevel(levelNumber);

        return new CampaignLevelGenerator2D.GenerationRequest
        {
            LevelNumber = levelNumber,
            DisplayName = $"Kampanya Bölümü {levelNumber}",
            EducationalMessage = EducationalMessages[(levelNumber - 1) % EducationalMessages.Length],
            BaseCampaignSeed = FastTrackBaseSeed,
            // Fast-Track attempts are cheap relative to the pilot path (no
            // uniqueness search per attempt), so larger boards can afford a
            // larger attempt budget without generation becoming slow overall.
            MaxAttempts = profile.GridWidth <= 6 ? 300 : profile.GridWidth <= 8 ? 600 : 1000,
            RequireExactUniqueness = false,
            GeneratorVersionOverride = FastTrackGeneratorVersion
        };
    }
}
