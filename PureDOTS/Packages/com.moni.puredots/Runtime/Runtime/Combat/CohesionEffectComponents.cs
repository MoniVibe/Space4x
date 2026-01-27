using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Squad cohesion - unit cohesion level affecting combat effectiveness.
    /// </summary>
    public struct SquadCohesion : IComponentData
    {
        public float CohesionLevel;        // 0.0 to 1.0
        public CohesionThreshold Threshold; // Categorical level
        public uint LastUpdatedTick;
        public float DegradationRate;       // Per second under fire
        public float RegenRate;            // Per second when not fighting
    }

    /// <summary>
    /// Cohesion thresholds - categorical cohesion levels.
    /// </summary>
    public enum CohesionThreshold : byte
    {
        Broken = 0,        // < 0.3
        Fragmented = 1,    // 0.3-0.6
        Cohesive = 2,      // 0.6-0.8
        Elite = 3          // > 0.8
    }

    /// <summary>
    /// Cohesion combat multipliers - applied to combat stats during resolution.
    /// Read by combat resolution systems (e.g., HitDetectionSystem, DamageApplicationSystem).
    /// 
    /// Usage Pattern:
    /// - Read CohesionCombatMultipliers component on entity
    /// - Apply multipliers to combat calculations during resolution
    /// - Example: effectiveDamage = baseDamage * multipliers.DamageMultiplier;
    /// - Example: effectiveAccuracy = baseAccuracy * multipliers.AccuracyMultiplier;
    /// - Example: effectiveDefense = baseDefense * multipliers.DefenseMultiplier;
    /// 
    /// These multipliers are updated by CohesionEffectSystem based on squad cohesion level.
    /// See DamageApplicationSystem for reference implementation of reading multipliers.
    /// </summary>
    public struct CohesionCombatMultipliers : IComponentData
    {
        /// <summary>
        /// Accuracy multiplier (1.0 = no change, 1.3 = +30% accuracy).
        /// Applied to hit chance calculations.
        /// </summary>
        public float AccuracyMultiplier;

        /// <summary>
        /// Damage multiplier (1.0 = no change, 1.5 = +50% damage).
        /// Applied to damage calculations.
        /// </summary>
        public float DamageMultiplier;

        /// <summary>
        /// Defense multiplier (1.0 = no change, 1.4 = +40% defense).
        /// Applied to defense/dodge calculations.
        /// </summary>
        public float DefenseMultiplier;
    }
}

