using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Displays player name tabs at the top of the screen (below TurnIndicatorPanel).
    /// Each player gets a colored tab showing their nickname.
    /// The local player's tab is highlighted with accent styling.
    /// The active turn player's tab is visually emphasized.
    /// </summary>
    public class PlayerHUDPanel : MonoBehaviour
    {
        // ---------------------------------------------------------
        // Per-player entry
        // ---------------------------------------------------------

        private struct PlayerEntry
        {
            public GameObject Root;
            public Image Background;
            public Image ColorBar;
            public TextMeshProUGUI NameText;
            public Outline Outline;
        }

        private PlayerEntry[] _entries;
        private int _localPlayerIndex = -1;
        private int _activePlayerIndex = -1;
        private int _playerCount;

        // ---------------------------------------------------------
        // Colours
        // ---------------------------------------------------------

        private static readonly Color NormalBg    = new(0.12f, 0.12f, 0.16f, 0.9f);
        private static readonly Color SelfBg      = new(0.18f, 0.22f, 0.30f, 0.95f);
        private static readonly Color ActiveBg    = new(0.22f, 0.28f, 0.38f, 0.95f);
        private static readonly Color SelfActiveBg = new(0.28f, 0.34f, 0.46f, 1f);

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>
        /// Builds the panel elements. Called once by GameUIManager.
        /// </summary>
        public void Build(RectTransform root)
        {
            // Position below TurnIndicatorPanel (60px + 5px gap)
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, 50f);
            root.anchoredPosition = new Vector2(0f, -65f);

            // Root background transparent so entries stand out
            var rootBg = root.GetComponent<Image>();
            if (rootBg != null) rootBg.color = Color.clear;

            UIFactory.AddHorizontalLayout(root, spacing: 6,
                padding: new RectOffset(20, 20, 4, 4),
                childAlignment: TextAnchor.MiddleCenter);

            // Pre-create 5 entries (max players)
            _entries = new PlayerEntry[5];
            for (int i = 0; i < 5; i++)
            {
                _entries[i] = CreateEntry(root, i);
                _entries[i].Root.SetActive(false);
            }
        }

        private PlayerEntry CreateEntry(RectTransform parent, int index)
        {
            var entry = new PlayerEntry();

            var go = new GameObject($"Player_{index}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140f, 42f);

            // Background
            entry.Background = go.AddComponent<Image>();
            entry.Background.color = NormalBg;

            // Outline (for self highlight)
            entry.Outline = go.AddComponent<Outline>();
            entry.Outline.effectColor = UIFactory.Accent;
            entry.Outline.effectDistance = new Vector2(2, 2);
            entry.Outline.enabled = false;

            // Color bar (left edge)
            var barGo = new GameObject("ColorBar", typeof(RectTransform));
            barGo.transform.SetParent(go.transform, false);
            var barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(0f, 1f);
            barRt.pivot = new Vector2(0f, 0.5f);
            barRt.sizeDelta = new Vector2(5f, 0f);
            barRt.anchoredPosition = Vector2.zero;
            entry.ColorBar = barGo.AddComponent<Image>();
            entry.ColorBar.color = UIFactory.GetPlayerColor(index);

            // Name text
            entry.NameText = UIFactory.CreateTMP(go.transform, "Name",
                $"Player {index + 1}", fontSize: 16,
                align: TextAlignmentOptions.MidlineCenter);
            entry.NameText.margin = new Vector4(12, 0, 4, 0);
            entry.NameText.raycastTarget = false;

            entry.Root = go;
            return entry;
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Initialises the HUD with player names.
        /// </summary>
        /// <param name="names">Array of player nicknames.</param>
        /// <param name="localPlayerIndex">Index of the local player (-1 for no highlight).</param>
        public void SetupPlayers(string[] names, int localPlayerIndex)
        {
            _playerCount = Mathf.Min(names.Length, 5);
            _localPlayerIndex = localPlayerIndex;

            for (int i = 0; i < 5; i++)
            {
                if (i < _playerCount)
                {
                    _entries[i].Root.SetActive(true);
                    _entries[i].NameText.text = names[i];
                    _entries[i].ColorBar.color = UIFactory.GetPlayerColor(i);

                    bool isSelf = (i == _localPlayerIndex);
                    _entries[i].Outline.enabled = isSelf;
                    _entries[i].Background.color = isSelf ? SelfBg : NormalBg;
                }
                else
                {
                    _entries[i].Root.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Updates the visual emphasis to show whose turn it is.
        /// </summary>
        public void UpdateActiveTurn(int playerIndex)
        {
            _activePlayerIndex = playerIndex;
            RefreshVisuals();
        }

        // ---------------------------------------------------------
        // Visual Refresh
        // ---------------------------------------------------------

        private void RefreshVisuals()
        {
            for (int i = 0; i < _playerCount; i++)
            {
                bool isSelf = (i == _localPlayerIndex);
                bool isActive = (i == _activePlayerIndex);

                // Background
                if (isSelf && isActive)
                    _entries[i].Background.color = SelfActiveBg;
                else if (isActive)
                    _entries[i].Background.color = ActiveBg;
                else if (isSelf)
                    _entries[i].Background.color = SelfBg;
                else
                    _entries[i].Background.color = NormalBg;

                // Outline
                if (isSelf)
                {
                    _entries[i].Outline.enabled = true;
                    _entries[i].Outline.effectColor = isActive
                        ? Color.white : UIFactory.Accent;
                }
                else if (isActive)
                {
                    _entries[i].Outline.enabled = true;
                    _entries[i].Outline.effectColor = UIFactory.GetPlayerColor(i);
                }
                else
                {
                    _entries[i].Outline.enabled = false;
                }

                // Text colour
                _entries[i].NameText.color = isActive
                    ? Color.white
                    : new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
