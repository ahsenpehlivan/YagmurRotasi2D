# YagmurRotasi2D Roadmap

## Main Goal

Build a clean 2D mobile pipe puzzle game from scratch.

The game should teach rainwater harvesting and water conservation through simple pipe-rotation gameplay.

The old 3D version is archived. This 2D version must be developed as a clean new project.

---

# Phase 0 - Project Setup

## Goal

Prepare a clean 2D Unity project structure.

## Tasks

- Create Unity 2D Core project.
- Create `GameScene2D`.
- Set camera to Orthographic.
- Set portrait-friendly framing.
- Create folder structure.
- Add `CLAUDE.md`, `ROADMAP_2D.md`, and `CURRENT_TASK.md`.

## Success Criteria

- `GameScene2D` opens.
- Camera is visible and centered.
- Folder structure exists.
- No old 3D scripts or prefabs are copied.

---

# Phase 1 - 2D Grid and Pipe Rotation

## Goal

Create the first playable 2D interaction.

## Scope

Only build:

- 5x5 grid
- Placeholder grid cells
- Straight pipe
- Corner pipe
- Pipe tap/click rotation
- One manual test layout

No water solving yet.

## Tasks

- Create `Direction2D.cs`.
- Create `PipeType2D.cs`.
- Create `PipeTile2D.cs`.
- Create placeholder prefabs:
  - `GridCell2D`
  - `PipeStraight2D`
  - `PipeCorner2D`
- Add `BoxCollider2D` to pipes.
- Add `SpriteRenderer` placeholder visuals.
- On tap/click, rotate pipe by 90 degrees on Z axis.
- Track `rotationIndex` from 0 to 3.
- Log pipe name, pipe type, rotation index and open directions.

## Success Criteria

- 5x5 board is visible.
- Straight pipe clearly switches between horizontal and vertical.
- Corner pipe clearly rotates through four L directions.
- One tap equals one rotation.
- No double input.
- No 3D collider or 3D rotation used.
- Console has no errors.

---

# Phase 2 - FlowSolver2D

## Goal

Detect whether water can flow from Source to Target.

## Scope

Add route validation.

No scoring, no multi-level system, no animation yet.

## Tasks

- Create `FlowSolver2D.cs`.
- Create `Source2D` placeholder.
- Create `Target2D` placeholder.
- Add one test level layout manually.
- Source has output direction.
- Target has input direction.
- FlowSolver checks pipe connections using grid coordinates.
- Use DFS or BFS.
- Store successful ordered path.
- Highlight valid path by changing pipe color to blue.
- Show result text:
  - `Hazır`
  - `Su hedefe ulaştı!`
  - `Bağlantı eksik!`

## Success Criteria

- Correct straight route succeeds.
- Wrong pipe rotation fails.
- Corner route succeeds.
- Successful path order is Source to Target.
- Failed route does not highlight pipes.
- Console has no errors.

---

# Phase 3 - LevelManager2D and 3 Ready Levels

## Goal

Turn the test scene into a small multi-level prototype.

## Scope

Add three hand-made levels.

No scoring yet.

## Tasks

- Create `LevelData2D.cs`.
- Create `PipeSpawnData2D.cs`.
- Create `LevelManager2D.cs`.
- Create hardcoded list of 3 levels.
- Implement:
  - `LoadLevel(int index)`
  - `ReloadCurrentLevel()`
  - `LoadNextLevel()`
- Clean old pipes/source/target safely before loading a new level.
- Keep grid persistent if useful.
- Instantiate source, target and pipes from prefabs.

## Initial Levels

### Level 1 - İlk Rota

- Source: left middle
- Target: right middle
- 3 straight pipes
- Middle pipe starts wrong

### Level 2 - Parka Giden Yol

- Source: left middle
- Target: upper right
- Straight + corner mixed route
- One pipe starts wrong

### Level 3 - Uzun Su Yolu

- Longer L/Z route
- Straight + corner pipes
- Multiple rotations needed

## Success Criteria

- Level 1 loads automatically.
- Reload resets the level.
- Next Level loads cleanly.
- No old object remains clickable.
- No duplicates appear.
- Levels 1, 2 and 3 are solvable.
- Console has no errors.

---

# Phase 4 - UI, Score and Stars

## Goal

Add basic game progression and feedback.

## Scope

Add:

- Move count
- Wrong attempt count
- Score
- Stars
- Info card
- Retry and next level UI

## Tasks

- Create `ScoreManager2D.cs`.
- Create `UIManager2D.cs`.
- Add top UI:
  - Level name
  - Moves
  - Score
  - Result text
