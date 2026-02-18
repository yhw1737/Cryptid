using System;
using UnityEngine;

namespace Cryptid.Data
{
    /// <summary>
    /// ScriptableObject that maps TerrainType and StructureType to visual prefabs.
    /// The MapGenerator uses this to instantiate the correct 3D models for each tile.
    /// 
    /// Separates data (MapPieceData) from visuals (this) for flexibility.
    /// Create via: Assets > Create > Cryptid > Tile Visual Config
    /// </summary>
    [CreateAssetMenu(
        fileName = "TileVisualConfig",
        menuName = "Cryptid/Tile Visual Config",
        order = 1)]
    public class TileVisualConfig : ScriptableObject
    {
        [Header("Terrain Prefabs")]
        [Tooltip("One entry per TerrainType. The prefab to instantiate as the tile base.")]
        [SerializeField] private TerrainPrefabEntry[] _terrainPrefabs;

        [Header("Structure Prefabs")]
        [Tooltip("One entry per StructureType (excluding None). Spawned on top of terrain.")]
        [SerializeField] private StructurePrefabEntry[] _structurePrefabs;

        [Header("Fallback")]
        [Tooltip("Default prefab if a terrain/structure mapping is missing")]
        [SerializeField] private GameObject _fallbackPrefab;

        // ---------------------------------------------------------
        // Lookup Methods
        // ---------------------------------------------------------

        /// <summary>
        /// Returns the prefab for the given terrain type.
        /// Falls back to _fallbackPrefab if not found.
        /// </summary>
        public GameObject GetTerrainPrefab(TerrainType terrain)
        {
            if (_terrainPrefabs != null)
            {
                foreach (var entry in _terrainPrefabs)
                {
                    if (entry.Terrain == terrain && entry.Prefab != null)
                        return entry.Prefab;
                }
            }

            Debug.LogWarning($"[TileVisualConfig] No prefab for terrain: {terrain}. Using fallback.");
            return _fallbackPrefab;
        }

        /// <summary>
        /// Returns the prefab for the given structure type.
        /// Returns null for StructureType.None.
        /// </summary>
        public GameObject GetStructurePrefab(StructureType structure)
        {
            if (structure == StructureType.None)
                return null;

            if (_structurePrefabs != null)
            {
                foreach (var entry in _structurePrefabs)
                {
                    if (entry.Structure == structure && entry.Prefab != null)
                        return entry.Prefab;
                }
            }

            Debug.LogWarning($"[TileVisualConfig] No prefab for structure: {structure}.");
            return null;
        }

        /// <summary>
        /// Returns a debug color for each terrain type.
        /// Used by Gizmos and visual debuggers when prefabs aren't loaded.
        /// </summary>
        public static Color GetTerrainDebugColor(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Desert   => new Color(0.93f, 0.79f, 0.46f), // Sandy yellow
                TerrainType.Forest   => new Color(0.18f, 0.55f, 0.20f), // Dark green
                TerrainType.Water    => new Color(0.25f, 0.52f, 0.85f), // Blue
                TerrainType.Swamp    => new Color(0.42f, 0.50f, 0.30f), // Murky green
                TerrainType.Mountain => new Color(0.55f, 0.55f, 0.55f), // Gray
                _                    => Color.magenta                    // Error
            };
        }
    }

    /// <summary>
    /// Maps a TerrainType to a prefab reference.
    /// </summary>
    [Serializable]
    public struct TerrainPrefabEntry
    {
        public TerrainType Terrain;
        public GameObject Prefab;
    }

    /// <summary>
    /// Maps a StructureType to a prefab reference.
    /// </summary>
    [Serializable]
    public struct StructurePrefabEntry
    {
        public StructureType Structure;
        public GameObject Prefab;
    }
}
