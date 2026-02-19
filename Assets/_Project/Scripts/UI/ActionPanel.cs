using System;
using Cryptid.Systems.Turn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Central panel with Question / Search action buttons.
    /// Shows contextual instruction text based on the current turn phase.
    /// Visible during ChooseAction, SelectTile, and Search phases.
    /// Created and managed by GameUIManager.
    /// </summary>
    public class ActionPanel : MonoBehaviour
    {
        private Button _questionBtn;
        private Button _searchBtn;
        private TextMeshProUGUI _instructionText;
        private GameObject _buttonContainer;

        /// <summary>Fired when the player clicks an action button.</summary>
        public event Action<PlayerAction> OnActionClicked;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>Builds the panel UI. Called once by GameUIManager.</summary>
        public void Build(RectTransform root)
        {
            // Anchor to bottom-center
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 120f);
            root.sizeDelta = new Vector2(460f, 140f);

            UIFactory.AddVerticalLayout(root, spacing: 10,
                padding: new RectOffset(20, 20, 15, 15));

            // Instruction text (top row)
            var instrContainer = UIFactory.CreatePanel(root, "InstructionContainer");
            instrContainer.sizeDelta = new Vector2(420f, 30f);
            _instructionText = UIFactory.CreateTMP(instrContainer, "InstructionText",
                "", 18, color: new Color(0.85f, 0.85f, 0.85f));

            // Button row (bottom row)
            var btnRow = UIFactory.CreatePanel(root, "ButtonRow");
            btnRow.sizeDelta = new Vector2(420f, 60f);
            _buttonContainer = btnRow.gameObject;
            UIFactory.AddHorizontalLayout(btnRow, spacing: 20);

            // Question button (blue)
            _questionBtn = UIFactory.CreateButton(btnRow, "QuestionBtn", "Question",
                190, 55, new Color(0.16f, 0.50f, 0.73f));
            _questionBtn.onClick.AddListener(() => OnActionClicked?.Invoke(PlayerAction.Question));

            // Search button (red)
            _searchBtn = UIFactory.CreateButton(btnRow, "SearchBtn", "Search",
                190, 55, new Color(0.75f, 0.22f, 0.17f));
            _searchBtn.onClick.AddListener(() => OnActionClicked?.Invoke(PlayerAction.Search));
        }

        // ---------------------------------------------------------
        // Phase Update
        // ---------------------------------------------------------

        /// <summary>Updates visibility and instruction text based on current phase.</summary>
        public void UpdateForPhase(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.ChooseAction:
                    _buttonContainer.SetActive(true);
                    _instructionText.text = "Choose your action";
                    gameObject.SetActive(true);
                    break;

                case TurnPhase.SelectTile:
                    _buttonContainer.SetActive(false);
                    _instructionText.text = "Click a tile to ask about";
                    gameObject.SetActive(true);
                    break;

                case TurnPhase.Search:
                    _buttonContainer.SetActive(false);
                    _instructionText.text = "Click a tile to search for the Cryptid";
                    gameObject.SetActive(true);
                    break;

                case TurnPhase.PenaltyPlacement:
                    _buttonContainer.SetActive(false);
                    _instructionText.text = "Wrong! Place a cube on a tile your clue does NOT match";
                    gameObject.SetActive(true);
                    break;

                default:
                    gameObject.SetActive(false);
                    break;
            }
        }
    }
}