- Add bottom UI:
  - `Suyu Başlat`
  - `Yeniden Dene`
- Add info panel:
  - Title
  - Stars
  - Score
  - Path length
  - Educational text
  - Next level button
- Count moves when player rotates pipe.
- Count wrong attempts when flow check fails.
- Calculate final score on success.

## Score Formula

```text
finalScore = 100 + (pathLength * 10) + perfectBonus + moveBonus - wrongPenalty
```

Where:

```text
perfectBonus = 50 if wrongAttemptCount == 0
moveBonus = max(0, 10 - moveCount) * 5
wrongPenalty = wrongAttemptCount * 10
```

Score must not go below 0.

## Stars

Success always gives at least 1 star.

Use simple text stars for now:

```text
Yildiz: * (1/3)
Yildiz: ** (2/3)
Yildiz: *** (3/3)
```

## Success Criteria

- Pipe rotation increases move count.
- Wrong flow check increases wrong attempt count.
- Success calculates score.
- Info card opens after success.
- Reload resets score and moves.
- Next Level resets score and moves.
- Console has no errors.

---

# Phase 5 - WaterDrop2D Path Animation

## Goal

Show the water moving through the valid route.

## Scope

Add simple animated water drop.

No roof/gutter or flowers yet.

## Tasks

- Create `WaterFlowAnimator2D.cs`.
- Create `WaterDrop2D` placeholder prefab.
- On successful route:
  - Lock input
  - Spawn water drop at source
  - Move through ordered path
  - Reach target
  - Then open info card
- On failed route:
  - Do not spawn drop
  - Do not open info panel
- On reload/next:
  - Destroy active drop
  - Stop running animation
  - Unlock input safely

## Success Criteria

- Drop follows pipe centers in correct order.
- Info panel opens only after animation.
- Failed route does not spawn drop.
- Input is locked during animation.
- Reload cleans active drop.
- Next Level does not keep old drop.
- Console has no errors.

---

# Phase 6 - Rain Harvest and Nature Revival

## Goal

Add the educational rainwater harvesting visual sequence.

## Scope

Add simple placeholder animations:

- Roof/gutter
- Drop enters from roof/gutter
- Source bounce
- Flower bloom at target
- Optional duck jump

## Tasks

- Add roof/gutter placeholder near source.
- Drop first slides from roof/gutter to source.
- Drop bounces at source.
- Drop then follows pipe path.
- At target:
  - Target feedback plays
  - Flowers bloom
  - Optional duck jumps
- Info panel opens after all success animations.

## Success Criteria

- Successful route starts roof/gutter animation.
- Failed route does not start animation.
- Flowers start closed.
- Flowers bloom only after target is reached.
- Reload closes flowers.
- Next Level does not duplicate decorations.
- Info panel opens last.
- Console has no errors.

---

# Phase 7 - Asset Swap Preparation

## Goal

Prepare the system for real art assets.

## Scope

Do not import final assets yet unless provided.

Make placeholders easily replaceable.

## Tasks

- Keep visual references isolated.
- Use SpriteRenderer for board pieces.
- Keep pipe logic independent from sprite graphics.
- Ensure sprite changes do not affect FlowSolver.
- Ensure pivot is center-based.
- Document required final asset list.

## Required Final Asset Types

- Grid cell
- Straight pipe
- Corner pipe
- Source / roof / gutter
- Target / water tank / park
- Water drop
- Flower
- Optional duck
- UI buttons
- Background

## Success Criteria

- Replacing placeholder sprites does not break gameplay.
- Pipe logic still depends only on pipe type and rotationIndex.
- Console has no errors.

---

# Phase 8 - Scalable Campaign Foundation and Deterministic Level Generator

## Goal

Grow from 6 hand-authored levels to a scalable, data-driven campaign of at
least 100 levels (grid sizes 5x5 through 10x10), generated deterministically
in the Editor and shipped as pre-validated baked assets - never generated,
retried, or solved for uniqueness at runtime.

## Important

Levels are never hardcoded into `LevelManager2D`. Generation only ever runs
inside the Unity Editor; runtime only loads already-generated, already-
validated `CampaignLevelDefinition2D` assets via a `CampaignLevelCatalog2D`
Resources asset. Every generated candidate is validated by the real,
unmodified `FlowSolver2D` before it is ever saved.

## Phase 8A - Scalable Campaign Foundation and Pilot Generator (complete)

