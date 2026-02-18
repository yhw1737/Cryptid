using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;
using TMPro;

namespace Cryptid.Systems.Map
{
    /// <summary>
    /// Visual debugging overlay for the assembled world map.
    /// Requires a MapGenerator on the same or referenced GameObject.
    /// 
    /// Spawns TMP labels showing cube coordinates + terrain info
    /// for every tile in the generated WorldMap.
    /// 
    /// VISIBILITY FIRST: Provides both Gizmo outlines (Scene View)
    /// and TMP labels (Game View) for full map verification.
    /// </summary>
    [RequireComponent(typeof(MapGenerator))]
    public class MapDebugOverlay : MonoBehaviour
    {
        [Header("Label Settings")]
        [SerializeField] private int _fontSize = 2;
        [SerializeField] private float _labelYOffset = 0.1f;
        [SerializeField] private bool _showTerrain = true;
        [SerializeField] private bool _showCoordinates = true;
        [SerializeField] private bool _showPieceId;

        [Header("Color Settings")]
        [SerializeField] private bool _colorByPiece;

        private MapGenerator _mapGenerator;

        private static readonly Color[] PieceColors = new Color[]
        {
            new Color(1f, 0.4f, 0.4f),     // Piece 1: Red
            new Color(0.4f, 0.8f, 1f),      // Piece 2: Blue
            new Color(0.4f, 1f, 0.4f),      // Piece 3: Green
            new Color(1f, 1f, 0.4f),         // Piece 4: Yellow
            new Color(1f, 0.6f, 1f),         // Piece 5: Pink
            new Color(1f, 0.7f, 0.3f),       // Piece 6: Orange
        };

        private void Awake()
        {
            _mapGenerator = GetComponent<MapGenerator>();
        }

        /// <summary>
        /// Call after MapGenerator.GenerateMap() to spawn debug labels.
        /// </summary>
        [ContextMenu("Spawn Map Labels")]
        public void SpawnLabels()
        {
            ClearLabels();

            if (_mapGenerator == null)
                _mapGenerator = GetComponent<MapGenerator>();

            if (_mapGenerator.WorldMap == null || _mapGenerator.WorldMap.Count == 0)
            {
                Debug.LogWarning("[MapDebugOverlay] WorldMap is empty. Generate the map first.");
                return;
            }

            foreach (var kvp in _mapGenerator.WorldMap)
            {
                CreateLabel(kvp.Key, kvp.Value);
            }

            Debug.Log($"[MapDebugOverlay] Spawned {_mapGenerator.WorldMap.Count} debug labels.");
        }

        [ContextMenu("Clear Labels")]
        public void ClearLabels()
        {
            // Only destroy label children (tagged with name prefix)
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("MapLabel_"))
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
        }

        private void CreateLabel(HexCoordinates coords, WorldTile tile)
        {
            Vector3 worldPos = HexMetrics.HexToWorldPosition(coords);

            var labelObj = new GameObject($"MapLabel_{coords}");
            labelObj.transform.SetParent(transform);
            labelObj.transform.position = worldPos + Vector3.up * _labelYOffset;
            labelObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var tmp = labelObj.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = _fontSize;
            tmp.enableAutoSizing = false;
            tmp.rectTransform.sizeDelta = new Vector2(2f, 1.5f);

            // Build label text
            var lines = new List<string>();

            if (_showCoordinates)
                lines.Add($"<size={_fontSize * 0.7f}>{coords.ToLabel()}</size>");

            if (_showTerrain)
                lines.Add($"<b>{tile.Terrain}</b>");

            if (tile.Structure != StructureType.None)
                lines.Add($"<color=#FF6666>{tile.Structure}</color>");

            if (_showPieceId)
                lines.Add($"<size={_fontSize * 0.5f}>P{tile.SourcePieceId}</size>");

            tmp.text = string.Join("\n", lines);

            // Set color
            if (_colorByPiece && tile.SourcePieceId >= 1 && tile.SourcePieceId <= 6)
                tmp.color = PieceColors[tile.SourcePieceId - 1];
            else
                tmp.color = TileVisualConfig.GetTerrainDebugColor(tile.Terrain);
        }

        // ---------------------------------------------------------
        // Gizmos: Piece boundaries
        // ---------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (_mapGenerator == null || _mapGenerator.WorldMap == null) return;
            if (_mapGenerator.WorldMap.Count == 0) return;

            foreach (var kvp in _mapGenerator.WorldMap)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(kvp.Key);

                if (_colorByPiece && kvp.Value.SourcePieceId >= 1 && kvp.Value.SourcePieceId <= 6)
                    Gizmos.color = PieceColors[kvp.Value.SourcePieceId - 1];
                else
                    Gizmos.color = TileVisualConfig.GetTerrainDebugColor(kvp.Value.Terrain);

                DrawHexOutline(worldPos);
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
