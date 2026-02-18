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

            UIFactory.AddHorizontalLayout(root, spacing: 10,
                padding: new RectOffset(0, 15, 10, 10));

            // Vertical accent bar (player-coloured)
            var barRt = UIFactory.CreatePanel(root, "AccentBar", UIFactory.Accent);
            barRt.sizeDelta = new Vector2(6f, 60f);
            _accentBar = barRt.GetComponent<Image>();

            // Text column (label + clue description)
            var textCol = UIFactory.CreatePanel(root, "TextColumn");
            textCol.sizeDelta = new Vector2(380f, 60f);
            UIFactory.AddVerticalLayout(textCol, spacing: 2,
                padding: new RectOffset(5, 5, 5, 5),
                childAlignment: TextAnchor.MiddleLeft);

            // "YOUR CLUE" label
            var labelContainer = UIFactory.CreatePanel(textCol, "LabelContainer");
            labelContainer.sizeDelta = new Vector2(370f, 20f);
            _labelText = UIFactory.CreateTMP(labelContainer, "LabelText",
                "YOUR CLUE", 14, TextAlignmentOptions.MidlineLeft,
                new Color(0.6f, 0.6f, 0.6f));

            // Clue description text
            var clueContainer = UIFactory.CreatePanel(textCol, "ClueContainer");
            clueContainer.sizeDelta = new Vector2(370f, 30f);
            _clueText = UIFactory.CreateTMP(clueContainer, "ClueText",
                "", 20, TextAlignmentOptions.MidlineLeft);
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
            _labelText.text = $"PLAYER {playerIndex + 1}'S CLUE";
        }

        /// <summary>Clears the clue text.</summary>
        public void Clear()
        {
            _clueText.text = "";
            _labelText.text = "YOUR CLUE";
            _accentBar.color = UIFactory.Accent;
        }
    }
}
