// ================================================================
// FILE: SplineRoadSystem.cs
// Phase 2 — Gridless Building
// Handles Bezier-curve road placement and mesh generation.
// Listens for RoadPlacementRequested events from PlacementSystem.
//
// Refine: tune bezier resolution, road width, material assignment.
// ================================================================
using Godot;
using System;
using System.Collections.Generic;

namespace CityBuilder.Systems
{
    public partial class SplineRoadSystem : Node3D
    {
        private ECS.EntityManager _em;

        // Raised by PlacementSystem when user confirms a road segment
        public event Action<uint> OnRoadCreated;

        // How many intermediate points to sample along the Bezier curve
        private const int BezierResolution = 16;

        public override void _Ready()
        {
            _em = ECS.World.Instance.Entities;
        }

        // ── Public API ────────────────────────────────────────────

        /// Creates a road entity from two world-space endpoints.
        /// Control points are auto-generated for smooth S-curves.
        public uint CreateRoad(Vector3 start, Vector3 end)
        {
            uint roadId = _em.CreateEntity();

            var ctrl = _AutoControlPoints(start, end);

            _em.AddComponent(roadId, new ECS.PositionComponent(start));
            _em.AddComponent(roadId, new ECS.RoadComponent
            {
                StartPoint    = start,
                EndPoint      = end,
                ControlPoint1 = ctrl.Item1,
                ControlPoint2 = ctrl.Item2,
                Shape         = ECS.RoadShape.Curve,
                Width         = ECS.GameConstants.RoadWidth,
            });
            _em.AddComponent(roadId, new ECS.VoxelMeshComponent(ECS.VoxelType.Road,
                new Color(0.55f, 0.52f, 0.48f)));

            _BuildRoadMesh(roadId);
            OnRoadCreated?.Invoke(roadId);

            GD.Print($"[SplineRoadSystem] Road created: id={roadId} from {start} to {end}");
            return roadId;
        }

        /// Removes a road entity and its mesh node.
        public void DestroyRoad(uint roadId)
        {
            var meshNode = GetNodeOrNull<Node>($"Road_{roadId}");
            meshNode?.QueueFree();
            _em.DestroyEntity(roadId);
        }

        // ── Bezier Helpers ────────────────────────────────────────

        /// Samples a cubic Bezier at t ∈ [0,1]
        public static Vector3 SampleBezier(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u  = 1f - t;
            float u2 = u * u;
            float u3 = u2 * u;
            float t2 = t * t;
            float t3 = t2 * t;

            return u3 * p0
                 + 3f * u2 * t  * p1
                 + 3f * u  * t2 * p2
                 +      t3      * p3;
        }

        /// Returns a list of world-space points along a road entity's spline.
        public List<Vector3> GetRoadPoints(uint roadId)
        {
            var road   = _em.GetComponent<ECS.RoadComponent>(roadId);
            var points = new List<Vector3>(BezierResolution + 1);

            for (int i = 0; i <= BezierResolution; i++)
            {
                float t = i / (float)BezierResolution;
                points.Add(SampleBezier(
                    road.StartPoint, road.ControlPoint1,
                    road.ControlPoint2, road.EndPoint, t));
            }
            return points;
        }

        // ── Mesh Generation ───────────────────────────────────────
        // Phase 1: extrudes a flat ribbon mesh along the Bezier spine.
        // Phase 2 refine: add UV mapping, normal smoothing, shoulder verts.

        private void _BuildRoadMesh(uint roadId)
        {
            var points = GetRoadPoints(roadId);
            var road   = _em.GetComponent<ECS.RoadComponent>(roadId);

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            float half = road.Width / 2f;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 dir    = (points[i + 1] - points[i]).Normalized();
                Vector3 right  = dir.Cross(Vector3.Up).Normalized() * half;

                Vector3 bl = points[i]     - right;
                Vector3 br = points[i]     + right;
                Vector3 tl = points[i + 1] - right;
                Vector3 tr = points[i + 1] + right;

                // Triangle 1
                st.AddVertex(bl); st.AddVertex(br); st.AddVertex(tl);
                // Triangle 2
                st.AddVertex(br); st.AddVertex(tr); st.AddVertex(tl);
            }

            st.GenerateNormals();
            var mesh = st.Commit();

            var mi   = new MeshInstance3D { Mesh = mesh, Name = $"Road_{roadId}" };
            AddChild(mi);
        }

        // ── Auto Control Points ───────────────────────────────────
        // Offset control points perpendicular to the road direction
        // so default roads have a gentle S-curve, not a sharp angle.

        private static (Vector3, Vector3) _AutoControlPoints(Vector3 start, Vector3 end)
        {
            Vector3 dir   = (end - start);
            float   len   = dir.Length();
            Vector3 perp  = new Vector3(-dir.Z, 0f, dir.X).Normalized() * (len * 0.25f);

            return (start + dir * 0.33f + perp,
                    start + dir * 0.66f - perp);
        }

        // ── Tests ─────────────────────────────────────────────────

        public void _RunTests()
        {
            GD.Print("[SplineRoadSystem] Running tests...");

            // Bezier midpoint should lie between start and end
            var mid = SampleBezier(
                Vector3.Zero,
                new Vector3(1, 0, 0),
                new Vector3(2, 0, 0),
                new Vector3(3, 0, 0), 0.5f);

            System.Diagnostics.Debug.Assert(
                Mathf.IsEqualApprox(mid.X, 1.5f, 0.01f),
                "Bezier midpoint X should be ~1.5");

            GD.Print("[SplineRoadSystem] Tests passed ✓");
        }
    }
}
