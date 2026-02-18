using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Systems.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace Cryptid.Systems.Clue
{
    /// <summary>
    /// Visual Test Runner for the Clue Solver system.
    /// 
    /// VISIBILITY FIRST: Allows testing clue logic directly in Play Mode.
    /// - Highlights valid tiles in green, invalid in dark red
    /// - Overlays per-tile pass/fail info as floating labels
    /// - Logs per-clue statistics to Console
    /// - Click a highlighted tile to see which clues it passes/fails
    /// 
    /// Setup:
    /// 1. Add this component to a GameObject in the scene
    /// 2. Assign the MapGenerator reference
    /// 3. Add ClueDefinition assets to the Clues list
    /// 4. Enter Play Mode → click "Run Solver" in Inspector (or press Space)
    /// </summary>
    public class ClueTestRunner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("MapGenerator that provides the world map data")]
        [SerializeField] private MapGenerator _mapGenerator;

        [Header("Clue Definitions")]
        [Tooltip("List of clue definitions to test. The solver will AND them together.")]
        [SerializeField] private List<ClueDefinition> _clueDefinitions = new List<ClueDefinition>();

        [Header("Visual Settings")]
        [Tooltip("Color for tiles that pass ALL clues")]
        [SerializeField] private Color _validColor = new Color(0.2f, 0.9f, 0.3f, 1f);

        [Tooltip("Color for tiles that fail one or more clues")]
        [SerializeField] private Color _invalidColor = new Color(0.3f, 0.1f, 0.1f, 1f);

        [Tooltip("Color for the single solution tile (if exactly one valid)")]
        [SerializeField] private Color _solutionColor = new Color(1f, 0.85f, 0f, 1f);

        [Tooltip("Show floating labels with pass/fail counts")]
        [SerializeField] private bool _showLabels = true;

        [Tooltip("Use Space key to run solver")]
        [SerializeField] private bool _spaceToRun = true;

        // ---------------------------------------------------------
        // Runtime State
        // ---------------------------------------------------------

        private ClueSolver _solver;
        private SolverResult _lastResult;
        private bool _isShowingResults;
        private readonly Dictionary<HexCoordinates, Color> _originalColors = new();
        private readonly List<GameObject> _labelObjects = new();
        private HashSet<HexCoordinates> _validCoords;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Update()
        {
            if (_spaceToRun && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (_isShowingResults)
                    ClearResults();
                else
                    RunSolver();
            }
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Executes the solver with the current clue set and visualizes results.
        /// Can be called from Inspector button or code.
        /// </summary>
        [ContextMenu("Run Solver")]
        public void RunSolver()
        {
            if (_mapGenerator == null)
            {
                Debug.LogError("[ClueTestRunner] No MapGenerator assigned!");
                return;
            }

            if (_mapGenerator.WorldMap == null || _mapGenerator.WorldMap.Count == 0)
            {
                Debug.LogError("[ClueTestRunner] WorldMap is empty. Generate the map first.");
                return;
            }

            // Create solver and load clues
            _solver = new ClueSolver();
            _solver.SetWorldMap(_mapGenerator.WorldMap);
            _solver.AddCluesFromDefinitions(_clueDefinitions);

            Debug.Log($"[ClueTestRunner] Running solver with {_solver.ClueCount} clues on {_mapGenerator.WorldMap.Count} tiles...");

            // Log clue descriptions
            for (int i = 0; i < _solver.Clues.Count; i++)
            {
                Debug.Log($"  Clue {i + 1}: {_solver.Clues[i].Description}");
            }

            // Run solver
            _lastResult = _solver.Solve();

            // Build valid coordinate set for quick lookup
            _validCoords = new HashSet<HexCoordinates>();
            foreach (var tile in _lastResult.ValidTiles)
            {
                _validCoords.Add(tile.Coordinates);
            }

            // Log per-clue stats
            LogPerClueStats();

            // Visualize results
            ApplyVisualization();

            _isShowingResults = true;
        }

        /// <summary>
        /// Clears the solver visualization and restores original tile colors.
        /// </summary>
        [ContextMenu("Clear Results")]
        public void ClearResults()
        {
            RestoreOriginalColors();
            ClearLabels();
            _isShowingResults = false;
            _validCoords = null;
            Debug.Log("[ClueTestRunner] Results cleared.");
        }

        /// <summary>
        /// Diagnoses a specific HexTile, logging which clues it passes/fails.
        /// Called when clicking a tile while results are shown.
        /// </summary>
        public void DiagnoseTile(HexTile hexTile)
        {
            if (_solver == null || !_isShowingResults) return;

            var diagnosis = _solver.DiagnoseTile(hexTile.TileData);

            Debug.Log($"=== Diagnosis for {hexTile.TileData} ===");
            foreach (var kvp in diagnosis)
            {
                string icon = kvp.Value ? "✓" : "✗";
                Debug.Log($"  {icon} {kvp.Key}");
            }
        }

        // ---------------------------------------------------------
        // Visualization
        // ---------------------------------------------------------

        private void ApplyVisualization()
        {
            _originalColors.Clear();
            ClearLabels();

            // Find all HexTile components in the scene
            var hexTiles = FindObjectsByType<HexTile>(FindObjectsSortMode.None);

            foreach (var hexTile in hexTiles)
            {
                var renderer = hexTile.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                // Save original color
                _originalColors[hexTile.Coordinates] = renderer.material.color;

                bool isValid = _validCoords.Contains(hexTile.Coordinates);

                if (_lastResult.IsSolved && isValid)
                {
                    // Single solution — gold highlight
                    renderer.material.color = _solutionColor;
                }
                else if (isValid)
                {
                    renderer.material.color = _validColor;
                }
                else
                {
                    renderer.material.color = _invalidColor;
                }

                // Spawn floating label
                if (_showLabels && isValid)
                {
                    SpawnLabel(hexTile, isValid);
                }
            }

            // Summary log
            string status = _lastResult.IsSolved
                ? $"★ SOLVED! Answer: {_lastResult.ValidTiles[0].Coordinates}"
                : $"{_lastResult.ValidTiles.Count} valid tiles remaining";

            Debug.Log($"[ClueTestRunner] {status}");
        }

        private void SpawnLabel(HexTile hexTile, bool isValid)
        {
            Vector3 pos = hexTile.transform.position + Vector3.up * 0.5f;

            var labelObj = new GameObject($"Label_{hexTile.Coordinates}");
            labelObj.transform.position = pos;
            labelObj.transform.SetParent(transform);

            var tmp = labelObj.AddComponent<TextMeshPro>();
            tmp.text = isValid ? "O" : "";
            tmp.fontSize = 3f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = isValid ? Color.green : Color.red;

            // Billboard: face camera
            tmp.transform.rotation = Quaternion.Euler(90, 0, 0);

            _labelObjects.Add(labelObj);
        }

        private void RestoreOriginalColors()
        {
            var hexTiles = FindObjectsByType<HexTile>(FindObjectsSortMode.None);

            foreach (var hexTile in hexTiles)
            {
                var renderer = hexTile.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                if (_originalColors.TryGetValue(hexTile.Coordinates, out Color original))
                {
                    renderer.material.color = original;
                }
            }

            _originalColors.Clear();
        }

        private void ClearLabels()
        {
            foreach (var label in _labelObjects)
            {
                if (label != null)
                {
                    if (Application.isPlaying)
                        Destroy(label);
                    else
                        DestroyImmediate(label);
                }
            }
            _labelObjects.Clear();
        }

        // ---------------------------------------------------------
        // Diagnostics
        // ---------------------------------------------------------

        private void LogPerClueStats()
        {
            Debug.Log("--- Per-Clue Statistics ---");
            for (int i = 0; i < _lastResult.PerClueStats.Count; i++)
            {
                var stats = _lastResult.PerClueStats[i];
                float passRate = _lastResult.TotalTiles > 0
                    ? (float)stats.PassCount / _lastResult.TotalTiles * 100f
                    : 0f;
                Debug.Log($"  Clue {i + 1} [{stats.ClueDescription}]: " +
                         $"{stats.PassCount} pass / {stats.FailCount} fail ({passRate:F1}%)");
            }
        }

        // ---------------------------------------------------------
        // Gizmos (Scene View always visible)
        // ---------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (!_isShowingResults || _validCoords == null) return;

            foreach (var coord in _validCoords)
            {
                Vector3 pos = HexMetrics.HexToWorldPosition(coord);
                Gizmos.color = _lastResult.IsSolved ? _solutionColor : _validColor;
                Gizmos.DrawWireSphere(pos + Vector3.up * 0.3f, 0.2f);
            }
        }
    }
}
