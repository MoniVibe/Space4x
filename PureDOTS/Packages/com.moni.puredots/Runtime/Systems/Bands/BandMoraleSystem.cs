using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(BandAggregationSystem))]
    public partial struct BandMoraleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BandStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = time.FixedDeltaTime;

            foreach (var (stats, intent) in SystemAPI.Query<RefRW<BandStats>, RefRO<BandIntent>>())
            {
                var morale = stats.ValueRW.Morale;
                morale += intent.ValueRO.IntentWeight * deltaTime * 0.1f;
                morale = math.clamp(morale, 0f, 100f);
                stats.ValueRW.Morale = morale;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
