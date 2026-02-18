using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Systems.Clue;

namespace Cryptid.Data
{
    /// <summary>
    /// Immutable result of puzzle generation.
    /// Contains the answer tile, per-player clues, and diagnostic data.
    /// 
    /// Created by PuzzleGenerator and consumed by game systems.
    /// </summary>
    public class PuzzleSetup
    {
        /// <summary> The single correct answer tile (the Cryptid Habitat). </summary>
        public WorldTile AnswerTile { get; }

        /// <summary>
        /// Per-player clue assignments. Index = player index (0-based).
        /// Each player receives exactly one clue.
        /// AND of all clues must yield exactly the AnswerTile.
        /// </summary>
        public IReadOnlyList<IClue> PlayerClues { get; }

        /// <summary> Number of players this puzzle was generated for. </summary>
        public int PlayerCount => PlayerClues.Count;

        /// <summary>
        /// All candidate clues that the answer tile satisfies.
        /// Useful for debugging and understanding clue space.
        /// </summary>
        public IReadOnlyList<IClue> AllCandidateClues { get; }

        /// <summary> Random seed used for generation (for reproducibility). </summary>
        public int Seed { get; }

        public PuzzleSetup(
            WorldTile answerTile,
            IReadOnlyList<IClue> playerClues,
            IReadOnlyList<IClue> allCandidateClues,
            int seed)
        {
            AnswerTile = answerTile;
            PlayerClues = playerClues;
            AllCandidateClues = allCandidateClues;
            Seed = seed;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Puzzle Setup (Seed: {Seed}) ===");
            sb.AppendLine($"Answer: {AnswerTile}");
            sb.AppendLine($"Players: {PlayerCount}");
            for (int i = 0; i < PlayerClues.Count; i++)
            {
                sb.AppendLine($"  Player {i + 1}: {PlayerClues[i].Description}");
            }
            sb.AppendLine($"Candidate clues considered: {AllCandidateClues.Count}");
            return sb.ToString();
        }
    }
}
