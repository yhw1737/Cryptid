using System;
using System.Collections.Generic;
using Cryptid.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Right-side scrolling log panel that records all game actions.
    /// Shows questions, responses, searches, and penalties in chronological order.
    /// Each entry is colour-coded by the acting player.
    /// Created and managed by GameUIManager.
    /// Supports minimize/maximize toggle with notification badge and chat input.
    /// </summary>
    public class GameLogPanel : MonoBehaviour
    {
        private ScrollRect _scrollRect;
        private RectTransform _contentRoot;
        private readonly List<GameObject> _entries = new();

        private const int MAX_ENTRIES = 100;
        private const float ENTRY_HEIGHT = 28f;
        private const float PANEL_WIDTH = 340f;
        private const float INPUT_HEIGHT = 32f;

        // Minimize support
        private GameObject _bodyContainer;
        private Button _toggleButton;
        private TextMeshProUGUI _toggleLabel;
        private GameObject _notificationBadge;
        private TextMeshProUGUI _badgeCountText;
        private bool _isMinimized;
        private int _unreadCount;

        // Chat input
        private TMP_InputField _chatInput;
        private GameObject _chatInputContainer;

        /// <summary>Whether the log panel is currently minimized.</summary>
        public bool IsMinimized => _isMinimized;

        /// <summary>Fired when the panel is minimized or maximized.</summary>
        public event Action<bool> OnMinimizeToggled;

        /// <summary>Fired when the user sends a chat message (Enter key).</summary>
        public event Action<string> OnChatMessageSent;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>Builds the scrollable log panel. Called once by GameUIManager.</summary>
        public void Build(RectTransform root)
        {
            // Anchor to right side of screen
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 0.5f);
            root.anchoredPosition = new Vector2(-10f, 0f);
            root.sizeDelta = new Vector2(PANEL_WIDTH, -130f); // Leave space at top/bottom

            // Header with toggle button
            var headerRt = UIFactory.CreatePanel(root, "LogHeader");
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 30f);

            var headerLayout = UIFactory.AddHorizontalLayout(headerRt, spacing: 5,
                padding: new RectOffset(5, 5, 2, 2));
            headerLayout.childControlWidth = false;
            headerLayout.childForceExpandWidth = false;

            // Header title
            var titleContainer = UIFactory.CreatePanel(headerRt, "TitleContainer");
            titleContainer.sizeDelta = new Vector2(PANEL_WIDTH - 80f, 26f);
            var headerTmp = UIFactory.CreateTMP(titleContainer, "HeaderText",
                L.Get("game_log"), 14, TextAlignmentOptions.Center,
                new Color(0.5f, 0.5f, 0.5f));

            // Toggle button (▼/▲)
            _toggleButton = UIFactory.CreateButton(headerRt, "ToggleBtn", "▼",
                30, 26, new Color(0.25f, 0.25f, 0.35f), 14);
            _toggleLabel = _toggleButton.GetComponentInChildren<TextMeshProUGUI>();
            _toggleButton.onClick.AddListener(ToggleMinimize);

            // Notification badge (hidden by default)
            var badgeContainer = UIFactory.CreatePanel(headerRt, "NotifBadge",
                new Color(0.91f, 0.30f, 0.24f));
            badgeContainer.sizeDelta = new Vector2(24f, 24f);
            _badgeCountText = UIFactory.CreateTMP(badgeContainer, "BadgeCount",
                "0", 12, TextAlignmentOptions.Center, Color.white);
            _notificationBadge = badgeContainer.gameObject;
            _notificationBadge.SetActive(false);

            // Body container (everything below header, toggled on minimize)
            _bodyContainer = new GameObject("Body", typeof(RectTransform));
            _bodyContainer.transform.SetParent(root, false);
            var bodyRt = _bodyContainer.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.sizeDelta = new Vector2(-10f, -35f);
            bodyRt.anchoredPosition = new Vector2(0f, -15f);

            // ScrollRect viewport (leave room for input field at bottom)
            var viewport = UIFactory.CreatePanel(bodyRt, "Viewport");
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.sizeDelta = new Vector2(0f, -INPUT_HEIGHT - 4f);
            viewport.anchoredPosition = new Vector2(0f, (INPUT_HEIGHT + 4f) / 2f);
            var viewportMask = viewport.gameObject.AddComponent<RectMask2D>();

            // Content container (grows downward)
            _contentRoot = UIFactory.CreatePanel(viewport, "Content");
            _contentRoot.anchorMin = new Vector2(0f, 1f);
            _contentRoot.anchorMax = new Vector2(1f, 1f);
            _contentRoot.pivot = new Vector2(0.5f, 1f);
            _contentRoot.sizeDelta = new Vector2(0f, 0f);

            var vlg = UIFactory.AddVerticalLayout(_contentRoot, spacing: 2,
                padding: new RectOffset(5, 5, 5, 5),
                childAlignment: TextAnchor.UpperLeft);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            var fitter = _contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ScrollRect component
            _scrollRect = root.gameObject.AddComponent<ScrollRect>();
            _scrollRect.viewport = viewport;
            _scrollRect.content = _contentRoot;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 30f;

            // Chat input field at bottom of body
            BuildChatInput(bodyRt);
        }

        /// <summary>Creates the chat input field anchored to the bottom of the body container.</summary>
        private void BuildChatInput(RectTransform parent)
        {
            _chatInputContainer = new GameObject("ChatInputContainer", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            _chatInputContainer.transform.SetParent(parent, false);
            var containerRt = _chatInputContainer.GetComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(1f, 0f);
            containerRt.pivot = new Vector2(0.5f, 0f);
            containerRt.anchoredPosition = Vector2.zero;
            containerRt.sizeDelta = new Vector2(0f, INPUT_HEIGHT);
            _chatInputContainer.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

            // Create TMP_InputField
            var inputGo = new GameObject("ChatInput", typeof(RectTransform));
            inputGo.transform.SetParent(containerRt, false);
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 1f);
            inputRt.pivot = new Vector2(0.5f, 0.5f);
            inputRt.sizeDelta = new Vector2(-8f, -4f);
            inputRt.anchoredPosition = Vector2.zero;

            // Text area
            var textAreaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textAreaGo.transform.SetParent(inputRt, false);
            var textAreaRt = textAreaGo.GetComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.sizeDelta = new Vector2(-10f, 0f);
            textAreaRt.anchoredPosition = Vector2.zero;

            // Placeholder text
            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(textAreaRt, false);
            var placeholderRt = placeholderGo.GetComponent<RectTransform>();
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.sizeDelta = Vector2.zero;
            placeholderRt.anchoredPosition = Vector2.zero;
            var placeholderTmp = placeholderGo.GetComponent<TextMeshProUGUI>();
            placeholderTmp.text = L.Get("chat_placeholder");
            placeholderTmp.fontSize = 13;
            placeholderTmp.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderTmp.fontStyle = FontStyles.Italic;
            if (UIFactory.KoreanFont != null) placeholderTmp.font = UIFactory.KoreanFont;

            // Input text
            var inputTextGo = new GameObject("Text", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            inputTextGo.transform.SetParent(textAreaRt, false);
            var inputTextRt = inputTextGo.GetComponent<RectTransform>();
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.sizeDelta = Vector2.zero;
            inputTextRt.anchoredPosition = Vector2.zero;
            var inputTextTmp = inputTextGo.GetComponent<TextMeshProUGUI>();
            inputTextTmp.fontSize = 13;
            inputTextTmp.color = Color.white;
            if (UIFactory.KoreanFont != null) inputTextTmp.font = UIFactory.KoreanFont;

            // Setup TMP_InputField
            _chatInput = inputGo.AddComponent<TMP_InputField>();
            _chatInput.textViewport = textAreaRt;
            _chatInput.textComponent = inputTextTmp;
            _chatInput.placeholder = placeholderTmp;
            _chatInput.fontAsset = UIFactory.KoreanFont;
            _chatInput.pointSize = 13;
            _chatInput.characterLimit = 100;
            _chatInput.lineType = TMP_InputField.LineType.SingleLine;
            _chatInput.onSubmit.AddListener(OnChatSubmit);

            // Background image for input field
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.18f, 0.18f, 0.25f, 0.8f);
            _chatInput.targetGraphic = inputBg;
        }

        // ---------------------------------------------------------
        // Minimize / Maximize
        // ---------------------------------------------------------

        /// <summary>Toggles the log panel between minimized and maximized.</summary>
        public void ToggleMinimize()
        {
            _isMinimized = !_isMinimized;
            _bodyContainer.SetActive(!_isMinimized);
            _toggleLabel.text = _isMinimized ? "▲" : "▼";

            // Resize the root panel: when minimized, shrink to header only
            var rootRt = GetComponent<RectTransform>() ?? transform.parent?.GetComponent<RectTransform>();
            if (rootRt != null)
            {
                if (_isMinimized)
                {
                    // Collapse to just the header height
                    rootRt.anchorMin = new Vector2(1f, 1f);
                    rootRt.anchorMax = new Vector2(1f, 1f);
                    rootRt.pivot = new Vector2(1f, 1f);
                    rootRt.anchoredPosition = new Vector2(-10f, -60f);
                    rootRt.sizeDelta = new Vector2(PANEL_WIDTH, 34f);
                }
                else
                {
                    // Restore full height
                    rootRt.anchorMin = new Vector2(1f, 0f);
                    rootRt.anchorMax = new Vector2(1f, 1f);
                    rootRt.pivot = new Vector2(1f, 0.5f);
                    rootRt.anchoredPosition = new Vector2(-10f, 0f);
                    rootRt.sizeDelta = new Vector2(PANEL_WIDTH, -130f);
                }
            }

            // Toggle panel background visibility
            var rootImage = GetComponent<Image>();
            if (rootImage != null)
                rootImage.enabled = !_isMinimized;

            if (!_isMinimized)
            {
                // Clear unread count when maximized
                _unreadCount = 0;
                _notificationBadge.SetActive(false);
            }

            OnMinimizeToggled?.Invoke(_isMinimized);
        }

        /// <summary>Sets the panel to a specific minimized state.</summary>
        public void SetMinimized(bool minimized)
        {
            if (_isMinimized == minimized) return;
            ToggleMinimize();
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Adds a colour-coded entry to the log. Player colour is applied to the prefix.
        /// </summary>
        /// <param name="playerIndex">Player associated with this entry (-1 for system messages).</param>
        /// <param name="message">The log text.</param>
        public void AddEntry(int playerIndex, string message)
        {
            // Limit entries
            if (_entries.Count >= MAX_ENTRIES)
            {
                var oldest = _entries[0];
                _entries.RemoveAt(0);
                Destroy(oldest);
            }

            var entryContainer = UIFactory.CreatePanel(_contentRoot, $"LogEntry_{_entries.Count}");
            entryContainer.sizeDelta = new Vector2(0f, ENTRY_HEIGHT);

            Color textColor = playerIndex >= 0
                ? UIFactory.GetPlayerColor(playerIndex)
                : new Color(0.6f, 0.6f, 0.6f);

            var tmp = UIFactory.CreateTMP(entryContainer, "Text",
                message, 13, TextAlignmentOptions.MidlineLeft, textColor);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Truncate;

            _entries.Add(entryContainer.gameObject);

            // Update notification badge if minimized
            if (_isMinimized)
            {
                _unreadCount++;
                _notificationBadge.SetActive(true);
                _badgeCountText.text = _unreadCount > 99 ? "99+" : _unreadCount.ToString();
            }

            // Auto-scroll to bottom
            Canvas.ForceUpdateCanvases();
            _scrollRect.normalizedPosition = Vector2.zero;
        }

        /// <summary>Adds a system message (grey text, no player colour).</summary>
        public void AddSystemMessage(string message) => AddEntry(-1, message);

        /// <summary>Clears all log entries.</summary>
        public void Clear()
        {
            foreach (var entry in _entries)
            {
                if (entry != null) Destroy(entry);
            }
            _entries.Clear();
        }

        // ---------------------------------------------------------
        // Chat Input
        // ---------------------------------------------------------

        /// <summary>
        /// Pressing Enter when chat is NOT focused will focus the chat input.
        /// This lets users start typing immediately without clicking the field.
        /// </summary>
        private void Update()
        {
            if (_chatInput == null || _isMinimized) return;
            if (!_chatInput.isFocused && Keyboard.current != null &&
                Keyboard.current.enterKey.wasPressedThisFrame)
            {
                _chatInput.ActivateInputField();
            }
        }

        /// <summary>Called when the user presses Enter in the chat input field.</summary>
        private void OnChatSubmit(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            OnChatMessageSent?.Invoke(text.Trim());
            _chatInput.text = "";

            // Re-activate the input field so user can keep typing
            _chatInput.ActivateInputField();
        }

        /// <summary>Shows or hides the chat input field.</summary>
        public void SetChatInputVisible(bool visible)
        {
            if (_chatInputContainer != null)
                _chatInputContainer.SetActive(visible);
        }

        /// <summary>Focuses the chat input field.</summary>
        public void FocusChatInput()
        {
            _chatInput?.ActivateInputField();
        }
    }
}
