using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Systems.Map;
using UnityEngine;
using TMPro;

namespace Cryptid.Systems.Clue
{
    /// <summary>
    /// Visual debugger for PuzzleGenerator output.
    /// 
    /// VISIBILITY FIRST:
    /// - Gold star on the answer tile
    /// - Per-player clue overlay: colored highlight per clue filter
    /// - Green = passes ALL clues (should be exactly 1 tile)
    /// - Dark = fails at least one clue
    /// - Console log with full puzzle breakdown
    /// 
    /// Setup:
    /// 1. Add to a GameObject
    /// 2. Assign MapGenerator reference
    /// 3. Set player count (3-5)
    /// 4. Play Mode → Press G to generate, C to clear
    /// </summary>
    public class PuzzleDebugVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapGenerator _mapGenerator;

        [Header("Puzzle Settings")]
        [Range(2, 5)]
        [SerializeField] private int _playerCount = 3;

        [Tooltip("Random seed (-1 = random)")]
        [SerializeField] private int _seed = -1;

        [Header("Visual Settings")]
        [SerializeField] private Color _answerColor = new Color(1f, 0.85f, 0f);
        [SerializeField] private Color _validColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _invalidColor = new Color(0.15f, 0.08f, 0.08f);

        [Header("Player Colors (for per-clue view)")]
        [SerializeField] private Color[] _playerColors = new Color[]
        {
            new Color(0.3f, 0.6f, 1f),   // Blue
            new Color(1f, 0.4f, 0.3f),    // Red
            new Color(0.4f, 0.9f, 0.4f),  // Green
            new Color(1f, 0.8f, 0.2f),    // Yellow
            new Color(0.8f, 0.4f, 0.9f),  // Purple
        };

        // ---------------------------------------------------------
        // Runtime State
        // ---------------------------------------------------------

        private PuzzleSetup _currentPuzzle;
        private PuzzleGenerator _generator;
        private bool _isShowing;
        private int _currentViewMode; // 0=all AND, 1..N=per-player
        private readonly Dictionary<HexCoordinates, Color> _savedColors = new();
        private readonly List<GameObject> _overlayObjects = new();

