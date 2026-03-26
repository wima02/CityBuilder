// ================================================================
// FILE: GameConstants.cs
// All game-wide constants. No magic numbers anywhere else.
// Refine: add your tuning values here, reference via GameConstants.X
// ================================================================
namespace CityBuilder.ECS
{
    public static class GameConstants
    {
        // ── Entity limits ────────────────────────────────────────
        public const int   MaxEntities               = 10_000;

        // ── Voxel / World ────────────────────────────────────────
        public const float VoxelSize                 = 1.0f;   // 1 unit = 1 metre in Godot

        // ── Happiness & Upgrades ─────────────────────────────────
        public const float UpgradeHappinessThreshold = 0.60f;  // avg happiness to unlock next tier
        public const float BeautifulScoreRadius      = 15.0f;  // metres — beauty influence range

        // ── Needs cycle ──────────────────────────────────────────
        public const float NeedUpdateIntervalSec     = 5.0f;   // seconds between needs tick

        // ── Pathfinding ──────────────────────────────────────────
        public const float PathGridCellSize          = 1.0f;   // virtual A* grid resolution
        public const int   PathMaxIterations         = 5_000;  // safety cap per frame

        // ── Roads ────────────────────────────────────────────────
        public const float RoadWidth                 = 2.0f;
        public const float RoadSnapRadius            = 0.5f;   // snap to existing road endpoint

        // ── Economy ──────────────────────────────────────────────
        public const float SupplyTickIntervalSec     = 10.0f;
        public const int   WarehouseDefaultCapacity  = 100;

        // ── Object Pool ──────────────────────────────────────────
        public const int   AgentPoolInitialSize      = 50;
    }
}
