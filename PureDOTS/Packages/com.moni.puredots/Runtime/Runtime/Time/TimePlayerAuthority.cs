using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Player time authority identifier.
    /// Lightweight type for future multiplayer per-player time control.
    /// </summary>
    public struct TimeAuthorityId
    {
        /// <summary>Player ID value.</summary>
        public byte Value;
        
        /// <summary>Single-player authority ID (0).</summary>
        public static TimeAuthorityId SinglePlayer => new TimeAuthorityId { Value = 0 };
        
        /// <summary>Invalid/unassigned authority ID.</summary>
        public static TimeAuthorityId Invalid => new TimeAuthorityId { Value = byte.MaxValue };
        
        /// <summary>
        /// Creates a time authority ID from a player ID.
        /// </summary>
        public static TimeAuthorityId FromPlayerId(byte playerId) => new TimeAuthorityId { Value = playerId };
    }
    
    /// <summary>
    /// Marker component for future per-player time authority mapping.
    /// Currently unused in single-player mode.
    /// 
    /// In multiplayer, this will map player IDs to their time control entities.
    /// Each player may have their own TimeState/RewindState for per-player time control.
    /// </summary>
    public struct PlayerTimeAuthority : IComponentData
    {
        /// <summary>Player ID this authority represents.</summary>
        public byte PlayerId;
        /// <summary>Optional root entity for per-player TimeState/RewindState (future MP use).</summary>
        public Entity TimeRoot;
        
        /// <summary>
        /// Creates a player time authority for single-player.
        /// </summary>
        public static PlayerTimeAuthority CreateSinglePlayer() => new PlayerTimeAuthority
        {
            PlayerId = TimePlayerIds.SinglePlayer,
            TimeRoot = Entity.Null
        };
        
        /// <summary>
        /// Creates a player time authority for a specific player.
        /// </summary>
        public static PlayerTimeAuthority Create(byte playerId, Entity timeRoot = default) => new PlayerTimeAuthority
        {
            PlayerId = playerId,
            TimeRoot = timeRoot
        };
    }
}

