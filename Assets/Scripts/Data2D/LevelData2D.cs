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
    }
}
