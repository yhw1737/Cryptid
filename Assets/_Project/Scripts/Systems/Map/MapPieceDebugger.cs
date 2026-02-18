using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;
using TMPro;

namespace Cryptid.Systems.Map
{
    /// <summary>
    /// Visual debugger for a single MapPieceData ScriptableObject.
    /// Renders tiles as colored hex outlines (Gizmos) and spawns TMP labels
    /// showing terrain type, structure, and local coordinates.
    /// 
    /// VISIBILITY FIRST: Attach to a GameObject, assign a MapPieceData,
    /// and immediately see the piece layout in Scene/Game View.
    /// </summary>
    public class MapPieceDebugger : MonoBehaviour
    {
        [Header("Data Source")]
        [SerializeField] private MapPieceData _mapPiece;

        [Header("Rotation Preview")]
        [Tooltip("Number of 60° clockwise rotations to apply (0-5)")]
        [Range(0, 5)]
        [SerializeField] private int _rotationSteps;

        [Header("Gizmo Settings")]
        [SerializeField] private bool _useTerrainColors = true;
        [SerializeField] private Color _defaultColor = Color.white;
        [SerializeField] private Color _structureMarkerColor = Color.red;

        [Header("Label Settings")]
        [SerializeField] private bool _showLabelsInPlayMode = true;
        [SerializeField] private int _labelFontSize = 2;
        [SerializeField] private float _labelYOffset = 0.05f;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Start()
        {
            if (_showLabelsInPlayMode && _mapPiece != null)
            {
                SpawnTileLabels();
            }
        }

        // ---------------------------------------------------------
        // Labels (Game View)
        // ---------------------------------------------------------

        /// <summary>
        /// Spawns floating TMP labels for each tile showing terrain + coordinates.
        /// </summary>
        [ContextMenu("Spawn Tile Labels")]
        public void SpawnTileLabels()
        {
            ClearChildren();

            if (_mapPiece == null || _mapPiece.Tiles == null) return;

            foreach (var tile in _mapPiece.Tiles)
            {
                HexCoordinates localHex = HexCoordinates.FromOffset(tile.LocalOffset.x, tile.LocalOffset.y);
                HexCoordinates rotatedHex = localHex.Rotate(_rotationSteps);
                Vector3 worldPos = transform.position + HexMetrics.HexToWorldPosition(rotatedHex);

                CreateTileLabel(tile, worldPos);
            }
        }

        [ContextMenu("Clear Labels")]
        public void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(transform.GetChild(i).gameObject);
                else
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        private void CreateTileLabel(TileData tile, Vector3 position)
        {
            var labelObj = new GameObject($"Tile_{tile.LocalOffset.x}_{tile.LocalOffset.y}");
            labelObj.transform.SetParent(transform);
            labelObj.transform.position = position + Vector3.up * _labelYOffset;
            labelObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var tmp = labelObj.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = _labelFontSize;
            tmp.enableAutoSizing = false;
            tmp.rectTransform.sizeDelta = new Vector2(2f, 1f);

            // Line 1: Terrain (+ Structure if any)
            // Line 2: Offset coords
            string structureText = tile.Structure != StructureType.None
                ? $"\n<color=#FF6666>{tile.Structure}</color>"
                : "";
            string animalText = tile.Animal != AnimalType.None
                ? $"\n<color=#FFAA00>{tile.Animal}</color>"
                : "";

            tmp.text = $"<b>{tile.Terrain}</b>{structureText}{animalText}\n<size=1>({tile.LocalOffset.x},{tile.LocalOffset.y})</size>";
            tmp.color = _useTerrainColors
                ? TileVisualConfig.GetTerrainDebugColor(tile.Terrain)
                : _defaultColor;
        }

        // ---------------------------------------------------------
        // Gizmos (Scene View)
        // ---------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (_mapPiece == null || _mapPiece.Tiles == null) return;

            foreach (var tile in _mapPiece.Tiles)
            {
                HexCoordinates localHex = HexCoordinates.FromOffset(tile.LocalOffset.x, tile.LocalOffset.y);
                HexCoordinates rotatedHex = localHex.Rotate(_rotationSteps);
                Vector3 worldPos = transform.position + HexMetrics.HexToWorldPosition(rotatedHex);

                // Draw hex outline with terrain color
                Gizmos.color = _useTerrainColors
                    ? TileVisualConfig.GetTerrainDebugColor(tile.Terrain)
                    : _defaultColor;

                DrawHexOutline(worldPos);

                // Draw structure marker
                if (tile.Structure != StructureType.None)
                {
                    Gizmos.color = _structureMarkerColor;
                    Gizmos.DrawWireCube(worldPos + Vector3.up * 0.2f, Vector3.one * 0.3f);
                }

                // Draw animal territory marker
                if (tile.Animal != AnimalType.None)
                {
                    Gizmos.color = tile.Animal == AnimalType.Bear
                        ? new Color(0.6f, 0.3f, 0.1f) // Brown
                        : new Color(0.8f, 0.6f, 0.2f); // Tawny
                    Gizmos.DrawWireSphere(worldPos + Vector3.up * 0.1f, 0.15f);
                }
            }
        }

        private void DrawHexOutline(Vector3 center)
        {
            Vector3[] corners = HexMetrics.GetHexCorners(center);
            for (int i = 0; i < 6; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 6]);
            }
        }
    }
}
