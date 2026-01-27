using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using PureDOTS.Runtime.Social;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Service for calculating relation decay over time.
    /// </summary>
    [BurstCompile]
    public static class RelationDecayService
    {
        /// <summary>
        /// Calculate decay amount for a relation value.
        /// </summary>
        [BurstCompile]
        public static sbyte CalculateDecayAmount(
            sbyte currentIntensity,
            float decayRatePerDay,
            uint ticksSinceInteraction,
            float ticksPerDay,
            sbyte minIntensity)
        {
            return RelationCalculator.CalculateDecay(
                currentIntensity,
                ticksSinceInteraction,
                decayRatePerDay,
                ticksPerDay,
                minIntensity);
        }

        /// <summary>
        /// Check if a relation should decay (hasn't been updated recently).
        /// </summary>
        [BurstCompile]
        public static bool ShouldDecay(
            uint lastInteractionTick,
            uint currentTick,
            float decayCheckInterval)
        {
            uint ticksSinceInteraction = currentTick - lastInteractionTick;
            return ticksSinceInteraction >= (uint)(decayCheckInterval * 60f); // Convert to ticks
        }
    }
}

