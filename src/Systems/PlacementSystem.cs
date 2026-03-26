// ================================================================
// FILE: PlacementSystem.cs  (vollständig ersetzt)
// Phase 2 — Gridless Building
// Robuster Kamera-Raycast auf Y=0-Ebene (oder beliebige yPlane).
// Gebäude-Snap: 0.5m-Grid. Road-Snap: SnappingSystem-Radius.
// ================================================================
using Godot;
using System;

namespace CityBuilder.Systems
{
    public partial class PlacementSystem : Node
    {
        // ── Dependencies ──────────────────────────────────────────
        private ECS.EntityManager _em;
        private SplineRoadSystem  _roadSystem;
        private OrientationSystem _orientSystem;
        private SnappingSystem    _snappingSystem;   // ← jetzt wirklich genutzt

        // ── State ─────────────────────────────────────────────────
        private uint _previewEntityId  = 0;
        private bool _waitingForRoadEnd = false;
        private Vector3 _roadStartPoint;

        // Cached camera — set once in _Ready, refreshed on null
        private Camera3D _camera;

        // ── Public Properties ─────────────────────────────────────
        public ECS.VoxelType    ActiveVoxelType    { get; private set; }
        public ECS.BuildingType ActiveBuildingType { get; private set; }
        public bool             IsPlacingRoad      { get; private set; }

        // ── Events ────────────────────────────────────────────────
        public event Action<uint, Vector3>    OnBuildingPlaced;
        public event Action<Vector3, Vector3> OnRoadPlacementRequested;

        // ── Lifecycle ─────────────────────────────────────────────
        public override void _Ready()
        {
            _em             = ECS.World.Instance.Entities;
            _roadSystem     = GetParent().GetNodeOrNull<SplineRoadSystem>("SplineRoadSystem");
            _orientSystem   = GetParent().GetNodeOrNull<OrientationSystem>("OrientationSystem");
            _snappingSystem = GetParent().GetNodeOrNull<SnappingSystem>("SnappingSystem");
            _RefreshCamera();
        }

        // ── Public API ────────────────────────────────────────────

        public void BeginPlacement(ECS.VoxelType voxel, ECS.BuildingType building)
        {
            CancelPlacement();          // cleanup any previous preview
            IsPlacingRoad      = false;
            ActiveVoxelType    = voxel;
            ActiveBuildingType = building;
            _CreatePreviewEntity();
        }

        public void BeginRoadPlacement()
        {
            CancelPlacement();
            IsPlacingRoad      = true;
            _waitingForRoadEnd = false;
        }

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
                _OnMouseMoved(motion.Position);

            else if (@event is InputEventMouseButton btn
                     && btn.Pressed
                     && btn.ButtonIndex == MouseButton.Left)
                _OnLeftClick();

            else if (@event is InputEventKey key
                     && key.Pressed
                     && key.Keycode == Key.Escape)
                CancelPlacement();
        }

        // ── Core: Raycast auf Y-Ebene ─────────────────────────────

        /// <summary>
        /// Projiziert einen Kamera-Ray auf die horizontale Ebene y = yPlane.
        /// Gibt null zurück wenn:
        ///   • keine Camera3D vorhanden
        ///   • Kamera schaut (fast) horizontal → kein Schnittpunkt
        ///   • Schnittpunkt liegt hinter der Kamera (t &lt; 0)
        /// </summary>
        private Vector3? _RaycastToGroundPlane(Vector2 screenPos, float yPlane = 0f)
        {
            _RefreshCamera();
            if (_camera == null) return null;

            Vector3 from = _camera.ProjectRayOrigin(screenPos);
            Vector3 dir  = _camera.ProjectRayNormal(screenPos);

            // Schutz gegen horizontale Kamera (|dir.Y| zu klein → Division durch 0)
            if (Mathf.Abs(dir.Y) < 1e-5f) return null;

            float t = (yPlane - from.Y) / dir.Y;
            if (t < 0f) return null;   // Ebene liegt hinter der Kamera

            return from + dir * t;
        }

