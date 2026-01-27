using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Contracts;
using PureDOTS.Runtime.Production.Contracts;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Production.Contracts
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ContractQualityAggregationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ContractHarnessEnabled>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            foreach (var (request, result) in SystemAPI.Query<
                RefRW<ContractQualityRequest>,
                RefRW<ContractQualityResult>>())
            {
                if (request.ValueRO.LastProcessedTick == tick)
                {
                    continue;
                }

                request.ValueRW.LastProcessedTick = tick;
                result.ValueRW.LastProcessedTick = tick;

                var totalWeight = math.max(0.0001f, request.ValueRO.WeightA + request.ValueRO.WeightB);
                var weighted = (request.ValueRO.InputA * request.ValueRO.WeightA + request.ValueRO.InputB * request.ValueRO.WeightB) / totalWeight;
                result.ValueRW.OutputValue = math.clamp(weighted, request.ValueRO.MinValue, request.ValueRO.MaxValue);
            }
        }
    }
}
