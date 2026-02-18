using System;
using UnityEngine;

namespace Cryptid.Data
{
    /// <summary>
    /// Data for a single hex tile within a MapPiece.
    /// Stored in local (piece-relative) offset coordinates.
    /// Serializable for use in ScriptableObjects and Inspector editing.
    /// </summary>
    [Serializable]
    public struct TileData
    {
        [Tooltip("Local offset coordinate within the map piece (col, row)")]
        public Vector2Int LocalOffset;

        [Tooltip("Terrain type of this tile")]
        public TerrainType Terrain;

        [Tooltip("Structure on this tile (None if empty)")]
        public StructureType Structure;

        [Tooltip("Animal territory this tile belongs to (None if outside any territory)")]
        public AnimalType Animal;

        /// <summary>
        /// Creates a TileData with specified properties.
        /// </summary>
        public TileData(Vector2Int localOffset, TerrainType terrain,
                        StructureType structure = StructureType.None,
                        AnimalType animal = AnimalType.None)
        {
            LocalOffset = localOffset;
            Terrain = terrain;
            Structure = structure;
            Animal = animal;
        }

        public override string ToString()
        {
            string result = $"[{LocalOffset.x},{LocalOffset.y}] {Terrain}";
            if (Structure != StructureType.None)
                result += $" + {Structure}";
            if (Animal != AnimalType.None)
                result += $" ({Animal})";
            return result;
        }
    }
}
