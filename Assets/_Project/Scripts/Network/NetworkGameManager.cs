using System;
using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Systems.Clue;
using Cryptid.Systems.Gameplay;
using Cryptid.Systems.Map;
using Cryptid.Systems.Turn;
using Cryptid.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Cryptid.Network
{
    /// <summary>
    /// Host-authoritative network game orchestrator.
    ///
    /// Responsibilities:
    ///   - HOST: Runs TurnManager + PuzzleGenerator, validates actions, broadcasts state.
    ///   - CLIENT: Receives state updates, places tokens, updates UI.
    ///   - ALL: Routes tile clicks and UI actions through the network.
    ///
    /// Communication uses <see cref="CustomMessagingManager"/> (named messages)
    /// to avoid the need for registered NetworkObject prefabs.
    ///
    /// Implements <see cref="ITurnState"/> so GameUIManager can bind to it
    /// the same way it binds to the local TurnManager.
    /// </summary>
    public class NetworkGameManager : MonoBehaviour, ITurnState
    {
        // =============================================================
        // Constants
        // =============================================================

        private const string MSG_CHANNEL = "CryptidGame";

        // =============================================================
        // Message Types
        // =============================================================

        private static class NetMsg
        {
            // Host → Client(s)
            public const byte AssignPlayer    = 1;
            public const byte SetupMap        = 2;
            public const byte SendClue        = 3;
            public const byte TurnStarted     = 4;
            public const byte PhaseChanged    = 5;
            public const byte QuestionAsked   = 6;
            public const byte PlaceToken      = 7;
            public const byte SearchResult    = 8;
            public const byte GameWon         = 9;
            public const byte GamePhaseChanged = 10;
            public const byte ShowError       = 11;
            public const byte PenaltyResult   = 12;

            // Client → Host
            public const byte ChooseAction    = 50;
            public const byte SubmitQuestion  = 51;
            public const byte SubmitSearch    = 52;
            public const byte SubmitPenalty   = 53;
            public const byte RequestStart    = 54;
        }

        // =============================================================
        // Synced State (maintained on all clients)
        // =============================================================

        private int _localPlayerIndex = -1;
        private int _currentPlayerIndex;
        private int _turnNumber;
        private TurnPhase _currentPhase;
        private GamePhase _gamePhase = GamePhase.Lobby;
        private int _playerCount;
        private string _myClueDescription;
        private bool _isHost;

        // =============================================================
        // Host-Only State
        // =============================================================

        private TurnManager _turnManager;
        private PuzzleSetup _puzzle;
        private PuzzleGenerator _puzzleGenerator;
        private readonly Dictionary<ulong, int> _clientPlayerMap = new();

        // =============================================================
        // Scene References
        // =============================================================

        private MapGenerator _mapGenerator;
        private TokenPlacer _tokenPlacer;
        private GameUIManager _uiManager;
        private TileInteractionSystem _tileInteraction;

        // =============================================================
        // ITurnState Implementation
        // =============================================================

        public int CurrentPlayerIndex => _currentPlayerIndex;
        public int TurnNumber => _turnNumber;
        public TurnPhase CurrentPhase => _currentPhase;
        public int PlayerCount => _playerCount;

        // =============================================================
        // Public Properties
        // =============================================================

        public int LocalPlayerIndex => _localPlayerIndex;
        public string MyClueDescription => _myClueDescription;
        public bool IsHost => _isHost;
        public GamePhase CurrentGamePhase => _gamePhase;
        public int ConnectedPlayerCount => _clientPlayerMap.Count;

        // =============================================================
        // Events (same signatures as TurnManager for UI binding)
        // =============================================================

        public event Action<int> OnTurnStarted;
        public event Action<int, TurnPhase> OnPhaseChanged;
        public event Action<int, int, HexCoordinates> OnQuestionAsked;
        public event Action<int, HexCoordinates, bool> OnResponseGiven;
        public event Action<int, HexCoordinates, bool> OnSearchPerformed;
        public event Action<int, HexCoordinates> OnSearchDiscPlaced;
        public event Action<int, HexCoordinates, bool> OnSearchVerification;
        public event Action<int, HexCoordinates> OnPenaltyCubePlaced;
        public event Action<int> OnGameWon;

        /// <summary>Fired when the high-level game phase changes.</summary>
        public event Action<GamePhase> OnGamePhaseChanged;

        /// <summary>Fired when the local player receives their clue.</summary>
        public event Action<string> OnClueReceived;

        /// <summary>Fired when the host sends an error message to this client.</summary>
        public event Action<string> OnError;

        /// <summary>Fired when a player connects/disconnects (host only). Arg: current player count.</summary>
        public event Action<int> OnPlayerCountChanged;

        // =============================================================
        // Initialization
        // =============================================================

        /// <summary>
        /// Called by <see cref="ConnectionManager"/> after NetworkManager starts.
        /// Sets up message handlers and scene references.
        /// </summary>
        public void Initialize(bool isHost)
        {
            _isHost = isHost;

            // Find scene references via GameService (registered by GameBootstrapper.Awake)
            _mapGenerator = GameService.Get<MapGenerator>();
            _tokenPlacer = GameService.Get<TokenPlacer>();
            _uiManager = GameService.Get<GameUIManager>();
            _tileInteraction = GameService.Get<TileInteractionSystem>();

            // Subscribe to tile clicks
            if (_tileInteraction != null)
                _tileInteraction.OnTileSelected += HandleTileClicked;

            // Subscribe to UI action buttons
            if (_uiManager != null)
            {
                _uiManager.OnActionChosen += HandleUIAction;
                _uiManager.OnRestartRequested += HandleRestart;
            }

            // Register custom message handler
            NetworkManager.Singleton.CustomMessagingManager
                .RegisterNamedMessageHandler(MSG_CHANNEL, HandleMessage);

            if (_isHost)
            {
                _puzzleGenerator = new PuzzleGenerator();

                // Host is player 0
                _clientPlayerMap[NetworkManager.Singleton.LocalClientId] = 0;
                _localPlayerIndex = 0;

                // Listen for client connections
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                Debug.Log("[NetworkGameManager] Initialized as HOST (Player 1)");
            }
            else
            {
                Debug.Log("[NetworkGameManager] Initialized as CLIENT (waiting for player assignment...)");
            }
        }

        private void OnDestroy()
        {
            if (_tileInteraction != null)
                _tileInteraction.OnTileSelected -= HandleTileClicked;

            if (_uiManager != null)
            {
                _uiManager.OnActionChosen -= HandleUIAction;
                _uiManager.OnRestartRequested -= HandleRestart;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.CustomMessagingManager?
                    .UnregisterNamedMessageHandler(MSG_CHANNEL);

                if (_isHost)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                }
            }
        }

        // =============================================================
        // Host: Player Management
        // =============================================================

        private void HandleClientConnected(ulong clientId)
        {
            if (!_isHost) return;

            // Skip self (host already assigned)
            if (clientId == NetworkManager.Singleton.LocalClientId) return;

            int playerIndex = _clientPlayerMap.Count;
            _clientPlayerMap[clientId] = playerIndex;

            // Tell the client their index
            SendToClient(clientId, NetMsg.AssignPlayer, w =>
            {
                w.WriteValueSafe(playerIndex);
            });

            OnPlayerCountChanged?.Invoke(_clientPlayerMap.Count);
            Debug.Log($"[NetworkGameManager] Client {clientId} → Player {playerIndex + 1} " +
                      $"({_clientPlayerMap.Count} players total)");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!_isHost) return;

            if (_clientPlayerMap.Remove(clientId))
            {
                OnPlayerCountChanged?.Invoke(_clientPlayerMap.Count);
                Debug.Log($"[NetworkGameManager] Client {clientId} disconnected " +
                          $"({_clientPlayerMap.Count} players remain)");
            }
        }

        // =============================================================
        // Host: Game Flow
        // =============================================================

        /// <summary>
        /// Host starts the game. Generates map, puzzle, and distributes clues.
        /// </summary>
        public void StartGame()
        {
            if (!_isHost)
            {
                Debug.LogWarning("[NetworkGameManager] Only the host can start the game!");
                return;
            }

            _playerCount = _clientPlayerMap.Count;
            if (_playerCount < 2)
            {
                Debug.LogWarning("[NetworkGameManager] Need at least 2 players!");
                return;
            }

            _gamePhase = GamePhase.Setup;
            OnGamePhaseChanged?.Invoke(_gamePhase);

            // Generate deterministic seeds
            int mapSeed = UnityEngine.Random.Range(0, int.MaxValue);
            int puzzleSeed = UnityEngine.Random.Range(0, int.MaxValue);

            // Generate map on host
            GenerateMapFromSeed(mapSeed);

            // Generate puzzle (host only)
            _puzzle = _puzzleGenerator.Generate(_mapGenerator.WorldMap, _playerCount, puzzleSeed);
            if (_puzzle == null)
            {
                Debug.LogError("[NetworkGameManager] Puzzle generation failed!");
                return;
            }

            // Create TurnManager (host only)
            _turnManager = new TurnManager(_playerCount, _puzzle);
            SubscribeTurnEvents();

            // Send setup data to all clients
            SendToAllClients(NetMsg.SetupMap, w =>
            {
                w.WriteValueSafe(mapSeed);
                w.WriteValueSafe(_playerCount);
            });

            // Send each client their clue (targeted)
            foreach (var kvp in _clientPlayerMap)
            {
                ulong clientId = kvp.Key;
                int playerIndex = kvp.Value;
                string clueDesc = _puzzle.PlayerClues[playerIndex].Description;

                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    // Host receives own clue directly
                    _myClueDescription = clueDesc;
                    OnClueReceived?.Invoke(clueDesc);
                }
                else
                {
                    SendToClient(clientId, NetMsg.SendClue, w =>
                    {
                        WriteString(w, clueDesc);
                        w.WriteValueSafe(playerIndex);
                    });
                }
            }

            // Bind UI
            if (_uiManager != null)
            {
                _uiManager.HumanPlayerIndex = _localPlayerIndex;
                _uiManager.BindNetworkGameplay(this, _playerCount);
            }

            // Transition to Playing
            _gamePhase = GamePhase.Playing;
            OnGamePhaseChanged?.Invoke(_gamePhase);
            SendToAllClients(NetMsg.GamePhaseChanged, w =>
            {
                w.WriteValueSafe((byte)GamePhase.Playing);
            });

            // Start turns
            _turnManager.StartFirstTurn();

            Debug.Log($"[NetworkGameManager] Game started! {_playerCount} players, " +
                      $"MapSeed={mapSeed}, PuzzleSeed={puzzleSeed}");
        }

        private void GenerateMapFromSeed(int seed)
        {
            if (_mapGenerator == null) return;

            // Disable legacy debuggers
            DisableLegacyDebuggers();

            _mapGenerator.SetSeed(seed);
            _mapGenerator.GenerateMap();
            _mapGenerator.SpawnVisuals();

            // Center camera
            var cam = FindFirstObjectByType<Cryptid.Systems.RTSCameraController>();
            if (cam != null && _mapGenerator.WorldMap != null)
                cam.CenterOnMap(_mapGenerator.WorldMap);
        }

        private void DisableLegacyDebuggers()
        {
            foreach (var d in FindObjectsByType<MapPieceDebugger>(FindObjectsSortMode.None))
                d.gameObject.SetActive(false);
            foreach (var d in FindObjectsByType<MapDebugOverlay>(FindObjectsSortMode.None))
                d.enabled = false;
            foreach (var d in FindObjectsByType<PuzzleDebugVisualizer>(FindObjectsSortMode.None))
                d.gameObject.SetActive(false);
            foreach (var d in FindObjectsByType<HexGridDebugger>(FindObjectsSortMode.None))
                d.gameObject.SetActive(false);
        }

        // =============================================================
        // Host: Turn Event Handlers (TurnManager → Broadcast)
        // =============================================================

        private void SubscribeTurnEvents()
        {
            _turnManager.OnTurnStarted += OnHost_TurnStarted;
            _turnManager.OnPhaseChanged += OnHost_PhaseChanged;
            _turnManager.OnQuestionAsked += OnHost_QuestionAsked;
            _turnManager.OnResponseGiven += OnHost_ResponseGiven;
            _turnManager.OnSearchDiscPlaced += OnHost_SearchDiscPlaced;
            _turnManager.OnSearchVerification += OnHost_SearchVerification;
            _turnManager.OnSearchPerformed += OnHost_SearchPerformed;
            _turnManager.OnPenaltyCubePlaced += OnHost_PenaltyCubePlaced;
            _turnManager.OnGameWon += OnHost_GameWon;
        }

        private void OnHost_TurnStarted(int playerIndex)
        {
            _currentPlayerIndex = playerIndex;
            _turnNumber = _turnManager.TurnNumber;
            _currentPhase = _turnManager.CurrentPhase;

            OnTurnStarted?.Invoke(playerIndex);

            SendToAllClients(NetMsg.TurnStarted, w =>
            {
                w.WriteValueSafe(playerIndex);
                w.WriteValueSafe(_turnNumber);
                w.WriteValueSafe((byte)_currentPhase);
            });
        }

        private void OnHost_PhaseChanged(int playerIndex, TurnPhase phase)
        {
            _currentPhase = phase;

            OnPhaseChanged?.Invoke(playerIndex, phase);

            SendToAllClients(NetMsg.PhaseChanged, w =>
            {
                w.WriteValueSafe(playerIndex);
                w.WriteValueSafe((byte)phase);
            });
        }

        private void OnHost_QuestionAsked(int asking, int target, HexCoordinates tile)
        {
            OnQuestionAsked?.Invoke(asking, target, tile);

            SendToAllClients(NetMsg.QuestionAsked, w =>
            {
                w.WriteValueSafe(asking);
                w.WriteValueSafe(target);
                WriteCoords(w, tile);
            });

            // Auto-respond (host evaluates the clue)
            _turnManager.AutoRespond(_mapGenerator.WorldMap);
        }

        private void OnHost_ResponseGiven(int responding, HexCoordinates tile, bool result)
        {
            TokenType type = result ? TokenType.Disc : TokenType.Cube;

            // Place locally
            _tokenPlacer?.PlaceTokenAt(tile, type, responding);
            OnResponseGiven?.Invoke(responding, tile, result);

            // Broadcast
            SendToAllClients(NetMsg.PlaceToken, w =>
            {
                WriteCoords(w, tile);
                w.WriteValueSafe((byte)type);
                w.WriteValueSafe(responding);
                w.WriteValueSafe(true); // isResponse
                w.WriteValueSafe(result);
            });
        }

        private void OnHost_SearchDiscPlaced(int player, HexCoordinates tile)
        {
            _tokenPlacer?.PlaceTokenAt(tile, TokenType.Disc, player);
            OnSearchDiscPlaced?.Invoke(player, tile);

            SendToAllClients(NetMsg.PlaceToken, w =>
            {
                WriteCoords(w, tile);
                w.WriteValueSafe((byte)TokenType.Disc);
                w.WriteValueSafe(player);
                w.WriteValueSafe(false); // not a response
                w.WriteValueSafe(false); // unused
            });
        }

        private void OnHost_SearchVerification(int verifier, HexCoordinates tile, bool result)
        {
            TokenType type = result ? TokenType.Disc : TokenType.Cube;
            _tokenPlacer?.PlaceTokenAt(tile, type, verifier);
            OnSearchVerification?.Invoke(verifier, tile, result);

            SendToAllClients(NetMsg.PlaceToken, w =>
            {
                WriteCoords(w, tile);
                w.WriteValueSafe((byte)type);
                w.WriteValueSafe(verifier);
                w.WriteValueSafe(false);
                w.WriteValueSafe(false);
            });
        }

        private void OnHost_SearchPerformed(int player, HexCoordinates tile, bool correct)
        {
            OnSearchPerformed?.Invoke(player, tile, correct);

            SendToAllClients(NetMsg.SearchResult, w =>
            {
                w.WriteValueSafe(player);
                WriteCoords(w, tile);
                w.WriteValueSafe(correct);
            });
        }

        private void OnHost_PenaltyCubePlaced(int player, HexCoordinates tile)
        {
            _tokenPlacer?.PlaceTokenAt(tile, TokenType.Cube, player);
            OnPenaltyCubePlaced?.Invoke(player, tile);

            SendToAllClients(NetMsg.PenaltyResult, w =>
            {
                w.WriteValueSafe(player);
                WriteCoords(w, tile);
            });
        }

        private void OnHost_GameWon(int winner)
        {
            _gamePhase = GamePhase.GameOver;
            OnGamePhaseChanged?.Invoke(_gamePhase);
            OnGameWon?.Invoke(winner);

            string answerInfo = _puzzle != null
                ? _puzzle.AnswerTile.Coordinates.ToString()
                : "";

            SendToAllClients(NetMsg.GameWon, w =>
            {
                w.WriteValueSafe(winner);
                WriteString(w, answerInfo);
            });
        }

        // =============================================================
        // Host: Process Client Actions
        // =============================================================

        private void ProcessChooseAction(int playerIndex, PlayerAction action)
        {
            if (!_isHost || _turnManager == null) return;
            if (playerIndex != _turnManager.CurrentPlayerIndex) return;
            _turnManager.ChooseAction(action);
        }

        private void ProcessSubmitQuestion(int playerIndex, HexCoordinates tile, int target)
        {
            if (!_isHost || _turnManager == null) return;
            if (playerIndex != _turnManager.CurrentPlayerIndex) return;
            if (!ValidateTileInteraction(tile, playerIndex)) return;
            _turnManager.SubmitQuestion(tile, target);
        }

        private void ProcessSubmitSearch(int playerIndex, HexCoordinates tile)
        {
            if (!_isHost || _turnManager == null) return;
            if (playerIndex != _turnManager.CurrentPlayerIndex) return;
            if (!ValidateTileInteraction(tile, playerIndex)) return;
            _turnManager.SubmitSearch(tile, _mapGenerator.WorldMap);
        }

        private void ProcessSubmitPenalty(int playerIndex, HexCoordinates tile, ulong senderClientId)
        {
            if (!_isHost || _turnManager == null) return;
            if (playerIndex != _turnManager.CurrentPlayerIndex) return;
            if (!ValidateTileInteraction(tile, playerIndex)) return;

            bool accepted = _turnManager.SubmitPenaltyCube(tile, _mapGenerator.WorldMap);
            if (!accepted)
            {
                SendToClient(senderClientId, NetMsg.ShowError, w =>
                {
                    WriteString(w, "That tile matches your clue! Choose a different tile.");
                });
            }
        }

        private bool ValidateTileInteraction(HexCoordinates coords, int playerIndex)
        {
            if (_tokenPlacer == null) return true;
            return _tokenPlacer.CanInteract(coords, playerIndex);
        }

        // =============================================================
        // Tile & UI Input Handlers (runs on local client)
        // =============================================================

        private void HandleTileClicked(HexTile tile)
        {
            if (tile == null) return;
            if (_gamePhase != GamePhase.Playing) return;
            if (_currentPlayerIndex != _localPlayerIndex) return;

            var coords = tile.Coordinates;

            switch (_currentPhase)
            {
                case TurnPhase.SelectTile:
                    int target = (_uiManager != null && _uiManager.SelectedTargetPlayer >= 0)
                        ? _uiManager.SelectedTargetPlayer
                        : (_localPlayerIndex + 1) % _playerCount;

                    if (_isHost)
                        ProcessSubmitQuestion(_localPlayerIndex, coords, target);
                    else
                        SendToHost(NetMsg.SubmitQuestion, w =>
                        {
                            WriteCoords(w, coords);
                            w.WriteValueSafe(target);
                        });
                    break;

                case TurnPhase.Search:
                    if (_isHost)
                        ProcessSubmitSearch(_localPlayerIndex, coords);
                    else
                        SendToHost(NetMsg.SubmitSearch, w => WriteCoords(w, coords));
                    break;

                case TurnPhase.PenaltyPlacement:
                    if (_isHost)
                        ProcessSubmitPenalty(_localPlayerIndex, coords,
                            NetworkManager.Singleton.LocalClientId);
                    else
                        SendToHost(NetMsg.SubmitPenalty, w => WriteCoords(w, coords));
                    break;
            }
        }

        private void HandleUIAction(PlayerAction action)
        {
            if (_currentPlayerIndex != _localPlayerIndex) return;

            if (_isHost)
                ProcessChooseAction(_localPlayerIndex, action);
            else
                SendToHost(NetMsg.ChooseAction, w => w.WriteValueSafe((byte)action));
        }

        private void HandleRestart()
        {
            // Simple restart: shutdown network and reload scene
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();
            GameService.ClearAll();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        // =============================================================
        // Message Dispatcher
        // =============================================================

        private void HandleMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte msgType);

            switch (msgType)
            {
                // Host → Client messages
                case NetMsg.AssignPlayer:     Handle_AssignPlayer(reader); break;
                case NetMsg.SetupMap:         Handle_SetupMap(reader); break;
                case NetMsg.SendClue:         Handle_SendClue(reader); break;
                case NetMsg.TurnStarted:      Handle_TurnStarted(reader); break;
                case NetMsg.PhaseChanged:     Handle_PhaseChanged(reader); break;
                case NetMsg.QuestionAsked:    Handle_QuestionAsked(reader); break;
                case NetMsg.PlaceToken:       Handle_PlaceToken(reader); break;
                case NetMsg.SearchResult:     Handle_SearchResult(reader); break;
                case NetMsg.GameWon:          Handle_GameWon(reader); break;
                case NetMsg.GamePhaseChanged: Handle_GamePhaseChanged(reader); break;
                case NetMsg.ShowError:        Handle_ShowError(reader); break;
                case NetMsg.PenaltyResult:    Handle_PenaltyResult(reader); break;

                // Client → Host messages
                case NetMsg.ChooseAction:     Handle_ChooseAction(senderId, reader); break;
                case NetMsg.SubmitQuestion:   Handle_SubmitQuestion(senderId, reader); break;
                case NetMsg.SubmitSearch:     Handle_SubmitSearch(senderId, reader); break;
                case NetMsg.SubmitPenalty:     Handle_SubmitPenalty(senderId, reader); break;
                case NetMsg.RequestStart:     Handle_RequestStart(senderId); break;

                default:
                    Debug.LogWarning($"[NetworkGameManager] Unknown message type: {msgType}");
                    break;
            }
        }

        // =============================================================
        // Client Message Handlers (Host → Client)
        // =============================================================

        private void Handle_AssignPlayer(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int playerIndex);
            _localPlayerIndex = playerIndex;
            if (_uiManager != null)
                _uiManager.HumanPlayerIndex = playerIndex;
            Debug.Log($"[NetworkGameManager] Assigned as Player {playerIndex + 1}");
        }

        private void Handle_SetupMap(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int mapSeed);
            reader.ReadValueSafe(out int playerCount);

            _playerCount = playerCount;
            GenerateMapFromSeed(mapSeed);

            // Bind UI for network mode
            if (_uiManager != null)
            {
                _uiManager.HumanPlayerIndex = _localPlayerIndex;
                _uiManager.BindNetworkGameplay(this, _playerCount);
            }

            Debug.Log($"[NetworkGameManager] Map generated from seed {mapSeed}. " +
                      $"{_playerCount} players.");
        }

        private void Handle_SendClue(FastBufferReader reader)
        {
            string clue = ReadString(reader);
            reader.ReadValueSafe(out int playerIndex);

            _localPlayerIndex = playerIndex;
            _myClueDescription = clue;
            OnClueReceived?.Invoke(clue);
            Debug.Log($"[NetworkGameManager] Received clue: \"{clue}\" (Player {playerIndex + 1})");
        }

        private void Handle_TurnStarted(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int playerIndex);
            reader.ReadValueSafe(out int turnNumber);
            reader.ReadValueSafe(out byte phase);

            _currentPlayerIndex = playerIndex;
            _turnNumber = turnNumber;
            _currentPhase = (TurnPhase)phase;

            OnTurnStarted?.Invoke(playerIndex);
        }

        private void Handle_PhaseChanged(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int playerIndex);
            reader.ReadValueSafe(out byte phase);

            _currentPhase = (TurnPhase)phase;
            OnPhaseChanged?.Invoke(playerIndex, _currentPhase);
        }

        private void Handle_QuestionAsked(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int asking);
            reader.ReadValueSafe(out int target);
            var tile = ReadCoords(reader);

            OnQuestionAsked?.Invoke(asking, target, tile);
        }

        private void Handle_PlaceToken(FastBufferReader reader)
        {
            var tile = ReadCoords(reader);
            reader.ReadValueSafe(out byte tokenTypeByte);
            reader.ReadValueSafe(out int playerIndex);
            reader.ReadValueSafe(out bool isResponse);
            reader.ReadValueSafe(out bool result);

            var tokenType = (TokenType)tokenTypeByte;

            // Place token visually
            _tokenPlacer?.PlaceTokenAt(tile, tokenType, playerIndex);

            // Fire appropriate event based on context
            if (isResponse)
            {
                OnResponseGiven?.Invoke(playerIndex, tile, result);
            }
            else
            {
                // Could be search disc or verification — fire generic events
                if (tokenType == TokenType.Disc)
                    OnSearchDiscPlaced?.Invoke(playerIndex, tile);
                else
                    OnSearchVerification?.Invoke(playerIndex, tile, false);
            }
        }

        private void Handle_SearchResult(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int player);
            var tile = ReadCoords(reader);
            reader.ReadValueSafe(out bool correct);

            OnSearchPerformed?.Invoke(player, tile, correct);
        }

        private void Handle_PenaltyResult(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int player);
            var tile = ReadCoords(reader);

            _tokenPlacer?.PlaceTokenAt(tile, TokenType.Cube, player);
            OnPenaltyCubePlaced?.Invoke(player, tile);
        }

        private void Handle_GameWon(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int winner);
            string answerInfo = ReadString(reader);

            _gamePhase = GamePhase.GameOver;
            OnGamePhaseChanged?.Invoke(_gamePhase);
            OnGameWon?.Invoke(winner);

            // Show game over panel directly
            if (_uiManager != null)
            {
                _uiManager.ShowNetworkGameOver(winner, answerInfo);
            }
        }

        private void Handle_GamePhaseChanged(FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte phase);
            _gamePhase = (GamePhase)phase;
            OnGamePhaseChanged?.Invoke(_gamePhase);
        }

        private void Handle_ShowError(FastBufferReader reader)
        {
            string msg = ReadString(reader);
            _uiManager?.LogPanel?.AddSystemMessage($"  \u26a0 {msg}");
            OnError?.Invoke(msg);
        }

        // =============================================================
        // Host Message Handlers (Client → Host)
        // =============================================================

        private void Handle_ChooseAction(ulong senderId, FastBufferReader reader)
        {
            if (!_isHost) return;
            reader.ReadValueSafe(out byte action);

            if (!_clientPlayerMap.TryGetValue(senderId, out int playerIndex)) return;
            ProcessChooseAction(playerIndex, (PlayerAction)action);
        }

        private void Handle_SubmitQuestion(ulong senderId, FastBufferReader reader)
        {
            if (!_isHost) return;
            var tile = ReadCoords(reader);
            reader.ReadValueSafe(out int target);

            if (!_clientPlayerMap.TryGetValue(senderId, out int playerIndex)) return;
            ProcessSubmitQuestion(playerIndex, tile, target);
        }

        private void Handle_SubmitSearch(ulong senderId, FastBufferReader reader)
        {
            if (!_isHost) return;
            var tile = ReadCoords(reader);

            if (!_clientPlayerMap.TryGetValue(senderId, out int playerIndex)) return;
            ProcessSubmitSearch(playerIndex, tile);
        }

        private void Handle_SubmitPenalty(ulong senderId, FastBufferReader reader)
        {
            if (!_isHost) return;
            var tile = ReadCoords(reader);

            if (!_clientPlayerMap.TryGetValue(senderId, out int playerIndex)) return;
            ProcessSubmitPenalty(playerIndex, tile, senderId);
        }

        private void Handle_RequestStart(ulong senderId)
        {
            if (!_isHost) return;
            Debug.Log($"[NetworkGameManager] Client {senderId} requested game start.");
            // Only host triggers start directly (via ConnectionManager button)
        }

        // =============================================================
        // Message Sending Helpers
        // =============================================================

        private void SendToHost(byte msgType, Action<FastBufferWriter> writeBody = null)
        {
            using var writer = new FastBufferWriter(512, Allocator.Temp);
            writer.WriteValueSafe(msgType);
            writeBody?.Invoke(writer);

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                MSG_CHANNEL, NetworkManager.ServerClientId,
                writer, NetworkDelivery.ReliableSequenced);
        }

        private void SendToAllClients(byte msgType, Action<FastBufferWriter> writeBody = null)
        {
            using var writer = new FastBufferWriter(512, Allocator.Temp);
            writer.WriteValueSafe(msgType);
            writeBody?.Invoke(writer);

            var serverClientId = NetworkManager.ServerClientId;
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == serverClientId) continue; // Skip host
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                    MSG_CHANNEL, clientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        private void SendToClient(ulong clientId, byte msgType,
            Action<FastBufferWriter> writeBody = null)
        {
            using var writer = new FastBufferWriter(512, Allocator.Temp);
            writer.WriteValueSafe(msgType);
            writeBody?.Invoke(writer);

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                MSG_CHANNEL, clientId, writer, NetworkDelivery.ReliableSequenced);
        }

        // =============================================================
        // Serialization Helpers
        // =============================================================

        private static void WriteCoords(FastBufferWriter writer, HexCoordinates c)
        {
            writer.WriteValueSafe(c.X);
            writer.WriteValueSafe(c.Y);
            writer.WriteValueSafe(c.Z);
        }

        private static HexCoordinates ReadCoords(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int x);
            reader.ReadValueSafe(out int y);
            reader.ReadValueSafe(out int z);
            return new HexCoordinates(x, y, z);
        }

        private static void WriteString(FastBufferWriter writer, string s)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(s ?? "");
            writer.WriteValueSafe(data.Length);
            if (data.Length > 0)
                writer.WriteBytesSafe(data);
        }

        private static string ReadString(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int len);
            if (len <= 0) return "";
            byte[] data = new byte[len];
            reader.ReadBytesSafe(ref data, len);
            return System.Text.Encoding.UTF8.GetString(data);
        }
    }
}
