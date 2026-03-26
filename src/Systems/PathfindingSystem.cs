// ================================================================
// FILE: PathfindingSystem.cs
// Phase 3 — Citizen Agents
// A* pathfinding on a virtual 1m grid.
// Writes PathComponent onto requesting entities.
//
// Refine: add road-preference weighting, diagonal movement,
//         or swap to Godot NavigationServer for NavMesh support.
// ================================================================
using Godot;
using System;
using System.Collections.Generic;

namespace CityBuilder.Systems
{
    public partial class PathfindingSystem : Node
    {
        private ECS.EntityManager _em;

        // Virtual grid cell size matches GameConstants
        private readonly float _cellSize = ECS.GameConstants.PathGridCellSize;

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        // ── Public API ────────────────────────────────────────────

        /// Calculates a path for an entity and writes it into PathComponent.
        /// Call from AgentSpawnerSystem or NeedsCycleSystem.
        public bool RequestPath(uint entityId, Vector3 from, Vector3 to)
        {
            var path = _FindPath(from, to);
            if (path == null)
            {
                GD.PushWarning($"[PathfindingSystem] No path found for entity {entityId}");
                return false;
            }

            if (_em.HasComponent<ECS.PathComponent>(entityId))
                _em.SetComponent(entityId, new ECS.PathComponent { Waypoints = path, CurrentStep = 0 });
            else
                _em.AddComponent(entityId, new ECS.PathComponent { Waypoints = path, CurrentStep = 0 });

            return true;
        }

        // ── A* Core ───────────────────────────────────────────────

        private Vector3[]? _FindPath(Vector3 startWorld, Vector3 endWorld)
        {
            var start = _WorldToGrid(startWorld);
            var end   = _WorldToGrid(endWorld);

            if (start == end)
                return new[] { endWorld };

            var open   = new SortedList<float, Vector2I>(new _DuplicateKeyComparer());
            var cameFrom = new Dictionary<Vector2I, Vector2I>();
            var gScore   = new Dictionary<Vector2I, float> { [start] = 0f };

            open.Add(_Heuristic(start, end), start);

            int iterations = 0;

            while (open.Count > 0 && iterations++ < ECS.GameConstants.PathMaxIterations)
            {
                var current = open.Values[0];
                open.RemoveAt(0);

                if (current == end)
                    return _ReconstructPath(cameFrom, current, endWorld);

                foreach (var neighbor in _GetNeighbors(current))
                {
                    float tentativeG = gScore.GetValueOrDefault(current, float.MaxValue) + 1f;

                    if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor]   = tentativeG;
                        float f            = tentativeG + _Heuristic(neighbor, end);
                        open.Add(f, neighbor);
                    }
                }
            }

            return null; // no path found within iteration limit
        }

        private Vector3[] _ReconstructPath(
            Dictionary<Vector2I, Vector2I> cameFrom,
            Vector2I current, Vector3 exactEnd)
        {
            var path = new List<Vector3> { exactEnd };
            while (cameFrom.TryGetValue(current, out var prev))
            {
                path.Add(_GridToWorld(current));
                current = prev;
            }
            path.Reverse();
            return path.ToArray();
        }

        // ── Grid Helpers ──────────────────────────────────────────

        private Vector2I _WorldToGrid(Vector3 world)
            => new((int)Mathf.Floor(world.X / _cellSize),
                   (int)Mathf.Floor(world.Z / _cellSize));

        private Vector3 _GridToWorld(Vector2I cell)
            => new(cell.X * _cellSize + _cellSize * 0.5f,
                   0f,
                   cell.Y * _cellSize + _cellSize * 0.5f);

        private static float _Heuristic(Vector2I a, Vector2I b)
            => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y); // Manhattan distance

        private static IEnumerable<Vector2I> _GetNeighbors(Vector2I cell)
        {
            yield return new Vector2I(cell.X + 1, cell.Y);
            yield return new Vector2I(cell.X - 1, cell.Y);
            yield return new Vector2I(cell.X, cell.Y + 1);
            yield return new Vector2I(cell.X, cell.Y - 1);
        }

        // SortedList requires unique keys — this comparer allows duplicates
        private class _DuplicateKeyComparer : IComparer<float>
        {
            public int Compare(float x, float y)
                => x <= y ? -1 : 1;
        }

        // ── Tests ─────────────────────────────────────────────────

        public void _RunTests()
        {
            GD.Print("[PathfindingSystem] Running tests...");

            // Direct neighbor path
            var path = _FindPath(Vector3.Zero, new Vector3(3f, 0f, 0f));
            System.Diagnostics.Debug.Assert(path != null, "Path should exist for open space");
            System.Diagnostics.Debug.Assert(path!.Length > 0, "Path should have waypoints");

            // Same cell
            var samePath = _FindPath(Vector3.Zero, Vector3.Zero);
            System.Diagnostics.Debug.Assert(
                samePath != null && samePath.Length == 1,
                "Same-cell path should return single waypoint");

            GD.Print("[PathfindingSystem] Tests passed ✓");
        }
    }
}
