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
    /// Selection is shown via a floating green square pyramid above the tile
    /// that bobs up and down using a sine wave motion.
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
        private bool _isDimmed;

        // Selection indicator: floating green square pyramid
        private GameObject _selectIndicator;
        private static readonly Color IndicatorColor = new Color(0.2f, 0.85f, 0.3f, 0.9f);
        private const float INDICATOR_BASE_HEIGHT = 1.5f;
        private const float INDICATOR_BOB_AMPLITUDE = 0.25f;
        private const float INDICATOR_BOB_SPEED = 2f;
        private const float INDICATOR_SCALE = 0.3f;
        private const float INDICATOR_ROTATE_SPEED = 30f;

        private static readonly Color HighlightTint = new Color(1f, 1f, 1f, 1f) * 1.3f;
        private const float DIM_FACTOR = 0.35f;

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
        /// Brightens the base color slightly.
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;
            _isHighlighted = highlighted;
            UpdateVisual();
        }

        /// <summary>
        /// Selects this tile (mouse click).
        /// Shows a floating green square pyramid indicator above the tile.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;

            if (_isSelected)
                CreateIndicator();
            else
                DestroyIndicator();

            UpdateVisual();
        }

        /// <summary>
        /// Dims this tile to indicate it is not a valid placement target.
        /// Used during PenaltyPlacement phase on non-placeable tiles.
        /// </summary>
        public void SetDimmed(bool dimmed)
        {
            if (_isDimmed == dimmed) return;
            _isDimmed = dimmed;
            UpdateVisual();
        }

        private void Update()
        {
            if (_selectIndicator != null)
            {
                // Sine-wave bobbing
                float y = INDICATOR_BASE_HEIGHT +
                           Mathf.Sin(Time.time * INDICATOR_BOB_SPEED) * INDICATOR_BOB_AMPLITUDE;
                _selectIndicator.transform.localPosition = new Vector3(0f, y, 0f);

                // Slow rotation
                _selectIndicator.transform.Rotate(Vector3.up, INDICATOR_ROTATE_SPEED * Time.deltaTime, Space.Self);
            }
        }

        private void CreateIndicator()
        {
            if (_selectIndicator != null) return;

            // Create a square pyramid indicator above the tile
            _selectIndicator = new GameObject("SelectIndicator");
            _selectIndicator.transform.SetParent(transform, false);
            _selectIndicator.transform.localPosition = new Vector3(0f, INDICATOR_BASE_HEIGHT, 0f);
            _selectIndicator.transform.localScale = Vector3.one * INDICATOR_SCALE;

            var mf = _selectIndicator.AddComponent<MeshFilter>();
            mf.mesh = CreateSquarePyramidMesh();

            var mr = _selectIndicator.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = IndicatorColor;
            mat.SetFloat("_Smoothness", 0.8f);
            mr.material = mat;
        }

        private void DestroyIndicator()
        {
            if (_selectIndicator != null)
            {
                Destroy(_selectIndicator);
                _selectIndicator = null;
            }
        }

        /// <summary>Creates a square pyramid mesh (4 triangular sides + 1 square base).</summary>
        private static Mesh CreateSquarePyramidMesh()
        {
            // 5 vertices: 4 base corners + 1 apex
            Vector3[] verts =
            {
                new(-1f, 0f, -1f), // 0: base front-left
                new( 1f, 0f, -1f), // 1: base front-right
                new( 1f, 0f,  1f), // 2: base back-right
                new(-1f, 0f,  1f), // 3: base back-left
                new( 0f, 1.5f, 0f) // 4: apex
            };

            // 6 triangles: 4 sides + 2 for the square base
            int[] triangles =
            {
                // Front face
                0, 4, 1,
                // Right face
                1, 4, 2,
                // Back face
                2, 4, 3,
                // Left face
                3, 4, 0,
                // Base (two triangles, facing down)
                0, 1, 2,
                0, 2, 3
            };

            var mesh = new Mesh { name = "SquarePyramid" };
            mesh.vertices = verts;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void UpdateVisual()
        {
            if (_baseMaterial == null) return;

            if (_isHighlighted && !_isSelected)
            {
                _baseMaterial.color = _baseColor * HighlightTint;
            }
            else if (_isDimmed)
            {
                _baseMaterial.color = _baseColor * DIM_FACTOR;
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
            _isDimmed = false;
            DestroyIndicator();
            if (_baseMaterial != null)
                _baseMaterial.color = _baseColor;
        }

        private void OnDestroy()
        {
            DestroyIndicator();
        }
    }
}
