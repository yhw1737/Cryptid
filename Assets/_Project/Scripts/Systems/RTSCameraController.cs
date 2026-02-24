using Cryptid.Core;
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
    ///   - Middle Mouse Button + Drag: Orbit (rotate around look-at point)
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
        // Orbit Settings
        // ---------------------------------------------------------

        [Header("Orbit Settings (Middle Mouse)")]
        [Tooltip("Horizontal rotation speed (degrees per pixel)")]
        [SerializeField] private float _orbitSpeedX = 0.3f;

        [Tooltip("Vertical rotation speed (degrees per pixel)")]
        [SerializeField] private float _orbitSpeedY = 0.2f;

        [Tooltip("Minimum vertical angle (degrees from horizon)")]
        [SerializeField] private float _orbitPitchMin = 30f;

        [Tooltip("Maximum vertical angle (degrees from horizon)")]
        [SerializeField] private float _orbitPitchMax = 89f;

        [Tooltip("Smoothing factor for orbit rotation (lower = smoother)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _orbitSmoothing = 0.15f;

        // ---------------------------------------------------------
        // Internal State
        // ---------------------------------------------------------

        private Vector3 _lastMouseWorldPos;
        private Vector3 _targetPosition;
        private float _targetZoom;
        private Camera _cam;
        private bool _isPanning;

        // Orbit state
        private bool _isOrbiting;
        private Vector2 _lastMouseScreenPos;
        private float _targetYaw;
        private float _targetPitch = 45f; // Start at 45° from horizon
        private float _currentYaw;
        private float _currentPitch = 45f;
        private Vector3 _orbitPivot; // Point we orbit around

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

            // Apply projection from settings
            ApplyProjection(SettingsManager.UsePerspective);

            _mouse = Mouse.current;
        }

        private void OnEnable()
        {
            SettingsManager.OnPerspectiveChanged += ApplyProjection;
        }

        private void OnDisable()
        {
            SettingsManager.OnPerspectiveChanged -= ApplyProjection;
        }

        private void ApplyProjection(bool usePerspective)
        {
            if (_cam == null) return;
            _cam.orthographic = !usePerspective;
            if (usePerspective)
            {
                _cam.fieldOfView = 60f;
            }
        }

        private void Start()
        {
            _targetPosition = transform.position;
            _targetZoom = _cam != null && _cam.orthographic
                ? _cam.orthographicSize
                : transform.position.y;

            // Initialize orbit angles from current rotation
            Vector3 euler = transform.eulerAngles;
            _targetYaw = euler.y;
            _currentYaw = euler.y;
            _targetPitch = Mathf.Clamp(euler.x, _orbitPitchMin, _orbitPitchMax);
            _currentPitch = _targetPitch;

            _orbitPivot = GetLookAtPoint();
        }

        private void Update()
        {
            if (_mouse == null)
            {
                _mouse = Mouse.current;
                if (_mouse == null) return;
            }

            HandlePanInput();
            HandleOrbitInput();
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
        // Orbit Logic (Middle Mouse Button)
        // ---------------------------------------------------------

        private void HandleOrbitInput()
        {
            // Start orbiting on middle mouse press
            if (_mouse.middleButton.wasPressedThisFrame)
            {
                _isOrbiting = true;
                _lastMouseScreenPos = _mouse.position.ReadValue();

                // Set orbit pivot to the point the camera is looking at
                _orbitPivot = GetLookAtPoint();
            }

            // Stop orbiting on release
            if (_mouse.middleButton.wasReleasedThisFrame)
            {
                _isOrbiting = false;
            }

            // Calculate rotation delta while dragging
            if (_isOrbiting)
            {
                Vector2 currentScreenPos = _mouse.position.ReadValue();
                Vector2 delta = currentScreenPos - _lastMouseScreenPos;

                _targetYaw -= delta.x * _orbitSpeedX;    // Left/right: reversed
                _targetPitch -= delta.y * _orbitSpeedY;   // Up/down: original direction
                _targetPitch = Mathf.Clamp(_targetPitch, _orbitPitchMin, _orbitPitchMax);

                _lastMouseScreenPos = currentScreenPos;
            }
        }

        /// <summary>
        /// Gets the point the camera is looking at on the ground plane.
        /// </summary>
        private Vector3 GetLookAtPoint()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float distance))
                return ray.GetPoint(distance);

            // Fallback: project straight down
            return new Vector3(transform.position.x, 0f, transform.position.z);
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
            // Smoothly interpolate orbit angles
            _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, _orbitSmoothing);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, _orbitSmoothing);

            // Calculate camera position from orbit angles + zoom distance
            float pitchRad = _currentPitch * Mathf.Deg2Rad;
            float yawRad = _currentYaw * Mathf.Deg2Rad;

            // Orbit distance derived from zoom
            float orbitDist = _targetZoom;
            if (_cam != null && _cam.orthographic)
            {
                // In orthographic mode, actual distance doesn't matter for rendering,
                // but we still use it for the camera position offset
                orbitDist = Mathf.Max(20f, _targetZoom);
            }

            // Smoothly interpolate pan target
            Vector3 smoothTarget = Vector3.Lerp(
                _orbitPivot,
                new Vector3(_targetPosition.x, 0f, _targetPosition.z),
                _panSmoothing);
            _orbitPivot = smoothTarget;

            // Camera offset from pivot
            Vector3 offset = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                -Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            ) * orbitDist;

            transform.position = _orbitPivot + offset;
            transform.LookAt(_orbitPivot);

            // Smoothly interpolate orthographic size (zoom)
            if (_cam != null && _cam.orthographic)
            {
                _cam.orthographicSize = Mathf.Lerp(
                    _cam.orthographicSize, _targetZoom, _zoomSmoothing);
            }
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
            _targetPosition = new Vector3(worldPosition.x, 0f, worldPosition.z);
            _orbitPivot = _targetPosition;
        }

        /// <summary>
        /// Centers the camera on the map by computing the bounding center
        /// of all tile positions and adjusting zoom to fit.
        /// </summary>
        public void CenterOnMap(System.Collections.Generic.Dictionary<Core.HexCoordinates, Data.WorldTile> worldMap)
        {
            if (worldMap == null || worldMap.Count == 0) return;

            Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, 0f, float.MinValue);

            foreach (var kvp in worldMap)
            {
                Vector3 pos = Core.HexMetrics.HexToWorldPosition(kvp.Key);
                if (pos.x < min.x) min.x = pos.x;
                if (pos.z < min.z) min.z = pos.z;
                if (pos.x > max.x) max.x = pos.x;
                if (pos.z > max.z) max.z = pos.z;
            }

            Vector3 center = (min + max) * 0.5f;
            float mapWidth = max.x - min.x;
            float mapHeight = max.z - min.z;
            float extent = Mathf.Max(mapWidth, mapHeight);

            // Set zoom to fit the map with some margin
            _targetZoom = Mathf.Clamp(extent * 0.7f, _zoomMin, _zoomMax);
            _targetPosition = new Vector3(center.x, 0f, center.z);
            _orbitPivot = _targetPosition;

            if (_cam != null && _cam.orthographic)
                _cam.orthographicSize = _targetZoom;

            Debug.Log($"[RTSCamera] Centered on map. Center: {center}, Zoom: {_targetZoom:F1}");
        }

        /// <summary>
        /// Resets camera to origin with default zoom.
        /// </summary>
        [ContextMenu("Reset Camera")]
        public void ResetCamera()
        {
            _targetPosition = Vector3.zero;
            _orbitPivot = Vector3.zero;
            _targetZoom = (_zoomMin + _zoomMax) * 0.5f;
            _targetYaw = 0f;
            _targetPitch = 45f;
        }
    }
}
