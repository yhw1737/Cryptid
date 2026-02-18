using Cryptid.Core;
using Cryptid.Systems.Turn;

namespace Cryptid.Systems.Gameplay
{
    /// <summary>
    /// Command pattern interface for player actions.
    /// Each player action is encapsulated as a command object
    /// for undo support, network replay, and action logging.
    /// </summary>
    public interface IGameCommand
    {
        /// <summary> Human-readable description for debug logging. </summary>
        string Description { get; }

        /// <summary> Executes the command. </summary>
        void Execute();

        /// <summary> Undoes the command (for replay/undo systems). </summary>
        void Undo();
    }

    /// <summary>
    /// Command: Player asks another player about a tile.
    /// "Does the Cryptid live at [coordinates]?"
    /// </summary>
    public class QuestionCommand : IGameCommand
    {
        private readonly TurnManager _turnManager;
        private readonly HexCoordinates _tileCoords;
        private readonly int _targetPlayerIndex;

        public string Description =>
            $"Player {_turnManager.CurrentPlayerIndex + 1} → Player {_targetPlayerIndex + 1}: " +
            $"Question at {_tileCoords}";

        public QuestionCommand(TurnManager turnManager, HexCoordinates tileCoords, int targetPlayerIndex)
        {
            _turnManager = turnManager;
            _tileCoords = tileCoords;
            _targetPlayerIndex = targetPlayerIndex;
        }

        public void Execute()
        {
            _turnManager.SubmitQuestion(_tileCoords, _targetPlayerIndex);
        }

        public void Undo()
        {
            // Question undo would need to restore turn state
            // Placeholder for future implementation
            UnityEngine.Debug.Log($"[QuestionCommand] Undo not yet supported.");
        }
    }

    /// <summary>
    /// Command: Player searches a tile to find the Cryptid.
    /// This is the "final answer" attempt.
    /// </summary>
    public class SearchCommand : IGameCommand
    {
        private readonly TurnManager _turnManager;
        private readonly HexCoordinates _tileCoords;

        public string Description =>
            $"Player {_turnManager.CurrentPlayerIndex + 1}: Search at {_tileCoords}";

        public SearchCommand(TurnManager turnManager, HexCoordinates tileCoords)
        {
            _turnManager = turnManager;
            _tileCoords = tileCoords;
        }

        public void Execute()
        {
            _turnManager.SubmitSearch(_tileCoords);
        }

        public void Undo()
        {
            UnityEngine.Debug.Log($"[SearchCommand] Undo not yet supported.");
        }
    }
}
