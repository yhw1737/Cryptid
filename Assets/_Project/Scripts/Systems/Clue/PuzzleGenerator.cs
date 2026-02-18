using System;
using System.Collections.Generic;
using System.Linq;
using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;

namespace Cryptid.Systems.Clue
{
    /// <summary>
    /// Generates a valid Cryptid puzzle using Reverse Puzzle Generation:
    /// 
    /// 1. Pick a random answer tile from the world map
    /// 2. Enumerate all candidate clues the answer satisfies
    /// 3. Find a combination of N clues (one per player) whose AND yields exactly 1 tile
    /// 4. Return the PuzzleSetup
    /// 
    /// This guarantees the puzzle has a unique solution.
    /// </summary>
    public class PuzzleGenerator
    {
        // ---------------------------------------------------------
        // Configuration
        // ---------------------------------------------------------

        /// <summary> Distance ranges to try for distance-based clues. </summary>
        private static readonly int[] DistanceValues = { 1, 2, 3 };

        /// <summary> Max attempts to find a valid puzzle before giving up. </summary>
        private const int MaxAnswerAttempts = 50;

        /// <summary> Max clue combinations to try per answer tile. </summary>
        private const int MaxCombinationAttempts = 5000;

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Generates a valid puzzle for the given world map and player count.
        /// </summary>
        /// <param name="worldMap">The assembled world map.</param>
        /// <param name="playerCount">Number of players (3-5).</param>
        /// <param name="seed">Random seed for reproducibility. -1 for random.</param>
        /// <returns>A valid PuzzleSetup, or null if no puzzle could be found.</returns>
        public PuzzleSetup Generate(
            IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap,
            int playerCount,
            int seed = -1)
        {
            if (worldMap == null || worldMap.Count == 0)
            {
                Debug.LogError("[PuzzleGenerator] World map is empty!");
                return null;
            }

            if (playerCount < 2 || playerCount > 5)
            {
                Debug.LogError($"[PuzzleGenerator] Invalid player count: {playerCount}. Must be 2-5.");
                return null;
            }

            // Initialize RNG
            int actualSeed = seed >= 0 ? seed : Environment.TickCount;
            var rng = new System.Random(actualSeed);
            Debug.Log($"[PuzzleGenerator] Starting generation: {worldMap.Count} tiles, " +
                      $"{playerCount} players, seed={actualSeed}");

            // Get all tiles as a shuffled list
            var allTiles = worldMap.Values.ToList();
            Shuffle(allTiles, rng);

            // Try different answer tiles
            int attempts = 0;
            foreach (var answerTile in allTiles)
            {
                if (++attempts > MaxAnswerAttempts)
                {
                    Debug.LogWarning($"[PuzzleGenerator] Exhausted {MaxAnswerAttempts} answer attempts.");
                    break;
                }

                // Step 1: Enumerate candidate clues for this answer
                var candidates = EnumerateCandidateClues(answerTile, worldMap);

                if (candidates.Count < playerCount)
                {
                    // Not enough clues for this answer tile
                    continue;
                }

                Debug.Log($"[PuzzleGenerator] Trying answer {answerTile.Coordinates}: " +
                          $"{candidates.Count} candidate clues");

                // Step 2: Find a valid clue combination
                var combination = FindValidCombination(
                    candidates, playerCount, worldMap, rng);

                if (combination != null)
                {
                    var setup = new PuzzleSetup(answerTile, combination, candidates, actualSeed);
                    Debug.Log($"[PuzzleGenerator] Puzzle found!\n{setup}");
                    return setup;
                }
            }

            Debug.LogError("[PuzzleGenerator] Failed to generate a valid puzzle! " +
                          "Try adding more map pieces or adjusting clue parameters.");
            return null;
        }

        // ---------------------------------------------------------
        // Step 1: Enumerate Candidate Clues
        // ---------------------------------------------------------

        /// <summary>
        /// Generates all possible clues that the answer tile satisfies.
        /// These become the candidate pool for clue selection.
        /// </summary>
        public List<IClue> EnumerateCandidateClues(
            WorldTile answerTile,
            IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            var candidates = new List<IClue>();

            // --- Terrain-based clues ---

            // "On [Terrain]" (5 options, only 1 will pass)
            foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
            {
                var clue = new OnTerrainClue(terrain);
                if (clue.Check(answerTile, worldMap))
                    candidates.Add(clue);
            }

            // "On [TerrainA] or [TerrainB]" (C(5,2) = 10 pairs)
            var terrains = (TerrainType[])Enum.GetValues(typeof(TerrainType));
            for (int i = 0; i < terrains.Length; i++)
            {
                for (int j = i + 1; j < terrains.Length; j++)
                {
                    var clue = new OnTerrainPairClue(terrains[i], terrains[j]);
                    if (clue.Check(answerTile, worldMap))
                        candidates.Add(clue);
                }
            }

            // --- Distance-based clues ---

            // "Within N hexes of [Terrain]"
            foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
            {
                foreach (int dist in DistanceValues)
                {
                    var clue = new WithinDistanceOfTerrainClue(terrain, dist);
                    if (clue.Check(answerTile, worldMap))
                        candidates.Add(clue);
                }
            }

            // "Within N hexes of [Structure]"
            foreach (StructureType structure in Enum.GetValues(typeof(StructureType)))
            {
                if (structure == StructureType.None) continue;

                foreach (int dist in DistanceValues)
                {
                    var clue = new WithinDistanceOfStructureClue(structure, dist);
                    if (clue.Check(answerTile, worldMap))
                        candidates.Add(clue);
                }
            }

            // "Within N hexes of [Animal] territory"
            foreach (AnimalType animal in Enum.GetValues(typeof(AnimalType)))
            {
                if (animal == AnimalType.None) continue;

                foreach (int dist in DistanceValues)
                {
                    var clue = new WithinDistanceOfAnimalClue(animal, dist);
                    if (clue.Check(answerTile, worldMap))
                        candidates.Add(clue);
                }
            }

            // --- Negated terrain clues ---
            // "NOT on [Terrain]" (useful as broader clues)
            foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
            {
                var inner = new OnTerrainClue(terrain);
                var clue = new NotClue(inner);
                if (clue.Check(answerTile, worldMap))
                    candidates.Add(clue);
            }

            return candidates;
        }