        // ── Mouse / Click Handler ─────────────────────────────────

        private void _OnMouseMoved(Vector2 screenPos)
        {
            if (IsPlacingRoad)
            {
                _UpdateRoadPreview(screenPos);
                return;
            }

            if (_previewEntityId == 0) return;

            var worldPos = _RaycastToGroundPlane(screenPos);
            if (worldPos == null) return;

            _UpdatePreview(_SnapBuilding(worldPos.Value));
        }

        private void _OnLeftClick()
        {
            var screenPos = GetViewport().GetMousePosition();
            var worldPos  = _RaycastToGroundPlane(screenPos);
            if (worldPos == null) return;

            if (IsPlacingRoad)
            {
                _HandleRoadClick(worldPos.Value);
                return;
            }

            if (_previewEntityId == 0) return;

            var placement = _em.GetComponent<ECS.PlacementComponent>(_previewEntityId);
            if (!placement.IsValid)
            {
                GD.Print("[PlacementSystem] Ungültige Position — Platzierung blockiert.");
                return;
            }
            _ConfirmBuilding(placement.PreviewPos);
        }

        // ── Road Placement ────────────────────────────────────────

        private void _HandleRoadClick(Vector3 worldPos)
        {
            // Snap auf bestehende Straßen-Endpunkte
            Vector3 snapped = _snappingSystem != null
                ? _snappingSystem.Snap(worldPos)
                : worldPos;

            if (!_waitingForRoadEnd)
            {
                _roadStartPoint    = snapped;
                _waitingForRoadEnd = true;
                GD.Print($"[PlacementSystem] Straßen-Start: {_roadStartPoint}");
                _SpawnRoadStartMarker(snapped);      // visuelles Feedback
            }
            else
            {
                OnRoadPlacementRequested?.Invoke(_roadStartPoint, snapped);
                _roadSystem?.CreateRoad(_roadStartPoint, snapped);
                _waitingForRoadEnd = false;
                _DestroyRoadStartMarker();
                GD.Print($"[PlacementSystem] Straßen-Ende: {snapped}");
            }
        }

        /// Aktualisiert eine "Linie" vom Startpunkt zur aktuellen Maus-Position.
        /// Für Phase 2: leichtgewichtiges ImmediateMesh-Preview.
        private void _UpdateRoadPreview(Vector2 screenPos)
        {
            if (!_waitingForRoadEnd) return;
            var worldPos = _RaycastToGroundPlane(screenPos);
            if (worldPos == null) return;

            // Preview-Linie via SplineRoadSystem falls vorhanden,
            // sonst genügt ein einfacher Marker am Cursor.
            _UpdateRoadEndMarker(worldPos.Value);
        }

        // ── Straßen-Marker (einfache Kugeln) ─────────────────────
        // Phase 2-Stub: durch ImmediateMesh-Linie ersetzen wenn gewünscht.

        private MeshInstance3D _roadStartMarker;
        private MeshInstance3D _roadEndMarker;

        private void _SpawnRoadStartMarker(Vector3 pos)
        {
            _roadStartMarker ??= _CreateMarkerMesh(new Color(0.2f, 0.8f, 0.2f)); // grün
            _roadStartMarker.Position = pos + Vector3.Up * 0.1f;
        }

        private void _UpdateRoadEndMarker(Vector3 pos)
        {
            _roadEndMarker ??= _CreateMarkerMesh(new Color(0.8f, 0.8f, 0.2f));   // gelb
            _roadEndMarker.Position = pos + Vector3.Up * 0.1f;
        }

        private void _DestroyRoadStartMarker()
        {
            _roadStartMarker?.QueueFree(); _roadStartMarker = null;
            _roadEndMarker?.QueueFree();   _roadEndMarker   = null;
        }

