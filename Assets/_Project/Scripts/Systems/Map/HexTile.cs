using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;

namespace Cryptid.Systems.Map
{
    /// <summary>
    /// Component attached to each spawned hex tile GameObject.
    /// Stores runtime data (coordinates, terrain, etc.) and handles
    /// visual state changes (highlight, select).
    /// 
    /// Created by MapGenerator during tile spawning.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class HexTile : MonoBehaviour
    {
        // ---------------------------------------------------------
        // Data
        // ---------------------------------------------------------

        /// <summary> Global cube coordinates of this tile. </summary>
        public HexCoordinates Coordinates { get; private set; }

        /// <summary> Full world tile data. </summary>
        public WorldTile TileData { get; private set; }

        // ---------------------------------------------------------
        // Visual State
        // ---------------------------------------------------------

        private MeshRenderer _renderer;
        private Material _baseMaterial;
        private Color _baseColor;
        private bool _isHighlighted;
        private bool _isSelected;

        [Header("Highlight Settings")]
        private static readonly Color HighlightTint = new Color(1f, 1f, 1f, 1f) * 1.4f;
        private static readonly Color SelectColor = new Color(1f, 0.9f, 0.3f);

        // ---------------------------------------------------------
        // Initialization
        // ---------------------------------------------------------

        /// <summary>
        /// Called by MapGenerator after instantiation to inject tile data.
        /// </summary>
        public void Initialize(WorldTile worldTile)
        {
            TileData = worldTile;
            Coordinates = worldTile.Coordinates;

            _renderer = GetComponent<MeshRenderer>();
            if (_renderer != null)
            {
                _baseMaterial = _renderer.material;
                _baseColor = _baseMaterial.color;
            }
        }

        // ---------------------------------------------------------
        // Visual Feedback
        // ---------------------------------------------------------

        /// <summary>
        /// Highlights this tile (mouse hover).
        /// Brightens the base color.
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;
            _isHighlighted = highlighted;
            UpdateVisual();
        }

        /// <summary>
        /// Selects this tile (mouse click).
        /// Applies a distinct selection color.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_baseMaterial == null) return;

            if (_isSelected)
            {
                _baseMaterial.color = SelectColor;
            }
            else if (_isHighlighted)
            {
                _baseMaterial.color = _baseColor * HighlightTint;
            }
            else
            {
                _baseMaterial.color = _baseColor;
            }
        }

        /// <summary>
        /// Resets all visual states to default.
        /// </summary>
        public void ResetVisual()
        {
            _isHighlighted = false;
            _isSelected = false;
            if (_baseMaterial != null)
                _baseMaterial.color = _baseColor;
        }
    }
}
