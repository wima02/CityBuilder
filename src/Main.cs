// ================================================================
// FILE: Main.cs  (Godot AutoLoad / Root Node)
// Game entry point. Wires all systems together.
// Project → Settings → AutoLoad → Scripts/Main.cs
//
// Refine: add UI node references, connect HUD events here.
// ================================================================
using Godot;

namespace CityBuilder
{
    public partial class Main : Node
    {
        // Systems — added as children so they receive _Process / _Input
        private ECS.GameStateManager         _stateManager;
        private Systems.VoxelRenderSystem    _renderSystem;
        private Systems.SplineRoadSystem     _roadSystem;
        private Systems.PlacementSystem      _placementSystem;
        private Systems.OrientationSystem    _orientSystem;
        private Systems.SnappingSystem       _snappingSystem;
        private Systems.PathfindingSystem    _pathfinding;
        private Systems.AgentSpawnerSystem   _agentSpawner;
        private Systems.AgentMovementSystem  _agentMovement;
        private Systems.NeedsCycleSystem     _needsCycle;
        private Systems.BeautifulScoreSystem _beautySystem;
        private Systems.HappinessSystem      _happiness;
        private Systems.SupplyChainSystem    _supplyChain;
        private Systems.LaborSystem          _labor;
        private Systems.ResearchSystem       _research;
        private Systems.TownUpgradeSystem    _townUpgrade;

        public override void _Ready()
        {
            // State manager must be first — it initializes the ECS World
            _stateManager      = new ECS.GameStateManager { Name = "GameStateManager" };
            AddChild(_stateManager);
            _stateManager.OnStateEntered += _OnStateEntered;
        }

        private void _OnStateEntered(ECS.GameState state)
        {
            if (state == ECS.GameState.Simulating && _renderSystem == null)
                _InitSystems();
        }

        private void _InitSystems()
        {
            // Phase 1 — Rendering
            _renderSystem    = _Add<Systems.VoxelRenderSystem>("VoxelRenderSystem");

            // Phase 2 — Gridless Building
            _orientSystem    = _Add<Systems.OrientationSystem>("OrientationSystem");
            _snappingSystem  = _Add<Systems.SnappingSystem>("SnappingSystem");
            _roadSystem      = _Add<Systems.SplineRoadSystem>("SplineRoadSystem");
            _placementSystem = _Add<Systems.PlacementSystem>("PlacementSystem");

            // Phase 3 — Citizens
            _pathfinding     = _Add<Systems.PathfindingSystem>("PathfindingSystem");
            _agentSpawner    = _Add<Systems.AgentSpawnerSystem>("AgentSpawnerSystem");
            _agentMovement   = _Add<Systems.AgentMovementSystem>("AgentMovementSystem");
            _needsCycle      = _Add<Systems.NeedsCycleSystem>("NeedsCycleSystem");
            _beautySystem    = _Add<Systems.BeautifulScoreSystem>("BeautifulScoreSystem");
            _happiness       = _Add<Systems.HappinessSystem>("HappinessSystem");

            // Phase 4 — Economy
            _supplyChain     = _Add<Systems.SupplyChainSystem>("SupplyChainSystem");
            _labor           = _Add<Systems.LaborSystem>("LaborSystem");
            _research        = _Add<Systems.ResearchSystem>("ResearchSystem");
            _townUpgrade     = _Add<Systems.TownUpgradeSystem>("TownUpgradeSystem");

            // Wire up cross-system events
            _townUpgrade.OnTownUpgraded += tier =>
                GD.Print($"[Main] Town reached tier {tier} 🎉");
            _research.OnUnlockReady += tier =>
                GD.Print($"[Main] Research unlock available at tier {tier}");

            GD.Print("[Main] All systems initialized.");
        }

        private T _Add<T>(string name) where T : Node, new()
        {
            var node = new T { Name = name };
            AddChild(node);
            return node;
        }

        public override void _Input(InputEvent @event)
        {
            // Configure these action names in Godot's Input Map
            if (@event.IsActionPressed("ui_cancel"))   _stateManager.TogglePause();
            if (@event.IsActionPressed("build_mode"))  _stateManager.ToggleBuildMode();
            if (@event.IsActionPressed("photo_mode"))  _stateManager.EnterPhotoMode();
        }
    }
}
