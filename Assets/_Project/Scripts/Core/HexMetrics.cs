using UnityEngine;

namespace Cryptid.Core
{
    /// <summary>
    /// Constants and layout metrics for flat-top hexagonal grids.
    /// All world-space positioning calculations reference these values.
    /// </summary>
    public static class HexMetrics
    {
        // ---------------------------------------------------------
        // Core Dimensions (Flat-Top Hex)
        // ---------------------------------------------------------

        /// <summary>
        /// Outer radius: center to vertex (corner).
        /// Adjust this value to scale the entire hex grid.
        /// </summary>
        public const float OuterRadius = 1f;

        /// <summary>
        /// Inner radius: center to edge midpoint.
        /// Ratio: inner = outer * sqrt(3) / 2
        /// </summary>
        public static readonly float InnerRadius = OuterRadius * Mathf.Sqrt(3f) / 2f;

        // ---------------------------------------------------------
        // World-Space Conversion
        // ---------------------------------------------------------

        /// <summary>
        /// Converts cube hex coordinates to a flat world-space position (XZ plane).
        /// Uses flat-top hex layout.
        /// Y is always 0 (ground level).
        /// </summary>
        public static Vector3 HexToWorldPosition(HexCoordinates hex)
        {
            // Flat-top hex: 
            // x_world = outerRadius * 3/2 * q
            // z_world = outerRadius * sqrt(3) * (r + q/2)
            // where q = hex.X (axial q), r = hex.Z (axial r)

            float x = OuterRadius * 1.5f * hex.X;
            float z = InnerRadius * 2f * (hex.Z + hex.X * 0.5f);

            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// Returns the 6 corner positions of a flat-top hexagon at the given center.
        /// Corners start at the right vertex (0°) and proceed counter-clockwise.
        /// </summary>
        public static Vector3[] GetHexCorners(Vector3 center)
        {
            var corners = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angleDeg = 60f * i;
                float angleRad = Mathf.Deg2Rad * angleDeg;
                corners[i] = center + new Vector3(
                    OuterRadius * Mathf.Cos(angleRad),
                    0f,
                    OuterRadius * Mathf.Sin(angleRad)
                );
            }
            return corners;
        }
    }
}
