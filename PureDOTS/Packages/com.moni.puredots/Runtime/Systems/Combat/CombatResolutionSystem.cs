using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Processes ActiveCombat entities, coordinating combat flow, stance modifiers, morale checks, and combat end conditions.
    /// HitDetectionSystem handles actual hit/miss rolls and damage event creation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(HitDetectionSystem))]
    public partial struct CombatResolutionSystem : ISystem
    {
        private EntityStorageInfoLookup _entityLookup;
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<CombatStats> _combatStatsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _entityLookup = state.GetEntityStorageInfoLookup();
            _healthLookup = state.GetComponentLookup<Health>(true);
            _combatStatsLookup = state.GetComponentLookup<CombatStats>(true);
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
            var currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _entityLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _combatStatsLookup.Update(ref state);

            var jobHandle = new ProcessCombatJob
            {
                CurrentTick = currentTick,
                Ecb = ecb,
                EntityLookup = _entityLookup,
                HealthLookup = _healthLookup,
                CombatStatsLookup = _combatStatsLookup
            }.ScheduleParallel(state.Dependency);

            state.Dependency = jobHandle;
        }

        [BurstCompile]
        public partial struct ProcessCombatJob : IJobEntity
        {
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public EntityStorageInfoLookup EntityLookup;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<CombatStats> CombatStatsLookup;

            void Execute(
                Entity combatEntity,
                [EntityIndexInQuery] int entityInQueryIndex,
                ref ActiveCombat combat)
            {
                // Validate combatants exist
                if (!EntityLookup.Exists(combat.Combatant1) || !EntityLookup.Exists(combat.Combatant2))
                {
                    // End combat if combatants are invalid
                    Ecb.RemoveComponent<ActiveCombat>(entityInQueryIndex, combatEntity);
                    return;
                }

                // Check if combatants are dead
                bool combatant1Dead = false;
                bool combatant2Dead = false;

                if (HealthLookup.HasComponent(combat.Combatant1))
                {
                    var health1 = HealthLookup[combat.Combatant1];
                    combatant1Dead = health1.Current <= 0f;
                }

                if (HealthLookup.HasComponent(combat.Combatant2))
                {
                    var health2 = HealthLookup[combat.Combatant2];
                    combatant2Dead = health2.Current <= 0f;
                }

                // End combat if someone died
                if (combatant1Dead || combatant2Dead)
                {
                    Ecb.RemoveComponent<ActiveCombat>(entityInQueryIndex, combatEntity);
                    return;
                }

                // Check morale/yield thresholds
                if (CombatStatsLookup.HasComponent(combat.Combatant1))
                {
                    var stats1 = CombatStatsLookup[combat.Combatant1];
                    if (HealthLookup.HasComponent(combat.Combatant1))
                    {
                        var health1 = HealthLookup[combat.Combatant1];
                        float healthPercent = health1.Current / health1.Max;
                        if (healthPercent < (stats1.Morale / 100f))
                        {
                            // Combatant1 yields
                            Ecb.RemoveComponent<ActiveCombat>(entityInQueryIndex, combatEntity);
                            return;
                        }
                    }
                }

                if (CombatStatsLookup.HasComponent(combat.Combatant2))
                {
                    var stats2 = CombatStatsLookup[combat.Combatant2];
                    if (HealthLookup.HasComponent(combat.Combatant2))
                    {
                        var health2 = HealthLookup[combat.Combatant2];
                        float healthPercent = health2.Current / health2.Max;
                        if (healthPercent < (stats2.Morale / 100f))
                        {
                            // Combatant2 yields
                            Ecb.RemoveComponent<ActiveCombat>(entityInQueryIndex, combatEntity);
                            return;
                        }
                    }
                }

                // Check first blood condition
                if (combat.IsDuelToFirstBlood)
                {
                    if (combat.Combatant1Damage > 0 || combat.Combatant2Damage > 0)
                    {
                        // First blood drawn - end combat
                        Ecb.RemoveComponent<ActiveCombat>(entityInQueryIndex, combatEntity);
                        return;
                    }
                }

                // Advance combat round (attacks happen via HitDetectionSystem based on AttackSpeed)
                combat.CurrentRound++;

                // Apply stance modifiers to combat stats (temporary for this round)
                // Note: Actual damage modifiers are applied in HitDetectionSystem
                ApplyStanceModifiers(in combat.Combatant1, in combat.Combatant1Stance);
                ApplyStanceModifiers(in combat.Combatant2, in combat.Combatant2Stance);
            }

            [BurstCompile]
            private static void ApplyStanceModifiers(in Entity combatant, in ActiveCombat.CombatStance stance)
            {
                // Stance modifiers are applied dynamically during hit/damage calculation
                // This is a placeholder - actual modifiers applied in HitDetectionSystem/DamageApplicationSystem
            }
        }
    }
}

