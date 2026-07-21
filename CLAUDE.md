# YagmurRotasi2D - Claude Project Context

## Project Summary

YagmurRotasi2D is a clean 2D reboot of the previous 3D rainwater pipe puzzle prototype.

The game is a mobile portrait 2D educational puzzle game developed in Unity. It teaches children and young users about:

- Rainwater harvesting
- Water conservation
- Sustainable city infrastructure
- Flood prevention
- Using collected rainwater for parks, trees and nature

The core gameplay is simple:

> Rotate pipe tiles, create a valid water route, press **Suyu Başlat**, guide the rainwater to the target, and earn points/stars.

This is a new 2D project. The previous 3D version is archived and must not be modified or copied from.

---

## Main Rule

Do not copy old 3D scripts, prefabs, scenes, materials or systems.

Use the old project only as a design reference, not as code.

The new system must be built cleanly with:

- SpriteRenderer
- BoxCollider2D
- Physics2D
- Orthographic camera
- XY gameplay plane
- Z-axis rotation only

Never use:

- 3D pipe rotation
- 3D colliders
- XZ board logic
- 3D prefab structures
- Old 3D scripts

---

## Target Platform

- Unity 2D Core
- Android mobile
- Portrait orientation
- Touch input required
- Mouse input should also work in Unity Editor for testing

---

## Visual Direction

The first version should use placeholders only.

Use simple temporary shapes:

- Green squares for grid cells
- Grey rectangles for straight pipes
- L-shaped grey placeholders for corner pipes
- Blue circle for source/water
- Green square or circle for target
- Blue circle for water drop

Final art assets will be added later.

The first priority is clean, stable gameplay logic.

---

## Coordinate System

The game uses Unity 2D XY coordinates:

- X = left / right
- Y = up / down
- Z = draw order only

Camera:

- Orthographic
- Portrait mobile framing
- Board centered on screen

Pipe rotation:

- Only rotate on Z axis.
- Do not rotate on X axis.
- Do not rotate on Y axis.
- Do not use 3D Euler rotation.

Example rotation:

```csharp
transform.localRotation = Quaternion.Euler(0f, 0f, -rotationIndex * 90f);
```

---

## Core Gameplay

The player sees a 5x5 grid.

Each level has:

- Source
- Target
- Straight pipes
- Corner pipes
- Optional obstacles later

The player taps a pipe to rotate it by 90 degrees.

Then the player presses:

```text
Suyu Başlat
```

If the route is valid:

- Water route is detected.
- Water drop animates along the path.
- Score is calculated.
- Stars are awarded.
- Educational info card opens.

If the route is invalid:

- No water animation plays.
- Wrong attempt count increases.
- Result text says: `Bağlantı eksik!`

---

## Direction System

Use a clean 2D direction enum:

```text
Up
Right
Down
Left
```

Vector mapping:

```text
Up    = (0, 1)
Right = (1, 0)
Down  = (0, -1)
Left  = (-1, 0)
```

Opposite mapping:

```text
Up <-> Down
Right <-> Left
```

---

## Pipe Types

Initial MVP only uses:

```text
Straight
Corner
```

Do not add T pipe in the first implementation.

Later optional pipe types:

```text
T
Cross
LockedPipe
Obstacle
```

---

## Pipe Logic

### Straight Pipe

```text
rotationIndex 0: Left + Right
rotationIndex 1: Up + Down
rotationIndex 2: Left + Right
rotationIndex 3: Up + Down
```

### Corner Pipe

```text
rotationIndex 0: Up + Right
rotationIndex 1: Right + Down
rotationIndex 2: Down + Left
rotationIndex 3: Left + Up
```

Pipe open directions must be calculated from `rotationIndex`, not from visual rotation.

Visuals are only for display.

---

## Scene Structure

Create a new scene:

```text
Assets/Scenes/GameScene2D.unity
```

Recommended hierarchy:

```text
GameScene2D
  Main Camera

  GameRoot2D
    BoardManager2D
    LevelManager2D
    FlowSolver2D
    ScoreManager2D
    WaterFlowAnimator2D

  BoardRoot
    GridCells
    Pipes
    SourceTarget
    Effects

  Canvas
    TopPanel
      LevelNameText
      MoveText
      ScoreText
      ResultText

    BottomPanel
      StartWaterButton
      RetryButton

    InfoPanel
      TitleText
      StarText
      ScoreText
      PathLengthText
      InfoText
      NextLevelButton

  EventSystem
```

---

## Folder Structure

Use this folder structure:

```text
Assets/
  Scenes/
    GameScene2D.unity

  Scripts/
    Core2D/
      Direction2D.cs
      PipeType2D.cs

    Data2D/
      LevelData2D.cs
      PipeSpawnData2D.cs

    Gameplay2D/
      PipeTile2D.cs
      BoardManager2D.cs
      LevelManager2D.cs
      FlowSolver2D.cs
      WaterFlowAnimator2D.cs
      ScoreManager2D.cs
      GameState2D.cs

    UI2D/
      UIManager2D.cs

  Prefabs2D/
    GridCell2D.prefab
    PipeStraight2D.prefab
    PipeCorner2D.prefab
    Source2D.prefab
    Target2D.prefab
    WaterDrop2D.prefab

  Art2D/
    Placeholder/
    FinalSprites/

  Audio/
```

---

## Input Rules

Use 2D input.

For Unity Editor:

```text
Mouse click should rotate pipes.
```

For mobile:

```text
Touch tap should rotate pipes.
```

Use:

```text
Physics2D.Raycast
BoxCollider2D
Camera.ScreenToWorldPoint
```

UI clicks must not rotate pipes behind buttons.

Always check whether pointer is over UI before raycasting to pipes.

---

## Cleanup Rules

Unity `Destroy()` is delayed until the end of the frame.

When reloading or changing levels:

- Disable old pipe colliders immediately.
- Disable old interactable state immediately.
- Clear old objects safely.
- Then instantiate new level objects.

No old pipe should remain clickable after reload or next level.

No duplicate pipe/source/target/effect object should remain.

---

## System Separation

Keep logic modular:

- `PipeTile2D` handles pipe type, rotation and open directions.
- `FlowSolver2D` handles path validation.
- `LevelManager2D` handles level loading.
- `BoardManager2D` handles grid/coordinate conversion.
- `ScoreManager2D` handles moves, wrong attempts, score and stars.
- `WaterFlowAnimator2D` handles only water/drop movement.
- `UIManager2D` handles UI text, buttons and panels.

Do not create one giant script.

---

## Do Not Add Yet

Do not add these until the roadmap says so:

- Random level generator
- T pipe
- Save system
- Main menu
- Final art assets
- Sound effects
- Particle systems
- Complex animations
- External asset packs
- 3D objects
- 3D colliders
- Old 3D scripts

---

## Development Principle

Build in small safe phases.

After each phase:

1. Test in Play Mode.
2. Confirm no console errors.
3. Confirm mobile portrait layout still works.
4. Report changed files and test result.

Stability is more important than adding many features.

This is a 15-day internship project. The final result should be clean, playable, educational and presentable.