using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Focus
{
    /// <summary>
    /// Static helpers for querying focus effects.
    /// Games use these to integrate focus with their systems.
    /// </summary>
    public static class FocusEffectHelpers
    {
        /// <summary>
        /// Gets the attack speed multiplier from active combat focus abilities.
        /// </summary>
        public static float GetAttackSpeedMultiplier(in DynamicBuffer<ActiveFocusAbility> abilities)
        {
            float multiplier = 1f;

            for (int i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                multiplier *= GetAbilityAttackSpeedMultiplier(ability.AbilityType, ability.Stacks);
            }

            return multiplier;
        }

        /// <summary>
        /// Gets the dodge bonus from active focus abilities.
        /// </summary>
        public static float GetDodgeBonus(in DynamicBuffer<ActiveFocusAbility> abilities)
        {
            float bonus = 0f;

            for (int i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                bonus += GetAbilityDodgeBonus(ability.AbilityType, ability.Stacks);
            }

            return math.saturate(bonus); // Cap at 1.0
        }

        /// <summary>
        /// Gets the critical hit chance bonus from active focus abilities.
        /// </summary>
        public static float GetCritBonus(in DynamicBuffer<ActiveFocusAbility> abilities)
        {
            float bonus = 0f;

            for (int i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                bonus += GetAbilityCritBonus(ability.AbilityType, ability.Stacks);
            }

            return math.saturate(bonus);
        }

        /// <summary>
        /// Gets the damage multiplier from active focus abilities.
        /// </summary>
        public static float GetDamageMultiplier(in DynamicBuffer<ActiveFocusAbility> abilities)
        {
            float multiplier = 1f;

            for (int i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                multiplier *= GetAbilityDamageMultiplier(ability.AbilityType, ability.Stacks);
            }

            return multiplier;
        }

        /// <summary>
        /// Gets the damage reduction from active focus abilities.
        /// </summary>
        public static float GetDamageReduction(in DynamicBuffer<ActiveFocusAbility> abilities)
        {
            float reduction = 0f;

            for (int i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                reduction += GetAbilityDamageReduction(ability.AbilityType, ability.Stacks);
            }

            return math.clamp(reduction, -0.5f, 0.75f);
        }

        /// <summary>
        /// Checks if a specific ability is active.
        /// </summary>
        public static bool IsAbilityActive(in DynamicBuffer<ActiveFocusAbility> abilities, FocusAbilityType abilityType)
        {
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i].AbilityType == abilityType)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the stack count for a specific ability.
        /// </summary>
        public static byte GetAbilityStacks(in DynamicBuffer<ActiveFocusAbility> abilities, FocusAbilityType abilityType)
        {
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i].AbilityType == abilityType)
                {
                    return abilities[i].Stacks;
                }
            }
            return 0;
        }

        private static float GetAbilityAttackSpeedMultiplier(FocusAbilityType ability, byte stacks)
        {
            float stackMult = stacks > 1 ? 1f + (stacks - 1) * 0.3f : 1f;

            return ability switch
            {
                FocusAbilityType.DualWieldStrike => 2f * stackMult,
                FocusAbilityType.QuickDraw => 1.25f * stackMult,
                FocusAbilityType.BlindingSpeed => 3f * stackMult,
                FocusAbilityType.AttackSpeedBoost => 1.5f * stackMult,
                FocusAbilityType.PowerStrike => 0.8f,
                FocusAbilityType.Berserk => 1.5f * stackMult,
                FocusAbilityType.Multicast => 1.5f * stackMult,
                _ => 1f
            };
        }

        private static float GetAbilityDodgeBonus(FocusAbilityType ability, byte stacks)
        {
            float stackMult = stacks > 1 ? 1f + (stacks - 1) * 0.3f : 1f;

            return ability switch
            {
                FocusAbilityType.DodgeBoost => 0.4f * stackMult,
                _ => 0f
            };
        }

        private static float GetAbilityCritBonus(FocusAbilityType ability, byte stacks)
        {
            float stackMult = stacks > 1 ? 1f + (stacks - 1) * 0.3f : 1f;

            return ability switch
            {
                FocusAbilityType.CriticalFocus => 0.25f * stackMult,
                FocusAbilityType.Feint => 0.15f * stackMult,
                _ => 0f
            };
        }

        private static float GetAbilityDamageMultiplier(FocusAbilityType ability, byte stacks)
        {
            float stackMult = stacks > 1 ? 1f + (stacks - 1) * 0.3f : 1f;

            return ability switch
            {
                FocusAbilityType.DualWieldStrike => 0.7f,
                FocusAbilityType.Riposte => 1.2f,
                FocusAbilityType.PrecisionStrike => 1.3f * stackMult,
                FocusAbilityType.BlindingSpeed => 0.6f,
                FocusAbilityType.SweepAttack => 0.8f,
                FocusAbilityType.PowerStrike => 1.75f * stackMult,
                FocusAbilityType.Charge => 2f * stackMult,
                FocusAbilityType.Berserk => 2.5f * stackMult,
                FocusAbilityType.SpellAmplify => 1.5f * stackMult,
                FocusAbilityType.ElementalMastery => 1.3f * stackMult,
                _ => 1f
            };
        }

        private static float GetAbilityDamageReduction(FocusAbilityType ability, byte stacks)
        {
            float stackMult = stacks > 1 ? 1f + (stacks - 1) * 0.3f : 1f;

            return ability switch
            {
                FocusAbilityType.IgnorePain => 0.3f * stackMult,
                FocusAbilityType.SecondWind => 0.1f,
                FocusAbilityType.Berserk => -0.3f, // Take more damage
                FocusAbilityType.IronWill => 0.2f * stackMult,
                FocusAbilityType.SpellShield => 0.25f * stackMult,
                _ => 0f
            };
        }
    }

    /// <summary>
    /// Static helpers for profession focus integration.
    /// Games use these to apply focus modifiers to job outputs.
    /// </summary>
    public static class ProfessionFocusHelpers
    {
        /// <summary>
        /// Applies crafting modifiers from focus to output values.
        /// Returns (quantity, quality, materialCost).
        /// </summary>
        public static (int quantity, float quality, float materialCost) ApplyCraftingModifiers(
            int baseQuantity,
            float baseQuality,
            float baseMaterialCost,
            float baseWasteRate,
            in ProfessionFocusModifiers mods,
            uint randomSeed)
        {
            // Apply quantity modifier
            int quantity = (int)math.ceil(baseQuantity * mods.QuantityMultiplier);

            // Apply quality modifier
            float quality = baseQuality * mods.QualityMultiplier;

            // Apply waste/material modifier
            float materialCost = baseMaterialCost * mods.WasteMultiplier;

            // Bonus chance for extra quality/quantity
            if (mods.BonusChance > 0f)
            {
                // Deterministic random based on seed
                uint hash = randomSeed * 1103515245 + 12345;
                float roll = (hash % 10000) / 10000f;

                if (roll < mods.BonusChance)
                {
                    // Bonus triggered - add 10% quality or 1 extra item
                    if (quality < 90f)
                    {
                        quality += 10f;
                    }
                    else
                    {
                        quantity += 1;
                    }
                }
            }

            return (quantity, math.clamp(quality, 0f, 100f), materialCost);
        }

        /// <summary>
        /// Applies gathering modifiers from focus.
        /// Returns (yield, rareChance, nodePreservation).
        /// </summary>
        public static (float yield, float rareChance, float nodePreservation) ApplyGatheringModifiers(
            float baseYield,
            float baseRareChance,
            in ProfessionFocusModifiers mods)
        {
            float yield = baseYield * mods.QuantityMultiplier;
            float rareChance = baseRareChance + mods.BonusChance;
            float nodePreservation = 1f - mods.WasteMultiplier; // Lower waste = better preservation

            return (yield, math.saturate(rareChance), math.saturate(nodePreservation));
        }

        /// <summary>
        /// Applies healing modifiers from focus.
        /// Returns (healAmount, targetCount).
        /// </summary>
        public static (float healAmount, int targetCount) ApplyHealingModifiers(
            float baseHealAmount,
            int baseTargetCount,
            in ProfessionFocusModifiers mods)
        {
            float healAmount = baseHealAmount * mods.QualityMultiplier;
            int targetCount = (int)math.ceil(baseTargetCount * mods.TargetCountMultiplier);

            return (healAmount, math.max(1, targetCount));
        }

        /// <summary>
        /// Applies teaching modifiers from focus.
        /// Returns (xpGain, studentCount, retentionBonus).
        /// </summary>
        public static (float xpGain, int studentCount, float retentionBonus) ApplyTeachingModifiers(
            float baseXpGain,
            int baseStudentCount,
            in ProfessionFocusModifiers mods)
        {
            float xpGain = baseXpGain * mods.SpeedMultiplier;
            int studentCount = (int)math.ceil(baseStudentCount * mods.TargetCountMultiplier);
            float retentionBonus = mods.QualityMultiplier - 1f; // Bonus above 1.0

            return (xpGain, math.max(1, studentCount), retentionBonus);
        }

        /// <summary>
        /// Applies refining modifiers from focus.
        /// Returns (purity, yield, processingTime).
        /// </summary>
        public static (float purity, float yield, float processingTime) ApplyRefiningModifiers(
            float basePurity,
            float baseYield,
            float baseProcessingTime,
            in ProfessionFocusModifiers mods)
        {
            float purity = basePurity * mods.QualityMultiplier;
            float yield = baseYield * mods.QuantityMultiplier * (1f - (mods.WasteMultiplier - 1f));
            float processingTime = baseProcessingTime / mods.SpeedMultiplier;

            return (math.saturate(purity), math.max(0f, yield), math.max(0.1f, processingTime));
        }

        /// <summary>
        /// Calculates work speed multiplier from focus modifiers.
        /// </summary>
        public static float GetWorkSpeedMultiplier(in ProfessionFocusModifiers mods)
        {
            return mods.SpeedMultiplier;
        }

        /// <summary>
        /// Calculates quality multiplier from focus modifiers.
        /// </summary>
        public static float GetQualityMultiplier(in ProfessionFocusModifiers mods)
        {
            return mods.QualityMultiplier;
        }
    }

    /// <summary>
    /// Integration helpers for job systems.
    /// Provides convenient access to focus state and modifiers.
    /// </summary>
    public static class ProfessionFocusIntegration
    {
        /// <summary>
        /// Checks if entity has enough focus to perform an action.
        /// </summary>
        public static bool HasEnoughFocus(in EntityFocus focus, float requiredFocus)
        {
            return !focus.IsInComa && focus.CurrentFocus >= requiredFocus;
        }

        /// <summary>
        /// Checks if entity is too exhausted to work effectively.
        /// </summary>
        public static bool IsTooExhausted(in EntityFocus focus, byte exhaustionThreshold = 80)
        {
            return focus.IsInComa || focus.ExhaustionLevel >= exhaustionThreshold;
        }

        /// <summary>
        /// Gets focus percentage (0-1).
        /// </summary>
        public static float GetFocusPercentage(in EntityFocus focus)
        {
            return focus.MaxFocus > 0 ? focus.CurrentFocus / focus.MaxFocus : 0f;
        }

        /// <summary>
        /// Gets exhaustion percentage (0-1).
        /// </summary>
        public static float GetExhaustionPercentage(in EntityFocus focus)
        {
            return focus.ExhaustionLevel / 100f;
        }
    }
}

