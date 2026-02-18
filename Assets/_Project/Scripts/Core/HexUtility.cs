using System.Collections.Generic;

namespace Cryptid.Core
{
    /// <summary>
    /// Static utility methods for hex grid operations.
    /// Provides algorithms that operate on HexCoordinates.
    /// </summary>
    public static class HexUtility
    {
        /// <summary>
        /// Returns all hex coordinates within a given radius (inclusive) from the center.
        /// Uses cube coordinate range: -radius ≤ x,y,z ≤ +radius with x+y+z=0.
        /// </summary>
        public static List<HexCoordinates> GetHexesInRange(HexCoordinates center, int radius)
        {
            var results = new List<HexCoordinates>();

            for (int dx = -radius; dx <= radius; dx++)
            {
                // Clamp dy range so that x + y + z = 0 is satisfiable within radius
                int minDy = System.Math.Max(-radius, -dx - radius);
                int maxDy = System.Math.Min(radius, -dx + radius);

                for (int dy = minDy; dy <= maxDy; dy++)
                {
                    int dz = -dx - dy;
                    results.Add(center + new HexCoordinates(dx, dy, dz));
                }
            }

            return results;
        }

        /// <summary>
        /// Returns hex coordinates forming a ring at exactly the given radius from center.
        /// </summary>
        public static List<HexCoordinates> GetHexRing(HexCoordinates center, int radius)
        {
            if (radius <= 0)
                return new List<HexCoordinates> { center };

            var results = new List<HexCoordinates>();

            // Start at the hex in direction 4 (SW) scaled by radius
            HexCoordinates current = center + HexCoordinates.Directions[4] * radius;

            // Walk along each of the 6 edges
            for (int dir = 0; dir < 6; dir++)
            {
                for (int step = 0; step < radius; step++)
                {
                    results.Add(current);
                    current = current.GetNeighbor(dir);
                }
            }

            return results;
        }

        /// <summary>
        /// Returns a straight line of hex coordinates from start to end.
        /// Uses cube coordinate linear interpolation.
        /// </summary>
        public static List<HexCoordinates> GetLine(HexCoordinates start, HexCoordinates end)
        {
            int distance = start.DistanceTo(end);
            var results = new List<HexCoordinates>(distance + 1);

            if (distance == 0)
            {
                results.Add(start);
                return results;
            }

            for (int i = 0; i <= distance; i++)
            {
                float t = (float)i / distance;
                // Lerp each cube axis independently, then round
                float fx = Lerp(start.X, end.X, t);
                float fy = Lerp(start.Y, end.Y, t);
                float fz = Lerp(start.Z, end.Z, t);
                results.Add(CubeRound(fx, fy, fz));
            }

            return results;
        }

        // ---------------------------------------------------------
        // Internal Helpers
        // ---------------------------------------------------------

        /// <summary>
        /// Rounds fractional cube coordinates to the nearest valid hex.
        /// </summary>
        private static HexCoordinates CubeRound(float fx, float fy, float fz)
        {
            int rx = System.Math.Abs((int)System.Math.Round(fx) - (int)fx) > 0
                ? (int)System.Math.Round(fx) : (int)System.Math.Round(fx);
            int ry = (int)System.Math.Round(fy);
            int rz = (int)System.Math.Round(fz);

            float xDiff = System.Math.Abs(rx - fx);
            float yDiff = System.Math.Abs(ry - fy);
            float zDiff = System.Math.Abs(rz - fz);

            // Recalculate the axis with the largest rounding error
            if (xDiff > yDiff && xDiff > zDiff)
                rx = -ry - rz;
            else if (yDiff > zDiff)
                ry = -rx - rz;
            else
                rz = -rx - ry;

            return new HexCoordinates(rx, ry, rz);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
