using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Focus;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Focus
{
    /// <summary>
    /// Calculates profession and combat focus modifiers from active abilities.
    /// Job systems read these cached modifiers to apply focus effects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FocusSystemGroup))]
    [UpdateAfter(typeof(FocusExhaustionSystem))]
    public partial struct ProfessionFocusModifierSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Update profession modifiers
            new UpdateProfessionModifiersJob().ScheduleParallel();

            // Update combat modifiers
            new UpdateCombatModifiersJob().ScheduleParallel();
        }

        [BurstCompile]
        public partial struct UpdateProfessionModifiersJob : IJobEntity
        {
            void Execute(
                ref ProfessionFocusModifiers modifiers,
                in DynamicBuffer<ActiveFocusAbility> abilities)
            {
                // Reset to defaults
                modifiers.QualityMultiplier = 1f;
                modifiers.SpeedMultiplier = 1f;
                modifiers.WasteMultiplier = 1f;
                modifiers.TargetCountMultiplier = 1f;
                modifiers.BonusChance = 0f;
                modifiers.QuantityMultiplier = 1f;

                // Apply modifiers from active abilities
                for (int i = 0; i < abilities.Length; i++)
                {
                    var ability = abilities[i];
                    ApplyProfessionAbilityModifier(ref modifiers, ability.AbilityType, ability.Stacks);
                }
            }

            private static void ApplyProfessionAbilityModifier(
                ref ProfessionFocusModifiers mods,
                FocusAbilityType ability,
                byte stacks)
            {
                float stackMult = stacks > 1 ? 1f + (stacks - 1) * 0.5f : 1f;

                switch (ability)
                {
                    // Crafting
                    case FocusAbilityType.MassProduction:
                        mods.QuantityMultiplier *= 2f * stackMult;
                        mods.QualityMultiplier *= 0.7f; // 30% quality loss
                        break;
                    case FocusAbilityType.MasterworkFocus:
                        mods.QualityMultiplier *= 1.5f * stackMult;
                        mods.SpeedMultiplier *= 0.5f; // 2x slower
                        break;
                    case FocusAbilityType.BatchCrafting:
                        mods.QuantityMultiplier *= 1.5f * stackMult;
                        break;
                    case FocusAbilityType.MaterialSaver:
                        mods.WasteMultiplier *= 0.7f * stackMult;
                        break;
                    case FocusAbilityType.QualityControl:
                        // Reduces variance (handled by game-side)
                        mods.BonusChance += 0.1f * stackMult;
                        break;
                    case FocusAbilityType.ExpertFinish:
                        mods.BonusChance += 0.2f * stackMult;
                        break;
                    case FocusAbilityType.RapidAssembly:
                        mods.SpeedMultiplier *= 1.5f * stackMult;
                        mods.QualityMultiplier *= 0.9f;
                        break;
                    case FocusAbilityType.InnovativeCraft:
                        mods.BonusChance += 0.15f * stackMult;
                        break;

                    // Gathering
                    case FocusAbilityType.SpeedGather:
                        mods.SpeedMultiplier *= 1.5f * stackMult;
                        break;
                    case FocusAbilityType.EfficientGather:
                        mods.QuantityMultiplier *= 1.25f * stackMult;
                        mods.WasteMultiplier *= 0.8f;
                        break;
                    case FocusAbilityType.GatherOverdrive:
                        mods.SpeedMultiplier *= 2f * stackMult;
                        mods.WasteMultiplier *= 1.2f; // More waste
                        break;
                    case FocusAbilityType.CarefulExtract:
                        mods.BonusChance += 0.25f * stackMult; // Rare material chance
                        mods.SpeedMultiplier *= 0.8f;
                        break;
                    case FocusAbilityType.BonusYield:
                        mods.BonusChance += 0.3f * stackMult;
                        break;
                    case FocusAbilityType.PreserveNode:
                        mods.WasteMultiplier *= 0.5f * stackMult;
                        break;
                    case FocusAbilityType.MultiGather:
                        mods.TargetCountMultiplier *= 2f * stackMult;
                        mods.SpeedMultiplier *= 0.7f;
                        break;

                    // Healing
                    case FocusAbilityType.MassHeal:
                        mods.TargetCountMultiplier *= 3f * stackMult;
                        mods.QualityMultiplier *= 0.5f; // Less healing per target
                        break;
                    case FocusAbilityType.LifeClutch:
                        mods.QualityMultiplier *= 2f * stackMult; // Emergency boost
                        break;
                    case FocusAbilityType.IntensiveCare:
                        mods.QualityMultiplier *= 1.75f * stackMult;
                        mods.SpeedMultiplier *= 0.6f;
                        break;
                    case FocusAbilityType.Stabilize:
                        // Prevents death (game-side logic)
                        break;
                    case FocusAbilityType.Purify:
                        mods.BonusChance += 0.5f * stackMult; // Debuff removal chance
                        break;
                    case FocusAbilityType.Regenerate:
                        mods.SpeedMultiplier *= 0.5f; // Slow but continuous
                        mods.QuantityMultiplier *= 2f; // Total healing over time
                        break;
                    case FocusAbilityType.SurgicalPrecision:
                        mods.QualityMultiplier *= 1.5f * stackMult;
                        break;

                    // Teaching
                    case FocusAbilityType.IntensiveLessons:
                        mods.SpeedMultiplier *= 2f * stackMult;
                        mods.QualityMultiplier *= 0.8f; // Less retention
                        break;
                    case FocusAbilityType.DeepTeaching:
                        mods.QualityMultiplier *= 1.5f * stackMult; // Better retention
                        mods.SpeedMultiplier *= 0.5f;
                        break;
                    case FocusAbilityType.GroupInstruction:
                        mods.TargetCountMultiplier *= 4f * stackMult;
                        mods.QualityMultiplier *= 0.7f;
                        break;
                    case FocusAbilityType.MentoringBond:
                        mods.QualityMultiplier *= 1.3f * stackMult;
                        break;
                    case FocusAbilityType.PracticalTraining:
                        mods.QualityMultiplier *= 1.25f * stackMult;
                        break;
                    case FocusAbilityType.InspiredTeaching:
                        mods.BonusChance += 0.2f * stackMult; // Eureka moment
                        break;

                    // Refining
                    case FocusAbilityType.RapidRefine:
                        mods.SpeedMultiplier *= 1.75f * stackMult;
                        mods.WasteMultiplier *= 1.15f;
                        break;
                    case FocusAbilityType.PureExtraction:
                        mods.QualityMultiplier *= 1.4f * stackMult; // Purity
                        mods.SpeedMultiplier *= 0.6f;
                        break;
                    case FocusAbilityType.BatchRefine:
                        mods.QuantityMultiplier *= 1.5f * stackMult;
                        break;
                    case FocusAbilityType.CatalystBoost:
                        mods.SpeedMultiplier *= 1.3f * stackMult;
                        mods.WasteMultiplier *= 0.9f;
                        break;
                    case FocusAbilityType.WasteRecovery:
                        mods.WasteMultiplier *= 0.6f * stackMult;
                        break;
                    case FocusAbilityType.PrecisionRefine:
                        mods.QualityMultiplier *= 1.2f * stackMult;
                        mods.BonusChance += 0.1f;
                        break;
                }
            }
        }

        [BurstCompile]
        public partial struct UpdateCombatModifiersJob : IJobEntity
        {
            void Execute(
                ref CombatFocusModifiers modifiers,
                in DynamicBuffer<ActiveFocusAbility> abilities)
            {
                // Reset to defaults
                modifiers.AttackSpeedMultiplier = 1f;
                modifiers.DamageMultiplier = 1f;
                modifiers.DodgeBonus = 0f;
                modifiers.CritBonus = 0f;
                modifiers.DamageReduction = 0f;
                modifiers.ParryBonus = 0f;

                // Apply modifiers from active abilities
                for (int i = 0; i < abilities.Length; i++)
                {
                    var ability = abilities[i];
                    ApplyCombatAbilityModifier(ref modifiers, ability.AbilityType, ability.Stacks);
                }
            }

            private static void ApplyCombatAbilityModifier(
                ref CombatFocusModifiers mods,
                FocusAbilityType ability,
                byte stacks)
            {
                float stackMult = stacks > 1 ? 1f + (stacks - 1) * 0.3f : 1f;

                switch (ability)
                {
                    // Finesse
                    case FocusAbilityType.Parry:
                        mods.ParryBonus += 0.3f * stackMult;
                        break;
                    case FocusAbilityType.DualWieldStrike:
                        mods.AttackSpeedMultiplier *= 2f * stackMult;
                        mods.DamageMultiplier *= 0.7f; // Less damage per hit
                        break;
                    case FocusAbilityType.CriticalFocus:
                        mods.CritBonus += 0.25f * stackMult;
                        break;
                    case FocusAbilityType.DodgeBoost:
                        mods.DodgeBonus += 0.4f * stackMult;
                        break;
                    case FocusAbilityType.Riposte:
                        mods.ParryBonus += 0.15f * stackMult;
                        mods.DamageMultiplier *= 1.2f; // Counter damage
                        break;
                    case FocusAbilityType.PrecisionStrike:
                        mods.DamageMultiplier *= 1.3f * stackMult; // Armor bypass
                        break;
                    case FocusAbilityType.Feint:
                        mods.CritBonus += 0.15f * stackMult;
                        break;
                    case FocusAbilityType.QuickDraw:
                        mods.AttackSpeedMultiplier *= 1.25f * stackMult;
                        break;
                    case FocusAbilityType.BlindingSpeed:
                        mods.AttackSpeedMultiplier *= 3f * stackMult;
                        mods.DamageMultiplier *= 0.6f;
                        break;

                    // Physique
                    case FocusAbilityType.IgnorePain:
                        mods.DamageReduction += 0.3f * stackMult;
                        break;
                    case FocusAbilityType.SweepAttack:
                        // Multi-target (handled by game-side)
                        mods.DamageMultiplier *= 0.8f;
                        break;
                    case FocusAbilityType.AttackSpeedBoost:
                        mods.AttackSpeedMultiplier *= 1.5f * stackMult;
                        break;
                    case FocusAbilityType.PowerStrike:
                        mods.DamageMultiplier *= 1.75f * stackMult;
                        mods.AttackSpeedMultiplier *= 0.8f;
                        break;
                    case FocusAbilityType.Charge:
                        mods.DamageMultiplier *= 2f * stackMult;
                        break;
                    case FocusAbilityType.Intimidate:
                        // Enemy debuff (handled by game-side)
                        break;
                    case FocusAbilityType.SecondWind:
                        // Health regen (handled by game-side)
                        mods.DamageReduction += 0.1f;
                        break;
                    case FocusAbilityType.Berserk:
                        mods.DamageMultiplier *= 2.5f * stackMult;
                        mods.AttackSpeedMultiplier *= 1.5f;
                        mods.DamageReduction -= 0.3f; // Take more damage
                        break;
                    case FocusAbilityType.IronWill:
                        mods.DamageReduction += 0.2f * stackMult;
                        break;

                    // Arcane (combat-relevant)
                    case FocusAbilityType.SpellAmplify:
                        mods.DamageMultiplier *= 1.5f * stackMult; // Spell damage
                        break;
                    case FocusAbilityType.Multicast:
                        mods.AttackSpeedMultiplier *= 1.5f * stackMult; // Chance for double cast
                        break;
                    case FocusAbilityType.SpellShield:
                        mods.DamageReduction += 0.25f * stackMult; // Magic damage only
                        break;
                    case FocusAbilityType.ElementalMastery:
                        mods.DamageMultiplier *= 1.3f * stackMult;
                        break;
                }

                // Clamp damage reduction to reasonable range
                if (mods.DamageReduction > 0.75f) mods.DamageReduction = 0.75f;
                if (mods.DamageReduction < -0.5f) mods.DamageReduction = -0.5f;
            }
        }
    }
}
