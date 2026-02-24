using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Network;
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
        private TurnTimer _turnTimer;
        private PuzzleSetup _currentPuzzle;
        private PuzzleGenerator _puzzleGenerator;
        private LobbyState _lobbyState;
        private PlayingState _playingState;
        private GameOverState _gameOverState;

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

            // Subscribe to chat input (local mode — just echo to log)
            if (_uiManager.LogPanel != null)
                _uiManager.LogPanel.OnChatMessageSent += HandleLocalChat;

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

            // If ConnectionManager exists, let it handle mode selection.
            // Otherwise (backwards compat), auto-start if configured.
            bool hasConnectionManager =
                FindFirstObjectByType<Cryptid.Network.ConnectionManager>() != null;

            if (!hasConnectionManager && _autoStart)
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

            // Step 1.5: Center camera on the generated map
            CenterCameraOnMap();

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

            // Bind UI FIRST so log entries appear before action handlers
            // (GameBootstrapper's HandleQuestionAsked calls AutoRespond synchronously,
            //  which chains into EndTurn → next turn. UI must log before that.)
            _uiManager.BindGameplay(_turnManager, _currentPuzzle, _playerCount);

            // Subscribe to turn events (after UI, so UI logs come first)
            _turnManager.OnTurnStarted += HandleTurnStarted;
            _turnManager.OnQuestionAsked += HandleQuestionAsked;
            _turnManager.OnResponseGiven += HandleResponseGiven;
            _turnManager.OnSearchPerformed += HandleSearchPerformed;
            _turnManager.OnSearchDiscPlaced += HandleSearchDiscPlaced;
            _turnManager.OnSearchVerification += HandleSearchVerification;
            _turnManager.OnPenaltyCubePlaced += HandlePenaltyCubePlaced;
            _turnManager.OnGameWon += HandleGameWon;

            // Create turn timer
            _turnTimer = gameObject.AddComponent<TurnTimer>();
            _turnTimer.OnTimerTick += HandleTimerTick;
            _turnTimer.OnTimerExpired += HandleTimerExpired;
            _turnManager.OnPhaseChanged += HandlePhaseChangedForTimer;

            Debug.Log("[GameBootstrapper] Setup complete. Puzzle ready.");
        }

        /// <summary>
        /// Centers the RTS camera on the generated map.
        /// </summary>
        private void CenterCameraOnMap()
        {
            var cam = FindFirstObjectByType<Cryptid.Systems.RTSCameraController>();
            if (cam != null && _mapGenerator.WorldMap != null)
            {
                cam.CenterOnMap(_mapGenerator.WorldMap);
            }
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

            var coords = tile.Coordinates;
            int currentPlayer = _turnManager.CurrentPlayerIndex;

            switch (_turnManager.CurrentPhase)
            {
                case TurnPhase.SelectTile:
                    if (!ValidateTileInteraction(coords, currentPlayer)) return;
                    int targetPlayer = (_uiManager != null && _uiManager.SelectedTargetPlayer >= 0)
                        ? _uiManager.SelectedTargetPlayer
                        : (currentPlayer + 1) % _playerCount;
                    _turnManager.SubmitQuestion(coords, targetPlayer);
                    break;

                case TurnPhase.Search:
                    if (!ValidateTileInteraction(coords, currentPlayer)) return;
                    _turnManager.SubmitSearch(coords, _mapGenerator.WorldMap);
                    break;

                case TurnPhase.PenaltyPlacement:
                    if (!ValidateTileInteraction(coords, currentPlayer)) return;
                    bool accepted = _turnManager.SubmitPenaltyCube(
                        coords, _mapGenerator.WorldMap);
                    if (!accepted && _uiManager?.LogPanel != null)
                    {
                        _uiManager.LogPanel.AddEntry(currentPlayer,
                            "  ⚠ " + L.Get("warn_matches_clue"));
                    }
                    break;

                default:
                    Debug.Log($"[GameBootstrapper] Tile clicked at {coords}, " +
                             $"but current phase is {_turnManager.CurrentPhase}. " +
                             $"Press Q or S first.");
                    break;
            }
        }

        /// <summary>
        /// Validates tile interaction per spec 5.3.A:
        ///   1. Cube Blocker: no cubes on tile (permanently dead)
        ///   2. No Self-Stacking: player has no existing tokens on tile
        ///   3. Discs are Stackable: other players' discs are OK (implicit)
        /// Shows UI feedback when validation fails.
        /// </summary>
        private bool ValidateTileInteraction(HexCoordinates coords, int playerIndex)
        {
            if (_tokenPlacer == null) return true;

            if (_tokenPlacer.HasAnyCube(coords))
            {
                Debug.Log($"[GameBootstrapper] Tile {coords} blocked — has cube (permanently dead).");
                _uiManager?.LogPanel?.AddEntry(playerIndex,
                    L.Get("warn_tile_has_cube"));
                return false;
            }

            if (_tokenPlacer.HasPlayerToken(coords, playerIndex))
            {
                Debug.Log($"[GameBootstrapper] Player {playerIndex + 1} already has a token at {coords}.");
                _uiManager?.LogPanel?.AddEntry(playerIndex,
                    L.Get("warn_already_token"));
                return false;
            }

            return true;
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
                Debug.Log($"[GameBootstrapper] Search failed! Player {playerIndex + 1} must place penalty cube.");
            }
        }

        /// <summary>
        /// Handles the searcher's initial disc placement at the start of a search.
        /// </summary>
        private void HandleSearchDiscPlaced(int playerIndex, HexCoordinates tile)
        {
            if (_tokenPlacer == null) return;
            _tokenPlacer.PlaceTokenAt(tile, TokenType.Disc, playerIndex);
        }

        /// <summary>
        /// Handles each verifier's response during clockwise search verification.
        /// Places disc (YES) or cube (NO) for the verifying player.
        /// </summary>
        private void HandleSearchVerification(int verifier, HexCoordinates tile, bool result)
        {
            if (_tokenPlacer == null) return;
            TokenType type = result ? TokenType.Disc : TokenType.Cube;
            _tokenPlacer.PlaceTokenAt(tile, type, verifier);
        }

        /// <summary>
        /// Handles penalty cube placed after a failed search or question.
        /// The active player places a cube on a tile where their own clue does NOT match.
        /// </summary>
        private void HandlePenaltyCubePlaced(int playerIndex, HexCoordinates tile)
        {
            if (_tokenPlacer == null) return;

            _tokenPlacer.PlaceTokenAt(tile, TokenType.Cube, playerIndex);

            // Log to UI
            if (_uiManager?.LogPanel != null)
            {
                _uiManager.LogPanel.AddEntry(playerIndex,
                    L.Format("log_penalty_placed", L.PlayerShort(playerIndex), tile));
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
            if (_turnManager != null && _turnManager.CurrentPhase == TurnPhase.ChooseAction)
            {
                _turnManager.ChooseAction(action);
            }
        }

        /// <summary>
        /// Handles the "Play Again" button from the GameOver screen.
        /// Reloads the current scene for a fresh game.
        /// </summary>
        private void HandleRestart()
        {
            GameService.ClearAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ---------------------------------------------------------
        // Debug Input
        // ---------------------------------------------------------

        private void HandleDebugInput()
        {
            // NOTE: All keyboard shortcuts (Enter, Q, S, Tab) removed.
            // They conflict with chat input and trigger unintended actions.
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

        /// <summary> The UI manager. </summary>
        public GameUIManager UIManager => _uiManager;

        // ---------------------------------------------------------
        // Network Mode Support
        // ---------------------------------------------------------

        /// <summary>
        /// Disables GameBootstrapper for network mode.
        /// Unsubscribes from tile and UI events, disables Update loop.
        /// Called by <c>ConnectionManager</c> when entering a network game.
        /// </summary>
        public void DisableForNetworkMode()
        {
            // Unsubscribe from tile clicks (NetworkGameManager will take over)
            if (_tileInteraction != null)
                _tileInteraction.OnTileSelected -= HandleTileClicked;

            // Unsubscribe from UI events
            if (_uiManager != null)
            {
                _uiManager.OnActionChosen -= HandleUIActionChosen;
                _uiManager.OnRestartRequested -= HandleRestart;

                // Unsubscribe local chat handler (NetworkGameManager handles chat in network mode)
                if (_uiManager.LogPanel != null)
                    _uiManager.LogPanel.OnChatMessageSent -= HandleLocalChat;
            }

            enabled = false;
            Debug.Log("[GameBootstrapper] Disabled for network mode.");
        }

        /// <summary>
        /// Starts the local game. Called by <c>ConnectionManager</c>
        /// when the user selects "Local Game".
        /// </summary>
        public void StartLocalGame()
        {
            _lobbyState.AddPlayer();
        }

        // ---------------------------------------------------------
        // Turn Timer
        // ---------------------------------------------------------

        private void HandlePhaseChangedForTimer(int playerIndex, TurnPhase phase)
        {
            if (_turnTimer == null) return;

            // Clear dimming from previous phase
            ClearTileDimming();

            switch (phase)
            {
                case TurnPhase.ChooseAction:
                case TurnPhase.SelectTile:
                case TurnPhase.Search:
                    _turnTimer.StartTurnTimer();
                    break;

                case TurnPhase.PenaltyPlacement:
                    _turnTimer.StartPenaltyTimer();
                    // Dim tiles that cannot receive a penalty cube
                    ApplyPenaltyDimming(playerIndex);
                    break;

                default:
                    _turnTimer.Stop();
                    _uiManager?.TurnIndicator?.HideTimer();
                    break;
            }
        }

        private void HandleTimerTick(float remaining)
        {
            _uiManager?.TurnIndicator?.UpdateTimer(remaining);
        }

        private void HandleTimerExpired()
        {
            if (_turnManager == null) return;

            var phase = _turnManager.CurrentPhase;
            int player = _turnManager.CurrentPlayerIndex;

            _uiManager?.TurnIndicator?.HideTimer();

            if (phase == TurnPhase.PenaltyPlacement)
            {
                // Auto-place penalty cube on a random valid tile
                AutoPlacePenaltyCube(player);
            }
            else
            {
                // Time ran out — skip turn
                _uiManager?.LogPanel?.AddEntry(player, L.Get("timer_expired"));
                _turnManager.SkipTurn();
            }
        }

        private void AutoPlacePenaltyCube(int playerIndex)
        {
            if (_currentPuzzle == null || _mapGenerator?.WorldMap == null) return;

            var clue = _currentPuzzle.PlayerClues[playerIndex];
            foreach (var kvp in _mapGenerator.WorldMap)
            {
                // Skip tiles that already have cubes or player tokens
                if (_tokenPlacer != null &&
                    (_tokenPlacer.HasAnyCube(kvp.Key) || _tokenPlacer.HasPlayerToken(kvp.Key, playerIndex)))
                    continue;

                if (!clue.Check(kvp.Value, _mapGenerator.WorldMap))
                {
                    // Found a valid tile (clue does NOT match)
                    _uiManager?.LogPanel?.AddEntry(playerIndex, L.Get("timer_auto_penalty"));
                    ClearTileDimming();
                    _turnManager.SubmitPenaltyCube(kvp.Key, _mapGenerator.WorldMap);
                    return;
                }
            }

            // Fallback: skip turn if no valid tile found
            _uiManager?.LogPanel?.AddEntry(playerIndex, L.Get("timer_auto_penalty"));
            ClearTileDimming();
            _turnManager.SkipTurn();
        }

        // ---------------------------------------------------------
        // Penalty Tile Dimming
        // ---------------------------------------------------------

        /// <summary>
        /// Dims all tiles that cannot receive a penalty cube during PenaltyPlacement.
        /// Valid penalty tiles: clue does NOT match AND no cube AND no player token.
        /// </summary>
        private void ApplyPenaltyDimming(int playerIndex)
        {
            if (_currentPuzzle == null || _mapGenerator?.WorldMap == null) return;

            var clue = _currentPuzzle.PlayerClues[playerIndex];
            bool hasAnyValid = false;

            foreach (var kvp in _mapGenerator.WorldMap)
            {
                var hexTile = FindHexTile(kvp.Key);
                if (hexTile == null) continue;

                bool hasCube = _tokenPlacer != null && _tokenPlacer.HasAnyCube(kvp.Key);
                bool hasToken = _tokenPlacer != null && _tokenPlacer.HasPlayerToken(kvp.Key, playerIndex);
                bool clueMatches = clue.Check(kvp.Value, _mapGenerator.WorldMap);

                // A tile is invalid for penalty if: clue matches it, has cube, or has player token
                bool isInvalid = clueMatches || hasCube || hasToken;
                hexTile.SetDimmed(isInvalid);

                if (!isInvalid) hasAnyValid = true;
            }

            // If no valid tiles, auto-skip
            if (!hasAnyValid)
            {
                _uiManager?.LogPanel?.AddSystemMessage(L.Get("timer_auto_penalty"));
                ClearTileDimming();
                _turnManager.SkipTurn();
            }
        }

        /// <summary>Clears dimming from all tiles.</summary>
        private void ClearTileDimming()
        {
            if (_mapGenerator?.WorldMap == null) return;

            foreach (var kvp in _mapGenerator.WorldMap)
            {
                var hexTile = FindHexTile(kvp.Key);
                hexTile?.SetDimmed(false);
            }
        }

        /// <summary>Finds the HexTile component for a given coordinate.</summary>
        private HexTile FindHexTile(HexCoordinates coords)
        {
            // MapGenerator stores spawned tiles by coordinate
            return _mapGenerator.GetHexTile(coords);
        }

        /// <summary>Handles chat messages in local/single-player mode (echo to log).</summary>
        private void HandleLocalChat(string message)
        {
            int currentPlayer = _turnManager?.CurrentPlayerIndex ?? 0;
            string name = L.Format("player_default", currentPlayer + 1);
            _uiManager?.LogPanel?.AddEntry(currentPlayer, $"[{name}]: {message}");
        }
    }
}
