using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Social
{
    /// <summary>
    /// Normalizes custody state and provides a small deterministic state machine bootstrap:
    /// Captured -> Detained, defaulting captor/holding entities, and stamping ticks.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(AISystemGroup))]
    public partial struct CustodyCanonicalizationSystem : ISystem
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
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (custodyRef, entity) in SystemAPI.Query<RefRW<CustodyState>>().WithEntityAccess())
            {
                var custody = custodyRef.ValueRO;
                var changed = false;

                if (custody.CapturedTick == 0u)
                {
                    custody.CapturedTick = tick;
                    changed = true;
                }

                if (custody.LastStatusTick == 0u)
                {
                    custody.LastStatusTick = tick;
                    changed = true;
                }

                if (custody.CaptorScope == Entity.Null && custody.HoldingEntity != Entity.Null)
                {
                    custody.CaptorScope = custody.HoldingEntity;
                    changed = true;
                }

                if (custody.HoldingEntity == Entity.Null && custody.CaptorScope != Entity.Null)
                {
                    custody.HoldingEntity = custody.CaptorScope;
                    changed = true;
                }

                if (custody.Status == CustodyStatus.Captured)
                {
                    custody.Status = CustodyStatus.Detained;
                    custody.LastStatusTick = tick;
                    changed = true;
                }

                if (changed)
                {
                    ecb.SetComponent(entity, custody);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

