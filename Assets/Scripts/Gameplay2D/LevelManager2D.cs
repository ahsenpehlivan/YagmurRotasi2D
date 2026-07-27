using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using YagmurRotasi2D.Audio2D;
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

        // Phase 9L: CloudAndRain/SuccessFXZone are no longer reachable from
        // BoardRoot at all - GameSceneWebLayoutBuilder2D now moves them to a
        // separate scene-level "IndependentWorldFXRoot" specifically so they
        // are never a descendant of BoardFitContainer/BoardRoot and can never
        // inherit GameplayBoardFitter2D's runtime scale/position changes.
        // That structural fix makes runtime enforcement of their transforms
        // unnecessary - the saved Edit Mode transforms are authoritative on
        // their own now, so this script no longer caches or writes to either
        // of them (the former ApplyAuthoredWeatherTransform/cloudAndRainCache
        // were removed entirely, not just repointed).

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

            // Per-level fair star thresholds (never one fixed global
            // threshold) - optimalMoves is recomputed fresh from THIS
            // level's own pipe data every load/reload, so a Reload never
            // reuses a stale value from a previous level.
            int optimalMoves = ScoreManager2D.CalculateOptimalMoves(data.pipes);
            scoreManager.ResetState(optimalMoves, data.useManualStarLimits, data.manualThreeStarMoveLimit, data.manualTwoStarMoveLimit);

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

            EnsureBoardTransformCorrections();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (currentLevelIndex == 0)
            {
                LogVisualScaleValidation();
            }
