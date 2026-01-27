using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Debugging
{
    /// <summary>
    /// Debug overlay system for navigation performance monitoring.
    /// Displays performance counters, budgets, and warnings in debug builds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct NavPerformanceDebugSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NavPerformanceBudget>();
            state.RequireForUpdate<NavPerformanceCounters>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Debug overlay is non-Burst, so we skip in Burst builds
#if !UNITY_BURST_EXPERIMENTAL_LOOP_INTRINSICS
            if (!SystemAPI.HasSingleton<NavPerformanceBudget>() || !SystemAPI.HasSingleton<NavPerformanceCounters>())
            {
                return;
            }

            var budget = SystemAPI.GetSingleton<NavPerformanceBudget>();
            var counters = SystemAPI.GetSingleton<NavPerformanceCounters>();

            // Display performance info in debug overlay
            // This would integrate with your existing debug overlay system
            // For now, we just log warnings when budgets are exceeded

            if (counters.LocalPathQueriesThisTick >= budget.MaxLocalPathQueriesPerTick)
            {
                Debug.LogWarning($"[NavPerformance] Local path queries budget exceeded: {counters.LocalPathQueriesThisTick}/{budget.MaxLocalPathQueriesPerTick}");
            }

            if (counters.StrategicRouteQueriesThisTick >= budget.MaxStrategicRoutePlansPerTick)
            {
                Debug.LogWarning($"[NavPerformance] Strategic route queries budget exceeded: {counters.StrategicRouteQueriesThisTick}/{budget.MaxStrategicRoutePlansPerTick}");
            }

            if (counters.NavRequestsQueued > budget.QueueSizeWarningThreshold)
            {
                Debug.LogWarning($"[NavPerformance] Queue size exceeds threshold: {counters.NavRequestsQueued} > {budget.QueueSizeWarningThreshold}");
            }

            if (counters.RequestsDroppedThisTick > 0)
            {
                Debug.LogWarning($"[NavPerformance] {counters.RequestsDroppedThisTick} requests dropped this tick");
            }
#endif
        }
    }
}

