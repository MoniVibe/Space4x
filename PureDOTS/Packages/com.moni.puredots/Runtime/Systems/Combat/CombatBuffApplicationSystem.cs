using PureDOTS.Runtime.Buffs;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Ensures combat entities have BuffStatCache for buff integration.
    /// The actual buff stat application is handled by BuffStatAggregationSystem.
    /// DamageApplicationSystem and HitDetectionSystem read BuffStatCache for modifiers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(HitDetectionSystem))]
    public partial struct CombatBuffApplicationSystem : ISystem
    {
        private ComponentLookup<BuffStatCache> _buffStatCacheLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _buffStatCacheLookup = state.GetComponentLookup<BuffStatCache>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _buffStatCacheLookup.Update(ref state);

            // Ensure entities in combat have BuffStatCache if they have active buffs
            var jobHandle = new EnsureBuffCacheJob
            {
                Ecb = ecb,
                BuffStatCacheLookup = _buffStatCacheLookup
            }.ScheduleParallel(state.Dependency);

            state.Dependency = jobHandle;
        }

        [BurstCompile]
        public partial struct EnsureBuffCacheJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<BuffStatCache> BuffStatCacheLookup;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                in ActiveCombat combat,
                in DynamicBuffer<ActiveBuff> activeBuffs)
            {
                // If entity has active buffs but no BuffStatCache, create it
                // BuffStatAggregationSystem will populate it
                if (activeBuffs.Length > 0 && !BuffStatCacheLookup.HasComponent(entity))
                {
                    Ecb.AddComponent(entityInQueryIndex, entity, new BuffStatCache());
                }
            }
        }
    }
}

