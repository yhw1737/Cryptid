using Cryptid.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Bottom-left panel displaying the current player's own clue.
    /// Visible during the Playing phase only.
    /// Shows a coloured accent bar matching the player's colour.
    /// Created and managed by GameUIManager.
    /// </summary>
    public class CluePanel : MonoBehaviour
    {
        private TextMeshProUGUI _labelText;
        private TextMeshProUGUI _clueText;
        private Image _accentBar;
        private GameObject _bodyContainer;
        private Button _toggleButton;
        private TextMeshProUGUI _toggleLabel;
        private bool _isMinimized;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>Builds the clue panel UI. Called once by GameUIManager.</summary>
        public void Build(RectTransform root)
        {
            // Anchor to bottom-left corner
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(20f, 20f);
            root.sizeDelta = new Vector2(420f, 80f);

            // Toggle button (top-right corner of panel)
            var toggleContainer = UIFactory.CreatePanel(root, "ToggleContainer");
            toggleContainer.anchorMin = new Vector2(1f, 1f);
            toggleContainer.anchorMax = new Vector2(1f, 1f);
            toggleContainer.pivot = new Vector2(1f, 1f);
            toggleContainer.anchoredPosition = new Vector2(-2f, -2f);
            toggleContainer.sizeDelta = new Vector2(26f, 22f);
            _toggleButton = UIFactory.CreateButton(toggleContainer, "ToggleBtn", "▼",
                26, 22, new Color(0.25f, 0.25f, 0.35f), 12);
            _toggleLabel = _toggleButton.GetComponentInChildren<TextMeshProUGUI>();
            _toggleButton.onClick.AddListener(ToggleMinimize);

            // Body container
            _bodyContainer = new GameObject("Body", typeof(RectTransform));
            _bodyContainer.transform.SetParent(root, false);
            var bodyRt = _bodyContainer.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.sizeDelta = Vector2.zero;

            UIFactory.AddHorizontalLayout(bodyRt, spacing: 10,
                padding: new RectOffset(0, 15, 10, 10));

            // Vertical accent bar (player-coloured)
            var barRt = UIFactory.CreatePanel(bodyRt, "AccentBar", UIFactory.Accent);
            barRt.sizeDelta = new Vector2(6f, 60f);
            _accentBar = barRt.GetComponent<Image>();

            // Text column (label + clue description)
            var textCol = UIFactory.CreatePanel(bodyRt, "TextColumn");
            textCol.sizeDelta = new Vector2(380f, 60f);
            UIFactory.AddVerticalLayout(textCol, spacing: 2,
                padding: new RectOffset(5, 5, 5, 5),
                childAlignment: TextAnchor.MiddleLeft);

            // "YOUR CLUE" label
            var labelContainer = UIFactory.CreatePanel(textCol, "LabelContainer");
            labelContainer.sizeDelta = new Vector2(370f, 20f);
            _labelText = UIFactory.CreateTMP(labelContainer, "LabelText",
                L.Get("your_clue"), 14, TextAlignmentOptions.MidlineLeft,
                new Color(0.6f, 0.6f, 0.6f));

            // Clue description text
            var clueContainer = UIFactory.CreatePanel(textCol, "ClueContainer");
            clueContainer.sizeDelta = new Vector2(370f, 30f);
            _clueText = UIFactory.CreateTMP(clueContainer, "ClueText",
                "", 20, TextAlignmentOptions.MidlineLeft);
        }

        // ---------------------------------------------------------
        // Minimize / Maximize
        // ---------------------------------------------------------

        /// <summary>Toggles the clue panel between minimized and maximized.</summary>
        public void ToggleMinimize()
        {
            _isMinimized = !_isMinimized;
            _bodyContainer.SetActive(!_isMinimized);
            _toggleLabel.text = _isMinimized ? "▲" : "▼";

            // Hide the entire panel background when minimized
            var rootImage = GetComponent<Image>() ?? transform.parent?.GetComponent<Image>();
            if (rootImage != null)
                rootImage.enabled = !_isMinimized;
        }

        // ---------------------------------------------------------
        // Update
        // ---------------------------------------------------------

        /// <summary>Updates the clue display for a specific player.</summary>
        public void UpdateClue(int playerIndex, string clueDescription)
        {
            var color = UIFactory.GetPlayerColor(playerIndex);
            _clueText.text = clueDescription;
            _clueText.color = color;
            _accentBar.color = color;
            _labelText.text = L.Format("player_clue", playerIndex + 1);
        }

        /// <summary>Clears the clue text.</summary>
        public void Clear()
        {
            _clueText.text = "";
            _labelText.text = L.Get("your_clue");
            _accentBar.color = UIFactory.Accent;
        }
    }
}
