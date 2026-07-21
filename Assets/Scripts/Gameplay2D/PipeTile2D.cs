using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Visual2D;

namespace YagmurRotasi2D.Gameplay2D
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class PipeTile2D : MonoBehaviour
    {
        [SerializeField] private PipeType2D pipeType;
        [SerializeField] private int rotationIndex;

        [Tooltip("The empty-pipe SpriteRenderer, on the BaseVisual child (which carries a fixed +90 degree art-alignment offset). The root itself has no SpriteRenderer.")]
        [SerializeField] private SpriteRenderer baseVisualRenderer;

        [Tooltip("Drives the WaterOverlay fill animation (real sprite-sheet frames if bound, otherwise a safe placeholder fallback).")]
        [SerializeField] private PipeWaterVisual2D waterVisual;

        private Collider2D pipeCollider;
        private Color defaultColor;

        public PipeType2D PipeType => pipeType;
        public int RotationIndex => rotationIndex;
        public Vector2Int GridPosition { get; private set; }

        /// <summary>Cross is a fixed four-way junction with only one meaningful logical state - it must never rotate. Straight/Corner/Tee are all rotatable. Computed from pipeType (not a separate serialized flag) so it can never drift out of sync with the type.</summary>
        public bool IsRotatable => pipeType != PipeType2D.Cross;

        /// <summary>Raised only when the player taps/clicks this pipe and it actually rotated (never for level-setup rotation, never for a no-op Cross tap).</summary>
        public event Action OnPlayerRotated;

        private static readonly Direction2D[][] StraightDirections =
        {
            new[] { Direction2D.Left, Direction2D.Right },
            new[] { Direction2D.Up, Direction2D.Down },
            new[] { Direction2D.Left, Direction2D.Right },
            new[] { Direction2D.Up, Direction2D.Down }
        };

        private static readonly Direction2D[][] CornerDirections =
        {
            new[] { Direction2D.Up, Direction2D.Right },
            new[] { Direction2D.Right, Direction2D.Down },
            new[] { Direction2D.Down, Direction2D.Left },
            new[] { Direction2D.Left, Direction2D.Up }
        };

        // Each 90-degree clockwise rotation advances the closed (missing) side
        // clockwise: rotationIndex 0 closes Down, 1 closes Left, 2 closes Up, 3
        // closes Right.
        private static readonly Direction2D[][] TeeDirections =
        {
            new[] { Direction2D.Up, Direction2D.Left, Direction2D.Right },
            new[] { Direction2D.Up, Direction2D.Right, Direction2D.Down },
            new[] { Direction2D.Right, Direction2D.Down, Direction2D.Left },
            new[] { Direction2D.Down, Direction2D.Left, Direction2D.Up }
        };

        // Cross is open on all four sides regardless of rotationIndex - it has
        // only one meaningful logical state.
        private static readonly Direction2D[] CrossDirections =
        {
            Direction2D.Up, Direction2D.Right, Direction2D.Down, Direction2D.Left
        };

        private void Awake()
        {
            pipeCollider = GetComponent<Collider2D>();
            defaultColor = baseVisualRenderer != null ? baseVisualRenderer.color : Color.white;
        }

        public void Initialize(PipeType2D type, int startRotationIndex, Vector2Int gridPosition)
        {
            pipeType = type;
            // Cross has only one meaningful logical state - always normalized to 0
            // regardless of what level data requests, so it can never be spawned
            // pre-rotated into a misleading index.
            rotationIndex = type == PipeType2D.Cross ? 0 : ((startRotationIndex % 4) + 4) % 4;
            GridPosition = gridPosition;
            ApplyRotation();
        }

        private void Update()
        {
            if (GameState2D.IsInputLocked)
                return;

            if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            Vector2 screenPos = Pointer.current.position.ReadValue();
            Vector2 worldPos = cam.ScreenToWorldPoint(screenPos);
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

            if (hit.collider == pipeCollider)
            {
                TryRotateByPlayer();
            }
        }

        /// <summary>
        /// Attempts a player-triggered rotation: no-ops entirely (no rotation, no
        /// OnPlayerRotated, no move consumed) for a non-rotatable pipe (Cross).
        /// Level setup must use Initialize() instead, which never fires
        /// OnPlayerRotated. Returns true if the pipe actually rotated.
        /// </summary>
        public bool TryRotateByPlayer()
        {
            if (!IsRotatable)
                return false;

            RotatePipe();
            OnPlayerRotated?.Invoke();
            return true;
        }

        /// <summary>Unconditionally advances rotationIndex by one 90-degree step and re-applies the visual rotation. Prefer TryRotateByPlayer() for tap input so non-rotatable pipes are respected.</summary>
        public void RotatePipe()
        {
            rotationIndex = (rotationIndex + 1) % 4;
            ApplyRotation();

            Debug.Log($"{name} | Type: {pipeType} | RotationIndex: {rotationIndex} | Open: {string.Join(", ", GetOpenDirections())}");
        }

        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, -rotationIndex * 90f);
        }

        public Direction2D[] GetOpenDirections()
        {
            switch (pipeType)
            {
                case PipeType2D.Corner:
                    return CornerDirections[rotationIndex];
                case PipeType2D.Tee:
                    return TeeDirections[rotationIndex];
                case PipeType2D.Cross:
                    return CrossDirections;
                case PipeType2D.Straight:
                default:
                    return StraightDirections[rotationIndex];
            }
        }

        public bool HasOpening(Direction2D direction)
        {
            Direction2D[] openings = GetOpenDirections();
            for (int i = 0; i < openings.Length; i++)
            {
                if (openings[i] == direction)
                    return true;
            }
            return false;
        }

        public void SetHighlight(bool isHighlighted)
        {
            if (baseVisualRenderer != null)
            {
                baseVisualRenderer.color = isHighlighted ? Color.blue : defaultColor;
            }
        }

        /// <summary>
        /// Call once when the water flow reaches this pipe, with the real world
        /// direction water enters from (FlowSolveResult2D/PipeFlowStep2D.
        /// PrimaryEntrySide - never guessed from position/rotation alone). Plays
        /// the real 4-frame fill animation, oriented to visually start from that
        /// side, if bound (PipeWaterSpriteSheetBinder/BranchingPipeAssetBinder),
        /// otherwise a safe placeholder fallback. Invokes onCompleted exactly
        /// once, either way.
        /// </summary>
        public void PlayWaterFlowVisual(Direction2D worldEntrySide, Action onCompleted)
        {
            SetHighlight(true);

            if (waterVisual != null)
            {
                waterVisual.PlayFill(pipeType, rotationIndex, worldEntrySide, onCompleted);
            }
            else
            {
                onCompleted?.Invoke();
            }
        }

        /// <summary>Hides the water overlay/fill animation and clears the highlight. Called on reload/next level.</summary>
        public void ResetWaterFlowVisual()
        {
            SetHighlight(false);

            if (waterVisual != null)
            {
                waterVisual.ResetFillAnimation();
            }
        }
    }
}
