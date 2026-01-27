using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Relations;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems.Relations
{
    /// <summary>
    /// Manages relations/econ/social performance budgets and counters.
    /// Resets counters each tick, enforces budgets, and logs warnings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup), OrderFirst = true)]
    public partial struct RelationPerformanceBudgetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            // Create budget singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<RelationPerformanceBudget>())
            {
                var budgetEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<RelationPerformanceBudget>(budgetEntity);
                state.EntityManager.SetComponentData(budgetEntity, RelationPerformanceBudget.CreateDefaults());
            }

            // Create counters singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<RelationPerformanceCounters>())
            {
                var countersEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<RelationPerformanceCounters>(countersEntity);
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
            var counters = SystemAPI.GetSingletonRW<RelationPerformanceCounters>();
            counters.ValueRW.RelationEventsThisTick = 0;
            counters.ValueRW.MarketUpdatesThisTick = 0;
            counters.ValueRW.PoliticalDecisionsThisTick = 0;
            counters.ValueRW.SocialInteractionsThisTick = 0;
            counters.ValueRW.OperationsDroppedThisTick = 0;
            counters.ValueRW.LastResetTick = timeState.Tick;

            // Count total personal relations
            int totalPersonalRelations = 0;
            foreach (var relations in SystemAPI.Query<DynamicBuffer<PersonalRelation>>())
            {
                totalPersonalRelations += relations.Length;
            }
            counters.ValueRW.TotalPersonalRelations = totalPersonalRelations;

            // Count total org relations (would need to query OrgRelation components)
            // For now, set to 0 - will be updated when OrgRelation system is refactored
            counters.ValueRW.TotalOrgRelations = 0;

            // Log warnings in dev builds if budgets exceeded or graph sizes too large
#if UNITY_EDITOR
            var budget = SystemAPI.GetSingleton<RelationPerformanceBudget>();
            
            if (counters.ValueRO.TotalPersonalRelations > budget.RelationGraphWarningThreshold)
            {
                UnityEngine.Debug.LogWarning($"[RelationPerformance] Personal relations graph size ({counters.ValueRO.TotalPersonalRelations}) exceeds warning threshold ({budget.RelationGraphWarningThreshold})");
            }

            if (counters.ValueRO.OperationsDroppedThisTick > 0)
            {
                UnityEngine.Debug.LogWarning($"[RelationPerformance] {counters.ValueRO.OperationsDroppedThisTick} operations dropped this tick due to budget exceeded");
            }
#endif
        }
    }
}