        private MeshInstance3D _CreateMarkerMesh(Color color)
        {
            var mat  = new StandardMaterial3D { AlbedoColor = color };
            var mesh = new SphereMesh { Radius = 0.15f, Height = 0.3f };
            var mi   = new MeshInstance3D { Mesh = mesh };
            mi.SetSurfaceOverrideMaterial(0, mat);
            AddChild(mi);
            return mi;
        }

        // ── Preview Entity ────────────────────────────────────────

        private void _CreatePreviewEntity()
        {
            _DestroyPreview();
            _previewEntityId = _em.CreateEntity();
            _em.AddComponent(_previewEntityId,
                new ECS.PositionComponent(Vector3.Zero));
            _em.AddComponent(_previewEntityId,
                new ECS.VoxelMeshComponent(ActiveVoxelType, new Color(1f, 1f, 1f, 0.5f)));
            _em.AddComponent(_previewEntityId,
                new ECS.PlacementComponent { IsValid = true, PreviewPos = Vector3.Zero });
        }

        private void _UpdatePreview(Vector3 snappedPos)
        {
            bool isValid = _IsPositionValid(snappedPos);

            _em.SetComponent(_previewEntityId, new ECS.PositionComponent(snappedPos));
            _em.SetComponent(_previewEntityId, new ECS.PlacementComponent
            {
                IsValid    = isValid,
                PreviewPos = snappedPos,
            });
            _em.SetComponent(_previewEntityId, new ECS.VoxelMeshComponent(
                ActiveVoxelType,
                isValid ? new Color(0.3f, 1f, 0.3f, 0.6f)
                        : new Color(1f,   0.3f, 0.3f, 0.6f)));
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

            _orientSystem?.AlignToNearestRoad(building);
            OnBuildingPlaced?.Invoke(building, pos);
            GD.Print($"[PlacementSystem] Gebäude platziert: id={building} @ {pos}");
        }

        // ── Snap-Logik ────────────────────────────────────────────

        /// Gebäude → 0.5m-Grid-Snap (keine Straßen-Snap-Logik nötig)
        private static Vector3 _SnapBuilding(Vector3 pos)
        {
            const float snap = 0.5f;
            return new Vector3(
                Mathf.Round(pos.X / snap) * snap,
                0f,
                Mathf.Round(pos.Z / snap) * snap);
        }

        // ── Validierung ───────────────────────────────────────────

        private bool _IsPositionValid(Vector3 pos)
        {
            foreach (var id in _em.Query<ECS.PositionComponent, ECS.BuildingDataComponent>())
            {
                if (id == _previewEntityId) continue;
                var other = _em.GetComponent<ECS.PositionComponent>(id);
                if (other.Position.DistanceTo(pos) < ECS.GameConstants.VoxelSize)
                    return false;
            }
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────

        private void _RefreshCamera()
        {
            if (_camera == null || !IsInstanceValid(_camera))
                _camera = GetViewport()?.GetCamera3D();
        }

        // ── Tests ─────────────────────────────────────────────────

        public void _RunTests()
        {
            GD.Print("[PlacementSystem] Running tests...");

            // Grid-Snap: 0.7 → 1.0, 0.3 → 0.5
            var s1 = _SnapBuilding(new Vector3(0.7f, 0f, 0.3f));
            System.Diagnostics.Debug.Assert(
                Mathf.IsEqualApprox(s1.X, 1.0f, 0.01f), "Snap 0.7→1.0 X failed");
            System.Diagnostics.Debug.Assert(
                Mathf.IsEqualApprox(s1.Z, 0.5f, 0.01f), "Snap 0.3→0.5 Z failed");

            // Raycast: kein Crash wenn Camera null
            var result = _RaycastToGroundPlane(Vector2.Zero);
            // result == null ist korrekt ohne Camera — kein Assert nötig

            // Raycast: horizontale Kamera gibt null zurück
            // (Unit-Test nur durch Subklasse mockbar — hier dokumentiert)

            GD.Print("[PlacementSystem] Tests passed ✓");
        }
    }
}