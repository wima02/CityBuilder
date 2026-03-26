// ================================================================
// FILE: SplineRoadSystem.cs  (vollständig ersetzt)
// Phase 2 — Gridless Building
// Bezier-Spline-Straßen mit:
//   • UV-Mapping (akkumulierte Bogenlänge → nahtloses Tiling)
//   • Explizite Vertex-Normalen (smooth Shading)
//   • Shoulder Vertices (Randsteine / Bordstein-Effekt)
// ================================================================
using Godot;
using System;
using System.Collections.Generic;

namespace CityBuilder.Systems
{
    public partial class SplineRoadSystem : Node3D
    {
        // ── Konstanten ────────────────────────────────────────────
        private const int   BezierResolution  = 24;    // Punkte entlang der Kurve
        private const float ShoulderWidth     = 0.25f; // Bordstein-Breite in Metern
        private const float ShoulderDrop      = 0.08f; // Bordstein-Absenkung in Metern
        private const float TextureTileLength = 2.0f;  // UV-Y wiederholt alle N Meter

        // UV-X Positionen der 5 Vertex-Spuren (normalisiert 0…1)
        private const float UvShoulderL = 0.00f;
        private const float UvEdgeL     = 0.12f;
        private const float UvCenter    = 0.50f;
        private const float UvEdgeR     = 0.88f;
        private const float UvShoulderR = 1.00f;

        // ── Dependencies ──────────────────────────────────────────
        private ECS.EntityManager _em;

        public event Action<uint> OnRoadCreated;

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        // ── Public API ────────────────────────────────────────────

        public uint CreateRoad(Vector3 start, Vector3 end)
        {
            uint roadId = _em.CreateEntity();
            var  ctrl   = _AutoControlPoints(start, end);

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
            _em.AddComponent(roadId, new ECS.VoxelMeshComponent(
                ECS.VoxelType.Road, new Color(0.55f, 0.52f, 0.48f)));

            _BuildRoadMesh(roadId);
            OnRoadCreated?.Invoke(roadId);

            GD.Print($"[SplineRoadSystem] Straße erstellt: id={roadId}  {start} → {end}");
            return roadId;
        }

        public void DestroyRoad(uint roadId)
        {
            GetNodeOrNull<Node>($"Road_{roadId}")?.QueueFree();
            _em.DestroyEntity(roadId);
        }

        // ── Bezier Sampling ───────────────────────────────────────

        public static Vector3 SampleBezier(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u  = 1f - t;
            float u2 = u * u,  u3 = u2 * u;
            float t2 = t * t,  t3 = t2 * t;
            return u3*p0 + 3f*u2*t*p1 + 3f*u*t2*p2 + t3*p3;
        }

        /// Tangente (erste Ableitung) des kubischen Bezier bei t
        private static Vector3 _SampleBezierTangent(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u  = 1f - t;
            // B'(t) = 3[(p1-p0)(1-t)² + 2(p2-p1)(1-t)t + (p3-p2)t²]
            return 3f * (u*u*(p1-p0) + 2f*u*t*(p2-p1) + t*t*(p3-p2));
        }

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

        // ── Mesh-Generierung ──────────────────────────────────────

        private void _BuildRoadMesh(uint roadId)
        {
            var road  = _em.GetComponent<ECS.RoadComponent>(roadId);
            float half = road.Width * 0.5f;

            // ① Alle Quer-Profile vorberechnen
            var profiles = _BuildProfiles(road, half);

            // ② Mesh aus Profilen zusammensetzen
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            for (int i = 0; i < profiles.Count - 1; i++)
                _AddSegmentQuads(st, profiles[i], profiles[i + 1]);

            // Kein GenerateNormals() — Normalen sind bereits explizit gesetzt
            var mesh = st.Commit();

            // Material mit Vertex-Color-Unterstützung
            var mat = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, // beide Seiten
            };
            mesh.SurfaceSetMaterial(0, mat);

