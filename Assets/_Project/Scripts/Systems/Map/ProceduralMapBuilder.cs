using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;

namespace Cryptid.Systems.Map
{
    /// <summary>
    /// Pure-logic procedural map builder (Spec 5.1.B).
    ///
    /// Steps:
    ///   1. Generate terrain via 2-octave Perlin Noise
    ///   2. Validate terrain distribution (no single type > maxFraction)
    ///   3. Place structures with minimum distance constraint
    ///   4. Place animal territories with minimum distance + terrain filtering
    ///
    /// Stateless — call <see cref="Build"/> with a config and seed.
    /// Returns a Dictionary&lt;HexCoordinates, WorldTile&gt; ready for MapGenerator.
    /// </summary>
    public static class ProceduralMapBuilder
    {
        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Generates a complete procedural map.
        /// Retries internally if terrain validation fails.
        /// </summary>
        /// <param name="config">Map generation parameters.</param>
        /// <param name="seed">Random seed (-1 = random).</param>
        /// <returns>Assembled world map, or null if generation failed after max retries.</returns>
        public static Dictionary<HexCoordinates, WorldTile> Build(
            ProceduralMapConfig config, int seed = -1)
        {
            if (seed < 0) seed = Random.Range(0, int.MaxValue);

            for (int attempt = 0; attempt < config.MaxRetries; attempt++)
            {
                int trySeed = seed + attempt;
                var rng = new System.Random(trySeed);

                // Step 1: Generate terrain
                var map = GenerateTerrain(config, trySeed);

                // Step 2: Validate distribution
                if (!ValidateTerrainDistribution(map, config))
                {
                    Debug.Log($"[ProceduralMapBuilder] Attempt {attempt + 1}: " +
                             $"terrain distribution invalid (seed {trySeed}). Retrying...");
                    continue;
                }

                // Step 3: Place structures
                if (!PlaceStructures(map, config, rng))
                {
                    Debug.Log($"[ProceduralMapBuilder] Attempt {attempt + 1}: " +
                             $"structure placement failed (seed {trySeed}). Retrying...");
                    continue;
                }

                // Step 4: Place animal territories
                if (!PlaceAnimalTerritories(map, config, rng))
                {
                    Debug.Log($"[ProceduralMapBuilder] Attempt {attempt + 1}: " +
                             $"animal placement failed (seed {trySeed}). Retrying...");
                    continue;
                }

                Debug.Log($"[ProceduralMapBuilder] Map generated successfully! " +
                         $"Seed: {trySeed}, Tiles: {map.Count}, " +
                         $"Size: {config.Width}x{config.Height} " +
                         $"(attempt {attempt + 1})");
                return map;
            }

            Debug.LogError($"[ProceduralMapBuilder] Failed after {config.MaxRetries} attempts!");
            return null;
        }

        // ---------------------------------------------------------
        // Step 1: Terrain Generation (Perlin Noise)
        // ---------------------------------------------------------

        /// <summary>
        /// Generates terrain for all tiles using 2-octave Perlin noise.
        /// Noise value is mapped to terrain type via threshold bands.
        /// </summary>
        private static Dictionary<HexCoordinates, WorldTile> GenerateTerrain(
            ProceduralMapConfig config, int seed)
        {
            var map = new Dictionary<HexCoordinates, WorldTile>();

            // Perlin offset from seed for reproducibility
            float offsetX = (seed % 10000) * 1.7f;
            float offsetY = (seed / 10000f) * 2.3f;

            for (int col = 0; col < config.Width; col++)
            {
                for (int row = 0; row < config.Height; row++)
                {
                    HexCoordinates coords = HexCoordinates.FromOffset(col, row);

                    // Primary noise (large biomes)
                    float nx = (col + offsetX) * config.NoiseScale;
                    float ny = (row + offsetY) * config.NoiseScale;
                    float noise = Mathf.PerlinNoise(nx, ny);

                    // Detail octave (smaller variation within biomes)
                    float dx = (col + offsetX + 500f) * config.DetailScale;
                    float dy = (row + offsetY + 500f) * config.DetailScale;
                    float detail = Mathf.PerlinNoise(dx, dy);

                    // Blend primary + detail
                    float value = Mathf.Clamp01(
                        noise * (1f - config.DetailWeight) +
                        detail * config.DetailWeight);

                    // Map noise value to terrain type via thresholds
                    TerrainType terrain = NoiseToBiome(value, config);

                    var tile = new WorldTile
                    {
                        Coordinates = coords,
                        Terrain = terrain,
                        Structure = StructureType.None,
                        Animal = AnimalType.None,
                        SourcePieceId = 0 // Procedural = 0
                    };

                    map[coords] = tile;
                }
            }

            return map;
        }