        // ---------------------------------------------------------
        // Step 2: Find Valid Combination
        // ---------------------------------------------------------

        /// <summary>
        /// Searches for a combination of N clues (from candidates) that
        /// together yield exactly 1 valid tile when AND-ed.
        /// 
        /// Uses randomized search rather than exhaustive enumeration
        /// for efficiency on larger candidate pools.
        /// </summary>
        private List<IClue> FindValidCombination(
            List<IClue> candidates,
            int playerCount,
            IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap,
            System.Random rng)
        {
            // Pre-compute pass sets for each candidate clue
            // passSet[i] = set of coordinates where candidates[i] returns true
            var passSets = new List<HashSet<HexCoordinates>>();
            foreach (var clue in candidates)
            {
                var passSet = new HashSet<HexCoordinates>();
                foreach (var kvp in worldMap)
                {
                    if (clue.Check(kvp.Value, worldMap))
                        passSet.Add(kvp.Key);
                }
                passSets.Add(passSet);
            }

            int totalTiles = worldMap.Count;

            // Randomized combination search
            int[] indices = new int[playerCount];
            int attempts = 0;

            while (attempts < MaxCombinationAttempts)
            {
                attempts++;

                // Pick random distinct indices
                if (!PickRandomDistinctIndices(indices, candidates.Count, rng))
                    continue;

                // Compute intersection size efficiently
                int intersectionCount = CountIntersection(passSets, indices, totalTiles);

                if (intersectionCount == 1)
                {
                    // Found a valid combination!
                    var result = new List<IClue>();
                    for (int i = 0; i < playerCount; i++)
                    {
                        result.Add(candidates[indices[i]]);
                    }

                    Debug.Log($"[PuzzleGenerator] Valid combination found after {attempts} attempts:");
                    for (int i = 0; i < result.Count; i++)
                    {
                        int passCount = passSets[indices[i]].Count;
                        float passRate = (float)passCount / totalTiles * 100f;
                        Debug.Log($"  Player {i + 1}: {result[i].Description} " +
                                 $"({passCount}/{totalTiles} tiles, {passRate:F0}%)");
                    }

                    return result;
                }
            }

            return null; // No valid combination found
        }

        // ---------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------

        /// <summary>
        /// Computes the intersection size of the pass-sets at the given indices.
        /// Uses the smallest set as the starting point for efficiency.
        /// </summary>
        private int CountIntersection(
            List<HashSet<HexCoordinates>> passSets,
            int[] indices,
            int totalTiles)
        {
            // Find the smallest set to iterate over
            int smallestIdx = 0;
            int smallestSize = int.MaxValue;
            for (int i = 0; i < indices.Length; i++)
            {
                int size = passSets[indices[i]].Count;
                if (size < smallestSize)
                {
                    smallestSize = size;
                    smallestIdx = i;
                }
            }

            // Count elements in smallest set that exist in ALL other sets
            int count = 0;
            foreach (var coord in passSets[indices[smallestIdx]])
            {
                bool inAll = true;
                for (int i = 0; i < indices.Length; i++)
                {
                    if (i == smallestIdx) continue;
                    if (!passSets[indices[i]].Contains(coord))
                    {
                        inAll = false;
                        break;
                    }
                }
                if (inAll) count++;

                // Early exit: if count exceeds 1, this combination is invalid
                if (count > 1) return count;
            }

            return count;
        }

        /// <summary>
        /// Picks N random distinct indices in [0, maxExclusive).
        /// Returns false if N > maxExclusive.
        /// </summary>
        private bool PickRandomDistinctIndices(int[] indices, int maxExclusive, System.Random rng)
        {
            if (indices.Length > maxExclusive) return false;

            var used = new HashSet<int>();
            for (int i = 0; i < indices.Length; i++)
            {
                int attempts = 0;
                int val;
                do
                {
                    val = rng.Next(maxExclusive);
                    if (++attempts > 100) return false;
                } while (used.Contains(val));

                indices[i] = val;
                used.Add(val);
            }

            return true;
        }

        /// <summary>
        /// Fisher-Yates shuffle.
        /// </summary>
        private void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