- Data-driven catalog architecture (`GridBounds2D`, `CampaignLevelDefinition2D`,
  `CampaignLevelCatalog2D`), variable-grid `BoardManager2D`/`LevelManager2D`,
  a deterministic seeded RNG, a graph-first generator (backbone + branches/
  cycles, degree-based pipe typing, unique-solution CSP solver, deterministic
  scrambles), difficulty/score metrics for the full 1-100 tier plan, an
  idempotent Levels 1-6 migration command, and the pilot batch: **Levels
  7-10 (5x5) and 11-20 (6x6)** generated and validated.
- **Levels 21-100 are deliberately not populated yet** - the architecture
  (difficulty tiers, generator, uniqueness solver, catalog) already supports
  them without another redesign; only the actual generation run is deferred.
- See `CURRENT_TASK.md`'s "Phase 8A" notes for the full architecture,
  algorithm and editor-command breakdown.

## Phase 8B - Generate and Tune Levels 21-100 (planned)

- Run the same Phase 8A generator/validation commands for the remaining
  tiers (7x7 Levels 26-40, 8x8 Levels 41-60, 9x9 Levels 61-80, 10x10 Levels
  81-100, plus Levels 21-25 to finish the 6x6 tier) - no new architecture
  expected, just running and validating more of it.
- Hand-review/tune difficulty pacing and educational message text per
  chapter once real generated data exists to look at.

## Phase 8C - Chapter-Based Level Select Screen (planned)

- A dedicated level-select screen (chapters grouped by the existing
  difficulty tiers) replacing the current "resume where you left off"-only
  flow - out of scope until Levels 21-100 exist.

## Success Criteria

- Every generated level is always solvable and validated by `FlowSolver2D`.
- Every generated level has exactly one solution (or generation is
  rejected and retried with a different seed).
- Regenerating a level from its stored seed reproduces the same content
  hash.
- Console has no errors.

---

# Phase 9 - Main Menu and How To Play

## Goal

Add basic presentation screens.

## Tasks

- Main menu
- Start button
- How to play screen
- Simple educational explanation
- Optional level select

## Success Criteria

- Player can start game from menu.
- How to play explains pipe rotation and rainwater goal.
- Mobile portrait UI is clean.

---

# Phase 10 - Sound, Polish and Android Build

## Goal

Prepare for delivery.

## Tasks

- Button click sound
- Pipe rotate sound
- Water flow sound
- Success sound
- Fail sound
- Android portrait build
- Device test
- UI safe area check
- Final bugfix

## Success Criteria

- Android build works.
- Touch input works on device.
- UI does not overflow.
- Game is playable from start to finish.
- Console has no critical errors.

## Visual Animation Plan Update

The project will support simple placeholder visuals first, but the final intended presentation includes animated environmental and success feedback assets.

Planned animation assets:

- Cloud rain loop animation (continuous)
- Pipe water flow animation (sprite-sheet based, triggered sequentially on the solved path)
- Flower bloom animation (one-shot at target success)
- Duck walk animation (one-shot at target success)

Important implementation rule:
These animations should be integrated in Unity as sprite sheets / frame sequences / Animation Clips, not as raw GIF playback.

Sequence on successful water flow:
1. Water starts from the cloud anchor area.
2. Main drop reaches the source.
3. Correct path pipes play their water-flow animation one by one in order.
4. Water reaches the target.
5. Flower bloom animation plays once.
6. Duck walk animation plays once.
7. Info panel opens after all success animations complete.

The cloud rain loop animation can play continuously as ambient visual feedback.

---

# Phase 7A/7B/7C - Animation Integration Sub-Phases

These sub-phases break down the "Visual Animation Plan Update" above into the
actual incremental implementation steps, once real sprite-sheet art started
becoming available mid-project.

## Phase 7A - Rain Cloud Sprite Sheet Animation Integration

- Generic sprite-frame animation component (`SpriteFrameAnimator2D`)
- Existing `RainCloudSpriteSheet` integration
- Continuous ambient rain-cloud loop
- Asset-safe sliced-sprite binding (`RainCloudSpriteSheetBinder`)

## Phase 7B - Pipe Fill Sprite Sheet Animations

- Existing `pipes_tileset.png` integration (Corner, Wide Straight, Narrow
  Straight four-frame fill groups; Tee and Cross detected/validated but not
  yet bound to gameplay)
- `PipeWaterVisual2D` per-pipe fill playback with a safe placeholder fallback
- Sequential Source-to-Target pipe fill playback, driven by completion
  callbacks with a timeout safety net (`WaterFlowAnimator2D`)
- Straight pipes gain a visual-only Wide/Narrow variant
  (`StraightVisualVariant2D`); the solver still only ever sees
  `PipeType2D.Straight`
- Asset-safe sprite-sheet binding onto the pipe prefab assets
  (`PipeWaterSpriteSheetBinder`)

