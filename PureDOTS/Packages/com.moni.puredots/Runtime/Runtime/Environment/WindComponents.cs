using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Environment
{
    /// <summary>
    /// Wind type enumeration.
    /// </summary>
    public enum WindType : byte
    {
        /// <summary>No wind (calm).</summary>
        Calm = 0,
        /// <summary>Light breeze.</summary>
        Breeze = 1,
        /// <summary>Moderate wind.</summary>
        Wind = 2,
        /// <summary>Strong wind/storm.</summary>
        Storm = 3
    }

    /// <summary>
    /// Global wind state for a world/planet.
    /// Singleton component tracking wind direction, strength, and type.
    /// </summary>
    public struct WindState : IComponentData
    {
        /// <summary>Wind direction (normalized float2 vector).</summary>
        public float2 Direction;

        /// <summary>Wind strength (0-1, where 1 = maximum).</summary>
        public float Strength;

        /// <summary>Wind type (calm, breeze, wind, storm).</summary>
        public WindType Type;

        /// <summary>Last update tick for wind changes.</summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Configuration for wind behavior.
    /// Singleton component defining how wind changes over time.
    /// </summary>
    public struct WindConfig : IComponentData
    {
        /// <summary>Base wind strength (0-1).</summary>
        public float BaseStrength;

        /// <summary>Rate at which wind direction changes (radians per tick).</summary>
        public float DirectionChangeRate;

        /// <summary>Wind strength oscillation amplitude.</summary>
        public float StrengthOscillation;

        /// <summary>Wind strength oscillation period in ticks.</summary>
        public uint StrengthPeriod;

        /// <summary>Thresholds for wind type classification (0-1).</summary>
        public float BreezeThreshold;
        public float WindThreshold;
        public float StormThreshold;

        /// <summary>
        /// Default configuration with sensible values.
        /// </summary>
        public static WindConfig Default => new WindConfig
        {
            BaseStrength = 0.3f, // Moderate wind
            DirectionChangeRate = 0.01f, // Slow direction changes
            StrengthOscillation = 0.2f, // ±20% variation
            StrengthPeriod = 500u,
            BreezeThreshold = 0.2f,
            WindThreshold = 0.5f,
            StormThreshold = 0.8f
        };
    }

    /// <summary>
    /// Per-cell wind override (for Tier-2 spatial wind field).
    /// Used when wind varies spatially (e.g., mountain valleys, storm fronts).
    /// </summary>
    public struct WindCell
    {
        /// <summary>Local wind direction override.</summary>
        public float2 LocalDirection;

        /// <summary>Local wind strength multiplier.</summary>
        public float LocalStrengthMultiplier;
    }
}
























