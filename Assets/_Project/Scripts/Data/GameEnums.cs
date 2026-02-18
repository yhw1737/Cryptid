namespace Cryptid.Data
{
    /// <summary>
    /// Terrain types for hex tiles on the Cryptid board.
    /// Each tile has exactly one terrain type.
    /// Used by the Clue system to determine habitat validity.
    /// </summary>
    public enum TerrainType
    {
        Desert   = 0,
        Forest   = 1,
        Water    = 2,
        Swamp    = 3,
        Mountain = 4
    }

    /// <summary>
    /// Structure types that can exist on a hex tile.
    /// A tile may have None or exactly one structure.
    /// Used by the Clue system for distance-based clues.
    /// </summary>
    public enum StructureType
    {
        None           = 0,
        StandingStone  = 1,
        AbandonedShack = 2
    }

    /// <summary>
    /// Animal territory types that overlay the map.
    /// Each territory covers a contiguous region of hexes.
    /// Mapped from art assets: Bear = Type A, Cougar = Type B.
    /// </summary>
    public enum AnimalType
    {
        None   = 0,
        Bear   = 1,  // Animals FREE - Bear model
        Cougar = 2   // Animals FREE - Wolf/Cougar model
    }
}