### Phase 7B.1 cleanup - Remove WaterDrop2D Path Object

- Removed the moving WaterDrop2D object from the success sequence; each pipe's
  own fill animation is now the only water-flow visual (rain-cloud loop stays
  separate/continuous)
- `WaterFlowAnimator2D` simplified to pipe-sequencing + target FX only
  (`PlaySuccess(orderedPath, targetFX)`)
- `WaterDrop2D.prefab` kept on disk as an unused legacy asset for now (see
  Phase 7B.1 report for deletion guidance)

## Phase 7C - Polished Mobile Canvas Layout

- SafeAreaRoot + SafeAreaFitter2D for notch/gesture-bar safe layout
- Compact TopHUD (level + move count only; no visible score)
- Reserved rain-cloud visual area between TopHUD and the 5x5 grid
- Compact ResultText status panel between the grid and bottom controls
- Two-button BottomControls (`Yeniden Dene` / `Su Toplamaya Başla`);
  persistent Next Level button removed from the main gameplay screen
- Simplified success InfoPanel (title, stars, educational text, Next Level
  button only - no score/path length)
- Placeholder Image backgrounds on every major UI element, ready for a later
  UI art swap (Phase 7D)

### Phase 7C.1 - Grass Success Area

- Grass ground is provided by the manually-configured `Background2D.prefab`
  (world-space, instantiated as-is, never modified by the builder)
- `SuccessFXZone` kept as an empty, inactive future parent for flower/duck
  staging, just below the grid
- Recentered camera framing (Y offset only) to leave room below the grid for
  this area without shrinking the grid

## Phase 7D - UI Asset Swap (planned)

- Real UI artwork for HUD badges, buttons, panels and the InfoCard
- Flower bloom and duck walk animations were originally planned here, but were
  implemented earlier than expected - see Phase 7E below.

## Phase 7E - Flower Bloom and Duck Walk Success Animations

- New `SuccessFXController2D` (`Assets/Scripts/Visual2D/SuccessFXController2D.cs`)
  drives the world-space success presentation under `BoardRoot/SuccessFXZone`,
  which now sits over the grass area already painted into `Background2D`
- Flowers (`FlowerFXRoot/Flower_0`, real `FlowerSpriteSheet.png` frames) are
  visible at frame 0 by default and bloom (non-looping, holds final frame)
  the moment the last pipe finishes filling
- Ducks (`DuckFXRoot/Duck_0`, real `ducksSpriteSheet.png` frames) are hidden
  by default and appear + start walking (looping) at the same moment as the
  flowers
- The combined presentation runs for exactly `successDuration` (6s, serialized
  and editable) before flowers hold their bloomed frame, ducks stop (remaining
  visible), and `WaterFlowAnimator2D`/`UIManager2D` proceed to show
  `Su hedefe ulaştı!` and open InfoPanel
- `WaterFlowAnimator2D.PlaySuccess` now takes a `SuccessFXController2D`
  instead of the old `TargetFX2D` placeholder; `targetFXDuration` was removed
- `TargetFX2D` and its placeholder `FlowerBloomFX`/`DuckWalkFX` circles on
  `Target2D.prefab` are kept as inert legacy code/assets (never called from
  the new success path) rather than deleted
- New `Assets/Editor/SuccessFXSpriteSheetBinder.cs`
  (`YagmurRotasi2D > Bind Flower and Duck Sprite Sheets`) binds the real
  sheets onto the scene's flower/duck animators; duck sheet choice is
  unambiguous, flower sheet had 8 candidates (see Phase 7E install report for
  the full list) and defaults to the unsuffixed `FlowerSpriteSheet.png`
- New non-destructive `YagmurRotasi2D > Install Phase 7E Flower Duck FX`
  command augments the currently open `GameScene2D` in place - unlike every
  `BuildGameScenePhaseX` command, it does not rebuild the scene, so the
  manually-placed `Background2D` instance is fully preserved

### Phase 7E.1 - Preserve Manual Clouds and Use All Flower Variants

- The user manually repositioned/resized the original rain cloud and added
  two more; these manual edits are authoritative and are never read, moved,
  resized or recreated by any installer/binder in this project
- `FlowerFXRoot` expanded from 1 to all 8 discovered flower variants
  (`Flower_0`..`Flower_7`), each with its own unique 5-frame sheet, wired into
  `SuccessFXController2D.flowerAnimators` (no code changes needed there - it
  already iterated the array generically)
- New non-destructive `YagmurRotasi2D > Install Phase 7E1 All Flowers
  Preserve Clouds` command; idempotent, contains zero cloud/rain-related code
