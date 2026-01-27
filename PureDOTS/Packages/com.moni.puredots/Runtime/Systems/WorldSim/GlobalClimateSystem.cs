using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.WorldSim
{
    /// <summary>
    /// COLD path: Global climate shifts (seasonal changes, long-term terraforming).
    /// Re-seeding weather patterns.
    /// Recalculation of large-scale connectivity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: UniversalPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct GlobalClimateSystem : ISystem
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

            // Only run on very long intervals or event-driven triggers
            // In full implementation, would check for seasonal changes or terraforming events
            if (timeState.Tick % 500 != 0)
            {
                return; // Run every 500 ticks
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.WorldSimOperationsThisTick >= budget.MaxWorldSimOperationsPerTick)
            {
                return;
            }

            // In full implementation, would:
            // 1. Apply global climate shifts (seasonal changes, terraforming)
            // 2. Re-seed weather patterns
            // 3. Recalculate large-scale connectivity (flooded areas, cut-off regions)

            // For now, just increment counter
            counters.ValueRW.WorldSimOperationsThisTick++;
        }
    }
}

