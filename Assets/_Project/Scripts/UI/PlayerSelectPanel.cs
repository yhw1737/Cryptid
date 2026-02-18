using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Panel for selecting which opponent to ask during a Question action.
    /// Displays coloured player buttons (excluding the current player).
    /// Default target is the next player in turn order; player can click to change.
    /// Created and managed by GameUIManager.
    /// </summary>
    public class PlayerSelectPanel : MonoBehaviour
    {
        private readonly List<Button> _playerButtons = new();
        private readonly List<Image> _buttonImages = new();
        private TextMeshProUGUI _headerText;
        private int _selectedTarget = -1;
        private int _currentPlayer = -1;

        /// <summary>Currently selected target player index (0-based).</summary>
        public int SelectedTarget => _selectedTarget;

        /// <summary>Fired when a target player is selected. Arg: playerIndex.</summary>
        public event Action<int> OnTargetSelected;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>
        /// Builds the panel with buttons for up to maxPlayers players.
        /// Actual visibility is controlled by Show/Hide.
        /// </summary>
        public void Build(RectTransform root, int maxPlayers)
        {
            // Position above the action panel
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 270f);
            root.sizeDelta = new Vector2(400f, 100f);

            UIFactory.AddVerticalLayout(root, spacing: 8,
                padding: new RectOffset(15, 15, 10, 10));

            // Header text
            var headerContainer = UIFactory.CreatePanel(root, "Header");
            headerContainer.sizeDelta = new Vector2(370f, 25f);
            _headerText = UIFactory.CreateTMP(headerContainer, "HeaderText",
                "Ask which player?", 16, color: new Color(0.7f, 0.7f, 0.7f));

            // Player button row
            var row = UIFactory.CreatePanel(root, "PlayerRow");
            row.sizeDelta = new Vector2(370f, 45f);
            UIFactory.AddHorizontalLayout(row, spacing: 10);

            for (int i = 0; i < maxPlayers; i++)
            {
                int idx = i; // capture for closure
                var color = UIFactory.GetPlayerColor(i);
                var btn = UIFactory.CreateButton(row, $"Player{i + 1}Btn",
                    $"P{i + 1}", 60, 40, color, 18);
                btn.onClick.AddListener(() => SelectTarget(idx));
                _playerButtons.Add(btn);
                _buttonImages.Add(btn.GetComponent<Image>());
            }

            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Show / Hide
        // ---------------------------------------------------------

        /// <summary>
        /// Shows the panel for a given turn, pre-selecting the default target.
        /// Hides the current player's button; hides buttons beyond totalPlayers.
        /// </summary>
        public void Show(int currentPlayer, int defaultTarget, int totalPlayers)
        {
            _currentPlayer = currentPlayer;
            _selectedTarget = defaultTarget;

            // Show only valid opponent buttons
            for (int i = 0; i < _playerButtons.Count; i++)
            {
                _playerButtons[i].gameObject.SetActive(i != currentPlayer && i < totalPlayers);
            }

            HighlightSelected();
            gameObject.SetActive(true);
        }

        /// <summary>Hides the panel.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Internal
        // ---------------------------------------------------------

        private void SelectTarget(int playerIndex)
        {
            if (playerIndex == _currentPlayer) return;

            _selectedTarget = playerIndex;
            HighlightSelected();
            OnTargetSelected?.Invoke(playerIndex);
        }

        /// <summary>Dims non-selected buttons and brightens the selected one.</summary>
        private void HighlightSelected()
        {
            for (int i = 0; i < _playerButtons.Count; i++)
            {
                if (i == _currentPlayer) continue;

                var baseColor = UIFactory.GetPlayerColor(i);
                _buttonImages[i].color = (i == _selectedTarget)
                    ? baseColor
                    : baseColor * 0.4f;
            }
        }
    }
}
