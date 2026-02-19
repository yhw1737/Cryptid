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

        /// <summary>
        /// Penalty after a failed search.
        /// The searcher must place a cube on a tile where their own clue does NOT match.
        /// </summary>
        PenaltyPlacement = 5,
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

        /// <summary>
        /// Fired when the searcher places their disc at the start of a search.
        /// Args: (playerIndex, tileCoords).
        /// </summary>
        public event Action<int, HexCoordinates> OnSearchDiscPlaced;

        /// <summary>
        /// Fired for each verifier during clockwise search verification.
        /// Args: (verifierIndex, tileCoords, result).
        /// YES → verifier places disc. NO → verifier places cube, search stops.
        /// </summary>
        public event Action<int, HexCoordinates, bool> OnSearchVerification;

        /// <summary>
        /// Fired when the searcher places a penalty cube after a failed search.
        /// Args: (playerIndex, tileCoords).
        /// Per Cryptid rules, the searcher must place a cube on a tile
        /// where their OWN clue does NOT match.
        /// </summary>
        public event Action<int, HexCoordinates> OnPenaltyCubePlaced;

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

            string resultStr = result ? "YES (disc)" : "NO (cube)";
            Debug.Log($"[TurnManager] Player {_questionTargetPlayer + 1} responds: {resultStr} " +
                     $"(Clue: {clue.Description})");

            OnResponseGiven?.Invoke(_questionTargetPlayer, _questionTile, result);

            if (result)
            {
                // YES → no penalty, end turn
                EndTurn();
            }
            else
            {
                // NO → asker must also place a penalty cube
                // on a tile where their OWN clue does NOT match.
                Debug.Log($"[TurnManager] Response was NO — Player {_currentPlayerIndex + 1} " +
                         $"must place a penalty cube on a non-matching tile.");
                SetPhase(TurnPhase.PenaltyPlacement);
            }
        }

        /// <summary>
        /// Player performs a Search: guesses the Cryptid's location.
        /// 
        /// Flow (per spec 5.3.B):
        ///   1. Searcher places their disc on the tile.
        ///   2. Clockwise verification: each other player checks their clue.
        ///      - YES → verifier places disc, continue.
        ///      - NO  → verifier places cube, search STOPS.
        ///   3. All YES → searcher wins!
        ///   4. Any NO  → searcher must place penalty cube on a non-matching tile.
        /// </summary>
        public void SubmitSearch(HexCoordinates tileCoords,
            IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            if (_currentPhase != TurnPhase.Search)
            {
                Debug.LogWarning($"[TurnManager] Cannot search in phase {_currentPhase}.");
                return;
            }

            if (!worldMap.TryGetValue(tileCoords, out WorldTile tileData))
            {
                Debug.LogError($"[TurnManager] Search: Tile {tileCoords} not found!");
                return;
            }

            // Step 1: Searcher places their disc
            Debug.Log($"[TurnManager] Player {_currentPlayerIndex + 1} searches tile {tileCoords}.");
            OnSearchDiscPlaced?.Invoke(_currentPlayerIndex, tileCoords);

            // Step 2: Clockwise verification by other players
            for (int i = 1; i < _playerCount; i++)
            {
                int verifier = (_currentPlayerIndex + i) % _playerCount;
                var clue = _puzzle.PlayerClues[verifier];
                bool result = clue.Check(tileData, worldMap);

                string verdict = result ? "YES (disc)" : "NO (cube)";
                Debug.Log($"[TurnManager] Verification: Player {verifier + 1} says {verdict} " +
                         $"(Clue: {clue.Description})");

                // Fire event so tokens are placed
                OnSearchVerification?.Invoke(verifier, tileCoords, result);

                if (!result)
                {
                    // Search denied by this verifier
                    Debug.Log($"[TurnManager] Search DENIED by Player {verifier + 1}. " +
                             $"Player {_currentPlayerIndex + 1} must place penalty cube.");
                    OnSearchPerformed?.Invoke(_currentPlayerIndex, tileCoords, false);
                    SetPhase(TurnPhase.PenaltyPlacement);
                    return;
                }
            }

            // Step 3: All players confirmed — searcher wins!
            Debug.Log($"[TurnManager] All players confirmed! " +
                     $"Player {_currentPlayerIndex + 1} found the Cryptid at {tileCoords}!");
            OnSearchPerformed?.Invoke(_currentPlayerIndex, tileCoords, true);
            OnGameWon?.Invoke(_currentPlayerIndex);
        }

        /// <summary>
        /// Submits a penalty cube placement after a failed search.
        /// The searcher must choose a tile where their OWN clue does NOT match.
        /// </summary>
        /// <param name="tileCoords">The tile to place the penalty cube on.</param>
        /// <param name="worldMap">The world map for clue evaluation.</param>
        /// <returns>True if accepted, false if the tile matches the searcher's clue (invalid).</returns>
        public bool SubmitPenaltyCube(HexCoordinates tileCoords,
            IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap)
        {
            if (_currentPhase != TurnPhase.PenaltyPlacement)
            {
                Debug.LogWarning($"[TurnManager] Cannot submit penalty cube in phase {_currentPhase}.");
                return false;
            }

            // Validate: tile must NOT match the searcher's own clue
            var clue = _puzzle.PlayerClues[_currentPlayerIndex];

            if (!worldMap.TryGetValue(tileCoords, out WorldTile tile))
            {
                Debug.LogError($"[TurnManager] Penalty: Tile {tileCoords} not found in world map!");
                return false;
            }

            if (clue.Check(tile, worldMap))
            {
                Debug.LogWarning($"[TurnManager] Cannot place penalty cube on a tile that " +
                                 $"matches your clue! Choose a tile where your clue does NOT match.");
                return false;
            }

            Debug.Log($"[TurnManager] Player {_currentPlayerIndex + 1} places penalty cube at {tileCoords}.");

            OnPenaltyCubePlaced?.Invoke(_currentPlayerIndex, tileCoords);

            EndTurn();
            return true;
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
