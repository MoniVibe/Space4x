using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems.Performance
{
    /// <summary>
    /// Manages universal performance budgets and counters across all domains.
    /// Resets counters each tick, enforces budgets, and logs warnings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup), OrderFirst = true)]
    public partial struct UniversalPerformanceBudgetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            // Create budget singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<UniversalPerformanceBudget>())
            {
                var budgetEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<UniversalPerformanceBudget>(budgetEntity);
                state.EntityManager.SetComponentData(budgetEntity, UniversalPerformanceBudget.CreateDefaults());
            }

            // Create counters singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<UniversalPerformanceCounters>())
            {
                var countersEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<UniversalPerformanceCounters>(countersEntity);
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

            // Reset all counters each tick
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();
            
            // Perception
            counters.ValueRW.PerceptionChecksThisTick = 0;
            counters.ValueRW.AwarenessUpdatesThisTick = 0;
            counters.ValueRW.LosRaysAttemptedThisTick = 0;
            counters.ValueRW.LosRaysGrantedThisTick = 0;
            counters.ValueRW.LosRaysDeferredThisTick = 0;
            counters.ValueRW.LosChecksPhysicsThisTick = 0;
            counters.ValueRW.LosChecksObstacleGridThisTick = 0;
            counters.ValueRW.LosChecksUnknownThisTick = 0;
            counters.ValueRW.SignalCellsSampledThisTick = 0;
            counters.ValueRW.MiracleEntitiesDetectedThisTick = 0;
            counters.ValueRW.AckEventsEmittedThisTick = 0;
            counters.ValueRW.AckEventsDroppedThisTick = 0;
            counters.ValueRW.CommsMessagesEmittedThisTick = 0;
            counters.ValueRW.CommsMessagesDroppedThisTick = 0;
            counters.ValueRW.CommsReceiptsThisTick = 0;

            // Combat
            counters.ValueRW.CombatOperationsThisTick = 0;
            counters.ValueRW.TargetSelectionsThisTick = 0;
            counters.ValueRW.AbilityEvaluationsThisTick = 0;

            // AI
            counters.ValueRW.ReflexOperationsThisTick = 0;
            counters.ValueRW.TacticalDecisionsThisTick = 0;
            counters.ValueRW.OperationalDecisionsThisTick = 0;
            counters.ValueRW.StrategicDecisionsThisTick = 0;

            // Jobs
            counters.ValueRW.JobReassignmentsThisTick = 0;
            counters.ValueRW.ScheduleEvaluationsThisTick = 0;

            // World Sim
            counters.ValueRW.CellUpdatesThisTick = 0;
            counters.ValueRW.PowerFlowSolvesThisTick = 0;
            counters.ValueRW.WorldSimOperationsThisTick = 0;

            // Relations (would sync with RelationPerformanceCounters if needed)
            counters.ValueRW.RelationEventsThisTick = 0;
            counters.ValueRW.SocialInteractionsThisTick = 0;

            // Navigation (would sync with NavPerformanceCounters if needed)
            counters.ValueRW.LocalPathQueriesThisTick = 0;
            counters.ValueRW.StrategicRouteQueriesThisTick = 0;

            // Time/Rewind
            counters.ValueRW.HistoryRecordsThisTick = 0;
            counters.ValueRW.RewindOperationsThisTick = 0;

            // Narrative
            counters.ValueRW.SituationUpdatesThisTick = 0;
            counters.ValueRW.TriggerEvaluationsThisTick = 0;

            // Power
            counters.ValueRW.NetworkRebuildsThisTick = 0;

            // Save/Load
            counters.ValueRW.SaveOperationsThisTick = 0;

            // Debug
            counters.ValueRW.DebugLogsThisTick = 0;

            // Aggregated totals
            counters.ValueRW.TotalHotOperationsThisTick = 0;
            counters.ValueRW.TotalWarmOperationsThisTick = 0;
            counters.ValueRW.TotalColdOperationsThisTick = 0;
            counters.ValueRW.TotalOperationsDroppedThisTick = 0;

            // Graph sizes (would be updated by graph systems)
            counters.ValueRW.TotalGraphEdges = 0;
            counters.ValueRW.TotalGraphNodes = 0;

            counters.ValueRW.LastResetTick = timeState.Tick;

            // Log warnings in dev builds if budgets exceeded
#if UNITY_EDITOR
            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            
            // Check individual domain budgets
            if (counters.ValueRO.PerceptionChecksThisTick > budget.MaxPerceptionChecksPerTick)
            {
                UnityEngine.Debug.LogWarning($"[UniversalPerformance] Perception checks ({counters.ValueRO.PerceptionChecksThisTick}) exceeded budget ({budget.MaxPerceptionChecksPerTick})");
            }

            if (counters.ValueRO.CombatOperationsThisTick > budget.MaxCombatOperationsPerTick)
            {
                UnityEngine.Debug.LogWarning($"[UniversalPerformance] Combat operations ({counters.ValueRO.CombatOperationsThisTick}) exceeded budget ({budget.MaxCombatOperationsPerTick})");
            }

            if (counters.ValueRO.TotalWarmOperationsThisTick > budget.TotalOperationsWarningThreshold)
            {
                UnityEngine.Debug.LogWarning($"[UniversalPerformance] Total warm operations ({counters.ValueRO.TotalWarmOperationsThisTick}) exceeded warning threshold ({budget.TotalOperationsWarningThreshold})");
            }

            if (counters.ValueRO.TotalColdOperationsThisTick > budget.TotalOperationsWarningThreshold)
            {
                UnityEngine.Debug.LogWarning($"[UniversalPerformance] Total cold operations ({counters.ValueRO.TotalColdOperationsThisTick}) exceeded warning threshold ({budget.TotalOperationsWarningThreshold})");
            }

            if (counters.ValueRO.TotalOperationsDroppedThisTick > 0)
            {
                UnityEngine.Debug.LogWarning($"[UniversalPerformance] {counters.ValueRO.TotalOperationsDroppedThisTick} operations dropped this tick due to budget exceeded");
            }
#endif
        }
    }
}

