using System;
using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Network;
using Cryptid.Systems.Map;
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
        private GameLogPanel _gameLogPanel;
        private TileInfoPanel _tileInfoPanel;
        private PlayerHUDPanel _playerHUD;
        private SettingsPanel _settingsPanel;

        // ---------------------------------------------------------
        // Bound game state (set via Bind methods)
        // ---------------------------------------------------------

        private GameStateMachine _fsm;
        private ITurnState _turnState;
        private TurnManager _turnManager; // null in network mode
        private NetworkGameManager _netManager; // null in local mode
        private PuzzleSetup _puzzle;
        private string _networkClue; // local player's clue in network mode
        private int _playerCount;
        private HexTile _selectedTile; // currently selected tile for info panel

        /// <summary>Index of the human player. Action buttons only show for this player.</summary>
        // All players are human-controlled in local mode (HumanPlayerIndex = -1).
        // In network mode, set to the local player's index.
        public int HumanPlayerIndex { get; set; } = -1;

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

        /// <summary>The game log panel, used by external systems to add entries.</summary>
        public GameLogPanel LogPanel => _gameLogPanel;

        /// <summary>The turn indicator panel, for timer display access.</summary>
        public TurnIndicatorPanel TurnIndicator => _turnIndicator;

        /// <summary>The tile info panel, for external access.</summary>
        public TileInfoPanel TileInfo => _tileInfoPanel;

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
            _turnState = turnManager;
            _netManager = null;
            _puzzle = puzzle;
            _networkClue = null;
            _playerCount = playerCount;

            _turnManager.OnTurnStarted    += HandleTurnStarted;
            _turnManager.OnPhaseChanged   += HandlePhaseChanged;
            _turnManager.OnQuestionAsked  += HandleQuestionAsked;
            _turnManager.OnResponseGiven  += HandleResponseGiven;
            _turnManager.OnSearchPerformed  += HandleSearchPerformed;
            _turnManager.OnSearchDiscPlaced += HandleSearchDiscPlaced;
            _turnManager.OnSearchVerification += HandleSearchVerification;
            _turnManager.OnGameWon          += HandleGameWon;

            // Bind tile hover/select to info panel
            var tileInteraction = GameService.Get<TileInteractionSystem>();
            if (tileInteraction != null)
            {
                tileInteraction.OnTileHovered += HandleTileHovered;
                tileInteraction.OnTileSelected += HandleTileSelected;
            }

            // Player HUD: default names for local mode
            string[] defaultNames = new string[playerCount];
            for (int i = 0; i < playerCount; i++)
                defaultNames[i] = L.Format("player_default", i + 1);
            _playerHUD.SetupPlayers(defaultNames, HumanPlayerIndex);
            _playerHUD.gameObject.SetActive(true);
            _turnIndicator.SetPlayerNames(defaultNames);
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

            // Game Log Panel (right-side scrolling log)
            var logRoot = UIFactory.CreatePanel(_canvas.transform,
                "GameLogPanel", UIFactory.PanelBg);
            _gameLogPanel = logRoot.gameObject.AddComponent<GameLogPanel>();
            _gameLogPanel.Build(logRoot);

            // Tile Info Panel (bottom-left: shows tile details on hover)
            var tileInfoRoot = UIFactory.CreatePanel(_canvas.transform,
                "TileInfoPanel", UIFactory.PanelBg);
            _tileInfoPanel = tileInfoRoot.gameObject.AddComponent<TileInfoPanel>();
            _tileInfoPanel.Build(tileInfoRoot);

            // Player HUD Panel (top bar, below turn indicator)
            var playerHudRoot = UIFactory.CreatePanel(_canvas.transform,
                "PlayerHUDPanel", UIFactory.PanelBg);
            _playerHUD = playerHudRoot.gameObject.AddComponent<PlayerHUDPanel>();
            _playerHUD.Build(playerHudRoot);

            // Settings Panel (overlay, reusable for both local and network)
            var settingsRoot = UIFactory.CreatePanel(_canvas.transform,
                "SettingsPanel", new Color(0.04f, 0.04f, 0.06f, 0.95f));
            _settingsPanel = settingsRoot.gameObject.AddComponent<SettingsPanel>();
            _settingsPanel.Build(settingsRoot);

            // Settings button (top-right corner, always visible)
            BuildSettingsButton();

            // Initially hide all gameplay panels
            ShowLobbyState();
        }

        // ---------------------------------------------------------
        // Settings Button (top-right)
        // ---------------------------------------------------------

        private void BuildSettingsButton()
        {
            var btn = UIFactory.CreateImageButton(_canvas.transform, "SettingsCornerBtn",
                IconProvider.Settings, 46, 46, new Color(0.25f, 0.25f, 0.35f, 0.85f));
            btn.onClick.AddListener(ToggleSettings);

            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-12f, -12f);
        }

        private void ToggleSettings()
        {
            if (_settingsPanel != null && _settingsPanel.gameObject.activeSelf)
                _settingsPanel.Hide();
            else
                _settingsPanel?.Show(inGame: true);
        }

        // ---------------------------------------------------------
        // FSM State Handlers
        // ---------------------------------------------------------

        /// <summary>Hides all gameplay panels (shown during Lobby / pre-game).</summary>
        private void ShowLobbyState()
        {
            if (_turnIndicator != null) _turnIndicator.gameObject.SetActive(false);
            if (_actionPanel != null)   _actionPanel.gameObject.SetActive(false);
            if (_playerSelect != null)  _playerSelect.Hide();
            if (_cluePanel != null)     _cluePanel.gameObject.SetActive(false);
            if (_gameOverPanel != null) _gameOverPanel.Hide();
            if (_tileInfoPanel != null) _tileInfoPanel.gameObject.SetActive(false);
            if (_playerHUD != null)     _playerHUD.gameObject.SetActive(false);

            // Hide game log panel during lobby / initial screen
            if (_gameLogPanel != null)
            {
                _gameLogPanel.gameObject.SetActive(false);
            }

            // Hide entire canvas during lobby to avoid duplicate settings button
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        /// <summary>Public version of ShowLobbyState for external callers (e.g. network lobby return).</summary>
        public void ShowLobbyStatePublic()
        {
            UnsubscribeTurnEvents();
            UnsubscribeNetworkEvents();
            _turnManager = null;
            _netManager = null;
            _puzzle = null;
            _networkClue = null;
            _gameLogPanel.Clear();
            ShowLobbyState();
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
                    // Re-enable canvas for gameplay
                    if (_canvas != null) _canvas.gameObject.SetActive(true);
                    _turnIndicator.gameObject.SetActive(true);
                    _cluePanel.gameObject.SetActive(true);
                    _gameLogPanel.gameObject.SetActive(true);
                    _gameLogPanel.SetMinimized(false);
                    _tileInfoPanel.gameObject.SetActive(true);
                    _playerHUD.gameObject.SetActive(true);
                    _gameOverPanel.Hide();
                    _gameLogPanel.AddSystemMessage(L.Get("log_game_started"));
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
                _turnState.TurnNumber, playerIndex, _turnState.CurrentPhase);
            _playerHUD.UpdateActiveTurn(playerIndex);

            // In network mode, only show action buttons on local player's turn
            bool isMyTurn = HumanPlayerIndex < 0 || playerIndex == HumanPlayerIndex;
            _actionPanel.UpdateForPhase(
                isMyTurn ? TurnPhase.ChooseAction : TurnPhase.TurnEnd);
            _playerSelect.Hide();

            // Update clue panel
            if (_puzzle != null && playerIndex < _puzzle.PlayerClues.Count)
            {
                // Local mode: show current turn player's clue
                _cluePanel.UpdateClue(playerIndex, _puzzle.PlayerClues[playerIndex].Description);
            }
            else if (_networkClue != null && HumanPlayerIndex >= 0)
            {
                // Network mode: always show local player's own clue
                _cluePanel.UpdateClue(HumanPlayerIndex, _networkClue);
            }

            // Log
            string playerName = GetPlayerName(playerIndex);
            _gameLogPanel.AddEntry(playerIndex,
                L.Format("log_turn_header", _turnState.TurnNumber, playerName));
        }

        private void HandlePhaseChanged(int playerIndex, TurnPhase phase)
        {
            // Update HUD phase text
            _turnIndicator.UpdateDisplay(_turnState.TurnNumber, playerIndex, phase);

            // In network mode, only show action UI on local player's turn
            bool isMyTurn = HumanPlayerIndex < 0 || playerIndex == HumanPlayerIndex;
            _actionPanel.UpdateForPhase(isMyTurn ? phase : TurnPhase.TurnEnd);

            if (phase == TurnPhase.SelectTile && isMyTurn)
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
            _gameLogPanel.AddEntry(asking,
                L.Format("log_asks", L.PlayerShort(asking), L.PlayerShort(target), tile));
        }

        private void HandleResponseGiven(int responding, HexCoordinates tile, bool result)
        {
            string logKey = result ? "log_responds_yes" : "log_responds_no";
            _gameLogPanel.AddEntry(responding,
                L.Format(logKey, L.PlayerShort(responding)));

            if (!result && _turnState != null)
            {
                int asker = _turnState.CurrentPlayerIndex;
                _gameLogPanel.AddEntry(asker,
                    L.Format("log_must_place_cube", L.PlayerShort(asker)));
            }
        }

        private void HandleSearchDiscPlaced(int player, HexCoordinates tile)
        {
            _gameLogPanel.AddEntry(player,
                L.Format("log_search_placing", L.PlayerShort(player), tile));
        }

        private void HandleSearchVerification(int verifier, HexCoordinates tile, bool result)
        {
            string logKey = result ? "log_verify_yes" : "log_verify_no";
            _gameLogPanel.AddEntry(verifier,
                L.Format(logKey, L.PlayerShort(verifier)));
        }

        private void HandleSearchPerformed(int player, HexCoordinates tile, bool correct)
        {
            if (correct)
            {
                _gameLogPanel.AddEntry(player,
                    L.Format("log_search_success", L.PlayerShort(player)));
            }
            else
            {
                _gameLogPanel.AddEntry(player,
                    L.Get("log_search_fail"));
                _gameLogPanel.AddEntry(player,
                    L.Format("log_must_place_cube", L.PlayerShort(player)));
            }
        }

        private void HandleGameWon(int winnerIndex)
        {
            string answerInfo = _puzzle != null
                ? L.Format("cryptid_location", _puzzle.AnswerTile.Coordinates)
                : "";
            _gameOverPanel.Show(winnerIndex, answerInfo);
        }

        // ---------------------------------------------------------
        // Tile Info Handlers
        // ---------------------------------------------------------

        private void HandleTileHovered(HexTile tile)
        {
            if (_tileInfoPanel == null) return;
            // When a tile is selected, always show selected tile info
            if (_selectedTile != null) return;
            _tileInfoPanel.ShowTileInfo(tile);
        }

        private void HandleTileSelected(HexTile tile)
        {
            _selectedTile = tile;
            if (_tileInfoPanel != null)
                _tileInfoPanel.ShowTileInfo(tile);
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

        private void SetupNetworkPlayerHUD(string[] names)
        {
            _playerHUD.SetupPlayers(names, HumanPlayerIndex);
            _turnIndicator.SetPlayerNames(names);
        }

        /// <summary>Returns the display name for a player index.</summary>
        private string GetPlayerName(int playerIndex)
        {
            if (_turnIndicator != null)
            {
                // Use TurnIndicator's player names if available
                var names = _turnIndicator.PlayerNames;
                if (names != null && playerIndex < names.Length)
                    return names[playerIndex];
            }
            return L.Format("player_default", playerIndex + 1);
        }

        // ---------------------------------------------------------
        // Network Gameplay Binding
        // ---------------------------------------------------------

        /// <summary>
        /// Binds the UI to a NetworkGameManager for network mode.
        /// Called by NetworkGameManager after setup is complete.
        /// Subscribes to network events instead of TurnManager events.
        /// </summary>
        public void BindNetworkGameplay(NetworkGameManager netMgr, int playerCount)
        {
            UnsubscribeTurnEvents();
            UnsubscribeNetworkEvents();

            _turnManager = null;
            _netManager = netMgr;
            _turnState = netMgr;
            _puzzle = null;
            _networkClue = netMgr.MyClueDescription;
            _playerCount = playerCount;

            // Subscribe to network events (same signatures as TurnManager)
            netMgr.OnTurnStarted       += HandleTurnStarted;
            netMgr.OnPhaseChanged      += HandlePhaseChanged;
            netMgr.OnQuestionAsked     += HandleQuestionAsked;
            netMgr.OnResponseGiven     += HandleResponseGiven;
            netMgr.OnSearchPerformed   += HandleSearchPerformed;
            netMgr.OnSearchDiscPlaced  += HandleSearchDiscPlaced;
            netMgr.OnSearchVerification += HandleSearchVerification;
            netMgr.OnGameWon           += HandleGameWon;

            // Receive clue updates
            netMgr.OnClueReceived += clue => _networkClue = clue;

            // Bind tile hover/select
            var tileInteraction = GameService.Get<TileInteractionSystem>();
            if (tileInteraction != null)
            {
                tileInteraction.OnTileHovered += HandleTileHovered;
                tileInteraction.OnTileSelected += HandleTileSelected;
            }

            // Show playing state UI
            _turnIndicator.gameObject.SetActive(true);
            _cluePanel.gameObject.SetActive(true);
            _gameLogPanel.gameObject.SetActive(true);
            _tileInfoPanel.gameObject.SetActive(true);
            _playerHUD.gameObject.SetActive(true);
            _gameOverPanel.Hide();
            _gameLogPanel.AddSystemMessage(L.Get("log_net_started"));

            // Player HUD: use network player names
            netMgr.OnPlayerNamesReceived += SetupNetworkPlayerHUD;
            if (netMgr.AllPlayerNames != null)
                SetupNetworkPlayerHUD(netMgr.AllPlayerNames);

            // Show initial clue
            if (_networkClue != null && HumanPlayerIndex >= 0)
                _cluePanel.UpdateClue(HumanPlayerIndex, _networkClue);
        }

        /// <summary>
        /// Shows the game over panel with network-provided info.
        /// Called by NetworkGameManager when the game ends.
        /// </summary>
        public void ShowNetworkGameOver(int winnerIndex, string answerCoords)
        {
            string answerInfo = !string.IsNullOrEmpty(answerCoords)
                ? L.Format("cryptid_location", answerCoords)
                : "";
            _gameOverPanel.Show(winnerIndex, answerInfo);
            _actionPanel.gameObject.SetActive(false);
            _playerSelect.Hide();
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
            _turnManager.OnSearchPerformed  -= HandleSearchPerformed;
            _turnManager.OnSearchDiscPlaced -= HandleSearchDiscPlaced;
            _turnManager.OnSearchVerification -= HandleSearchVerification;
            _turnManager.OnGameWon          -= HandleGameWon;
        }

        private void UnsubscribeNetworkEvents()
        {
            if (_netManager == null) return;

            _netManager.OnTurnStarted       -= HandleTurnStarted;
            _netManager.OnPhaseChanged      -= HandlePhaseChanged;
            _netManager.OnQuestionAsked     -= HandleQuestionAsked;
            _netManager.OnResponseGiven     -= HandleResponseGiven;
            _netManager.OnSearchPerformed   -= HandleSearchPerformed;
            _netManager.OnSearchDiscPlaced  -= HandleSearchDiscPlaced;
            _netManager.OnSearchVerification -= HandleSearchVerification;
            _netManager.OnGameWon           -= HandleGameWon;
            _netManager.OnPlayerNamesReceived -= SetupNetworkPlayerHUD;
        }

        private void UnsubscribeAll()
        {
            if (_fsm != null)
                _fsm.OnStateChanged -= HandleStateChanged;

            UnsubscribeTurnEvents();
            UnsubscribeNetworkEvents();

            // Unsubscribe tile hover/select
            var tileInteraction = GameService.Get<TileInteractionSystem>();
            if (tileInteraction != null)
            {
                tileInteraction.OnTileHovered -= HandleTileHovered;
                tileInteraction.OnTileSelected -= HandleTileSelected;
            }

            if (_actionPanel != null)
                _actionPanel.OnActionClicked -= HandleActionButtonClicked;

            if (_gameOverPanel != null)
                _gameOverPanel.OnRestartClicked -= HandleRestartClicked;
        }
    }
}
