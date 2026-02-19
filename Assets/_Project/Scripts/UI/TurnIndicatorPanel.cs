using Cryptid.Systems.Turn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Top-bar HUD panel showing turn number, active player, and current phase.
    /// Anchored to the top of the screen, full width.
    /// Created and managed by GameUIManager.
    /// </summary>
    public class TurnIndicatorPanel : MonoBehaviour
    {
        private TextMeshProUGUI _turnText;
        private TextMeshProUGUI _playerText;
        private TextMeshProUGUI _phaseText;
        private Image _playerColorBar;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>
        /// Builds the panel UI elements. Called once by GameUIManager.
        /// </summary>
        public void Build(RectTransform root)
        {
            // Anchor to top, full width, 60px tall
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, 60f);

            UIFactory.AddHorizontalLayout(root, spacing: 20,
                padding: new RectOffset(20, 20, 8, 8));

            // Player color indicator bar (thin vertical accent)
            var colorBarRt = UIFactory.CreatePanel(root, "PlayerColorBar",
                UIFactory.GetPlayerColor(0));
            colorBarRt.sizeDelta = new Vector2(8f, 44f);
            _playerColorBar = colorBarRt.GetComponent<Image>();

            // Turn number label
            var turnContainer = UIFactory.CreatePanel(root, "TurnContainer");
            turnContainer.sizeDelta = new Vector2(140f, 44f);
            _turnText = UIFactory.CreateTMP(turnContainer, "TurnText",
                "Turn 1", 22, TextAlignmentOptions.MidlineLeft);

            // Active player name
            var playerContainer = UIFactory.CreatePanel(root, "PlayerContainer");
            playerContainer.sizeDelta = new Vector2(260f, 44f);
            _playerText = UIFactory.CreateTMP(playerContainer, "PlayerText",
                "Player 1's Turn", 26, TextAlignmentOptions.MidlineLeft);

            // Current phase label
            var phaseContainer = UIFactory.CreatePanel(root, "PhaseContainer");
            phaseContainer.sizeDelta = new Vector2(300f, 44f);
            _phaseText = UIFactory.CreateTMP(phaseContainer, "PhaseText",
                "Choose Action", 20, TextAlignmentOptions.MidlineRight,
                new Color(0.7f, 0.7f, 0.7f));
        }

        // ---------------------------------------------------------
        // Update
        // ---------------------------------------------------------

        /// <summary>Updates all turn indicator fields.</summary>
        public void UpdateDisplay(int turnNumber, int playerIndex, TurnPhase phase)
        {
            _turnText.text = $"Turn {turnNumber}";

            _playerText.text = $"Player {playerIndex + 1}'s Turn";
            _playerText.color = UIFactory.GetPlayerColor(playerIndex);

            _playerColorBar.color = UIFactory.GetPlayerColor(playerIndex);

            _phaseText.text = PhaseToString(phase);
        }

        /// <summary>Converts TurnPhase enum to a human-readable string.</summary>
        private static string PhaseToString(TurnPhase phase) => phase switch
        {
            TurnPhase.ChooseAction    => "Choose Action",
            TurnPhase.SelectTile      => "Select a Tile",
            TurnPhase.WaitForResponse => "Waiting for Response...",
            TurnPhase.Search          => "Search a Tile",
            TurnPhase.PenaltyPlacement => "Place Penalty Cube",
            TurnPhase.TurnEnd         => "Turn Ending...",
            _ => phase.ToString()
        };
    }
}
