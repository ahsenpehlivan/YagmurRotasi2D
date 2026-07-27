using System;
using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Core2D;

namespace YagmurRotasi2D.Data2D
{
    [Serializable]
    public class LevelData2D
    {
        public string levelName;
        public int gridWidth;
        public int gridHeight;
        public Vector2Int sourcePos;
        public Direction2D sourceOutputDirection;
        public Vector2Int targetPos;
        public Direction2D targetInputDirection;
        public List<PipeSpawnData2D> pipes;
        public string infoText;
        public int twoStarScore = 170;
        public int threeStarScore = 220;

        /// <summary>
        /// Optional per-level override for ScoreManager2D's move-based star
        /// thresholds. False by default for every hand-authored and generated
        /// level - ScoreManager2D.CalculateAutomaticStarLimits (derived from
        /// this level's own optimalMoves) is always the default; these three
        /// fields only take effect when useManualStarLimits is explicitly set
        /// true, and only if manualThreeStarMoveLimit/manualTwoStarMoveLimit
        /// are themselves valid (see ScoreManager2D.ResolveStarLimits) -
        /// otherwise ScoreManager2D silently falls back to the automatic
        /// result with one development-only warning.
        /// </summary>
        public bool useManualStarLimits;
        public int manualThreeStarMoveLimit;
        public int manualTwoStarMoveLimit;
    }
}
