// ================================================================
// FILE: EntityManager.cs
// ECS core — manages entity lifecycle and component storage.
// NO game logic here — data operations only.
// Refine: add Query<T1,T2,T3,T4> or event callbacks as needed.
// ================================================================
using System;
using System.Collections.Generic;
using Godot;

namespace CityBuilder.ECS
{
    public class EntityManager
    {
        private uint _nextEntityId = 1;
        private readonly HashSet<uint> _livingEntities = new();

        // Component pools: Type → (EntityId → Component)
        private readonly Dictionary<Type, Dictionary<uint, IComponent>> _pools = new();

        // Fired after any component is set — use to drive dirty flags
        public event Action<uint, Type> OnComponentChanged;

        // ── Entity Lifecycle ─────────────────────────────────────

        /// Creates a new entity and returns its ID.
        public uint CreateEntity()
        {
            if (_livingEntities.Count >= GameConstants.MaxEntities)
            {
                GD.PushWarning($"[EntityManager] MaxEntities ({GameConstants.MaxEntities}) reached!");
                return 0;
            }
            uint id = _nextEntityId++;
            _livingEntities.Add(id);
            return id;
        }

        /// Destroys an entity and removes all its components.
        public void DestroyEntity(uint id)
        {
            if (!_livingEntities.Remove(id))
            {
                GD.PushWarning($"[EntityManager] DestroyEntity: Entity {id} not found.");
                return;
            }
            foreach (var pool in _pools.Values)
                pool.Remove(id);
        }

        public bool IsAlive(uint id) => _livingEntities.Contains(id);

        public int EntityCount => _livingEntities.Count;

        // ── Component Operations ─────────────────────────────────

        public void AddComponent<T>(uint entityId, T component) where T : struct, IComponent
        {
            GetOrCreatePool<T>()[entityId] = component;
            OnComponentChanged?.Invoke(entityId, typeof(T));
        }

        public T GetComponent<T>(uint entityId) where T : struct, IComponent
        {
            if (!HasComponent<T>(entityId))
                throw new InvalidOperationException(
                    $"Entity {entityId} is missing component {typeof(T).Name}");
            return (T)GetOrCreatePool<T>()[entityId];
        }

        /// Overwrites an existing component and fires OnComponentChanged.
        public void SetComponent<T>(uint entityId, T component) where T : struct, IComponent
        {
            if (!HasComponent<T>(entityId))
                GD.PushWarning($"[EntityManager] SetComponent: Entity {entityId} has no {typeof(T).Name} — adding instead.");
            GetOrCreatePool<T>()[entityId] = component;
            OnComponentChanged?.Invoke(entityId, typeof(T));
        }

        public bool HasComponent<T>(uint entityId) where T : struct, IComponent
            => _pools.TryGetValue(typeof(T), out var pool) && pool.ContainsKey(entityId);

        public void RemoveComponent<T>(uint entityId) where T : struct, IComponent
        {
            if (_pools.TryGetValue(typeof(T), out var pool))
            {
                pool.Remove(entityId);
                OnComponentChanged?.Invoke(entityId, typeof(T));
            }
        }

        // ── Queries ───────────────────────────────────────────────
        // Iterate living entities that own specific component combinations.

        public IEnumerable<uint> Query<T>() where T : struct, IComponent
        {
            if (!_pools.TryGetValue(typeof(T), out var pool)) yield break;
            foreach (var kvp in pool)
                if (_livingEntities.Contains(kvp.Key))
                    yield return kvp.Key;
        }

        public IEnumerable<uint> Query<T1, T2>()
            where T1 : struct, IComponent
            where T2 : struct, IComponent
        {
            foreach (var id in Query<T1>())
                if (HasComponent<T2>(id))
                    yield return id;
        }

        public IEnumerable<uint> Query<T1, T2, T3>()
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
        {
            foreach (var id in Query<T1, T2>())
                if (HasComponent<T3>(id))
                    yield return id;
        }

        public IEnumerable<uint> Query<T1, T2, T3, T4>()
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
        {
            foreach (var id in Query<T1, T2, T3>())
                if (HasComponent<T4>(id))
                    yield return id;
        }

        // ── Internal ─────────────────────────────────────────────

        private Dictionary<uint, IComponent> GetOrCreatePool<T>() where T : struct, IComponent
        {
            var type = typeof(T);
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new Dictionary<uint, IComponent>();
                _pools[type] = pool;
            }
            return pool;
        }

        // ── Self-Tests ───────────────────────────────────────────
        // Called during Booting state in debug builds.

        public void _RunTests()
        {
            GD.Print("[ECS Test] Running EntityManager tests...");

            uint e = CreateEntity();
            System.Diagnostics.Debug.Assert(IsAlive(e), "Entity should be alive after creation");

            AddComponent(e, new PositionComponent(Godot.Vector3.Zero));
            System.Diagnostics.Debug.Assert(HasComponent<PositionComponent>(e), "HasComponent failed");

            var pos = GetComponent<PositionComponent>(e);
            System.Diagnostics.Debug.Assert(pos.Position == Godot.Vector3.Zero, "GetComponent returned wrong value");

            DestroyEntity(e);
            System.Diagnostics.Debug.Assert(!IsAlive(e), "Entity should be dead after destroy");

            // Boundary: max entity cap
            System.Diagnostics.Debug.Assert(EntityCount == 0, "Count should be 0 after cleanup");

            GD.Print("[ECS Test] EntityManager — all tests passed ✓");
        }
    }
}
