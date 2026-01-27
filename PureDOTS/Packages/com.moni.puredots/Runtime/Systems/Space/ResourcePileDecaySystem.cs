using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ResourcePileDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourcePile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (pile, meta, entity) in SystemAPI.Query<RefRW<ResourcePile>, RefRO<ResourcePileMeta>>().WithEntityAccess())
            {
                if (meta.ValueRO.DecaySeconds <= 0f)
                {
                    continue;
                }

                var decayRate = pile.ValueRO.Amount / meta.ValueRO.DecaySeconds;
                pile.ValueRW.Amount -= decayRate * deltaTime;
                if (pile.ValueRW.Amount <= 0.01f)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
