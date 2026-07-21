using UnityEngine;

namespace YagmurRotasi2D.Core2D
{
    public enum Direction2D
    {
        Up,
        Right,
        Down,
        Left
    }

    public static class Direction2DExtensions
    {
        public static Vector2Int ToVector(this Direction2D direction)
        {
            switch (direction)
            {
                case Direction2D.Up: return new Vector2Int(0, 1);
                case Direction2D.Right: return new Vector2Int(1, 0);
                case Direction2D.Down: return new Vector2Int(0, -1);
                case Direction2D.Left: return new Vector2Int(-1, 0);
                default: return Vector2Int.zero;
            }
        }

        public static Direction2D Opposite(this Direction2D direction)
        {
            switch (direction)
            {
                case Direction2D.Up: return Direction2D.Down;
                case Direction2D.Down: return Direction2D.Up;
                case Direction2D.Right: return Direction2D.Left;
                case Direction2D.Left: return Direction2D.Right;
                default: return direction;
            }
        }

        /// <summary>
        /// Converts a world/board direction into a pipe's LOCAL direction - the
        /// same direction as seen in its canonical rotationIndex=0 frame, before
        /// PipeTile2D.ApplyRotation()'s root rotation is applied. This enum's own
        /// declaration order (Up, Right, Down, Left) already IS one 90-degree
        /// clockwise step, exactly matching ApplyRotation()'s -rotationIndex*90
        /// root rotation and every direction table in PipeTile2D (verified:
        /// CornerDirections[0] {Up,Right} rotated by +1 index step consistently
        /// reproduces CornerDirections[1..3], and the same holds for
        /// TeeDirections) - so converting world back to local is exact integer
        /// arithmetic on the enum's own values, never
        /// Transform.InverseTransformDirection (which would introduce needless
        /// floating-point comparison for a value that is always an exact
        /// multiple of 90 degrees).
        /// </summary>
        public static Direction2D ToLocalDirection(this Direction2D worldDirection, int rotationIndex)
        {
            int local = (((int)worldDirection - rotationIndex) % 4 + 4) % 4;
            return (Direction2D)local;
        }
    }
}
