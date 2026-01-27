using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Environment
{
    /// <summary>
    /// Global sunlight configuration for a world/planet.
    /// Singleton component defining sunlight distribution parameters.
    /// </summary>
    public struct SunlightConfig : IComponentData
    {
        /// <summary>Distance falloff factor for sunlight (how much distance reduces sunlight).</summary>
        public float FalloffDistance;

        /// <summary>Minimum sunlight value (0-1).</summary>
        public float MinSunlight;

        /// <summary>Maximum sunlight value (0-1).</summary>
        public float MaxSunlight;

        /// <summary>Time-of-day factor influence (0-1, where 1 = full day/night cycle).</summary>
        public float TimeOfDayFactor;

        /// <summary>Base sunlight multiplier (applied to StarSolarYield).</summary>
        public float BaseMultiplier;

        /// <summary>
        /// Default configuration with sensible values.
        /// </summary>
        public static SunlightConfig Default => new SunlightConfig
        {
            FalloffDistance = 1.0f, // Standard falloff
            MinSunlight = 0.0f, // Can go to zero
            MaxSunlight = 1.0f, // Can reach full intensity
            TimeOfDayFactor = 1.0f, // Full day/night cycle
            BaseMultiplier = 1.0f // No base modification
        };
    }

    /// <summary>
    /// Per-cell sunlight intensity (for Tier-2 spatial sunlight).
    /// Used when sunlight varies spatially (e.g., terrain shadowing, time of day).
    /// </summary>
    public struct SunlightCell
    {
        /// <summary>Sunlight intensity at this cell (0-1).</summary>
        public float Intensity;

        /// <summary>Time-of-day modifier (0-1, where 1 = noon, 0 = midnight).</summary>
        public float TimeOfDayModifier;

        /// <summary>Terrain shadowing factor (0-1, where 1 = no shadow, 0 = full shadow).</summary>
        public float ShadowFactor;
    }

    /// <summary>
    /// Global sunlight state for a world/planet.
    /// Singleton component tracking current sunlight intensity from parent star.
    /// </summary>
    public struct SunlightState : IComponentData
    {
        /// <summary>Current global sunlight intensity (0-1, derived from StarSolarYield).</summary>
        public float GlobalIntensity;

        /// <summary>Source star entity (Entity.Null if no star).</summary>
        public Entity SourceStar;

        /// <summary>Last update tick.</summary>
        public uint LastUpdateTick;
    }
}
























