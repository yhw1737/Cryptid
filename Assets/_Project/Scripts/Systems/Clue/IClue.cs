using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;

namespace Cryptid.Systems.Clue
{
    /// <summary>
    /// Interface for all clue types in the Cryptid deduction game.
    /// Each clue defines a rule that a tile either satisfies or does not.
    /// The Solver combines multiple clues to find the single valid "Cryptid Habitat".
    /// </summary>
    public interface IClue
    {
        /// <summary>
        /// Human-readable description of this clue (e.g., "Within 2 hexes of Water").
        /// Displayed in the debug UI and test runner.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Evaluates whether the given tile satisfies this clue.
        /// </summary>
        /// <param name="tile">The tile to evaluate.</param>
        /// <param name="worldMap">Full world map for distance/neighbor queries.</param>
        /// <returns>True if the tile satisfies this clue's condition.</returns>
        bool Check(WorldTile tile, IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap);
    }
}
