// ================================================================
// FILE: World.cs
// Singleton container for all ECS resources.
// Initialized by GameStateManager during Booting state.
// Refine: register additional system references here as they are added.
// ================================================================
using Godot;

namespace CityBuilder.ECS
{
    public class World
    {
        public static World Instance { get; private set; }

        // Core ECS
        public EntityManager Entities { get; private set; }

        // Town singleton entity — holds TownTierComponent etc.
        public uint TownEntityId { get; private set; }

        public World()
        {
            Instance  = this;
            Entities  = new EntityManager();
            _CreateTownEntity();
        }

        // ── Town Entity ───────────────────────────────────────────
        // A single entity that holds global town-level components.

        private void _CreateTownEntity()
        {
            TownEntityId = Entities.CreateEntity();
            Entities.AddComponent(TownEntityId, new TownTierComponent
            {
                Tier = 1, AverageHappiness = 0f
            });
            GD.Print($"[World] Town entity created: id={TownEntityId}");
        }

        // ── Debug / Test Scene ────────────────────────────────────
        // Spawns a minimal 5×5 ground + 1 house scene for Phase 1 testing.
        // Remove or replace in production.

        public void SpawnTestScene()
        {
            var em = Entities;

            // 5×5 ground plane
            for (int x = -2; x <= 2; x++)
            for (int z = -2; z <= 2; z++)
            {
                uint e = em.CreateEntity();
                em.AddComponent(e, new PositionComponent(
                    new Vector3(x * GameConstants.VoxelSize, 0f, z * GameConstants.VoxelSize)));
                em.AddComponent(e, new VoxelMeshComponent(VoxelType.Ground,
                    new Color(0.4f, 0.62f, 0.3f)));
            }

            // One dwelling as first building
            uint house = em.CreateEntity();
            em.AddComponent(house, new PositionComponent(new Vector3(0f, 1f, 0f)));
            em.AddComponent(house, new VoxelMeshComponent(VoxelType.House,
                new Color(0.9f, 0.8f, 0.6f)));
            em.AddComponent(house, new BuildingDataComponent
            {
                Type = BuildingType.Dwelling, Capacity = 4, OccupiedBy = 0
            });

            // One train station for Phase 3 agent spawning
            uint station = em.CreateEntity();
            em.AddComponent(station, new PositionComponent(new Vector3(6f, 1f, 0f)));
            em.AddComponent(station, new VoxelMeshComponent(VoxelType.TrainStation,
                new Color(0.7f, 0.7f, 0.9f)));
            em.AddComponent(station, new BuildingDataComponent
            {
                Type = BuildingType.TrainStation, Capacity = 20, OccupiedBy = 0
            });

            GD.Print($"[World] Test scene spawned: {em.EntityCount} entities total.");
        }
    }
}
