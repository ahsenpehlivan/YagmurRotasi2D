# Current Task - YagmurRotasi2D

## Active Phase

Phase 8A - Scalable Campaign Foundation and Pilot Generator

## Notes (Phase 8A - Scalable Campaign Foundation and Pilot Generator)

- **Goal**: replace the fixed 5x5/6-hardcoded-level assumption with a
  data-driven campaign architecture that can scale to 100 levels across grid
  sizes 5x5-10x10, without hardcoding any of them into `LevelManager2D`.
  Generation is **Editor-only** - runtime never generates, retries, or
  solves for uniqueness; it only ever loads already-generated, already-
  validated baked `CampaignLevelDefinition2D` assets via a
  `CampaignLevelCatalog2D` Resources asset. Levels 1-6 (the existing
  hand-authored production levels) are preserved byte-for-byte; this phase
  additionally generates a pilot batch, Levels 7-20 (7-10 at 5x5, 11-20 at
  6x6). **Levels 21-100 are deliberately NOT generated in this phase** - the
  architecture already supports them (the full 6-tier/100-level difficulty
  plan is already encoded), but populating them is Phase 8B.
- **Prerequisite gate** (must all pass before this phase's own work is
  trusted): `Run Phase 7F Branching Solver Tests` (16/16), `Validate Pipe
  Flow Animation Directions` (13/13), `Validate Production Branching Levels
  4-6`. These were last confirmed passing at the end of Phase 7F.5.2 - **re-
  confirm them in your own Editor session before relying on anything below**,
  since no live Unity access was available while writing this phase.
- **New core types** (`Assets/Scripts/Core2D/GridBounds2D.cs`,
  `Assets/Scripts/Campaign2D/CampaignLevelDefinition2D.cs` +
  `CampaignLevelCatalog2D.cs`): `GridBounds2D` is a centered-coordinate grid
  abstraction (`(0,0)` = middle cell) replacing every hardcoded 5x5/`±2`
  assumption - verified to reproduce Level 1's exact Source `(-2,2)`/Target
  `(2,-2)` for a 5x5 board. `CampaignLevelDefinition2D` is a ScriptableObject
  wrapping the exact same `PipeSpawnData2D`/`Direction2D`/`PipeType2D` runtime
  types production gameplay already understands (`ToLevelData()` converts to
  the existing `LevelData2D`) - no second runtime data model.
- **`BoardManager2D` generalized for variable grid size**: new
  `SetGridSize(w,h)` reallocates the pipe grid and recomputes `cellSize =
  ReferenceBoardWorldSize(5) / max(w,h)`, so 5x5 always yields `cellSize=1`
  (byte-identical to the original fixed board) while 6x6-10x10 shrink
  proportionally via uniform `transform.localScale` on every spawned
  instance (which also correctly scales `BoxCollider2D` via Unity's
  `lossyScale`). `LevelManager2D.LoadLevel` now calls `SetGridSize`+
  `BuildGrid()` per level instead of once at scene start.
