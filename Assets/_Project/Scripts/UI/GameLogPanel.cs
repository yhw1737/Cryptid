using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Right-side scrolling log panel that records all game actions.
    /// Shows questions, responses, searches, and penalties in chronological order.
    /// Each entry is colour-coded by the acting player.
    /// Created and managed by GameUIManager.
    /// </summary>
    public class GameLogPanel : MonoBehaviour
    {
        private ScrollRect _scrollRect;
        private RectTransform _contentRoot;
        private readonly List<GameObject> _entries = new();

        private const int MAX_ENTRIES = 100;
        private const float ENTRY_HEIGHT = 28f;
        private const float PANEL_WIDTH = 340f;

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

            // Header label
            var headerRt = UIFactory.CreatePanel(root, "LogHeader");
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 30f);
            var headerTmp = UIFactory.CreateTMP(headerRt, "HeaderText",
                "GAME LOG", 14, TextAlignmentOptions.Center,
                new Color(0.5f, 0.5f, 0.5f));

            // ScrollRect viewport
            var viewport = UIFactory.CreatePanel(root, "Viewport");
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.sizeDelta = new Vector2(-10f, -35f);
            viewport.anchoredPosition = new Vector2(0f, -15f);
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
    }
}