- `SuccessFXSpriteSheetBinder` updated to assign flower sheets 1-to-1 by a
  deterministic (non-alphabetical) order instead of binding one sheet to
  every flower animator

### Phase 7E.2 - Duck 4s and Custom Grid Visual

- `SuccessFXController2D` gained a separate `duckAnimationDuration` (4s)
  distinct from `successDuration` (now 5s, was 6s) - ducks stop and freeze
  (staying visible) at 4s, the full presentation (incl. InfoPanel) completes
  at 5s, via one coroutine with two sequential waits (no overlapping timers)
- Custom grid-cell artwork (`Assets/Art2D/FinalSprites/Grid/grid.png`, single
  bordered tile, MODE 1) bound onto `GridCell2D.prefab` via new
  `Assets/Editor/GridVisualBinder.cs` (`YagmurRotasi2D > Bind Custom Grid
  Visual`) - purely visual, no BoardManager2D/FlowSolver2D/PipeTile2D changes
- New non-destructive `YagmurRotasi2D > Install Phase 7E2 Duck 4s and Grid
  Visual` command; touches only the FX timing fields and the grid prefab -
  no cloud/flower/duck/background/Canvas references at all

### Phase 7E.3 - Final UI, Start End Markers, Flower Animation Repair

- Root-cause fix for flowers appearing pre-bloomed: `SuccessFXController2D
  .flowerAnimators` had gone entirely null (8x `{fileID: 0}`) after a prior
  manual scene edit; new `Assets/Editor/FlowerAnimationRepair.cs`
  (`YagmurRotasi2D > Repair and Rebind Flower Animations`) rediscovers
  `Flower_0`..`Flower_7` by name and rewires the array, plus
  `SuccessFXController2D` gained a runtime auto-discovery fallback so this
  can't silently break the flower presentation again
- Final UI package art bound onto existing Canvas objects only (no new UI,
  no listener/RectTransform changes) via new
  `Assets/Editor/UIPackageBinder.cs` (`YagmurRotasi2D > Bind Final UI
  Package`)
- Custom `StartPoint.png` bound onto `Source2D.prefab`'s existing root
  SpriteRenderer via new `Assets/Editor/StartEndMarkerBinder.cs`
  (`YagmurRotasi2D > Bind Start End Marker Sprites`); no `EndPoint.png`
  exists yet, so `Target2D`'s placeholder is unchanged and clearly reported
- New non-destructive `YagmurRotasi2D > Install Phase 7E3 UI Markers Flower
  Repair` command combines all three; zero cloud/background/board/grid-logic
  references

### Phase 7E.4 - Complete UI Package and Global PixelFont

- Confirmed the UI package is exactly 13 flat-color tiles (no hidden larger
  package exists anywhere in the project); `UIPackageBinder` rewritten to use
  base+inlay layered "PackageDecoration" children and `_pressed` SpriteSwap
  states on every visible Canvas object, not just a single color swap
- Star display converted from `InfoStarText` (Text) to `StarRoot/Star_0..2`
  (Image), driven by the existing `ScoreManager2D.StarCount` via new
  `UIManager2D.starImages[]`/`UpdateStarImages` - `InfoStarText` deactivated,
  not deleted
- New `Assets/Editor/PixelFontBinder.cs` (`YagmurRotasi2D > Apply PixelFont
  Everywhere`) applies `Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF.ttf`
  (a dynamic `Font`, chosen over the package's other pre-baked bitmap Font
  variant after confirming that one lacks Turkish characters entirely) to
  every legacy `UnityEngine.UI.Text` component - no TextMeshPro package is
  installed in this project, so TMP conversion was out of safe scope
- New non-destructive `YagmurRotasi2D > Install Phase 7E4 Complete UI and
  PixelFont` command combined both; zero cloud/background/board/grid-logic/
  flower/duck/timing references. **Removed in Phase 7E.5** (Thaleah is no
  longer used anywhere) - see below.

### Phase 7E.5 - SHPinscher Font and Success Panel Repair

- Thaleah replaced project-wide by `Assets/SHPinscher-Regular11/
  SHPinscher-Regular.otf` (dynamic Font); new `SHPinscherFontBinder`
  verifies Turkish glyph coverage for real via `Font.HasCharacter` instead
  of assuming it
- Root-cause fix for the incomplete/missing success panel: `StarRoot` never
  existed in the saved scene (`UIManager2D.starImages` was empty) and
  `InfoCard` was missing its own decoration - both are now created by the
  fully idempotent `UIPackageBinder`, regardless of the historical cause
