using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Power
{
    /// <summary>
    /// COLD path: Network rebuilding when new nodes/edges appear.
    /// Route optimization/rebalancing.
    /// Event-driven.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: UniversalPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct NetworkRebuildSystem : ISystem
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

            // Only run when network changes or on long intervals
            // In full implementation, would check for network dirty flags or events
            if (timeState.Tick % 200 != 0)
            {
                return; // Run every 200 ticks, or event-driven
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.NetworkRebuildsThisTick >= budget.MaxNetworkRebuildsPerTick)
            {
                return;
            }

            // In full implementation, would:
            // 1. Rebuild power/logistics networks when nodes/edges change
            // 2. Optimize routes
            // 3. Rebalance convoy assignments

            // For now, just increment counter
            counters.ValueRW.NetworkRebuildsThisTick++;
        }
    }
}

