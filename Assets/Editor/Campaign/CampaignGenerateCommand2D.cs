using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;

/// <summary>
/// Editor commands that actually run CampaignLevelGenerator2D and persist its
/// output as assets (Phase 8A Part T): the pilot batch (Levels 7-20) and
/// single-level regeneration from an already-stored seed. Both commands are
/// idempotent - re-running them with the same generator version reproduces
/// the exact same accepted seed/candidate for each level (attempt-derivation
/// is a pure function of levelNumber/seed/version/attempt), and writes into
/// the SAME asset (by fixed path, via CampaignAssetIO2D) rather than creating
/// a duplicate each time.
/// </summary>
public static class CampaignGenerateCommand2D
{
    /// <summary>
    /// Fixed campaign seed for the entire pilot batch - deliberately a plain
    /// constant (not derived from DateTime/environment) so this command
    /// produces byte-identical output every time it is run, on any machine.
    /// </summary>
    public const int PilotBaseSeed = 802026;

    private const int PilotFirstLevel = 7;
    private const int PilotLastLevel = 20;

    private static readonly string[] EducationalMessages =
    {
        "Yağmur suyu toplanıp doğru yönlendirilirse şehirdeki yeşil alanları sulamak için kullanılabilir.",
        "Borularda sızıntı olmadan taşınan su, kurak günler için biriktirilebilir.",
        "Doğru planlanmış bir su ağı, hem parkları sular hem de taşkınları önler.",
        "Her boru parçası suyun doğru yöne akması için doğru şekilde çevrilmelidir.",
        "Toplanan yağmur suyu, şehir ağaçlarının ve çiçeklerin yaşaması için değerlidir."
    };

