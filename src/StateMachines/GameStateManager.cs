// ================================================================
// FILE: GameStateManager.cs
// Hierarchical state machine for global game flow.
// ALL state transitions go through HERE — nowhere else.
// Refine: add new states to GameState enum + transition table below.
// ================================================================
using Godot;
using System;

namespace CityBuilder.ECS
{
    public enum GameState
    {
        Booting,
        MainMenu,
        Loading,
        Simulating,
        BuildMode,
        Paused,
        PhotoMode,
    }

    public partial class GameStateManager : Node
    {
        public static GameStateManager Instance { get; private set; }
        public GameState CurrentState { get; private set; } = GameState.Booting;

        // Loose coupling — other systems subscribe, not poll
        public event Action<GameState> OnStateEntered;
        public event Action<GameState> OnStateExited;

        // Guard: set to true during tutorial or cutscene to block build mode
        public bool IsCutscenePlaying { get; set; } = false;

        public override void _Ready()
        {
            Instance = this;
            _EnterState(GameState.Booting);
        }

        // ── Public Transition API ─────────────────────────────────

        public void StartNewGame()   => _TryTransition(GameState.Simulating);
        public void OpenMainMenu()   => _TryTransition(GameState.MainMenu);

        public void ToggleBuildMode()
        {
            if      (CurrentState == GameState.Simulating) _TryTransition(GameState.BuildMode);
            else if (CurrentState == GameState.BuildMode)  _TryTransition(GameState.Simulating);
        }

        public void TogglePause()
        {
            if      (CurrentState == GameState.Simulating) _TryTransition(GameState.Paused);
            else if (CurrentState == GameState.Paused)     _TryTransition(GameState.Simulating);
        }

        public void EnterPhotoMode() => _TryTransition(GameState.PhotoMode);
        public void ExitPhotoMode()  => _TryTransition(GameState.Simulating);

        public void BeginLoading()   => _TryTransition(GameState.Loading);
        public void FinishLoading()  => _TryTransition(GameState.Simulating);

        // ── Internal Logic ────────────────────────────────────────

        private void _TryTransition(GameState target)
        {
            if (!_IsTransitionAllowed(CurrentState, target))
            {
                GD.PushWarning($"[StateManager] Transition blocked: {CurrentState} → {target}");
                return;
            }
            _ExitState(CurrentState);
            _EnterState(target);
        }

        private bool _IsTransitionAllowed(GameState from, GameState to)
        {
            // Guard: no BuildMode during cutscenes
            if (to == GameState.BuildMode && IsCutscenePlaying) return false;

            return (from, to) switch
            {
                (GameState.Booting,    GameState.MainMenu)   => true,
                (GameState.MainMenu,   GameState.Simulating) => true,
                (GameState.MainMenu,   GameState.Loading)    => true,
                (GameState.Loading,    GameState.Simulating) => true,
                (GameState.Simulating, GameState.BuildMode)  => true,
                (GameState.Simulating, GameState.Paused)     => true,
                (GameState.Simulating, GameState.PhotoMode)  => true,
                (GameState.Simulating, GameState.MainMenu)   => true,
                (GameState.BuildMode,  GameState.Simulating) => true,
                (GameState.Paused,     GameState.Simulating) => true,
                (GameState.Paused,     GameState.MainMenu)   => true,
                (GameState.PhotoMode,  GameState.Simulating) => true,
                _ => false
            };
        }

        private void _EnterState(GameState state)
        {
            CurrentState = state;
            GD.Print($"[StateManager] → {state}");
            OnStateEntered?.Invoke(state);

            if (state == GameState.Booting) _OnBooting();
        }

        private void _ExitState(GameState state)
            => OnStateExited?.Invoke(state);

        private void _OnBooting()
        {
            var world = new World();
            GD.Print("[Booting] ECS World initialized.");

            if (OS.IsDebugBuild())
            {
                world.Entities._RunTests();
                world.SpawnTestScene();
            }

            // Deferred so _Ready() finishes before we transition
            CallDeferred(nameof(_GoToMainMenu));
        }

        private void _GoToMainMenu()
            => _TryTransition(GameState.MainMenu);
    }
}
