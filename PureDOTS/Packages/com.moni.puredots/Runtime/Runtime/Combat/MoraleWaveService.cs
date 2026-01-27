using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Static service for morale wave calculations.
    /// </summary>
    [Unity.Burst.BurstCompile]
    public static class MoraleWaveService
    {
        /// <summary>
        /// Gets morale threshold from morale level (0-100).
        /// </summary>
        public static MoraleThreshold GetMoraleThreshold(float moraleLevel)
        {
            if (moraleLevel < 20f)
                return MoraleThreshold.Routed;
            if (moraleLevel < 40f)
                return MoraleThreshold.Shaken;
            if (moraleLevel < 70f)
                return MoraleThreshold.Steady;
            return MoraleThreshold.Inspired;
        }

        /// <summary>
        /// Calculates wave intensity at distance from source.
        /// </summary>
        public static float CalculateWaveIntensity(
            float baseIntensity,
            float distance,
            float radius,
            float decayRate)
        {
            if (distance > radius)
                return 0f;

            // Linear decay: Intensity * (1 - (distance / radius) * decayRate)
            float distanceRatio = distance / radius;
            float decayFactor = 1f - (distanceRatio * decayRate);
            return baseIntensity * math.max(0f, decayFactor);
        }

        /// <summary>
        /// Checks if morale wave should propagate to target.
        /// </summary>
        public static bool ShouldPropagate(
            float intensity,
            float distance,
            float minIntensity)
        {
            return math.abs(intensity) >= minIntensity && distance > 0f;
        }
    }
}



