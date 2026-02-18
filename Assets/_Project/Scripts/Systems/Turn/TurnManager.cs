using System;
using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;

namespace Cryptid.Systems.Turn
{
    /// <summary>
    /// Sub-phases within a single player's turn.
    /// </summary>
    public enum TurnPhase
    {
        /// <summary> Turn just started. Player chooses an action. </summary>
        ChooseAction = 0,

        /// <summary> Player is selecting a tile to ask about (Question). </summary>
        SelectTile = 1,

        /// <summary> Opponent is responding to the question. </summary>
        WaitForResponse = 2,

        /// <summary> Player chooses to Search (final guess attempt). </summary>
        Search = 3,

        /// <summary> Turn is complete. Advance to next player. </summary>
        TurnEnd = 4,
    }

    /// <summary>
    /// Player action types in Cryptid.
    /// </summary>
    public enum PlayerAction
    {
        /// <summary> Ask another player about a tile ("Could the cryptid be here?"). </summary>
        Question = 0,

        /// <summary> Search a tile to attempt to find the Cryptid. </summary>
        Search = 1,
    }

    /// <summary>
    /// Manages the turn-based flow of the Cryptid game.
    /// 
    /// Handles:
    /// - Player turn order (circular)
    /// - Turn phase transitions (ChooseAction → SelectTile → Response → End)
    /// - Question/Search action execution
    /// - Win condition checking
    /// 
    /// Events are fired for UI and network sync.
    /// 
    /// Usage:
    ///   var turnMgr = new TurnManager(playerCount, puzzleSetup);
    ///   turnMgr.StartFirstTurn();
    ///   // ... game logic calls SubmitAction(), SubmitResponse(), etc.
    /// </summary>
    public class TurnManager
    {
        // ---------------------------------------------------------
        // State
        // ---------------------------------------------------------

        private readonly int _playerCount;
        private readonly PuzzleSetup _puzzle;
        private int _currentPlayerIndex;
        private int _turnNumber;
        private TurnPhase _currentPhase;

        // Question tracking
        private int _questionTargetPlayer;
        private HexCoordinates _questionTile;

        // ---------------------------------------------------------
        // Properties
        // ---------------------------------------------------------

        public int CurrentPlayerIndex => _currentPlayerIndex;
        public int TurnNumber => _turnNumber;
        public TurnPhase CurrentPhase => _currentPhase;
        public int PlayerCount => _playerCount;

        // ---------------------------------------------------------
        // Events
        // ---------------------------------------------------------

        /// <summary> Fired when a new turn begins. Arg: playerIndex. </summary>
        public event Action<int> OnTurnStarted;

        /// <summary> Fired when the turn phase changes. Args: (playerIndex, newPhase). </summary>
        public event Action<int, TurnPhase> OnPhaseChanged;

        /// <summary> Fired when a question is asked. Args: (askingPlayer, targetPlayer, tileCoords). </summary>
        public event Action<int, int, HexCoordinates> OnQuestionAsked;

        /// <summary> Fired when a response is given. Args: (respondingPlayer, tileCoords, result). </summary>
        public event Action<int, HexCoordinates, bool> OnResponseGiven;

        /// <summary> Fired when a search is performed. Args: (playerIndex, tileCoords, isCorrect). </summary>
        public event Action<int, HexCoordinates, bool> OnSearchPerformed;

        /// <summary> Fired when a player wins. Arg: winnerIndex. </summary>
        public event Action<int> OnGameWon;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        public TurnManager(int playerCount, PuzzleSetup puzzle)
        {
            _playerCount = playerCount;
            _puzzle = puzzle;
            _currentPlayerIndex = 0;
            _turnNumber = 0;
            _currentPhase = TurnPhase.ChooseAction;
        }

        // ---------------------------------------------------------
        // Turn Flow
        // ---------------------------------------------------------

        /// <summary>
        /// Starts the first turn of the game.
        /// </summary>
        public void StartFirstTurn()
        {
            _currentPlayerIndex = 0;
            _turnNumber = 1;
            SetPhase(TurnPhase.ChooseAction);
            OnTurnStarted?.Invoke(_currentPlayerIndex);
            Debug.Log($"[TurnManager] Turn {_turnNumber}: Player {_currentPlayerIndex + 1}'s turn.");
        }

