using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;

namespace Cryptid.Systems.Clue
{
    // =============================================================
    // Terrain-based Clues
    // =============================================================

    /// <summary>
    /// Clue: "The Cryptid lives on [Terrain]."
    /// Checks if the tile's terrain matches the specified type.
    /// </summary>
    public class OnTerrainClue : IClue
    {
        private readonly TerrainType _terrain;

        public string Description => L.TerrainName(_terrain) +
            (L.CurrentLanguage == L.Language.KR ? " 위" : "");

        public OnTerrainClue(TerrainType terrain)
        {
            _terrain = terrain;
        }

        public bool Check(WorldTile tile, IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            return tile.Terrain == _terrain;
        }
    }

    /// <summary>
    /// Clue: "The Cryptid lives on [TerrainA] or [TerrainB]."
    /// Checks if the tile's terrain matches either of two types.
    /// Common in Cryptid: "On Desert or Forest".
    /// </summary>
    public class OnTerrainPairClue : IClue
    {
        private readonly TerrainType _terrainA;
        private readonly TerrainType _terrainB;

        public string Description => L.CurrentLanguage == L.Language.KR
            ? $"{L.TerrainName(_terrainA)} 또는 {L.TerrainName(_terrainB)} 위"
            : $"On {L.TerrainName(_terrainA)} or {L.TerrainName(_terrainB)}";

        public OnTerrainPairClue(TerrainType terrainA, TerrainType terrainB)
        {
            _terrainA = terrainA;
            _terrainB = terrainB;
        }

        public bool Check(WorldTile tile, IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            return tile.Terrain == _terrainA || tile.Terrain == _terrainB;
        }
    }

    // =============================================================
    // Distance-based Clues
    // =============================================================

    /// <summary>
    /// Clue: "The Cryptid lives within [N] hexes of [Terrain]."
    /// Checks all tiles within distance N for the target terrain.
    /// Distance 0 means "directly on that terrain".
    /// </summary>
    public class WithinDistanceOfTerrainClue : IClue
    {
        private readonly TerrainType _terrain;
        private readonly int _distance;

        public string Description => _distance == 0
            ? (L.CurrentLanguage == L.Language.KR
                ? $"{L.TerrainName(_terrain)} 위"
                : $"On {L.TerrainName(_terrain)}")
            : (L.CurrentLanguage == L.Language.KR
                ? $"{L.TerrainName(_terrain)}에서 {_distance}칸 이내"
                : $"Within {_distance} hex(es) of {L.TerrainName(_terrain)}");

        public WithinDistanceOfTerrainClue(TerrainType terrain, int distance)
        {
            _terrain = terrain;
            _distance = distance;
        }

        public bool Check(WorldTile tile, IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            // Get all hex positions within range
            var hexesInRange = HexUtility.GetHexesInRange(tile.Coordinates, _distance);

            foreach (var hex in hexesInRange)
            {
                if (worldMap.TryGetValue(hex, out WorldTile neighbor))
                {
                    if (neighbor.Terrain == _terrain)
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Clue: "The Cryptid lives within [N] hexes of a [Structure]."
    /// Checks all tiles within distance N for the target structure.
    /// </summary>
    public class WithinDistanceOfStructureClue : IClue
    {
        private readonly StructureType _structure;
        private readonly int _distance;

        public string Description => _distance == 0
            ? (L.CurrentLanguage == L.Language.KR
                ? $"{L.StructureName(_structure)} 위"
                : $"On {L.StructureName(_structure)}")
            : (L.CurrentLanguage == L.Language.KR
                ? $"{L.StructureName(_structure)}에서 {_distance}칸 이내"
                : $"Within {_distance} hex(es) of {L.StructureName(_structure)}");

        public WithinDistanceOfStructureClue(StructureType structure, int distance)
        {
            _structure = structure;
            _distance = distance;
        }

        public bool Check(WorldTile tile, IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            var hexesInRange = HexUtility.GetHexesInRange(tile.Coordinates, _distance);

            foreach (var hex in hexesInRange)
            {
                if (worldMap.TryGetValue(hex, out WorldTile neighbor))
                {
                    if (neighbor.Structure == _structure)
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Clue: "The Cryptid lives within [N] hexes of [Animal] territory."
    /// Checks all tiles within distance N for the target animal.
    /// </summary>
    public class WithinDistanceOfAnimalClue : IClue
    {
        private readonly AnimalType _animal;
        private readonly int _distance;

        public string Description => _distance == 0
            ? (L.CurrentLanguage == L.Language.KR
                ? $"{L.AnimalName(_animal)} 영역 위"
                : $"On {L.AnimalName(_animal)} territory")
            : (L.CurrentLanguage == L.Language.KR
                ? $"{L.AnimalName(_animal)} 영역에서 {_distance}칸 이내"
                : $"Within {_distance} hex(es) of {L.AnimalName(_animal)} territory");

        public WithinDistanceOfAnimalClue(AnimalType animal, int distance)
        {
            _animal = animal;
            _distance = distance;
        }

        public bool Check(WorldTile tile, IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            var hexesInRange = HexUtility.GetHexesInRange(tile.Coordinates, _distance);

            foreach (var hex in hexesInRange)
            {
                if (worldMap.TryGetValue(hex, out WorldTile neighbor))
                {
                    if (neighbor.Animal == _animal)
                        return true;
                }
            }

            return false;
        }
    }

    // =============================================================
    // Negation Clue (Decorator Pattern)
    // =============================================================

    /// <summary>
    /// Inverts any clue: "NOT [inner clue]".
    /// Example: Wrapping OnTerrainClue(Forest) produces "Not on Forest".
    /// Useful for constructing complex composite clues.
    /// </summary>
    public class NotClue : IClue
    {
        private readonly IClue _inner;

        public string Description => L.CurrentLanguage == L.Language.KR
            ? $"{_inner.Description} 아님"
            : $"NOT ({_inner.Description})";

        public NotClue(IClue inner)
        {
            _inner = inner;
        }

        public bool Check(WorldTile tile, IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            return !_inner.Check(tile, worldMap);
        }
    }

    // =============================================================
    // Composite Clue (OR combination)
    // =============================================================

    /// <summary>
    /// Combines two clues with OR logic: "ClueA OR ClueB".
    /// Example: "On Desert OR within 1 hex of Water".
    /// The Solver itself applies AND logic between assigned clues,
    /// so OR must be expressed within a single clue using this wrapper.
    /// </summary>
    public class OrClue : IClue
    {
        private readonly IClue _clueA;
        private readonly IClue _clueB;

        public string Description => L.CurrentLanguage == L.Language.KR
            ? $"({_clueA.Description}) 또는 ({_clueB.Description})"
            : $"({_clueA.Description}) OR ({_clueB.Description})";

        public OrClue(IClue clueA, IClue clueB)
        {
            _clueA = clueA;
            _clueB = clueB;
        }

        public bool Check(WorldTile tile, IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            return _clueA.Check(tile, worldMap) || _clueB.Check(tile, worldMap);
        }
    }
}
