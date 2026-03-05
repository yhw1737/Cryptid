using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Static helpers for DOTween-based UI animations.
    /// Provides panel show/hide (scale+fade) and button interaction effects.
    /// </summary>
    public static class UIAnimator
    {
        // ---------------------------------------------------------
        // Durations & Easing
        // ---------------------------------------------------------

        public const float PanelShowDuration = 0.25f;
        public const float PanelHideDuration = 0.18f;
        public const float ButtonHoverScale  = 1.06f;
        public const float ButtonHoverDur    = 0.15f;
        public const float ButtonClickPunch  = 0.08f;
        public const float ButtonClickDur    = 0.2f;

        // ---------------------------------------------------------
        // Panel Animations
        // ---------------------------------------------------------

        /// <summary>
        /// Shows a panel with scale-up + fade-in from center.
        /// Sets gameObject active before animating.
        /// </summary>
        public static void ShowPanel(GameObject panel)
        {
            if (panel == null) return;
            panel.SetActive(true);

            var rt = panel.GetComponent<RectTransform>();
            var cg = GetOrAddCanvasGroup(panel);

            DOTween.Kill(rt);
            DOTween.Kill(cg);

            rt.localScale = Vector3.one * 0.85f;
            cg.alpha = 0f;

            rt.DOScale(Vector3.one, PanelShowDuration).SetEase(Ease.OutBack).SetUpdate(true);
            cg.DOFade(1f, PanelShowDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        /// <summary>
        /// Hides a panel with scale-down + fade-out, then deactivates.
        /// </summary>
        public static void HidePanel(GameObject panel)
        {
            if (panel == null || !panel.activeSelf) return;

            var rt = panel.GetComponent<RectTransform>();
            var cg = GetOrAddCanvasGroup(panel);

            DOTween.Kill(rt);
            DOTween.Kill(cg);

            rt.DOScale(Vector3.one * 0.85f, PanelHideDuration).SetEase(Ease.InBack).SetUpdate(true);
            cg.DOFade(0f, PanelHideDuration).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => panel.SetActive(false));
        }

        /// <summary>
        /// Instantly shows panel without animation (for panels that are on by default).
        /// </summary>
        public static void ShowInstant(GameObject panel)
        {
            if (panel == null) return;
            panel.SetActive(true);
            var rt = panel.GetComponent<RectTransform>();
            if (rt != null) rt.localScale = Vector3.one;
            var cg = GetOrAddCanvasGroup(panel);
            cg.alpha = 1f;
        }

        // ---------------------------------------------------------
        // Button Animations
        // ---------------------------------------------------------

        /// <summary>
        /// Adds hover/click animation behaviour to a Button.
        /// Call this after creating the button.
        /// </summary>
        public static void AddButtonAnimations(Button btn)
        {
            if (btn == null) return;
            var animator = btn.gameObject.AddComponent<ButtonAnimator>();
            animator.Initialize(btn);
        }

        // ---------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------

        private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }
    }

    /// <summary>
    /// MonoBehaviour that adds hover scale + click punch animations to a Button.
    /// Attached automatically by UIAnimator.AddButtonAnimations().
    /// </summary>
    public class ButtonAnimator : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private RectTransform _rt;
        private Vector3 _originalScale;

        public void Initialize(Button btn)
        {
            _rt = btn.GetComponent<RectTransform>();
            _originalScale = _rt.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_rt == null) return;
            DOTween.Kill(_rt);
            _rt.DOScale(_originalScale * UIAnimator.ButtonHoverScale,
                UIAnimator.ButtonHoverDur).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_rt == null) return;
            DOTween.Kill(_rt);
            _rt.DOScale(_originalScale, UIAnimator.ButtonHoverDur)
                .SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_rt == null) return;
            _rt.DOPunchScale(Vector3.one * UIAnimator.ButtonClickPunch,
                UIAnimator.ButtonClickDur, 6, 0.5f).SetUpdate(true);
        }
    }
}
