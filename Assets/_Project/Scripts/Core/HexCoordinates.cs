using System;
using UnityEngine;

namespace Cryptid.Core
{
    /// <summary>
    /// Represents a position in a hexagonal grid using Cube Coordinates (x, y, z).
    /// Constraint: x + y + z = 0.
    /// Immutable value type for safe usage as dictionary keys and in collections.
    /// </summary>
    [Serializable]
    public struct HexCoordinates : IEquatable<HexCoordinates>
    {
        [SerializeField] private int _x;
        [SerializeField] private int _y;
        [SerializeField] private int _z;

        public int X => _x;
        public int Y => _y;
        public int Z => _z;

        // ---------------------------------------------------------
        // Construction
        // ---------------------------------------------------------

        /// <summary>
        /// Creates a HexCoordinates from cube coordinates.
        /// Validates that x + y + z == 0.
        /// </summary>
        public HexCoordinates(int x, int y, int z)
        {
            if (x + y + z != 0)
            {
                throw new ArgumentException(
                    $"Invalid cube coordinates: ({x}, {y}, {z}). " +
                    $"Sum must be 0 but was {x + y + z}.");
            }

            _x = x;
            _y = y;
            _z = z;
        }

        /// <summary>
        /// Creates HexCoordinates from two axes. Z is derived: z = -x - y.
        /// </summary>
        public static HexCoordinates FromAxial(int q, int r)
        {
            return new HexCoordinates(q, r, -q - r);
        }

        /// <summary>
        /// Creates HexCoordinates from offset coordinates (col, row).
        /// Uses even-q offset layout (flat-top hexagons).
        /// </summary>
        public static HexCoordinates FromOffset(int col, int row)
        {
            int x = col;
            int z = row - (col + (col & 1)) / 2;
            int y = -x - z;
            return new HexCoordinates(x, y, z);
        }

        // ---------------------------------------------------------
        // Conversion
        // ---------------------------------------------------------

        /// <summary>
        /// Converts cube coordinates to offset coordinates (col, row).
        /// Uses even-q offset layout (flat-top hexagons).
        /// </summary>
        public Vector2Int ToOffset()
        {
            int col = _x;
            int row = _z + (_x + (_x & 1)) / 2;
            return new Vector2Int(col, row);
        }

        /// <summary>
        /// Returns the cube coordinates as a Vector3Int (x, y, z).
        /// </summary>
        public Vector3Int ToVector3Int()
        {
            return new Vector3Int(_x, _y, _z);
        }

        // ---------------------------------------------------------
        // Neighbors (6 directions)
        // ---------------------------------------------------------

        /// <summary>
        /// The six cube-coordinate direction vectors for hex neighbors.
        /// Order: E, NE, NW, W, SW, SE
        /// </summary>
        public static readonly HexCoordinates[] Directions = new HexCoordinates[]
        {
            new HexCoordinates(+1, -1,  0), // E
            new HexCoordinates(+1,  0, -1), // NE
            new HexCoordinates( 0, +1, -1), // NW
            new HexCoordinates(-1, +1,  0), // W
            new HexCoordinates(-1,  0, +1), // SW
            new HexCoordinates( 0, -1, +1), // SE
        };

        /// <summary>
        /// Returns the neighbor in the given direction index (0-5).
        /// </summary>
        public HexCoordinates GetNeighbor(int direction)
        {
            if (direction < 0 || direction > 5)
                throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be 0-5.");

            HexCoordinates dir = Directions[direction];
            return new HexCoordinates(_x + dir._x, _y + dir._y, _z + dir._z);
        }

        /// <summary>
        /// Returns all six neighbors of this hex.
        /// </summary>
        public HexCoordinates[] GetAllNeighbors()
        {
            var neighbors = new HexCoordinates[6];
            for (int i = 0; i < 6; i++)
            {
                neighbors[i] = GetNeighbor(i);
            }
            return neighbors;
        }

        // ---------------------------------------------------------
        // Distance
        // ---------------------------------------------------------

        /// <summary>
        /// Manhattan distance in cube coordinates: max(|dx|, |dy|, |dz|).
        /// </summary>
        public int DistanceTo(HexCoordinates other)
        {
            return Mathf.Max(
                Mathf.Abs(_x - other._x),
                Mathf.Abs(_y - other._y),
                Mathf.Abs(_z - other._z)
            );
        }

        // ---------------------------------------------------------
        // Rotation (60° increments around origin)
        // ---------------------------------------------------------

        /// <summary>
        /// Rotates 60° clockwise around the origin.
        /// Formula: (x, y, z) → (-z, -x, -y)
        /// </summary>
        public HexCoordinates RotateCW()
        {
            return new HexCoordinates(-_z, -_x, -_y);
        }

        /// <summary>
        /// Rotates 60° counter-clockwise around the origin.
        /// Formula: (x, y, z) → (-y, -z, -x)
        /// </summary>
        public HexCoordinates RotateCCW()
        {
            return new HexCoordinates(-_y, -_z, -_x);
        }

        /// <summary>
        /// Rotates N * 60° clockwise around the origin.
        /// </summary>
        public HexCoordinates Rotate(int steps)
        {
            // Normalize to 0-5 range
            steps = ((steps % 6) + 6) % 6;

            HexCoordinates result = this;
            for (int i = 0; i < steps; i++)
            {
                result = result.RotateCW();
            }
            return result;
        }

        /// <summary>
        /// Rotates around a pivot point by N * 60° clockwise.
        /// Translates to origin, rotates, then translates back.
        /// </summary>
        public HexCoordinates RotateAround(HexCoordinates pivot, int steps)
        {
            HexCoordinates offset = this - pivot;
            HexCoordinates rotated = offset.Rotate(steps);
            return rotated + pivot;
        }

        // ---------------------------------------------------------
        // Operators
        // ---------------------------------------------------------

        public static HexCoordinates operator +(HexCoordinates a, HexCoordinates b)
        {
            return new HexCoordinates(a._x + b._x, a._y + b._y, a._z + b._z);
        }

        public static HexCoordinates operator -(HexCoordinates a, HexCoordinates b)
        {
            return new HexCoordinates(a._x - b._x, a._y - b._y, a._z - b._z);
        }

        public static HexCoordinates operator *(HexCoordinates a, int scalar)
        {
            return new HexCoordinates(a._x * scalar, a._y * scalar, a._z * scalar);
        }

        public static bool operator ==(HexCoordinates a, HexCoordinates b)
        {
            return a._x == b._x && a._y == b._y && a._z == b._z;
        }

        public static bool operator !=(HexCoordinates a, HexCoordinates b)
        {
            return !(a == b);
        }

        // ---------------------------------------------------------
        // Equality & Hashing
        // ---------------------------------------------------------

        public bool Equals(HexCoordinates other)
        {
            return _x == other._x && _y == other._y && _z == other._z;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoordinates other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_x, _y, _z);
        }

        // ---------------------------------------------------------
        // String Representation
        // ---------------------------------------------------------

        public override string ToString()
        {
            return $"({_x}, {_y}, {_z})";
        }

        /// <summary>
        /// Short label for visual debugging overlays.
        /// </summary>
        public string ToLabel()
        {
            return $"{_x},{_y},{_z}";
        }

        /// <summary>
        /// Zero origin coordinate.
        /// </summary>
        public static readonly HexCoordinates Zero = new HexCoordinates(0, 0, 0);
    }
}
