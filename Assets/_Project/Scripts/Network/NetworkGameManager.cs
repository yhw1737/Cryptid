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
            public const byte LobbyUpdate    = 13;
            public const byte PlayerNames    = 14;
            public const byte TimerSync      = 15;
            public const byte TimerStop      = 16;
            public const byte ChatMessage    = 17;
            public const byte RemoveDiscs    = 18;

            // Client → Host
            public const byte ChooseAction    = 50;
            public const byte SubmitQuestion  = 51;
            public const byte SubmitSearch    = 52;
            public const byte SubmitPenalty   = 53;
            public const byte RequestStart    = 54;
            public const byte SetNickname    = 55;
            public const byte ToggleReady    = 56;
            public const byte SendChat       = 57;
        }

        // =============================================================
        // Lobby Data
        // =============================================================

        public struct LobbyPlayerInfo
        {
            public int PlayerIndex;
            public string Nickname;
            public bool IsReady;
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
        private readonly Dictionary<ulong, string> _playerNicknames = new();
        private readonly Dictionary<ulong, bool> _playerReady = new();
        private readonly HashSet<int> _disconnectedPlayers = new();
        private string[] _playerNames;

        // =============================================================
        // Scene References
        // =============================================================

        private MapGenerator _mapGenerator;
        private TokenPlacer _tokenPlacer;
        private GameUIManager _uiManager;
        private TileInteractionSystem _tileInteraction;
        private TurnTimer _turnTimer; // Host-side timer for network sync

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
        public string[] AllPlayerNames => _playerNames;

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

        /// <summary>Fired when lobby state changes (player list, nicknames, ready status).</summary>
        public event Action<LobbyPlayerInfo[]> OnLobbyUpdated;

        /// <summary>Fired on clients when the host sends all player names at game start.</summary>
        public event Action<string[]> OnPlayerNamesReceived;

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

                // Subscribe to chat input
                if (_uiManager.LogPanel != null)
                    _uiManager.LogPanel.OnChatMessageSent += SendChatMessage;
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
                _playerNicknames[NetworkManager.Singleton.LocalClientId] = "Player 1";
                _playerReady[NetworkManager.Singleton.LocalClientId] = false;

                // Listen for client connections
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                // Show host in lobby immediately
                BroadcastLobbyUpdate();

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
            if (clientId == NetworkManager.Singleton.LocalClientId) return;

            // Enforce max 5 players
            if (_clientPlayerMap.Count >= 5)
            {
                Debug.LogWarning($"[NetworkGameManager] Rejecting client {clientId} — max 5 players.");
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }

            int playerIndex = _clientPlayerMap.Count;
            _clientPlayerMap[clientId] = playerIndex;
            _playerNicknames[clientId] = $"Player {playerIndex + 1}";
            _playerReady[clientId] = false;

            SendToClient(clientId, NetMsg.AssignPlayer, w =>
            {
                w.WriteValueSafe(playerIndex);
            });

            OnPlayerCountChanged?.Invoke(_clientPlayerMap.Count);
            BroadcastLobbyUpdate();

            // Log connection event
            string connectName = $"Player {playerIndex + 1}";
            _uiManager?.LogPanel?.AddSystemMessage(L.Format("player_connected", connectName));

            // Broadcast connect event to all clients
            SendToAllClients(NetMsg.ChatMessage, w =>
            {
                w.WriteValueSafe(-1); // system message
                WriteString(w, L.Format("player_connected", connectName));
                WriteString(w, "");
            });

            Debug.Log($"[NetworkGameManager] Client {clientId} → Player {playerIndex + 1} " +
                      $"({_clientPlayerMap.Count} players total)");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!_isHost) return;

            if (_clientPlayerMap.TryGetValue(clientId, out int playerIndex))
            {
                string disconnectName = _playerNicknames.TryGetValue(clientId, out var dn)
                    ? dn : $"Player {playerIndex + 1}";
                _clientPlayerMap.Remove(clientId);
                _playerNicknames.Remove(clientId);
                _playerReady.Remove(clientId);
                OnPlayerCountChanged?.Invoke(_clientPlayerMap.Count);

                if (_gamePhase == GamePhase.Playing)
                {
                    // Mid-game disconnect: mark player as disconnected, skip their turns
                    _disconnectedPlayers.Add(playerIndex);
                    string name = _playerNames != null && playerIndex < _playerNames.Length
                        ? _playerNames[playerIndex]
                        : L.Format("player_default", playerIndex + 1);
                    Debug.Log($"[NetworkGameManager] Player {playerIndex + 1} ({name}) " +
                              $"disconnected mid-game. Will skip their turns.");

                    // If it was the disconnected player's turn, skip to next
                    if (_turnManager != null && _turnManager.CurrentPlayerIndex == playerIndex)
                    {
                        _turnManager.SkipTurn();
                        // Keep skipping until we find a connected player
                        SkipDisconnectedPlayers();
                    }

                    // Check if only one player remains
                    int activePlayers = _playerCount - _disconnectedPlayers.Count;
                    if (activePlayers <= 1)
                    {
                        // Find the remaining player and declare them the winner
                        for (int i = 0; i < _playerCount; i++)
                        {
                            if (!_disconnectedPlayers.Contains(i))
                            {
                                OnHost_GameWon(i);
                                return;
                            }
                        }
                    }
                }
                else
                {
                    BroadcastLobbyUpdate();
                }

                // Log disconnect event
                _uiManager?.LogPanel?.AddSystemMessage(L.Format("player_disconnected", disconnectName));

                // Broadcast disconnect event to remaining clients
                SendToAllClients(NetMsg.ChatMessage, w =>
                {
                    w.WriteValueSafe(-1); // system message
                    WriteString(w, L.Format("player_disconnected", disconnectName));
                    WriteString(w, "");
                });

                Debug.Log($"[NetworkGameManager] Client {clientId} disconnected " +
                          $"({_clientPlayerMap.Count} players remain)");
            }
        }

        /// <summary>
        /// Checks if the current turn player is disconnected and skips forward.
        /// Called after a disconnect or after turn advance.
        /// </summary>
        private void SkipDisconnectedPlayers()
        {
            if (_turnManager == null) return;

            int safetyCount = 0;
            while (_disconnectedPlayers.Contains(_turnManager.CurrentPlayerIndex)
                   && safetyCount < _playerCount)
            {
                _turnManager.SkipTurn();
                safetyCount++;
            }
        }

        /// <summary>Whether a given player index is disconnected.</summary>
        public bool IsPlayerDisconnected(int playerIndex) =>
            _disconnectedPlayers.Contains(playerIndex);

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
            if (_playerCount < 3 || _playerCount > 5)
            {
                Debug.LogWarning($"[NetworkGameManager] Need 3-5 players! Current: {_playerCount}");
                return;
            }

            // Build player names
            _playerNames = new string[_playerCount];
            foreach (var kvp in _clientPlayerMap)
            {
                int idx = kvp.Value;
                _playerNames[idx] = _playerNicknames.TryGetValue(kvp.Key, out var n)
                    ? n : $"Player {idx + 1}";
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

            // Send player names to all clients
            SendToAllClients(NetMsg.PlayerNames, w =>
            {
                w.WriteValueSafe(_playerCount);
                for (int i = 0; i < _playerCount; i++)
                    WriteString(w, _playerNames[i]);
            });
            OnPlayerNamesReceived?.Invoke(_playerNames);

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

            // Initialize host timer for sync
            InitializeHostTimer();

            // Transition to Playing
            _gamePhase = GamePhase.Playing;
            OnGamePhaseChanged?.Invoke(_gamePhase);

            // Switch BGM from lobby to in-game track
            AudioManager.Instance?.PlayIngameBGM();

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

            _mapGenerator.SetSeed(seed);
            _mapGenerator.GenerateMap();
            _mapGenerator.SpawnVisuals();

            // Center camera
            var cam = FindFirstObjectByType<Cryptid.Systems.RTSCameraController>();
            if (cam != null && _mapGenerator.WorldMap != null)
                cam.CenterOnMap(_mapGenerator.WorldMap);
        }

        // =============================================================
        // Lobby: Public API
        // =============================================================

        /// <summary>Sets the local player's display name and notifies the host.</summary>
        public void SetLocalNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname)) return;

            if (_isHost)
            {
                var localId = NetworkManager.Singleton.LocalClientId;
                if (_playerReady.TryGetValue(localId, out var ready) && ready) return;
                _playerNicknames[localId] = nickname;
                BroadcastLobbyUpdate();
            }
            else
            {
                SendToHost(NetMsg.SetNickname, w => WriteString(w, nickname));
            }
        }

        /// <summary>Toggles the local player's ready state.</summary>
        public void SetLocalReady(bool ready)
        {
            if (_isHost)
            {
                var localId = NetworkManager.Singleton.LocalClientId;
                _playerReady[localId] = ready;
                BroadcastLobbyUpdate();
            }
            else
            {
                SendToHost(NetMsg.ToggleReady, w => w.WriteValueSafe(ready));
            }
        }

        /// <summary>Returns true if the host can start the game (3-5 players, all ready).</summary>
        public bool CanStartGame()
        {
            if (!_isHost) return false;
            int count = _clientPlayerMap.Count;
            if (count < 3 || count > 5) return false;
            foreach (var kvp in _playerReady)
                if (!kvp.Value) return false;
            return true;
        }

        private void BroadcastLobbyUpdate()
        {
            var infos = BuildLobbyPlayerInfos();
            OnLobbyUpdated?.Invoke(infos);

            SendToAllClients(NetMsg.LobbyUpdate, w =>
            {
                w.WriteValueSafe(infos.Length);
                for (int i = 0; i < infos.Length; i++)
                {
                    w.WriteValueSafe(infos[i].PlayerIndex);
                    WriteString(w, infos[i].Nickname);
                    w.WriteValueSafe(infos[i].IsReady);
                }
            });
        }

        private LobbyPlayerInfo[] BuildLobbyPlayerInfos()
        {
            var infos = new LobbyPlayerInfo[_clientPlayerMap.Count];
            int idx = 0;
            foreach (var kvp in _clientPlayerMap)
            {
                infos[idx] = new LobbyPlayerInfo
                {
                    PlayerIndex = kvp.Value,
                    Nickname = _playerNicknames.TryGetValue(kvp.Key, out var name)
                        ? name : $"Player {kvp.Value + 1}",
                    IsReady = _playerReady.TryGetValue(kvp.Key, out var r) && r
                };
                idx++;
            }
            System.Array.Sort(infos, (a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            return infos;
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
            _turnManager.OnSearchPrepared += OnHost_SearchPrepared;
            _turnManager.OnPenaltyCubePlaced += OnHost_PenaltyCubePlaced;
            _turnManager.OnGameWon += OnHost_GameWon;
        }

        private void OnHost_TurnStarted(int playerIndex)
        {
            // Skip disconnected players
            if (_disconnectedPlayers.Contains(playerIndex))
            {
                SkipDisconnectedPlayers();
                return;
            }

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

            // Clear dimming from previous phase
            ClearTileDimming();

            OnPhaseChanged?.Invoke(playerIndex, phase);

            SendToAllClients(NetMsg.PhaseChanged, w =>
            {
                w.WriteValueSafe(playerIndex);
                w.WriteValueSafe((byte)phase);
            });

            // Timer management (host-side)
            if (_turnTimer != null)
            {
                switch (phase)
                {
                    case TurnPhase.ChooseAction:
                    case TurnPhase.SelectTile:
                    case TurnPhase.Search:
                        _turnTimer.StartTurnTimer();
                        BroadcastTimerStart(TurnTimer.TurnDuration);
                        break;

                    case TurnPhase.PenaltyPlacement:
                        _turnTimer.StartPenaltyTimer();
                        BroadcastTimerStart(TurnTimer.PenaltyDuration);
                        ApplyPenaltyDimming(playerIndex);
                        break;

                    default:
                        _turnTimer.Stop();
                        BroadcastTimerStop();
                        _uiManager?.TurnIndicator?.HideTimer();
                        break;
                }
            }
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

        /// <summary>
        /// Handles prepared search data on host. Starts animated reveal coroutine.
        /// Each verification step is revealed with a delay, then broadcasts to clients.
        /// </summary>
        private void OnHost_SearchPrepared(int searcherIndex, HexCoordinates tile,
            System.Collections.Generic.List<SearchVerificationStep> steps)
        {
            StartCoroutine(AnimateNetworkSearchVerification(searcherIndex, tile, steps));
        }

        private System.Collections.IEnumerator AnimateNetworkSearchVerification(
            int searcherIndex, HexCoordinates tile,
            System.Collections.Generic.List<SearchVerificationStep> steps)
        {
            const float stepDelay = 1.0f;
            bool searchFailed = false;

            yield return new UnityEngine.WaitForSeconds(stepDelay);

            foreach (var step in steps)
            {
                // FireSearchVerification triggers OnHost_SearchVerification
                // which places token locally and broadcasts to clients
                _turnManager.FireSearchVerification(step.VerifierIndex, tile, step.Result);

                if (!step.Result)
                {
                    searchFailed = true;
                    yield return new UnityEngine.WaitForSeconds(stepDelay * 0.5f);
                    break;
                }

                yield return new UnityEngine.WaitForSeconds(stepDelay);
            }

            // Remove discs on search failure
            if (searchFailed && _tokenPlacer != null)
            {
                _tokenPlacer.RemoveDiscsAt(tile);
                // Broadcast disc removal to clients
                SendToAllClients(NetMsg.RemoveDiscs, w => WriteCoords(w, tile));
            }

            yield return new UnityEngine.WaitForSeconds(stepDelay * 0.5f);

            // FinalizeSearch triggers OnHost_SearchPerformed which broadcasts
            _turnManager.FinalizeSearch(tile, !searchFailed);
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
                    WriteString(w, L.Get("warn_matches_clue"));
                });
            }
        }

        private bool ValidateTileInteraction(HexCoordinates coords, int playerIndex)
        {
            if (_tokenPlacer == null) return true;

            // Cubes always block
            if (_tokenPlacer.HasAnyCube(coords)) return false;

            // For Questions (SelectTile), discs are allowed — only cubes block
            bool isQuestion = _turnManager?.CurrentPhase == TurnPhase.SelectTile;
            if (!isQuestion && _tokenPlacer.HasPlayerToken(coords, playerIndex))
                return false;

            return true;
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
            if (_isHost)
            {
                ReturnToLobby();
            }
            else
            {
                // Client requests restart from host
                SendToHost(NetMsg.RequestStart, w => { });
            }
        }

        // =============================================================
        // Return to Lobby (preserves connections)
        // =============================================================

        /// <summary>
        /// Resets the game state and returns all players to the lobby.
        /// Host keeps the room alive; all clients remain connected.
        /// </summary>
        public void ReturnToLobby()
        {
            if (!_isHost) return;

            // 1. Clean up host game state
            CleanupGameState();

            // 2. Reset lobby ready states
            var keys = new List<ulong>(_playerReady.Keys);
            foreach (var key in keys)
                _playerReady[key] = false;
            _disconnectedPlayers.Clear();

            // 3. Transition to Lobby phase
            _gamePhase = GamePhase.Lobby;
            OnGamePhaseChanged?.Invoke(_gamePhase);

            // 4. Broadcast lobby return to all clients
            SendToAllClients(NetMsg.GamePhaseChanged, w =>
            {
                w.WriteValueSafe((byte)GamePhase.Lobby);
            });

            // 5. Update lobby display
            BroadcastLobbyUpdate();

            // 6. Show lobby UI on host
            var connectionMgr = FindFirstObjectByType<ConnectionManager>();
            connectionMgr?.ShowLobbyAfterGame();

            Debug.Log("[NetworkGameManager] Returned to lobby. Connections preserved.");
        }

        /// <summary>
        /// Cleans up all game-specific state (tokens, map visuals, turn manager).
        /// Preserves network connections and player assignments.
        /// </summary>
        private void CleanupGameState()
        {
            // Stop timer
            if (_turnTimer != null)
            {
                _turnTimer.Stop();
                Destroy(_turnTimer);
                _turnTimer = null;
            }

            // Clear tile dimming
            ClearTileDimming();

            // Unsubscribe turn events
            if (_turnManager != null)
            {
                _turnManager.OnTurnStarted      -= OnHost_TurnStarted;
                _turnManager.OnPhaseChanged      -= OnHost_PhaseChanged;
                _turnManager.OnQuestionAsked     -= OnHost_QuestionAsked;
                _turnManager.OnResponseGiven     -= OnHost_ResponseGiven;
                _turnManager.OnSearchDiscPlaced  -= OnHost_SearchDiscPlaced;
                _turnManager.OnSearchVerification -= OnHost_SearchVerification;
                _turnManager.OnSearchPerformed   -= OnHost_SearchPerformed;
                _turnManager.OnSearchPrepared    -= OnHost_SearchPrepared;
                _turnManager.OnPenaltyCubePlaced -= OnHost_PenaltyCubePlaced;
                _turnManager.OnGameWon           -= OnHost_GameWon;
                _turnManager = null;
            }

            // Clear tokens
            _tokenPlacer?.ClearAllTokens();

            // Clear map visuals (keep the generator for re-generation)
            _mapGenerator?.ClearVisuals();

            // Clear puzzle
            _puzzle = null;
            _myClueDescription = null;
            _playerNames = null;

            // Reset turn state
            _currentPlayerIndex = 0;
            _turnNumber = 0;
            _currentPhase = TurnPhase.ChooseAction;

            // Reset UI
            _uiManager?.ShowLobbyStatePublic();
        }

        /// <summary>
        /// Client-side cleanup when returning to lobby.
        /// </summary>
        private void CleanupClientGameState()
        {
            // Stop client timer
            if (_turnTimer != null)
            {
                _turnTimer.Stop();
                Destroy(_turnTimer);
                _turnTimer = null;
            }

            ClearTileDimming();
            _tokenPlacer?.ClearAllTokens();
            _mapGenerator?.ClearVisuals();
            _myClueDescription = null;
            _playerNames = null;
            _currentPlayerIndex = 0;
            _turnNumber = 0;
            _currentPhase = TurnPhase.ChooseAction;
            _uiManager?.ShowLobbyStatePublic();
        }

        // =============================================================
        // Host: Timer & Penalty Dimming
        // =============================================================

        /// <summary>Initializes the turn timer on the host. Called after StartGame.</summary>
        private void InitializeHostTimer()
        {
            _turnTimer = gameObject.AddComponent<TurnTimer>();
            _turnTimer.OnTimerTick += OnHost_TimerTick;
            _turnTimer.OnTimerExpired += OnHost_TimerExpired;
        }

        private void OnHost_TimerTick(float remaining)
        {
            _uiManager?.TurnIndicator?.UpdateTimer(remaining);
        }

        private void OnHost_TimerExpired()
        {
            if (_turnManager == null) return;

            var phase = _turnManager.CurrentPhase;
            int player = _turnManager.CurrentPlayerIndex;

            _uiManager?.TurnIndicator?.HideTimer();
            BroadcastTimerStop();

            if (phase == TurnPhase.PenaltyPlacement)
            {
                AutoPlacePenaltyCube(player);
            }
            else if (phase != TurnPhase.TurnEnd)
            {
                // Time ran out during regular turn — force into penalty phase, auto-place cube
                _uiManager?.LogPanel?.AddEntry(player, L.Get("timer_expired"));
                _turnManager.ForcePhase(TurnPhase.PenaltyPlacement);
                AutoPlacePenaltyCube(player);
            }
        }

        private void AutoPlacePenaltyCube(int playerIndex)
        {
            if (_puzzle == null || _mapGenerator?.WorldMap == null) return;

            var clue = _puzzle.PlayerClues[playerIndex];
            foreach (var kvp in _mapGenerator.WorldMap)
            {
                if (_tokenPlacer != null &&
                    (_tokenPlacer.HasAnyCube(kvp.Key) || _tokenPlacer.HasPlayerToken(kvp.Key, playerIndex)))
                    continue;

                if (!clue.Check(kvp.Value, _mapGenerator.WorldMap))
                {
                    _uiManager?.LogPanel?.AddEntry(playerIndex, L.Get("timer_auto_penalty"));
                    ClearTileDimming();
                    _turnManager.SubmitPenaltyCube(kvp.Key, _mapGenerator.WorldMap);
                    return;
                }
            }

            // No valid tile — skip turn
            _uiManager?.LogPanel?.AddEntry(playerIndex, L.Get("timer_auto_penalty"));
            ClearTileDimming();
            _turnManager.SkipTurn();
        }

        private void BroadcastTimerStart(float duration)
        {
            SendToAllClients(NetMsg.TimerSync, w =>
            {
                w.WriteValueSafe(duration);
            });
        }

        private void BroadcastTimerStop()
        {
            SendToAllClients(NetMsg.TimerStop, w => { });
        }

        // ---------------------------------------------------------
        // Penalty Tile Dimming (both host and client)
        // ---------------------------------------------------------

        /// <summary>
        /// Dims tiles that cannot receive a penalty cube.
        /// On host: uses actual clue data.
        /// </summary>
        private void ApplyPenaltyDimming(int playerIndex)
        {
            if (_mapGenerator?.WorldMap == null) return;

            // Only the host has puzzle data; clients get dimming from PhaseChanged handler
            if (!_isHost || _puzzle == null) return;

            var clue = _puzzle.PlayerClues[playerIndex];
            bool hasAnyValid = false;

            foreach (var kvp in _mapGenerator.WorldMap)
            {
                var hexTile = _mapGenerator.GetHexTile(kvp.Key);
                if (hexTile == null) continue;

                bool hasCube = _tokenPlacer != null && _tokenPlacer.HasAnyCube(kvp.Key);
                bool hasToken = _tokenPlacer != null && _tokenPlacer.HasPlayerToken(kvp.Key, playerIndex);
                bool clueMatches = clue.Check(kvp.Value, _mapGenerator.WorldMap);
                bool isInvalid = clueMatches || hasCube || hasToken;

                hexTile.SetDimmed(isInvalid);
                if (!isInvalid) hasAnyValid = true;
            }

            if (!hasAnyValid)
            {
                _uiManager?.LogPanel?.AddSystemMessage(L.Get("timer_auto_penalty"));
                ClearTileDimming();
                _turnManager.SkipTurn();
            }
        }

        /// <summary>
        /// Applies dimming on client side during penalty placement.
        /// Clients don't have clue data for other players, so they dim tiles with cubes only.
        /// For the local player, dim tiles that match their clue.
        /// </summary>
        private void ApplyClientPenaltyDimming(int playerIndex)
        {
            if (_mapGenerator?.WorldMap == null) return;

            foreach (var kvp in _mapGenerator.WorldMap)
            {
                var hexTile = _mapGenerator.GetHexTile(kvp.Key);
                if (hexTile == null) continue;

                bool hasCube = _tokenPlacer != null && _tokenPlacer.HasAnyCube(kvp.Key);
                // Dim tiles with cubes for everyone; clients can't see other players' clues
                hexTile.SetDimmed(hasCube);
            }
        }

        private void ClearTileDimming()
        {
            if (_mapGenerator?.WorldMap == null) return;

            foreach (var kvp in _mapGenerator.WorldMap)
            {
                var hexTile = _mapGenerator.GetHexTile(kvp.Key);
                hexTile?.SetDimmed(false);
            }
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
                case NetMsg.LobbyUpdate:     Handle_LobbyUpdate(reader); break;
                case NetMsg.PlayerNames:     Handle_PlayerNames(reader); break;
                case NetMsg.TimerSync:       Handle_TimerSync(reader); break;
                case NetMsg.TimerStop:       Handle_TimerStop(reader); break;
                case NetMsg.ChatMessage:     Handle_ChatMessage(reader); break;
                case NetMsg.RemoveDiscs:     Handle_RemoveDiscs(reader); break;

                // Client → Host messages
                case NetMsg.ChooseAction:     Handle_ChooseAction(senderId, reader); break;
                case NetMsg.SubmitQuestion:   Handle_SubmitQuestion(senderId, reader); break;
                case NetMsg.SubmitSearch:     Handle_SubmitSearch(senderId, reader); break;
                case NetMsg.SubmitPenalty:     Handle_SubmitPenalty(senderId, reader); break;
                case NetMsg.RequestStart:     Handle_RequestStart(senderId); break;
                case NetMsg.SetNickname:     Handle_SetNickname(senderId, reader); break;
                case NetMsg.ToggleReady:     Handle_ToggleReady(senderId, reader); break;
                case NetMsg.SendChat:        Handle_SendChat(senderId, reader); break;

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

            // Clear dimming before applying new state
            ClearTileDimming();

            // Apply penalty dimming on client side
            if (_currentPhase == TurnPhase.PenaltyPlacement)
                ApplyClientPenaltyDimming(playerIndex);

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

        private void Handle_RemoveDiscs(FastBufferReader reader)
        {
            var tile = ReadCoords(reader);
            _tokenPlacer?.RemoveDiscsAt(tile);
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

            // Client-side: if returning to lobby, clean up game state
            if (_gamePhase == GamePhase.Lobby && !_isHost)
            {
                CleanupClientGameState();
                var connectionMgr = FindFirstObjectByType<ConnectionManager>();
                connectionMgr?.ShowLobbyAfterGame();
            }
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

            // If game is over and client requests restart, return to lobby
            if (_gamePhase == GamePhase.GameOver)
            {
                ReturnToLobby();
                return;
            }

            Debug.Log($"[NetworkGameManager] Client {senderId} requested game start.");
        }

        private void Handle_SetNickname(ulong senderId, FastBufferReader reader)
        {
            if (!_isHost) return;
            string nickname = ReadString(reader);
            if (!_clientPlayerMap.ContainsKey(senderId)) return;
            if (_playerReady.TryGetValue(senderId, out var ready) && ready) return;
            _playerNicknames[senderId] = nickname;
            BroadcastLobbyUpdate();
        }

        private void Handle_ToggleReady(ulong senderId, FastBufferReader reader)
        {
            if (!_isHost) return;
            reader.ReadValueSafe(out bool readyState);
            if (!_clientPlayerMap.ContainsKey(senderId)) return;
            _playerReady[senderId] = readyState;
            BroadcastLobbyUpdate();
        }

        private void Handle_LobbyUpdate(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int count);
            var infos = new LobbyPlayerInfo[count];
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int playerIndex);
                string nickname = ReadString(reader);
                reader.ReadValueSafe(out bool isReady);
                infos[i] = new LobbyPlayerInfo
                {
                    PlayerIndex = playerIndex,
                    Nickname = nickname,
                    IsReady = isReady
                };
            }
            OnLobbyUpdated?.Invoke(infos);
        }

        private void Handle_PlayerNames(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int count);
            _playerNames = new string[count];
            for (int i = 0; i < count; i++)
                _playerNames[i] = ReadString(reader);
            OnPlayerNamesReceived?.Invoke(_playerNames);
        }

        private void Handle_TimerSync(FastBufferReader reader)
        {
            reader.ReadValueSafe(out float duration);
            // Client-side: start a local timer for display
            if (_turnTimer == null)
            {
                _turnTimer = gameObject.AddComponent<TurnTimer>();
                _turnTimer.OnTimerTick += tick => _uiManager?.TurnIndicator?.UpdateTimer(tick);
                _turnTimer.OnTimerExpired += () => _uiManager?.TurnIndicator?.HideTimer();
            }
            if (duration >= TurnTimer.TurnDuration - 1f)
                _turnTimer.StartTurnTimer();
            else
                _turnTimer.StartPenaltyTimer();
        }

        private void Handle_TimerStop(FastBufferReader reader)
        {
            _turnTimer?.Stop();
            _uiManager?.TurnIndicator?.HideTimer();
        }

        private void Handle_ChatMessage(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int playerIndex);
            string message = ReadString(reader);
            string senderName = ReadString(reader);

            if (playerIndex < 0)
            {
                // System message (connect/disconnect/etc.)
                _uiManager?.LogPanel?.AddSystemMessage(message);
            }
            else
            {
                // Player chat message
                _uiManager?.LogPanel?.AddEntry(playerIndex,
                    $"[{senderName}]: {message}");
            }
        }

        // =============================================================
        // Host: Chat Handler
        // =============================================================

        private void Handle_SendChat(ulong senderId, FastBufferReader reader)
        {
            if (!_isHost) return;
            string message = ReadString(reader);

            if (!_clientPlayerMap.TryGetValue(senderId, out int playerIndex)) return;

            string senderName = _playerNicknames.TryGetValue(senderId, out var n)
                ? n : $"Player {playerIndex + 1}";

            // Show on host's log
            _uiManager?.LogPanel?.AddEntry(playerIndex,
                $"[{senderName}]: {message}");

            // Broadcast to all other clients
            SendToAllClients(NetMsg.ChatMessage, w =>
            {
                w.WriteValueSafe(playerIndex);
                WriteString(w, message);
                WriteString(w, senderName);
            });
        }

        // =============================================================
        // Public Chat API
        // =============================================================

        /// <summary>
        /// Sends a chat message from the local player.
        /// Host broadcasts immediately; clients send to host for relay.
        /// </summary>
        public void SendChatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (_isHost)
            {
                string senderName = _playerNicknames.TryGetValue(
                    NetworkManager.Singleton.LocalClientId, out var n)
                    ? n : $"Player {_localPlayerIndex + 1}";

                // Show on host's local log
                _uiManager?.LogPanel?.AddEntry(_localPlayerIndex,
                    $"[{senderName}]: {message}");

                // Broadcast to all clients
                SendToAllClients(NetMsg.ChatMessage, w =>
                {
                    w.WriteValueSafe(_localPlayerIndex);
                    WriteString(w, message);
                    WriteString(w, senderName);
                });
            }
            else
            {
                SendToHost(NetMsg.SendChat, w => WriteString(w, message));
            }
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
