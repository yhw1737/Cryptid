using UnityEngine;
using UnityEngine.InputSystem;

namespace Cryptid.Systems
{
    /// <summary>
    /// RTS-style camera controller for top-down hex map viewing.
    /// Uses the New Input System.
    /// 
    /// Controls:
    ///   - Right Mouse Button + Drag: Pan the camera
    ///   - Scroll Wheel: Zoom in/out
    /// 
    /// The camera looks straight down at the XZ plane.
    /// Panning moves on the XZ plane; zooming adjusts Y height.
    /// </summary>
    public class RTSCameraController : MonoBehaviour
    {
        // ---------------------------------------------------------
        // Pan Settings
        // ---------------------------------------------------------

        [Header("Pan Settings")]
        [Tooltip("How fast the camera pans relative to mouse movement")]
        [SerializeField] private float _panSpeed = 0.5f;

        [Tooltip("Smoothing factor for pan movement (lower = smoother)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _panSmoothing = 0.15f;

        // ---------------------------------------------------------
        // Zoom Settings
        // ---------------------------------------------------------

        [Header("Zoom Settings")]
        [Tooltip("How fast the camera zooms per scroll tick")]
        [SerializeField] private float _zoomSpeed = 5f;

        [Tooltip("Minimum camera height (closest zoom)")]
        [SerializeField] private float _zoomMin = 3f;

        [Tooltip("Maximum camera height (farthest zoom)")]
        [SerializeField] private float _zoomMax = 50f;

        [Tooltip("Smoothing factor for zoom (lower = smoother)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _zoomSmoothing = 0.1f;

        // ---------------------------------------------------------
        // Internal State
        // ---------------------------------------------------------

        private Vector3 _lastMouseWorldPos;
        private Vector3 _targetPosition;
        private float _targetZoom;
        private Camera _cam;
        private bool _isPanning;

        // Input System references
        private Mouse _mouse;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null)
                _cam = Camera.main;

            _mouse = Mouse.current;
        }

        private void Start()
        {
            _targetPosition = transform.position;
            _targetZoom = transform.position.y;
        }

        private void Update()
        {
            if (_mouse == null)
            {
                _mouse = Mouse.current;
                if (_mouse == null) return;
            }

            HandlePanInput();
            HandleZoomInput();
            ApplyMovement();
        }

        // ---------------------------------------------------------
        // Pan Logic (Right Mouse Button)
        // ---------------------------------------------------------

        private void HandlePanInput()
        {
            // Start panning on right mouse button press
            if (_mouse.rightButton.wasPressedThisFrame)
            {
                _isPanning = true;
                _lastMouseWorldPos = GetMouseWorldPosition();
            }

            // Stop panning on right mouse button release
            if (_mouse.rightButton.wasReleasedThisFrame)
            {
                _isPanning = false;
            }

            // Calculate pan delta while dragging
            if (_isPanning)
            {
                Vector3 currentMouseWorldPos = GetMouseWorldPosition();
                Vector3 delta = _lastMouseWorldPos - currentMouseWorldPos;

                // Apply delta to target position (XZ only)
                _targetPosition += new Vector3(delta.x, 0f, delta.z);

                // Update last position for next frame
                _lastMouseWorldPos = GetMouseWorldPosition();
            }
        }

        // ---------------------------------------------------------
        // Zoom Logic (Scroll Wheel)
        // ---------------------------------------------------------

        private void HandleZoomInput()
        {
            // Input System returns scroll delta in pixels (typically ±120 per notch)
            Vector2 scrollDelta = _mouse.scroll.ReadValue();
            float scrollInput = scrollDelta.y;

            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                // Normalize: scroll values can be large (120 per notch), scale down
                float normalizedScroll = Mathf.Sign(scrollInput);
                _targetZoom -= normalizedScroll * _zoomSpeed;
                _targetZoom = Mathf.Clamp(_targetZoom, _zoomMin, _zoomMax);
            }
        }

        // ---------------------------------------------------------
        // Apply Smoothed Movement
        // ---------------------------------------------------------

        private void ApplyMovement()
        {
            // Smoothly interpolate position (XZ pan)
            Vector3 smoothedPos = Vector3.Lerp(
                transform.position,
                new Vector3(_targetPosition.x, _targetZoom, _targetPosition.z),
                _panSmoothing);

            // Smoothly interpolate zoom (Y height)
            float smoothedY = Mathf.Lerp(transform.position.y, _targetZoom, _zoomSmoothing);

            transform.position = new Vector3(smoothedPos.x, smoothedY, smoothedPos.z);
        }

        // ---------------------------------------------------------
        // Utility
        // ---------------------------------------------------------

        /// <summary>
        /// Casts a ray from the mouse position to the XZ ground plane (Y=0)
        /// and returns the world-space intersection point.
        /// </summary>
        private Vector3 GetMouseWorldPosition()
        {
            Vector2 mouseScreenPos = _mouse.position.ReadValue();
            Ray ray = _cam.ScreenPointToRay(mouseScreenPos);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }

            return transform.position;
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Instantly moves the camera to look at the given world position.
        /// </summary>
        public void FocusOn(Vector3 worldPosition)
        {
            _targetPosition = new Vector3(worldPosition.x, _targetZoom, worldPosition.z);
            transform.position = _targetPosition;
        }

        /// <summary>
        /// Resets camera to origin with default zoom.
        /// </summary>
        [ContextMenu("Reset Camera")]
        public void ResetCamera()
        {
            _targetPosition = Vector3.zero;
            _targetZoom = (_zoomMin + _zoomMax) * 0.5f;
        }
    }
}
