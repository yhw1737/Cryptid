namespace Cryptid.Systems.Turn
{
    /// <summary>
    /// Provides read-only access to turn state.
    /// Implemented by both <see cref="TurnManager"/> (local mode)
    /// and <c>NetworkGameManager</c> (network mode)
    /// so that UI can bind to either source uniformly.
    /// </summary>
    public interface ITurnState
    {
        /// <summary>Index of the player whose turn it is (0-based).</summary>
        int CurrentPlayerIndex { get; }

        /// <summary>Sequential turn number (starts at 1).</summary>
        int TurnNumber { get; }

        /// <summary>Current sub-phase within the turn.</summary>
        TurnPhase CurrentPhase { get; }

        /// <summary>Total number of players.</summary>
        int PlayerCount { get; }
    }
}
