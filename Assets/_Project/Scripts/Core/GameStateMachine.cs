using System;
using UnityEngine;

namespace Cryptid.Core
{
    /// <summary>
    /// High-level game phases for the Cryptid game flow.
    /// Managed by GameStateMachine.
    /// </summary>
    public enum GamePhase
    {
        /// <summary> Initial state. Waiting for players / lobby. </summary>
        Lobby = 0,

        /// <summary> Map generation, puzzle creation, clue distribution. </summary>
        Setup = 1,

        /// <summary> Active gameplay: players take turns. </summary>
        Playing = 2,

        /// <summary> A player has found the Cryptid. Show results. </summary>
        GameOver = 3,
    }

    /// <summary>
    /// Interface for individual game states.
    /// Each state handles enter/exit/update logic for one GamePhase.
    /// </summary>
    public interface IGameState
    {
        GamePhase Phase { get; }
        void Enter();
        void Update();
        void Exit();
    }

    /// <summary>
    /// Finite State Machine for game flow management.
    /// 
    /// Controls transitions between game phases:
    ///   Lobby → Setup → Playing → GameOver
    /// 
    /// Each state is an IGameState implementation that encapsulates
    /// the logic for that phase (e.g., SetupState generates the map and puzzle).
    /// 
    /// Events are fired on transitions for UI and other systems to react.
    /// 
    /// Usage:
    ///   var fsm = new GameStateMachine();
    ///   fsm.RegisterState(new LobbyState());
    ///   fsm.RegisterState(new SetupState());
    ///   fsm.TransitionTo(GamePhase.Lobby);
    /// </summary>
    public class GameStateMachine
    {
        private IGameState _currentState;
        private readonly IGameState[] _states;

        /// <summary> The currently active game phase. </summary>
        public GamePhase CurrentPhase => _currentState?.Phase ?? GamePhase.Lobby;

        /// <summary> Fired when a state transition occurs. Args: (oldPhase, newPhase). </summary>
        public event Action<GamePhase, GamePhase> OnStateChanged;

        public GameStateMachine()
        {
            // Array indexed by GamePhase enum values
            int phaseCount = Enum.GetValues(typeof(GamePhase)).Length;
            _states = new IGameState[phaseCount];
        }

        /// <summary>
        /// Registers an IGameState implementation for its phase.
        /// </summary>
        public void RegisterState(IGameState state)
        {
            int index = (int)state.Phase;
            _states[index] = state;
            Debug.Log($"[FSM] Registered state: {state.Phase}");
        }

        /// <summary>
        /// Transitions to a new game phase.
        /// Exits the current state and enters the new one.
        /// </summary>
        public void TransitionTo(GamePhase newPhase)
        {
            int index = (int)newPhase;
            if (_states[index] == null)
            {
                Debug.LogError($"[FSM] No state registered for {newPhase}!");
                return;
            }

            GamePhase oldPhase = CurrentPhase;

            _currentState?.Exit();

            Debug.Log($"[FSM] {oldPhase} → {newPhase}");

            _currentState = _states[index];
            _currentState.Enter();

            OnStateChanged?.Invoke(oldPhase, newPhase);
        }

        /// <summary>
        /// Calls Update() on the current state. Should be called from MonoBehaviour.Update().
        /// </summary>
        public void Update()
        {
            _currentState?.Update();
        }
    }

    // =================================================================
    // Concrete State Implementations
    // =================================================================

    /// <summary>
    /// Lobby state: waiting for players to join.
    /// In single-player debug mode, immediately ready to proceed.
    /// </summary>
    public class LobbyState : IGameState
    {
        public GamePhase Phase => GamePhase.Lobby;

        private readonly GameStateMachine _fsm;
        private readonly int _requiredPlayers;
        private int _currentPlayers;

        public LobbyState(GameStateMachine fsm, int requiredPlayers = 1)
        {
            _fsm = fsm;
            _requiredPlayers = requiredPlayers;
        }

        public void Enter()
        {
            Debug.Log("[LobbyState] Waiting for players...");
            _currentPlayers = 0;
        }

        /// <summary>
        /// Call this when a player joins (or auto-call for local player).
        /// </summary>
        public void AddPlayer()
        {
            _currentPlayers++;
            Debug.Log($"[LobbyState] Player joined ({_currentPlayers}/{_requiredPlayers})");

            if (_currentPlayers >= _requiredPlayers)
            {
                Debug.Log("[LobbyState] All players ready!");
                _fsm.TransitionTo(GamePhase.Setup);
            }
        }

        public void Update() { }
        public void Exit() { }
    }

    /// <summary>
    /// Setup state: generates the map and puzzle.
    /// Auto-transitions to Playing when complete.
    /// </summary>
    public class SetupState : IGameState
    {
        public GamePhase Phase => GamePhase.Setup;

        private readonly GameStateMachine _fsm;
        private readonly Action _onSetup;

        /// <summary>
        /// Creates a SetupState.
        /// </summary>
        /// <param name="fsm">Reference to the FSM for auto-transition.</param>
        /// <param name="onSetup">Callback that performs map gen + puzzle gen + clue distribution.</param>
        public SetupState(GameStateMachine fsm, Action onSetup)
        {
            _fsm = fsm;
            _onSetup = onSetup;
        }

        public void Enter()
        {
            Debug.Log("[SetupState] Generating map and puzzle...");
            _onSetup?.Invoke();
            Debug.Log("[SetupState] Setup complete. Transitioning to Playing...");
            _fsm.TransitionTo(GamePhase.Playing);
        }

        public void Update() { }
        public void Exit() { }
    }

    /// <summary>
    /// Playing state: active gameplay with turns.
    /// Delegates turn logic to TurnManager.
    /// </summary>
    public class PlayingState : IGameState
    {
        public GamePhase Phase => GamePhase.Playing;

        private readonly GameStateMachine _fsm;
        private readonly Action _onGameOver;

        /// <summary>
        /// Flag set by TurnManager when a player finds the Cryptid.
        /// </summary>
        public bool IsGameOver { get; set; }

        public PlayingState(GameStateMachine fsm, Action onGameOver = null)
        {
            _fsm = fsm;
            _onGameOver = onGameOver;
        }

        public void Enter()
        {
            IsGameOver = false;
            Debug.Log("[PlayingState] Game started! Players take turns.");
        }

        public void Update()
        {
            if (IsGameOver)
            {
                _onGameOver?.Invoke();
                _fsm.TransitionTo(GamePhase.GameOver);
            }
        }

        public void Exit()
        {
            Debug.Log("[PlayingState] Game phase ended.");
        }

        /// <summary>
        /// Call this when a player correctly identifies the Cryptid location.
        /// </summary>
        public void TriggerGameOver()
        {
            IsGameOver = true;
        }
    }

    /// <summary>
    /// GameOver state: displays results.
    /// </summary>
    public class GameOverState : IGameState
    {
        public GamePhase Phase => GamePhase.GameOver;

        /// <summary> Index of the winning player (0-based). -1 if none. </summary>
        public int WinnerIndex { get; set; } = -1;

        public void Enter()
        {
            string winner = WinnerIndex >= 0 ? $"Player {WinnerIndex + 1}" : "Unknown";
            Debug.Log($"[GameOverState] Game Over! Winner: {winner}");
        }

        public void Update() { }
        public void Exit() { }
    }
}
