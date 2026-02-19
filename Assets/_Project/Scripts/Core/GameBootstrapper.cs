using System.Collections;
using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Systems.Clue;
using Cryptid.Systems.Gameplay;
using Cryptid.Systems.Map;
using Cryptid.Systems.Turn;
using Cryptid.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cryptid.Core
{
    /// <summary>
    /// Main entry point that wires up all game systems.
    /// 
    /// Responsibilities:
    /// - Registers services in GameService
    /// - Initializes the FSM with concrete states
    /// - Orchestrates the game lifecycle
    /// 
    /// This is the ONLY MonoBehaviour that knows about all systems.
    /// All other systems communicate via events and GameService lookups.
    /// 
    /// Setup:
    /// 1. Add to a GameObject in the scene
    /// 2. Assign MapGenerator reference
    /// 3. Set player count
    /// 4. Play Mode → Press Enter to start game
    /// 
    /// Debug Controls:
    ///   Enter: Start game (from Lobby)
    ///   Q: Choose "Question" action (during turn)
    ///   S: Choose "Search" action (during turn)
    ///   Tab: Show current game state info
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private MapGenerator _mapGenerator;
        [SerializeField] private TokenPlacer _tokenPlacer;
        [SerializeField] private TileInteractionSystem _tileInteraction;
        [SerializeField] private GameUIManager _uiManager;

        [Header("Game Settings")]
        [Range(2, 5)]
        [SerializeField] private int _playerCount = 3;

        [Tooltip("Random seed for puzzle generation (-1 = random)")]
        [SerializeField] private int _puzzleSeed = -1;

        [Tooltip("Auto-start game on Play (skip lobby)")]
        [SerializeField] private bool _autoStart = false;

        // ---------------------------------------------------------
        // Runtime
        // ---------------------------------------------------------

        private GameStateMachine _fsm;
        private TurnManager _turnManager;
        private PuzzleSetup _currentPuzzle;
        private PuzzleGenerator _puzzleGenerator;
        private SimpleAIPlayer _aiPlayer;
        private LobbyState _lobbyState;
        private PlayingState _playingState;
        private GameOverState _gameOverState;
        private bool _isAITurnInProgress;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            // Register self and sub-systems
            GameService.Register(this);

            if (_mapGenerator != null)
                GameService.Register(_mapGenerator);

            if (_tokenPlacer != null)
                GameService.Register(_tokenPlacer);

            // Auto-create GameUIManager if not assigned
            if (_uiManager == null)
                _uiManager = gameObject.AddComponent<GameUIManager>();
            GameService.Register(_uiManager);

            // Build FSM
            _fsm = new GameStateMachine();
            _puzzleGenerator = new PuzzleGenerator();

            _lobbyState = new LobbyState(_fsm, _autoStart ? 1 : _playerCount);

            var setupState = new SetupState(_fsm, PerformSetup);

            _playingState = new PlayingState(_fsm);

            _gameOverState = new GameOverState();

            _fsm.RegisterState(_lobbyState);
            _fsm.RegisterState(setupState);
            _fsm.RegisterState(_playingState);
            _fsm.RegisterState(_gameOverState);

            // Listen for state changes
            _fsm.OnStateChanged += HandleStateChanged;

            GameService.Register(_fsm);

            // Bind UI to FSM
            _uiManager.BindFSM(_fsm);

            // Subscribe to UI events
            _uiManager.OnActionChosen += HandleUIActionChosen;
            _uiManager.OnRestartRequested += HandleRestart;

            // Subscribe to tile clicks for turn actions
            if (_tileInteraction != null)
            {
                _tileInteraction.OnTileSelected += HandleTileClicked;
                GameService.Register(_tileInteraction);
            }

            Debug.Log($"[GameBootstrapper] Initialized. Players: {_playerCount}, " +
                     $"AutoStart: {_autoStart}");
        }

        private void Start()
        {
            _fsm.TransitionTo(GamePhase.Lobby);

            if (_autoStart)
            {
                _lobbyState.AddPlayer(); // Auto-join triggers Setup
            }
        }

        private void Update()
        {
            _fsm.Update();
            HandleDebugInput();
        }

        private void OnDestroy()
        {
            if (_tileInteraction != null)
                _tileInteraction.OnTileSelected -= HandleTileClicked;

            if (_uiManager != null)
            {
                _uiManager.OnActionChosen -= HandleUIActionChosen;
                _uiManager.OnRestartRequested -= HandleRestart;
            }

            _aiPlayer?.Dispose();
            GameService.ClearAll();
        }

        // ---------------------------------------------------------
        // Setup Phase Logic
        // ---------------------------------------------------------

        /// <summary>
        /// Called by SetupState.Enter(). Generates map + puzzle.
        /// </summary>
        private void PerformSetup()
        {
            // Step 1: Generate map (if not already done by MapGenerator.Start)
            if (_mapGenerator.WorldMap == null || _mapGenerator.WorldMap.Count == 0)
            {
                _mapGenerator.GenerateMap();
                _mapGenerator.SpawnVisuals();
            }

            // Step 2: Generate puzzle
            _currentPuzzle = _puzzleGenerator.Generate(
                _mapGenerator.WorldMap, _playerCount, _puzzleSeed);

            if (_currentPuzzle == null)
            {
                Debug.LogError("[GameBootstrapper] Puzzle generation failed! Cannot start game.");
                return;
            }

            // Step 3: Create TurnManager
            _turnManager = new TurnManager(_playerCount, _currentPuzzle);
            GameService.Register(_turnManager);

            // Subscribe to turn events
            _turnManager.OnTurnStarted += HandleTurnStarted;
            _turnManager.OnQuestionAsked += HandleQuestionAsked;
            _turnManager.OnResponseGiven += HandleResponseGiven;
            _turnManager.OnSearchPerformed += HandleSearchPerformed;
            _turnManager.OnSearchPenalty += HandleSearchPenalty;
            _turnManager.OnGameWon += HandleGameWon;

            // Bind UI to gameplay systems
            _uiManager.BindGameplay(_turnManager, _currentPuzzle, _playerCount);

            // Create AI player for single-player mode
            _aiPlayer = new SimpleAIPlayer(_turnManager, _currentPuzzle,
                _mapGenerator.WorldMap, _puzzleSeed >= 0 ? _puzzleSeed + 1 : (int?)null);
            _aiPlayer.HumanPlayerIndex = 0;
            _aiPlayer.OnAIActionReady += HandleAIAction;

            Debug.Log("[GameBootstrapper] Setup complete. Puzzle ready.");
        }

        // ---------------------------------------------------------
        // State Change Handlers
        // ---------------------------------------------------------

        private void HandleStateChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            if (newPhase == GamePhase.Playing && _turnManager != null)
            {
                _turnManager.StartFirstTurn();
            }
        }

        // ---------------------------------------------------------
        // Turn Event Handlers
        // ---------------------------------------------------------

        private void HandleTurnStarted(int playerIndex)
        {
            Debug.Log($"[GameBootstrapper] === Turn {_turnManager.TurnNumber} === " +
                     $"Player {playerIndex + 1} | Phase: {_turnManager.CurrentPhase} " +
                     $"| Press Q (question) or S (search)");
        }

        /// <summary>
        /// Handles tile click during gameplay.
        /// Routes to Question or Search based on current TurnPhase.
        /// </summary>
        private void HandleTileClicked(HexTile tile)
        {
            if (_fsm.CurrentPhase != GamePhase.Playing || _turnManager == null) return;
            if (tile == null) return;
            // Block interaction during AI turns
            if (_isAITurnInProgress) return;
            if (_turnManager.CurrentPlayerIndex != 0) return; // Only human (P1) can click

            switch (_turnManager.CurrentPhase)
            {
                case TurnPhase.SelectTile:
                    // Use UI-selected target if available, fallback to next player
                    int targetPlayer = (_uiManager != null && _uiManager.SelectedTargetPlayer >= 0)
                        ? _uiManager.SelectedTargetPlayer
                        : (_turnManager.CurrentPlayerIndex + 1) % _playerCount;
                    _turnManager.SubmitQuestion(tile.Coordinates, targetPlayer);
                    break;

                case TurnPhase.Search:
                    _turnManager.SubmitSearch(tile.Coordinates);
                    break;

                default:
                    Debug.Log($"[GameBootstrapper] Tile clicked at {tile.Coordinates}, " +
                             $"but current phase is {_turnManager.CurrentPhase}. " +
                             $"Press Q or S first.");
                    break;
            }
        }

        private void HandleQuestionAsked(int askingPlayer, int targetPlayer, HexCoordinates tile)
        {
            // Auto-respond for now (single-player debug)
            _turnManager.AutoRespond(_mapGenerator.WorldMap);
        }

        private void HandleResponseGiven(int respondingPlayer, HexCoordinates tile, bool result)
        {
            if (_tokenPlacer == null) return;

            // Place token: Disc if clue matches (yes), Cube if not (no)
            TokenType tokenType = result ? TokenType.Disc : TokenType.Cube;
            _tokenPlacer.PlaceTokenAt(tile, tokenType, respondingPlayer);
        }

        private void HandleSearchPerformed(int playerIndex, HexCoordinates tile, bool isCorrect)
        {
            if (!isCorrect)
            {
                Debug.Log($"[GameBootstrapper] Wrong search! Applying penalty...");
                // Apply penalty: all other players reveal their clue result
                _turnManager.ApplySearchPenalty(tile, playerIndex, _mapGenerator.WorldMap);
            }
        }

        /// <summary>
        /// Handles penalty token placement after a failed search.
        /// Each opponent places a disc (match) or cube (no match) on the searched tile.
        /// </summary>
        private void HandleSearchPenalty(int respondingPlayer, HexCoordinates tile, bool clueMatches)
        {
            if (_tokenPlacer == null) return;

            TokenType tokenType = clueMatches ? TokenType.Disc : TokenType.Cube;
            _tokenPlacer.PlaceTokenAt(tile, tokenType, respondingPlayer);

            // Log to UI
            if (_uiManager?.LogPanel != null)
            {
                string token = clueMatches ? "disc" : "cube";
                _uiManager.LogPanel.AddEntry(respondingPlayer,
                    $"  Penalty: P{respondingPlayer + 1} reveals {token}");
            }
        }

        private void HandleGameWon(int winnerIndex)
        {
            _gameOverState.WinnerIndex = winnerIndex;
            _playingState.TriggerGameOver();
        }

        /// <summary>
        /// Handles action button clicks from the UI (Question / Search).
        /// </summary>
        private void HandleUIActionChosen(PlayerAction action)
        {
            if (_turnManager != null && _turnManager.CurrentPhase == TurnPhase.ChooseAction
                && !_isAITurnInProgress && _turnManager.CurrentPlayerIndex == 0)
            {
                _turnManager.ChooseAction(action);
            }
        }

        /// <summary>
        /// Handles the "Play Again" button from the GameOver screen.
        /// Reloads the current scene.
        /// </summary>
        private void HandleRestart()
        {
            _aiPlayer?.Dispose();
            GameService.ClearAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ---------------------------------------------------------
        // AI Player Integration
        // ---------------------------------------------------------

        /// <summary>
        /// Handles the AI's decision. Executes the action with a short delay
        /// so the human player can see what's happening.
        /// </summary>
        private void HandleAIAction(int playerIndex, PlayerAction action,
            HexCoordinates tile, int targetPlayer)
        {
            if (_isAITurnInProgress) return;
            StartCoroutine(ExecuteAITurnCoroutine(playerIndex, action, tile, targetPlayer));
        }

        /// <summary>
        /// Coroutine that executes an AI turn with delays for visual readability.
        /// </summary>
        private IEnumerator ExecuteAITurnCoroutine(int playerIndex, PlayerAction action,
            HexCoordinates tile, int targetPlayer)
        {
            _isAITurnInProgress = true;
            float delay = _aiPlayer?.ActionDelay ?? 0.6f;

            // Hide action panel during AI turns
            // (the UI reacts to phase changes, but we disable buttons proactively)

            // Wait before choosing action
            yield return new WaitForSeconds(delay);

            if (_turnManager == null || _turnManager.CurrentPlayerIndex != playerIndex)
            {
                _isAITurnInProgress = false;
                yield break;
            }

            // Step 1: Choose action
            _turnManager.ChooseAction(action);

            // Wait before selecting tile
            yield return new WaitForSeconds(delay);

            if (_turnManager == null)
            {
                _isAITurnInProgress = false;
                yield break;
            }

            // Step 2: Submit tile action
            if (action == PlayerAction.Question)
            {
                _turnManager.SubmitQuestion(tile, targetPlayer);
            }
            else
            {
                _turnManager.SubmitSearch(tile);
            }

            _isAITurnInProgress = false;
        }

        // ---------------------------------------------------------
        // Debug Input
        // ---------------------------------------------------------

        private void HandleDebugInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Enter → Start game from lobby
            if (kb.enterKey.wasPressedThisFrame && _fsm.CurrentPhase == GamePhase.Lobby)
            {
                _lobbyState.AddPlayer();
                return;
            }

            // Tab → Show state info
            if (kb.tabKey.wasPressedThisFrame)
            {
                LogCurrentState();
                return;
            }

            // Turn controls (only during Playing phase, human player only)
            if (_fsm.CurrentPhase != GamePhase.Playing || _turnManager == null) return;
            if (_isAITurnInProgress || _turnManager.CurrentPlayerIndex != 0) return;

            // Q → Choose Question
            if (kb.qKey.wasPressedThisFrame &&
                _turnManager.CurrentPhase == TurnPhase.ChooseAction)
            {
                _turnManager.ChooseAction(PlayerAction.Question);
                Debug.Log("[Debug] Select a tile (click) then press T to target player, " +
                         "or the tile click will auto-target next player.");
                return;
            }

            // S → Choose Search
            if (kb.sKey.wasPressedThisFrame &&
                _turnManager.CurrentPhase == TurnPhase.ChooseAction)
            {
                _turnManager.ChooseAction(PlayerAction.Search);
                Debug.Log("[Debug] Click a tile to search.");
                return;
            }
        }

        private void LogCurrentState()
        {
            Debug.Log($"=== Game State ===\n" +
                     $"FSM Phase: {_fsm.CurrentPhase}\n" +
                     $"Turn: {_turnManager?.TurnNumber ?? 0}\n" +
                     $"Current Player: {(_turnManager != null ? _turnManager.CurrentPlayerIndex + 1 : 0)}\n" +
                     $"Turn Phase: {_turnManager?.CurrentPhase}\n" +
                     $"Puzzle: {(_currentPuzzle != null ? "Generated" : "None")}\n" +
                     $"Services: {GameService.ServiceCount}");
        }

        // ---------------------------------------------------------
        // Public Accessors
        // ---------------------------------------------------------

        /// <summary> Currently active puzzle setup. </summary>
        public PuzzleSetup CurrentPuzzle => _currentPuzzle;

        /// <summary> The game FSM. </summary>
        public GameStateMachine FSM => _fsm;

        /// <summary> The active turn manager. Null before Setup phase. </summary>
        public TurnManager TurnMgr => _turnManager;
    }
}
