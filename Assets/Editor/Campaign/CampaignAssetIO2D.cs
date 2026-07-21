using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;

/// <summary>
/// Shared Editor-only asset load/create/save helpers for campaign level and
/// catalog assets - used by every Phase 8 command (migration, generation,
/// regeneration) so there is exactly one place that decides asset paths and
/// idempotent load-or-create behavior, instead of each command re-implementing
/// its own copy.
/// </summary>
public static class CampaignAssetIO2D
{
    public const string ResourcesFolder = "Assets/Resources";
    public const string LevelsFolder = "Assets/Resources/CampaignLevels";
    public const string CatalogAssetPath = "Assets/Resources/CampaignLevelCatalog2D.asset";

    public static string LevelAssetPath(int levelNumber) => $"{LevelsFolder}/Level_{levelNumber:000}.asset";

    /// <summary>Loads the definition asset already saved for this level number, or null if none exists yet - never creates one (callers that must create use LoadOrCreateDefinitionAsset).</summary>
    public static CampaignLevelDefinition2D LoadDefinitionAsset(int levelNumber)
    {
        return AssetDatabase.LoadAssetAtPath<CampaignLevelDefinition2D>(LevelAssetPath(levelNumber));
    }

    /// <summary>Idempotent: returns the existing asset at this level's fixed path if present, otherwise creates a brand-new empty one there. Never creates a second asset for the same level number.</summary>
    public static CampaignLevelDefinition2D LoadOrCreateDefinitionAsset(int levelNumber)
    {
        EnsureFolder(LevelsFolder);

        var existing = LoadDefinitionAsset(levelNumber);
        if (existing != null)
        {
            return existing;
        }

        var created = ScriptableObject.CreateInstance<CampaignLevelDefinition2D>();
        AssetDatabase.CreateAsset(created, LevelAssetPath(levelNumber));
        return created;
    }

    /// <summary>Copies every field from source onto target in place (so an existing asset's identity/GUID/inbound references are preserved across regeneration) rather than replacing the object reference.</summary>
    public static void CopyInto(CampaignLevelDefinition2D target, CampaignLevelDefinition2D source)
    {
        target.stableLevelId = source.stableLevelId;
        target.levelNumber = source.levelNumber;
        target.displayName = source.displayName;
        target.chapterNumber = source.chapterNumber;
        target.gridWidth = source.gridWidth;
        target.gridHeight = source.gridHeight;
        target.sourceCell = source.sourceCell;
        target.sourceOutputDirection = source.sourceOutputDirection;
        target.targetCell = source.targetCell;
        target.targetEntryDirection = source.targetEntryDirection;
        target.deterministicSeed = source.deterministicSeed;
        target.generatorVersion = source.generatorVersion;
        target.difficultyTier = source.difficultyTier;
        target.educationalMessage = source.educationalMessage;
        target.twoStarScore = source.twoStarScore;
        target.threeStarScore = source.threeStarScore;
        target.minimumRequiredTaps = source.minimumRequiredTaps;
        target.solvedReachablePipeCount = source.solvedReachablePipeCount;
        target.flowWaveCount = source.flowWaveCount;
        target.solutionCount = source.solutionCount;
        target.branchCount = source.branchCount;
        target.cycleCount = source.cycleCount;
        target.difficultyScore = source.difficultyScore;
        target.contentHash = source.contentHash;
        target.pipes = source.pipes;
    }

    public static CampaignLevelCatalog2D LoadOrCreateCatalogAsset()
    {
        EnsureFolder(ResourcesFolder);

        var existing = AssetDatabase.LoadAssetAtPath<CampaignLevelCatalog2D>(CatalogAssetPath);
        if (existing != null)
        {
            return existing;
        }

        var created = ScriptableObject.CreateInstance<CampaignLevelCatalog2D>();
        AssetDatabase.CreateAsset(created, CatalogAssetPath);
        return created;
    }

    /// <summary>Grows catalog.levels to at least levelNumber entries (padding new slots with null) and assigns definition at index (levelNumber - 1) - never shrinks or reorders existing entries.</summary>
    public static void AssignCatalogSlot(CampaignLevelCatalog2D catalog, int levelNumber, CampaignLevelDefinition2D definition)
    {
        while (catalog.levels.Count < levelNumber)
        {
            catalog.levels.Add(null);
        }
        catalog.levels[levelNumber - 1] = definition;
    }

    public static void EnsureFolder(string fullFolderPath)
    {
        if (AssetDatabase.IsValidFolder(fullFolderPath))
        {
            return;
        }

        int lastSlash = fullFolderPath.LastIndexOf('/');
        string parent = fullFolderPath.Substring(0, lastSlash);
        string leaf = fullFolderPath.Substring(lastSlash + 1);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
