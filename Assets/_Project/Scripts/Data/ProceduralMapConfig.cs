using System;
using UnityEngine;

namespace Cryptid.Data
{
    /// <summary>
    /// Configuration for procedural hex map generation (Spec 5.1).
    /// Controls map size, terrain noise, structure/animal distribution.
    ///
    /// Create via: Assets > Create > Cryptid > Procedural Map Config
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProceduralMapConfig",
        menuName = "Cryptid/Procedural Map Config",
        order = 3)]
    public class ProceduralMapConfig : ScriptableObject
    {
        // ---------------------------------------------------------
        // Map Dimensions
        // ---------------------------------------------------------

        [Header("Map Dimensions")]
        [Tooltip("Number of columns (width)")]
        [Range(4, 20)]
        [SerializeField] private int _width = 12;

        [Tooltip("Number of rows (height)")]
        [Range(4, 15)]
        [SerializeField] private int _height = 9;

        // ---------------------------------------------------------
        // Terrain Generation
        // ---------------------------------------------------------

        [Header("Terrain Noise")]
        [Tooltip("Scale of the primary Perlin noise layer (lower = larger biomes)")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _noiseScale = 0.15f;

        [Tooltip("Second octave scale for detail variation")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _detailScale = 0.35f;

        [Tooltip("Weight of the detail octave (0 = none, 1 = equal to primary)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _detailWeight = 0.25f;

        [Tooltip("Terrain distribution thresholds (sorted ascending). " +
                 "4 values split noise [0,1] into 5 terrain bands.")]
        [SerializeField] private float[] _terrainThresholds = { 0.20f, 0.40f, 0.60f, 0.80f };

        [Tooltip("Terrain type order for the 5 noise bands (low to high)")]
        [SerializeField] private TerrainType[] _terrainOrder =
        {
            TerrainType.Water,
            TerrainType.Swamp,
            TerrainType.Forest,
            TerrainType.Desert,
            TerrainType.Mountain
        };

        // ---------------------------------------------------------
        // Structure Placement
        // ---------------------------------------------------------

        [Header("Structures")]
        [Tooltip("Number of Standing Stones to place")]
        [Range(1, 6)]
        [SerializeField] private int _standingStoneCount = 2;

        [Tooltip("Number of Abandoned Shacks to place")]
        [Range(1, 6)]
        [SerializeField] private int _abandonedShackCount = 2;

        [Tooltip("Minimum hex distance between any two structures")]
        [Range(2, 5)]
        [SerializeField] private int _minStructureDistance = 3;

        // ---------------------------------------------------------
        // Animal Territories
        // ---------------------------------------------------------

        [Header("Animals")]
        [Tooltip("Number of Bear territory centers to place")]
        [Range(1, 4)]
        [SerializeField] private int _bearTerritoryCount = 2;

        [Tooltip("Number of Cougar territory centers to place")]
        [Range(1, 4)]
        [SerializeField] private int _cougarTerritoryCount = 2;

        [Tooltip("Radius (in hexes) of each animal territory from its center")]
        [Range(1, 3)]
        [SerializeField] private int _animalTerritoryRadius = 1;

        [Tooltip("Minimum hex distance between territory centers")]
        [Range(2, 5)]
        [SerializeField] private int _minTerritoryDistance = 3;

        [Tooltip("Terrain types where animals cannot spawn")]
        [SerializeField] private TerrainType[] _animalBlockedTerrains = { TerrainType.Water };

        // ---------------------------------------------------------
        // Validation
        // ---------------------------------------------------------

        [Header("Map Validation")]
        [Tooltip("Maximum fraction of one terrain type (prevent 90% water etc.)")]
        [Range(0.2f, 0.6f)]
        [SerializeField] private float _maxTerrainFraction = 0.40f;

        [Tooltip("Maximum number of generation retries before giving up")]
        [Range(5, 100)]
        [SerializeField] private int _maxRetries = 50;

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        public int Width => _width;
        public int Height => _height;
        public float NoiseScale => _noiseScale;
        public float DetailScale => _detailScale;
        public float DetailWeight => _detailWeight;
        public float[] TerrainThresholds => _terrainThresholds;
        public TerrainType[] TerrainOrder => _terrainOrder;
        public int StandingStoneCount => _standingStoneCount;
        public int AbandonedShackCount => _abandonedShackCount;
        public int MinStructureDistance => _minStructureDistance;
        public int BearTerritoryCount => _bearTerritoryCount;
        public int CougarTerritoryCount => _cougarTerritoryCount;
        public int AnimalTerritoryRadius => _animalTerritoryRadius;
        public int MinTerritoryDistance => _minTerritoryDistance;
        public TerrainType[] AnimalBlockedTerrains => _animalBlockedTerrains;
        public float MaxTerrainFraction => _maxTerrainFraction;
        public int MaxRetries => _maxRetries;

        /// <summary> Total tile count for this map size. </summary>
        public int TotalTiles => _width * _height;

        // ---------------------------------------------------------
        // Validation
        // ---------------------------------------------------------

        private void OnValidate()
        {
            if (_terrainThresholds == null || _terrainThresholds.Length != 4)
            {
                Debug.LogWarning("[ProceduralMapConfig] TerrainThresholds must have exactly 4 values.");
            }

            if (_terrainOrder == null || _terrainOrder.Length != 5)
            {
                Debug.LogWarning("[ProceduralMapConfig] TerrainOrder must have exactly 5 entries " +
                                 "(one per terrain type).");
            }
        }
    }
}