#endif

            OnLevelLoaded?.Invoke();
        }

        // Manually verified in the Unity Editor (Phase 9H) - authoritative,
        // not derived from any formula. Applied as ABSOLUTE assignments only
        // (never *=), so repeated level loads or builder reruns always
        // converge on exactly these numbers - never 0.49/0.343 compounding.
        // BoardContainerVisualScale is built from BoardManager2D.
        // VisualPackingScale (not a separate 0.7f literal) so the container
        // scale and GridToWorld's spacing (Phase 9I fix) can never drift
        // apart from each other again.
        private static readonly Vector3 BoardRootLocalPosition = new Vector3(-0.270000011f, -0.0799999982f, 0f);
        private static readonly Vector3 BoardContainerVisualScale = new Vector3(BoardManager2D.VisualPackingScale, BoardManager2D.VisualPackingScale, 1f);

        /// <summary>
        /// Reasserts BoardRoot's local offset (under BoardFitContainer) and
        /// GridCells/Pipes/SourceTarget's own container scale on every level
        /// load - defensive: BuildGrid()/SpawnPipe()/the Source&amp;Target
        /// Instantiate calls above only ever touch their SPAWNED INSTANCES'
        /// own transforms, never these containers' own transform, so this is
        /// currently a no-op after the builder has set them once. Kept as a
        /// safety net per spec rather than relying on that never changing.
        /// Derives BoardRoot from pipesContainer.parent and GridCells from
        /// BoardRoot.Find("GridCells") - no new serialized field needed.
        /// Never touches CloudAndRain/SuccessFXZone or anything under them -
        /// this method has no reference to either, by construction.
        /// </summary>
        private void EnsureBoardTransformCorrections()
        {
            if (pipesContainer == null || sourceTargetContainer == null)
                return;

            Transform boardRoot = pipesContainer.parent;
            if (boardRoot != null)
            {
                boardRoot.localPosition = BoardRootLocalPosition;

                Transform gridCells = boardRoot.Find("GridCells");
                if (gridCells != null)
                {
                    gridCells.localScale = BoardContainerVisualScale;
                }
            }

            pipesContainer.localScale = BoardContainerVisualScale;
            sourceTargetContainer.localScale = BoardContainerVisualScale;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Part D validation log (Level 1 only, Editor/Development Build only): reports the numbers the Phase 9E visual-scale fix and the Phase 9H manual transform correction are supposed to guarantee, so a regression is visible in the Console instead of only being caught by eye.</summary>
        private void LogVisualScaleValidation()
        {
            // Compares WORLD-space renderer bounds against a WORLD-space cell
            // size rather than the raw local CellSize - BoardFitContainer's
            // own screen-fit scale multiplies both the renderers and the
            // board uniformly, so dividing world bounds by a still-local
            // cell size would silently report the wrong ratio whenever the
            // board isn't at fitScale=1 (i.e. almost always). This cancels
            // that outer scale out and reports the TRUE authoring-time
            // visual-to-cell ratio (~PipeVisualRatio for pipes). Also
            // includes VisualPackingScale (Phase 9I) - the ACTUAL spacing
            // GridToWorld now places cells at, matching GridCells/Pipes/
            // SourceTarget's own container scale, not the raw un-shrunk
            // CellSize.
            float worldCellSize = boardManager.CellSize * BoardManager2D.VisualPackingScale * boardManager.transform.lossyScale.x;
            PipeTile2D firstPipe = activePipes.Count > 0 ? activePipes[0] : null;
            SpriteRenderer pipeRenderer = firstPipe != null ? firstPipe.GetComponentInChildren<SpriteRenderer>() : null;
            SpriteRenderer sourceRenderer = currentSourceInstance != null ? currentSourceInstance.GetComponentInChildren<SpriteRenderer>() : null;
            SpriteRenderer targetRenderer = currentTargetInstance != null ? currentTargetInstance.GetComponentInChildren<SpriteRenderer>() : null;

            string pipeSize = pipeRenderer != null ? $"{pipeRenderer.bounds.size.x:0.000}x{pipeRenderer.bounds.size.y:0.000}" : "n/a";
            string sourceSize = sourceRenderer != null ? $"{sourceRenderer.bounds.size.x:0.000}x{sourceRenderer.bounds.size.y:0.000}" : "n/a";
            string targetSize = targetRenderer != null ? $"{targetRenderer.bounds.size.x:0.000}x{targetRenderer.bounds.size.y:0.000}" : "n/a";
            float pipeRatio = pipeRenderer != null && worldCellSize > 0f ? Mathf.Max(pipeRenderer.bounds.size.x, pipeRenderer.bounds.size.y) / worldCellSize : 0f;

            Debug.Log($"LevelManager2D visual-scale validation (Level 1): cellWorldSize={worldCellSize:0.000}, " +
                $"pipeRenderer={pipeSize} (ratio={pipeRatio:0.00}), sourceRenderer={sourceSize}, targetRenderer={targetSize}.");

            Transform boardRoot = pipesContainer != null ? pipesContainer.parent : null;
            Transform gridCells = boardRoot != null ? boardRoot.Find("GridCells") : null;
            string boardRootPos = boardRoot != null ? $"({boardRoot.localPosition.x:0.000000000},{boardRoot.localPosition.y:0.000000000},{boardRoot.localPosition.z:0.000000000})" : "n/a";
            string gridCellsScale = gridCells != null ? $"({gridCells.localScale.x:0.0},{gridCells.localScale.y:0.0},{gridCells.localScale.z:0.0})" : "n/a";
            string pipesScale = pipesContainer != null ? $"({pipesContainer.localScale.x:0.0},{pipesContainer.localScale.y:0.0},{pipesContainer.localScale.z:0.0})" : "n/a";
            string sourceTargetScale = sourceTargetContainer != null ? $"({sourceTargetContainer.localScale.x:0.0},{sourceTargetContainer.localScale.y:0.0},{sourceTargetContainer.localScale.z:0.0})" : "n/a";

            Debug.Log($"LevelManager2D manual transform validation (Level 1): BoardRoot.localPosition={boardRootPos}, " +
                $"GridCells.localScale={gridCellsScale}, Pipes.localScale={pipesScale}, SourceTarget.localScale={sourceTargetScale}. " +
                $"Expected: BoardRoot=(-0.270000011,-0.079999998,0), GridCells/Pipes/SourceTarget=({BoardManager2D.VisualPackingScale:0.0},{BoardManager2D.VisualPackingScale:0.0},1) [derives from BoardManager2D.VisualPackingScale].");

            // Read-only, scene-wide lookup purely for this diagnostic line -
            // never cached, never written to. Phase 9L: SuccessFXZone/
            // CloudAndRain are no longer descendants of BoardRoot at all (see
            // GameSceneWebLayoutBuilder2D.EnsureIndependentWorldFXRoot) - they
            // now live under a separate scene-level "IndependentWorldFXRoot",
            // specifically so nothing in the BoardRoot/BoardFitContainer
            // hierarchy (including GameplayBoardFitter2D) can ever affect
            // them again. This script does not enforce any of their
            // transforms anymore; they are reported here only so a
            // regression in the SAVED SCENE is still visible in the Console.
            GameObject independentWorldFXRootGO = GameObject.Find("IndependentWorldFXRoot");
            Transform successFXZoneReadOnly = independentWorldFXRootGO != null ? independentWorldFXRootGO.transform.Find("SuccessFXZone") : null;
            Transform duckFXRootReadOnly = successFXZoneReadOnly != null ? successFXZoneReadOnly.Find("DuckFXRoot") : null;
            Transform flowerFXRootReadOnly = successFXZoneReadOnly != null ? successFXZoneReadOnly.Find("FlowerFXRoot") : null;
            Transform cloudAndRainReadOnly = independentWorldFXRootGO != null ? independentWorldFXRootGO.transform.Find("CloudAndRain") : null;

            Debug.Log($"LevelManager2D effect transform validation (Level 1, read-only): DuckFXRoot={DescribeTransform(duckFXRootReadOnly)}, " +
                $"FlowerFXRoot={DescribeTransform(flowerFXRootReadOnly)}, CloudAndRain={DescribeTransform(cloudAndRainReadOnly)}. " +
                "None of these three are written by this script (Phase 9L) - the currently-saved GameScene2D Edit Mode transforms, " +
                "under IndependentWorldFXRoot, are authoritative and structurally protected from GameplayBoardFitter2D/BoardRoot changes.");
        }

        private static string DescribeTransform(Transform t)
        {
            if (t == null) return "n/a";
            return $"pos=({t.localPosition.x:0.000000000},{t.localPosition.y:0.000000000},{t.localPosition.z:0.000000000}) " +
                $"scale=({t.localScale.x:0.000000000},{t.localScale.y:0.000000000},{t.localScale.z:0.000000000}) " +
                $"rot=({t.localRotation.x:0.000},{t.localRotation.y:0.000},{t.localRotation.z:0.000},{t.localRotation.w:0.000})";
        }
#endif

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

        /// <summary>
        /// Part B (Phase 9E visual-scale audit): every pipe sprite actually
        /// spawned (GetPrefabFor only ever returns pipeStraightWidePrefab/
        /// pipeStraightNarrowPrefab/pipeCornerPrefab/pipeTeePrefab/
        /// pipeCrossPrefab - all five share the same pipes_tileset.png atlas,
        /// 32x32px sub-sprites at spritePixelsToUnits=32) is authored to
        /// render at EXACTLY 1.0 world unit on both axes at localScale 1 -
        /// i.e. it already fills 100% of a cellSize=1 cell edge-to-edge with
        /// zero margin. This is NOT a double-scale bug (BoardFitContainer's
        /// screen-fit scale and this cellSize multiplication are two
        /// different, correctly-separate concerns - the former fits the
        /// WHOLE already-consistent board to the viewport, the latter keeps
        /// each pipe's LOCAL size matched to the LOCAL grid spacing so it
        /// still aligns after SetGridSize() changes cellSize for a
        /// different grid width/height) - the sprite itself was simply
        /// authored at 100% instead of leaving any margin.
        /// </summary>
        private const float PipeVisualRatio = 0.84f; // spec: pipe visual bounds 78-88% of one cell

        private void SpawnPipe(PipeSpawnData2D spawn)
        {
            GameObject prefab = GetPrefabFor(spawn);
            Transform parent = pipesContainer != null ? pipesContainer : transform;

            GameObject instance = Instantiate(prefab, boardManager.GridToWorld(spawn.gridPos), Quaternion.identity, parent);
            ApplyPipeVisualScale(instance, boardManager.CellSize);
            PipeTile2D pipe = instance.GetComponent<PipeTile2D>();
            pipe.Initialize(spawn.pipeType, spawn.startRotationIndex, spawn.gridPos);
            pipe.OnPlayerRotated += HandlePipeRotatedByPlayer;

            boardManager.SetPipe(spawn.gridPos, pipe);
            activePipes.Add(pipe);
        }

        /// <summary>
        /// Applies PipeVisualRatio on top of cellSize to the pipe's root
        /// (never a "dedicated visual child" - none of the pipe prefabs have
        /// one, the SpriteRenderer/BoxCollider2D/PipeTile2D all live on the
        /// same root GameObject - see class doc), then immediately
        /// compensates BoxCollider2D.size by the inverse ratio so the
        /// WORLD-SPACE tap target is completely unchanged even though the
        /// rendered sprite is now smaller. WaterOverlay (a child of the
        /// pipe root) shrinks in lockstep automatically since child
        /// transforms inherit parent scale - no separate handling needed for
        /// "water overlays must match the resized pipe visuals".
        /// Idempotent per spawn: instance is always a fresh Instantiate, so
        /// collider.size always starts from the prefab's untouched authored
        /// baseline - never divides an already-divided value.
        /// </summary>
        private static void ApplyPipeVisualScale(GameObject instance, float cellSize)
        {
            instance.transform.localScale = Vector3.one * cellSize * PipeVisualRatio;

            BoxCollider2D pipeCollider = instance.GetComponent<BoxCollider2D>();
            if (pipeCollider != null)
            {
                pipeCollider.size /= PipeVisualRatio;
            }
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

        /// <summary>
        /// The single centralized "a pipe rotation was actually accepted"
        /// path - every spawned pipe's OnPlayerRotated is wired here (see
        /// SpawnPipe above), and PipeTile2D.OnPlayerRotated itself only ever
        /// fires from TryRotateByPlayer() after a real rotation happened
        /// (never for Source/Target, which aren't PipeTile2D at all; never
        /// during Initialize()/level setup; never while GameState2D.
        /// IsInputLocked is true, since PipeTile2D.Update() already refuses
        /// to even attempt a rotation then). Playing the pipe click sound
        /// here - rather than inside PipeTile2D itself - keeps pipe rotation
        /// logic completely unchanged; this is a pure addition alongside the
        /// existing RegisterMove()/OnPlayerMoved call.
        /// </summary>
        private void HandlePipeRotatedByPlayer()
        {
            scoreManager.RegisterMove();
            GameAudioManager2D.Instance?.PlayPipeClick();
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