- New `TitleBadge` (Badges/white + white_inlay) wraps `InfoTitleText`
  (reparented, stretched to fill, same visual position) so the title no
  longer floats directly on the bare tan panel
- Stars restored and wired to `ScoreManager2D.StarCount` via
  `UIManager2D.starImages[]`/`UpdateStarImages` (earned=alpha 1, unearned=
  alpha 0.3, clamped 0-3, reset on level load); `InfoStarText` stays
  deactivated as inert legacy
- MoveBadge shifted +30px right (baseline-comparison idempotent, not
  unconditional); `LevelBadge` untouched
- New non-destructive `YagmurRotasi2D > Install Phase 7E5 SHPinscher and
  Success Panel Repair` command combines all of the above; zero cloud/
  background/board/grid-logic/flower/duck/timing references

### Phase 7E.6 - Dedicated Success Panel Prefab

- Abandoned patching the old (unreliable) `InfoPanel` scene hierarchy;
  replaced with a standalone `Assets/Prefabs2D/UI/SuccessPanel2D.prefab`
  owning its own `SuccessPanelView2D` component and all its own visual
  references (title, body, three stars, Next Level button)
- New `Assets/Scripts/UI2D/SuccessPanelView2D.cs` (`Show`/`Hide`/`SetStars`);
  all three stars always active/enabled, "unearned" is alpha-only
- New `Assets/Editor/SuccessPanelPrefabBuilder.cs`
  (`YagmurRotasi2D > Build Dedicated Success Panel Prefab` /
  `Install Dedicated Success Panel`) composites multiple UI package layers
  per element (shadow, base, inset) instead of one flat sprite
- `UIManager2D.dedicatedSuccessPanel` is now the live success UI;
  `HandleNextLevelRequested()` is the single shared level-advance path for
  both the new panel and the legacy (now-inert) `InfoNextLevelButton`
- Old `InfoPanel` kept disabled as legacy; its fields remain on
  `UIManager2D` for compatibility but are no longer the active display
- New non-destructive `YagmurRotasi2D > Install Phase 7E6 Dedicated Success
  Panel` command combines building the prefab and installing it; zero
  cloud/background/board/grid-logic/flower/duck/timing references

### Phase 7E.7 - Success Text Readability and Top Menu Button

- `SuccessPanel2D`'s `BodyText` font size increased (34->40, Best Fit 32-40,
  line spacing 1.1, MiddleCenter) for readability - still SHPinscher-Regular11
- New small `MenuButton` (Buttons/brown+inlay+pressed, three plain
  `Badges/white.png` bars as the icon) placed next to `LevelBadge` in
  `TopHUD`, position always recomputed from `LevelBadge`'s own RectTransform
  (naturally idempotent); reserved `UIManager2D.HandleMenuButtonPressed` is
  an intentional no-op pending a future menu/pause panel
- `LevelBadge`/`MoveBadge` positions (incl. the earlier +30px MoveBadge
  shift) untouched

### Phase 7E.8 - Main Menu Implementation

- New separate `Assets/Scenes/MainMenuScene2D.unity` (built by new
  `Assets/Editor/MainMenuSceneBuilder2D.cs`) using the user's dedicated
  main-menu artwork (`Assets/Art2D/FinalSprites/MainMenu/`) for the
  background and three main buttons (all with embedded Turkish labels), plus
  the shared UI package for the Settings/Reset-confirmation panels
- New `Assets/Scripts/Gameplay2D/GameProgress2D.cs` (PlayerPrefs-backed
  current level / highest unlocked level / per-level best stars) and
  `Assets/Scripts/Audio2D/GameAudioSettings2D.cs` (Music/SFX on-off
  preference, no AudioClips yet) - first save systems in the project
- `LevelManager2D.Start()` now resumes `GameProgress2D.CurrentLevel` instead
  of always starting at level 1; `UIManager2D` records star results on
  success and advances saved progress on Next Level (clamped to the level
  count) - the only code changes made to `GameScene2D`, no visual/hierarchy
  changes
- New `Assets/Scripts/UI2D/MainMenuController2D.cs` drives Oyuna Başla /
  Ayarlar / Level Sıfırla (with a confirmation step before actually
  resetting)
- `MainMenuScene2D` registered first in Build Settings, `GameScene2D`
  second; pre-existing entries preserved after them

### Phase 7E.9 - In-Game Menu Panel

- The existing top `MenuButton` (Phase 7E.7, Transform unchanged) now opens a
  new dedicated `Assets/Prefabs2D/UI/InGameMenu2D.prefab` instance (built by
  new `Assets/Editor/InGameMenuPrefabBuilder.cs`) - Devam Et / Bölümü Yeniden
  Başlat / Ayarlar / Ana Menüye Dön - never patches the old InfoPanel or
  DedicatedSuccessPanel
