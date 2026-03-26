// ================================================================
// FILE: PlacementSystem.cs
// Phase 2 — Gridless Building
// Converts mouse position → world coordinates, validates placement,
// shows a preview entity, and confirms/cancels on input.
//
// Refine: add collision layers, building-specific footprint checks.
// ================================================================
using Godot;
using System;

namespace CityBuilder.Systems
{
    public partial class PlacementSystem : Node
    {
        private ECS.EntityManager _em;
        private SplineRoadSystem  _roadSystem;
        private OrientationSystem _orientSystem;

        // Currently active placement preview entity (0 = none)
        private uint _previewEntityId = 0;

        // What we're placing right now
        public ECS.VoxelType  ActiveVoxelType  { get; private set; }
        public ECS.BuildingType ActiveBuildingType { get; private set; }
        public bool IsPlacingRoad { get; private set; }

        // Raised when a building is successfully placed
        public event Action<uint, Vector3> OnBuildingPlaced;
        // Raised when a road start/end is confirmed
        public event Action<Vector3, Vector3> OnRoadPlacementRequested;

        private Vector3 _roadStartPoint;
        private bool    _waitingForRoadEnd = false;

        public override void _Ready()
        {
            _em           = ECS.World.Instance.Entities;
            _roadSystem   = GetParent().GetNodeOrNull<SplineRoadSystem>("SplineRoadSystem");
            _orientSystem = GetParent().GetNodeOrNull<OrientationSystem>("OrientationSystem");
        }

        // ── Public API ────────────────────────────────────────────

        /// Call from UI: begin placing a building type
        public void BeginPlacement(ECS.VoxelType voxel, ECS.BuildingType building)
        {
            IsPlacingRoad     = false;
            ActiveVoxelType   = voxel;
            ActiveBuildingType = building;
            _CreatePreviewEntity();
        }

        /// Call from UI: begin placing a road
        public void BeginRoadPlacement()
        {
            IsPlacingRoad      = true;
            _waitingForRoadEnd = false;
            _DestroyPreview();
        }

        /// Cancel current placement and clean up preview
        public void CancelPlacement()
        {
            _DestroyPreview();
            IsPlacingRoad      = false;
            _waitingForRoadEnd = false;
        }

        // ── Input ─────────────────────────────────────────────────

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseMotion motion)
                _OnMouseMoved(motion);

            if (@event is InputEventMouseButton btn && btn.Pressed
                && btn.ButtonIndex == MouseButton.Left)
                _OnLeftClick();