        // Pre-computed per-player pass sets
        private List<HashSet<HexCoordinates>> _playerPassSets;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            _generator = new PuzzleGenerator();
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current == null) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;

            if (kb.gKey.wasPressedThisFrame)
                GeneratePuzzle();
            else if (kb.cKey.wasPressedThisFrame)
                ClearVisualization();
            else if (kb.vKey.wasPressedThisFrame)
                CycleViewMode();
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Generates a new puzzle and visualizes it.
        /// </summary>
        [ContextMenu("Generate Puzzle")]
        public void GeneratePuzzle()
        {
            if (_mapGenerator == null)
            {
                Debug.LogError("[PuzzleDebugVisualizer] No MapGenerator assigned!");
                return;
            }

            if (_mapGenerator.WorldMap == null || _mapGenerator.WorldMap.Count == 0)
            {
                Debug.LogError("[PuzzleDebugVisualizer] WorldMap is empty.");
                return;
            }

            // Clear previous
            ClearVisualization();

            // Generate
            _currentPuzzle = _generator.Generate(_mapGenerator.WorldMap, _playerCount, _seed);

            if (_currentPuzzle == null)
            {
                Debug.LogError("[PuzzleDebugVisualizer] Puzzle generation failed!");
                return;
            }

            // Pre-compute per-player pass sets
            ComputePlayerPassSets();

            // Show combined view
            _currentViewMode = 0;
            ApplyVisualization();
            _isShowing = true;

            // Log full puzzle info
            Debug.Log(_currentPuzzle.ToString());
            Debug.Log("[PuzzleDebugVisualizer] Press V to cycle views, C to clear.");
        }

        /// <summary>
        /// Clears all visualization.
        /// </summary>
        [ContextMenu("Clear")]
        public void ClearVisualization()
        {
            RestoreTileColors();
            ClearOverlays();
            _isShowing = false;
            _currentPuzzle = null;
            _playerPassSets = null;
        }

        // ---------------------------------------------------------
        // View Modes
        // ---------------------------------------------------------

        /// <summary>
        /// Cycles through view modes:
        /// 0 = Combined (AND of all clues)
        /// 1..N = Individual player clue view
        /// </summary>
        private void CycleViewMode()
        {
            if (!_isShowing || _currentPuzzle == null) return;

            _currentViewMode = (_currentViewMode + 1) % (_currentPuzzle.PlayerCount + 1);

            string viewName = _currentViewMode == 0
                ? "Combined (AND)"
                : $"Player {_currentViewMode}: {_currentPuzzle.PlayerClues[_currentViewMode - 1].Description}";

            Debug.Log($"[PuzzleDebugVisualizer] View: {viewName}");

            RestoreTileColors();
            ClearOverlays();
            ApplyVisualization();
        }

        // ---------------------------------------------------------
        // Visualization
        // ---------------------------------------------------------

        private void ApplyVisualization()
        {
            var hexTiles = FindObjectsByType<HexTile>(FindObjectsSortMode.None);

            // Save original colors (only once)
            if (_savedColors.Count == 0)
            {
                foreach (var ht in hexTiles)
                {
                    var r = ht.GetComponent<MeshRenderer>();
                    if (r != null)
                        _savedColors[ht.Coordinates] = r.material.color;
                }
            }

            if (_currentViewMode == 0)
            {
                ApplyCombinedView(hexTiles);
            }
            else
            {
                ApplyPlayerView(hexTiles, _currentViewMode - 1);
            }
        }

        /// <summary>
        /// Combined view: highlights tiles that pass ALL clues.
        /// Answer tile gets gold. Valid=green, Invalid=dark.
        /// </summary>
        private void ApplyCombinedView(HexTile[] hexTiles)
        {
            // Compute intersection of all player pass sets
            HashSet<HexCoordinates> allValid = null;
            foreach (var passSet in _playerPassSets)
            {
                if (allValid == null)
                    allValid = new HashSet<HexCoordinates>(passSet);
                else
                    allValid.IntersectWith(passSet);
            }

            foreach (var ht in hexTiles)
            {
                var renderer = ht.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                bool isAnswer = ht.Coordinates.Equals(_currentPuzzle.AnswerTile.Coordinates);
                bool isValid = allValid != null && allValid.Contains(ht.Coordinates);

                if (isAnswer)
                {
                    renderer.material.color = _answerColor;
                    SpawnAnswerMarker(ht.transform.position);
                }
                else if (isValid)
                {
                    renderer.material.color = _validColor;
                }
                else
                {
                    renderer.material.color = _invalidColor;
                }
            }

            Debug.Log($"[PuzzleDebugVisualizer] Combined view: {allValid?.Count ?? 0} valid tile(s).");
        }

        /// <summary>
        /// Per-player view: highlights tiles that pass one specific player's clue.
        /// </summary>
        private void ApplyPlayerView(HexTile[] hexTiles, int playerIndex)
        {
            var passSet = _playerPassSets[playerIndex];
            Color playerColor = playerIndex < _playerColors.Length
                ? _playerColors[playerIndex]
                : Color.white;

            foreach (var ht in hexTiles)
            {
                var renderer = ht.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                bool isAnswer = ht.Coordinates.Equals(_currentPuzzle.AnswerTile.Coordinates);
                bool passes = passSet.Contains(ht.Coordinates);

                if (isAnswer)
                {
                    renderer.material.color = _answerColor;
                }
                else if (passes)
                {
                    renderer.material.color = playerColor;
                }
                else
                {
                    renderer.material.color = _invalidColor;
                }
            }

            int passCount = passSet.Count;
            float passRate = _mapGenerator.WorldMap.Count > 0
                ? (float)passCount / _mapGenerator.WorldMap.Count * 100f
                : 0f;

            Debug.Log($"[PuzzleDebugVisualizer] Player {playerIndex + 1}: " +
                     $"{_currentPuzzle.PlayerClues[playerIndex].Description} " +
                     $"({passCount} tiles, {passRate:F0}%)");
        }

        // ---------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------

        private void ComputePlayerPassSets()
        {
            _playerPassSets = new List<HashSet<HexCoordinates>>();

            foreach (var clue in _currentPuzzle.PlayerClues)
            {
                var passSet = new HashSet<HexCoordinates>();
                foreach (var kvp in _mapGenerator.WorldMap)
                {
                    if (clue.Check(kvp.Value, _mapGenerator.WorldMap))
                        passSet.Add(kvp.Key);
                }
                _playerPassSets.Add(passSet);
            }
        }

        private void SpawnAnswerMarker(Vector3 position)
        {
            // Spawn a floating label above the answer tile
            var markerObj = new GameObject("AnswerMarker");
            markerObj.transform.position = position + Vector3.up * 0.6f;
            markerObj.transform.SetParent(transform);

            var tmp = markerObj.AddComponent<TextMeshPro>();
            tmp.text = "ANSWER";
            tmp.fontSize = 2.5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = _answerColor;
            tmp.transform.rotation = Quaternion.Euler(90, 0, 0);

            _overlayObjects.Add(markerObj);
        }

        private void RestoreTileColors()
        {
            var hexTiles = FindObjectsByType<HexTile>(FindObjectsSortMode.None);
            foreach (var ht in hexTiles)
            {
                var renderer = ht.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                if (_savedColors.TryGetValue(ht.Coordinates, out Color original))
                    renderer.material.color = original;
            }
            _savedColors.Clear();
        }

        private void ClearOverlays()
        {
            foreach (var obj in _overlayObjects)
            {
                if (obj != null)
                {
                    if (Application.isPlaying) Destroy(obj);
                    else DestroyImmediate(obj);
                }
            }
            _overlayObjects.Clear();
        }

        // ---------------------------------------------------------
        // Gizmos
        // ---------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (_currentPuzzle == null) return;

            // Draw answer tile marker in Scene view
            Vector3 answerPos = HexMetrics.HexToWorldPosition(_currentPuzzle.AnswerTile.Coordinates);
            Gizmos.color = _answerColor;
            Gizmos.DrawWireSphere(answerPos + Vector3.up * 0.5f, 0.3f);
            Gizmos.DrawLine(answerPos, answerPos + Vector3.up * 1f);
        }
    }
}
