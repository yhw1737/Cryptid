using System;
using Cryptid.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Full-screen overlay panel shown when a critical error occurs.
    /// Displays the error message and a "Restart" button.
    /// Created and managed by GameUIManager.
    /// </summary>
    public class ErrorPanel : MonoBehaviour
    {
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _messageText;
        private Button _restartBtn;

        /// <summary>Fired when the player clicks "Restart".</summary>
        public event Action OnRestartClicked;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>Builds the error overlay. Called once by GameUIManager.</summary>
        public void Build(RectTransform root)
        {
            // Fullscreen overlay
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;

            // Semi-transparent dark background (blocks raycasts to game world)
            var overlay = root.gameObject.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.9f);
            overlay.raycastTarget = true;

            // Centre content box
            var center = UIFactory.CreatePanel(root, "CenterBox",
                new Color(0.15f, 0.05f, 0.05f, 0.95f)); // Darker red tint for errors
            center.anchorMin = new Vector2(0.5f, 0.5f);
            center.anchorMax = new Vector2(0.5f, 0.5f);
            center.pivot = new Vector2(0.5f, 0.5f);
            center.sizeDelta = new Vector2(600f, 350f);

            UIFactory.AddVerticalLayout(center, spacing: 20,
                padding: new RectOffset(40, 40, 40, 40));

            // "ERROR" title
            var titleContainer = UIFactory.CreatePanel(center, "TitleContainer");
            titleContainer.sizeDelta = new Vector2(520f, 60f);
            _titleText = UIFactory.CreateTMP(titleContainer, "Title",
                L.Get("error_title"), 48, color: new Color(1f, 0.3f, 0.3f)); // Red

            // Error message
            var messageContainer = UIFactory.CreatePanel(center, "MessageContainer");
            messageContainer.sizeDelta = new Vector2(520f, 120f);
            _messageText = UIFactory.CreateTMP(messageContainer, "Message", "", 22);
            _messageText.alignment = TextAlignmentOptions.Center;
            _messageText.overflowMode = TextOverflowModes.Ellipsis;

            // Spacer
            var spacer = UIFactory.CreatePanel(center, "Spacer");
            spacer.sizeDelta = new Vector2(520f, 20f);

            // Restart button
            _restartBtn = UIFactory.CreateButton(center, "RestartBtn", L.Get("restart"),
                240, 60, new Color(0.7f, 0.2f, 0.2f), 26); // Dark red button
            _restartBtn.onClick.AddListener(() => OnRestartClicked?.Invoke());

            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Show / Hide
        // ---------------------------------------------------------

        /// <summary>Shows the panel with a custom error message.</summary>
        public void Show(string title, string message)
        {
            _titleText.text = title;
            _messageText.text = message;
            UIAnimator.ShowPanel(gameObject);
        }

        /// <summary>Shows the panel with default error title.</summary>
        public void Show(string message)
        {
            Show(L.Get("error_title"), message);
        }

        /// <summary>Hides the panel.</summary>
        public void Hide()
        {
            UIAnimator.HidePanel(gameObject);
        }
    }
}