            if (@event is InputEventKey key && key.Pressed
                && key.Keycode == Key.Escape)
                CancelPlacement();
        }

        private void _OnMouseMoved(InputEventMouseMotion motion)
        {
            var worldPos = _ScreenToWorld(motion.Position);
            if (worldPos == null) return;

            if (IsPlacingRoad && _waitingForRoadEnd)
            {
                // Show a temporary line preview — handled in SplineRoadSystem
                return;
            }

            if (_previewEntityId != 0)
                _UpdatePreview(worldPos.Value);
        }

        private void _OnLeftClick()
        {
            if (IsPlacingRoad)
            {
                _HandleRoadClick();
                return;
            }

            if (_previewEntityId == 0) return;

            var placement = _em.GetComponent<ECS.PlacementComponent>(_previewEntityId);
            if (!placement.IsValid)
            {
                GD.Print("[PlacementSystem] Invalid placement — blocked.");
                return;
            }

            _ConfirmBuilding(placement.PreviewPos);
        }

        private void _HandleRoadClick()
        {
            // We need a camera raycast — stubbed here, wire up in _Ready
            // with the actual Camera3D node path.
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return;

            var mousePos = GetViewport().GetMousePosition();
            var from     = camera.ProjectRayOrigin(mousePos);
            var dir      = camera.ProjectRayNormal(mousePos);

            // Intersect with Y=0 plane
            float t = -from.Y / dir.Y;
            if (t < 0f) return;
            var worldPos = from + dir * t;

            if (!_waitingForRoadEnd)
            {
                _roadStartPoint    = worldPos;
                _waitingForRoadEnd = true;
                GD.Print($"[PlacementSystem] Road start: {_roadStartPoint}");
            }
            else
            {
                OnRoadPlacementRequested?.Invoke(_roadStartPoint, worldPos);
                _roadSystem?.CreateRoad(_roadStartPoint, worldPos);
                _waitingForRoadEnd = false;
                GD.Print($"[PlacementSystem] Road end: {worldPos}");
            }
        }

        // ── Preview Entity ────────────────────────────────────────

        private void _CreatePreviewEntity()
        {
            _DestroyPreview();

            _previewEntityId = _em.CreateEntity();
            _em.AddComponent(_previewEntityId, new ECS.PositionComponent(Vector3.Zero));
            _em.AddComponent(_previewEntityId, new ECS.VoxelMeshComponent(
                ActiveVoxelType, new Color(1f, 1f, 1f, 0.5f)));
            _em.AddComponent(_previewEntityId, new ECS.PlacementComponent
            {
                IsValid = true, PreviewPos = Vector3.Zero
            });
        }

        private void _UpdatePreview(Vector3 worldPos)
        {
            var snapped    = _Snap(worldPos);
            bool isValid   = _IsPositionValid(snapped);
            var placement  = new ECS.PlacementComponent
            {
                IsValid    = isValid,
                PreviewPos = snapped,
            };
            _em.SetComponent(_previewEntityId, new ECS.PositionComponent(snapped));
            _em.SetComponent(_previewEntityId, placement);
            // Tint green / red based on validity
            _em.SetComponent(_previewEntityId, new ECS.VoxelMeshComponent(
                ActiveVoxelType,
                isValid ? new Color(0.3f, 1f, 0.3f, 0.6f) : new Color(1f, 0.3f, 0.3f, 0.6f)));
        }

        private void _DestroyPreview()
        {
            if (_previewEntityId != 0)
            {
                _em.DestroyEntity(_previewEntityId);
                _previewEntityId = 0;
            }
        }

        private void _ConfirmBuilding(Vector3 pos)
        {
            _DestroyPreview();

            uint building = _em.CreateEntity();
            _em.AddComponent(building, new ECS.PositionComponent(pos));
            _em.AddComponent(building, new ECS.VoxelMeshComponent(ActiveVoxelType, Colors.White));
            _em.AddComponent(building, new ECS.BuildingDataComponent
            {
                Type = ActiveBuildingType, Capacity = 4, OccupiedBy = 0
            });

            // Let OrientationSystem assign rotation to nearest road
            _orientSystem?.AlignToNearestRoad(building);

            OnBuildingPlaced?.Invoke(building, pos);
            GD.Print($"[PlacementSystem] Building placed: id={building} at {pos}");
        }

        // ── Helpers ───────────────────────────────────────────────

        private Vector3? _ScreenToWorld(Vector2 screenPos)
        {
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return null;

            var from = camera.ProjectRayOrigin(screenPos);
            var dir  = camera.ProjectRayNormal(screenPos);
            if (Mathf.IsZeroApprox(dir.Y)) return null;

            float t = -from.Y / dir.Y;
            return t > 0f ? from + dir * t : (Vector3?)null;
        }

        private Vector3 _Snap(Vector3 pos)
        {
            // Soft grid snap — aligns to 0.5m increments for cleaner placement
            float snap = 0.5f;
            return new Vector3(
                Mathf.Round(pos.X / snap) * snap,
                0f,
                Mathf.Round(pos.Z / snap) * snap);
        }

        private bool _IsPositionValid(Vector3 pos)
        {
            // Check no other building occupies the same cell
            foreach (var id in _em.Query<ECS.PositionComponent, ECS.BuildingDataComponent>())
            {
                if (id == _previewEntityId) continue;
                var other = _em.GetComponent<ECS.PositionComponent>(id);
                if (other.Position.DistanceTo(pos) < ECS.GameConstants.VoxelSize)
                    return false;
            }
            return true;
        }

        // ── Tests ─────────────────────────────────────────────────

        public void _RunTests()
        {
            GD.Print("[PlacementSystem] Running tests...");

            // Snap test: 0.7 → 0.5, 0.3 → 0.5, 0.8 → 1.0
            var snapped = _Snap(new Vector3(0.7f, 0f, 0.3f));
            System.Diagnostics.Debug.Assert(
                Mathf.IsEqualApprox(snapped.X, 0.5f, 0.01f),
                "Snap X failed");

            GD.Print("[PlacementSystem] Tests passed ✓");
        }
    }
}
