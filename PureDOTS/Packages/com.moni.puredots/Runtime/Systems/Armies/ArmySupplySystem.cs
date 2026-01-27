using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ArmyMovementSystem))]
    public partial struct ArmySupplySystem : ISystem
    {
        private ComponentLookup<AggregateEntity> _aggregateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _aggregateLookup = state.GetComponentLookup<AggregateEntity>(true);
            state.RequireForUpdate<ArmyStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var deltaTime = math.max(1e-3f, time.FixedDeltaTime);
            _aggregateLookup.Update(ref state);

            foreach (var (stats, armyId, entity) in SystemAPI.Query<RefRW<ArmyStats>, RefRO<ArmyId>>().WithEntityAccess())
            {
                var supply = stats.ValueRW.SupplyLevel;
                if (supply <= 0f)
                {
                    stats.ValueRW.Morale = math.max(0f, stats.ValueRO.Morale - deltaTime * 2f);
                    stats.ValueRW.Cohesion = math.max(0f, stats.ValueRO.Cohesion - deltaTime * 1.5f);
                    continue;
                }

                stats.ValueRW.SupplyLevel = math.max(0f, supply - deltaTime * 0.05f);
                stats.ValueRW.Morale = math.min(100f, stats.ValueRO.Morale + deltaTime * 0.2f);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
