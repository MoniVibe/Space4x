using Unity.Burst;
using Unity.Mathematics;

namespace PureDOTS.Runtime
{
    /// <summary>
    /// Helper class for computing collision damage based on material properties.
    /// Centralizes damage calculation logic so it can be easily tuned or changed later.
    /// </summary>
    [BurstCompile]
    public static class CollisionDamage
    {
        /// <summary>
        /// Computes damage from a collision using material-aware calculation.
        /// </summary>
        /// <param name="impulseMagnitude">Collision impulse magnitude from physics event (N·s)</param>
        /// <param name="relativeSpeed">Relative velocity magnitude (m/s). Can be computed from relativeVelocity.</param>
        /// <param name="materialA">Material stats for the first entity</param>
        /// <param name="materialB">Material stats for the second entity</param>
        /// <param name="damagePerImpulse">Global damage multiplier (from ImpactDamage.DamagePerImpulse or PhysicsConfig)</param>
        /// <param name="useEnergyFormula">If true, uses energy-based formula (0.5 * m_eff * v²). If false, uses impulse-based formula.</param>
        /// <returns>Computed damage amount</returns>
        [BurstCompile]
        public static float ComputeDamage(
            float impulseMagnitude,
            float relativeSpeed,
            in MaterialStats materialA,
            in MaterialStats materialB,
            float damagePerImpulse,
            bool useEnergyFormula = false)
        {
            if (useEnergyFormula)
            {
                // Energy-based formula: damage ∝ 0.5 * m_eff * v²
                // Effective mass factor from density
                float effectiveMassFactor = 0.5f * (materialA.Density + materialB.Density);
                float energy = 0.5f * effectiveMassFactor * relativeSpeed * relativeSpeed;
                return energy * damagePerImpulse;
            }
            else
            {
                // Impulse-based formula: damage ∝ impulse * material_hardness
                // Material factor is average hardness
                float materialFactor = 0.5f * (materialA.Hardness + materialB.Hardness);
                return impulseMagnitude * materialFactor * damagePerImpulse;
            }
        }

        /// <summary>
        /// Simplified version that only uses impulse (no relative speed needed).
        /// Uses the impulse-based formula.
        /// </summary>
        [BurstCompile]
        public static float ComputeDamage(
            float impulseMagnitude,
            in MaterialStats materialA,
            in MaterialStats materialB,
            float damagePerImpulse)
        {
            float materialFactor = 0.5f * (materialA.Hardness + materialB.Hardness);
            return impulseMagnitude * materialFactor * damagePerImpulse;
        }
    }
}