        /// <summary>
        /// Converts a [0,1] noise value to a TerrainType using configured thresholds.
        /// </summary>
        private static TerrainType NoiseToBiome(float value, ProceduralMapConfig config)
        {
            float[] thresholds = config.TerrainThresholds;
            TerrainType[] order = config.TerrainOrder;

            for (int i = 0; i < thresholds.Length; i++)
            {
                if (value < thresholds[i])
                    return order[i];
            }
            return order[^1]; // Highest band
        }

        // ---------------------------------------------------------
        // Step 2: Terrain Validation
        // ---------------------------------------------------------

        /// <summary>
        /// Ensures no single terrain type exceeds maxFraction of total tiles.
        /// Prevents degenerate maps (90% water, etc.).
        /// </summary>
        private static bool ValidateTerrainDistribution(
            Dictionary<HexCoordinates, WorldTile> map, ProceduralMapConfig config)
        {
            var counts = new Dictionary<TerrainType, int>();
            foreach (var tile in map.Values)
            {
                if (!counts.ContainsKey(tile.Terrain))
                    counts[tile.Terrain] = 0;
                counts[tile.Terrain]++;
            }

            int total = map.Count;
            int maxAllowed = Mathf.CeilToInt(total * config.MaxTerrainFraction);

            foreach (var kvp in counts)
            {
                if (kvp.Value > maxAllowed)
                {
                    Debug.Log($"[ProceduralMapBuilder] Terrain '{kvp.Key}' has {kvp.Value}/{total} tiles " +
                             $"(max {maxAllowed}). Distribution invalid.");
                    return false;
                }
            }

            // Also verify all 5 terrain types are present
            if (counts.Count < 5)
            {
                Debug.Log($"[ProceduralMapBuilder] Only {counts.Count}/5 terrain types present. " +
                         "Distribution invalid.");
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------
        // Step 3: Structure Placement
        // ---------------------------------------------------------

        /// <summary>
        /// Places structures (StandingStone, AbandonedShack) randomly
        /// on non-Water tiles, respecting minimum distance constraint.
        /// </summary>
        private static bool PlaceStructures(
            Dictionary<HexCoordinates, WorldTile> map,
            ProceduralMapConfig config,
            System.Random rng)
        {
            // Collect eligible tiles (not Water)
            var eligible = new List<HexCoordinates>();
            foreach (var kvp in map)
            {
                if (kvp.Value.Terrain != TerrainType.Water)
                    eligible.Add(kvp.Key);
            }

            var placed = new List<HexCoordinates>();

            // Place Standing Stones
            for (int i = 0; i < config.StandingStoneCount; i++)
            {
                var chosen = PickRandomDistant(eligible, placed,
                    config.MinStructureDistance, rng);
                if (chosen == null) return false;

                var coords = chosen.Value;
                var tile = map[coords];
                tile.Structure = StructureType.StandingStone;
                map[coords] = tile;
                placed.Add(coords);
            }

            // Place Abandoned Shacks
            for (int i = 0; i < config.AbandonedShackCount; i++)
            {
                var chosen = PickRandomDistant(eligible, placed,
                    config.MinStructureDistance, rng);
                if (chosen == null) return false;

                var coords = chosen.Value;
                var tile = map[coords];
                tile.Structure = StructureType.AbandonedShack;
                map[coords] = tile;
                placed.Add(coords);
            }

            return true;
        }

        // ---------------------------------------------------------
        // Step 4: Animal Territories
        // ---------------------------------------------------------

        /// <summary>
        /// Places animal territory centers, then paints nearby tiles
        /// within radius. Centers must respect minimum distance and avoid
        /// blocked terrains.
        /// </summary>
        private static bool PlaceAnimalTerritories(
            Dictionary<HexCoordinates, WorldTile> map,
            ProceduralMapConfig config,
            System.Random rng)
        {
            // Eligible: non-blocked terrain, no structure
            var blocked = new HashSet<TerrainType>(config.AnimalBlockedTerrains);
            var eligible = new List<HexCoordinates>();
            foreach (var kvp in map)
            {
                if (!blocked.Contains(kvp.Value.Terrain) &&
                    kvp.Value.Structure == StructureType.None)
                {
                    eligible.Add(kvp.Key);
                }
            }

            var centers = new List<HexCoordinates>();

            // Place Bear territories
            for (int i = 0; i < config.BearTerritoryCount; i++)
            {
                if (!PlaceTerritory(map, eligible, centers, AnimalType.Bear,
                    config.AnimalTerritoryRadius, config.MinTerritoryDistance,
                    blocked, rng))
                    return false;
            }

            // Place Cougar territories
            for (int i = 0; i < config.CougarTerritoryCount; i++)
            {
                if (!PlaceTerritory(map, eligible, centers, AnimalType.Cougar,
                    config.AnimalTerritoryRadius, config.MinTerritoryDistance,
                    blocked, rng))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Picks a center tile and paints all tiles within radius with the animal type.
        /// </summary>
        private static bool PlaceTerritory(
            Dictionary<HexCoordinates, WorldTile> map,
            List<HexCoordinates> eligible,
            List<HexCoordinates> existingCenters,
            AnimalType animal,
            int radius,
            int minDistance,
            HashSet<TerrainType> blockedTerrains,
            System.Random rng)
        {
            var center = PickRandomDistant(eligible, existingCenters, minDistance, rng);
            if (center == null) return false;

            var centerCoords = center.Value;
            existingCenters.Add(centerCoords);

            // Paint center + neighbors within radius
            PaintTerritory(map, centerCoords, animal, radius, blockedTerrains);
            return true;
        }

        /// <summary>
        /// Sets the animal type on all tiles within `radius` hexes of the center,
        /// skipping tiles on blocked terrain or already occupied by another animal.
        /// </summary>
        private static void PaintTerritory(
            Dictionary<HexCoordinates, WorldTile> map,
            HexCoordinates center,
            AnimalType animal,
            int radius,
            HashSet<TerrainType> blockedTerrains)
        {
            foreach (var kvp in map)
            {
                int dist = center.DistanceTo(kvp.Key);
                if (dist > radius) continue;

                var tile = kvp.Value;

                // Don't overwrite existing animal or paint on blocked terrain
                if (tile.Animal != AnimalType.None) continue;
                if (blockedTerrains.Contains(tile.Terrain)) continue;

                tile.Animal = animal;
                map[kvp.Key] = tile;
            }
        }

        // ---------------------------------------------------------
        // Utility
        // ---------------------------------------------------------

        /// <summary>
        /// Picks a random coordinate from `eligible` that is at least
        /// `minDistance` hexes away from all `existing` coordinates.
        /// Returns null if no valid position found.
        /// </summary>
        private static HexCoordinates? PickRandomDistant(
            List<HexCoordinates> eligible,
            List<HexCoordinates> existing,
            int minDistance,
            System.Random rng)
        {
            // Build filtered list
            var candidates = new List<HexCoordinates>();
            foreach (var coord in eligible)
            {
                bool valid = true;
                foreach (var placed in existing)
                {
                    if (coord.DistanceTo(placed) < minDistance)
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid) candidates.Add(coord);
            }

            if (candidates.Count == 0) return null;

            return candidates[rng.Next(candidates.Count)];
        }
    }
}
