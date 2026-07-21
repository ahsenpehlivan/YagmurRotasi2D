using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Core2D;

namespace YagmurRotasi2D.Gameplay2D
{
    public class BoardManager2D : MonoBehaviour
    {
        [SerializeField] private GameObject gridCellPrefab;
        [SerializeField] private Transform gridCellsContainer;
        [SerializeField] private int width = 5;
        [SerializeField] private int height = 5;
        [SerializeField] private float cellSize = 1f;

        /// <summary>
        /// The world-space span the manually-positioned board art (wooden grid
        /// background, camera framing) was already built around - the original
        /// fixed 5x5 board at cellSize=1 spans exactly this many units in each
        /// axis. Deriving cellSize from this reference for every grid size means
        /// 5x5 always yields cellSize=1 (byte-identical to the pre-Phase-8A
        /// board), while 6x6 through 10x10 shrink proportionally to fit the same
        /// physical area instead of overflowing it.
        /// </summary>
        public const float ReferenceBoardWorldSize = 5f;

        private PipeTile2D[,] pipes;
        private readonly List<GameObject> spawnedGridCells = new List<GameObject>();

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public GridBounds2D Bounds => new GridBounds2D(width, height);

        private void Awake()
        {
            InitializeGrid();
        }

        /// <summary>
        /// (Re)allocates the internal pipe grid from the current width/height.
        /// Called automatically by Awake() during normal gameplay. Editor-only
        /// validation fixtures that construct a BoardManager2D outside the
        /// normal scene-load lifecycle (where MonoBehaviour Awake() timing
        /// cannot be relied upon) must call this explicitly before any
        /// SetPipe/GetPipe/IsInsideGrid/BuildGrid call.
        /// </summary>
        public void InitializeGrid()
        {
            pipes = new PipeTile2D[width, height];
        }

        /// <summary>
        /// Resizes the board for a level whose dimensions differ from the last
        /// one loaded - reallocates the internal pipe grid and recomputes
        /// cellSize from ReferenceBoardWorldSize so the new grid fits the same
        /// physical board area the original fixed 5x5 board occupied. Does not
        /// touch the visual grid cells itself - call BuildGrid() afterward (it
        /// clears and rebuilds them for the new dimensions/cellSize).
        /// </summary>
        public void SetGridSize(int newWidth, int newHeight)
        {
            width = Mathf.Max(1, newWidth);
            height = Mathf.Max(1, newHeight);
            cellSize = ReferenceBoardWorldSize / Mathf.Max(width, height);
            InitializeGrid();
        }

        /// <summary>Clears any previously-built grid cells (safe to call repeatedly - e.g. across levels with different dimensions) and instantiates one per current Bounds cell, each scaled to the current cellSize.</summary>
        public void BuildGrid()
        {
            ClearGridCells();

            if (gridCellPrefab == null)
                return;

            Transform parent = gridCellsContainer != null ? gridCellsContainer : transform;

            foreach (Vector2Int cell in Bounds.AllCells())
            {
                Vector3 worldPos = GridToWorld(cell);
                GameObject instance = Instantiate(gridCellPrefab, worldPos, Quaternion.identity, parent);
                instance.transform.localScale = Vector3.one * cellSize;
                spawnedGridCells.Add(instance);
            }
        }

        private void ClearGridCells()
        {
            foreach (GameObject cell in spawnedGridCells)
            {
                if (cell != null)
                {
                    cell.SetActive(false);
                    Destroy(cell);
                }
            }
            spawnedGridCells.Clear();
        }

        /// <summary>
        /// Grid coordinates are centered on the board (e.g. (0,0) is the middle cell),
        /// matching the coordinates used directly in LevelData2D.
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return transform.position + Bounds.CellToLocalPosition(gridPos, cellSize);
        }

        public bool IsInsideGrid(Vector2Int gridPos)
        {
            return Bounds.Contains(gridPos);
        }

        public void SetPipe(Vector2Int gridPos, PipeTile2D pipe)
        {
            if (!IsInsideGrid(gridPos))
                return;

            Vector2Int index = Bounds.CellToArrayIndex(gridPos);
            pipes[index.x, index.y] = pipe;
        }

        public PipeTile2D GetPipe(Vector2Int gridPos)
        {
            if (!IsInsideGrid(gridPos))
                return null;

            Vector2Int index = Bounds.CellToArrayIndex(gridPos);
            return pipes[index.x, index.y];
        }

        public void ClearPipes()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    pipes[x, y] = null;
                }
            }
        }
    }
}