            var mi = new MeshInstance3D { Mesh = mesh, Name = $"Road_{roadId}" };
            AddChild(mi);
        }

        // ── Profil-Berechnung ─────────────────────────────────────

        /// Ein Quer-Profil mit 5 Vertices an einer Bogenstelle
        private readonly struct RoadProfile
        {
            public readonly Vector3[] Positions; // [0..4]
            public readonly Vector3[] Normals;   // [0..4]
            public readonly float     UvY;       // akkumulierte Länge / TileLength

            public RoadProfile(Vector3[] pos, Vector3[] nor, float uvY)
            {
                Positions = pos;
                Normals   = nor;
                UvY       = uvY;
            }
        }

        private List<RoadProfile> _BuildProfiles(ECS.RoadComponent road, float half)
        {
            var profiles   = new List<RoadProfile>(BezierResolution + 1);
            float arcLen   = 0f;
            Vector3 prevPt = road.StartPoint;

            for (int i = 0; i <= BezierResolution; i++)
            {
                float t = i / (float)BezierResolution;

                Vector3 pt  = SampleBezier(
                    road.StartPoint, road.ControlPoint1,
                    road.ControlPoint2, road.EndPoint, t);

                Vector3 tan = _SampleBezierTangent(
                    road.StartPoint, road.ControlPoint1,
                    road.ControlPoint2, road.EndPoint, t).Normalized();

                // Absicherung gegen Null-Tangente (Start/End-Degeneration)
                if (tan.LengthSquared() < 0.001f)
                    tan = (road.EndPoint - road.StartPoint).Normalized();

                // Bogenlänge akkumulieren (für UV.Y)
                arcLen += pt.DistanceTo(prevPt);
                prevPt  = pt;

                float uvY = arcLen / TextureTileLength;

                // Rechts-Vektor in der XZ-Ebene (Straße liegt auf Y=0)
                Vector3 right = new Vector3(tan.Z, 0f, -tan.X).Normalized();

                // Schulter-Normale leicht nach außen-unten
                Vector3 shoulderNormL = (-right + Vector3.Down * 0.5f).Normalized();
                Vector3 shoulderNormR = ( right + Vector3.Down * 0.5f).Normalized();

                var pos = new Vector3[5];
                var nor = new Vector3[5];

                float sw = ShoulderWidth;
                float sd = ShoulderDrop;

                // [0] linke Schulter
                pos[0] = pt - right * (half + sw) + Vector3.Down * sd;
                nor[0] = shoulderNormL;

                // [1] linke Fahrbahnkante
                pos[1] = pt - right * half;
                nor[1] = Vector3.Up;

                // [2] Mitte
                pos[2] = pt;
                nor[2] = Vector3.Up;

                // [3] rechte Fahrbahnkante
                pos[3] = pt + right * half;
                nor[3] = Vector3.Up;

                // [4] rechte Schulter
                pos[4] = pt + right * (half + sw) + Vector3.Down * sd;
                nor[4] = shoulderNormR;

                profiles.Add(new RoadProfile(pos, nor, uvY));
            }

            return profiles;
        }

        // ── Quad-Streifen zusammensetzen ──────────────────────────

        // UV-X Werte für die 5 Spuren
        private static readonly float[] UvX =
            { UvShoulderL, UvEdgeL, UvCenter, UvEdgeR, UvShoulderR };

