using System;
using Cryptid.Core;
using Cryptid.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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

        [Header("Network Settings")]
        [SerializeField] private ushort _port = 7777;

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

        private struct LobbyEntry
        {
            public GameObject Root;
            public Image ColorBar;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI ReadyText;
        }

        private readonly LobbyEntry[] _lobbyEntries = new LobbyEntry[5];

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
            _canvas = UIFactory.CreateScreenCanvas("ConnectionUI_Canvas", 100);
            _canvas.transform.SetParent(transform);

            BuildModePanel();
            BuildJoinPanel();
            BuildLobbyPanel();

            _joinPanel.SetActive(false);
            _lobbyPanel.SetActive(false);
        }

        private void BuildModePanel()
        {
            _modePanel = CreateCenteredPanel("ModePanel", 400, 350);
            var root = _modePanel.GetComponent<RectTransform>();
            UIFactory.AddVerticalLayout(root, spacing: 15,
                padding: new RectOffset(30, 30, 30, 30));

            var title = UIFactory.CreateTMP(root, "Title", "CRYPTID",
                fontSize: 42, color: UIFactory.Accent);
            title.GetComponent<RectTransform>().sizeDelta = new Vector2(340, 60);

            var subtitle = UIFactory.CreateTMP(root, "Subtitle",
                "Choose Game Mode", fontSize: 20);
            subtitle.GetComponent<RectTransform>().sizeDelta = new Vector2(340, 30);

            var localBtn = UIFactory.CreateButton(root, "LocalBtn",
                "Local Game", 340, 55, new Color(0.18f, 0.80f, 0.44f));
            localBtn.onClick.AddListener(StartLocalGame);

            var hostBtn = UIFactory.CreateButton(root, "HostBtn",
                "Host Game", 340, 55, new Color(0.20f, 0.60f, 0.86f));
            hostBtn.onClick.AddListener(ShowHostLobby);

            var joinBtn = UIFactory.CreateButton(root, "JoinBtn",
                "Join Game", 340, 55, new Color(0.95f, 0.61f, 0.07f));
            joinBtn.onClick.AddListener(ShowJoinPanel);
        }

        private void BuildJoinPanel()
        {
            _joinPanel = CreateCenteredPanel("JoinPanel", 400, 280);
            var root = _joinPanel.GetComponent<RectTransform>();
            UIFactory.AddVerticalLayout(root, spacing: 12,
                padding: new RectOffset(25, 25, 25, 25));

            var title = UIFactory.CreateTMP(root, "Title",
                "Join Game", fontSize: 28, color: UIFactory.Accent);
            title.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 40);

            // IP Input
            _ipInput = CreateInputField(root, "IPInput", "127.0.0.1", 340, 45);

            var connectBtn = UIFactory.CreateButton(root, "ConnectBtn",
                "Connect", 200, 50, new Color(0.20f, 0.60f, 0.86f));
            connectBtn.onClick.AddListener(JoinGame);

            _joinStatusText = UIFactory.CreateTMP(root, "Status",
                "", fontSize: 16);
            _joinStatusText.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 30);

            var cancelBtn = UIFactory.CreateButton(root, "CancelBtn",
                "Cancel", 200, 40, new Color(0.6f, 0.2f, 0.2f), fontSize: 18);
            cancelBtn.onClick.AddListener(CancelNetworking);
        }

        private void BuildLobbyPanel()
        {
            _lobbyPanel = CreateCenteredPanel("LobbyPanel", 460, 560);
            var root = _lobbyPanel.GetComponent<RectTransform>();
            UIFactory.AddVerticalLayout(root, spacing: 8,
                padding: new RectOffset(20, 20, 20, 20));

            // Title
            _lobbyTitle = UIFactory.CreateTMP(root, "Title", "LOBBY",
                fontSize: 32, color: UIFactory.Accent);
            _lobbyTitle.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 45);

            // IP text (host only)
            _lobbyIpText = UIFactory.CreateTMP(root, "IpText", "",
                fontSize: 16, color: new Color(0.7f, 0.7f, 0.7f));
            _lobbyIpText.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 22);

            // Player count
            _lobbyCountText = UIFactory.CreateTMP(root, "CountText",
                "Players: 0 / 5", fontSize: 18);
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
                "Your Nickname:", fontSize: 16,
                align: TextAlignmentOptions.MidlineLeft);
            nickLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 22);

            _nicknameInput = CreateInputField(root, "NicknameInput", "", 420, 40, 16);
            _nicknameInput.characterLimit = 16;
            _nicknameInput.onEndEdit.AddListener(OnNicknameChanged);

            // Ready button
            _readyButton = UIFactory.CreateButton(root, "ReadyBtn",
                "\u2717 Not Ready", 300, 45, new Color(0.6f, 0.2f, 0.2f));
            _readyButton.onClick.AddListener(ToggleReady);
            _readyButtonLabel = _readyButton.GetComponentInChildren<TextMeshProUGUI>();

            // Start game button (host only)
            _startButton = UIFactory.CreateButton(root, "StartBtn",
                "Start Game (3+ needed)", 300, 45, new Color(0.18f, 0.80f, 0.44f));
            _startButton.onClick.AddListener(HostStartGame);
            _startButton.interactable = false;
            _startButtonLabel = _startButton.GetComponentInChildren<TextMeshProUGUI>();

            // Cancel button
            var cancelBtn = UIFactory.CreateButton(root, "CancelBtn",
                "Cancel", 200, 35, new Color(0.6f, 0.2f, 0.2f), fontSize: 16);
            cancelBtn.onClick.AddListener(CancelNetworking);
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
            entry.NameText.text = $"Player {index + 1}";
            entry.NameText.fontSize = 17;
            entry.NameText.alignment = TextAlignmentOptions.MidlineLeft;

            // Ready indicator
            var readyGo = new GameObject("Ready", typeof(RectTransform));
            readyGo.transform.SetParent(rt, false);
            readyGo.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 28);
            entry.ReadyText = readyGo.AddComponent<TextMeshProUGUI>();
            entry.ReadyText.text = "\u2717";
            entry.ReadyText.fontSize = 20;
            entry.ReadyText.alignment = TextAlignmentOptions.MidlineCenter;
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
                "Enter text...", fontSize,
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

        private void ShowHostLobby()
        {
            _isHost = true;
            _myReady = false;
            _modePanel.SetActive(false);
            _joinPanel.SetActive(false);

            SetupNetworking();
            StartHost();

            _lobbyPanel.SetActive(true);
            _lobbyTitle.text = "LOBBY (Host)";
            _startButton.gameObject.SetActive(true);
            _lobbyIpText.gameObject.SetActive(true);
            _lobbyIpText.text = $"IP: {GetLocalIPAddress()}:{_port}";
            _nicknameInput.text = "Player 1";
            UpdateReadyButtonVisual();
        }

        private void ShowJoinPanel()
        {
            _modePanel.SetActive(false);
            _lobbyPanel.SetActive(false);
            _joinPanel.SetActive(true);
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

            EnsureNetworkManager();

            if (_networkGameManager == null)
            {
                var go = NetworkManager.Singleton.gameObject;
                _networkGameManager = go.AddComponent<NetworkGameManager>();
            }
        }

        private void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;

            var go = new GameObject("NetworkManager");
            DontDestroyOnLoad(go);

            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", _port);

            if (nm.NetworkConfig == null)
                nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;

            Debug.Log("[ConnectionManager] Created NetworkManager with UnityTransport.");
        }

        // ---------------------------------------------------------
        // Host Flow
        // ---------------------------------------------------------

        private void StartHost()
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData("0.0.0.0", _port);

            NetworkManager.Singleton.StartHost();
            _networkGameManager.Initialize(isHost: true);
            _networkGameManager.OnLobbyUpdated += UpdateLobbyDisplay;

            Debug.Log($"[ConnectionManager] Hosting on {GetLocalIPAddress()}:{_port}");
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
            string ip = _ipInput != null ? _ipInput.text.Trim() : "127.0.0.1";
            if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

            SetupNetworking();

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData(ip, _port);

            _joinStatusText.text = $"Connecting to {ip}:{_port}...";

            NetworkManager.Singleton.OnClientConnectedCallback += OnJoinConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnJoinDisconnected;

            NetworkManager.Singleton.StartClient();
            _networkGameManager.Initialize(isHost: false);
        }

        private void OnJoinConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            Debug.Log("[ConnectionManager] Connected to host.");

            _isHost = false;
            _myReady = false;
            _joinPanel.SetActive(false);
            _lobbyPanel.SetActive(true);
            _startButton.gameObject.SetActive(false);
            _lobbyIpText.gameObject.SetActive(false);
            _lobbyTitle.text = "LOBBY";

            int idx = _networkGameManager.LocalPlayerIndex;
            _nicknameInput.text = idx >= 0 ? $"Player {idx + 1}" : "Player";
            UpdateReadyButtonVisual();

            _networkGameManager.OnLobbyUpdated += UpdateLobbyDisplay;
            _networkGameManager.OnGamePhaseChanged += phase =>
            {
                if (phase == GamePhase.Playing)
                    HideAllPanels();
            };
        }

        private void OnJoinDisconnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;
            _joinStatusText.text = "Disconnected from host.";
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
                _readyButtonLabel.text = "\u2713 Ready";
                var cb = _readyButton.colors;
                cb.normalColor = new Color(0.18f, 0.80f, 0.44f);
                cb.selectedColor = new Color(0.18f, 0.80f, 0.44f);
                _readyButton.colors = cb;
            }
            else
            {
                _readyButtonLabel.text = "\u2717 Not Ready";
                var cb = _readyButton.colors;
                cb.normalColor = new Color(0.6f, 0.2f, 0.2f);
                cb.selectedColor = new Color(0.6f, 0.2f, 0.2f);
                _readyButton.colors = cb;
            }
        }

        private void UpdateLobbyDisplay(NetworkGameManager.LobbyPlayerInfo[] players)
        {
            _lobbyCountText.text = $"Players: {players.Length} / 5";

            for (int i = 0; i < 5; i++)
            {
                if (i < players.Length)
                {
                    _lobbyEntries[i].Root.SetActive(true);
                    _lobbyEntries[i].ColorBar.color =
                        UIFactory.GetPlayerColor(players[i].PlayerIndex);

                    string suffix = (players[i].PlayerIndex == 0) ? " (Host)" : "";
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
                    ? "Start Game" : $"Start Game ({players.Length}/3 min)";
            }
        }

        // ---------------------------------------------------------
        // Cancel / Cleanup
        // ---------------------------------------------------------

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

        // ---------------------------------------------------------
        // Utility
        // ---------------------------------------------------------

        private static string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConnectionManager] Could not determine local IP: {e.Message}");
            }
            return "127.0.0.1";
        }
    }
}
