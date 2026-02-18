using Cryptid.Core;

namespace Cryptid.Data
{
    /// <summary>
    /// Runtime representation of a tile on the assembled world map.
    /// Created by MapGenerator when pieces are combined.
    /// Contains both the original data and its global position.
    /// </summary>
    public struct WorldTile
    {
        /// <summary> Global cube coordinates on the world map. </summary>
        public HexCoordinates Coordinates;

        /// <summary> Terrain type of this tile. </summary>
        public TerrainType Terrain;

        /// <summary> Structure on this tile (None if empty). </summary>
        public StructureType Structure;

        /// <summary> Animal territory (None if outside any territory). </summary>
        public AnimalType Animal;

        /// <summary> Which MapPiece this tile originated from (1-6). </summary>
        public int SourcePieceId;

        public WorldTile(HexCoordinates coordinates, TileData source, int sourcePieceId)
        {
            Coordinates = coordinates;
            Terrain = source.Terrain;
            Structure = source.Structure;
            Animal = source.Animal;
            SourcePieceId = sourcePieceId;
        }

        public override string ToString()
        {
            string result = $"{Coordinates} {Terrain}";
            if (Structure != StructureType.None)
                result += $" +{Structure}";
            if (Animal != AnimalType.None)
                result += $" ({Animal})";
            result += $" [Piece {SourcePieceId}]";
            return result;
        }
    }
}
