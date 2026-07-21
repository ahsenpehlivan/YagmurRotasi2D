using System;
using UnityEngine;
using YagmurRotasi2D.Core2D;

namespace YagmurRotasi2D.Data2D
{
    [Serializable]
    public class PipeSpawnData2D
    {
        public PipeType2D pipeType;
        public Vector2Int gridPos;

        /// <summary>The deliberately-scrambled player-facing rotation this pipe actually spawns with. LevelManager2D.SpawnPipe feeds this - and only this - into PipeTile2D.Initialize().</summary>
        public int startRotationIndex;

        /// <summary>
        /// The correct logical rotationIndex for this pipe in the completed
        /// network - production level metadata only. Never applied at spawn
        /// time (gameplay always uses startRotationIndex); used by editor
        /// validation tooling (BranchingSolverTestRunner, ProductionBranching
        /// LevelsValidator) and reserved for future hint/solution-verification
        /// features. For Cross this is always 0 (Cross is non-rotatable and
        /// always fully open regardless of rotationIndex).
        /// </summary>
        public int solvedRotationIndex;

        /// <summary>Visual-only; ignored by the solver and by Corner pipes. Both variants use identical Straight connection rules.</summary>
        public StraightVisualVariant2D straightVisualVariant;

        public PipeSpawnData2D(
            PipeType2D pipeType, Vector2Int gridPos, int startRotationIndex, int solvedRotationIndex,
            StraightVisualVariant2D straightVisualVariant = StraightVisualVariant2D.Wide)
        {
            this.pipeType = pipeType;
            this.gridPos = gridPos;
            this.startRotationIndex = startRotationIndex;
            this.solvedRotationIndex = solvedRotationIndex;
            this.straightVisualVariant = straightVisualVariant;
        }
    }
}
