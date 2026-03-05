using System;
using Cryptid.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Full-screen overlay panel shown when the game ends.
    /// Displays the winner, answer location, and a "Play Again" button.
    /// Created and managed by GameUIManager.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _winnerText;
        private TextMeshProUGUI _detailText;
        private Button _restartBtn;

        /// <summary>Fired when the player clicks "Play Again".</summary>
        public event Action OnRestartClicked;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>Builds the game-over overlay. Called once by GameUIManager.</summary>
        public void Build(RectTransform root)
        {
            // Fullscreen overlay
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;

            // Semi-transparent dark background (blocks raycasts to game world)
            var overlay = root.gameObject.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.8f);
            overlay.raycastTarget = true;

            // Centre content box
            var center = UIFactory.CreatePanel(root, "CenterBox",
                new Color(0.1f, 0.1f, 0.15f, 0.95f));
            center.anchorMin = new Vector2(0.5f, 0.5f);
            center.anchorMax = new Vector2(0.5f, 0.5f);
            center.pivot = new Vector2(0.5f, 0.5f);
            center.sizeDelta = new Vector2(500f, 300f);

            UIFactory.AddVerticalLayout(center, spacing: 15,
                padding: new RectOffset(30, 30, 30, 30));

            // "GAME OVER" title
            var titleContainer = UIFactory.CreatePanel(center, "TitleContainer");
            titleContainer.sizeDelta = new Vector2(440f, 50f);
            _titleText = UIFactory.CreateTMP(titleContainer, "Title",
                L.Get("game_over"), 42, color: UIFactory.Accent);

            // Winner announcement
            var winnerContainer = UIFactory.CreatePanel(center, "WinnerContainer");
            winnerContainer.sizeDelta = new Vector2(440f, 40f);
            _winnerText = UIFactory.CreateTMP(winnerContainer, "Winner", "", 32);

            // Answer detail (e.g. "The Cryptid was at (1, -2, 1)")
            var detailContainer = UIFactory.CreatePanel(center, "DetailContainer");
            detailContainer.sizeDelta = new Vector2(440f, 30f);
            _detailText = UIFactory.CreateTMP(detailContainer, "Detail",
                "", 18, color: new Color(0.7f, 0.7f, 0.7f));

            // Restart button
            _restartBtn = UIFactory.CreateButton(center, "RestartBtn", L.Get("play_again"),
                220, 50, new Color(0.18f, 0.55f, 0.34f), 24);
            _restartBtn.onClick.AddListener(() => OnRestartClicked?.Invoke());

            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Show / Hide
        // ---------------------------------------------------------

        /// <summary>Shows the panel with the winner info.</summary>
        public void Show(int winnerIndex, string answerInfo)
        {
            var color = UIFactory.GetPlayerColor(winnerIndex);
            _winnerText.text = L.Format("player_wins", winnerIndex + 1);
            _winnerText.color = color;
            _detailText.text = answerInfo;
            UIAnimator.ShowPanel(gameObject);
        }

        /// <summary>Hides the panel.</summary>
        public void Hide()
        {
            UIAnimator.HidePanel(gameObject);
        }
    }
}