- **`Resources`-based catalog discovery**: `LevelManager2D.CatalogResourcePath
  = "CampaignLevelCatalog2D"` (`Assets/Resources/CampaignLevelCatalog2D.asset`).
  New `internal static ResolveLevels()` prefers a non-empty catalog, else
  falls back to the original hardcoded `BuildLevels()` - works identically
  from Editor tooling and a built player, no per-scene field to wire.
  `GameProgress2D`'s existing `TotalLevels => LevelManager2D.
  ProductionLevelCount` (added back in Phase 7F.4, never touched here)
  already scales automatically to the catalog's real count - no changes were
  needed there for the pilot's 20 levels (unlock-chain, reset-scope, and
  existing PlayerPrefs keys all keep working exactly as before).
- **Deterministic generation pipeline** (`Assets/Editor/Campaign/`, Editor-
  only, never compiled into a build):
  - `CampaignSeededRandom2D` wraps `System.Random` (never `UnityEngine
    .Random`'s global order-dependent state); `DeriveAttemptSeed(baseSeed,
    levelNumber, generatorVersion, attempt)` is a stable hash combiner so
    the same 4 inputs always reproduce the same seed.
  - `CampaignGraphBuilder2D` builds the LOGICAL solved graph (per-cell open-
    direction sets) graph-first: a Source-to-Target backbone path, then
    branches/cycles that always reconnect to an existing network node
    (never a dead end).
  - `CampaignPipeConverter2D` converts the graph to real `PipeType2D`+
    `solvedRotationIndex` (degree 2=Straight/Corner, 3=Tee, 4=Cross - every
    rotation verified against the real `PipeTile2D.GetOpenDirections()`,
    never a re-typed table), then generates a deterministic starting
    scramble respecting Straight's two-fold rotational symmetry.
  - Every candidate's solved layout is validated by the real, **unmodified**
    `FlowSolver2D.Solve()` before acceptance - the one authoritative safety
    net regardless of any generator heuristic bug.
  - `CampaignUniquenessSolver2D` counts how many rotation assignments solve
    the level (position/type fixed, only rotation searched), early-exiting
    at 2 - candidates without exactly one solution are rejected. Bounded by
    a complete-assignment budget (not wall-clock, for determinism); a
    budget-exhausted result is `Inconclusive` and treated as a rejected
    attempt, never assumed unique. One deliberately-safe pruning rule only
    (the pipe adjacent to Source's fixed first hop must open toward Source) -
    an "empty neighbor = leak" prune was considered and rejected, since
    `FlowSolver2D`'s leak rules are reachability-gated (a global property),
    so incrementally pruning on it could unsoundly under-count solutions.
  - `CampaignDifficultyProfiles2D` encodes the full 100-level plan (Levels
    1-10=5x5, 11-25=6x6, 26-40=7x7, 41-60=8x8, 61-80=9x9, 81-100=10x10,
    every 10th level using the upper end of its tier's range) - Phase 8A
    only ever calls this for Levels 7-20, but the table already covers 1-100
    so Phase 8B reuses it unchanged.
  - `CampaignMetrics2D` computes a weighted difficulty score (grid size,
    pipe count/type mix, branch/cycle count, minimum taps, wave count - not
    grid-size-alone) and score thresholds by reusing `ScoreManager2D
    .CalculateSuccess`'s exact existing formula/constants for an "optimal
    play" case, never a redesigned formula.
  - `CampaignContentHash2D` computes a stable SHA256 hash over level
    number/dimensions/Source/Target/generator version/seed/ordered pipe
    data - never a Unity instance ID - so re-generating from the same
    stored seed always reproduces the same hash (drift detection).
  - `CampaignLevelGenerator2D` orchestrates the full pipeline with a
    per-level retry loop (`Generate`, deterministic per-attempt seeds) and a
    direct single-shot regeneration entry point from an already-known seed
    (`GenerateFromExactSeed`) - never saves an invalid candidate; returns a
    clear rejection reason instead.
- **Idempotent migration** (`CampaignMigration2D.cs`,
  `YagmurRotasi2D > Phase 8 > Migrate Existing Levels 1-6 To Campaign
  Catalog`): copies `LevelManager2D.BuildLevels()`'s 6 levels verbatim into
  `CampaignLevelDefinition2D` assets, re-validates every solved layout with
  the real `FlowSolver2D`, confirms every starting layout is genuinely
  unsolved (both via the exact-rotation `IsAlreadySolvedState` check and by
  confirming `FlowSolver2D` itself fails on start rotations), then does a
  field-by-field comparison of the migrated data back against the original
  `BuildLevels()` output - **any single mismatch aborts the whole migration
  with nothing saved**. Re-running it after success rewrites the exact same
  values to the exact same asset paths (never a duplicate/second source of
  truth). `BuildLevels()` itself is never modified - it remains the
  documented fallback.
- **Pilot generation command** (`CampaignGenerateCommand2D.cs`,
  `YagmurRotasi2D > Phase 8 > Generate Pilot Campaign Levels 7-20`): fixed
  `PilotBaseSeed = 802026` (a plain constant, not derived from the clock, so
  output is reproducible on any machine); attempt budget 500 for 5x5 levels
  (7-10), 1000 for 6x6 (11-20), per Part S's tier-scaled retry policy.
  Generates sequentially 7->20; if any level fails after its attempt budget,
  the batch **stops there** (transactional per level - whatever already
  succeeded before the failure is still saved, nothing after it is
  attempted). Also added: `YagmurRotasi2D > Phase 8 > Regenerate Selected
  Level From Stored Seed` (Project-window selection of a generator-produced
  `CampaignLevelDefinition2D`; refuses on hand-authored Levels 1-6, which
  have no generation seed to replay).
- **Batch validation commands** (`CampaignValidationCommands2D.cs`):
  `Validate All Campaign Levels` re-derives, for every catalog entry: level-
  number sequencing/uniqueness, grid bounds sanity, no duplicate/out-of-
  bounds/Source-Target-overlapping pipe positions, score-threshold sanity,
  recomputed `minimumRequiredTaps` and `contentHash` matching the stored
  values (catches silent Inspector edits), generated levels' grid size
  matching the difficulty plan, the solved layout succeeding via the real
  `FlowSolver2D` with every placed pipe reached, and the starting layout
  genuinely unsolved. `Validate Unique Solutions` re-runs
  `CampaignUniquenessSolver2D` against each level's CURRENT stored pipe
  data (never trusting the stored `solutionCount`). `Export Campaign
  Validation Report` runs both and writes a plain-text report to
  `<project root>/CampaignValidationReport.txt` (outside `Assets/`, not
  Unity-imported).
- **Grid size preview** (`CampaignGridPreview2D.cs`,
  `YagmurRotasi2D > Phase 8 > Preview Grid Size > 5x5..10x10`): rebuilds only
  the visual grid cells on whichever `BoardManager2D` is in the currently
  open scene (intended for `GameScene2D`) - never touches pipes/Source/
  Target/any saved asset. This does mark the scene as modified in memory
  (BuildGrid() instantiates/destroys real GameObjects) - do not save the
  scene afterward unless the new size is intentional.
- **Generic debug level selector** (`CampaignDebugLevelWindow.cs`,
  `YagmurRotasi2D > Debug > Set Current Level (Any)`): a small EditorWindow
  with a level-number slider (1..`LevelManager2D.ProductionLevelCount`),
  reusing `DebugLevelCommands.SetCurrentLevel(int)`'s exact same real
  unlock-chain logic (widened from `private` to `internal`) rather than a
  second implementation - covers Levels 7-20 (and beyond, as the catalog
  grows) without one dedicated MenuItem per level.
- **What is NOT yet true, and cannot be claimed until you run these commands
  in your own Editor**: no `Assets/Resources/CampaignLevelCatalog2D.asset`
  exists yet on disk; no Level 7-20 data (seeds, dimensions, pipe counts,
  minimum taps, solution counts, attempt counts, hashes) has actually been
  generated; the prerequisite gate (16/16, 13/13, Production Levels 4-6) has
  not been re-confirmed since Phase 7F.5.2; no manual Play Mode test of
  Levels 7/10/11/15/20 has happened. Run, in order: `Migrate Existing Levels
  1-6 To Campaign Catalog`, then `Generate Pilot Campaign Levels 7-20`, then
  `Validate All Campaign Levels` and `Validate Unique Solutions` (or just
  `Export Campaign Validation Report` for both at once), then manually play
  a few of the new levels in `GameScene2D`.
- Nothing about `PipeType2D`, `FlowSolver2D`'s reciprocal/leak/BFS rules,
  `PipeFlowVisualProfile2D`'s entry-direction animation correction, Levels
  1-6's data, scene hierarchies (`Background2D`, clouds, flowers, `Duck_0`,
  `DedicatedSuccessPanel`, `InGameMenu2D`), `successDuration` (5s),
  `duckAnimationDuration` (4s), or the Tee visual offset changed in this
  phase.

## Notes (Phase 7F.5.2 - Correct Incoming Flow Edge Classification)

- **Root cause**: in the diamond-shaped Tee test network, merge tile (1,1)
  (distance 3) was recording 3 `IncomingSides` instead of 2. The spurious
  third entry (`Down`) came from processing tile (1,0) at distance 4: its
  `Up` port points back at (1,1), which was already visited - the old code
  unconditionally appended to any already-visited neighbor's
  `IncomingSides` without checking which tile was actually closer to
  Source, so this reverse observation of (1,1)'s own outgoing edge got
  recorded as if (1,1) were "entered from Down."
- **Fix**: the already-visited branch in `FlowSolver2D.Solve()` now
  compares `distance` (current tile) against `neighborStep.Distance`
  (already-recorded) before appending - only `distance + 1 ==
  neighborStep.Distance` (current is genuinely one step closer to Source)
  counts as a real arrival. The reverse case (neighbor closer to Source)
  and same-wave lateral cycle edges are left alone entirely - both remain
  logically valid reciprocal connections (unaffected leak/BFS/target
  rules), just excluded from animation-arrival metadata. This relies on
  `PipeFlowStep2D.Distance`, already tracked per step - no new distance map
  was needed.
- `PipeFlowAnimationDirectionValidator`'s Case11 was strengthened (not
  relaxed) - still requires exactly 2 distinct `IncomingSides`, and now
  additionally verifies `PrimaryEntrySide` is among them, both incoming
  neighbors sit at `mergeDistance - 1`, the outgoing neighbor sits at
  `mergeDistance + 1`, and the outgoing side is never included - with a
  full neighbor-by-neighbor distance/classification dump on failure.
- Files modified: `Assets/Scripts/Gameplay2D/FlowSolver2D.cs`,
  `Assets/Editor/PipeFlowAnimationDirectionValidator.cs`, this file.
- Nothing else changed: target reachability, reciprocal/leak validation,
  BFS visited/queue behavior, cycle termination, wave distances,
  `PipeType2D`/Tee/Cross mappings, Levels 1-6,
  `startRotationIndex`/`solvedRotationIndex`, `PipeFlowVisualProfile2D`'s
  correction rules, and success timing are all untouched.

## Notes (Phase 7F.5 - Direction-Aware Pipe Water Animation)

- **Root cause**: the logical solver was already correct (Phase 7F/7F.1-7F.4's
  BFS, reciprocal/leak rules, Levels 1-6 were never wrong), but every pipe's
  water-fill animation always played the same fixed frame sequence regardless
  of which side water actually entered from, since neither `FlowSolver2D`
  nor `WaterFlowAnimator2D`/`PipeWaterVisual2D` ever recorded or used arrival
  direction. Frames 0..3 progress from empty to fully-filled - the four
  sprite-sheet rows (Corner/Tee/Cross/WideStraight/NarrowStraight) each fill
  from one fixed authored side of their own raw art, so playing the same
  animation regardless of real entry made some pipes look like water was
  flowing backward.
- **`FlowSolveResult2D.FlowWaves` (`IReadOnlyList<IReadOnlyList<PipeTile2D>>`)
  replaced with `FlowSteps` (`IReadOnlyList<IReadOnlyList<PipeFlowStep2D>>`)**.
  New `PipeFlowStep2D` (Pipe, Distance, PrimaryEntrySide, IncomingSides).
  `FlowSolver2D.Solve()`'s BFS now records, for every reachable pipe, the
  real world direction water entered from - the FIRST valid discovery
  becomes `PrimaryEntrySide` (fixed `GetOpenDirections()` iteration order,
  never HashSet order) and is never replaced; later reciprocal discoveries
  from a branch merge or cycle rejoin are appended to `IncomingSides` without
  re-enqueueing the tile. The first pipe's entry side is
  `sourceOutputDirection.Opposite()`. BFS traversal, visited-set logic,
  reciprocal/leak validation and wave grouping are otherwise byte-for-byte
  unchanged.
- New `Direction2DExtensions.ToLocalDirection(worldDirection, rotationIndex)`
  (Core2D) converts a world entry side into the pipe's LOCAL (canonical
  rotationIndex=0) direction via exact enum-index arithmetic - verified via a
  round-trip property test (`local + rotationIndex ≡ world`), not floating-
  point transform math.
- New `PipeFlowVisualProfile2D` (Visual2D) holds, per pipe type, the LOCAL
  side its unmodified animation authoredly fills from, and resolves the
  WaterOverlay-only correction (an extra Z rotation and/or mirror on top of
  the type's existing, UNCHANGED fixed art-alignment offset) needed when the
  real local entry differs. **Frame order always plays forward (0..3, ending
  on the fully-filled frame) - correction is achieved entirely through this
  spatial transform, never by reversing frame order** (which would end on
  the wrong, empty-looking frame for this progressive-fill sheet).
  - Straight: 180-degree correction (self-inverse; a fully-filled symmetric
    channel looks identical either way).
  - Cross: exact rotation by whatever 90-degree multiple is needed (fully
    rotationally symmetric - correct for all four entries, not just two).
  - Corner: a 90-degree rotation + local mirror (`flipX`) - a diagonal
    reflection ((x,y)->(y,x)) that swaps which of its two ADJACENT open
    sides the fill starts from while keeping the same L-shaped silhouette
    aligned with BaseVisual.
  - Tee: fully correct only when water enters via the branch/stem (the one
    case the authored animation actually depicts); a through-line entry gets
    a best-effort partial mirror distinguishing the two through-line sides,
    but is **not a full fix** - see Known Limitations below.
  - Derived from direct inspection of `pipes_tileset.png`'s four rows plus
    the already-working, UNCHANGED fixed offsets (confidence: HIGH for Tee
    and for the overall method - Straight/Corner/Cross's authored local
    entries all independently converge on the same value, "Right", a strong
    internal consistency check; MODERATE for the exact raw-pixel geometry).
    Adjustable in one place (`PipeFlowVisualProfile2D.AuthoredLocalEntrySide`)
    if Play Mode shows a type still filling from the wrong side.
- `PipeTile2D.PlayWaterFlowVisual` now takes the real world entry side and
  forwards `pipeType`/`rotationIndex`/entry side to
  `PipeWaterVisual2D.PlayFill(...)` (renamed from `PlayFillAnimation`),
  which applies the correction to WaterOverlay's transform/flipX only -
  **BaseVisual is never touched**. `ResetFillAnimation()` restores
  WaterOverlay's captured base rotation and clears `flipX` so a fresh
  fill never compounds on top of a previous attempt's correction.
  `WaterFlowAnimator2D`/`UIManager2D`/the two branching-level editor
  validators were updated for the `FlowSteps` rename only - no other
  behavior changed (input locking, run-ID cancellation, timeout, wave
  grouping, success FX hand-off, `successDuration`/`duckAnimationDuration`
  all unchanged).
- New read-only `YagmurRotasi2D > Debug > Preview Pipe Flow Directions`
  (instantiates temporary, unsaved prefab copies to log every real
  rotation/entry combination's resolved correction) and
  `YagmurRotasi2D > Validate Pipe Flow Animation Directions` (13
  deterministic cases: correction values, world-to-local round-trip for all
  16 combinations, BaseVisual-never-touched, fully-filled-frame
  configuration, multi-entry-animates-once, Levels 4-6 still solve, and the
  existing 16-case branching solver validation still passes unchanged).
  Neither touches saved progress or the scene.
- **Known limitation (not resolved by this phase)**: a Tee entered via one
  of its two through-line ports (not the branch) still cannot be made fully
  visually correct with the existing single 4-frame animation and no new
  art - no rotation/mirror maps a through-line entry onto the authored
  "branch fills, spreads to both ends" pattern, since the branch and
  through-line ports are topologically different roles. A real fix needs
  either dedicated entry-specific Tee frame variants or a procedural
  fill/mask system - out of scope here. This does NOT affect solver
  correctness, only that specific visual case.
- Nothing about `PipeType2D`, Tee/Cross logical rotation mappings,
  `FlowSolver2D`'s reciprocal/leak rules, `solvedRotationIndex`/
  `startRotationIndex`, Levels 1-6 layouts, scoring, success timing, BaseVisual
  orientation, or Tee/Cross's established visual offsets changed.


## Notes (Phase 7F.4.2 - Tee Visual Orientation Correction)

- **Visual-only bug, confirmed by Play Mode testing**: the Tee sprite was
  rendering 90 degrees off from `PipeTile2D.GetOpenDirections()`'s logical
  directions - the solver was already reading the correct logical
  directions the whole time (branching/leak/reciprocal logic was never
  wrong), but the on-screen branches didn't visually match what the solver
  was actually validating.
- **Fix is Tee-specific only**: `BranchingPipeAssetBinder.GetVisualRotationOffset(PipeType2D)`
  replaces the single shared `VisualRotationOffset` constant - returns 180
  degrees for Tee, 90 degrees (unchanged) for everything else (Corner,
  Straight, and explicitly Cross too, per instruction). Assigned as an
  absolute `Quaternion.Euler(0f, 0f, offset)` on both `BaseVisual` and
  `WaterOverlay` (never `.Rotate()`/incremental), so re-running `Bind T and
  Cross Pipe Assets` any number of times always lands on the same fixed
  offset - it cannot accumulate extra rotation.
- `Assets/Prefabs2D/PipeTee2D.prefab` was found to already have both
  `BaseVisual`/`WaterOverlay` at local Z=180 on disk (`m_LocalRotation:
  {x:0,y:0,z:1,w:0}`, `m_LocalEulerAnglesHint: {x:0,y:0,z:180}`) - the
  binder fix ensures this value is now reproducible/preserved by the
  tooling instead of being reverted back to 90 the next time the binder
  runs (which it previously would have been).
- `BranchingPipePrefabValidator` now checks BaseVisual/WaterOverlay's local
  Z against the per-type expected offset (reads
  `BranchingPipeAssetBinder.GetVisualRotationOffset` - the same single
  source of truth the binder itself uses, not a second hardcoded value)
  using an angle-safe `Mathf.DeltaAngle` comparison (never raw float
  equality, since Unity can represent 180 as -180 internally), and also
  confirms BaseVisual and WaterOverlay match each other.
- New read-only `YagmurRotasi2D > Validate Tee Visual Orientation` prints,
  for every Tee rotationIndex 0-3, the real logical open directions
  alongside the prefab's stored BaseVisual/WaterOverlay rotation - it can
  only confirm the logical tables and the stored value are consistent, not
  rendered pixels, so it explicitly says final confirmation still needs
  Play Mode.
- **Nothing else changed**: `PipeType2D`, `PipeTile2D.GetOpenDirections()`
  (Tee's logical mapping is exactly as before - 0=Up+Left+Right,
  1=Up+Right+Down, 2=Right+Down+Left, 3=Down+Left+Up), `FlowSolver2D`,
  reciprocal/leak validation, BFS waves, every `solvedRotationIndex`/
  `startRotationIndex` value across Levels 1-6, and Straight/Corner/Cross's
  visual offsets are all untouched.


## Notes (Phase 7F.4.1 - Explicit Solved Rotation Metadata)

- **Root cause of the 15/16 failure**: `Case03` derived each pipe's solved
  rotation from the previous/next entries in `LevelData2D.pipes` (`DirectionBetween`
  over list order) - a linear-path assumption that held for Levels 1-3 (each
  one genuinely is a single ordered chain) but is structurally invalid for
  Levels 4-6, which are branching graphs: two pipes both adjacent to the same
  Tee/Cross split are not necessarily adjacent to each other. Exact failure:
  `(1, 2) and (0, 1) are not grid-adjacent (delta=(-1, -1))` at Level 4's
  Tee A, whose two branch legs sit at (1,2) and (0,1) - consecutive in the
  spawn list, but diagonal to each other.
- **Fix**: `PipeSpawnData2D` gained a new field, `solvedRotationIndex` -
  production level metadata for the pipe's correct logical rotation in the
  completed network, authored once by hand per pipe and never re-derived at
  validation time. `startRotationIndex` (the deliberately-scrambled
  player-facing spawn state) is completely unchanged in meaning and value
  for every existing pipe in Levels 1-6 - only `solvedRotationIndex` was
  added alongside it. `LevelManager2D.SpawnPipe` still passes only
  `startRotationIndex` into `PipeTile2D.Initialize()` - gameplay spawning
  was not touched.
- `Case03` (renamed "Existing Levels 1-3 remain solvable" -> **"All
  production levels remain solvable"**) now builds each level's solved
  configuration directly from `spawn.solvedRotationIndex` for every pipe -
  no previous/next-entry derivation, no `DirectionBetween`, no linear-path
  assumption anywhere. Those now-unused helpers
  (`DirectionBetween`/`FindSolvedRotationIndex`/the old
  `DescribeLevelDiagnostics`) were removed entirely rather than left as a
  dead fallback.
- `ProductionBranchingLevelsValidator` updated the same way - reads
  `solvedRotationIndex` directly, never derives it. Also gained explicit
  per-pipe data-integrity checks (rotation indices normalized 0-3, Cross
  always start=0/solved=0, no duplicate grid coordinates, every position
  including Source/Target inside the board) and corrected its minimum-tap
  calculation: Straight has only 2 logically distinct orientations
  (rotationIndex and rotationIndex+2 open the identical port pair), so its
  minimum-taps count is the shorter of the two rotational paths, not raw
  integer subtraction. Re-verified against this corrected formula for every
  Straight pipe in Levels 4-6 - all match the originally reported totals
  exactly (Level 4: 9, Level 5: 11, Level 6: 9 taps), no discrepancy found.
- Levels 1-3's `solvedRotationIndex` values are the exact same solved
  configuration already hand-derived and verified in Phase 7F.3 (re-derived
  independently here as a cross-check, with identical results) - positions,
  pipe types, `startRotationIndex` values, names, scores and challenge
  layouts are all byte-for-byte unchanged.
- Gameplay is unaffected: a normal level load still spawns every pipe at
  its scrambled `startRotationIndex` (unchanged), Restart/Reload still
  reloads the same scrambled layout, and `solvedRotationIndex` is read only
  by editor validation tooling - never by any runtime/gameplay code path.


## Notes (Phase 7F.4 - Production Branching Test Levels)

- **Prerequisite confirmed before adding levels**: `Run Phase 7F Branching
  Solver Tests` was already at `16/16 PASSED` (Phase 7F.3) before any of
  this phase's changes.
- **Three new production levels appended after Level 3** in
  `LevelManager2D.BuildLevels()` (Levels 1-3 are byte-for-byte unchanged):
  - **Level 4 - Üç Kollu Bahçe**: introduces Tee. A Tee splits the route
    into two branches (via Corner legs), a second Tee merges them back
    together, then continues to Target. 2 Tee, 4 Corner, 2 Straight.
  - **Level 5 - Dört Yönlü Kavşak**: introduces Cross. Two Cross pipes (both
    interior cells, never board edges), each with all four sides validly
    closed via Corner/Straight arcs - the player solves entirely by rotating
    the surrounding pieces, since Cross never rotates. 2 Cross, 2 Straight,
    9 Corner.
  - **Level 6 - Yağmur Dağıtım Ağı**: combines Tee and Cross. A Tee splits
    into two branches that merge at a Cross, which splits again into two
    more branches that merge at a second Tee before reaching Target - a
    genuine multi-wave branch/rejoin network (6 BFS waves). 2 Tee, 1 Cross,
    3 Straight, 4 Corner.
  - Every level's solved route was hand-derived and port-by-port verified
    (every open port on every pipe has a confirmed reciprocal neighbor, zero
    leaks) before being encoded - not just assumed solvable because the
    solver happened to accept it.
  - `startRotationIndex` values intentionally differ from the solved
    rotation for a meaningful subset of pieces on each level (Level 4: 5 of
    8 pieces scrambled, 9 total taps; Level 5: 6 of 11 rotatable pieces
    scrambled, 11 total taps; Level 6: 5 of 9 rotatable pieces scrambled, 9
    total taps) - all well above a 4-move minimum, none trivially
    pre-solved, none with every piece wrong. Cross pipes always spawn at
    rotationIndex 0 (matching `PipeTile2D.Initialize`'s own normalization).
- **Production level count is now derived, not hardcoded**: new
  `LevelManager2D.ProductionLevelCount => BuildLevels().Count` (reuses the
  real catalog, never a duplicated count).
  `GameProgress2D`'s private `TotalLevels` constant became a property
  backed by this - existing PlayerPrefs keys
  (`YagmurRotasi.CurrentLevel`/`HighestUnlockedLevel`/`YagmurRotasi.
  LevelStars.1-3`) are untouched and existing players' saved progress is
  preserved; `YagmurRotasi.LevelStars.4/5/6` now work automatically through
  the exact same generic per-level-number key logic that already existed
  (no new key-name code needed). `ResetLevelProgress()`'s loop
  (`for level in 1..TotalLevels`) now covers all 6 automatically too, still
  never touching `GameAudioSettings2D`'s keys and never calling
  `PlayerPrefs.DeleteAll()`.
- **Final-level behavior changed** (`UIManager2D.HandleNextLevelRequested`):
  pressing Next Level on the real final production level (dynamically
  `levelManager.CurrentLevelIndex + 1 >= levelManager.LevelCount` - Level 6
  today) now calls `levelManager.ReloadCurrentLevel()` instead of
  `LoadNextLevel()`'s modulo wrap-around to Level 1. Saved
  `GameProgress2D.CurrentLevel` was already correctly clamped to stay at the
  final level before this change; this only fixes the in-session visual
  behavior to match ("reload/remain on the final level, never wrap"). Every
  non-final level's Next Level behavior is unchanged.
- `MainMenuController2D.HandlePlayPressed` needed no change - it already
  just calls `SceneManager.LoadScene`, relying entirely on
  `GameProgress2D.CurrentLevel` (now correctly clamped to 1-6).
- **New editor tools**: `Assets/Editor/ProductionBranchingLevelsValidator.cs`
  (`YagmurRotasi2D > Validate Production Branching Levels 4-6`, read-only,
  reuses `BranchingSolverTestRunner`'s fixture helpers - widened from
  `private` to `internal` for same-assembly reuse, no duplicated
  board/solver construction) and `Assets/Editor/DebugLevelCommands.cs`
  (`YagmurRotasi2D > Debug > Set Current Level 4/5/6`, editor-only,
  PlayerPrefs-only, never touches a scene or audio settings).
- `BranchingSolverTestRunner`'s Case03 no longer hardcodes "3 production
  levels" - it now asserts `>= 3` and loops however many
  `LevelManager2D.BuildLevels()` actually returns, so it automatically
  covers Levels 4-6 too while still counting as exactly one of the 16
  top-level cases (per-level sub-logs `[PASS 03.N]` unchanged from Phase
  7F.3).
- New master command `YagmurRotasi2D > Install Phase 7F4 Branching
  Production Levels` - (re)confirms Tee/Cross prefab wiring on the scene's
  `LevelManager2D` (idempotent), then runs both the full 16-case solver
  validation and the dedicated Levels 4-6 report, only logging "complete"
  if both pass.
- No change to `GameScene2D`/`MainMenuScene2D` hierarchies, Levels 1-3
  layouts/rotations, Tee/Cross logical mappings, the BFS solver, or any
  preserved manual scene object. Procedural/random level generation remains
  Phase 8, not implemented here.

## Notes (Phase 7F.3 - Existing-Level Regression Fixture Repair)

- After Phase 7F.2's fixture fix, validation reached **15/16** - every
  Tee/Cross/branching/leak/reciprocal/cycle/input case passed; only
  `[FAIL 03/16] Existing Levels 1-3 remain solvable` failed
  (`Level: Üstten Aşağı Rota, FailureReason: ReciprocalConnectionMissing,
  LeakTile: Straight @ (-1, 2), LeakDirection: Right`).
- **Root cause confirmed (Outcome 1, not a level-data bug)**: `Case03` was
  feeding `PipeSpawnData2D.startRotationIndex` straight into the solver -
  but that field is the deliberately-**scrambled initial spawn rotation**
  the player must rotate pieces away from
  (`LevelManager2D.SpawnPipe` passes it directly into
  `PipeTile2D.Initialize()` for the level's starting layout), not a solved
  layout. There is no separate stored "solution" field anywhere in
  `PipeSpawnData2D`/`LevelData2D` (confirmed by inspection - only one
  rotation field exists). "Existing Levels 1-3 remain solvable" means the
  puzzle *can be solved*, not that its initial scrambled state already *is*
  solved.
- **Fix (test-only, zero production changes)**: `Case03` now derives each
  pipe's solved `rotationIndex` itself, using the real production
  `PipeTile2D.GetOpenDirections()` (never a duplicated direction table) and
  the level's own pipe list ordering - `BuildLevels()` always lists pipes in
  source-to-target route order, so each tile's two required open directions
  are simply "toward the previous route position" and "toward the next"
  (`DirectionBetween`), and `FindSolvedRotationIndex` tries all 4 rotations
  of the real pipe type until both are open. Verified by hand for all three
  levels that every tile's declared `pipeType` (Straight vs Corner) is
  actually consistent with its route shape (straight-through vs turn) - so
  this derivation succeeds for every tile in every level with zero
  exceptions, confirming all three levels are genuinely solvable and no
  level-data correction was ever needed.
- `Case03` now logs one sub-result per level -
  `[PASS 03.1] Level 1 - <name> solved configuration succeeds`,
  `03.2`, `03.3` - while still counting as one outer `03/16` case. On
  failure it logs a full tile-by-tile diagnostic (coordinate, type, solved
  rotation, open directions, every neighbor's coordinate/type/rotation and
  whether it reciprocates, with Source/Target handled explicitly rather than
  as ordinary neighbors) before throwing.
- `RunTests()`'s failed-case logging already included the full
  `exception.ToString()` (added in Phase 7F.2) - stack traces were not an
  issue for this failure, only the fixture's use of scrambled vs. solved
  rotations.
- **`FlowSolver2D`/`PipeTile2D`/`PipeType2D` were not touched** - the BFS
  traversal, visited-set logic, reciprocal validation, leak rules, cycle
  behavior, flow-wave generation and Tee/Cross mappings are exactly as
  Phase 7F/7F.2 left them. No special case for any coordinate or level name
  was added anywhere, including inside `FlowSolver2D`.
- Phase 7F is complete only once the harness reports 16/16 **and** Levels
  1-3 are manually confirmed solvable by actually rotating pipes in
  `GameScene2D` Play Mode (not just by this automated derivation).

## Notes (Phase 7F.2 - Validation Fixture Repair)

- Tee/Cross prefab creation (`Bind T and Cross Pipe Assets`) and scene wiring
  already worked correctly. The problem was isolated entirely to the
  validation harness: the initial run of `Run Phase 7F Branching Solver
  Tests` scored **3/16** - only the three cases that never touch
  `BoardManager2D`/`FlowSolver2D` (Tee rotation mapping, Cross tap does not
  rotate, Cross tap does not increase move count) passed; every case that
  built a board/solver fixture failed with the same
  `NullReferenceException`.
- **Root cause**: `BranchingSolverTestRunner.CreateBoard()` created a
  `BoardManager2D` via `AddComponent` and assumed `Awake()` (which allocates
  the internal `PipeTile2D[,] pipes` grid) had already run by the time
  `SetPipe`/`Solve` were called. In this Edit-Mode-menu-command context that
  assumption did not hold, so `pipes` was never allocated and the very first
  `SetPipe`/`Solve` call touching it threw.
- **Fix**: `BoardManager2D` gained a new public `InitializeGrid()` method -
  the exact same allocation `Awake()` already performed, just given a name
  so it can be called deterministically. `Awake()` now simply calls it.
  `BranchingSolverTestRunner.CreateBoard()` calls `board.InitializeGrid()`
  explicitly right after `AddComponent`, so the fixture no longer depends on
  Awake() timing at all.
- **Defense in depth**: `CreateBoard`/`CreateSolver`/`CreatePipe` now each
  `Require()` their own preconditions explicitly (non-null objects, the
  `boardManager` SerializedProperty actually being found, the
  `SetPipe`/`GetPipe` round-trip actually succeeding) so a future fixture
  regression fails with a clear diagnostic message instead of a bare NRE.
- **Full stack traces**: failed-case logging changed from `ex.Message` to
  `{ex}` (implicit `ToString()`) - every `[FAIL]` line now includes the
  exception type, message and complete stack trace.
- **`RunTests()` now returns `bool`** (true only if all 16 passed).
  `Install Phase 7F Branching Pipes` uses this to log an honest final
  result - `Phase 7F installation created the assets, but validation
  failed. Phase 7F is NOT complete.` if any case fails, instead of always
  claiming "install complete". Prefabs/scene wiring are never rolled back
  just because validation fails - they're independent steps.
- **Phase 7F is complete only when the harness reports 16/16 PASSED.** No
  Tee/Cross gameplay logic (`FlowSolver2D`, `PipeTile2D`, `PipeType2D`,
  reciprocal/leak rules, wave grouping) needed any change for this fix - the
  bug was entirely in the Edit-Mode fixture setup, not the production
  solver.

## Notes (Phase 7F.1 - Test Assembly Fix)

- **The NUnit asmdef approach failed**: `Assets/Tests/EditMode/
  YagmurRotasi2D.Tests.EditMode.asmdef` compiled as its own separate
  assembly, but production game code (`PipeTile2D`, `FlowSolver2D`,
  `BoardManager2D`, etc.) compiles into Unity's predefined `Assembly-CSharp`
  - a plain asmdef cannot reference `Assembly-CSharp` without
    `InternalsVisibleTo` plumbing, so every production type failed to
  resolve (CS0246). Both the asmdef and its test file
  (`BranchingFlowSolver2DTests.cs`) were **deleted** rather than patched.
- **No project-wide asmdef migration was performed** - the rest of the
  project still compiles exactly as before, through the predefined
  `Assembly-CSharp`/`Assembly-CSharp-Editor` pair. No new runtime asmdef was
  added.
- **Replacement**: `Assets/Editor/BranchingSolverTestRunner.cs` is now a
  plain Editor script (compiles into `Assembly-CSharp-Editor`, which
  automatically references `Assembly-CSharp` - but a normal assembly
  reference only sees PUBLIC members, not `internal` ones) that calls the
  real `FlowSolver2D`/`PipeTile2D`/`LevelManager2D` directly - no NUnit, no
  `TestRunnerApi`, no test asmdef. 16 `ValidationCase` entries (name +
  `Action`), each building its own isolated in-memory board and cleaning up
  via `RunFixture`'s try/finally. `LevelManager2D.BuildLevels()` was
  narrowed from `public` to `internal`, paired with a one-line
  `[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]` declared at the
  top of `LevelManager2D.cs` (the standard, minimal C# mechanism for this -
  not an asmdef, not a package) so only `Assembly-CSharp-Editor` can see it;
  arbitrary external code still cannot.
- **Menu command unchanged**: `YagmurRotasi2D > Run Phase 7F Branching
  Solver Tests` still exists, same name, now prints
  `[PASS NN/16] <case name>` / `[FAIL NN/16] <case name>` per case plus a
  final `16/16 PASSED` (or `N/16 PASSED, M FAILED`) summary.
- No production code needed a real bug fix from this validation pass -
  `FlowSolver2D`/`PipeTile2D`/`PipeType2D`/`WaterFlowAnimator2D`/production
  level data/Tee-Cross prefabs/scenes were all left exactly as Phase 7F left
  them, aside from the one `internal` visibility narrowing above.

## Notes (Phase 7F)

- **Pipe types extended**: `PipeType2D` now has `Straight=0, Corner=1, Tee=2,
  Cross=3` (explicit, append-only values - existing prefab serialization for
  Straight/Corner is untouched). Tee/Cross direction tables live in
  `PipeTile2D` alongside the existing Straight/Corner ones, using the same
  clockwise `rotationIndex` convention: Tee rotationIndex 0 = Up+Left+Right
  (closed Down), and each 90° step advances the closed side clockwise (1 =
  closed Left, 2 = closed Up, 3 = closed Right). Cross is always
  Up+Right+Down+Left regardless of rotationIndex.
- **Cross is non-rotatable**: `PipeTile2D.IsRotatable` (computed from
  `pipeType`, not a separate serialized flag, so it can never drift out of
  sync) is `false` only for Cross. Tap input now goes through
  `PipeTile2D.TryRotateByPlayer()`, which no-ops entirely (no rotation
  happens, `OnPlayerRotated` never fires, no move is consumed) when
  `IsRotatable` is false. `Initialize()` also force-normalizes a Cross's
  `rotationIndex` to 0 regardless of what level data requests.
- **Flow solver rewritten as a graph BFS** (`FlowSolver2D.Solve(...)`,
  replacing the old linear `TrySolve(...)`): traverses every pipe reachable
  from Source with a `visited` HashSet (so cycles terminate safely and a tile
  reached via multiple branches is only ever processed once), validating
  every water-reachable open port for a reciprocal connection. Returns a new
  `FlowSolveResult2D` (`IsSuccess`, `TargetReached`, `HasLeak`,
  `ReachableTiles`, `FlowWaves`, `FailureReason`, `LeakTile`,
  `LeakDirection`) instead of a bool + ordered list.
- **Success requires TargetReached AND !HasLeak** - reaching Target while
  another reachable branch leaks still fails the level, per spec. A pipe
  that's never reached by the BFS (disconnected from Source) can never cause
  a leak, regardless of its own open ports.
- **Wave-based animation**: `FlowSolveResult2D.FlowWaves` groups reachable
  pipes by BFS distance from Source. `WaterFlowAnimator2D.PlaySuccess(...)`
  now takes `IReadOnlyList<IReadOnlyList<PipeTile2D>>` and plays every pipe
  in a wave simultaneously, waiting for the whole wave (with a timeout safety
  net) before starting the next wave. Input locking, run-ID cancellation,
  the hand-off to `SuccessFXController2D`, `successDuration` (5s),
  `duckAnimationDuration` (4s) and Reload/level-change cancellation are all
  unchanged.
- **New prefabs**: `Assets/Prefabs2D/PipeTee2D.prefab` /
  `PipeCross2D.prefab`, built by `Assets/Editor/BranchingPipeAssetBinder.cs`
  (`YagmurRotasi2D > Bind T and Cross Pipe Assets`) from the Tee/Cross rows
  of the existing `pipes_tileset.png` sheet (the same sheet Corner/Straight
  already use - these two rows were already discovered/validated-but-never-
  bound by `PipeWaterSpriteSheetBinder`). Structure mirrors
  `PipeCorner2D.prefab` exactly (BaseVisual + WaterOverlay, same +90° visual
  offset, same sorting orders 1/2, same BoxCollider2D 0.9x0.9). **Visual
  orientation was not pixel-verified against gameplay direction logic** -
  verify in Play Mode; if the art looks rotated wrong, adjust
  BaseVisual/WaterOverlay's local rotation directly on the prefab (no code
  change needed, direction logic is independent of this value).
- `LevelManager2D` gained `pipeTeePrefab`/`pipeCrossPrefab` fields and its
  `GetPrefabFor` switch now handles all four types explicitly (previously any
  non-Straight type silently fell through to Corner - latent but harmless
  until Tee/Cross existed). **No production level (1-3) was changed** -
  Tee/Cross are spawnable but no level currently uses them.
- **Test/validation-only Tee and Cross layouts**: per the phase's own
  constraint (no production Level 4/5 yet), the required "Tee branch test"
  and "Cross branch test" layouts exist only as in-memory validation
  fixtures inside `Assets/Editor/BranchingSolverTestRunner.cs` (see Phase
  7F.1 notes above - this replaced an earlier broken NUnit-asmdef attempt) -
  never touch player progress, never added to `LevelManager2D.BuildLevels()`.
  `LevelManager2D.BuildLevels()` is `internal static` (data only, no
  behavior change) so this harness can regression-test the real Levels 1-3
  data directly instead of a re-typed copy.
- `LevelManager2D`/`Canvas`/`MainMenuScene2D`/in-game menu/clouds/
  Background2D/board-grid-visuals/flowers/ducks/`successDuration`/
  `duckAnimationDuration`/Levels 1-3 layouts are all untouched by this phase.

## Notes (Phase 7E.9)

- The existing top `MenuButton` (added in Phase 7E.7, unchanged Transform)
  now opens a dedicated, self-contained `Assets/Prefabs2D/UI/InGameMenu2D.prefab`
  instance (`SafeAreaRoot/InGameMenuHost/DedicatedInGameMenu`) - never patches
  the old `InfoPanel` or `DedicatedSuccessPanel`.
- **No `Time.timeScale` anywhere** - pausing is purely logical:
  `GameState2D.IsInputLocked = true` (the same centralized flag `PipeTile2D`
  already checks) plus `SetGameplayButtonsInteractable(false)` (the existing
  helper) while the menu is open. The rain-cloud animation (driven by
  `Update()`, unrelated to this flag) keeps running underneath, as intended.
- `UIManager2D.CanOpenInGameMenu()` blocks opening the menu while
  `GameState2D.IsInputLocked` is true (covers pipe-fill and the whole 5-second
  success FX sequence) **and** separately while
  `dedicatedSuccessPanel.IsVisible` is true (needed because input unlocks
  again slightly *before* the success panel actually appears) - the menu and
  the success panel can never show at the same time.
- **Devam Et**: closes the menu, restores input - touches nothing else.
- **Bölümü Yeniden Başlat**: calls `levelManager.ReloadCurrentLevel()` -
  the exact same path as the existing `ReloadButton`. `HandleLevelLoaded`
  (already subscribed) resets move count/pipe visuals/flowers/ducks/
  `DedicatedSuccessPanel`/input - none of that logic is duplicated. Reloads
  whatever level is currently loaded (e.g. Level 2 stays Level 2). Saved
  progress (`GameProgress2D`) is untouched.
- **Ayarlar** (inside the in-game menu) reuses `GameAudioSettings2D`
  directly from `InGameMenuView2D` (`ToggleMusic`/`ToggleSfx`) - same
  PlayerPrefs keys as `MainMenuScene2D`'s settings panel, no second audio
  system.
- **Ana Menüye Dön**: `SceneManager.LoadScene("MainMenuScene2D")`, no
  additive loading. Saved progress is untouched (it's saved incrementally at
  its own trigger points, not here); the menu instance lives inside
  `GameScene2D` (not `DontDestroyOnLoad`) so it's simply destroyed with the
  scene, never duplicated.
- `MainMenuScene2D` itself was not opened or modified by this phase.

## Notes (Phase 7E.8)

- **New separate scene**: `Assets/Scenes/MainMenuScene2D.unity`, built by
  `Assets/Editor/MainMenuSceneBuilder2D.cs`. `GameScene2D`'s own file is never
  opened or modified by this builder - it only ever creates/opens
  `MainMenuScene2D`. Building/updating the menu does switch the Editor's
  currently open scene away from whatever was open before (unavoidable when
  creating a genuinely separate scene) - re-open `GameScene2D` afterward if
  you need it.
- **Assets discovered**: `Assets/Art2D/FinalSprites/MainMenu/Background/
  MainMenu.png` (941x1672, already has the "Yağmur Rotası" title painted in -
  no separate title text created) and three button visuals -
  `Buttons/startGame.png` ("Oyuna Başla"), `Buttons/settings.png`
  ("Ayarlar"), `Buttons/levelReset.png` ("Level Sıfırla") - all 866x288 with
  their Turkish labels already drawn in, so none of the three main buttons
  get a separate overlaid label. Only one sprite exists per button (no
  `_pressed` variant), so `Button.Transition = ColorTint` (subtle) is used
  instead of SpriteSwap for these three.
- **Progress persistence**: new `Assets/Scripts/Gameplay2D/GameProgress2D.cs`
  (confirmed via project search that no save system existed before this).
  PlayerPrefs keys: `YagmurRotasi.CurrentLevel`, `YagmurRotasi.
  HighestUnlockedLevel`, `YagmurRotasi.LevelStars.1/2/3`. `LevelManager2D
  .Start()` now reads `GameProgress2D.CurrentLevel` (clamped, defaults to 1)
  instead of always hardcoding level 0 - this is the only code change made to
  `LevelManager2D`; its scene hierarchy and level definitions are untouched.
  `UIManager2D.HandleAnimationComplete` records earned stars the moment a
  level completes (not when Next Level is clicked, so progress survives even
  if the player closes the app while viewing the success panel);
  `HandleNextLevelRequested` advances `CurrentLevel`, clamped to the level
  count - completing the final level keeps saved progress pinned at the
  final level rather than requesting a nonexistent one (the in-session
  "Next Level" gameplay wrap-around to level 1 for replay is unchanged).
- **Audio settings persistence**: new
  `Assets/Scripts/Audio2D/GameAudioSettings2D.cs` (Music/SFX on-off only, no
  AudioClips yet - reserved for a later audio-system phase). Keys:
  `YagmurRotasi.Settings.MusicEnabled`/`SfxEnabled`, both default enabled.
- **Level Sıfırla** opens a confirmation panel (İlerlemeyi Sıfırla / Evet /
  Vazgeç) before calling `GameProgress2D.ResetLevelProgress()` - never an
  immediate reset, never `PlayerPrefs.DeleteAll()`, never touches audio
  settings.
- SettingsPanel/ResetConfirmationPanel reuse the shared UI package
  (`Panels/tan`+`tan_inlay`, `Badges/white`+`white_inlay`,
  `Buttons/brown`+`brown_inlay`) - the three dedicated main-menu visuals are
  reserved only for Play/Settings/Reset-progress on the main screen itself.
- All text created in `MainMenuScene2D` uses SHPinscher-Regular11
  (`Assets/SHPinscher-Regular11/SHPinscher-Regular.otf`) - same font,
  same dynamic-Font approach as GameScene2D.
- **Build Settings**: `MainMenuScene2D` set to index 0, `GameScene2D` to
  index 1 (neither was previously registered - only the default
  `SampleScene.unity` was). Pre-existing entries are preserved, moved after
  index 1, not deleted.

## Notes (Phase 7E.7)

- `SuccessPanel2D.prefab`'s `MainPanel/BodyPanel/BodyText` font size increased:
  `fontSize` 34 -> 40, `resizeTextForBestFit` false -> true (range 32-40),
  `alignment` UpperCenter -> MiddleCenter, `lineSpacing` 1 -> 1.1. Still
  SHPinscher-Regular11; content/RectTransform/BodyPanel/BodyPanelInset/
  grey+grey_inlay sprites all unchanged.
- New small `MenuButton` (Buttons/brown + brown_inlay + brown_pressed
  SpriteSwap) placed as a sibling of `LevelBadge` inside `TopHUD`, positioned
  immediately to its right (`anchoredPosition.x` = LevelBadge's right edge +
  14px, same anchors/pivot/Y as LevelBadge - recomputed fresh from
  LevelBadge every run, so it's naturally idempotent without needing a
  baseline-comparison trick). 76x76, square. Its icon is three plain
  `Badges/white.png` bar Images (`BarTop`/`BarMiddle`/`BarBottom`, 36x6, 16px
  spacing) - never a Unicode glyph.
- `UIManager2D.menuButton` wired; `HandleMenuButtonPressed()` is a reserved,
  intentional no-op (just logs) - no menu/pause panel existed anywhere in the
  project before this phase (confirmed by search), so none was invented here
  per the task's explicit scope limit.
- `LevelBadge` and `MoveBadge` positions (including MoveBadge's earlier
  +30px shift) are completely untouched - `MenuButton`'s position is derived
  from `LevelBadge`'s existing RectTransform, never the reverse.

## Notes (Phase 7E.6)

- **The old InfoPanel-patching approach has been abandoned.** Repeated attempts
  to repair the inactive `InfoPanel` scene hierarchy in place proved unreliable
  (see Phase 7E.5's root-cause notes - `StarRoot` never actually persisted).
  The success screen is now a completely separate, self-contained prefab:
  `Assets/Prefabs2D/UI/SuccessPanel2D.prefab`, with its own
  `SuccessPanelView2D` component owning every reference it needs (title, body,
  three stars, Next Level button) independently of the old hierarchy.
- The old `InfoPanel` GameObject remains in the scene, disabled, as inert
  legacy (its serialized fields on `UIManager2D` - `infoPanel`, `infoTitleText`,
  `infoStarText`, `infoDescriptionText`, `infoNextLevelButton`, `starImages` -
  are also kept for compatibility, but nothing shows it anymore).
- `UIManager2D.dedicatedSuccessPanel` (a `SuccessPanelView2D` reference) is now
  the live success UI: `HandleAnimationComplete` calls
  `dedicatedSuccessPanel.Show(scoreManager.StarCount, "Tebrikler!",
  levelManager.CurrentInfoText, HandleNextLevelRequested)`;
  `HandleLevelLoaded` calls `dedicatedSuccessPanel.Hide()`.
  `HandleNextLevelRequested()` (calling `levelManager.LoadNextLevel()`) is the
  single shared implementation used by both the legacy `InfoNextLevelButton`
  and the new panel's Next Level button - no duplicated level-advance logic.
- The prefab is instantiated exactly once, at `SafeAreaRoot/SuccessPanelHost/
  DedicatedSuccessPanel`, via the idempotent
  `SuccessPanelPrefabBuilder.TryInstallIntoScene`.
- All three stars are ALWAYS active/enabled - `SuccessPanelView2D.SetStars`
  only ever changes alpha (earned=1.0, unearned=0.3), never
  activates/deactivates a star GameObject. This is a deliberate departure from
  the old (broken) system to guarantee visibility.
- The panel composites multiple UI package layers (ModalBlocker plain-color
  scrim, PanelShadow, MainPanel+inset, TitleBadge+inset, BodyPanel+inset,
  StarTray+inset with a HorizontalLayoutGroup star row, NextLevelButton with
  SpriteSwap) instead of a single flat sprite swap.
- SHPinscher-Regular11, successDuration=5, duckAnimationDuration=4, all
  clouds/background/board/grid/flowers/ducks are untouched by this phase.

## Notes (Phase 7E.5)

- **Font replaced project-wide**: `Assets/SHPinscher-Regular11/SHPinscher-Regular.otf`
  (imported with `includeFontData=1`, a genuine dynamic Font asset) now replaces
  Thaleah everywhere. `Assets/Editor/PixelFontBinder.cs` was deleted and replaced
  by `Assets/Editor/SHPinscherFontBinder.cs`. Turkish glyph coverage (Ç Ğ İ Ö Ş Ü
  ç ğ ı ö ş ü) is verified for real via `UnityEngine.Font.HasCharacter` (not
  guessed) - the exact per-character result is logged every time the font binder
  runs; check the Console for any reported missing glyphs.
- **Root cause of the missing/incomplete success panel**: direct inspection of
  the saved scene found `UIManager2D.starImages: []` (empty) and no `StarRoot`
  object anywhere under `InfoCard` - the star-creation step from the previous
  phase never persisted for this scene (most likely an interrupted/partial
  previous run; the exact trigger couldn't be determined with certainty from
  static inspection). `InfoCard` was also still missing its own `PackageDecoration`
  tan_inlay layer. Both are now created by `UIPackageBinder`, which is fully
  idempotent (find-or-create by name), so this is repaired regardless of the
  historical cause and can't reappear from merely re-running a tool.
- **New `TitleBadge`** (`Badges/white` + `white_inlay`) wraps `InfoTitleText`
  (reparented inside it, stretched to fill, so its visual position is unchanged)
  - the title no longer floats directly on the tan panel with no badge behind it.
- **Stars restored**: `InfoCard/StarRoot/Star_0..Star_2` (Image, `Stars/star.png`,
  `preserveAspect=true`, `raycastTarget=false`) wired to `UIManager2D.starImages[]`.
  Earned = alpha 1.0, unearned = alpha 0.3 (no empty-star asset exists in the
  package). `ScoreManager2D.StarCount` still drives which stars are earned - no
  scoring logic changed. Stars now also reset to all-unearned on level load
  (`HandleLevelLoaded` calls `UpdateStarImages(0)`), and the displayed count is
  clamped to 0-3. Old `InfoStarText` stays deactivated (inert legacy, not deleted).
- **Hamle badge shifted right** by 30px (`anchoredPosition.x`: -56 -> -26).
  Idempotency is by comparing against the known original baseline (-56) rather
  than unconditionally adding +30 each run - if the value already differs from
  -56 (already shifted, or manually adjusted since), the shift is skipped.
  `LevelBadge` was not touched.

## Notes (Phase 7E.4)

- **UI package is now confirmed exhaustive**: `Assets/Art2D/FinalSprites/UI/`
  contains exactly 13 files (Buttons: `brown`/`brown_inlay`/`brown_pressed`;
  Panels: `tan`/`tan_inlay`/`tan_pressed`; Badges: `grey`/`grey_inlay`/
  `grey_pressed`/`white`/`white_inlay`/`white_pressed`; Stars: `star`) - a
  thorough search of `Assets/UI/`, `Assets/ThirdParty/`, `Assets/Plugins/`,
  `Assets/ImportedAssets/` and the full `Assets/` tree found nothing else
  UI-related. All are flat solid-color 48x48 tiles with zero sprite border
  (confirmed by opening each image) - `Image.Type = Simple` is correct
  throughout, there is no valid 9-slice border anywhere in this package.
- Every visible Canvas element now uses **base + inlay** layering (a
  `PackageDecoration` child inset behind the label, using the `_inlay`
  variant) instead of a single flat color swap - `ReloadButton`/
  `StartWaterButton`/`InfoNextLevelButton`/`ResultPanel`/`InfoCard`/
  `LevelBadge`/`MoveBadge` all got this. Buttons additionally use
  `Button.transition = SpriteSwap` with the `_pressed` variant wired to
  `spriteState.pressedSprite` (no dedicated highlighted/selected/disabled art
  exists in the package, so those slots reuse the base sprite rather than
  being left null). `ModalBlocker` is intentionally left as a plain
  translucent scrim (no opaque package tile is appropriate there).
- **Stars converted from text to Images**: `InfoCard/StarRoot/Star_0..Star_2`
  (new) replace the visible star display; `InfoStarText` is deactivated
  (kept as inert legacy, not deleted). No empty-star asset exists in the
  package, so an unearned star uses the same sprite at reduced alpha
  (`UIManager2D.EmptyStarAlpha = 0.3`). `ScoreManager2D.StarCount` still
  drives which stars show full alpha - `UIManager2D.starImages[]` +
  `UpdateStarImages(starCount)` is the only new logic; scoring itself is
  untouched.
- **PixelFont = `Assets/Thaleah_PixelFont`**. This project has **no
  TextMeshPro package installed** and no TMP usage anywhere in code, so all
  visible text is legacy `UnityEngine.UI.Text`. Converting to TMP would add
  an unverifiable package dependency - out of safe scope here. Applied
  instead: `Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF.ttf` as a
  dynamic `Font` asset directly on every existing `Text` component (assigned
  via `PixelFontBinder`). The package's OTHER font representation,
  `ThaleahFat.fontsettings` (a pre-baked bitmap Font), was inspected directly
  and confirmed to cover only ASCII 32-126 plus a few Latin-1 symbols - **no
  Turkish characters at all** - so it was deliberately not used. Whether the
  dynamic TTF's own glyph table fully covers Ç Ğ İ Ö Ş Ü ç ğ ı ö ş ü could
  not be verified programmatically; check visually in Play Mode.
- No runtime code creates new Text objects dynamically (UI is fully static;
  gameplay code only ever sets `.text` on existing components), so there is
  no "factory" to update for future-proofing - the font persists simply
  because it's saved on the existing scene components, which are never
  destroyed/recreated by Reload/Next Level (only pipes/Source/Target are).

## Notes (Phase 7E.3)

- **Flower bug root cause found and fixed**: `SuccessFXController2D.flowerAnimators`
  in the saved scene had all 8 slots pointing at `{fileID: 0}` (null) - most likely
  from an earlier manual scene edit that recreated/reparented the flower components
  without the array following along. With the array empty, every flower-reset and
  flower-play call silently skipped all 8 flowers (their null-guards did exactly
  what they're supposed to do), so nothing ever reset a flower's SpriteRenderer
  away from whatever sprite a previous Play session left it on (frame 4, fully
  bloomed) or animated it again. Each individual Flower_N's own frames
  array/config was already correct.
  - Fixed at the source: `YagmurRotasi2D > Repair and Rebind Flower Animations`
    rediscovers `Flower_0`..`Flower_7` by NAME under `FlowerFXRoot` and rewrites
    `flowerAnimators` from that discovery, then rebinds each flower's unique sheet
    and forces its SpriteRenderer back to frame 0.
  - Defense in depth: `SuccessFXController2D` now has a runtime fallback
    (`ResolvedFlowerAnimators()`) that auto-discovers flower animators under
    `flowerRoot` if the serialized array is ever empty again, so this class of bug
    can no longer silently disable the whole flower presentation - it logs a
    warning and keeps working instead.
- Final UI package art (`Assets/Art2D/FinalSprites/UI/`) bound onto the *existing*
  `ReloadButton`/`StartWaterButton`/`InfoNextLevelButton`/`ResultPanel`/`InfoCard`/
  `LevelBadge`/`MoveBadge` - only `Image.sprite`/`type`/`color`/`raycastTarget`
  change; Button listeners, RectTransforms and text children are untouched.
  `ModalBlocker` intentionally left alone (dimming scrim, not a decorative panel).
  No empty-star asset exists in the project, and stars are currently shown as text
  (`InfoStarText`), not Images - left unchanged rather than fabricating new UI.
- Custom `StartPoint.png` bound onto `Source2D.prefab`'s existing root
  `SpriteRenderer` (reused directly - no new child object, since it was already the
  dedicated visual renderer). **No `EndPoint.png` (or any end/target-marker asset)
  exists anywhere in the project** - `Target2D`'s green placeholder dot is
  therefore left completely unchanged; add the asset and re-run
  `YagmurRotasi2D > Bind Start End Marker Sprites` once it exists.
- Manual clouds (3), `Background2D`, custom grid sprite, all 8 flower world
  Transforms, `Duck_0`'s Transform, `successDuration` (5) and
  `duckAnimationDuration` (4) are all untouched by this phase's tools.

## Notes (Phase 7E.2)

- Overall success duration is now **5 seconds** (`SuccessFXController2D
  .successDuration`); duck animation now stops after **4 seconds**
  (new `duckAnimationDuration` field). Ducks remain visible (frozen on their
  current frame) from 4s to 5s and after. Flowers are unaffected by this
  change - they still bloom and hold their final frame using their own
  existing per-flower non-looping/hold-last-frame animators; `ResultText` and
  InfoPanel still only appear at the full 5-second mark.
- `RunSuccessTimer` is a single coroutine with two sequential waits
  (duckStopAt, then the remainder up to successDuration) - no overlapping or
  duplicate coroutines. The existing run-ID cancellation guard is unchanged
  and checked after each wait.
- Custom grid artwork (`Assets/Art2D/FinalSprites/Grid/grid.png`, a single
  bordered pixel-art square tile - MODE 1, one sprite per grid cell) is bound
  onto `Assets/Prefabs2D/GridCell2D.prefab`'s SpriteRenderer via
  `Assets/Editor/GridVisualBinder.cs`. `GridCell2D` instances are only ever
  created at **runtime** by `BoardManager2D.BuildGrid()` - they are never
  baked into the saved scene - so editing the prefab asset is the entire
  integration; no scene hierarchy changes were made or needed for the grid.
- Purely visual: `BoardManager2D` coordinates/width/height, `FlowSolver2D`,
  `PipeTile2D`, Source/Target positions, input raycasting, scoring and move
  counting are all untouched.
- Manual clouds (original repositioned/resized + two added), `Background2D`,
  all 8 flower Transforms, `Duck_0`'s Transform, and the Canvas/UI layout are
  all preserved - Phase 7E.2's installer only touches
  `SuccessFXController2D`'s two duration fields and the grid-cell prefab
  asset; it does not reference clouds/flowers/ducks/background/Canvas at all.

## Notes (Phase 7E.1)

- The user manually edited the rain-cloud setup in the scene (repositioned the
  original cloud, resized it, and added two more rain clouds). These edits are
  now **authoritative** - no installer command in this project touches
  cloud/rain objects in any way (no move, resize, delete, recreate,
  deactivate, or animation-component replacement). Multiple cloud objects
  existing simultaneously is expected and intentional, not a bug to "fix".
- `FlowerFXRoot` now holds one flower instance per discovered variant -
  `Flower_0` .. `Flower_7` (8 total), each with its own `SpriteRenderer` +
  `SpriteFrameAnimator2D` and its own unique 5-frame sheet. Assignment is
  1-to-1 by index: `FlowerSpriteSheet.png` -> `Flower_0`,
  `FlowerSpriteSheet (1).png` -> `Flower_1`, ... `FlowerSpriteSheet (7).png`
  -> `Flower_7` (see `SuccessFXSpriteSheetBinder.ParseFlowerVariantOrder` -
  this ordering is deliberately not alphabetical).
- `SuccessFXController2D.flowerAnimators` now holds all 8 animators; no code
  changes were needed in that script since it already iterated the array
  generically (`PrepareInitialState`/`PlaySuccessFX`/`ResetFX` all loop over
  however many flower animators are assigned).
- Newly created flower instances get a default staggered horizontal spread
  (x from -2.4 to 2.4, alternating slight y offset, slight scale variation)
  over the grass area; an instance that already exists keeps its current
  Transform untouched on every re-run (installer is idempotent).
- Duck setup (`Duck_0`) is preserved/reused exactly as the Phase 7E installer
  left it - only created if missing, never repositioned if already present.

## Notes (Phase 7E)

- `Assets/Prefabs2D/Background2D.prefab` is preserved exactly as before - this
  phase only adds objects under `BoardRoot/SuccessFXZone`; the background's
  Transform/SpriteRenderer/sprite/sorting order are never touched.
- Discovered assets: flowers have 8 candidate sprite sheets under
  `Assets/Art2D/FinalSprites/Flowers/` (`FlowerSpriteSheet.png` plus 7 numbered
  variants, each a 5-frame bud-to-bloom growth sequence). As of Phase 7E.1, all
  8 are used (one per flower instance) - see Phase 7E.1 notes above. Ducks are
  unambiguous: a single sheet,
  `Assets/Art2D/FinalSprites/DucksSpriteSheet/ducksSpriteSheet.png`, sliced
  into 94 frames (`ducksSpriteSheet_0`..`_93`).
- `SuccessFXZone` is now ACTIVE by default (a change from Phase 7C.1, where it
  was empty and inactive) - `FlowerFXRoot/Flower_0` is visible at frame 0 by
  default; `DuckFXRoot` (and its `Duck_0` child) stays hidden until success.
- On success (after the final pipe fills): flowers bloom and ducks appear +
  walk simultaneously for exactly 6 seconds (`SuccessFXController2D
  .successDuration`), then flowers hold their bloomed frame and ducks stop
  (staying visible). Only then does `ResultText` switch to
  `Su hedefe ulaştı!` and InfoPanel open.
- On Reload/Next Level, `UIManager2D` calls `successFXController.ResetFX()`
  (alongside the existing `waterFlowAnimator.CancelActiveAnimation()`) so
  flowers reset to frame 0, ducks hide, and no stale 6-second callback can
  open InfoPanel late.
- The old `TargetFX2D` component and its placeholder `FlowerBloomFX`/
  `DuckWalkFX` circles on `Target2D.prefab` are kept as inert legacy
  code/assets (already default-inactive in the prefab) - nothing in the new
  success path calls `TargetFX2D.PlaySuccessFX()` anymore.
  `LevelManager2D.CurrentTargetFX` is likewise kept but now unused.
- `Flower_0`/`Duck_0` Transform positions and scales are approximate defaults
  (placed side by side over the grass area) - adjust manually in the
  Inspector to match the real Background2D art.

## Notes (Phase 7C.1)

- The visible grass ground is provided by the manually-configured
  `Assets/Prefabs2D/Background2D.prefab` (a world-space background sprite,
  sorting order -100), instantiated as-is in `BuildGameScenePhase7C1` -
  its SpriteRenderer/sprite/position/scale are never touched by the builder.
- No placeholder `GrassStrip` rectangle is generated anymore (an earlier
  version of this phase briefly used one; it was replaced once the real
  background prefab was available).
- `BoardRoot/SuccessFXZone` remains as an empty, inactive future parent for
  flower bloom and duck walk animations (positioned at world (0, -3.5, 0),
  just below the grid). Flowers/ducks are still not implemented.
- `ResultText` visually reads as sitting below the grass area (grid → grass →
  ResultText → bottom buttons); `ResultPanel` itself was never moved - only
  the camera's Y position shifted (0 → -0.9, unchanged from the earlier
  grass-rectangle version) to leave room below the grid for this area and any
  future flower/duck content.
- Bottom controls (`Yeniden Dene` / `Su Toplamaya Başla`) are unchanged from
  Phase 7C.

## Notes (Phase 7C, still valid)

- The visible top HUD contains only the current level (`Bölüm 1`) and move
  count (`Hamle: 0`) - no score badge exists anywhere on screen.
- Score remains fully internal: `ScoreManager2D.CalculateSuccess(...)` still
  runs on every successful route (still needed for `StarCount`), but its
  numeric value is never shown to the player, in the HUD or in InfoPanel.
- The rain-cloud area is reserved visually between the TopHUD and the 5x5
  grid; the grid sits directly below it. Camera framing (orthographic size 6,
  position (0,0,-10)) was left unchanged - verified that a smaller size would
  clip the 5-unit-wide grid horizontally on narrow-aspect tall phones (e.g.
  1080x2400), while size 6 already leaves generous vertical margin around the
  board+cloud even after reserving the new top/bottom UI strips.
- `ResultText` sits in a compact `ResultPanel` between the grid and the
  bottom buttons.
- Bottom controls contain exactly two buttons: `Yeniden Dene` (secondary,
  smaller) and `Su Toplamaya Başla` (primary, larger, `StartWaterButton`
  GameObject name unchanged for script compatibility). There is no persistent
  `Sonraki Bölüm` button on the main gameplay screen anymore.
- Level progression only happens through `InfoNextLevelButton` inside the
  success `InfoPanel`.
- `InfoPanel` no longer shows score or path length - only title, stars and
  the educational text, plus the Next Level button.
- New `Assets/Scripts/UI2D/SafeAreaFitter2D.cs` drives `SafeAreaRoot`'s
  anchors from `Screen.safeArea`, so `TopHUD`/`StatusArea`/`BottomControls`/
  `InfoPanel` all stay clear of notches/gesture bars automatically.
- Final UI artwork (real badge/button/card sprites) remains deferred to
  Phase 7D. Real flower and duck animations remain deferred until after that.

## Menu Commands (current)

- `YagmurRotasi2D > Install Phase 7F4 Branching Production Levels` -
  non-destructive; (re)confirms Tee/Cross prefab wiring on the scene's
  `LevelManager2D`, then runs the full 16-case solver validation (now
  covering Levels 1-6) and the dedicated Levels 4-6 report. Idempotent.
  **This is the current recommended command for Phase 7F.4.**
- `YagmurRotasi2D > Validate Production Branching Levels 4-6` - read-only;
  logs pipe-type counts, Tee/Cross counts, solved-route
  success/leak/wave/reachable info and minimum rotation taps for Levels 4-6.
  Never touches saved progress or the scene.
- `YagmurRotasi2D > Debug > Set Current Level 4` / `5` / `6` - editor-only
  (never in a build); jumps local development `GameProgress2D.CurrentLevel`
  straight to the given level (and unlocks it if needed) without replaying
  earlier levels. Never touches saved stars, audio settings, or any scene.
- `YagmurRotasi2D > Install Phase 7F Branching Pipes` - non-destructive;
  binds/builds `PipeTee2D.prefab`/`PipeCross2D.prefab`, wires them onto the
  scene's `LevelManager2D`, and kicks off the branching solver's EditMode
  test suite. Idempotent. Superseded by `Install Phase 7F4 Branching
  Production Levels` above for day-to-day use, but still works standalone.
- `YagmurRotasi2D > Bind T and Cross Pipe Assets` - standalone; only
  builds/updates the two prefab assets from the sprite sheet, does not touch
  the scene.
- `YagmurRotasi2D > Validate Branching Pipe Prefabs` - read-only; logs a
  structured report (pipe type, rotatability, renderer/collider/animator
  counts, sorting orders, missing references) for both prefabs. Never
  modifies anything.
- `YagmurRotasi2D > Run Phase 7F Branching Solver Tests` - read-only; runs
  16 named in-memory validation cases directly against the production
  `FlowSolver2D`/`PipeTile2D`/`LevelManager2D` classes (plain Editor script,
  no NUnit/TestRunnerApi/test asmdef - see Phase 7F.1 notes) and logs a
  `[PASS/FAIL NN/16]` per case plus a final `16/16 PASSED` summary. Never
  touches the scene.
- `YagmurRotasi2D > Install Phase 7E9 In-Game Menu` - non-destructive;
  builds/updates `InGameMenu2D.prefab` and installs exactly one instance
  into `GameScene2D`, wiring `UIManager2D.inGameMenu`. Idempotent. Never
  touches `MainMenuScene2D` or world-space objects. **This is the current
  recommended command for the in-game menu.**
- `YagmurRotasi2D > Build In-Game Menu Prefab` - standalone; only
  builds/updates the prefab asset, does not touch the scene.
- `YagmurRotasi2D > Install In-Game Menu` - standalone; only
  instantiates/rewires the prefab instance into `GameScene2D` (requires the
  prefab to already exist).
- `YagmurRotasi2D > Build Phase 7E8 Main Menu` - creates/updates
  `MainMenuScene2D` (opens it, switching away from whatever scene was open),
  builds the full Canvas hierarchy, wires `MainMenuController2D`, and
  registers both scenes in Build Settings. Idempotent. Never touches
  `GameScene2D`.
- `YagmurRotasi2D > Rebind Main Menu Assets` - standalone; only updates the
  background/three button sprites in the already-open `MainMenuScene2D`.
  Preserves listeners and manually-adjusted RectTransforms.
- `YagmurRotasi2D > Install Phase 7E7 Readable Info and Menu Button` -
  non-destructive; updates only `SuccessPanel2D`'s `BodyText` font settings
  and installs/positions the `MenuButton`. Idempotent.
- `YagmurRotasi2D > Increase Success Info Text` - standalone; updates only
  `SuccessPanel2D/MainPanel/BodyPanel/BodyText`'s font settings.
- `YagmurRotasi2D > Install Small Top Menu Button` - standalone; only
  installs/positions `MenuButton` and wires `UIManager2D.menuButton`.
- `YagmurRotasi2D > Install Phase 7E6 Dedicated Success Panel` -
  non-destructive; builds/updates `SuccessPanel2D.prefab`, installs exactly
  one instance under `SafeAreaRoot/SuccessPanelHost`, wires
  `UIManager2D.dedicatedSuccessPanel`, and disables the old `InfoPanel`.
  Idempotent.
- `YagmurRotasi2D > Build Dedicated Success Panel Prefab` - standalone;
  only builds/updates the prefab asset itself, does not touch the scene.
- `YagmurRotasi2D > Install Dedicated Success Panel` - standalone; only
  instantiates/rewires the prefab instance into the currently open scene
  (requires the prefab to already exist).
- `YagmurRotasi2D > Install Phase 7E5 SHPinscher and Success Panel Repair` -
  still works for the font pass and the old InfoPanel's decorative bindings,
  but the old InfoPanel is no longer the live success screen as of Phase
  7E.6 - use `Install Phase 7E6 Dedicated Success Panel` for the success UI.
- `YagmurRotasi2D > Apply SHPinscher Font Everywhere` - standalone; applies
  `SHPinscher-Regular.otf` (dynamic Font) to every `Text` component
  (including inactive ones), and logs a real `Font.HasCharacter` check for
  every Turkish character. Touches nothing else.
- `YagmurRotasi2D > Repair Complete Success Panel` - standalone; repairs
  only InfoCard/TitleBadge/stars/InfoNextLevelButton. Does not touch
  ReloadButton/StartWaterButton/LevelBadge/MoveBadge/ModalBlocker or
  world-space objects.
- `YagmurRotasi2D > Shift Hamle Badge Right` - standalone; shifts only
  MoveBadge, idempotent via baseline comparison (not unconditional +30).
- `YagmurRotasi2D > Install Phase 7E4 Complete UI and PixelFont` - **removed**
  in Phase 7E.5 (its entire purpose was applying Thaleah, which is no longer
  used anywhere) - use `Install Phase 7E5 SHPinscher and Success Panel
  Repair` instead.
- `YagmurRotasi2D > Bind Complete UI Package` - standalone; skins every
  visible Canvas object with the full UI package (base+inlay+pressed,
  Button SpriteSwap, Image-based stars, TitleBadge). Preserves listeners and
  RectTransforms; touches nothing else.
- `YagmurRotasi2D > Bind Final UI Package` - kept only for backward
  compatibility; now calls the exact same logic as `Bind Complete UI
  Package` (no separate "partial" version exists anymore).
- `YagmurRotasi2D > Install Phase 7E3 UI Markers Flower Repair` -
  non-destructive; runs the (now-complete) `UIPackageBinder` +
  `StartEndMarkerBinder` + `FlowerAnimationRepair` together. Touches only UI
  Image visuals, the Source2D/Target2D marker sprite, and the flower
  animator wiring/frames - no clouds/background/board/grid-logic/level-data/
  timing changes. Idempotent.
- `YagmurRotasi2D > Bind Start End Marker Sprites` - standalone; only
  updates Source2D/Target2D's marker sprite. Preserves Source/Target logic,
  coordinates and board registration; touches nothing else.
- `YagmurRotasi2D > Repair and Rebind Flower Animations` - standalone; only
  rebinds and repairs the 8 flower animators (see root-cause note above).
  Preserves every flower's Transform; touches nothing else.
- `YagmurRotasi2D > Install Phase 7E2 Duck 4s and Grid Visual` -
  non-destructive; requires `SuccessFXController2D` to already exist (run
  Phase 7E1 first if not). Sets `successDuration = 5` /
  `duckAnimationDuration = 4` and binds the custom grid-cell sprite. Touches
  nothing else - no clouds/flowers/ducks/background/Canvas references at all.
- `YagmurRotasi2D > Bind Custom Grid Visual` - standalone; only rebinds the
  grid-cell sprite (from `Assets/Art2D/FinalSprites/Grid/grid.png` onto
  `GridCell2D.prefab`), without touching success timing or anything else.
  Also run automatically at the end of the Phase 7E2 install.
- `YagmurRotasi2D > Install Phase 7E1 All Flowers Preserve Clouds` -
  non-destructive; operates on the currently open `GameScene2D` in place.
  Expands `FlowerFXRoot` to all 8 flower variants, preserves the duck setup,
  and contains zero cloud/rain-related code (the user's manually edited
  clouds - repositioned original + two extra - are never read or touched).
  Idempotent - safe to run repeatedly. **This is the current recommended
  command**, superseding the single-flower Phase 7E installer below.
- `YagmurRotasi2D > Install Phase 7E Flower Duck FX` - non-destructive;
  operates on the currently open `GameScene2D` in place (does NOT rebuild the
  scene, does NOT touch `Background2D`). Idempotent - safe to run repeatedly.
  Still works (creates a single Flower_0), but Phase 7E1 above is preferred
  now that all 8 flower variants are available.
- `YagmurRotasi2D > Bind Flower and Duck Sprite Sheets` - re-binds the real
  flower/duck sprite sheets onto the scene's `SuccessFXController2D` without
  touching anything else. Also run automatically at the end of Install.
- `YagmurRotasi2D > Build Phase 7C1 Grass Success Area` - OLDER full scene
  rebuild command, from before Background2D/flower/duck existed. Do not run
  this (or any older builder) after Phase 7E - it would discard the manually
  placed Background2D and any Inspector-adjusted Flower_0/Duck_0 positions.
- `YagmurRotasi2D > Bind Pipe Fill Sprite Sheet` - re-binds
  `pipes_tileset.png` onto the pipe prefab assets without rebuilding the scene.
- `YagmurRotasi2D > Bind Rain Cloud Sprite Sheet` - re-binds
  `RainCloudSpriteSheet` onto an already-open `GameScene2D`.
