using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Static factory for creating common UI elements programmatically.
    /// Provides consistent styling across all game UI panels.
    /// No prefabs or manual setup required — just call these helpers.
    /// </summary>
    public static class UIFactory
    {
        // ---------------------------------------------------------
        // Korean Font (lazy-loaded from Resources)
        // ---------------------------------------------------------

        private static TMP_FontAsset _koreanFont;
        private static bool _fontLoadAttempted;

        /// <summary>
        /// Returns the Korean TMP font asset (NotoSansKR).
        /// Loaded from Resources/Fonts/NotoSansKR-Regular SDF.
        /// Generate it via Tools → Cryptid → Generate Korean Font Asset.
        /// </summary>
        public static TMP_FontAsset KoreanFont
        {
            get
            {
                // Re-load if previously loaded asset was destroyed (e.g. domain reload)
                if (_fontLoadAttempted && _koreanFont == null)
                    _fontLoadAttempted = false;

                if (!_fontLoadAttempted)
                {
                    _fontLoadAttempted = true;
                    _koreanFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansKR-Regular SDF");
                    if (_koreanFont == null)
                        Debug.LogWarning("[UIFactory] Korean font not found at Resources/Fonts/. " +
                                         "Run Tools \u2192 Cryptid \u2192 Generate Korean Font Asset in the editor.");
                }

                // Extra safety: verify the atlas texture is alive
                if (_koreanFont != null && _koreanFont.atlasTexture == null)
                {
                    Debug.LogWarning("[UIFactory] Korean font atlas texture is missing. " +
                        "Re-generate via Tools \u2192 Cryptid \u2192 Generate Korean Font Asset.");
                    _koreanFont = null;
                }

                return _koreanFont;
            }
        }

        // ---------------------------------------------------------
        // Player Colors (0-indexed, wraps around)
        // ---------------------------------------------------------

        public static readonly Color[] PlayerColors =
        {
            new Color(0.20f, 0.60f, 0.86f), // Player 1: Blue
            new Color(0.91f, 0.30f, 0.24f), // Player 2: Red
            new Color(0.18f, 0.80f, 0.44f), // Player 3: Green
            new Color(0.95f, 0.77f, 0.06f), // Player 4: Yellow
            new Color(0.61f, 0.35f, 0.71f), // Player 5: Purple
        };

        // ---------------------------------------------------------
        // Shared UI Palette
        // ---------------------------------------------------------

        public static readonly Color PanelBg      = new Color(0.08f, 0.08f, 0.12f, 0.85f);
        public static readonly Color ButtonNormal  = new Color(0.17f, 0.24f, 0.31f, 1f);
        public static readonly Color ButtonHover   = new Color(0.22f, 0.31f, 0.40f, 1f);
        public static readonly Color ButtonPressed = new Color(0.95f, 0.61f, 0.07f, 1f);
        public static readonly Color Accent        = new Color(0.95f, 0.61f, 0.07f, 1f);

        /// <summary>Returns the colour assigned to a player index (wraps around).</summary>
        public static Color GetPlayerColor(int index) =>
            PlayerColors[Mathf.Abs(index) % PlayerColors.Length];

        // ---------------------------------------------------------
        // Canvas
        // ---------------------------------------------------------

        /// <summary>
        /// Creates a ScreenSpace-Overlay Canvas with CanvasScaler and GraphicRaycaster.
        /// Also ensures an EventSystem exists in the scene (required for UI clicks).
        /// </summary>
        public static Canvas CreateScreenCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            // Ensure an EventSystem exists (required for UI interaction)
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            return canvas;
        }

        // ---------------------------------------------------------
        // Panels & Containers
        // ---------------------------------------------------------

        /// <summary>
        /// Creates a RectTransform child with an optional Image background.
        /// Anchors/size must be configured by the caller.
        /// </summary>
        public static RectTransform CreatePanel(Transform parent, string name, Color? bg = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();

            if (bg.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = bg.Value;
            }

            return rt;
        }

        // ---------------------------------------------------------
        // Text
        // ---------------------------------------------------------

        /// <summary>
        /// Creates a TextMeshProUGUI element that stretches to fill its parent by default.
        /// </summary>
        public static TextMeshProUGUI CreateTMP(
            Transform parent, string name, string text,
            float fontSize = 24,
            TextAlignmentOptions align = TextAlignmentOptions.Center,
            Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (KoreanFont != null)
                tmp.font = KoreanFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color ?? Color.white;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            return tmp;
        }

        // ---------------------------------------------------------
        // Buttons
        // ---------------------------------------------------------

        /// <summary>
        /// Creates a UI Button with a TMP label child.
        /// Size is determined by sizeDelta (w × h).
        /// </summary>
        public static Button CreateButton(
            Transform parent, string name, string label,
            float w = 200, float h = 50,
            Color? bg = null, float fontSize = 22)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);

            var img = go.AddComponent<Image>();
            img.color = bg ?? ButtonNormal;

            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor      = bg ?? ButtonNormal;
            cb.highlightedColor = ButtonHover;
            cb.pressedColor     = ButtonPressed;
            cb.selectedColor    = bg ?? ButtonNormal;
            cb.fadeDuration     = 0.1f;
            btn.colors = cb;

            // Child text label (non-raycastable so click goes to button)
            var labelTmp = CreateTMP(go.transform, "Label", label, fontSize);
            labelTmp.raycastTarget = false;

            // Add hover/click animations
            UIAnimator.AddButtonAnimations(btn);

            return btn;
        }

        /// <summary>
        /// Creates a UI Button with a Sprite icon instead of text.
        /// The icon fills the button area with preserved aspect ratio.
        /// </summary>
        public static Button CreateImageButton(
            Transform parent, string name, Sprite icon,
            float w = 46, float h = 46,
            Color? bg = null, Color? tint = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);

            var bgImg = go.AddComponent<Image>();
            bgImg.color = bg ?? ButtonNormal;

            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor      = bg ?? ButtonNormal;
            cb.highlightedColor = ButtonHover;
            cb.pressedColor     = ButtonPressed;
            cb.selectedColor    = bg ?? ButtonNormal;
            cb.fadeDuration     = 0.1f;
            btn.colors = cb;

            // Child image for the icon (non-raycastable so click goes to button)
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(go.transform, false);

                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.1f, 0.1f);
                iconRt.anchorMax = new Vector2(0.9f, 0.9f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;

                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.color = tint ?? Color.white;
                iconImg.raycastTarget = false;
            }

            // Add hover/click animations
            UIAnimator.AddButtonAnimations(btn);

            return btn;
        }

        // ---------------------------------------------------------
        // Layout Helpers
        // ---------------------------------------------------------

        /// <summary>Adds a HorizontalLayoutGroup to a RectTransform.</summary>
        public static HorizontalLayoutGroup AddHorizontalLayout(
            RectTransform rt, float spacing = 10,
            RectOffset padding = null,
            TextAnchor childAlignment = TextAnchor.MiddleCenter)
        {
            var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.padding = padding ?? new RectOffset(10, 10, 5, 5);
            hlg.childAlignment = childAlignment;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            return hlg;
        }

        /// <summary>Adds a VerticalLayoutGroup to a RectTransform.</summary>
        public static VerticalLayoutGroup AddVerticalLayout(
            RectTransform rt, float spacing = 10,
            RectOffset padding = null,
            TextAnchor childAlignment = TextAnchor.MiddleCenter)
        {
            var vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = padding ?? new RectOffset(10, 10, 10, 10);
            vlg.childAlignment = childAlignment;
            vlg.childControlWidth      = false;
            vlg.childControlHeight     = false;
            vlg.childForceExpandWidth  = false;
            vlg.childForceExpandHeight = false;
            return vlg;
        }
    }
}
