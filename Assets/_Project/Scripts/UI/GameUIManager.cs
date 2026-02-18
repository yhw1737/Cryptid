using System;
using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Systems.Turn;
using UnityEngine;

namespace Cryptid.UI
{
    /// <summary>
    /// Master UI controller for the Cryptid game.
    /// 
    /// Programmatically creates a Canvas and all UI panels on Awake.
    /// No prefab or manual editor setup required — just add this component.
    /// 
    /// Communication pattern (Event-Driven, per AGENT_RULES):
    ///   Game → UI:  Subscribes to TurnManager / FSM events to update panels.
    ///   UI → Game:  Fires OnActionChosen / OnRestartRequested for the
    ///               GameBootstrapper to handle.
    /// 
    /// Setup:
    ///   1. GameBootstrapper calls BindFSM() after FSM creation.
    ///   2. GameBootstrapper calls BindGameplay() after TurnManager/Puzzle creation.
    ///   3. GameBootstrapper subscribes to OnActionChosen / OnRestartRequested.
    /// </summary>
    [DefaultExecutionOrder(100)] // Run after GameBootstrapper (default 0)
    public class GameUIManager : MonoBehaviour
    {
        // ---------------------------------------------------------
        // Events (UI → Game logic)
        // ---------------------------------------------------------

        /// <summary>Fired when a player clicks the Question or Search button.</summary>
        public event Action<PlayerAction> OnActionChosen;

        /// <summary>Fired when the player clicks "Play Again" on the GameOver screen.</summary>
        public event Action OnRestartRequested;

        // ---------------------------------------------------------
        // Panel references (created in Awake)
        // ---------------------------------------------------------

        private Canvas _canvas;
        private TurnIndicatorPanel _turnIndicator;
        private ActionPanel _actionPanel;
        private PlayerSelectPanel _playerSelect;
        private CluePanel _cluePanel;
        private GameOverPanel _gameOverPanel;

        // ---------------------------------------------------------
        // Bound game state (set via Bind methods)
        // ---------------------------------------------------------

        private GameStateMachine _fsm;
        private TurnManager _turnManager;
        private PuzzleSetup _puzzle;
        private int _playerCount;

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Currently selected target for Question action.
        /// -1 if no target is selected.
        /// Read by GameBootstrapper when a tile is clicked during SelectTile phase.
        /// </summary>
        public int SelectedTargetPlayer =>
            _playerSelect != null ? _playerSelect.SelectedTarget : -1;

        /// <summary>
        /// Called by GameBootstrapper after FSM is created.
        /// Subscribes to state-change events to show/hide panels.
        /// </summary>
        public void BindFSM(GameStateMachine fsm)
        {
            _fsm = fsm;
            _fsm.OnStateChanged += HandleStateChanged;
        }

        /// <summary>
        /// Called by GameBootstrapper after TurnManager and PuzzleSetup are created
        /// (during the Setup phase). Subscribes to all turn events.
        /// Safe to call multiple times (unsubscribes first).
        /// </summary>
        public void BindGameplay(TurnManager turnManager, PuzzleSetup puzzle, int playerCount)
        {
            UnsubscribeTurnEvents();

            _turnManager = turnManager;
            _puzzle = puzzle;
            _playerCount = playerCount;

            _turnManager.OnTurnStarted    += HandleTurnStarted;
            _turnManager.OnPhaseChanged   += HandlePhaseChanged;
            _turnManager.OnQuestionAsked  += HandleQuestionAsked;
            _turnManager.OnResponseGiven  += HandleResponseGiven;
            _turnManager.OnSearchPerformed += HandleSearchPerformed;
            _turnManager.OnGameWon        += HandleGameWon;
        }

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            BuildAllUI();
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
        }

        // ---------------------------------------------------------
        // UI Construction
        // ---------------------------------------------------------

        /// <summary>Creates the Canvas and all panel components.</summary>
        private void BuildAllUI()
        {
            _canvas = UIFactory.CreateScreenCanvas("GameUI_Canvas", 10);
            _canvas.transform.SetParent(transform);

            // Turn Indicator (top bar)
            var turnRoot = UIFactory.CreatePanel(_canvas.transform,
                "TurnIndicatorPanel", UIFactory.PanelBg);
            _turnIndicator = turnRoot.gameObject.AddComponent<TurnIndicatorPanel>();
            _turnIndicator.Build(turnRoot);

            // Action Panel (bottom-center: Question / Search buttons)
            var actionRoot = UIFactory.CreatePanel(_canvas.transform,
                "ActionPanel", UIFactory.PanelBg);
            _actionPanel = actionRoot.gameObject.AddComponent<ActionPanel>();
            _actionPanel.Build(actionRoot);
            _actionPanel.OnActionClicked += HandleActionButtonClicked;

            // Player Select Panel (above action panel: target picker)
            var selectRoot = UIFactory.CreatePanel(_canvas.transform,
                "PlayerSelectPanel", UIFactory.PanelBg);
            _playerSelect = selectRoot.gameObject.AddComponent<PlayerSelectPanel>();
            _playerSelect.Build(selectRoot, 5); // Max 5 players; extras hidden at runtime

            // Clue Panel (bottom-left: current player's clue)
            var clueRoot = UIFactory.CreatePanel(_canvas.transform,
                "CluePanel", UIFactory.PanelBg);
            _cluePanel = clueRoot.gameObject.AddComponent<CluePanel>();
            _cluePanel.Build(clueRoot);

            // Game Over Panel (fullscreen overlay)
            var overRoot = UIFactory.CreatePanel(_canvas.transform, "GameOverPanel");
            _gameOverPanel = overRoot.gameObject.AddComponent<GameOverPanel>();
            _gameOverPanel.Build(overRoot);
            _gameOverPanel.OnRestartClicked += HandleRestartClicked;

            // Initially hide all gameplay panels
            ShowLobbyState();
        }

