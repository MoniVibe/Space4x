using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Contracts;
using PureDOTS.Runtime.Needs.Contracts;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Needs.Contracts
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ContractNeedOverrideSystem : ISystem
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
            foreach (var (needs, policy, intent) in SystemAPI.Query<
                RefRO<ContractNeedState>,
                RefRO<ContractNeedOverridePolicy>,
                RefRW<ContractNeedOverrideIntent>>())
            {
                var isCritical = needs.ValueRO.Hunger >= policy.ValueRO.CriticalHunger ||
                                 needs.ValueRO.Health <= policy.ValueRO.CriticalHealth;

                if (isCritical)
                {
                    if (intent.ValueRO.Active == 0 ||
                        tick - intent.ValueRO.LastChangedTick >= policy.ValueRO.MinStableTicks)
                    {
                        intent.ValueRW.Active = 1;
                        intent.ValueRW.Reason = 1;
                        intent.ValueRW.LastChangedTick = tick;
                    }
                    continue;
                }

                if (intent.ValueRO.Active == 1 &&
                    tick - intent.ValueRO.LastChangedTick >= policy.ValueRO.MinStableTicks)
                {
                    intent.ValueRW.Active = 0;
                    intent.ValueRW.Reason = 0;
                    intent.ValueRW.LastChangedTick = tick;
                }
            }
        }
    }
}
