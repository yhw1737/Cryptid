using System;
using UnityEngine;
using Cryptid.Core;

namespace Cryptid.Data
{
    /// <summary>
    /// ScriptableObject defining how 6 MapPieces are assembled into the full world map.
    /// Specifies which piece goes where, with what rotation and positional offset.
    /// 
    /// Create via: Assets > Create > Cryptid > Map Assembly Config
    /// </summary>
    [CreateAssetMenu(
        fileName = "MapAssemblyConfig_New",
        menuName = "Cryptid/Map Assembly Config",
        order = 2)]
    public class MapAssemblyConfig : ScriptableObject
    {
        [Header("Piece Placements")]
        [Tooltip("Define the 6 piece slots for map assembly")]
        [SerializeField] private PiecePlacement[] _placements;

        /// <summary> All piece placement definitions. </summary>
        public PiecePlacement[] Placements => _placements;

        /// <summary> Number of placements defined. </summary>
        public int PlacementCount => _placements != null ? _placements.Length : 0;

        private void OnValidate()
        {
            if (_placements != null && _placements.Length != 6)
            {
                Debug.LogWarning(
                    $"[MapAssemblyConfig] Expected 6 placements, got {_placements.Length}.", this);
            }
        }
    }

    /// <summary>
    /// Defines how a single MapPiece is placed on the world map.
    /// Combines a piece reference with rotation and positional offset.
    /// </summary>
    [Serializable]
    public struct PiecePlacement
    {
        [Tooltip("The MapPieceData to place")]
        public MapPieceData Piece;

        [Tooltip("Number of 60° clockwise rotations to apply (0-5)")]
        [Range(0, 5)]
        public int RotationSteps;

        [Tooltip("Translation offset in cube coordinates (applied after rotation)")]
        public Vector3Int OffsetCube;

        /// <summary>
        /// Returns the offset as HexCoordinates.
        /// </summary>
        public HexCoordinates GetOffset()
        {
            return new HexCoordinates(OffsetCube.x, OffsetCube.y, OffsetCube.z);
        }
    }
}
