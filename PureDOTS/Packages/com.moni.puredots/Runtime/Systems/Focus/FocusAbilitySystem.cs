using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Focus;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Focus
{
    /// <summary>
    /// Processes ability activation requests and manages active abilities.
    /// Updates drain rates and removes expired abilities.
    /// Runs after FocusRegenSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FocusSystemGroup))]
    [UpdateAfter(typeof(FocusRegenSystem))]
    public partial struct FocusAbilitySystem : ISystem
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

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process activation requests
            foreach (var (request, focus, abilities, entity) in SystemAPI
                .Query<RefRO<FocusAbilityRequest>, RefRW<EntityFocus>, DynamicBuffer<ActiveFocusAbility>>()
                .WithEntityAccess())
            {
                var req = request.ValueRO;

                if (req.Activate)
                {
                    // Check if ability is already active
                    bool alreadyActive = false;
                    for (int i = 0; i < abilities.Length; i++)
                    {
                        if (abilities[i].AbilityType == req.RequestedAbility)
                        {
                            alreadyActive = true;
                            break;
                        }
                    }

                    if (!alreadyActive && !focus.ValueRO.IsInComa)
                    {
                        // Get ability cost
                        float drainRate = FocusAbilityDefinitions.GetDrainRate(req.RequestedAbility);
                        float duration = req.DurationOverride > 0 ? req.DurationOverride : 0f;

                        // Check if we have enough focus to activate
                        if (focus.ValueRO.CurrentFocus >= drainRate * 0.5f) // Need at least 0.5s worth
                        {
                            var newAbility = new ActiveFocusAbility
                            {
                                AbilityType = req.RequestedAbility,
                                ActivatedTick = currentTick,
                                DurationRemaining = duration,
                                DrainRate = drainRate,
                                Stacks = 1
                            };

                            abilities.Add(newAbility);
                        }
                    }
                }
                else
                {
                    // Deactivate ability
                    for (int i = abilities.Length - 1; i >= 0; i--)
                    {
                        if (abilities[i].AbilityType == req.RequestedAbility)
                        {
                            abilities.RemoveAt(i);
                            break;
                        }
                    }
                }

                // Remove request component
                ecb.RemoveComponent<FocusAbilityRequest>(entity);
            }

            // Update active abilities and calculate total drain
            new UpdateAbilitiesJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct UpdateAbilitiesJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;

            void Execute(ref EntityFocus focus, ref DynamicBuffer<ActiveFocusAbility> abilities)
            {
                // Skip if in coma - deactivate all abilities
                if (focus.IsInComa)
                {
                    abilities.Clear();
                    focus.TotalDrainRate = 0f;
                    return;
                }

                float totalDrain = 0f;

                // Update abilities and remove expired ones
                for (int i = abilities.Length - 1; i >= 0; i--)
                {
                    var ability = abilities[i];

                    // Check duration
                    if (ability.DurationRemaining > 0f)
                    {
                        ability.DurationRemaining -= DeltaTime;
                        if (ability.DurationRemaining <= 0f)
                        {
                            // Ability expired
                            abilities.RemoveAt(i);
                            continue;
                        }
                        abilities[i] = ability;
                    }

                    // Check if we have enough focus to maintain
                    if (focus.CurrentFocus < ability.DrainRate * DeltaTime)
                    {
                        // Not enough focus - deactivate
                        abilities.RemoveAt(i);
                        continue;
                    }

                    totalDrain += ability.DrainRate;
                }

                focus.TotalDrainRate = totalDrain;
            }
        }
    }

    /// <summary>
    /// Static definitions for focus abilities.
    /// Games can extend by adding custom ability ranges.
    /// </summary>
    public static class FocusAbilityDefinitions
    {
        /// <summary>
        /// Gets the drain rate per second for an ability.
        /// </summary>
        public static float GetDrainRate(FocusAbilityType ability)
        {
            return ability switch
            {
                // Finesse abilities (moderate drain)
                FocusAbilityType.Parry => 2f,
                FocusAbilityType.DualWieldStrike => 4f,
                FocusAbilityType.CriticalFocus => 3f,
                FocusAbilityType.DodgeBoost => 2.5f,
                FocusAbilityType.Riposte => 3f,
                FocusAbilityType.PrecisionStrike => 3.5f,
                FocusAbilityType.Feint => 1.5f,
                FocusAbilityType.QuickDraw => 2f,
                FocusAbilityType.BlindingSpeed => 8f, // High drain

                // Physique abilities (higher drain)
                FocusAbilityType.IgnorePain => 4f,
                FocusAbilityType.SweepAttack => 5f,
                FocusAbilityType.AttackSpeedBoost => 3f,
                FocusAbilityType.PowerStrike => 4f,
                FocusAbilityType.Charge => 6f,
                FocusAbilityType.Intimidate => 2f,
                FocusAbilityType.SecondWind => 5f,
                FocusAbilityType.Berserk => 10f, // Very high drain
                FocusAbilityType.IronWill => 3f,

                // Arcane abilities (variable drain)
                FocusAbilityType.SummonBoost => 4f,
                FocusAbilityType.ManaRegen => 2f,
                FocusAbilityType.SpellAmplify => 3.5f,
                FocusAbilityType.Multicast => 5f,
                FocusAbilityType.SpellShield => 3f,
                FocusAbilityType.Channeling => 2.5f,
                FocusAbilityType.ArcaneReserve => 6f,
                FocusAbilityType.ElementalMastery => 4f,
                FocusAbilityType.Dispel => 3f,

                // Crafting abilities (low-moderate drain)
                FocusAbilityType.MassProduction => 3f,
                FocusAbilityType.MasterworkFocus => 4f,
                FocusAbilityType.BatchCrafting => 2.5f,
                FocusAbilityType.MaterialSaver => 2f,
                FocusAbilityType.QualityControl => 2.5f,
                FocusAbilityType.ExpertFinish => 3f,
                FocusAbilityType.RapidAssembly => 4f,
                FocusAbilityType.InnovativeCraft => 3.5f,

                // Gathering abilities
                FocusAbilityType.SpeedGather => 2f,
                FocusAbilityType.EfficientGather => 2.5f,
                FocusAbilityType.GatherOverdrive => 6f, // High drain
                FocusAbilityType.CarefulExtract => 3f,
                FocusAbilityType.BonusYield => 2f,
                FocusAbilityType.PreserveNode => 1.5f,
                FocusAbilityType.MultiGather => 4f,

                // Healing abilities
                FocusAbilityType.MassHeal => 5f,
                FocusAbilityType.LifeClutch => 8f, // Emergency, high drain
                FocusAbilityType.IntensiveCare => 4f,
                FocusAbilityType.Stabilize => 3f,
                FocusAbilityType.Purify => 2.5f,
                FocusAbilityType.Regenerate => 2f,
                FocusAbilityType.SurgicalPrecision => 3.5f,

                // Teaching abilities
                FocusAbilityType.IntensiveLessons => 5f,
                FocusAbilityType.DeepTeaching => 3f,
                FocusAbilityType.GroupInstruction => 4f,
                FocusAbilityType.MentoringBond => 2f,
                FocusAbilityType.PracticalTraining => 2.5f,
                FocusAbilityType.InspiredTeaching => 3f,

                // Refining abilities
                FocusAbilityType.RapidRefine => 3f,
                FocusAbilityType.PureExtraction => 4f,
                FocusAbilityType.BatchRefine => 2.5f,
                FocusAbilityType.CatalystBoost => 2f,
                FocusAbilityType.WasteRecovery => 2.5f,
                FocusAbilityType.PrecisionRefine => 3f,

                _ => 1f // Default minimal drain
            };
        }

        /// <summary>
        /// Gets the archetype for an ability.
        /// </summary>
        public static FocusArchetype GetArchetype(FocusAbilityType ability)
        {
            byte abilityValue = (byte)ability;
            return abilityValue switch
            {
                >= 10 and <= 29 => FocusArchetype.Finesse,
                >= 30 and <= 49 => FocusArchetype.Physique,
                >= 50 and <= 69 => FocusArchetype.Arcane,
                >= 70 and <= 89 => FocusArchetype.Crafting,
                >= 90 and <= 109 => FocusArchetype.Gathering,
                >= 110 and <= 129 => FocusArchetype.Healing,
                >= 130 and <= 149 => FocusArchetype.Teaching,
                >= 150 and <= 169 => FocusArchetype.Refining,
                _ => FocusArchetype.None
            };
        }
    }
}
