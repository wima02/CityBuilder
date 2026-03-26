// ================================================================
// FILE: OrientationSystem.cs
// Phase 2 — Gridless Building
// Auto-aligns buildings to face the nearest road after placement.
// Refine: handle corner buildings, add multi-road facing logic.
// ================================================================
using Godot;
using System;

namespace CityBuilder.Systems
{
    public partial class OrientationSystem : Node
    {
        private ECS.EntityManager _em;

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        /// Rotates the building entity to face the nearest road segment.
        /// Called by PlacementSystem after confirming a building.
        public void AlignToNearestRoad(uint buildingId)
        {
            if (!_em.HasComponent<ECS.PositionComponent>(buildingId))
            {
                GD.PushWarning($"[OrientationSystem] Entity {buildingId} has no PositionComponent.");
                return;
            }

            var buildingPos = _em.GetComponent<ECS.PositionComponent>(buildingId).Position;

            uint  nearestRoad = 0;
            float minDist     = float.MaxValue;
            Vector3 nearestPoint = Vector3.Zero;

            foreach (var roadId in _em.Query<ECS.RoadComponent>())
            {
                var road  = _em.GetComponent<ECS.RoadComponent>(roadId);
                var close = _ClosestPointOnSegment(buildingPos, road.StartPoint, road.EndPoint);
                float d   = buildingPos.DistanceTo(close);

                if (d < minDist)
                {
                    minDist      = d;
                    nearestRoad  = roadId;
                    nearestPoint = close;
                }
            }

            if (nearestRoad == 0) return; // no roads yet — keep default rotation

            // Face toward the nearest point on the road
            var dir    = (nearestPoint - buildingPos).Normalized();
            var rotY   = Mathf.RadToDeg(Mathf.Atan2(dir.X, dir.Z));

            var pos    = _em.GetComponent<ECS.PositionComponent>(buildingId);
            pos.RotationY = rotY;
            _em.SetComponent(buildingId, pos);

            // Cache the road reference on the building
            if (_em.HasComponent<ECS.BuildingDataComponent>(buildingId))
            {
                var bd = _em.GetComponent<ECS.BuildingDataComponent>(buildingId);
                bd.NearestRoadId = nearestRoad;
                _em.SetComponent(buildingId, bd);
            }
        }

        // ── Helpers ───────────────────────────────────────────────

        private static Vector3 _ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab   = b - a;
            float t  = (point - a).Dot(ab) / ab.LengthSquared();
            t        = Mathf.Clamp(t, 0f, 1f);
            return a + ab * t;
        }

        public void _RunTests()
        {
            GD.Print("[OrientationSystem] Running tests...");

            // Closest point on segment
            var cp = _ClosestPointOnSegment(
                new Vector3(0f, 0f, 1f),
                new Vector3(-2f, 0f, 0f),
                new Vector3(2f, 0f, 0f));
            System.Diagnostics.Debug.Assert(
                Mathf.IsEqualApprox(cp.X, 0f, 0.01f), "ClosestPoint X failed");
            System.Diagnostics.Debug.Assert(
                Mathf.IsEqualApprox(cp.Z, 0f, 0.01f), "ClosestPoint Z failed");

            GD.Print("[OrientationSystem] Tests passed ✓");
        }
    }
}


// ================================================================
// FILE: SnappingSystem.cs
// Phase 2 — Gridless Building
// Snaps road endpoints to existing road endpoints within snap radius,
// preventing T-junction gaps and floating roads.
// Refine: also snap to road midpoints for branching intersections.
// ================================================================
namespace CityBuilder.Systems
{
    public partial class SnappingSystem : Godot.Node
    {
        private ECS.EntityManager _em;

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        /// Returns the snapped position: either the input position,
        /// or an existing road endpoint if one is within snap radius.
        public Godot.Vector3 Snap(Godot.Vector3 inputPos)
        {
            float snapRadius = ECS.GameConstants.RoadSnapRadius;
            float bestDist   = snapRadius;
            Godot.Vector3 best = inputPos;

            foreach (var roadId in _em.Query<ECS.RoadComponent>())
            {
                var road = _em.GetComponent<ECS.RoadComponent>(roadId);

                float dStart = inputPos.DistanceTo(road.StartPoint);
                if (dStart < bestDist)
                {
                    bestDist = dStart;
                    best     = road.StartPoint;
                }

                float dEnd = inputPos.DistanceTo(road.EndPoint);
                if (dEnd < bestDist)
                {
                    bestDist = dEnd;
                    best     = road.EndPoint;
                }
            }

            return best;
        }

        /// Returns the entity ID of the road whose endpoint we snapped to,
        /// or 0 if no snap occurred. Used by SplineRoadSystem for junction merging.
        public uint FindSnapTarget(Godot.Vector3 inputPos)
        {
            float snapRadius = ECS.GameConstants.RoadSnapRadius;

            foreach (var roadId in _em.Query<ECS.RoadComponent>())
            {
                var road = _em.GetComponent<ECS.RoadComponent>(roadId);
                if (inputPos.DistanceTo(road.StartPoint) <= snapRadius) return roadId;
                if (inputPos.DistanceTo(road.EndPoint)   <= snapRadius) return roadId;
            }
            return 0;
        }

        public void _RunTests()
        {
            Godot.GD.Print("[SnappingSystem] Tests require a running scene — skipped in unit mode.");
        }
    }
}