- Pausing is purely logical (`GameState2D.IsInputLocked` +
  `SetGameplayButtonsInteractable(false)`), no `Time.timeScale` anywhere, so
  timed success/water coroutines are never at risk of freezing mid-state
- Restart routes through the existing `levelManager.ReloadCurrentLevel()` /
  `HandleLevelLoaded` path (no duplicated reset logic); Ana Menüye Dön loads
  `MainMenuScene2D` without resetting saved progress; in-menu Ayarlar reuses
  `GameAudioSettings2D` directly (no second audio system)
- New non-destructive `YagmurRotasi2D > Install Phase 7E9 In-Game Menu`
  command; zero cloud/background/board/grid-logic/flower/duck/timing/
  MainMenuScene2D references

### Phase 7F - T and Cross Branching Pipe Support

Note: distinct from "Phase 8 - Controlled Random Level Generator" below, which
is unrelated and still further out.

- `PipeType2D` extended with `Tee`/`Cross` (append-only numeric values -
  Straight/Corner unchanged); Tee uses the same clockwise `rotationIndex`
  convention as Corner (idx0 = Up+Left+Right, closed side advances clockwise
  each step); Cross is always Up+Right+Down+Left and is the one pipe type
  that never rotates (`PipeTile2D.IsRotatable`, computed from `pipeType`) -
  tapping it consumes no move and fires no rotation event
  (`PipeTile2D.TryRotateByPlayer()`)
- `FlowSolver2D` rewritten from a single linear source-to-target walk into a
  cycle-safe BFS graph traversal (`Solve(...)` returning a new
  `FlowSolveResult2D`): validates every water-reachable open port for a
  reciprocal connection, a tile reached via multiple branches is only ever
  processed once, and disconnected pipes outside the reachable network can
  never fail the route. Success requires Target reached AND zero leaks -
  reaching Target while another reachable branch leaks still fails
- `WaterFlowAnimator2D.PlaySuccess(...)` now plays BFS-distance "waves" of
  pipes simultaneously (branch-aware) instead of one linear ordered list;
  input locking, run-ID cancellation, timeout safety, and the hand-off to
  `SuccessFXController2D` (`successDuration` 5s, `duckAnimationDuration` 4s)
  are all unchanged
- New `Assets/Prefabs2D/PipeTee2D.prefab` / `PipeCross2D.prefab`, built by
  new `Assets/Editor/BranchingPipeAssetBinder.cs`
  (`YagmurRotasi2D > Bind T and Cross Pipe Assets`) from the Tee/Cross rows
  of the existing `pipes_tileset.png` sheet (already discovered/validated in
  Phase 7B, never bound to gameplay until now) - same BaseVisual/WaterOverlay
  structure, +90° visual offset, sorting orders and collider size as
  PipeCorner2D/PipeStraightWide2D
- `LevelManager2D` wired with `pipeTeePrefab`/`pipeCrossPrefab` fields and a
  corrected `GetPrefabFor` switch (previously any non-Straight type silently
  fell through to Corner); Levels 1-3 unchanged, no production level uses
  Tee/Cross yet
- Validation: an initial asmdef-based NUnit EditMode test assembly
  (`Assets/Tests/EditMode/`) could not reference the predefined
  `Assembly-CSharp` and was removed (Phase 7F.1). Replaced with a plain
  Editor-script validation harness, `Assets/Editor/BranchingSolverTestRunner.cs`
  (no NUnit/TestRunnerApi/test asmdef - compiles into
  `Assembly-CSharp-Editor`, which sees `Assembly-CSharp`'s `internal`
  members via a one-line `[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]`
  in `LevelManager2D.cs`), running 16 named in-memory cases directly
  against the real `FlowSolver2D`/`PipeTile2D`/`LevelManager2D` - covering
  existing-behavior regression (including the real Levels 1-3 data via
  `internal LevelManager2D.BuildLevels()`), Tee rotation mapping, Tee/Cross
  branch/leak/cycle scenarios, and Cross's non-rotatable tap behavior. These
  in-memory fixtures double as the phase's non-production "Tee/Cross test
  layouts"
- New read-only `YagmurRotasi2D > Validate Branching Pipe Prefabs` and
  `YagmurRotasi2D > Run Phase 7F Branching Solver Tests` commands, plus the
  master `YagmurRotasi2D > Install Phase 7F Branching Pipes`

### Phase 7F.4 - Production Branching Test Levels

