using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Combat.State;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Updates squad cohesion and calculates cohesion multipliers for combat stats.
    /// 
    /// This system:
    /// - Updates SquadCohesion based on combat state (degrades under fire, recovers when not fighting)
    /// - Calculates CohesionCombatMultipliers based on cohesion level
    /// 
    /// Combat resolution systems should read CohesionCombatMultipliers components and apply
    /// the multipliers during hit chance, damage, and defense calculations.
    /// 
    /// See DamageApplicationSystem for example of reading multipliers during combat resolution.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CohesionEffectSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var deltaTime = SystemAPI.Time.DeltaTime;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var combatStateLookup = SystemAPI.GetComponentLookup<CombatStateData>(true);

            // Update cohesion for squads/bands
            foreach (var (cohesion, entity) in SystemAPI.Query<
                RefRW<SquadCohesion>>()
                .WithEntityAccess())
            {
                bool isInCombat = false;
                if (combatStateLookup.HasComponent(entity))
                {
                    var combatState = combatStateLookup[entity];
                    isInCombat = combatState.Current == CombatState.Engaged ||
                                 combatState.Current == CombatState.Attacking ||
                                 combatState.Current == CombatState.Defending;
                }

                // Update cohesion based on combat state
                if (isInCombat)
                {
                    // Degrade under fire
                    cohesion.ValueRW.CohesionLevel = math.max(0f,
                        cohesion.ValueRO.CohesionLevel - (cohesion.ValueRO.DegradationRate * deltaTime));
                }
                else
                {
                    // Recover when not fighting
                    cohesion.ValueRW.CohesionLevel = math.min(1f,
                        cohesion.ValueRO.CohesionLevel + (cohesion.ValueRO.RegenRate * deltaTime));
                }

                // Update threshold
                cohesion.ValueRW.Threshold = CohesionEffectService.GetCohesionThreshold(
                    cohesion.ValueRO.CohesionLevel);
                cohesion.ValueRW.LastUpdatedTick = currentTick;

                // Update combat multipliers
                if (!SystemAPI.HasComponent<CohesionCombatMultipliers>(entity))
                {
                    ecb.AddComponent(entity, new CohesionCombatMultipliers());
                }
            }

            // Apply cohesion multipliers to combat stats
            foreach (var (multipliers, cohesion, stats) in SystemAPI.Query<
                RefRW<CohesionCombatMultipliers>,
                RefRO<SquadCohesion>,
                RefRW<CombatStats>>())
            {
                float cohesionLevel = cohesion.ValueRO.CohesionLevel;

                // Calculate multipliers based on cohesion level
                // These multipliers are read by combat resolution systems (e.g., DamageApplicationSystem)
                multipliers.ValueRW.AccuracyMultiplier = 1f + CohesionEffectService.GetCohesionAccuracyBonus(cohesionLevel);
                multipliers.ValueRW.DamageMultiplier = CohesionEffectService.GetCohesionAttackMultiplier(cohesionLevel);
                multipliers.ValueRW.DefenseMultiplier = CohesionEffectService.GetCohesionDefenseMultiplier(cohesionLevel);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

