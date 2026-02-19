using System.Collections.Generic;
using Cryptid.Data;
using UnityEngine;

namespace Cryptid.Systems.Clue
{
    /// <summary>
    /// Tiers based on how selective (restrictive) a clue is.
    /// Hard clues match fewer tiles, Soft clues match more tiles.
    /// </summary>
    public enum ClueTier
    {
        /// <summary> Matches &lt; 20% of tiles. Very restrictive. </summary>
        Hard,

        /// <summary> Matches 20-60% of tiles. Balanced. </summary>
        Medium,

        /// <summary> Matches &gt; 60% of tiles. Very permissive. </summary>
        Soft,
    }

    /// <summary>
    /// Ensures fair clue distribution by grouping candidate clues into
    /// power tiers (Hard / Medium / Soft) based on valid tile count.
    ///
    /// Players should receive clues from the SAME tier so that no player
    /// has a significantly more or less informative clue than others.
    ///
    /// Tier thresholds (fraction of total map tiles):
    ///   Hard:   &lt; 20%   (highly selective, very informative)
    ///   Medium: 20-60%  (balanced)
    ///   Soft:   &gt; 60%   (broadly permissive, less informative)
    ///
    /// Priority order: Medium → Hard → Soft → unrestricted fallback.
    /// </summary>
    public static class ClueBalancer
    {
        // ---------------------------------------------------------
        // Tier Boundaries
        // ---------------------------------------------------------

        /// <summary> Clues matching fewer tiles than this fraction are Hard. </summary>
        private const float HardThreshold = 0.20f;

        /// <summary> Clues matching more tiles than this fraction are Soft. </summary>
        private const float SoftThreshold = 0.60f;

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Classifies a single clue into a tier based on its pass-set size
        /// relative to total tile count.
        /// </summary>
        public static ClueTier Classify(int validTileCount, int totalTiles)
        {
            float ratio = (float)validTileCount / totalTiles;

            if (ratio < HardThreshold) return ClueTier.Hard;
            if (ratio > SoftThreshold) return ClueTier.Soft;
            return ClueTier.Medium;
        }

        /// <summary>
        /// Groups candidate clue indices into tiers using pre-computed pass-sets.
        /// </summary>
        /// <param name="passSets">Pass-sets for each candidate clue.</param>
        /// <param name="totalTiles">Total number of tiles in the map.</param>
        /// <returns>Dictionary mapping each tier to a list of candidate indices.</returns>
        public static Dictionary<ClueTier, List<int>> GroupByTier(
            List<HashSet<HexCoordinates>> passSets,
            int totalTiles)
        {
            var tiers = new Dictionary<ClueTier, List<int>>
            {
                { ClueTier.Hard,   new List<int>() },
                { ClueTier.Medium, new List<int>() },
                { ClueTier.Soft,   new List<int>() },
            };

            for (int i = 0; i < passSets.Count; i++)
            {
                var tier = Classify(passSets[i].Count, totalTiles);
                tiers[tier].Add(i);
            }

            return tiers;
        }

        /// <summary>
        /// Selects the best tier that has at least <paramref name="requiredCount"/>
        /// candidate clues. Priority: Medium → Hard → Soft.
        /// </summary>
        /// <returns>The chosen tier, or null if no single tier has enough clues.</returns>
        public static ClueTier? SelectBestTier(
            Dictionary<ClueTier, List<int>> tiers,
            int requiredCount)
        {
            // Ordered by preference
            ClueTier[] priority = { ClueTier.Medium, ClueTier.Hard, ClueTier.Soft };

            foreach (var tier in priority)
            {
                if (tiers[tier].Count >= requiredCount)
                    return tier;
            }

            return null;
        }

        /// <summary>
        /// Logs tier distribution for debugging.
        /// </summary>
        public static void LogTierDistribution(
            Dictionary<ClueTier, List<int>> tiers,
            List<HashSet<HexCoordinates>> passSets,
            int totalTiles)
        {
            foreach (var kvp in tiers)
            {
                if (kvp.Value.Count == 0) continue;

                // Compute average pass rate for this tier
                float avgRate = 0f;
                foreach (int idx in kvp.Value)
                    avgRate += (float)passSets[idx].Count / totalTiles;
                avgRate /= kvp.Value.Count;

                Debug.Log($"[ClueBalancer] {kvp.Key}: {kvp.Value.Count} clues " +
                          $"(avg match rate {avgRate * 100f:F0}%)");
            }
        }
    }
}