        /// <summary>
        /// Player chooses their action for this turn.
        /// </summary>
        public void ChooseAction(PlayerAction action)
        {
            if (_currentPhase != TurnPhase.ChooseAction)
            {
                Debug.LogWarning($"[TurnManager] Cannot choose action in phase {_currentPhase}.");
                return;
            }

            switch (action)
            {
                case PlayerAction.Question:
                    SetPhase(TurnPhase.SelectTile);
                    Debug.Log($"[TurnManager] Player {_currentPlayerIndex + 1} chose to Question.");
                    break;

                case PlayerAction.Search:
                    SetPhase(TurnPhase.Search);
                    Debug.Log($"[TurnManager] Player {_currentPlayerIndex + 1} chose to Search.");
                    break;
            }
        }

        /// <summary>
        /// Submits a Question action: "Does the cryptid live at this tile?"
        /// Directed at a specific opponent.
        /// </summary>
        /// <param name="tileCoords">The tile being asked about.</param>
        /// <param name="targetPlayerIndex">The opponent being asked.</param>
        public void SubmitQuestion(HexCoordinates tileCoords, int targetPlayerIndex)
        {
            if (_currentPhase != TurnPhase.SelectTile)
            {
                Debug.LogWarning($"[TurnManager] Cannot submit question in phase {_currentPhase}.");
                return;
            }

            if (targetPlayerIndex == _currentPlayerIndex)
            {
                Debug.LogWarning("[TurnManager] Cannot ask yourself!");
                return;
            }

            _questionTile = tileCoords;
            _questionTargetPlayer = targetPlayerIndex;

            SetPhase(TurnPhase.WaitForResponse);
            OnQuestionAsked?.Invoke(_currentPlayerIndex, targetPlayerIndex, tileCoords);

            Debug.Log($"[TurnManager] Player {_currentPlayerIndex + 1} asks Player {targetPlayerIndex + 1}: " +
                     $"\"Is the cryptid at {tileCoords}?\"");
        }

        /// <summary>
        /// The target player responds to the question using their clue.
        /// In a real game, this is private — the system auto-evaluates.
        /// </summary>
        public void AutoRespond(IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            if (_currentPhase != TurnPhase.WaitForResponse)
            {
                Debug.LogWarning($"[TurnManager] Cannot respond in phase {_currentPhase}.");
                return;
            }

            // Get the target player's clue
            var clue = _puzzle.PlayerClues[_questionTargetPlayer];

            // Get the tile data
            if (!worldMap.TryGetValue(_questionTile, out WorldTile tile))
            {
                Debug.LogError($"[TurnManager] Tile {_questionTile} not found in world map!");
                return;
            }

            // Evaluate the clue
            bool result = clue.Check(tile, worldMap);

            string resultStr = result ? "YES (cube)" : "NO (disc)";
            Debug.Log($"[TurnManager] Player {_questionTargetPlayer + 1} responds: {resultStr} " +
                     $"(Clue: {clue.Description})");

            OnResponseGiven?.Invoke(_questionTargetPlayer, _questionTile, result);

            // End turn
            EndTurn();
        }

        /// <summary>
        /// Player performs a Search: guesses the Cryptid's location.
        /// If correct, they win. If wrong, they're penalized (in full game, eliminated).
        /// </summary>
        public void SubmitSearch(HexCoordinates tileCoords)
        {
            if (_currentPhase != TurnPhase.Search)
            {
                Debug.LogWarning($"[TurnManager] Cannot search in phase {_currentPhase}.");
                return;
            }

            bool isCorrect = tileCoords.Equals(_puzzle.AnswerTile.Coordinates);

            OnSearchPerformed?.Invoke(_currentPlayerIndex, tileCoords, isCorrect);

            if (isCorrect)
            {
                Debug.Log($"[TurnManager] Player {_currentPlayerIndex + 1} found the Cryptid at {tileCoords}! WINNER!");
                OnGameWon?.Invoke(_currentPlayerIndex);
            }
            else
            {
                Debug.Log($"[TurnManager] Player {_currentPlayerIndex + 1} searched {tileCoords} — WRONG! " +
                         $"(Answer was {_puzzle.AnswerTile.Coordinates})");
                EndTurn();
            }
        }

        // ---------------------------------------------------------
        // Internal
        // ---------------------------------------------------------

        private void EndTurn()
        {
            SetPhase(TurnPhase.TurnEnd);
            AdvanceToNextPlayer();
        }

        private void AdvanceToNextPlayer()
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _playerCount;
            _turnNumber++;

            SetPhase(TurnPhase.ChooseAction);
            OnTurnStarted?.Invoke(_currentPlayerIndex);

            Debug.Log($"[TurnManager] Turn {_turnNumber}: Player {_currentPlayerIndex + 1}'s turn.");
        }

        private void SetPhase(TurnPhase phase)
        {
            _currentPhase = phase;
            OnPhaseChanged?.Invoke(_currentPlayerIndex, phase);
        }
    }
}
