using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Formation;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Static service for formation combat calculations.
    /// </summary>
    [Unity.Burst.BurstCompile]
    public static class FormationCombatService
    {
        /// <summary>
        /// Gets formation defense bonus (scaled by integrity).
        /// </summary>
        public static float GetFormationDefenseBonus(
            in FormationBonus bonus,
            in FormationIntegrity integrity,
            in FormationCombatConfig config)
        {
            if (integrity.IntegrityPercent < config.IntegrityThreshold)
                return 1f; // No bonus if integrity too low

            // Scale bonus by integrity: BaseBonus * Integrity
            return math.lerp(1f, bonus.DefenseMultiplier, integrity.IntegrityPercent);
        }

        /// <summary>
        /// Gets formation attack bonus (scaled by integrity).
        /// </summary>
        public static float GetFormationAttackBonus(
            in FormationBonus bonus,
            in FormationIntegrity integrity,
            in FormationCombatConfig config)
        {
            if (integrity.IntegrityPercent < config.IntegrityThreshold)
                return 1f;

            return math.lerp(1f, bonus.AttackMultiplier, integrity.IntegrityPercent);
        }

        /// <summary>
        /// Gets formation morale bonus (scaled by integrity).
        /// </summary>
        public static float GetFormationMoraleBonus(
            in FormationBonus bonus,
            in FormationIntegrity integrity,
            in FormationCombatConfig config)
        {
            if (integrity.IntegrityPercent < config.IntegrityThreshold)
                return 1f;

            return math.lerp(1f, bonus.MoraleMultiplier, integrity.IntegrityPercent);
        }

        /// <summary>
        /// Gets formation integrity (0-1) based on member positions.
        /// </summary>
        public static float GetFormationIntegrity(
            byte membersInPosition,
            byte totalMembers)
        {
            if (totalMembers == 0)
                return 0f;

            return (float)membersInPosition / (float)totalMembers;
        }

        /// <summary>
        /// Gets base bonuses for a formation type.
        /// </summary>
        public static FormationCombatConfig GetBaseConfig(FormationType type)
        {
            return type switch
            {
                FormationType.Phalanx => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 2.0f,    // +100% defense
                    BaseAttackMultiplier = 1.2f,     // +20% attack
                    BaseMoraleMultiplier = 1.3f,     // +30% morale
                    IntegrityThreshold = 0.3f
                },
                FormationType.Skirmish => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 0.7f,    // -30% defense (loose formation)
                    BaseAttackMultiplier = 1.3f,     // +30% attack
                    BaseMoraleMultiplier = 1.1f,     // +10% morale
                    IntegrityThreshold = 0.2f
                },
                FormationType.Line => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 1.4f,    // +40% defense
                    BaseAttackMultiplier = 1.1f,     // +10% attack
                    BaseMoraleMultiplier = 1.2f,     // +20% morale
                    IntegrityThreshold = 0.4f
                },
                FormationType.Wedge => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 0.8f,    // -20% defense (offensive)
                    BaseAttackMultiplier = 1.6f,     // +60% attack
                    BaseMoraleMultiplier = 1.4f,     // +40% morale
                    IntegrityThreshold = 0.5f
                },
                FormationType.Square => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 1.4f,    // +40% defense (all sides)
                    BaseAttackMultiplier = 1.0f,     // No attack bonus
                    BaseMoraleMultiplier = 1.25f,    // +25% morale
                    IntegrityThreshold = 0.4f
                },
                FormationType.Column => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 1.1f,    // +10% defense
                    BaseAttackMultiplier = 1.0f,     // No attack bonus
                    BaseMoraleMultiplier = 1.1f,     // +10% morale
                    IntegrityThreshold = 0.3f
                },
                // Fleet formations (Space4X)
                FormationType.Echelon => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 1.2f,    // +20% defense (staggered)
                    BaseAttackMultiplier = 1.3f,     // +30% attack (concentrated fire)
                    BaseMoraleMultiplier = 1.15f,    // +15% morale
                    IntegrityThreshold = 0.4f
                },
                FormationType.Diamond => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 1.3f,    // +30% defense (all-around)
                    BaseAttackMultiplier = 1.2f,     // +20% attack
                    BaseMoraleMultiplier = 1.2f,     // +20% morale
                    IntegrityThreshold = 0.5f
                },
                FormationType.Vanguard => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 0.9f,    // -10% defense (forward position)
                    BaseAttackMultiplier = 1.4f,     // +40% attack (offensive)
                    BaseMoraleMultiplier = 1.3f,     // +30% morale
                    IntegrityThreshold = 0.4f
                },
                FormationType.Rearguard => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 1.2f,    // +20% defense (defensive)
                    BaseAttackMultiplier = 1.0f,     // No attack bonus
                    BaseMoraleMultiplier = 1.1f,     // +10% morale
                    IntegrityThreshold = 0.3f
                },
                FormationType.Screen => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 1.1f,    // +10% defense (wide coverage)
                    BaseAttackMultiplier = 1.1f,     // +10% attack
                    BaseMoraleMultiplier = 1.05f,    // +5% morale
                    IntegrityThreshold = 0.3f
                },
                _ => new FormationCombatConfig
                {
                    BaseDefenseMultiplier = 1.0f,
                    BaseAttackMultiplier = 1.0f,
                    BaseMoraleMultiplier = 1.0f,
                    IntegrityThreshold = 0.3f
                }
            };
        }
    }
}

