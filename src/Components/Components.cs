// ================================================================
// FILE: Components.cs
// All ECS components as pure data structs — NO methods, NO logic.
// Refine: add fields to existing structs, or append new structs below.
// ================================================================
using Godot;

namespace CityBuilder.ECS
{
    // Marker interface — all components implement this
    public interface IComponent { }

    // ── Enums ────────────────────────────────────────────────────

    public enum VoxelType
    {
        Ground, House, Market, Warehouse,
        Road, Tree, Lamp, Bench, Farm, TrainStation, ResearchBuilding
    }

    public enum BuildingType
    {
        Dwelling, Market, Warehouse, Farm,
        Research, TrainStation
    }

    public enum AgentState
    {
        Idle, Walking, Working, Shopping, Resting, Homeless
    }

    public enum RoadShape
    {
        Straight, Curve, TJunction, Crossroads
    }

    // ── Position & Rotation ──────────────────────────────────────
    // Tracks world position and Y-axis rotation for every visible entity
    public struct PositionComponent : IComponent
    {
        public Vector3 Position;
        public float   RotationY; // degrees 0–360

        public PositionComponent(Vector3 pos, float rotY = 0f)
        {
            Position  = pos;
            RotationY = rotY;
        }
    }

    // ── Voxel Appearance ─────────────────────────────────────────
    // Defines how an entity is rendered as a voxel mesh
    public struct VoxelMeshComponent : IComponent
    {
        public VoxelType MeshType;
        public Color     Tint; // per-instance color variation

        public VoxelMeshComponent(VoxelType type, Color tint = default)
        {
            MeshType = type;
            Tint     = tint == default ? Colors.White : tint;
        }
    }

    // ── Road Segment ─────────────────────────────────────────────
    // Spline-based road: start and end define the Bezier spine
    public struct RoadComponent : IComponent
    {
        public Vector3   StartPoint;
        public Vector3   EndPoint;
        public Vector3   ControlPoint1; // Bezier handle
        public Vector3   ControlPoint2; // Bezier handle
        public RoadShape Shape;
        public float     Width;         // defaults to GameConstants.RoadWidth
    }

    // ── Placement State ──────────────────────────────────────────
    // Temporary component added during build-mode dragging
    public struct PlacementComponent : IComponent
    {
        public bool     IsValid;      // true = green preview, false = red
        public Vector3  PreviewPos;
        public uint     SnapTargetId; // 0 = no snap target
    }

    // ── Citizen Needs ────────────────────────────────────────────
    // Values from 0.0 (unmet) to 1.0 (fully satisfied)
    public struct NeedsComponent : IComponent
    {
        public float Food;
        public float Clothing;
        public float Entertainment;
        public float Shelter;       // 0 = homeless

        public readonly float Average =>
            (Food + Clothing + Entertainment + Shelter) / 4f;
    }

    // ── Happiness ────────────────────────────────────────────────
    public struct HappinessComponent : IComponent
    {
        public float Value;          // overall happiness 0–1
        public float BeautifulScore; // environmental beauty 0–100
    }

    // ── Building Data ────────────────────────────────────────────
    public struct BuildingDataComponent : IComponent
    {
        public BuildingType Type;
        public int          Capacity;    // max residents / workers
        public int          OccupiedBy;  // current assigned entity count
        public uint         NearestRoadId; // cached by OrientationSystem
    }

    // ── Inventory ────────────────────────────────────────────────
    public struct InventoryComponent : IComponent
    {
        public int Food;
        public int Goods;
        public int ResearchPoints;
    }

    // ── Labor ────────────────────────────────────────────────────
    public struct LaborComponent : IComponent
    {
        public uint AssignedWorkerEntityId; // 0 = unoccupied
        public int  MaxWorkers;
    }

    // ── Citizen Agent ────────────────────────────────────────────
    public struct AgentComponent : IComponent
    {
        public AgentState State;
        public uint        HomeEntityId;
        public uint        WorkplaceEntityId;
        public Vector3     TargetPosition;
        public float       MoveSpeed; // metres per second
    }

    // ── Pathfinding ──────────────────────────────────────────────
    // Written by PathfindingSystem, consumed by AgentMovementSystem
    public struct PathComponent : IComponent
    {
        public Vector3[] Waypoints;   // null = no path yet
        public int       CurrentStep;
    }

    // ── Aesthetic Modifier ───────────────────────────────────────
    // Lamps, benches, and plants contribute positively to BeautifulScore
    public struct AestheticModifierComponent : IComponent
    {
        public float BeautyValue; // positive contribution
        public float Radius;      // effect radius in metres
    }

    // ── Town Tier ────────────────────────────────────────────────
    // Singleton-style component on the "town entity"
    public struct TownTierComponent : IComponent
    {
        public int   Tier;              // 1 = Village … 5 = Metropolis
        public float AverageHappiness;  // updated by HappinessSystem
    }

    // ── Research ─────────────────────────────────────────────────
    public struct ResearchComponent : IComponent
    {
        public int PointsAccumulated;
        public int CurrentUnlockCost; // points needed for next unlock
    }

    // ── Supply Chain ─────────────────────────────────────────────
    // Links a producer to a storage target
    public struct SupplyLinkComponent : IComponent
    {
        public uint ProducerEntityId;
        public uint StorageEntityId;
        public float TransferRatePerTick;
    }
}
