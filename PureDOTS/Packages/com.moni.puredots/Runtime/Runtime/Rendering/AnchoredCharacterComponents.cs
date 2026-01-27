using System.Runtime.InteropServices;
using Unity.Entities;

namespace PureDOTS.Runtime.Rendering
{
    /// <summary>
    /// Anchor reason codes for debugging and telemetry.
    /// </summary>
    public enum AnchorReason : byte
    {
        /// <summary>Player manually favorited this character.</summary>
        PlayerFavorite = 0,
        /// <summary>Story-critical NPC that must remain visible.</summary>
        StoryCritical = 1,
        /// <summary>Veteran/legendary status earned through gameplay.</summary>
        Veteran = 2,
        /// <summary>Leadership role (chief, captain, flagship).</summary>
        Leader = 3,
        /// <summary>Auto-anchored by attention/interaction tracking.</summary>
        AttentionTracked = 4
    }

    /// <summary>
    /// Core marker component - entity is anchored and exempt from normal culling/despawn rules.
    /// Anchored characters remain visible and simulated regardless of distance or LOD settings.
    /// </summary>
    public struct AnchoredCharacter : IComponentData
    {
        /// <summary>
        /// Priority level (0-10, higher = more important).
        /// Used for budget enforcement when limits are reached.
        /// 0 = normal anchored (player favorite)
        /// 1-5 = increasingly important (veterans, leaders)
        /// 10 = critical (flagship, avatar, story-critical NPCs)
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Which player anchored this entity?
        /// Entity.Null = shared anchor (all players care, e.g., major NPCs)
        /// </summary>
        public Entity AnchoredBy;

        /// <summary>
        /// Reason/tag for why this entity was anchored (for debug/telemetry).
        /// </summary>
        public AnchorReason Reason;

        /// <summary>
        /// Tick when this character was anchored.
        /// </summary>
        public uint AnchoredAtTick;
    }

    /// <summary>
    /// Optional rendering configuration for anchored characters.
    /// Controls LOD behavior, shadows, and VFX at distance.
    /// </summary>
    public struct AnchoredRenderConfig : IComponentData
    {
        /// <summary>
        /// Minimum LOD level this character will use (never goes lower).
        /// 0 = full detail always
        /// 1 = medium detail minimum
        /// 2 = low detail minimum (but never culled entirely)
        /// </summary>
        public byte MinLODLevel;

        /// <summary>
        /// Should this character cast shadows even at extreme distance?
        /// False = shadows cull normally (performance optimization)
        /// </summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool AlwaysCastShadows;

        /// <summary>
        /// Should VFX/particles be active even when far?
        /// False = model renders, but particles cull (performance optimization)
        /// </summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool AlwaysRenderVFX;

        /// <summary>
        /// Maximum render distance override.
        /// 0 = infinite (guaranteed visible at any distance)
        /// > 0 = special larger radius (e.g., 2x normal) but still culled beyond
        /// </summary>
        public float MaxRenderDistance;

        /// <summary>
        /// Creates default render config with medium detail minimum.
        /// </summary>
        public static AnchoredRenderConfig Default => new AnchoredRenderConfig
        {
            MinLODLevel = 1,
            AlwaysCastShadows = false,
            AlwaysRenderVFX = false,
            MaxRenderDistance = 0f // Infinite
        };

        /// <summary>
        /// Creates high-quality config for important characters.
        /// </summary>
        public static AnchoredRenderConfig HighQuality => new AnchoredRenderConfig
        {
            MinLODLevel = 0,
            AlwaysCastShadows = true,
            AlwaysRenderVFX = true,
            MaxRenderDistance = 0f // Infinite
        };
    }

    /// <summary>
    /// Optional simulation configuration for anchored characters.
    /// Controls AI/behavior tick rate at distance for performance optimization.
    /// </summary>
    public struct AnchoredSimConfig : IComponentData
    {
        /// <summary>
        /// Tick rate divisor when at distance.
        /// 1 = full rate (every tick)
        /// 2 = half rate (every 2nd tick)
        /// 4 = quarter rate (every 4th tick)
        /// </summary>
        public byte TickRateDivisor;

        /// <summary>
        /// Distance from camera before reduced tick rate kicks in.
        /// Below this distance, always full simulation.
        /// </summary>
        public float DistanceForReduced;

        /// <summary>
        /// Override: always run full simulation regardless of distance.
        /// Use for story-critical NPCs that must react instantly.
        /// </summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool AlwaysFullSimulation;

        /// <summary>
        /// Creates default sim config with full simulation.
        /// </summary>
        public static AnchoredSimConfig Default => new AnchoredSimConfig
        {
            TickRateDivisor = 1,
            DistanceForReduced = 500f,
            AlwaysFullSimulation = false
        };

        /// <summary>
        /// Creates config that always runs full simulation.
        /// </summary>
        public static AnchoredSimConfig AlwaysFull => new AnchoredSimConfig
        {
            TickRateDivisor = 1,
            DistanceForReduced = 0f,
            AlwaysFullSimulation = true
        };

        /// <summary>
        /// Creates performance-optimized config with reduced distant updates.
        /// </summary>
        public static AnchoredSimConfig Optimized => new AnchoredSimConfig
        {
            TickRateDivisor = 4,
            DistanceForReduced = 200f,
            AlwaysFullSimulation = false
        };
    }
}

