using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Narrative;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Narrative
{
    /// <summary>
    /// WARM path: Situation progression.
    /// Each active situation/operation updates every M ticks.
    /// Check if conditions met (time elapsed, flags, counters).
    /// Few conditions per situation, not scanning entire world.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(UniversalPerformanceBudgetSystem))]
    public partial struct SituationProgressionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<UniversalPerformanceBudget>();
            state.RequireForUpdate<UniversalPerformanceCounters>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.SituationUpdatesThisTick >= budget.MaxSituationUpdatesPerTick)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Process active situations/operations
            // In full implementation, would query for Situation/Operation components
            foreach (var (cadence, entity) in
                SystemAPI.Query<RefRO<UpdateCadence>>()
                .WithEntityAccess())
            {
                // Check update cadence (situations update every M ticks)
                if (!UpdateCadenceHelpers.ShouldUpdate(timeState.Tick, cadence.ValueRO))
                {
                    continue;
                }

                // Check budget
                if (counters.ValueRO.SituationUpdatesThisTick >= budget.MaxSituationUpdatesPerTick)
                {
                    break;
                }

                // In full implementation, would:
                // 1. Check if time elapsed conditions met
                // 2. Check if flags/counters conditions met
                // 3. Progress situation state
                // 4. Update NarrativeSnapshot for affected entities

                counters.ValueRW.SituationUpdatesThisTick++;
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

