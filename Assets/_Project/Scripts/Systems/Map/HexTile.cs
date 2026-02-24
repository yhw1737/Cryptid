using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;

namespace Cryptid.Systems.Map
{
    /// <summary>
    /// Component attached to each spawned hex tile GameObject.
    /// Stores runtime data (coordinates, terrain, etc.) and handles
    /// visual state changes (highlight, select, outline).
    /// 
    /// Hover shows a white outline ring around the tile.
    /// Selection shows a green outline ring plus a floating inverted pyramid.
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

        // Selection indicator: floating green inverted pyramid
        private GameObject _selectIndicator;
        private static readonly Color IndicatorColor = new Color(0.2f, 0.85f, 0.3f, 0.9f);
        private const float INDICATOR_BASE_HEIGHT = 1.5f;
        private const float INDICATOR_BOB_AMPLITUDE = 0.25f;
        private const float INDICATOR_BOB_SPEED = 2f;
        private const float INDICATOR_SCALE = 0.3f;
        private const float INDICATOR_ROTATE_SPEED = 30f;

        // Outline ring rendered around the tile edge
        private GameObject _outlineRing;
        private MeshRenderer _outlineRenderer;
        private Material _outlineMaterial;
        private static readonly Color OutlineHoverColor  = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color OutlineSelectColor = new Color(0.2f, 0.9f, 0.3f, 0.9f);
        private const float OUTLINE_Y_OFFSET = 0.05f;
        private const float OUTLINE_OUTER_SCALE = 1.08f;
        private const float OUTLINE_INNER_SCALE = 0.92f;

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

            CreateOutlineRing();
        }

        // ---------------------------------------------------------
        // Visual Feedback
        // ---------------------------------------------------------

        /// <summary>
        /// Highlights this tile (mouse hover).
        /// Shows a white outline ring around the tile edge.
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;
            _isHighlighted = highlighted;
            UpdateVisual();
        }

        /// <summary>
        /// Selects this tile (mouse click).
        /// Shows a green outline ring and a floating inverted pyramid indicator.
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
                _selectIndicator.transform.Rotate(Vector3.up,
                    INDICATOR_ROTATE_SPEED * Time.deltaTime, Space.Self);
            }
        }

        // ---------------------------------------------------------
        // Outline Ring
        // ---------------------------------------------------------

        /// <summary>
        /// Creates a hex-shaped outline ring mesh that sits on the tile surface.
        /// Hidden by default, shown in white (hover) or green (select).
        /// </summary>
        private void CreateOutlineRing()
        {
            _outlineRing = new GameObject("OutlineRing");
            _outlineRing.transform.SetParent(transform, false);
            _outlineRing.transform.localPosition = new Vector3(0f, OUTLINE_Y_OFFSET, 0f);

            var mf = _outlineRing.AddComponent<MeshFilter>();
            mf.mesh = CreateHexRingMesh();

            _outlineRenderer = _outlineRing.AddComponent<MeshRenderer>();
            _outlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _outlineMaterial.color = OutlineHoverColor;
            _outlineRenderer.material = _outlineMaterial;
            _outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outlineRenderer.receiveShadows = false;

            _outlineRing.SetActive(false);
        }

        /// <summary>
        /// Generates a flat hexagonal ring mesh (outer hex - inner hex).
        /// The ring width conveys the outline thickness.
        /// </summary>
        private static Mesh CreateHexRingMesh()
        {
            const int segments = 6;
            var verts = new Vector3[segments * 4];
            var tris  = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Deg2Rad * (60f * i - 30f);
                float a1 = Mathf.Deg2Rad * (60f * (i + 1) - 30f);

                // Each quad: outer0, outer1, inner1, inner0
                int vi = i * 4;
                verts[vi + 0] = new Vector3(Mathf.Cos(a0) * OUTLINE_OUTER_SCALE, 0f,
                                            Mathf.Sin(a0) * OUTLINE_OUTER_SCALE);
                verts[vi + 1] = new Vector3(Mathf.Cos(a1) * OUTLINE_OUTER_SCALE, 0f,
                                            Mathf.Sin(a1) * OUTLINE_OUTER_SCALE);
                verts[vi + 2] = new Vector3(Mathf.Cos(a1) * OUTLINE_INNER_SCALE, 0f,
                                            Mathf.Sin(a1) * OUTLINE_INNER_SCALE);
                verts[vi + 3] = new Vector3(Mathf.Cos(a0) * OUTLINE_INNER_SCALE, 0f,
                                            Mathf.Sin(a0) * OUTLINE_INNER_SCALE);

                int ti = i * 6;
                tris[ti + 0] = vi + 0; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 2;
                tris[ti + 3] = vi + 0; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;
            }

            var mesh = new Mesh { name = "HexRing" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------
        // Selection Indicator
        // ---------------------------------------------------------

        private void CreateIndicator()
        {
            if (_selectIndicator != null) return;

            _selectIndicator = new GameObject("SelectIndicator");
            _selectIndicator.transform.SetParent(transform, false);
            _selectIndicator.transform.localPosition = new Vector3(0f, INDICATOR_BASE_HEIGHT, 0f);
            _selectIndicator.transform.localScale = Vector3.one * INDICATOR_SCALE;

            var mf = _selectIndicator.AddComponent<MeshFilter>();
            mf.mesh = CreateInvertedPyramidMesh();

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

        /// <summary>Creates an inverted square pyramid mesh (역사각뿔). Apex points downward.</summary>
        private static Mesh CreateInvertedPyramidMesh()
        {
            Vector3[] verts =
            {
                new(-1f, 0f, -1f), // 0: top front-left
                new( 1f, 0f, -1f), // 1: top front-right
                new( 1f, 0f,  1f), // 2: top back-right
                new(-1f, 0f,  1f), // 3: top back-left
                new( 0f, -1.5f, 0f) // 4: apex (pointing down)
            };

            int[] triangles =
            {
                0, 1, 4,  // Front
                1, 2, 4,  // Right
                2, 3, 4,  // Back
                3, 0, 4,  // Left
                0, 2, 1,  // Top cap
                0, 3, 2
            };

            var mesh = new Mesh { name = "InvertedPyramid" };
            mesh.vertices = verts;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------
        // Visual Update
        // ---------------------------------------------------------

        private void UpdateVisual()
        {
            if (_baseMaterial == null) return;

            // Outline ring visibility and colour
            if (_outlineRing != null)
            {
                bool showOutline = _isHighlighted || _isSelected;
                _outlineRing.SetActive(showOutline);
                if (showOutline && _outlineMaterial != null)
                {
                    _outlineMaterial.color = _isSelected
                        ? OutlineSelectColor : OutlineHoverColor;
                }
            }

            // Tile colour
            if (_isDimmed)
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
            if (_outlineRing != null) _outlineRing.SetActive(false);
            if (_baseMaterial != null) _baseMaterial.color = _baseColor;
        }

        private void OnDestroy()
        {
            DestroyIndicator();
            if (_outlineRing != null) Destroy(_outlineRing);
        }
    }
}
