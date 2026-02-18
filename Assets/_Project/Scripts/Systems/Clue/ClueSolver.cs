using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;

namespace Cryptid.Systems.Clue
{
    /// <summary>
    /// Result of a solver run, containing valid tiles and diagnostic info.
    /// </summary>
    public struct SolverResult
    {
        /// <summary> Tiles that satisfy ALL active clues. </summary>
        public List<WorldTile> ValidTiles;

        /// <summary> Number of clues evaluated. </summary>
        public int ClueCount;

        /// <summary> Total tiles in the world map. </summary>
        public int TotalTiles;

        /// <summary> Per-clue pass counts for diagnostics. </summary>
        public List<ClueStats> PerClueStats;

        /// <summary> True if exactly one valid tile was found (puzzle solved). </summary>
        public bool IsSolved => ValidTiles != null && ValidTiles.Count == 1;

        public override string ToString()
        {
            return $"[Solver] {ClueCount} clues, {TotalTiles} tiles → {ValidTiles?.Count ?? 0} valid" +
                   (IsSolved ? " ★ SOLVED!" : "");
        }
    }

    /// <summary>
    /// Diagnostic statistics for a single clue's evaluation.
    /// </summary>
    public struct ClueStats
    {
        public string ClueDescription;
        public int PassCount;
        public int FailCount;
    }

    /// <summary>
    /// The Cryptid Solver: filters the world map through a set of clues
    /// to find tiles that satisfy ALL conditions simultaneously.
    /// 
    /// In a valid Cryptid game setup, exactly one tile should remain
    /// after all clues are applied — the Cryptid Habitat (the answer).
    /// 
    /// Usage:
    ///   var solver = new ClueSolver();
    ///   solver.SetWorldMap(mapGenerator.WorldMap);
    ///   solver.AddClue(clueDefinition.ToClue());
    ///   SolverResult result = solver.Solve();
    /// </summary>
    public class ClueSolver
    {
        private readonly List<IClue> _clues = new List<IClue>();
        private IReadOnlyDictionary<HexCoordinates, WorldTile> _worldMap;

        // ---------------------------------------------------------
        // Setup
        // ---------------------------------------------------------

        /// <summary>
        /// Sets the world map to evaluate clues against.
        /// Must be called before Solve().
        /// </summary>
        public void SetWorldMap(IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            _worldMap = worldMap;
        }

        /// <summary>
        /// Adds a single clue to the solver.
        /// </summary>
        public void AddClue(IClue clue)
        {
            if (clue != null)
                _clues.Add(clue);
        }

        /// <summary>
        /// Adds multiple clues from ClueDefinition ScriptableObjects.
        /// Converts each definition to its runtime IClue.
        /// </summary>
        public void AddCluesFromDefinitions(IEnumerable<ClueDefinition> definitions)
        {
            foreach (var def in definitions)
            {
                if (def == null) continue;
                IClue clue = def.ToClue();
                if (clue != null)
                    _clues.Add(clue);
            }
        }

        /// <summary>
        /// Removes all clues from the solver.
        /// </summary>
        public void ClearClues()
        {
            _clues.Clear();
        }

        /// <summary>
        /// Returns the current number of active clues.
        /// </summary>
        public int ClueCount => _clues.Count;

        /// <summary>
        /// Returns a read-only view of active clues.
        /// </summary>
        public IReadOnlyList<IClue> Clues => _clues;

        // ---------------------------------------------------------
        // Solving
        // ---------------------------------------------------------

        /// <summary>
        /// Runs the solver: evaluates every tile against all active clues.
        /// A tile is "valid" only if ALL clues return true for it.
        /// 
        /// Returns a SolverResult with the list of valid tiles and diagnostics.
        /// </summary>
        public SolverResult Solve()
        {
            var result = new SolverResult
            {
                ValidTiles = new List<WorldTile>(),
                ClueCount = _clues.Count,
                TotalTiles = 0,
                PerClueStats = new List<ClueStats>()
            };

            if (_worldMap == null)
            {
                Debug.LogError("[ClueSolver] No world map set! Call SetWorldMap() first.");
                return result;
            }

            if (_clues.Count == 0)
            {
                Debug.LogWarning("[ClueSolver] No clues set. All tiles are valid.");
                foreach (var tile in _worldMap.Values)
                    result.ValidTiles.Add(tile);
                result.TotalTiles = result.ValidTiles.Count;
                return result;
            }

            // Initialize per-clue stats
            for (int i = 0; i < _clues.Count; i++)
            {
                result.PerClueStats.Add(new ClueStats
                {
                    ClueDescription = _clues[i].Description,
                    PassCount = 0,
                    FailCount = 0
                });
            }

            // Evaluate each tile against all clues
            foreach (var kvp in _worldMap)
            {
                WorldTile tile = kvp.Value;
                result.TotalTiles++;

                bool passesAll = true;

                for (int i = 0; i < _clues.Count; i++)
                {
                    bool passes = _clues[i].Check(tile, _worldMap);

                    // Update per-clue stats
                    var stats = result.PerClueStats[i];
                    if (passes)
                        stats.PassCount++;
                    else
                        stats.FailCount++;
                    result.PerClueStats[i] = stats;

                    if (!passes)
                    {
                        passesAll = false;
                        // Continue checking remaining clues for stats
                    }
                }

                if (passesAll)
                {
                    result.ValidTiles.Add(tile);
                }
            }

            Debug.Log(result.ToString());
            return result;
        }

        /// <summary>
        /// Evaluates a single tile against all clues and returns per-clue results.
        /// Useful for debugging why a specific tile passes or fails.
        /// </summary>
        public Dictionary<string, bool> DiagnoseTile(WorldTile tile)
        {
            var diagnosis = new Dictionary<string, bool>();

            if (_worldMap == null) return diagnosis;

            foreach (var clue in _clues)
            {
                diagnosis[clue.Description] = clue.Check(tile, _worldMap);
            }

            return diagnosis;
        }
    }
}
