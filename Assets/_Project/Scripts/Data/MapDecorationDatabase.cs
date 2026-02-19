using System.Collections.Generic;
using UnityEngine;

namespace Cryptid.Data
{
    /// <summary>
    /// Runtime database that loads terrain decoration, structure, and animal
    /// prefabs from Resources/MapDecorations at startup.
    /// 
    /// Folder layout expected:
    ///   MapDecorations/Forest/   → tree/bush/grass prefabs
    ///   MapDecorations/Desert/   → rock/grass prefabs
    ///   MapDecorations/Swamp/    → mushroom/stump/branch prefabs
    ///   MapDecorations/Mountain/ → large rock prefabs
    ///   MapDecorations/Structure/StandingStone  → standing stone model
    ///   (AbandonedShack, Bear, Cougar → procedural placeholders)
    /// </summary>
    public static class MapDecorationDatabase
    {
        // ---------------------------------------------------------
        // Cached Prefab Arrays
        // ---------------------------------------------------------

        private static Dictionary<TerrainType, GameObject[]> _terrainDecorations;
        private static GameObject _standingStonePrefab;
        private static bool _loaded;

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Ensures all prefabs are loaded from Resources. Safe to call multiple times.
        /// </summary>
        public static void Load()
        {
            if (_loaded) return;

            _terrainDecorations = new Dictionary<TerrainType, GameObject[]>();

            // Forest decorations (trees, bushes, grass)
            var forest = Resources.LoadAll<GameObject>("MapDecorations/Forest");
            if (forest.Length > 0) _terrainDecorations[TerrainType.Forest] = forest;

            // Desert decorations (sparse rocks, dry grass)
            var desert = Resources.LoadAll<GameObject>("MapDecorations/Desert");
            if (desert.Length > 0) _terrainDecorations[TerrainType.Desert] = desert;

            // Swamp decorations (mushrooms, stumps, branches)
            var swamp = Resources.LoadAll<GameObject>("MapDecorations/Swamp");
            if (swamp.Length > 0) _terrainDecorations[TerrainType.Swamp] = swamp;

            // Mountain decorations (large rocks)
            var mountain = Resources.LoadAll<GameObject>("MapDecorations/Mountain");
            if (mountain.Length > 0) _terrainDecorations[TerrainType.Mountain] = mountain;

            // Structure: StandingStone
            _standingStonePrefab = Resources.Load<GameObject>("MapDecorations/Structure/StandingStone");

            int total = 0;
            foreach (var kvp in _terrainDecorations) total += kvp.Value.Length;
            Debug.Log($"[MapDecorationDB] Loaded {total} decoration prefabs across " +
                      $"{_terrainDecorations.Count} terrain types. " +
                      $"StandingStone: {(_standingStonePrefab != null ? "OK" : "MISSING")}");

            _loaded = true;
        }

        /// <summary>
        /// Returns a random decoration prefab for the given terrain, or null if none.
        /// </summary>
        public static GameObject GetRandomDecoration(TerrainType terrain, System.Random rng)
        {
            if (!_loaded) Load();

            if (_terrainDecorations.TryGetValue(terrain, out var prefabs) && prefabs.Length > 0)
                return prefabs[rng.Next(prefabs.Length)];

            return null;
        }

        /// <summary>
        /// Returns all decoration prefabs for the given terrain (for weighted selection).
        /// </summary>
        public static GameObject[] GetDecorations(TerrainType terrain)
        {
            if (!_loaded) Load();

            return _terrainDecorations.TryGetValue(terrain, out var prefabs)
                ? prefabs
                : System.Array.Empty<GameObject>();
        }

        /// <summary>
        /// Returns the StandingStone prefab, or null if not found.
        /// </summary>
        public static GameObject GetStandingStonePrefab()
        {
            if (!_loaded) Load();
            return _standingStonePrefab;
        }

        /// <summary>
        /// Checks whether decoration prefabs exist for a given terrain type.
        /// </summary>
        public static bool HasDecorations(TerrainType terrain)
        {
            if (!_loaded) Load();
            return _terrainDecorations.ContainsKey(terrain);
        }
    }
}
