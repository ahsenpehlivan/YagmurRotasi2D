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

        /// <summary>
        /// Phase 9I: GridCells/Pipes/SourceTarget's own container transforms
        /// are deliberately kept at a manually-verified visual scale smaller
        /// than 1 (see LevelManager2D/GameSceneWebLayoutBuilder2D - both
        /// build (VisualPackingScale, VisualPackingScale, 1) container
        /// scales from THIS constant, the single source of truth) so their
        /// CONTENTS render smaller. But every spawned cell/pipe/Source/Target
        /// is placed via Instantiate(prefab, worldPos, ...), which sets
        /// WORLD position directly and independently of the parent
        /// container's own scale - left alone, that meant cell/pipe centers
        /// stayed spaced at the FULL, un-shrunk CellSize while their
        /// rendered size shrank by the container's scale, causing visible
        /// gaps between cells. GridToWorld() below applies this SAME ratio
        /// to its spacing calculation so spacing and rendered size shrink in
        /// lockstep - CellSize's own reported VALUE is intentionally left
        /// untouched (grid bounds/collider/other consumers that need the
        /// "logical" cell size are unaffected); only the placement math is
        /// adjusted.
        /// </summary>
        public const float VisualPackingScale = 0.8f;

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
        ///
        /// Phase 9F root-cause fix: this used to be `transform.position +
        /// Bounds.CellToLocalPosition(...)` - a raw Vector3 addition that
        /// mixed a WORLD-space point (transform.position, which already
        /// reflects BoardFitContainer's fitScale via the parent chain) with
        /// an UNSCALED local offset (cell*cellSize, never multiplied by
        /// fitScale). Every spawned cell/pipe/Source/Target's own RENDERED
        /// SIZE correctly scales with fitScale (localScale = cellSize * ...
        /// combined with the parent's lossyScale = fitScale), but the raw
        /// addition here left the SPACING between them frozen at the
        /// unscaled cellSize - the two only matched by coincidence at
        /// fitScale ~= 1. Enlarging the board (Phase 9D) pushed fitScale
        /// well above 1, so each cell's world size grew far past the still-
        /// fixed spacing between cell centers - the exact, sole cause of the
        /// reported grid/pipe overlap. transform.TransformPoint() applies
        /// BoardManager2D's own local-to-world matrix (including the
        /// inherited fitScale) to the offset before adding it, so spacing
        /// and rendered size now scale in lockstep at any fitScale - single
        /// source of truth is cellSize, as required.
        ///
        /// Phase 9I addendum: the same class of mismatch reappeared one
        /// level down. GridCells/Pipes/SourceTarget's own container scale
        /// (VisualPackingScale, see its doc comment) shrinks what's rendered
        /// INSIDE them, but Instantiate(prefab, worldPos, ...) sets world
        /// position directly, independent of that container's scale - so
        /// spacing stayed at the un-shrunk cellSize while rendered size
        /// shrank, reopening a gap. Multiplying by VisualPackingScale here
        /// closes it the same way: spacing and rendered size shrink in
        /// lockstep again, now including the container's own scale too.
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return transform.TransformPoint(Bounds.CellToLocalPosition(gridPos, cellSize * VisualPackingScale));
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
