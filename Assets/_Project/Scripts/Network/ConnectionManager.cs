using System;
using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.UI;
using Steamworks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.Network
{
    /// <summary>
    /// Pre-game flow: mode selection (Local / Host / Join), lobby with
    /// nicknames &amp; ready states, and NetworkManager lifecycle.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private GameBootstrapper _bootstrapper;

        // ---------------------------------------------------------
        // Runtime
        // ---------------------------------------------------------

        private NetworkGameManager _networkGameManager;
        private Canvas _canvas;
        private bool _isHost;
        private bool _myReady;

        // UI Panels
        private GameObject _modePanel;
        private GameObject _joinPanel;
        private GameObject _lobbyPanel;
        private SettingsPanel _settingsPanel;

        // Join panel elements
        private TextMeshProUGUI _joinStatusText;
        private TMP_InputField _ipInput;

        // Lobby elements
        private TextMeshProUGUI _lobbyTitle;
        private TextMeshProUGUI _lobbyIpText;
        private TextMeshProUGUI _lobbyCountText;
        private TMP_InputField _nicknameInput;
        private Button _readyButton;
        private TextMeshProUGUI _readyButtonLabel;
        private Button _startButton;
        private TextMeshProUGUI _startButtonLabel;

        // Voice controls
        private Button _micToggleBtn;
        private Image _micToggleIcon;
        private Button _speakerToggleBtn;
        private Image _speakerToggleIcon;

        private struct LobbyEntry
        {
            public GameObject Root;
            public Image ColorBar;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI ReadyText;
        }

        private readonly LobbyEntry[] _lobbyEntries = new LobbyEntry[5];

        // Localized label tracking for language refresh
        private readonly List<(TextMeshProUGUI text, string key)> _localizedLabels = new();

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<GameBootstrapper>();

            BuildUI();
        }

        private void OnDestroy()
        {
            L.OnLanguageChanged -= OnLanguageChanged;

            if (_networkGameManager != null)
                _networkGameManager.OnLobbyUpdated -= UpdateLobbyDisplay;

            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        // ---------------------------------------------------------
        // UI Construction
        // ---------------------------------------------------------

        private void BuildUI()
        {
            // Initialize settings (loads language, volume, etc.)
            SettingsManager.Initialize();

            _canvas = UIFactory.CreateScreenCanvas("ConnectionUI_Canvas", 100);
            _canvas.transform.SetParent(transform);

            BuildModePanel();
            BuildJoinPanel();
            BuildLobbyPanel();
            BuildSettingsPanel();
            BuildSettingsButton();

            _joinPanel.SetActive(false);
            _lobbyPanel.SetActive(false);

            L.OnLanguageChanged -= OnLanguageChanged;
            L.OnLanguageChanged += OnLanguageChanged;
        }

        private void BuildModePanel()
        {
            _modePanel = CreateCenteredPanel("ModePanel", 400, 350);
            var root = _modePanel.GetComponent<RectTransform>();
            UIFactory.AddVerticalLayout(root, spacing: 15,
                padding: new RectOffset(30, 30, 30, 30));

            var title = UIFactory.CreateTMP(root, "Title", L.Get("title_cryptid"),
                fontSize: 42, color: UIFactory.Accent);
            Loc(title, "title_cryptid");
            title.GetComponent<RectTransform>().sizeDelta = new Vector2(340, 60);

            var subtitle = UIFactory.CreateTMP(root, "Subtitle",
                L.Get("choose_game_mode"), fontSize: 20);
            Loc(subtitle, "choose_game_mode");
            subtitle.GetComponent<RectTransform>().sizeDelta = new Vector2(340, 30);

            var localBtn = UIFactory.CreateButton(root, "LocalBtn",
                L.Get("local_game"), 340, 55, new Color(0.18f, 0.80f, 0.44f));
            localBtn.onClick.AddListener(StartLocalGame);
            Loc(localBtn.GetComponentInChildren<TextMeshProUGUI>(), "local_game");

            var hostBtn = UIFactory.CreateButton(root, "HostBtn",
                L.Get("host_game"), 340, 55, new Color(0.20f, 0.60f, 0.86f));
            hostBtn.onClick.AddListener(ShowHostLobby);
            Loc(hostBtn.GetComponentInChildren<TextMeshProUGUI>(), "host_game");

            var joinBtn = UIFactory.CreateButton(root, "JoinBtn",
                L.Get("join_game"), 340, 55, new Color(0.95f, 0.61f, 0.07f));
            joinBtn.onClick.AddListener(ShowJoinPanel);
            Loc(joinBtn.GetComponentInChildren<TextMeshProUGUI>(), "join_game");
        }

        private void BuildJoinPanel()
        {
            _joinPanel = CreateCenteredPanel("JoinPanel", 400, 280);
            var root = _joinPanel.GetComponent<RectTransform>();
            UIFactory.AddVerticalLayout(root, spacing: 12,
                padding: new RectOffset(25, 25, 25, 25));

            var joinTitle = UIFactory.CreateTMP(root, "Title",
                L.Get("join_game"), fontSize: 28, color: UIFactory.Accent);
            Loc(joinTitle, "join_game");
            joinTitle.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 40);

            // Steam ID Input
            _ipInput = CreateInputField(root, "SteamIdInput", "", 340, 45);
            _ipInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            _ipInput.characterLimit = 20;
            // Update placeholder text
            if (_ipInput.placeholder is TextMeshProUGUI ph)
                ph.text = L.Get("enter_steam_id");

            var connectBtn = UIFactory.CreateButton(root, "ConnectBtn",
                L.Get("connect"), 200, 50, new Color(0.20f, 0.60f, 0.86f));
            connectBtn.onClick.AddListener(JoinGame);
            Loc(connectBtn.GetComponentInChildren<TextMeshProUGUI>(), "connect");

            _joinStatusText = UIFactory.CreateTMP(root, "Status",
                "", fontSize: 16);
            _joinStatusText.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 30);

            var cancelBtn = UIFactory.CreateButton(root, "CancelBtn",
                L.Get("cancel"), 200, 40, new Color(0.6f, 0.2f, 0.2f), fontSize: 18);
            cancelBtn.onClick.AddListener(CancelNetworking);
        }

        private void BuildLobbyPanel()
        {
            _lobbyPanel = CreateCenteredPanel("LobbyPanel", 460, 560);
            var root = _lobbyPanel.GetComponent<RectTransform>();
            UIFactory.AddVerticalLayout(root, spacing: 8,
                padding: new RectOffset(20, 20, 20, 20));

            // Title
            _lobbyTitle = UIFactory.CreateTMP(root, "Title", L.Get("lobby"),
                fontSize: 32, color: UIFactory.Accent);
            _lobbyTitle.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 45);

            // IP text row (host only — Steam ID + copy button)
            var ipRow = UIFactory.CreatePanel(root, "IpRow");
            ipRow.sizeDelta = new Vector2(420, 28);
            var ipLayout = UIFactory.AddHorizontalLayout(ipRow, spacing: 6,
                padding: new RectOffset(0, 0, 0, 0),
                childAlignment: TextAnchor.MiddleLeft);
            ipLayout.childControlWidth = false;
            ipLayout.childForceExpandWidth = false;

            _lobbyIpText = UIFactory.CreateTMP(ipRow, "IpText", "",
                fontSize: 16, color: new Color(0.85f, 0.85f, 0.55f));
            _lobbyIpText.GetComponent<RectTransform>().sizeDelta = new Vector2(330, 26);

            var copyBtn = UIFactory.CreateButton(ipRow, "CopyBtn",
                L.Get("copy"), 80, 26, new Color(0.25f, 0.45f, 0.65f), fontSize: 14);
            copyBtn.onClick.AddListener(CopySteamIdToClipboard);

            // Player count
            _lobbyCountText = UIFactory.CreateTMP(root, "CountText",
                L.Format("players_count", 0), fontSize: 18);
            _lobbyCountText.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 26);

            // Player list container
            var listContainer = UIFactory.CreatePanel(root, "PlayerList",
                new Color(0.06f, 0.06f, 0.09f, 0.8f));
            listContainer.sizeDelta = new Vector2(420, 185);
            var listVL = UIFactory.AddVerticalLayout(listContainer, spacing: 2,
                padding: new RectOffset(5, 5, 5, 5),
                childAlignment: TextAnchor.UpperCenter);
            listVL.childControlWidth = true;
            listVL.childForceExpandWidth = true;

            for (int i = 0; i < 5; i++)
            {
                _lobbyEntries[i] = CreateLobbyEntry(listContainer, i);
                _lobbyEntries[i].Root.SetActive(false);
            }

            // Nickname label + input
            var nickLabel = UIFactory.CreateTMP(root, "NickLabel",
                L.Get("your_nickname"), fontSize: 16,
                align: TextAlignmentOptions.MidlineLeft);
            Loc(nickLabel, "your_nickname");
            nickLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 22);

            _nicknameInput = CreateInputField(root, "NicknameInput", "", 420, 40, 16);
            _nicknameInput.characterLimit = 16;
            _nicknameInput.onEndEdit.AddListener(OnNicknameChanged);

            // Voice controls row
            var voiceRow = UIFactory.CreatePanel(root, "VoiceRow");
            voiceRow.sizeDelta = new Vector2(420, 40);
            var voiceLayout = UIFactory.AddHorizontalLayout(voiceRow, spacing: 10,
                padding: new RectOffset(5, 5, 2, 2),
                childAlignment: TextAnchor.MiddleCenter);
            voiceLayout.childControlWidth = false;
            voiceLayout.childForceExpandWidth = false;

            // Mic toggle (icon button)
            _micToggleBtn = UIFactory.CreateImageButton(voiceRow, "MicBtn",
                IconProvider.MicOn, 40, 36, new Color(0.20f, 0.55f, 0.35f));
            _micToggleBtn.onClick.AddListener(OnMicToggle);
            _micToggleIcon = _micToggleBtn.transform.Find("Icon")?.GetComponent<Image>();

            // Speaker toggle (icon button)
            _speakerToggleBtn = UIFactory.CreateImageButton(voiceRow, "SpeakerBtn",
                IconProvider.SpeakerOn, 40, 36, new Color(0.20f, 0.55f, 0.35f));
            _speakerToggleBtn.onClick.AddListener(OnSpeakerToggle);
            _speakerToggleIcon = _speakerToggleBtn.transform.Find("Icon")?.GetComponent<Image>();

            // Ready button
            _readyButton = UIFactory.CreateButton(root, "ReadyBtn",
                L.Get("not_ready"), 300, 45, new Color(0.6f, 0.2f, 0.2f));
            _readyButton.onClick.AddListener(ToggleReady);
            _readyButtonLabel = _readyButton.GetComponentInChildren<TextMeshProUGUI>();

            // Start game button (host only)
            _startButton = UIFactory.CreateButton(root, "StartBtn",
                L.Get("start_game"), 300, 45, new Color(0.18f, 0.80f, 0.44f));
            _startButton.onClick.AddListener(HostStartGame);
            _startButton.interactable = false;
            _startButtonLabel = _startButton.GetComponentInChildren<TextMeshProUGUI>();
            Loc(_startButtonLabel, "start_game");

            // Cancel button
            var cancelBtn = UIFactory.CreateButton(root, "CancelBtn",
                L.Get("cancel"), 200, 35, new Color(0.6f, 0.2f, 0.2f), fontSize: 16);
            cancelBtn.onClick.AddListener(CancelNetworking);
        }

        private void BuildSettingsPanel()
        {
            var settingsRoot = UIFactory.CreatePanel(_canvas.transform,
                "SettingsPanel", new Color(0.04f, 0.04f, 0.06f, 0.95f));
            _settingsPanel = settingsRoot.gameObject.AddComponent<SettingsPanel>();
            _settingsPanel.Build(settingsRoot);
        }

        /// <summary>Creates a settings icon button anchored to the top-right of the canvas.</summary>
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

        /// <summary>Copies the host's Steam ID to the system clipboard.</summary>
        private void CopySteamIdToClipboard()
        {
            if (SteamManager.Initialized)
            {
                string steamId = SteamManager.MySteamId.ToString();
                GUIUtility.systemCopyBuffer = steamId;
                Debug.Log($"[ConnectionManager] Copied Steam ID to clipboard: {steamId}");
            }
        }

        private void ShowSettings()
        {
            _settingsPanel?.Show(inGame: false);
        }

        private void ToggleSettings()
        {
            if (_settingsPanel != null && _settingsPanel.gameObject.activeSelf)
                _settingsPanel.Hide();
            else
                _settingsPanel?.Show(inGame: false);
        }

        /// <summary>Registers a TMP label with its localization key for automatic refresh.</summary>
        private TextMeshProUGUI Loc(TextMeshProUGUI tmp, string key)
        {
            _localizedLabels.Add((tmp, key));
            return tmp;
        }

        private void OnLanguageChanged(L.Language _)
        {
            // Refresh all tracked simple labels
            foreach (var (text, key) in _localizedLabels)
            {
                if (text != null) text.text = L.Get(key);
            }

            // Refresh contextual labels
            if (_lobbyTitle != null)
                _lobbyTitle.text = _isHost ? L.Get("lobby_host") : L.Get("lobby");

            UpdateReadyButtonVisual();
        }

        private LobbyEntry CreateLobbyEntry(RectTransform parent, int index)
        {
            var entry = new LobbyEntry();

            var go = new GameObject($"PlayerEntry_{index}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 33);
            go.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 0.9f);
            UIFactory.AddHorizontalLayout(rt, spacing: 8,
                padding: new RectOffset(8, 8, 2, 2));

            // Color bar
            var barGo = new GameObject("ColorBar", typeof(RectTransform));
            barGo.transform.SetParent(rt, false);
            barGo.GetComponent<RectTransform>().sizeDelta = new Vector2(6, 28);
            entry.ColorBar = barGo.AddComponent<Image>();
            entry.ColorBar.color = UIFactory.GetPlayerColor(index);

            // Name
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(rt, false);
            nameGo.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 28);
            entry.NameText = nameGo.AddComponent<TextMeshProUGUI>();
            if (UIFactory.KoreanFont != null) entry.NameText.font = UIFactory.KoreanFont;
            entry.NameText.text = L.Format("player_default", index + 1);
            entry.NameText.fontSize = 17;
            entry.NameText.alignment = TextAlignmentOptions.MidlineLeft;

            // Ready indicator
            var readyGo = new GameObject("Ready", typeof(RectTransform));
            readyGo.transform.SetParent(rt, false);
            readyGo.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 28);
            entry.ReadyText = readyGo.AddComponent<TextMeshProUGUI>();
            if (UIFactory.KoreanFont != null) entry.ReadyText.font = UIFactory.KoreanFont;
            entry.ReadyText.text = "\u2717";
            entry.ReadyText.fontSize = 20;
            entry.ReadyText.alignment = TextAlignmentOptions.Center;
            entry.ReadyText.color = new Color(0.6f, 0.6f, 0.6f);

            entry.Root = go;
            return entry;
        }

        /// <summary>Creates a TMP_InputField with proper text area / viewport setup.</summary>
        private static TMP_InputField CreateInputField(
            Transform parent, string name, string defaultText,
            float w, float h, float fontSize = 20)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);

            var input = go.AddComponent<TMP_InputField>();
            input.text = defaultText;

            var textArea = new GameObject("TextArea", typeof(RectTransform));
            textArea.transform.SetParent(go.transform, false);
            var taRT = textArea.GetComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero;
            taRT.anchorMax = Vector2.one;
            taRT.sizeDelta = Vector2.zero;
            taRT.offsetMin = new Vector2(10, 0);
            taRT.offsetMax = new Vector2(-10, 0);

            var txt = UIFactory.CreateTMP(textArea.transform, "Text",
                "", fontSize, align: TextAlignmentOptions.MidlineLeft);
            txt.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            input.textComponent = txt;
            input.textViewport = taRT;

            var placeholder = UIFactory.CreateTMP(textArea.transform, "Placeholder",
                L.Get("enter_text"), fontSize,
                align: TextAlignmentOptions.MidlineLeft,
                color: new Color(0.5f, 0.5f, 0.5f));
            placeholder.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            input.placeholder = placeholder;

            return input;
        }

        private GameObject CreateCenteredPanel(string name, float width, float height)
        {
            var panel = UIFactory.CreatePanel(_canvas.transform, name, UIFactory.PanelBg);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(width, height);
            panel.anchoredPosition = Vector2.zero;
            return panel.gameObject;
        }

        // ---------------------------------------------------------
        // Mode Selection
        // ---------------------------------------------------------

        private void StartLocalGame()
        {
            HideAllPanels();
            if (_bootstrapper != null)
                _bootstrapper.StartLocalGame();
            Debug.Log("[ConnectionManager] Starting local game.");
        }

        private async void ShowHostLobby()
        {
            _isHost = true;
            _myReady = false;
            _modePanel.SetActive(false);
            _joinPanel.SetActive(false);

            SetupNetworking();
            StartHost();

            UIAnimator.ShowPanel(_lobbyPanel);
            _lobbyTitle.text = L.Get("lobby_host");
            _startButton.gameObject.SetActive(true);
            // Show Steam ID row (parent of _lobbyIpText)
            _lobbyIpText.transform.parent.gameObject.SetActive(true);
            _lobbyIpText.text = SteamManager.Initialized
                ? $"Steam ID: {SteamManager.MySteamId}" : "Steam ID: (unavailable)";
            _nicknameInput.text = SteamManager.Initialized
                ? SteamManager.PlayerName : L.Format("player_default", 1);
            UpdateReadyButtonVisual();

            // Initialize Vivox and join voice channel
            await InitVivoxAndJoinChannel();
        }

        private void ShowJoinPanel()
        {
            _modePanel.SetActive(false);
            _lobbyPanel.SetActive(false);
            UIAnimator.ShowPanel(_joinPanel);
            _joinStatusText.text = "";
        }

        private void HideAllPanels()
        {
            _modePanel.SetActive(false);
            _lobbyPanel.SetActive(false);
            _joinPanel.SetActive(false);
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Networking Setup
        // ---------------------------------------------------------

        private void SetupNetworking()
        {
            if (_bootstrapper != null)
                _bootstrapper.DisableForNetworkMode();

            EnsureSteam();
            EnsureNetworkManager();

            if (_networkGameManager == null)
            {
                var go = NetworkManager.Singleton.gameObject;
                _networkGameManager = go.AddComponent<NetworkGameManager>();
            }
        }

        /// <summary>Ensures SteamManager singleton exists and is initialized.</summary>
        private void EnsureSteam()
        {
            if (SteamManager.Instance != null) return;
            var steamGo = new GameObject("SteamManager");
            steamGo.AddComponent<SteamManager>();
        }

        private void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;

            var go = new GameObject("NetworkManager");
            DontDestroyOnLoad(go);

            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<FacepunchTransport>();

            if (nm.NetworkConfig == null)
                nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;

            Debug.Log("[ConnectionManager] Created NetworkManager with FacepunchTransport.");
        }

        // ---------------------------------------------------------
        // Host Flow
        // ---------------------------------------------------------

        private void StartHost()
        {
            NetworkManager.Singleton.StartHost();
            _networkGameManager.Initialize(isHost: true);
            _networkGameManager.OnLobbyUpdated += UpdateLobbyDisplay;

            Debug.Log($"[ConnectionManager] Hosting via Steam Relay " +
                      $"(SteamId: {SteamClient.SteamId})");
        }

        private void HostStartGame()
        {
            if (_networkGameManager == null || !_networkGameManager.CanStartGame()) return;

            HideAllPanels();
            _networkGameManager.OnLobbyUpdated -= UpdateLobbyDisplay;
            _networkGameManager.StartGame();
        }

        // ---------------------------------------------------------
        // Join Flow
        // ---------------------------------------------------------

        private void JoinGame()
        {
            string steamIdStr = _ipInput != null ? _ipInput.text.Trim() : "";
            if (!ulong.TryParse(steamIdStr, out ulong hostId) || hostId == 0)
            {
                _joinStatusText.text = L.Get("invalid_steam_id");
                return;
            }

            SetupNetworking();

            var transport = NetworkManager.Singleton.GetComponent<FacepunchTransport>();
            if (transport != null)
                transport.TargetSteamId = new SteamId { Value = hostId };

            _joinStatusText.text = L.Format("connecting_steam", hostId);

            NetworkManager.Singleton.OnClientConnectedCallback += OnJoinConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnJoinDisconnected;

            NetworkManager.Singleton.StartClient();
            _networkGameManager.Initialize(isHost: false);
        }

        private async void OnJoinConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            Debug.Log("[ConnectionManager] Connected to host.");

            _isHost = false;
            _myReady = false;
            _joinPanel.SetActive(false);
            UIAnimator.ShowPanel(_lobbyPanel);
            _startButton.gameObject.SetActive(false);
            _lobbyIpText.transform.parent.gameObject.SetActive(false);
            _lobbyTitle.text = L.Get("lobby");

            int idx = _networkGameManager.LocalPlayerIndex;
            _nicknameInput.text = SteamManager.Initialized
                ? SteamManager.PlayerName
                : (idx >= 0 ? L.Format("player_default", idx + 1) : L.Format("player_default", 0));
            UpdateReadyButtonVisual();

            _networkGameManager.OnLobbyUpdated += UpdateLobbyDisplay;
            _networkGameManager.OnGamePhaseChanged += phase =>
            {
                if (phase == GamePhase.Playing)
                    HideAllPanels();
            };

            // Initialize Vivox and join voice channel
            await InitVivoxAndJoinChannel();
        }

        private void OnJoinDisconnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;
            _joinStatusText.text = L.Get("disconnected");
            Debug.LogWarning("[ConnectionManager] Disconnected from host.");
        }

        // ---------------------------------------------------------
        // Lobby Interaction
        // ---------------------------------------------------------

        private void OnNicknameChanged(string nickname)
        {
            if (_myReady) return;
            if (string.IsNullOrWhiteSpace(nickname)) return;
            _networkGameManager?.SetLocalNickname(nickname.Trim());
        }

        // ---------------------------------------------------------
        // Voice Controls
        // ---------------------------------------------------------

        /// <summary>Initializes Vivox, logs in, and joins the lobby voice channel.</summary>
        private async System.Threading.Tasks.Task InitVivoxAndJoinChannel()
        {
            EnsureVivoxManager();

            var vivox = VivoxManager.Instance;
            if (vivox == null) return;

            await vivox.InitializeAsync();
            if (!vivox.IsReady)
            {
                Debug.LogWarning("[ConnectionManager] Vivox not available — voice chat disabled.");
                return;
            }

            string displayName = _nicknameInput != null && !string.IsNullOrWhiteSpace(_nicknameInput.text)
                ? _nicknameInput.text
                : (SteamManager.Initialized ? SteamManager.PlayerName : "Player");
            await vivox.LoginAsync(displayName);

            // If login failed, don't attempt to join channel
            if (!vivox.IsReady)
            {
                Debug.LogWarning("[ConnectionManager] Vivox login failed — skipping channel join.");
                return;
            }

            await vivox.JoinChannelAsync("cryptid_lobby");

            // Subscribe to mute state changes for UI update
            vivox.OnMuteStateChanged -= UpdateVoiceToggleVisuals;
            vivox.OnMuteStateChanged += UpdateVoiceToggleVisuals;

            UpdateVoiceToggleVisuals(vivox.IsInputMuted, vivox.IsOutputMuted);
        }

        /// <summary>Ensures VivoxManager singleton exists.</summary>
        private void EnsureVivoxManager()
        {
            if (VivoxManager.Instance != null) return;
            var go = new GameObject("VivoxManager");
            go.AddComponent<VivoxManager>();
        }

        // Local mute state fallback (for visual feedback when Vivox is unavailable)
        private bool _localMicMuted;
        private bool _localSpeakerMuted;

        private void OnMicToggle()
        {
            var vivox = VivoxManager.Instance;
            if (vivox != null && vivox.IsReady)
            {
                vivox.ToggleInputMute();
            }
            else
            {
                // Vivox not ready — toggle local state for visual feedback
                _localMicMuted = !_localMicMuted;
                UpdateVoiceToggleVisuals(_localMicMuted, _localSpeakerMuted);
            }
        }

        private void OnSpeakerToggle()
        {
            var vivox = VivoxManager.Instance;
            if (vivox != null && vivox.IsReady)
            {
                vivox.ToggleOutputMute();
            }
            else
            {
                // Vivox not ready — toggle local state for visual feedback
                _localSpeakerMuted = !_localSpeakerMuted;
                UpdateVoiceToggleVisuals(_localMicMuted, _localSpeakerMuted);
            }
        }

        private void UpdateVoiceToggleVisuals(bool inputMuted, bool outputMuted)
        {
            if (_micToggleBtn != null)
            {
                // Swap icon sprite
                if (_micToggleIcon != null)
                    _micToggleIcon.sprite = inputMuted ? IconProvider.MicOff : IconProvider.MicOn;

                var baseColor = inputMuted
                    ? new Color(0.6f, 0.2f, 0.2f)
                    : new Color(0.20f, 0.55f, 0.35f);
                var cb = _micToggleBtn.colors;
                cb.normalColor      = baseColor;
                cb.highlightedColor = baseColor * 1.15f;
                cb.pressedColor     = baseColor * 0.85f;
                cb.selectedColor    = baseColor;
                _micToggleBtn.colors = cb;
            }

            if (_speakerToggleBtn != null)
            {
                // Swap icon sprite
                if (_speakerToggleIcon != null)
                    _speakerToggleIcon.sprite = outputMuted ? IconProvider.SpeakerOff : IconProvider.SpeakerOn;

                var baseColor = outputMuted
                    ? new Color(0.6f, 0.2f, 0.2f)
                    : new Color(0.20f, 0.55f, 0.35f);
                var cb = _speakerToggleBtn.colors;
                cb.normalColor      = baseColor;
                cb.highlightedColor = baseColor * 1.15f;
                cb.pressedColor     = baseColor * 0.85f;
                cb.selectedColor    = baseColor;
                _speakerToggleBtn.colors = cb;
            }
        }

        private void ToggleReady()
        {
            _myReady = !_myReady;
            _networkGameManager?.SetLocalReady(_myReady);
            _nicknameInput.interactable = !_myReady;
            UpdateReadyButtonVisual();
        }

        private void UpdateReadyButtonVisual()
        {
            if (_readyButtonLabel == null) return;

            if (_myReady)
            {
                _readyButtonLabel.text = L.Get("ready");
                var cb = _readyButton.colors;
                cb.normalColor = new Color(0.18f, 0.80f, 0.44f);
                cb.selectedColor = new Color(0.18f, 0.80f, 0.44f);
                _readyButton.colors = cb;
            }
            else
            {
                _readyButtonLabel.text = L.Get("not_ready");
                var cb = _readyButton.colors;
                cb.normalColor = new Color(0.6f, 0.2f, 0.2f);
                cb.selectedColor = new Color(0.6f, 0.2f, 0.2f);
                _readyButton.colors = cb;
            }
        }

        private void UpdateLobbyDisplay(NetworkGameManager.LobbyPlayerInfo[] players)
        {
            _lobbyCountText.text = L.Format("players_count", players.Length);

            for (int i = 0; i < 5; i++)
            {
                if (i < players.Length)
                {
                    _lobbyEntries[i].Root.SetActive(true);
                    _lobbyEntries[i].ColorBar.color =
                        UIFactory.GetPlayerColor(players[i].PlayerIndex);

                    string suffix = (players[i].PlayerIndex == 0) ? L.Get("host_suffix") : "";
                    _lobbyEntries[i].NameText.text = $"{players[i].Nickname}{suffix}";

                    _lobbyEntries[i].ReadyText.text = players[i].IsReady ? "\u2713" : "\u2717";
                    _lobbyEntries[i].ReadyText.color = players[i].IsReady
                        ? new Color(0.18f, 0.80f, 0.44f)
                        : new Color(0.6f, 0.6f, 0.6f);
                }
                else
                {
                    _lobbyEntries[i].Root.SetActive(false);
                }
            }

            if (_isHost && _startButton != null)
            {
                bool canStart = _networkGameManager.CanStartGame();
                _startButton.interactable = canStart;
                _startButtonLabel.text = players.Length >= 3
                    ? L.Get("start_game") : L.Format("start_game_need", players.Length);
            }
        }

        // ---------------------------------------------------------
        // Cancel / Cleanup
        // ---------------------------------------------------------

        /// <summary>
        /// Shows the lobby panel after a network game ends (return to lobby).
        /// Called by NetworkGameManager when the host clicks "Play Again".
        /// Preserves the network connection.
        /// </summary>
        public void ShowLobbyAfterGame()
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(true);

            _modePanel.SetActive(false);
            _joinPanel.SetActive(false);
            UIAnimator.ShowPanel(_lobbyPanel);

            _myReady = false;
            _nicknameInput.interactable = true;
            UpdateReadyButtonVisual();

            if (_isHost)
            {
                _lobbyTitle.text = L.Get("lobby_host");
                _startButton.gameObject.SetActive(true);
                _startButton.interactable = false;
                _lobbyIpText.transform.parent.gameObject.SetActive(true);
            }
            else
            {
                _lobbyTitle.text = L.Get("lobby");
                _startButton.gameObject.SetActive(false);
                _lobbyIpText.transform.parent.gameObject.SetActive(false);
            }

            // Re-subscribe to lobby updates
            if (_networkGameManager != null)
            {
                _networkGameManager.OnLobbyUpdated -= UpdateLobbyDisplay;
                _networkGameManager.OnLobbyUpdated += UpdateLobbyDisplay;
            }
        }

        private void CancelNetworking()
        {
            if (_networkGameManager != null)
                _networkGameManager.OnLobbyUpdated -= UpdateLobbyDisplay;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            if (_networkGameManager != null)
            {
                Destroy(_networkGameManager);
                _networkGameManager = null;
            }

            if (_bootstrapper != null)
                _bootstrapper.enabled = true;

            _isHost = false;
            _myReady = false;

            _modePanel.SetActive(true);
            _lobbyPanel.SetActive(false);
            _joinPanel.SetActive(false);

            if (_canvas != null)
                _canvas.gameObject.SetActive(true);
        }

    }
}
