using UnityEngine;
using TMPro;

namespace Cryptid.Core
{
    /// <summary>
    /// Visual debugging component for hex grid coordinates.
    /// Spawns a hex grid of configurable radius and renders:
    ///   - Hex outlines via Gizmos (Scene View)
    ///   - Cube coordinate labels via TextMeshPro (Game View & Scene View)
    /// 
    /// VISIBILITY FIRST: This component exists solely for visual verification
    /// that the coordinate math is correct.
    /// </summary>
    public class HexGridDebugger : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int _gridRadius = 3;

        [Header("Gizmo Colors")]
        [SerializeField] private Color _hexOutlineColor = Color.white;
        [SerializeField] private Color _originColor = Color.yellow;
        [SerializeField] private Color _neighborHighlightColor = Color.cyan;

        [Header("Label Settings")]
        [SerializeField] private GameObject _labelPrefab;
        [SerializeField] private float _labelYOffset = 0.05f;
        [SerializeField] private int _labelFontSize = 3;

        [Header("Debug Options")]
        [SerializeField] private bool _showNeighborsOfOrigin = true;
        [SerializeField] private bool _showDistanceColors;
        [SerializeField] private bool _spawnLabelsOnStart = true;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Start()
        {
            if (_spawnLabelsOnStart)
            {
                SpawnCoordinateLabels();
            }
        }

        // ---------------------------------------------------------
        // Label Spawning (Game View Visibility)
        // ---------------------------------------------------------

        /// <summary>
        /// Creates TextMeshPro labels at each hex position showing cube coordinates.
        /// If no label prefab is assigned, creates a simple one at runtime.
        /// </summary>
        [ContextMenu("Spawn Coordinate Labels")]
        public void SpawnCoordinateLabels()
        {
            // Clear existing labels
            ClearLabels();

            var hexes = HexUtility.GetHexesInRange(HexCoordinates.Zero, _gridRadius);

            foreach (var hex in hexes)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(hex);
                CreateLabel(hex, worldPos);
            }
        }

        /// <summary>
        /// Removes all child label objects.
        /// </summary>
        [ContextMenu("Clear Labels")]
        public void ClearLabels()
        {
            // Destroy children in reverse order
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(transform.GetChild(i).gameObject);
                else
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        private void CreateLabel(HexCoordinates hex, Vector3 worldPos)
        {
            GameObject labelObj;

            if (_labelPrefab != null)
            {
                labelObj = Instantiate(_labelPrefab, transform);
            }
            else
            {
                // Create a runtime label with TextMeshPro
                labelObj = new GameObject($"HexLabel_{hex}");
                labelObj.transform.SetParent(transform);

                var tmp = labelObj.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = _labelFontSize;
                tmp.enableAutoSizing = false;
                tmp.rectTransform.sizeDelta = new Vector2(2f, 1f);

                // Rotate text to face up (readable from top-down camera)
                labelObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            labelObj.transform.position = worldPos + Vector3.up * _labelYOffset;
            labelObj.name = $"Hex_{hex}";

            // Set label text
            var textComponent = labelObj.GetComponent<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = hex.ToLabel();

                // Color code: origin is yellow, others are white
                if (hex == HexCoordinates.Zero)
                    textComponent.color = _originColor;
                else if (_showDistanceColors)
                    textComponent.color = GetDistanceColor(hex.DistanceTo(HexCoordinates.Zero));
            }
        }

        /// <summary>
        /// Returns a color based on distance from origin for visual debugging.
        /// </summary>
        private Color GetDistanceColor(int distance)
        {
            float t = Mathf.InverseLerp(0f, _gridRadius, distance);
            return Color.Lerp(Color.green, Color.red, t);
        }

        // ---------------------------------------------------------
        // Gizmos (Scene View Visibility)
        // ---------------------------------------------------------

        private void OnDrawGizmos()
        {
            var hexes = HexUtility.GetHexesInRange(HexCoordinates.Zero, _gridRadius);
            var originNeighbors = HexCoordinates.Zero.GetAllNeighbors();

            foreach (var hex in hexes)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(hex);

                // Determine color
                if (hex == HexCoordinates.Zero)
                {
                    Gizmos.color = _originColor;
                }
                else if (_showNeighborsOfOrigin && IsInArray(hex, originNeighbors))
                {
                    Gizmos.color = _neighborHighlightColor;
                }
                else if (_showDistanceColors)
                {
                    Gizmos.color = GetDistanceColor(hex.DistanceTo(HexCoordinates.Zero));
                }
                else
                {
                    Gizmos.color = _hexOutlineColor;
                }

                DrawHexOutline(worldPos);

                // Draw small sphere at center
                Gizmos.DrawWireSphere(worldPos, 0.05f);
            }
        }

        /// <summary>
        /// Draws the outline of a flat-top hexagon using Gizmos lines.
        /// </summary>
        private void DrawHexOutline(Vector3 center)
        {
            Vector3[] corners = HexMetrics.GetHexCorners(center);
            for (int i = 0; i < 6; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 6]);
            }
        }

        private static bool IsInArray(HexCoordinates target, HexCoordinates[] array)
        {
            foreach (var item in array)
            {
                if (item == target)
                    return true;
            }
            return false;
        }
    }
}
