// ================================================================
// FILE: ECSTests.cs
// Unit tests for EntityManager, World, and core components.
// Run via _RunTests() methods or a dedicated test scene.
// Refine: add edge cases, stress tests with MaxEntities.
// ================================================================
using Godot;
using System.Diagnostics;

namespace CityBuilder.Tests
{
    public partial class ECSTests : Node
    {
        public override void _Ready()
        {
            if (!OS.IsDebugBuild()) return;
            RunAll();
        }

        public static void RunAll()
        {
            GD.Print("=== ECS Tests ===");

            Test_CreateAndDestroyEntity();
            Test_AddAndGetComponent();
            Test_SetComponentFiresEvent();
            Test_QuerySingleComponent();
            Test_QueryTwoComponents();
            Test_MaxEntitiesCap();

            GD.Print("=== All ECS Tests Passed ✓ ===");
        }

        private static void Test_CreateAndDestroyEntity()
        {
            var em = new ECS.EntityManager();
            uint e = em.CreateEntity();
            Debug.Assert(em.IsAlive(e), "Entity should be alive");
            em.DestroyEntity(e);
            Debug.Assert(!em.IsAlive(e), "Entity should be dead");
            GD.Print("[PASS] CreateAndDestroyEntity");
        }

        private static void Test_AddAndGetComponent()
        {
            var em = new ECS.EntityManager();
            uint e = em.CreateEntity();
            em.AddComponent(e, new ECS.PositionComponent(Godot.Vector3.One));
            var pos = em.GetComponent<ECS.PositionComponent>(e);
            Debug.Assert(pos.Position == Godot.Vector3.One, "Position should be Vector3.One");
            GD.Print("[PASS] AddAndGetComponent");
        }

        private static void Test_SetComponentFiresEvent()
        {
            var em = new ECS.EntityManager();
            bool eventFired = false;
            em.OnComponentChanged += (_, _) => eventFired = true;

            uint e = em.CreateEntity();
            em.AddComponent(e, new ECS.PositionComponent(Godot.Vector3.Zero));
            Debug.Assert(eventFired, "OnComponentChanged should fire on AddComponent");
            GD.Print("[PASS] SetComponentFiresEvent");
        }

        private static void Test_QuerySingleComponent()
        {
            var em = new ECS.EntityManager();
            uint a = em.CreateEntity();
            uint b = em.CreateEntity();
            em.AddComponent(a, new ECS.PositionComponent(Godot.Vector3.Zero));

            int count = 0;
            foreach (var id in em.Query<ECS.PositionComponent>()) count++;
            Debug.Assert(count == 1, "Query should return exactly 1 entity");
            GD.Print("[PASS] QuerySingleComponent");
        }

        private static void Test_QueryTwoComponents()
        {
            var em = new ECS.EntityManager();
            uint a = em.CreateEntity();
            uint b = em.CreateEntity();
            em.AddComponent(a, new ECS.PositionComponent(Godot.Vector3.Zero));
            em.AddComponent(a, new ECS.VoxelMeshComponent(ECS.VoxelType.House));
            em.AddComponent(b, new ECS.PositionComponent(Godot.Vector3.Zero)); // no VoxelMesh

            int count = 0;
            foreach (var _ in em.Query<ECS.PositionComponent, ECS.VoxelMeshComponent>()) count++;
            Debug.Assert(count == 1, "Two-component query should match only entity a");
            GD.Print("[PASS] QueryTwoComponents");
        }

        private static void Test_MaxEntitiesCap()
        {
            var em = new ECS.EntityManager();
            // Create up to the cap
            for (int i = 0; i < ECS.GameConstants.MaxEntities; i++)
                em.CreateEntity();

            // One more should return 0
            uint overflow = em.CreateEntity();
            Debug.Assert(overflow == 0, "Overflow entity should return id 0");
            GD.Print("[PASS] MaxEntitiesCap");
        }
    }
}