        // ---------------------------------------------------------
        // FSM State Handlers
        // ---------------------------------------------------------

        /// <summary>Hides all gameplay panels (shown during Lobby / pre-game).</summary>
        private void ShowLobbyState()
        {
            _turnIndicator.gameObject.SetActive(false);
            _actionPanel.gameObject.SetActive(false);
            _playerSelect.Hide();
            _cluePanel.gameObject.SetActive(false);
            _gameOverPanel.Hide();
        }

        private void HandleStateChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            switch (newPhase)
            {
                case GamePhase.Lobby:
                    ShowLobbyState();
                    break;

                case GamePhase.Setup:
                    // Keep panels hidden during map/puzzle generation
                    break;

                case GamePhase.Playing:
                    _turnIndicator.gameObject.SetActive(true);
                    _cluePanel.gameObject.SetActive(true);
                    _gameOverPanel.Hide();
                    break;

                case GamePhase.GameOver:
                    _actionPanel.gameObject.SetActive(false);
                    _playerSelect.Hide();
                    break;
            }
        }

        // ---------------------------------------------------------
        // TurnManager Event Handlers
        // ---------------------------------------------------------

        private void HandleTurnStarted(int playerIndex)
        {
            // Update HUD
            _turnIndicator.UpdateDisplay(
                _turnManager.TurnNumber, playerIndex, _turnManager.CurrentPhase);

            // Show action buttons (ChooseAction phase)
            _actionPanel.UpdateForPhase(TurnPhase.ChooseAction);
            _playerSelect.Hide();

            // Update clue panel for the current player
            if (_puzzle != null && playerIndex < _puzzle.PlayerClues.Count)
            {
                _cluePanel.UpdateClue(playerIndex, _puzzle.PlayerClues[playerIndex].Description);
            }
        }

        private void HandlePhaseChanged(int playerIndex, TurnPhase phase)
        {
            // Update HUD phase text
            _turnIndicator.UpdateDisplay(_turnManager.TurnNumber, playerIndex, phase);

            // Toggle action panel visibility
            _actionPanel.UpdateForPhase(phase);

            // Show/hide player select panel
            if (phase == TurnPhase.SelectTile)
            {
                int defaultTarget = (playerIndex + 1) % _playerCount;
                _playerSelect.Show(playerIndex, defaultTarget, _playerCount);
            }
            else
            {
                _playerSelect.Hide();
            }
        }

        private void HandleQuestionAsked(int asking, int target, HexCoordinates tile)
        {
            // Future: could add a toast / log panel entry
            Debug.Log($"[UI] Player {asking + 1} asks Player {target + 1} about {tile}");
        }

        private void HandleResponseGiven(int responding, HexCoordinates tile, bool result)
        {
            string answer = result ? "YES (cube)" : "NO (disc)";
            Debug.Log($"[UI] Player {responding + 1} responds: {answer}");
        }

        private void HandleSearchPerformed(int player, HexCoordinates tile, bool correct)
        {
            if (!correct)
            {
                Debug.Log($"[UI] Player {player + 1} searched wrong tile.");
            }
        }

        private void HandleGameWon(int winnerIndex)
        {
            string answerInfo = _puzzle != null
                ? $"The Cryptid was at {_puzzle.AnswerTile.Coordinates}"
                : "";
            _gameOverPanel.Show(winnerIndex, answerInfo);
        }

        // ---------------------------------------------------------
        // UI Button Handlers
        // ---------------------------------------------------------

        private void HandleActionButtonClicked(PlayerAction action)
        {
            OnActionChosen?.Invoke(action);
        }

        private void HandleRestartClicked()
        {
            OnRestartRequested?.Invoke();
        }

        // ---------------------------------------------------------
        // Cleanup
        // ---------------------------------------------------------

        private void UnsubscribeTurnEvents()
        {
            if (_turnManager == null) return;

            _turnManager.OnTurnStarted     -= HandleTurnStarted;
            _turnManager.OnPhaseChanged    -= HandlePhaseChanged;
            _turnManager.OnQuestionAsked   -= HandleQuestionAsked;
            _turnManager.OnResponseGiven   -= HandleResponseGiven;
            _turnManager.OnSearchPerformed -= HandleSearchPerformed;
            _turnManager.OnGameWon         -= HandleGameWon;
        }

        private void UnsubscribeAll()
        {
            if (_fsm != null)
                _fsm.OnStateChanged -= HandleStateChanged;

            UnsubscribeTurnEvents();

            if (_actionPanel != null)
                _actionPanel.OnActionClicked -= HandleActionButtonClicked;

            if (_gameOverPanel != null)
                _gameOverPanel.OnRestartClicked -= HandleRestartClicked;
        }
    }
}
