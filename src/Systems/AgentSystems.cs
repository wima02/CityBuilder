// ================================================================
// FILE: AgentSystems.cs (Vollständig aktualisiert)
// Phase 3 — Citizen Agents
// Spawnt Bürger (jetzt mit VoxelMesh!) und bewegt sie mit
// sanfter Rotation (LerpAngle) entlang der A*-Wegpunkte.
// ================================================================
using Godot;
using System;

namespace CityBuilder.Systems
{
    // ── Spawner System ────────────────────────────────────────────
    public partial class AgentSpawnerSystem : Node
    {
        private ECS.EntityManager _em;
        private PathfindingSystem _pathfinding;

        public float SpawnIntervalSec { get; set; } = 15f;
        private float _timer = 0f;

        public override void _Ready()
        {
            _em          = ECS.World.Instance.Entities;
            _pathfinding = GetParent().GetNodeOrNull<PathfindingSystem>("PathfindingSystem");
        }

        public override void _Process(double delta)
        {
            _timer += (float)delta;
            if (_timer < SpawnIntervalSec) return;
            _timer = 0f;
            _TrySpawnCitizen();
        }

        private void _TrySpawnCitizen()
        {
            uint stationId = _FindTrainStation();
            if (stationId == 0) return; // Kein Bahnhof = kein Spawn

            uint houseId = _FindFreeDwelling();
            var stationPos = _em.GetComponent<ECS.PositionComponent>(stationId).Position;

            uint citizenId = _em.CreateEntity();
            
            // Sichtbarkeit herstellen!
            _em.AddComponent(citizenId, new ECS.PositionComponent(stationPos));
            _em.AddComponent(citizenId, new ECS.VoxelMeshComponent(
                ECS.VoxelType.Citizen, new Color(0.9f, 0.4f, 0.4f))); // Rot/Orange für Kontrast

            _em.AddComponent(citizenId, new ECS.AgentComponent
            {
                State             = ECS.AgentState.Idle,
                HomeEntityId      = houseId,
                WorkplaceEntityId = 0,
                TargetPosition    = houseId != 0
                    ? _em.GetComponent<ECS.PositionComponent>(houseId).Position
                    : stationPos,
                MoveSpeed = 2.5f,
            });

            _em.AddComponent(citizenId, new ECS.NeedsComponent
            {
                Food = 0.8f, Clothing = 0.8f, Entertainment = 0.5f,
                Shelter = houseId != 0 ? 1.0f : 0.0f,
            });

            _em.AddComponent(citizenId, new ECS.HappinessComponent
            {
                Value = 0.7f, BeautifulScore = 0f
            });

            if (houseId != 0)
            {
                _OccupyDwelling(houseId);
                _pathfinding?.RequestPath(citizenId, stationPos,
                    _em.GetComponent<ECS.PositionComponent>(houseId).Position);
                
                // Setze State sofort auf Walking, damit MovementSystem greift
                var agent = _em.GetComponent<ECS.AgentComponent>(citizenId);
                agent.State = ECS.AgentState.Walking;
                _em.SetComponent(citizenId, agent);
            }

            GD.Print($"[AgentSpawner] Bürger {citizenId} gespawnt — home={houseId}");
        }

        private uint _FindTrainStation()
        {
            foreach (var id in _em.Query<ECS.BuildingDataComponent>())
                if (_em.GetComponent<ECS.BuildingDataComponent>(id).Type == ECS.BuildingType.TrainStation)
                    return id;
            return 0;
        }

        private uint _FindFreeDwelling()
        {
            foreach (var id in _em.Query<ECS.BuildingDataComponent>())
            {
                var bd = _em.GetComponent<ECS.BuildingDataComponent>(id);
                if (bd.Type == ECS.BuildingType.Dwelling && bd.OccupiedBy < bd.Capacity)
                    return id;
            }
            return 0; 
        }

        private void _OccupyDwelling(uint houseId)
        {
            var bd = _em.GetComponent<ECS.BuildingDataComponent>(houseId);
            bd.OccupiedBy++;
            _em.SetComponent(houseId, bd);
        }
    }

    // ── Movement System ───────────────────────────────────────────
    public partial class AgentMovementSystem : Node
    {
        private ECS.EntityManager _em;

        public event Action<uint> OnAgentArrived;

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        public override void _Process(double delta)
        {
            foreach (var id in _em.Query<ECS.AgentComponent, ECS.PathComponent, ECS.PositionComponent>())
            {
                var agent = _em.GetComponent<ECS.AgentComponent>(id);
                if (agent.State != ECS.AgentState.Walking) continue;

                _AdvanceAlongPath(id, (float)delta);
            }
        }

        private void _AdvanceAlongPath(uint id, float dt)
        {
            var path = _em.GetComponent<ECS.PathComponent>(id);
            if (path.Waypoints == null || path.CurrentStep >= path.Waypoints.Length)
            {
                _ArriveAtDestination(id);
                return;
            }

            var pos    = _em.GetComponent<ECS.PositionComponent>(id);
            var agent  = _em.GetComponent<ECS.AgentComponent>(id);
            var target = path.Waypoints[path.CurrentStep];
            var dir    = (target - pos.Position);
            float dist = dir.Length();
            float step = agent.MoveSpeed * dt;

            if (step >= dist)
            {
                pos.Position = target;
                path.CurrentStep++;
            }
            else
            {
                pos.Position += dir.Normalized() * step;
            }

            // Weiche Rotation in Laufrichtung
            if (dir.LengthSquared() > 0.001f)
            {
                float currentRad = Mathf.DegToRad(pos.RotationY);
                float targetRad  = Mathf.Atan2(dir.X, dir.Z);
                // LerpAngle löst das Problem des "Rundum-Drehens" bei Übergang von 359° zu 1°
                float newRad     = Mathf.LerpAngle(currentRad, targetRad, 10f * dt);
                pos.RotationY    = Mathf.RadToDeg(newRad);
            }

            _em.SetComponent(id, pos);
            _em.SetComponent(id, path);
        }

        private void _ArriveAtDestination(uint id)
        {
            var agent   = _em.GetComponent<ECS.AgentComponent>(id);
            agent.State = ECS.AgentState.Idle;
            _em.SetComponent(id, agent);
            _em.RemoveComponent<ECS.PathComponent>(id);
            OnAgentArrived?.Invoke(id);
            
            GD.Print($"[AgentMovement] Bürger {id} hat sein Ziel erreicht.");
        }
    }
}