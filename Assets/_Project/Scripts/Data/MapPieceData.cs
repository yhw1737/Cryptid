using UnityEngine;

namespace Cryptid.Data
{
    /// <summary>
    /// ScriptableObject defining a single map piece (one of 6 board sections).
    /// Contains an array of TileData in local offset coordinates.
    /// 
    /// At runtime, the MapGenerator reads these pieces, applies rotation/translation,
    /// and assembles them into the full world map.
    /// 
    /// Create via: Assets > Create > Cryptid > Map Piece Data
    /// </summary>
    [CreateAssetMenu(
        fileName = "MapPiece_New",
        menuName = "Cryptid/Map Piece Data",
        order = 0)]
    public class MapPieceData : ScriptableObject
    {
        [Header("Piece Identity")]
        [Tooltip("Unique identifier for this map piece (1-6)")]
        [SerializeField] private int _pieceId;

        [Tooltip("Human-readable name for editor reference")]
        [SerializeField] private string _pieceName;

        [Header("Tile Definitions")]
        [Tooltip("All tiles in this piece, using local offset coordinates")]
        [SerializeField] private TileData[] _tiles;

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary> Unique piece identifier (1-6). </summary>
        public int PieceId => _pieceId;

        /// <summary> Display name for this piece. </summary>
        public string PieceName => _pieceName;

        /// <summary> All tiles defined in this piece. </summary>
        public TileData[] Tiles => _tiles;

        /// <summary> Number of tiles in this piece. </summary>
        public int TileCount => _tiles != null ? _tiles.Length : 0;

        // ---------------------------------------------------------
        // Validation
        // ---------------------------------------------------------

        private void OnValidate()
        {
            if (_pieceId < 1 || _pieceId > 6)
            {
                Debug.LogWarning($"[MapPieceData] PieceId should be 1-6, got {_pieceId}.", this);
            }

            if (_tiles == null || _tiles.Length == 0)
            {
                Debug.LogWarning($"[MapPieceData] '{_pieceName}' has no tiles defined.", this);
            }
        }
    }
}
