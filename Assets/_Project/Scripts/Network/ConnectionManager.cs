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
    /// Manages the pre-game flow: mode selection (Local / Host / Join)
    /// and NetworkManager lifecycle.
    ///
    /// Scene setup:
    ///   1. Add this component to any GameObject in the scene.
    ///   2. Assign the GameBootstrapper reference.
    ///   3. The rest is created at runtime.
    ///
    /// Flow:
    ///   Scene Load → Mode Selection UI → User picks mode:
    ///     Local:  GameBootstrapper runs normally.
    ///     Host:   NetworkManager starts as host, lobby shown, Start button.
    ///     Join:   IP entry, connects as client, waits for host to start.
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

        // UI Elements
        private GameObject _modePanel;
        private GameObject _hostLobbyPanel;
        private GameObject _joinPanel;
        private TextMeshProUGUI _hostStatusText;
        private TextMeshProUGUI _joinStatusText;
        private TMP_InputField _ipInput;
        private Button _startGameButton;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            // Auto-find bootstrapper if not assigned
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<GameBootstrapper>();

            BuildModeSelectionUI();
        }

        private void OnDestroy()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        // ---------------------------------------------------------
        // UI Construction
        // ---------------------------------------------------------

        private void BuildModeSelectionUI()
        {
            _canvas = UIFactory.CreateScreenCanvas("ConnectionUI_Canvas", 100);
            _canvas.transform.SetParent(transform);

            // --- Mode Selection Panel (centered) ---
            _modePanel = CreateCenteredPanel("ModePanel", 400, 350);

            var modeRoot = _modePanel.GetComponent<RectTransform>();
            UIFactory.AddVerticalLayout(modeRoot, spacing: 15,
                padding: new RectOffset(30, 30, 30, 30));

            // Title
            var title = UIFactory.CreateTMP(modeRoot, "Title", "CRYPTID",
                fontSize: 42, color: UIFactory.Accent);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.sizeDelta = new Vector2(340, 60);

            var subtitle = UIFactory.CreateTMP(modeRoot, "Subtitle",
                "Choose Game Mode", fontSize: 20);
            var subRT = subtitle.GetComponent<RectTransform>();
            subRT.sizeDelta = new Vector2(340, 30);

            // Buttons
            var localBtn = UIFactory.CreateButton(modeRoot, "LocalBtn",
                "Local Game", 340, 55, new Color(0.18f, 0.80f, 0.44f));
            localBtn.onClick.AddListener(StartLocalGame);

            var hostBtn = UIFactory.CreateButton(modeRoot, "HostBtn",
                "Host Game", 340, 55, new Color(0.20f, 0.60f, 0.86f));
            hostBtn.onClick.AddListener(ShowHostLobby);

            var joinBtn = UIFactory.CreateButton(modeRoot, "JoinBtn",
                "Join Game", 340, 55, new Color(0.95f, 0.61f, 0.07f));
            joinBtn.onClick.AddListener(ShowJoinPanel);

            // --- Host Lobby Panel ---
            _hostLobbyPanel = CreateCenteredPanel("HostLobbyPanel", 400, 300);

            var hostRoot = _hostLobbyPanel.GetComponent<RectTransform>();
            UIFactory.AddVerticalLayout(hostRoot, spacing: 12,
                padding: new RectOffset(25, 25, 25, 25));

            var hostTitle = UIFactory.CreateTMP(hostRoot, "Title",
                "Hosting Game", fontSize: 28, color: UIFactory.Accent);
            hostTitle.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 40);

            _hostStatusText = UIFactory.CreateTMP(hostRoot, "Status",
                "Waiting for players...", fontSize: 18);
            _hostStatusText.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 80);

            _startGameButton = UIFactory.CreateButton(hostRoot, "StartBtn",
                "Start Game", 200, 50, new Color(0.18f, 0.80f, 0.44f));
            _startGameButton.onClick.AddListener(HostStartGame);
            _startGameButton.interactable = false; // Need 2+ players

            var cancelHostBtn = UIFactory.CreateButton(hostRoot, "CancelBtn",
                "Cancel", 200, 40, new Color(0.6f, 0.2f, 0.2f),
                fontSize: 18);
            cancelHostBtn.onClick.AddListener(CancelNetworking);

            _hostLobbyPanel.SetActive(false);

            // --- Join Panel ---
            _joinPanel = CreateCenteredPanel("JoinPanel", 400, 280);

            var joinRoot = _joinPanel.GetComponent<RectTransform>();
            UIFactory.AddVerticalLayout(joinRoot, spacing: 12,
                padding: new RectOffset(25, 25, 25, 25));

            var joinTitle = UIFactory.CreateTMP(joinRoot, "Title",
                "Join Game", fontSize: 28, color: UIFactory.Accent);
            joinTitle.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 40);

            // IP Input
            var inputGo = new GameObject("IPInput", typeof(RectTransform));
            inputGo.transform.SetParent(joinRoot, false);
            var inputRT = inputGo.GetComponent<RectTransform>();
            inputRT.sizeDelta = new Vector2(340, 45);

            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            _ipInput = inputGo.AddComponent<TMP_InputField>();
            _ipInput.text = "127.0.0.1";

            // Input text area
            var textArea = new GameObject("TextArea", typeof(RectTransform));
            textArea.transform.SetParent(inputGo.transform, false);
            var taRT = textArea.GetComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero;
            taRT.anchorMax = Vector2.one;
            taRT.sizeDelta = Vector2.zero;
            taRT.offsetMin = new Vector2(10, 0);
            taRT.offsetMax = new Vector2(-10, 0);

            var inputText = UIFactory.CreateTMP(textArea.transform, "Text",
                "", fontSize: 20, align: TextAlignmentOptions.MidlineLeft);
            inputText.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            _ipInput.textComponent = inputText;
            _ipInput.textViewport = taRT;

            var connectBtn = UIFactory.CreateButton(joinRoot, "ConnectBtn",
                "Connect", 200, 50, new Color(0.20f, 0.60f, 0.86f));
            connectBtn.onClick.AddListener(JoinGame);

            _joinStatusText = UIFactory.CreateTMP(joinRoot, "Status",
                "", fontSize: 16);
            _joinStatusText.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 30);

            var cancelJoinBtn = UIFactory.CreateButton(joinRoot, "CancelBtn",
                "Cancel", 200, 40, new Color(0.6f, 0.2f, 0.2f),
                fontSize: 18);
            cancelJoinBtn.onClick.AddListener(CancelNetworking);

            _joinPanel.SetActive(false);
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

            // Let GameBootstrapper handle everything
            if (_bootstrapper != null)
                _bootstrapper.StartLocalGame();

            Debug.Log("[ConnectionManager] Starting local game.");
        }

        private void ShowHostLobby()
        {
            _modePanel.SetActive(false);
            _joinPanel.SetActive(false);
            _hostLobbyPanel.SetActive(true);

            SetupNetworking();
            StartHost();
        }

        private void ShowJoinPanel()
        {
            _modePanel.SetActive(false);
            _hostLobbyPanel.SetActive(false);
            _joinPanel.SetActive(true);
            _joinStatusText.text = "";
        }

        private void HideAllPanels()
        {
            _modePanel.SetActive(false);
            _hostLobbyPanel.SetActive(false);
            _joinPanel.SetActive(false);

            // Disable entire canvas after mode selected
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Networking Setup
        // ---------------------------------------------------------

        private void SetupNetworking()
        {
            // Disable GameBootstrapper's input handling
            if (_bootstrapper != null)
                _bootstrapper.DisableForNetworkMode();

            // Create or find NetworkManager
            EnsureNetworkManager();

            // Create NetworkGameManager
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

            // Configure transport
            transport.SetConnectionData("127.0.0.1", _port);

            // Assign transport to NetworkManager via config
            // In NGO 2.x, NetworkConfig is auto-created
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

            // Update lobby UI on player join/leave
            _networkGameManager.OnPlayerCountChanged += UpdateHostLobbyUI;

            UpdateHostLobbyUI(_networkGameManager.ConnectedPlayerCount);

            string localIP = GetLocalIPAddress();
            Debug.Log($"[ConnectionManager] Hosting on {localIP}:{_port}");
        }

        private void HostStartGame()
        {
            HideAllPanels();
            _networkGameManager.OnPlayerCountChanged -= UpdateHostLobbyUI;
            _networkGameManager.StartGame();
        }

        private void UpdateHostLobbyUI(int playerCount)
        {
            if (_hostStatusText != null)
            {
                string ip = GetLocalIPAddress();
                _hostStatusText.text = $"IP: {ip}:{_port}\n" +
                                       $"Players connected: {playerCount}\n" +
                                       $"(Need at least 2 to start)";
            }

            if (_startGameButton != null)
                _startGameButton.interactable = playerCount >= 2;
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

            _joinStatusText.text = "Connected! Waiting for host to start...";
            Debug.Log("[ConnectionManager] Connected to host.");

            // Hide join panel eventually (when game starts via S2C_SetupMap)
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
        // Cancel / Cleanup
        // ---------------------------------------------------------

        private void CancelNetworking()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            if (_networkGameManager != null)
            {
                Destroy(_networkGameManager);
                _networkGameManager = null;
            }

            // Re-enable GameBootstrapper
            if (_bootstrapper != null)
                _bootstrapper.enabled = true;

            // Show mode selection
            _modePanel.SetActive(true);
            _hostLobbyPanel.SetActive(false);
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
