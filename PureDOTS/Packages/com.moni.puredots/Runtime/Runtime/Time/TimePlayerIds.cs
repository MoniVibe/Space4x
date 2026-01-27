namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Constants for player IDs in the time system.
    /// Used to identify the source/owner of time control commands and time bubbles.
    /// </summary>
    public static class TimePlayerIds
    {
        /// <summary>Single-player or system-generated commands. Use this for all single-player operations.</summary>
        public const byte SinglePlayer = 0;
        
        /// <summary>Invalid/unassigned player ID.</summary>
        public const byte Invalid = byte.MaxValue;
    }
}