        /// Verbindet zwei aufeinanderfolgende Profile mit 4 Quad-Streifen.
        /// Jeder Quad = 2 Dreiecke, 6 Vertices (kein Index-Buffer nötig für SurfaceTool).
        private static void _AddSegmentQuads(
            SurfaceTool st,
            RoadProfile a,   // Profil bei i
            RoadProfile b)   // Profil bei i+1
        {
            // 4 Streifen: [0-1], [1-2], [2-3], [3-4]
            for (int lane = 0; lane < 4; lane++)
            {
                //  a[lane]──────b[lane]
                //     |  \         |
                //     |   \        |
                //  a[lane+1]────b[lane+1]

                Vector3 a0 = a.Positions[lane],     a1 = a.Positions[lane + 1];
                Vector3 b0 = b.Positions[lane],     b1 = b.Positions[lane + 1];
                Vector3 n_a0 = a.Normals[lane],     n_a1 = a.Normals[lane + 1];
                Vector3 n_b0 = b.Normals[lane],     n_b1 = b.Normals[lane + 1];
                float   ux0 = UvX[lane],            ux1 = UvX[lane + 1];

                // Dreieck 1: a0, b0, a1
                _AddVert(st, a0, n_a0, new Vector2(ux0, a.UvY));
                _AddVert(st, b0, n_b0, new Vector2(ux0, b.UvY));
                _AddVert(st, a1, n_a1, new Vector2(ux1, a.UvY));

                // Dreieck 2: b0, b1, a1
                _AddVert(st, b0, n_b0, new Vector2(ux0, b.UvY));
                _AddVert(st, b1, n_b1, new Vector2(ux1, b.UvY));
                _AddVert(st, a1, n_a1, new Vector2(ux1, a.UvY));
            }
        }

        private static void _AddVert(
            SurfaceTool st, Vector3 pos, Vector3 normal, Vector2 uv)
        {
            st.SetNormal(normal);
            st.SetUV(uv);
            st.AddVertex(pos);
        }

        // ── Auto-Kontrollpunkte ───────────────────────────────────

        private static (Vector3, Vector3) _AutoControlPoints(Vector3 start, Vector3 end)
        {
            Vector3 dir  = end - start;
            float   len  = dir.Length();
            Vector3 perp = new Vector3(-dir.Z, 0f, dir.X).Normalized() * (len * 0.25f);
            return (start + dir * 0.33f + perp,
                    start + dir * 0.66f - perp);
        }

        // ── Tests ─────────────────────────────────────────────────

        public void _RunTests()
        {
            GD.Print("[SplineRoadSystem] Running tests...");

            // Bezier-Mittelpunkt auf gerader Linie muss X=1.5 sein
            var mid = SampleBezier(
                Vector3.Zero,
                new Vector3(1, 0, 0),
                new Vector3(2, 0, 0),
                new Vector3(3, 0, 0), 0.5f);
            System.Diagnostics.Debug.Assert(
                Mathf.IsEqualApprox(mid.X, 1.5f, 0.01f),
                "Bezier-Mittelpunkt X sollte ~1.5 sein");

            // Tangente bei t=0 zeigt in Richtung p1-p0
            var tan = _SampleBezierTangent(
                Vector3.Zero,
                new Vector3(1, 0, 0),
                new Vector3(2, 0, 0),
                new Vector3(3, 0, 0), 0f);
            System.Diagnostics.Debug.Assert(
                tan.X > 0f, "Tangente bei t=0 sollte in +X zeigen");

            // Profile-Anzahl muss BezierResolution+1 sein
            var road = new ECS.RoadComponent
            {
                StartPoint    = Vector3.Zero,
                EndPoint      = new Vector3(5, 0, 0),
                ControlPoint1 = new Vector3(1, 0, 1),
                ControlPoint2 = new Vector3(4, 0, -1),
                Width         = ECS.GameConstants.RoadWidth,
            };
            var profiles = _BuildProfiles(road, road.Width * 0.5f);
            System.Diagnostics.Debug.Assert(
                profiles.Count == BezierResolution + 1,
                $"Profil-Anzahl sollte {BezierResolution + 1} sein");

            // UV.Y muss monoton wachsen
            for (int i = 1; i < profiles.Count; i++)
                System.Diagnostics.Debug.Assert(
                    profiles[i].UvY >= profiles[i - 1].UvY,
                    $"UV.Y muss monoton wachsen (Schritt {i})");

            GD.Print("[SplineRoadSystem] Tests passed ✓");
        }
    }
}