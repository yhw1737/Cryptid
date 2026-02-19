using System;
using System.Collections.Generic;
using System.Linq;
using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Systems.Clue;
using Cryptid.Systems.Turn;
using UnityEngine;

namespace Cryptid.Systems.Gameplay
{
    /// <summary>
    /// Simple AI controller for non-human players in single-player mode.
    /// 
    /// Strategy:
    /// - Always chooses Question (never searches voluntarily).
    /// - Picks a random tile that the AI's own clue matches (to gather info).
    /// - Asks the next player in turn order.
    /// 
    /// The AI does NOT try to optimally deduce the answer.
    /// This is intentional: it provides realistic "opponent turns" that generate
    /// public token information for the human player to use.
    /// 
    /// Usage:
    ///   var ai = new SimpleAIPlayer(turnManager, puzzle, worldMap);
    ///   ai.HumanPlayerIndex = 0;    // Player 1 is human
    ///   // AI auto-acts when TurnManager fires OnTurnStarted for non-human players
    /// </summary>
    public class SimpleAIPlayer
    {
        private readonly TurnManager _turnManager;
        private readonly PuzzleSetup _puzzle;
        private readonly IReadOnlyDictionary<HexCoordinates, WorldTile> _worldMap;
        private readonly System.Random _rng;

        // Cached valid tiles per player (tiles matching that player's clue)
        private readonly Dictionary<int, List<HexCoordinates>> _validTilesCache = new();

        /// <summary>
        /// Index of the human player (won't be auto-played). Default: 0.
        /// </summary>
        public int HumanPlayerIndex { get; set; } = 0;

        /// <summary>
        /// Delay in seconds before the AI acts (for visual readability).
        /// The caller is responsible for implementing the delay (e.g. via coroutine).
        /// </summary>
        public float ActionDelay { get; set; } = 0.6f;

        /// <summary>
        /// Fired when the AI wants to take an action.
        /// Args: (playerIndex, action, targetTile, targetPlayer).
        /// The caller should execute the action with the appropriate delay.
        /// </summary>
        public event Action<int, PlayerAction, HexCoordinates, int> OnAIActionReady;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        public SimpleAIPlayer(
            TurnManager turnManager,
            PuzzleSetup puzzle,
            IReadOnlyDictionary<HexCoordinates, WorldTile> worldMap,
            int? seed = null)
        {
            _turnManager = turnManager;
            _puzzle = puzzle;
            _worldMap = worldMap;
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            // Pre-cache valid tiles for each AI player
            CacheValidTiles();

            // Subscribe to turn start
            _turnManager.OnTurnStarted += HandleTurnStarted;
        }

        /// <summary>Unsubscribes from events. Call when disposing.</summary>
        public void Dispose()
        {
            _turnManager.OnTurnStarted -= HandleTurnStarted;
        }

        // ---------------------------------------------------------
        // AI Logic
        // ---------------------------------------------------------

        private void HandleTurnStarted(int playerIndex)
        {
            // Skip human player
            if (playerIndex == HumanPlayerIndex) return;

            // Decide action and fire event
            DecideAndNotify(playerIndex);
        }

        /// <summary>
        /// Decides what action the AI takes and fires OnAIActionReady.
        /// </summary>
        private void DecideAndNotify(int playerIndex)
        {
            // Strategy: always Question (searching is risky without full deduction)
            var action = PlayerAction.Question;

            // Pick a tile: randomly from tiles matching our clue
            var tile = PickQuestionTile(playerIndex);

            // Pick a target: next player that isn't ourselves
            int target = PickTarget(playerIndex);

            Debug.Log($"[AI] Player {playerIndex + 1} decides: Question P{target + 1} about {tile}");

            OnAIActionReady?.Invoke(playerIndex, action, tile, target);
        }

        /// <summary>
        /// Picks a tile to ask about. Prefers tiles matching the AI's own clue
        /// (since asking about tiles you know match narrows down other clues).
        /// Falls back to any random tile.
        /// </summary>
        private HexCoordinates PickQuestionTile(int playerIndex)
        {
            // Try tiles that match our clue first
            if (_validTilesCache.TryGetValue(playerIndex, out var validTiles) && validTiles.Count > 0)
            {
                return validTiles[_rng.Next(validTiles.Count)];
            }

            // Fallback: any random tile
            var allTiles = _worldMap.Keys.ToList();
            return allTiles[_rng.Next(allTiles.Count)];
        }

        /// <summary>
        /// Picks a target player (not self) to ask.
        /// Simple strategy: next player in turn order.
        /// </summary>
        private int PickTarget(int playerIndex)
        {
            int target = (playerIndex + 1) % _turnManager.PlayerCount;

            // Edge case: if target is self (shouldn't happen with 2+ players)
            if (target == playerIndex)
                target = (target + 1) % _turnManager.PlayerCount;

            return target;
        }

        /// <summary>
        /// Pre-computes which tiles each AI player's clue matches.
        /// </summary>
        private void CacheValidTiles()
        {
            for (int i = 0; i < _puzzle.PlayerCount; i++)
            {
                if (i == HumanPlayerIndex) continue;

                var clue = _puzzle.PlayerClues[i];
                var valid = new List<HexCoordinates>();

                foreach (var kvp in _worldMap)
                {
                    if (clue.Check(kvp.Value, _worldMap))
                    {
                        valid.Add(kvp.Key);
                    }
                }

                _validTilesCache[i] = valid;

                Debug.Log($"[AI] Player {i + 1} clue \"{clue.Description}\" matches {valid.Count} tiles.");
            }
        }
    }
}