- Three new production levels appended after Level 3 (Levels 1-3 unchanged):
  **Level 4 - Üç Kollu Bahçe** (Tee split/merge), **Level 5 - Dört Yönlü
  Kavşak** (two fully-wired, non-rotatable Cross junctions), **Level 6 -
  Yağmur Dağıtım Ağı** (combined Tee+Cross multi-wave branch/rejoin network)
- Production level count is now derived (`LevelManager2D.
  ProductionLevelCount`) instead of hardcoded; `GameProgress2D` progress/
  reset/star-key logic scales to all 6 levels automatically with existing
  PlayerPrefs keys unchanged and existing players' progress preserved
- Final-level "Next Level" now reloads the real final level instead of
  wrapping to Level 1
- `PipeSpawnData2D` gained an explicit `solvedRotationIndex` field
  (production level metadata, populated for all 6 levels) after the
  original list-order-based derivation proved structurally invalid for
  branching (non-linear) levels - `Case03`/`ProductionBranchingLevelsValidator`
  now read it directly instead of inferring from spawn order; gameplay still
  only ever spawns `startRotationIndex`
- New `YagmurRotasi2D > Validate Production Branching Levels 4-6` (read-only
  report) and `YagmurRotasi2D > Debug > Set Current Level 4/5/6`
  (editor-only dev shortcuts), plus the master
  `YagmurRotasi2D > Install Phase 7F4 Branching Production Levels`

### Phase 7F.5 - Direction-Aware Pipe Water Animation

- Water-fill animations now visually start from the side water really
  enters each pipe (real BFS entry data, `PipeFlowStep2D` /
  `FlowSolveResult2D.FlowSteps`) instead of always playing the same fixed
  animation - fixes pipes that previously looked like water was flowing
  backward. Achieved entirely via a WaterOverlay-only spatial correction
  (`PipeFlowVisualProfile2D`); frame order and BaseVisual are never touched.
  Known limitation: a Tee entered via a through-line port (not the branch)
  isn't fully correctable without new art - see CURRENT_TASK.md.

### Phase 8A - Scalable Campaign Foundation and Pilot Generator

- Replaced the fixed 5x5/6-hardcoded-level assumption with a data-driven
  campaign architecture: `GridBounds2D` (centered-coordinate grid
  abstraction), `CampaignLevelDefinition2D`/`CampaignLevelCatalog2D`
  ScriptableObjects (reusing the existing `PipeSpawnData2D`/`LevelData2D`
  runtime model, no second representation), and a variable-grid
  `BoardManager2D`/`LevelManager2D` (`SetGridSize` derives `cellSize` from a
  fixed reference world size, so 5x5 stays byte-identical while 6x6-10x10
  scale proportionally via uniform instance scaling)
- New Editor-only deterministic generator pipeline (`Assets/Editor/Campaign/`):
  seeded RNG (`CampaignSeededRandom2D`), graph-first level construction
  (`CampaignGraphBuilder2D` - backbone then reconnecting branches/cycles,
  never a dead end), degree-based pipe conversion + deterministic scrambling
  (`CampaignPipeConverter2D`), a bounded unique-solution CSP solver
  (`CampaignUniquenessSolver2D`), the full 1-100 difficulty/tier plan
  (`CampaignDifficultyProfiles2D`), weighted difficulty/score-threshold
  metrics reusing `ScoreManager2D`'s existing formula (`CampaignMetrics2D`),
  stable SHA256 content hashing (`CampaignContentHash2D`), and the
  orchestrator (`CampaignLevelGenerator2D`) - every candidate is still
  validated by the real, unmodified `FlowSolver2D` before acceptance
- Idempotent migration of Levels 1-6 into the catalog
  (`YagmurRotasi2D > Phase 8 > Migrate Existing Levels 1-6 To Campaign
  Catalog`) with field-by-field verification against the original
  `BuildLevels()` data (aborts with nothing saved on any mismatch); pilot
  generation of Levels 7-20 (`Generate Pilot Campaign Levels 7-20`, 7-10 at
  5x5, 11-20 at 6x6, fixed seed for reproducibility); plus regeneration,
  batch structural/uniqueness validation, a report export, a non-destructive
  grid-size scene preview, and a generic debug level selector covering any
  level number instead of one dedicated command per level
- `GameProgress2D`'s unlock/reset logic (already derived from
  `LevelManager2D.ProductionLevelCount` since Phase 7F.4) scales to the
  pilot's 20 levels automatically - no changes needed
- Levels 21-100, and a chapter-based level-select screen, are deferred to
  Phase 8B/8C - see the Phase 8 section above

**Next planned phase: Phase 8B - Generate and Tune Levels 21-100.**