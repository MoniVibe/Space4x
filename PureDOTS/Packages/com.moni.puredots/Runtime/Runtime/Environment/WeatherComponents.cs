using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Environment
{
    /// <summary>
    /// Weather types for the weather state system.
    /// </summary>
    public enum WeatherType : byte
    {
        Clear = 0,
        Rain = 1,
        Storm = 2,
        Drought = 3
    }

    /// <summary>
    /// Global weather state singleton used to coordinate weather effects and transitions.
    /// Used by both Godgame (environment systems) and Space4X (planet conditions).
    /// </summary>
    public struct WeatherState : IComponentData
    {
        /// <summary>
        /// Current weather type.
        /// </summary>
        public WeatherType CurrentWeather;

        /// <summary>
        /// Duration remaining in ticks until weather change.
        /// </summary>
        public uint DurationRemaining;

        /// <summary>
        /// Weather intensity (0-1). Affects moisture addition rate, visual effects, etc.
        /// </summary>
        public float Intensity;

        /// <summary>
        /// Random seed for deterministic weather transitions.
        /// </summary>
        public uint WeatherSeed;
    }
}

