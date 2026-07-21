using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;

// A plain assembly reference only grants access to PUBLIC members - internal
// members of Assembly-CSharp are NOT automatically visible to
// Assembly-CSharp-Editor without this. Needed so BranchingSolverTestRunner.cs
// (an Editor script) can call the internal LevelManager2D.BuildLevels() below
// without widening it to public.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace YagmurRotasi2D.Gameplay2D
{
    public class LevelManager2D : MonoBehaviour
    {
        [SerializeField] private BoardManager2D boardManager;
        [SerializeField] private ScoreManager2D scoreManager;
        [SerializeField] private GameObject pipeStraightWidePrefab;
        [SerializeField] private GameObject pipeStraightNarrowPrefab;
        [SerializeField] private GameObject pipeCornerPrefab;
        [SerializeField] private GameObject pipeTeePrefab;
        [SerializeField] private GameObject pipeCrossPrefab;
        [SerializeField] private GameObject sourcePrefab;
        [SerializeField] private GameObject targetPrefab;
        [SerializeField] private Transform pipesContainer;
        [SerializeField] private Transform sourceTargetContainer;

        private List<LevelData2D> levels;
        private int currentLevelIndex;
        private GameObject currentSourceInstance;
        private GameObject currentTargetInstance;
        private TargetFX2D currentTargetFX;

        private readonly List<PipeTile2D> activePipes = new List<PipeTile2D>();

        public Vector2Int SourceCell { get; private set; }
        public Direction2D SourceOutputDirection { get; private set; }
        public Vector2Int TargetCell { get; private set; }
        public Direction2D TargetInputDirection { get; private set; }
        public IReadOnlyList<PipeTile2D> ActivePipes => activePipes;

        public int CurrentLevelIndex => currentLevelIndex;
        public int LevelCount => levels.Count;
        public string CurrentLevelName => levels[currentLevelIndex].levelName;
        public string CurrentInfoText => levels[currentLevelIndex].infoText;
        public int CurrentTwoStarScore => levels[currentLevelIndex].twoStarScore;
        public int CurrentThreeStarScore => levels[currentLevelIndex].threeStarScore;
        public TargetFX2D CurrentTargetFX => currentTargetFX;

        /// <summary>Raised after a level finishes spawning (initial load, reload or next level).</summary>
        public event Action OnLevelLoaded;

        /// <summary>Raised whenever the player (not level setup) rotates a pipe.</summary>
        public event Action OnPlayerMoved;

        private void Awake()
        {
            levels = ResolveLevels();
        }

        private void Start()
        {
            // BuildGrid() is now called per-level from LoadLevel() (grid
            // dimensions can differ between levels as of Phase 8A) instead of
            // once here.

            // Phase 9A: a level explicitly picked in LevelSelectScene2D takes
            // priority (CampaignSession2D is in-memory-only navigation intent,
            // never persisted - see its own doc comment) - falls back to the
            // saved GameProgress2D.CurrentLevel (defaults safely to level 1 if
            // no progress exists yet) for any other entry path into this scene.
            // Clamped again here in case either value is ever out of range for
            // this scene's level list.
            int startIndex = CampaignSession2D.HasSelectedLevel
                ? Mathf.Clamp(CampaignSession2D.SelectedLevelNumber - 1, 0, levels.Count - 1)
                : Mathf.Clamp(GameProgress2D.CurrentLevel - 1, 0, levels.Count - 1);
            LoadLevel(startIndex);
        }

        public void ReloadCurrentLevel()
        {
            LoadLevel(currentLevelIndex);
        }

        public void LoadNextLevel()
        {
            LoadLevel(currentLevelIndex + 1);
        }

        public void LoadLevel(int index)
        {
            if (levels == null || levels.Count == 0)
                return;

            currentLevelIndex = ((index % levels.Count) + levels.Count) % levels.Count;
            LevelData2D data = levels[currentLevelIndex];

            ClearLevelObjects();
            scoreManager.ResetState();

            // Grid dimensions can differ between levels as of Phase 8A - resize
            // (reallocates the pipe grid and recomputes cellSize so the new
            // grid fits the same physical board area) and rebuild the visual
            // grid cells for this level's actual size. A 5x5 level always
            // yields cellSize=1, byte-identical to the original fixed board.
            boardManager.SetGridSize(data.gridWidth, data.gridHeight);
            boardManager.BuildGrid();

            SourceCell = data.sourcePos;
            SourceOutputDirection = data.sourceOutputDirection;
            TargetCell = data.targetPos;
            TargetInputDirection = data.targetInputDirection;

            // Scale relative to each prefab's OWN authored localScale, not
            // Vector3.one - BoardManager2D.CellSize is normalized so a 5x5
            // board (the original fixed board) always yields CellSize=1, so
            // multiplying the prefab's authored scale by CellSize reproduces
            // that exact pre-Phase-8A size at 5x5 and shrinks proportionally
            // for 6x6-10x10. The previous `Vector3.one * CellSize` discarded
            // the prefab's authored scale entirely (Source's 0.1, Target's
            // 0.2), which is why both markers rendered far too large once
            // variable grid sizes were introduced.
            Transform stParent = sourceTargetContainer != null ? sourceTargetContainer : transform;
            currentSourceInstance = Instantiate(sourcePrefab, boardManager.GridToWorld(SourceCell), Quaternion.identity, stParent);
            currentSourceInstance.transform.localScale = sourcePrefab.transform.localScale * boardManager.CellSize;
            currentTargetInstance = Instantiate(targetPrefab, boardManager.GridToWorld(TargetCell), Quaternion.identity, stParent);
            currentTargetInstance.transform.localScale = targetPrefab.transform.localScale * boardManager.CellSize;
            currentTargetFX = currentTargetInstance.GetComponent<TargetFX2D>();

            foreach (PipeSpawnData2D spawn in data.pipes)
            {
                SpawnPipe(spawn);
            }

            OnLevelLoaded?.Invoke();
        }

        /// <summary>
        /// Phase 8 Fast-Track QA: campaign-specific solved-orientation check,
        /// separate from FlowSolver2D's connectivity/leak check. Compares every
        /// active pipe's CURRENT logical port mask (its current rotationIndex)
        /// against the level asset's STORED solved port mask (solvedRotationIndex)
        /// via PipeTile2D.GetPortMask - logical open-direction sets, not raw
        /// rotationIndex, so Straight's two-fold symmetry (0≈2, 1≈3) and Cross's
        /// single logical orientation are handled for free by the port-mask
        /// tables themselves (see PipeTile2D.GetPortMask's own doc comment) with
        /// no special-casing needed here.
        ///
        /// This exists because FlowSolver2D.Solve() only proves the CURRENT
        /// rotation state forms a connected, leak-free Source-to-Target route -
        /// it has no notion of "the intended solved layout" at all, so a
        /// different-but-still-valid rotation combination (most easily reached
        /// via Straight's symmetry, or an alternate routing through a
        /// branching/cyclic network) can satisfy it without matching the
        /// level's designed solution.
        /// </summary>
        public bool ValidateCurrentMatchesSolved(out int mismatchCount, out Vector2Int firstMismatchCell, out int firstMismatchCurrentMask, out int firstMismatchExpectedMask)
        {
            mismatchCount = 0;
            firstMismatchCell = Vector2Int.zero;
            firstMismatchCurrentMask = 0;
            firstMismatchExpectedMask = 0;

            var solvedByCell = new Dictionary<Vector2Int, PipeSpawnData2D>();
            foreach (PipeSpawnData2D spawn in levels[currentLevelIndex].pipes)
            {
                solvedByCell[spawn.gridPos] = spawn;
            }

            bool firstMismatchRecorded = false;
            foreach (PipeTile2D pipe in activePipes)
            {
                if (!solvedByCell.TryGetValue(pipe.GridPosition, out PipeSpawnData2D spawn))
                {
                    // Defensive only - every active pipe was spawned FROM this
                    // exact level's pipe list, so its coordinate always has a
                    // matching entry.
                    continue;
                }

                int currentMask = pipe.GetCanonicalPortMask();
                int expectedMask = PipeTile2D.GetPortMask(spawn.pipeType, spawn.solvedRotationIndex);

                if (currentMask != expectedMask)
                {
                    mismatchCount++;
                    if (!firstMismatchRecorded)
                    {
                        firstMismatchRecorded = true;
                        firstMismatchCell = pipe.GridPosition;
                        firstMismatchCurrentMask = currentMask;
                        firstMismatchExpectedMask = expectedMask;
                    }
                }
            }

            return mismatchCount == 0;
        }

        private void ClearLevelObjects()
        {
            foreach (PipeTile2D pipe in activePipes)
            {
                if (pipe == null)
                    continue;

                // Disable immediately so the tile stops accepting input/raycasts this
                // frame; Destroy() itself only takes effect at the end of the frame.
                pipe.gameObject.SetActive(false);
                Destroy(pipe.gameObject);
            }

            activePipes.Clear();
            boardManager.ClearPipes();

            if (currentSourceInstance != null)
            {
                currentSourceInstance.SetActive(false);
                Destroy(currentSourceInstance);
                currentSourceInstance = null;
            }

            if (currentTargetInstance != null)
            {
                currentTargetInstance.SetActive(false);
                Destroy(currentTargetInstance);
                currentTargetInstance = null;
            }

            currentTargetFX = null;
        }

        private void SpawnPipe(PipeSpawnData2D spawn)
        {
            GameObject prefab = GetPrefabFor(spawn);
            Transform parent = pipesContainer != null ? pipesContainer : transform;

            GameObject instance = Instantiate(prefab, boardManager.GridToWorld(spawn.gridPos), Quaternion.identity, parent);
            instance.transform.localScale = Vector3.one * boardManager.CellSize;
            PipeTile2D pipe = instance.GetComponent<PipeTile2D>();
            pipe.Initialize(spawn.pipeType, spawn.startRotationIndex, spawn.gridPos);
            pipe.OnPlayerRotated += HandlePipeRotatedByPlayer;

            boardManager.SetPipe(spawn.gridPos, pipe);
            activePipes.Add(pipe);
        }

        /// <summary>straightVisualVariant only picks which visual prefab to spawn for Straight; ignored by every other type.</summary>
        private GameObject GetPrefabFor(PipeSpawnData2D spawn)
        {
            switch (spawn.pipeType)
            {
                case PipeType2D.Straight:
                    return spawn.straightVisualVariant == StraightVisualVariant2D.Narrow
                        ? pipeStraightNarrowPrefab
                        : pipeStraightWidePrefab;
                case PipeType2D.Tee:
                    return pipeTeePrefab;
                case PipeType2D.Cross:
                    return pipeCrossPrefab;
                case PipeType2D.Corner:
                default:
                    return pipeCornerPrefab;
            }
        }

        private void HandlePipeRotatedByPlayer()
        {
            scoreManager.RegisterMove();
            OnPlayerMoved?.Invoke();
        }

        /// <summary>Internal/static so the Assembly-CSharp-Editor validation harnesses (BranchingSolverTestRunner, ProductionBranchingLevelsValidator) and ProductionLevelCount below can use the exact production level data without needing a scene or a LevelManager2D instance. Not exposed publicly - visible to Assembly-CSharp-Editor only via the InternalsVisibleTo declaration above.</summary>
        internal static List<LevelData2D> BuildLevels()
        {
            var list = new List<LevelData2D>();

            // All levels now start at the top-left grid corner and end at the
            // bottom-right grid corner (Cloud2D/RainLoopFX in the scene already sit
            // above (-2, 2), so this aligns the gameplay source with the visual rain).
            list.Add(new LevelData2D
            {
                levelName = "Üstten Aşağı Rota",
                gridWidth = 5,
                gridHeight = 5,
                sourcePos = new Vector2Int(-2, 2),
                sourceOutputDirection = Direction2D.Right,
                targetPos = new Vector2Int(2, -2),
                targetInputDirection = Direction2D.Up,
                pipes = new List<PipeSpawnData2D>
                {
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(-1, 2), 0, 0, StraightVisualVariant2D.Wide),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(0, 2), 1, 0, StraightVisualVariant2D.Narrow),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(1, 2), 0, 0, StraightVisualVariant2D.Wide),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, 2), 0, 2),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(2, 1), 1, 1, StraightVisualVariant2D.Narrow),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(2, 0), 0, 1, StraightVisualVariant2D.Wide),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(2, -1), 1, 1, StraightVisualVariant2D.Narrow)
                },
                infoText = "Yağmur suyu doğru borularla yönlendirilirse boşa akmadan ihtiyaç duyulan alana ulaşabilir.",
                twoStarScore = 150,
                threeStarScore = 200
            });

            list.Add(new LevelData2D
            {
                levelName = "Kıvrımlı Su Yolu",
                gridWidth = 5,
                gridHeight = 5,
                sourcePos = new Vector2Int(-2, 2),
                sourceOutputDirection = Direction2D.Right,
                targetPos = new Vector2Int(2, -2),
                targetInputDirection = Direction2D.Up,
                pipes = new List<PipeSpawnData2D>
                {
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(-1, 2), 0, 2),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(-1, 1), 0, 0),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(0, 1), 1, 0, StraightVisualVariant2D.Narrow),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 1), 1, 2),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(1, 0), 1, 1, StraightVisualVariant2D.Wide),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, -1), 3, 0),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, -1), 2, 2)
                },
                infoText = "Suyun planlı bir rotadan taşınması, parkların ve yeşil alanların sulanmasına yardımcı olur.",
                twoStarScore = 170,
                threeStarScore = 220
            });

            list.Add(new LevelData2D
            {
                levelName = "Uzun Tasarruf Rotası",
                gridWidth = 5,
                gridHeight = 5,
                sourcePos = new Vector2Int(-2, 2),
                sourceOutputDirection = Direction2D.Right,
                targetPos = new Vector2Int(2, -2),
                targetInputDirection = Direction2D.Up,
                pipes = new List<PipeSpawnData2D>
                {
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(-1, 2), 0, 0, StraightVisualVariant2D.Wide),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(0, 2), 0, 2),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(0, 1), 1, 3),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(-1, 1), 1, 0, StraightVisualVariant2D.Narrow),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(-2, 1), 1, 1),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(-2, 0), 2, 0),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(-1, 0), 0, 0, StraightVisualVariant2D.Wide),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(0, 0), 1, 0, StraightVisualVariant2D.Narrow),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(1, 0), 0, 0, StraightVisualVariant2D.Wide),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, 0), 2, 2),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(2, -1), 0, 1, StraightVisualVariant2D.Narrow)
                },
                infoText = "Yağmur suyunu biriktirmek ve doğru yere taşımak, kurak dönemlerde su tasarrufu sağlar.",
                twoStarScore = 170,
                threeStarScore = 220
            });

            // Phase 7F.4: Tee/Cross production test levels, appended after
            // Level 3 (Levels 1-3 above are byte-for-byte unchanged). Every
            // pipe's startRotationIndex below is the deliberately-scrambled
            // player-facing spawn state - NOT the solved state - matching the
            // exact same convention as Levels 1-3. Each level's solved route
            // was hand-derived and verified (every reciprocal edge and every
            // pipe's full port usage checked) before being encoded here; the
            // startRotationIndex values are then chosen to differ from the
            // solved rotation for a meaningful subset of pieces.
            list.Add(new LevelData2D
            {
                levelName = "Üç Kollu Bahçe",
                gridWidth = 5,
                gridHeight = 5,
                sourcePos = new Vector2Int(-2, 2),
                sourceOutputDirection = Direction2D.Right,
                targetPos = new Vector2Int(2, -2),
                targetInputDirection = Direction2D.Up,
                pipes = new List<PipeSpawnData2D>
                {
                    // Entry, pre-solved.
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(-1, 2), 0, 0, StraightVisualVariant2D.Wide),
                    // Tee A: splits into two branches (Right, Down). Solved index 2 (Right+Down+Left); starts at 0 (2 taps).
                    new PipeSpawnData2D(PipeType2D.Tee, new Vector2Int(0, 2), 0, 2),
                    // Branch 1 (via the Right split).
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 2), 0, 2),
                    // Branch 2 (via the Down split).
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(0, 1), 2, 0),
                    // Tee B: merges both branches (Up, Left) and continues down (Down). Solved index 3; starts at 1 (2 taps).
                    new PipeSpawnData2D(PipeType2D.Tee, new Vector2Int(1, 1), 1, 3),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(1, 0), 0, 1, StraightVisualVariant2D.Narrow),
                    // Pre-solved approach into Target.
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, -1), 0, 0),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, -1), 2, 2)
                },
                infoText = "Üç kollu borular suyu birden fazla yöne ayırır. Açık kalan her kol mutlaka bağlanmalıdır.",
                twoStarScore = 180,
                threeStarScore = 230
            });

            list.Add(new LevelData2D
            {
                levelName = "Dört Yönlü Kavşak",
                gridWidth = 5,
                gridHeight = 5,
                sourcePos = new Vector2Int(-2, 2),
                sourceOutputDirection = Direction2D.Right,
                targetPos = new Vector2Int(2, -2),
                targetInputDirection = Direction2D.Up,
                pipes = new List<PipeSpawnData2D>
                {
                    // Entry, pre-solved.
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(-1, 2), 2, 2),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(-1, 1), 1, 1, StraightVisualVariant2D.Wide),
                    // Cross A (interior cell, not a board edge). Always fully open, never rotates.
                    new PipeSpawnData2D(PipeType2D.Cross, new Vector2Int(-1, 0), 0, 0),
                    // Left arc closing Cross A's Left+Down ports.
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(-2, 0), 3, 1),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(-2, -1), 2, 0),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(-1, -1), 1, 3),
                    // Cross B (interior cell). Always fully open, never rotates.
                    new PipeSpawnData2D(PipeType2D.Cross, new Vector2Int(0, 0), 0, 0),
                    // Upper-right arc closing Cross B's Up+Right ports.
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(0, 1), 1, 1),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 1), 0, 2),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 0), 1, 3),
                    // Exit chain to Target, pre-solved.
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(0, -1), 0, 0),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(1, -1), 1, 0, StraightVisualVariant2D.Narrow),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, -1), 2, 2)
                },
                infoText = "Dört yönlü kavşak borusu suyu her yöne dağıtır ve döndürülemez.",
                twoStarScore = 220,
                threeStarScore = 280
            });

            list.Add(new LevelData2D
            {
                levelName = "Yağmur Dağıtım Ağı",
                gridWidth = 5,
                gridHeight = 5,
                sourcePos = new Vector2Int(-2, 2),
                sourceOutputDirection = Direction2D.Right,
                targetPos = new Vector2Int(2, -2),
                targetInputDirection = Direction2D.Up,
                pipes = new List<PipeSpawnData2D>
                {
                    // Entry, pre-solved.
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(-1, 2), 0, 0, StraightVisualVariant2D.Wide),
                    // Tee 1: splits into two branches (Right, Down). Solved index 2; starts at 0 (2 taps).
                    new PipeSpawnData2D(PipeType2D.Tee, new Vector2Int(0, 2), 0, 2),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, 2), 2, 2),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(0, 1), 0, 0),
                    // Cross: merges both branches (Up, Left), then splits again into two new branches (Right, Down).
                    new PipeSpawnData2D(PipeType2D.Cross, new Vector2Int(1, 1), 0, 0),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(2, 1), 0, 2),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(2, 0), 0, 1, StraightVisualVariant2D.Narrow),
                    new PipeSpawnData2D(PipeType2D.Straight, new Vector2Int(1, 0), 1, 1, StraightVisualVariant2D.Wide),
                    new PipeSpawnData2D(PipeType2D.Corner, new Vector2Int(1, -1), 2, 0),
                    // Tee 2: merges both of the Cross's branches (Up, Left) and continues down to Target. Solved index 3; starts at 1 (2 taps).
                    new PipeSpawnData2D(PipeType2D.Tee, new Vector2Int(2, -1), 1, 3)
                },
                infoText = "Tam bir yağmur suyu ağı, sızıntı olmadan birçok alana su taşıyabilir.",
                twoStarScore = 200,
                threeStarScore = 250
            });

            return list;
        }

        /// <summary>
        /// Fixed Resources path (Assets/Resources/CampaignLevelCatalog2D.asset)
        /// so the catalog can be found identically from Editor tooling and from
        /// a built player, with no per-scene serialized field to forget to
        /// wire. Public so Editor migration/generation tools can save/verify
        /// the asset at the exact path this reads from.
        /// </summary>
        public const string CatalogResourcePath = "CampaignLevelCatalog2D";

        /// <summary>
        /// The real, currently-active production level list: the campaign
        /// catalog (Assets/Resources/CampaignLevelCatalog2D.asset) once a
        /// successfully-migrated/generated catalog exists and is non-empty,
        /// otherwise the original hardcoded BuildLevels() as a safe fallback.
        /// Internal/static for the same reason as BuildLevels() below - Editor
        /// validation tooling needs to validate exactly what runtime will
        /// actually load.
        /// </summary>
        internal static List<LevelData2D> ResolveLevels()
        {
            CampaignLevelCatalog2D catalog = Resources.Load<CampaignLevelCatalog2D>(CatalogResourcePath);
            if (catalog != null && catalog.levels != null && catalog.levels.Count > 0)
            {
                return catalog.ToLevelDataList();
            }

            return BuildLevels();
        }

        /// <summary>The real, currently-active production level count (catalog if present, else BuildLevels()) - used by GameProgress2D so clamping/unlock logic never needs a second hardcoded level count. Not cached: both sources are small, cheap, pure data builds.</summary>
        public static int ProductionLevelCount => ResolveLevels().Count;
    }
}
