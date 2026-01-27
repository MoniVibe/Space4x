using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Time
{
    /// <summary>
    /// COLD path: Full history recompression/archiving.
    /// Expensive rewinds/scrubbing with UI.
    /// Rebuilding derived histories (graphs, analytics).
    /// Runs rarely, event-driven.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: UniversalPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct HistoryArchivalSystem : ISystem
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
            // In full implementation, would check for archival triggers or UI scrub requests
            if (timeState.Tick % 1000 != 0)
            {
                return; // Run every 1000 ticks
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.RewindOperationsThisTick >= budget.MaxRewindOperationsPerTick)
            {
                return;
            }

            // In full implementation, would:
            // 1. Recompress old history data
            // 2. Archive history beyond retention period
            // 3. Rebuild derived histories (graphs, analytics)
            // 4. Handle expensive UI scrubbing operations

            // For now, just increment counter
            counters.ValueRW.RewindOperationsThisTick++;
        }
    }
}

