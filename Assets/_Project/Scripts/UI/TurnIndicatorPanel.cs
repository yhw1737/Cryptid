using Cryptid.Core;
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
        private TextMeshProUGUI _timerText;
        private Image _playerColorBar;
        private string[] _playerNames;

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
                L.Format("turn_n", 1), 22, TextAlignmentOptions.MidlineLeft);

            // Active player name
            var playerContainer = UIFactory.CreatePanel(root, "PlayerContainer");
            playerContainer.sizeDelta = new Vector2(260f, 44f);
            _playerText = UIFactory.CreateTMP(playerContainer, "PlayerText",
                L.Format("player_turn", L.Format("player_default", 1)), 26, TextAlignmentOptions.MidlineLeft);

            // Current phase label
            var phaseContainer = UIFactory.CreatePanel(root, "PhaseContainer");
            phaseContainer.sizeDelta = new Vector2(240f, 44f);
            _phaseText = UIFactory.CreateTMP(phaseContainer, "PhaseText",
                L.Get("phase_choose"), 20, TextAlignmentOptions.MidlineRight,
                new Color(0.7f, 0.7f, 0.7f));

            // Timer display
            var timerContainer = UIFactory.CreatePanel(root, "TimerContainer");
            timerContainer.sizeDelta = new Vector2(80f, 44f);
            _timerText = UIFactory.CreateTMP(timerContainer, "TimerText",
                "", 22, TextAlignmentOptions.MidlineRight, UIFactory.Accent);
            _timerText.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Update
        // ---------------------------------------------------------

        /// <summary>Sets player names so the indicator shows nicknames instead of "Player N".</summary>
        public void SetPlayerNames(string[] names) => _playerNames = names;

        /// <summary>Currently set player names.</summary>
        public string[] PlayerNames => _playerNames;

        /// <summary>Updates all turn indicator fields.</summary>
        public void UpdateDisplay(int turnNumber, int playerIndex, TurnPhase phase)
        {
            _turnText.text = L.Format("turn_n", turnNumber);

            string displayName = _playerNames != null && playerIndex < _playerNames.Length
                ? _playerNames[playerIndex]
                : L.Format("player_default", playerIndex + 1);
            _playerText.text = L.Format("player_turn", displayName);
            _playerText.color = UIFactory.GetPlayerColor(playerIndex);

            _playerColorBar.color = UIFactory.GetPlayerColor(playerIndex);

            _phaseText.text = PhaseToString(phase);
        }

        /// <summary>Converts TurnPhase enum to a human-readable string.</summary>
        private static string PhaseToString(TurnPhase phase) => phase switch
        {
            TurnPhase.ChooseAction    => L.Get("phase_choose"),
            TurnPhase.SelectTile      => L.Get("phase_select_tile"),
            TurnPhase.WaitForResponse => L.Get("phase_waiting"),
            TurnPhase.Search          => L.Get("phase_search"),
            TurnPhase.PenaltyPlacement => L.Get("phase_penalty"),
            TurnPhase.TurnEnd         => L.Get("phase_turn_end"),
            _ => phase.ToString()
        };

        // ---------------------------------------------------------
        // Timer
        // ---------------------------------------------------------

        /// <summary>Updates the timer countdown display. Pass negative to hide.</summary>
        public void UpdateTimer(float secondsRemaining)
        {
            if (secondsRemaining < 0f)
            {
                _timerText.gameObject.SetActive(false);
                return;
            }

            _timerText.gameObject.SetActive(true);
            int secs = Mathf.CeilToInt(secondsRemaining);
            _timerText.text = $"{secs}s";

            // Color: flash red when <= 5 seconds
            _timerText.color = secs <= 5
                ? new Color(0.91f, 0.30f, 0.24f)
                : UIFactory.Accent;
        }

        /// <summary>Hides the timer display.</summary>
        public void HideTimer()
        {
            _timerText.gameObject.SetActive(false);
        }
    }
}
