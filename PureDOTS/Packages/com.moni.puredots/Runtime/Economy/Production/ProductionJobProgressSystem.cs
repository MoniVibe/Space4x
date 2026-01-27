using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy.Production
{
    /// <summary>
    /// Advances production jobs over time, applying speed modifiers.
    /// Worker skill, business tooling, fatigue affect progress speed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProductionJobProgressSystem : ISystem
    {
        private ComponentLookup<BusinessProduction> _productionLookup;
        private BufferLookup<ProductionJob> _jobBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _productionLookup = state.GetComponentLookup<BusinessProduction>(false);
            _jobBufferLookup = state.GetBufferLookup<ProductionJob>(false);
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

            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var timeScale = math.max(0f, tickTimeState.CurrentSpeedMultiplier);
            var deltaTime = tickTimeState.FixedDeltaTime * timeScale;

            _productionLookup.Update(ref state);
            _jobBufferLookup.Update(ref state);

            foreach (var (production, entity) in SystemAPI.Query<RefRW<BusinessProduction>>().WithEntityAccess())
            {
                if (!_jobBufferLookup.HasBuffer(entity))
                {
                    continue;
                }

                var jobs = _jobBufferLookup[entity];
                var throughput = production.ValueRO.Throughput;

                for (int i = 0; i < jobs.Length; i++)
                {
                    var job = jobs[i];
                    
                    // Advance progress based on throughput and time
                    // Simple model: progress = worker-hours / base time cost
                    float progressDelta = (throughput * deltaTime) / job.BaseTimeCost;
                    job.RemainingTime = math.max(0f, job.RemainingTime - (throughput * deltaTime));
                    job.Progress = 1f - (job.RemainingTime / job.BaseTimeCost);

                    jobs[i] = job;
                }

                production.ValueRW.LastUpdateTick = tickTimeState.Tick;
            }
        }
    }
}

