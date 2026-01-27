using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Static service for cohesion effect calculations.
    /// </summary>
    [Unity.Burst.BurstCompile]
    public static class CohesionEffectService
    {
        /// <summary>
        /// Gets cohesion threshold from cohesion level (0-1).
        /// </summary>
        public static CohesionThreshold GetCohesionThreshold(float cohesionLevel)
        {
            if (cohesionLevel < 0.3f)
                return CohesionThreshold.Broken;
            if (cohesionLevel < 0.6f)
                return CohesionThreshold.Fragmented;
            if (cohesionLevel < 0.8f)
                return CohesionThreshold.Cohesive;
            return CohesionThreshold.Elite;
        }

        /// <summary>
        /// Gets cohesion attack multiplier.
        /// </summary>
        public static float GetCohesionAttackMultiplier(float cohesionLevel)
        {
            // Linear scaling: 0.5x at 0 cohesion, 1.5x at 1.0 cohesion
            return math.lerp(0.5f, 1.5f, cohesionLevel);
        }

        /// <summary>
        /// Gets cohesion defense multiplier.
        /// </summary>
        public static float GetCohesionDefenseMultiplier(float cohesionLevel)
        {
            // Linear scaling: 0.6x at 0 cohesion, 1.4x at 1.0 cohesion
            return math.lerp(0.6f, 1.4f, cohesionLevel);
        }

        /// <summary>
        /// Gets cohesion accuracy bonus (additive, not multiplicative).
        /// </summary>
        public static float GetCohesionAccuracyBonus(float cohesionLevel)
        {
            // +0% at 0 cohesion, +30% at 1.0 cohesion
            return cohesionLevel * 0.3f;
        }

        /// <summary>
        /// Gets squad cohesion from entity (if in squad/band).
        /// </summary>
        public static float GetSquadCohesion(Entity entity)
        {
            // Default cohesion if not in squad
            return 0.5f;
        }
    }
}



