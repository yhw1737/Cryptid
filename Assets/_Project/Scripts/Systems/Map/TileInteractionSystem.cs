using System;
using Cryptid.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace Cryptid.Systems.Map
{
    /// <summary>
    /// Handles mouse interaction with hex tiles.
    /// Performs raycasting from the camera to detect tile hover and click.
    /// 
    /// Features:
    ///   - Hover highlight (visual feedback on mouse-over)
    ///   - Left-click selection
    ///   - Info panel showing selected tile data
    /// 
    /// Uses New Input System for mouse input.
    /// </summary>
    public class TileInteractionSystem : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _tileLayerMask = ~0; // Default: all layers

        [Header("Info Display")]
        [Tooltip("Optional TMP text to show hovered tile info")]
        [SerializeField] private TextMeshProUGUI _hoverInfoText;
        [Tooltip("Optional TMP text to show selected tile info")]
        [SerializeField] private TextMeshProUGUI _selectedInfoText;

        // ---------------------------------------------------------
        // Events (for decoupled UI/Logic)
        // ---------------------------------------------------------

        /// <summary> Fired when a tile is hovered. Null when no tile is under cursor. </summary>
        public event Action<HexTile> OnTileHovered;

        /// <summary> Fired when a tile is left-clicked. </summary>
        public event Action<HexTile> OnTileSelected;

        // ---------------------------------------------------------
        // State
        // ---------------------------------------------------------

        private HexTile _currentHovered;
        private HexTile _currentSelected;
        private Mouse _mouse;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            if (_camera == null)
                _camera = Camera.main;

            _mouse = Mouse.current;
        }

        private void Update()
        {
            if (_mouse == null)
            {
                _mouse = Mouse.current;
                if (_mouse == null) return;
            }

            HandleHover();
            HandleClick();
        }

        // ---------------------------------------------------------
        // Hover Logic
        // ---------------------------------------------------------

        private void HandleHover()
        {
            Vector2 mousePos = _mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, _tileLayerMask))
            {
                // Try to get HexTile from hit object
                if (hit.collider.TryGetComponent<HexTile>(out var tile))
                {
                    if (tile != _currentHovered)
                    {
                        // Unhighlight previous
                        _currentHovered?.SetHighlight(false);

                        // Highlight new
                        _currentHovered = tile;
                        _currentHovered.SetHighlight(true);

                        OnTileHovered?.Invoke(_currentHovered);
                        UpdateHoverInfo(_currentHovered);
                    }
                    return;
                }
            }

            // No tile under cursor
            if (_currentHovered != null)
            {
                _currentHovered.SetHighlight(false);
                _currentHovered = null;
                OnTileHovered?.Invoke(null);
                UpdateHoverInfo(null);
            }
        }

        // ---------------------------------------------------------
        // Click Logic (Left Mouse Button)
        // ---------------------------------------------------------

        private void HandleClick()
        {
            if (_mouse.leftButton.wasPressedThisFrame)
            {
                if (_currentHovered != null)
                {
                    // Deselect previous
                    _currentSelected?.SetSelected(false);

                    // Select new (or toggle if same)
                    if (_currentSelected == _currentHovered)
                    {
                        _currentSelected = null;
                        UpdateSelectedInfo(null);
                    }
                    else
                    {
                        _currentSelected = _currentHovered;
                        _currentSelected.SetSelected(true);
                        UpdateSelectedInfo(_currentSelected);
                    }

                    OnTileSelected?.Invoke(_currentSelected);
                }
                else
                {
                    // Clicked empty space — deselect
                    _currentSelected?.SetSelected(false);
                    _currentSelected = null;
                    OnTileSelected?.Invoke(null);
                    UpdateSelectedInfo(null);
                }
            }
        }

        // ---------------------------------------------------------
        // Info Display
        // ---------------------------------------------------------

        private void UpdateHoverInfo(HexTile tile)
        {
            if (_hoverInfoText == null) return;

            if (tile == null)
            {
                _hoverInfoText.text = "";
                return;
            }

            var data = tile.TileData;
            _hoverInfoText.text =
                $"Hover: {data.Coordinates} | {data.Terrain}" +
                (data.Structure != Data.StructureType.None ? $" | {data.Structure}" : "") +
                (data.Animal != Data.AnimalType.None ? $" | {data.Animal}" : "");
        }

        private void UpdateSelectedInfo(HexTile tile)
        {
            if (_selectedInfoText == null) return;

            if (tile == null)
            {
                _selectedInfoText.text = "No tile selected";
                return;
            }

            var data = tile.TileData;
            _selectedInfoText.text =
                $"Selected: {data.Coordinates}\n" +
                $"Terrain: {data.Terrain}\n" +
                $"Structure: {data.Structure}\n" +
                $"Animal: {data.Animal}\n" +
                $"Piece: {data.SourcePieceId}";
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary> Returns the currently hovered tile, or null. </summary>
        public HexTile GetHoveredTile() => _currentHovered;

        /// <summary> Returns the currently selected tile, or null. </summary>
        public HexTile GetSelectedTile() => _currentSelected;

        /// <summary> Clears the current selection. </summary>
        public void ClearSelection()
        {
            _currentSelected?.SetSelected(false);
            _currentSelected = null;
            UpdateSelectedInfo(null);
        }
    }
}
