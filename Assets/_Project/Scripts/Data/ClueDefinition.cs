using UnityEngine;
using Cryptid.Data;
using Cryptid.Systems.Clue;

namespace Cryptid.Data
{
    /// <summary>
    /// Serializable clue category for the ScriptableObject inspector.
    /// Maps to concrete IClue implementations at runtime.
    /// </summary>
    public enum ClueCategory
    {
        /// <summary> On a specific terrain type. </summary>
        OnTerrain = 0,

        /// <summary> On one of two terrain types. </summary>
        OnTerrainPair = 1,

        /// <summary> Within N hexes of a terrain type. </summary>
        WithinDistanceOfTerrain = 2,

        /// <summary> Within N hexes of a structure type. </summary>
        WithinDistanceOfStructure = 3,

        /// <summary> Within N hexes of an animal territory. </summary>
        WithinDistanceOfAnimal = 4,
    }

    /// <summary>
    /// Data-driven clue definition stored as a ScriptableObject.
    /// Allows designers to create/edit clues in the Unity Inspector.
    /// 
    /// Resolves to a concrete IClue instance at runtime via ToClue().
    /// 
    /// Usage:
    /// 1. Create via Assets > Create > Cryptid > Clue Definition
    /// 2. Set Category, then fill in relevant fields
    /// 3. Call ToClue() in game code to get the runtime IClue
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewClue",
        menuName = "Cryptid/Clue Definition",
        order = 10)]
    public class ClueDefinition : ScriptableObject
    {
        [Header("Clue Configuration")]
        [Tooltip("The logical category of this clue")]
        [SerializeField] private ClueCategory _category;

        [Header("Terrain Parameters")]
        [Tooltip("Primary terrain (used by OnTerrain, OnTerrainPair, WithinDistanceOfTerrain)")]
        [SerializeField] private TerrainType _terrainA;

        [Tooltip("Secondary terrain (used by OnTerrainPair only)")]
        [SerializeField] private TerrainType _terrainB;

        [Header("Structure Parameter")]
        [Tooltip("Structure type (used by WithinDistanceOfStructure)")]
        [SerializeField] private StructureType _structure;

        [Header("Animal Parameter")]
        [Tooltip("Animal type (used by WithinDistanceOfAnimal)")]
        [SerializeField] private AnimalType _animal;

        [Header("Distance Parameter")]
        [Tooltip("Max hex distance for distance-based clues (0 = on the tile itself)")]
        [Range(0, 5)]
        [SerializeField] private int _distance = 1;

        [Header("Modifiers")]
        [Tooltip("If true, the clue result is inverted (NOT logic)")]
        [SerializeField] private bool _negate;

        // ---------------------------------------------------------
        // Public Accessors
        // ---------------------------------------------------------

        public ClueCategory Category => _category;
        public bool IsNegated => _negate;

        /// <summary>
        /// Returns a human-readable description based on current settings.
        /// </summary>
        public string GetDescription()
        {
            string desc = _category switch
            {
                ClueCategory.OnTerrain =>
                    $"On {_terrainA}",
                ClueCategory.OnTerrainPair =>
                    $"On {_terrainA} or {_terrainB}",
                ClueCategory.WithinDistanceOfTerrain =>
                    _distance == 0 ? $"On {_terrainA}" : $"Within {_distance} hex(es) of {_terrainA}",
                ClueCategory.WithinDistanceOfStructure =>
                    _distance == 0 ? $"On {_structure}" : $"Within {_distance} hex(es) of {_structure}",
                ClueCategory.WithinDistanceOfAnimal =>
                    _distance == 0 ? $"On {_animal} territory" : $"Within {_distance} hex(es) of {_animal} territory",
                _ => "Unknown Clue"
            };

            return _negate ? $"NOT ({desc})" : desc;
        }

        // ---------------------------------------------------------
        // Runtime Conversion
        // ---------------------------------------------------------

        /// <summary>
        /// Converts this data definition to a runtime IClue instance.
        /// Called once during game setup; the resulting IClue is used for all checks.
        /// </summary>
        public IClue ToClue()
        {
            IClue baseClue = _category switch
            {
                ClueCategory.OnTerrain =>
                    new OnTerrainClue(_terrainA),
                ClueCategory.OnTerrainPair =>
                    new OnTerrainPairClue(_terrainA, _terrainB),
                ClueCategory.WithinDistanceOfTerrain =>
                    new WithinDistanceOfTerrainClue(_terrainA, _distance),
                ClueCategory.WithinDistanceOfStructure =>
                    new WithinDistanceOfStructureClue(_structure, _distance),
                ClueCategory.WithinDistanceOfAnimal =>
                    new WithinDistanceOfAnimalClue(_animal, _distance),
                _ => null
            };

            if (baseClue == null)
            {
                Debug.LogError($"[ClueDefinition] Unknown category: {_category}");
                return null;
            }

            return _negate ? new NotClue(baseClue) : baseClue;
        }
    }
}
