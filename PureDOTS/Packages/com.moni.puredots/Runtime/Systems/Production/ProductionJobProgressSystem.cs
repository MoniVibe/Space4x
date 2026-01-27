using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Production;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Production
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionJobExecutionSystem))]
    public partial struct ProductionJobProgressSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return;
            }

            var tick = tickTime.Tick;

            foreach (var job in SystemAPI.Query<RefRW<ProductionJob>>())
            {
                if (job.ValueRO.State != ProductionJobState.Executing)
                {
                    continue;
                }

                var lastTick = job.ValueRO.LastUpdateTick;
                if (lastTick == 0)
                {
                    job.ValueRW.LastUpdateTick = tick;
                    continue;
                }

                var delta = tick > lastTick ? tick - lastTick : 0u;
                if (delta == 0)
                {
                    continue;
                }

                var remaining = job.ValueRO.RemainingTicks == 0 ? job.ValueRO.TotalTicks : job.ValueRO.RemainingTicks;
                if (delta >= remaining)
                {
                    job.ValueRW.RemainingTicks = 0;
                    job.ValueRW.State = ProductionJobState.Delivering;
                }
                else
                {
                    job.ValueRW.RemainingTicks = remaining - delta;
                }

                job.ValueRW.LastUpdateTick = tick;
            }
        }
    }
}
