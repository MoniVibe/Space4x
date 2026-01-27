using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using EffectiveDeltaTime = PureDOTS.Runtime.Time.EffectiveDeltaTime;

namespace PureDOTS.Systems.Combat
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CombatLoopSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CombatLoopState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var globalDelta = SystemAPI.Time.DeltaTime;
            var effectiveDeltaLookup = SystemAPI.GetComponentLookup<EffectiveDeltaTime>(true);
            effectiveDeltaLookup.Update(ref state);
            
            foreach (var (config, loopState, transform, entity) in SystemAPI
                         .Query<RefRO<CombatLoopConfig>, RefRW<CombatLoopState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                ref var stateRW = ref loopState.ValueRW;
                // Use effective delta for cooldown (Phase 1: time correctness)
                float delta = TimeAwareHelpers.GetEffectiveDelta(effectiveDeltaLookup, entity, globalDelta);
                stateRW.WeaponCooldown = math.max(0f, stateRW.WeaponCooldown - delta);

                switch (stateRW.Phase)
                {
                    case CombatLoopPhase.Idle:
                        stateRW.Phase = CombatLoopPhase.Patrol;
                        stateRW.PhaseTimer = 1f;
                        break;
                    case CombatLoopPhase.Patrol:
                        stateRW.PhaseTimer -= delta;
                        if (stateRW.PhaseTimer <= 0f)
                        {
                            stateRW.Phase = CombatLoopPhase.Intercept;
                            stateRW.PhaseTimer = 1f;
                        }
                        break;
                    case CombatLoopPhase.Intercept:
                        stateRW.PhaseTimer -= delta;
                        if (stateRW.PhaseTimer <= 0f)
                        {
                            stateRW.Phase = CombatLoopPhase.Attack;
                        }
                        break;
                    case CombatLoopPhase.Attack:
                        if (stateRW.WeaponCooldown <= 0f)
                        {
                            stateRW.WeaponCooldown = config.ValueRO.WeaponCooldownSeconds;
                        }
                        stateRW.Phase = CombatLoopPhase.Retreat;
                        stateRW.PhaseTimer = 2f;
                        break;
                    case CombatLoopPhase.Retreat:
                        stateRW.PhaseTimer -= delta;
                        if (stateRW.PhaseTimer <= 0f)
                        {
                            stateRW.Phase = CombatLoopPhase.Patrol;
                        }
                        break;
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
