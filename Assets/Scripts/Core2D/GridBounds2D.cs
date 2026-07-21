using System.Collections.Generic;
using UnityEngine;

namespace YagmurRotasi2D.Core2D
{
    /// <summary>
    /// Centered-coordinate grid bounds - (0,0) is always the middle cell,
    /// matching the exact convention BoardManager2D/LevelData2D already use
    /// for the fixed 5x5 board (Level 1's Source at (-2,2) is precisely
    /// TopLeft of a 5x5 GridBounds2D; Target at (2,-2) is precisely
    /// BottomRight - verified below). Replaces ad-hoc +/-2 offsets and
    /// hardcoded 5x5 loops so BoardManager2D and the level generator work
    /// identically for any width/height from 5x5 up to 10x10.
    /// </summary>
    public readonly struct GridBounds2D
    {
        public int Width { get; }
        public int Height { get; }

        public GridBounds2D(int width, int height)
        {
            Width = width;
            Height = height;
        }

        private int OffsetX => (Width - 1) / 2;
        private int OffsetY => (Height - 1) / 2;

        public Vector2Int MinCell => new Vector2Int(-OffsetX, -OffsetY);
        public Vector2Int MaxCell => new Vector2Int(Width - 1 - OffsetX, Height - 1 - OffsetY);

        /// <summary>Top-left in screen/world terms (min X, max Y) - the established Source corner.</summary>
        public Vector2Int TopLeft => new Vector2Int(MinCell.x, MaxCell.y);

        /// <summary>Bottom-right in screen/world terms (max X, min Y) - the established Target corner.</summary>
        public Vector2Int BottomRight => new Vector2Int(MaxCell.x, MinCell.y);

        public bool Contains(Vector2Int cell)
        {
            Vector2Int index = CellToArrayIndex(cell);
            return index.x >= 0 && index.x < Width && index.y >= 0 && index.y < Height;
        }

        public Vector2Int CellToArrayIndex(Vector2Int cell)
        {
            return new Vector2Int(cell.x + OffsetX, cell.y + OffsetY);
        }

        public Vector2Int ArrayIndexToCell(Vector2Int index)
        {
            return new Vector2Int(index.x - OffsetX, index.y - OffsetY);
        }

        /// <summary>Position relative to the board's own origin, in board-local units - multiply by the desired world cell size and add the board Transform's position/TransformPoint to place it in the scene.</summary>
        public Vector3 CellToLocalPosition(Vector2Int cell, float cellSize)
        {
            return new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);
        }

        public IEnumerable<Vector2Int> AllCells()
        {
            for (int x = MinCell.x; x <= MaxCell.x; x++)
            {
                for (int y = MinCell.y; y <= MaxCell.y; y++)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }
    }
}
