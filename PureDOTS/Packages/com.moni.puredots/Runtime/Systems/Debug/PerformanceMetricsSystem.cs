using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Debug
{
    /// <summary>
    /// WARM/COLD path: Performance metrics and logging.
    /// Logging, profiling, visualization behind debug flags.
    /// Use sampling (log 1 in N events, not every event).
    /// Disable entirely in release builds or keep extremely minimal.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(UniversalPerformanceBudgetSystem))]
    public partial struct PerformanceMetricsSystem : ISystem
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
            if (counters.ValueRO.DebugLogsThisTick >= budget.MaxDebugLogsPerTick)
            {
                return;
            }

            // Only log on intervals (sampling)
            // In full implementation, would use sampling (log 1 in N events)
            if (timeState.Tick % 100 != 0)
            {
                return; // Sample every 100 ticks
            }

#if UNITY_EDITOR
            // In full implementation, would:
            // 1. Log performance metrics (behind debug flags)
            // 2. Profile system execution times
            // 3. Visualize performance data
            // 4. Use sampling to reduce overhead

            // For now, just increment counter
            counters.ValueRW.DebugLogsThisTick++;
#endif
        }
    }
}

