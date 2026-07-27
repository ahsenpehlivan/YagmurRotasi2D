using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;

namespace YagmurRotasi2D.Campaign2D
{
    /// <summary>
    /// One production campaign level, as a serialized asset instead of a hand-
    /// written entry in LevelManager2D.BuildLevels(). Reuses the exact same
    /// runtime data model (PipeSpawnData2D/Direction2D/PipeType2D) production
    /// gameplay already understands - ToLevelData() converts to the existing
    /// LevelData2D LevelManager2D already knows how to load, so no gameplay
    /// code needs a second level-representation to understand. Metrics fields
    /// are informational/reporting only (recomputed and re-verified by the
    /// batch validator, never trusted blindly at load time).
    /// </summary>
    [CreateAssetMenu(fileName = "CampaignLevel", menuName = "YagmurRotasi2D/Campaign Level Definition")]
    public class CampaignLevelDefinition2D : ScriptableObject
    {
        /// <summary>Sentinel for solutionCount meaning "exact uniqueness was intentionally not checked" (Phase 8 Fast-Track generation skips the expensive CampaignUniquenessSolver2D search) - distinct from a real computed value, which is always >= 1 for an accepted level.</summary>
        public const int SolutionCountNotChecked = -1;

        [Header("Identity")]
        public string stableLevelId;
        public int levelNumber;
        public string displayName;
        public int chapterNumber;

        [Header("Board")]
        public int gridWidth = 5;
        public int gridHeight = 5;
        public Vector2Int sourceCell;
        public Direction2D sourceOutputDirection;
        public Vector2Int targetCell;
        public Direction2D targetEntryDirection;

        [Header("Generation")]
        public int deterministicSeed;
        public string generatorVersion;
        public int difficultyTier;

        [Header("Presentation")]
        [TextArea(2, 4)]
        public string educationalMessage;
        public int twoStarScore;
        public int threeStarScore;

        [Header("Derived metrics (recomputed and re-verified by batch validation - never trusted blindly)")]
        public int minimumRequiredTaps;
        public int solvedReachablePipeCount;
        public int flowWaveCount;
        public int solutionCount;
        public int branchCount;
        public int cycleCount;
        public float difficultyScore;
        public string contentHash;

        [Header("Pipes")]
        public List<PipeSpawnData2D> pipes = new List<PipeSpawnData2D>();

        [Header("Manual star-limit override (optional - default OFF, see ScoreManager2D.ResolveStarLimits)")]
        [Tooltip("Deliberately NOT copied by CampaignAssetIO2D.CopyInto - a human-authored tuning decision on this saved asset, independent of and never overwritten by regeneration from a stored seed.")]
        public bool useManualStarLimits;
        public int manualThreeStarMoveLimit;
        public int manualTwoStarMoveLimit;

        public GridBounds2D Bounds => new GridBounds2D(gridWidth, gridHeight);

        /// <summary>Converts to the exact same LevelData2D LevelManager2D already loads from BuildLevels() - no second runtime representation.</summary>
        public LevelData2D ToLevelData()
        {
            return new LevelData2D
            {
                levelName = displayName,
                gridWidth = gridWidth,
                gridHeight = gridHeight,
                sourcePos = sourceCell,
                sourceOutputDirection = sourceOutputDirection,
                targetPos = targetCell,
                targetInputDirection = targetEntryDirection,
                pipes = new List<PipeSpawnData2D>(pipes),
                infoText = educationalMessage,
                twoStarScore = twoStarScore,
                threeStarScore = threeStarScore,
                useManualStarLimits = useManualStarLimits,
                manualThreeStarMoveLimit = manualThreeStarMoveLimit,
                manualTwoStarMoveLimit = manualTwoStarMoveLimit
            };
        }
    }
}