    [MenuItem("YagmurRotasi2D/Phase 8/Generate Pilot Campaign Levels 7-20")]
    public static bool GeneratePilotLevels()
    {
        var summary = new List<string>();
        bool allSucceeded = true;
        var savedDefinitions = new List<(int levelNumber, CampaignLevelDefinition2D definition)>();

        for (int levelNumber = PilotFirstLevel; levelNumber <= PilotLastLevel; levelNumber++)
        {
            var request = BuildRequest(levelNumber);
            CampaignLevelGenerator2D.GenerationResult result = CampaignLevelGenerator2D.Generate(request);

            if (!result.Success)
            {
                Debug.LogError($"CampaignGenerateCommand2D: Level {levelNumber} FAILED after {result.AttemptsUsed} attempt(s) - " +
                    $"{result.LastRejectionReason}. Stopping the pilot batch here - levels {PilotFirstLevel}-{levelNumber - 1} " +
                    "(if any succeeded above) were already saved; levels after this one were not attempted.");
                allSucceeded = false;
                break;
            }

            CampaignLevelDefinition2D asset = CampaignAssetIO2D.LoadOrCreateDefinitionAsset(levelNumber);
            CampaignAssetIO2D.CopyInto(asset, result.Definition);
            Object.DestroyImmediate(result.Definition);
            EditorUtility.SetDirty(asset);

            savedDefinitions.Add((levelNumber, asset));

            summary.Add($"Level {levelNumber}: {asset.gridWidth}x{asset.gridHeight}, seed={result.AcceptedSeed}, " +
                $"attempts={result.AttemptsUsed}, pipes={asset.pipes.Count}, minTaps={asset.minimumRequiredTaps}, " +
                $"solutionCount={asset.solutionCount}, hash={asset.contentHash.Substring(0, 12)}...");
        }

        if (savedDefinitions.Count > 0)
        {
            CampaignLevelCatalog2D catalog = CampaignAssetIO2D.LoadOrCreateCatalogAsset();
            foreach ((int levelNumber, CampaignLevelDefinition2D definition) in savedDefinitions)
            {
                CampaignAssetIO2D.AssignCatalogSlot(catalog, levelNumber, definition);
            }
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log((allSucceeded
            ? $"CampaignGenerateCommand2D: Pilot generation SUCCEEDED - Levels {PilotFirstLevel}-{PilotLastLevel} generated and saved."
            : $"CampaignGenerateCommand2D: Pilot generation INCOMPLETE - {savedDefinitions.Count} level(s) saved before the failure above.")
            + "\n" + string.Join("\n", summary));

        return allSucceeded;
    }

    /// <summary>
    /// Regenerates whichever single CampaignLevelDefinition2D asset is
    /// currently selected in the Project window, using its own stored
    /// deterministicSeed/generatorVersion/levelNumber - so it reproduces
    /// exactly what the generator would (still) produce for that exact seed.
    /// Refuses (does nothing) if the selection is not a generator-produced
    /// definition (generatorVersion == "hand-authored" - Levels 1-6 are never
    /// regenerated this way, only re-migrated via CampaignMigration2D).
    /// </summary>
    [MenuItem("YagmurRotasi2D/Phase 8/Regenerate Selected Level From Stored Seed")]
    public static bool RegenerateSelectedLevel()
    {
        var selected = Selection.activeObject as CampaignLevelDefinition2D;
        if (selected == null)
        {
            Debug.LogError("CampaignGenerateCommand2D: Select a CampaignLevelDefinition2D asset in the Project window first.");
            return false;
        }

        if (selected.generatorVersion == "hand-authored")
        {
            Debug.LogError($"CampaignGenerateCommand2D: Level {selected.levelNumber} is hand-authored (Levels 1-6), not generator " +
                "output - it has no generation seed to replay. Use \"Migrate Existing Levels 1-6 To Campaign Catalog\" instead.");
            return false;
        }

        var request = BuildRequest(selected.levelNumber);
        CampaignLevelGenerator2D.GenerationResult result = CampaignLevelGenerator2D.GenerateFromExactSeed(request, selected.deterministicSeed);

        if (!result.Success)
        {
            Debug.LogError($"CampaignGenerateCommand2D: Regeneration of Level {selected.levelNumber} from stored seed " +
                $"{selected.deterministicSeed} FAILED - {result.LastRejectionReason}. This indicates the generator algorithm has " +
                "changed since this asset was produced (content drift) - the stored asset was NOT modified.");
            return false;
        }

        string oldHash = selected.contentHash;
        CampaignAssetIO2D.CopyInto(selected, result.Definition);
        Object.DestroyImmediate(result.Definition);
        EditorUtility.SetDirty(selected);
        AssetDatabase.SaveAssets();

        bool identical = oldHash == selected.contentHash;
        Debug.Log($"CampaignGenerateCommand2D: Level {selected.levelNumber} regenerated from seed {selected.deterministicSeed} - " +
            (identical
                ? "content hash UNCHANGED (byte-identical to the previously saved asset)."
                : $"content hash CHANGED (old={oldHash.Substring(0, 12)}..., new={selected.contentHash.Substring(0, 12)}...) - " +
                  "the generator produces different output for this seed now than when this asset was last saved."));

        return true;
    }

    private static CampaignLevelGenerator2D.GenerationRequest BuildRequest(int levelNumber)
    {
        CampaignDifficultyProfiles2D.Profile profile = CampaignDifficultyProfiles2D.ForLevel(levelNumber);

        return new CampaignLevelGenerator2D.GenerationRequest
        {
            LevelNumber = levelNumber,
            DisplayName = $"Kampanya Bölümü {levelNumber}",
            EducationalMessage = EducationalMessages[(levelNumber - 1) % EducationalMessages.Length],
            BaseCampaignSeed = PilotBaseSeed,
            // Part S's retry policy: larger grid tiers get a larger attempt
            // budget, since a bigger board's graph/uniqueness search space is
            // harder to satisfy on any single attempt.
            MaxAttempts = profile.GridWidth <= 5 ? 500 : 1000
        };
    }
}
