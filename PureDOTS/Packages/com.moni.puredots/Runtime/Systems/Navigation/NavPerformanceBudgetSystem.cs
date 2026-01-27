using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Manages navigation performance budgets and counters.
    /// Resets counters each tick, enforces budgets, and logs warnings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup), OrderFirst = true)]
    public partial struct NavPerformanceBudgetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            // Create budget singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<NavPerformanceBudget>())
            {
                var budgetEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<NavPerformanceBudget>(budgetEntity);
                state.EntityManager.SetComponentData(budgetEntity, NavPerformanceBudget.CreateDefaults());
            }

            // Create counters singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<NavPerformanceCounters>())
            {
                var countersEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<NavPerformanceCounters>(countersEntity);
            }
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

            // Reset counters each tick
            var counters = SystemAPI.GetSingletonRW<NavPerformanceCounters>();
            counters.ValueRW.LocalPathQueriesThisTick = 0;
            counters.ValueRW.StrategicRouteQueriesThisTick = 0;
            counters.ValueRW.FlowFieldBuildsThisTick = 0;
            counters.ValueRW.RequestsDroppedThisTick = 0;
            counters.ValueRW.MaxQueueSizeThisTick = 0;
            counters.ValueRW.LastResetTick = timeState.Tick;

            // Update queue size tracking (find entity with NavRequestQueue buffer)
            var queueSize = 0;
            foreach (var queue in SystemAPI.Query<DynamicBuffer<NavRequestQueue>>())
            {
                queueSize += queue.Length;
                if (queue.Length > counters.ValueRW.MaxQueueSizeThisTick)
                {
                    counters.ValueRW.MaxQueueSizeThisTick = queue.Length;
                }
            }
            counters.ValueRW.NavRequestsQueued = queueSize;

            // Log warnings in dev builds if queue size exceeds threshold
#if UNITY_EDITOR
            var budget = SystemAPI.GetSingleton<NavPerformanceBudget>();
            if (counters.ValueRO.NavRequestsQueued > budget.QueueSizeWarningThreshold)
            {
                UnityEngine.Debug.LogWarning($"[NavPerformance] Queue size ({counters.ValueRO.NavRequestsQueued}) exceeds warning threshold ({budget.QueueSizeWarningThreshold})");
            }

            if (counters.ValueRO.RequestsDroppedThisTick > 0)
            {
                UnityEngine.Debug.LogWarning($"[NavPerformance] {counters.ValueRO.RequestsDroppedThisTick} requests dropped this tick due to budget exceeded");
            }
#endif
        }
    }
}

