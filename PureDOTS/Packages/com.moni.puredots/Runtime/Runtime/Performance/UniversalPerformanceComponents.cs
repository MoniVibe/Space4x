using Unity.Entities;

namespace PureDOTS.Runtime.Performance
{
    /// <summary>
    /// Universal performance budget configuration singleton.
    /// Aggregates budgets across all domains (perception, combat, AI, world sim, etc.).
    /// </summary>
    public struct UniversalPerformanceBudget : IComponentData
    {
        // Perception & Knowledge
        public int MaxPerceptionChecksPerTick;
        public int MaxAwarenessUpdatesPerTick;
        public int MaxLosRaysPerTick;
        public int MaxSignalCellsSampledPerTick;
        public float LosChecksUnknownWarningRatio;
        public int MaxAckEventsPerTick;
        public int MaxCommsMessagesPerTick;

        // Combat & Damage
        public int MaxCombatOperationsPerTick;
        public int MaxTargetSelectionsPerTick;
        public int MaxAbilityEvaluationsPerTick;

        // AI Brain Layers
        public int MaxReflexOperationsPerTick;
        public int MaxTacticalDecisionsPerTick;
        public int MaxOperationalDecisionsPerTick;
        public int MaxStrategicDecisionsPerTick;

        // Jobs & Schedules
        public int MaxJobReassignmentsPerTick;
        public int MaxScheduleEvaluationsPerTick;

        // World Sim (Weather, Fire, Disease, Ecology, Power)
        public int MaxCellUpdatesPerTick;
        public int MaxPowerFlowSolvesPerTick;
        public int MaxWorldSimOperationsPerTick;

        // Relations & Social (already defined in RelationPerformanceBudget, but included here for aggregation)
        public int MaxRelationEventsPerTick;
        public int MaxSocialInteractionsPerTick;

        // Navigation (already defined in NavPerformanceBudget, but included here for aggregation)
        public int MaxLocalPathQueriesPerTick;
        public int MaxStrategicRoutePlansPerTick;

        // Time, Rewind & Logging
        public int MaxHistoryRecordsPerTick;
        public int MaxRewindOperationsPerTick;

        // Narrative & Situation Triggers
        public int MaxSituationUpdatesPerTick;
        public int MaxTriggerEvaluationsPerTick;

        // Power & Infrastructure
        public int MaxNetworkRebuildsPerTick;

        // Save/Load
        public int MaxSaveOperationsPerTick;

        // Debug & Metrics
        public int MaxDebugLogsPerTick;

        // Warning thresholds
        public int TotalOperationsWarningThreshold;
        public int GraphSizeWarningThreshold;

        /// <summary>
        /// Creates default budget configuration.
        /// </summary>
        public static UniversalPerformanceBudget CreateDefaults()
        {
            return new UniversalPerformanceBudget
            {
                // Perception
                MaxPerceptionChecksPerTick = 20,
                MaxAwarenessUpdatesPerTick = 15,
                MaxLosRaysPerTick = 24,
                MaxSignalCellsSampledPerTick = 1000,
                LosChecksUnknownWarningRatio = 0.5f,
                MaxAckEventsPerTick = 64,
                MaxCommsMessagesPerTick = 32,

                // Combat
                MaxCombatOperationsPerTick = 30,
                MaxTargetSelectionsPerTick = 25,
                MaxAbilityEvaluationsPerTick = 20,

                // AI
                MaxReflexOperationsPerTick = 100, // Hot path, but still cap it
                MaxTacticalDecisionsPerTick = 40,
                MaxOperationalDecisionsPerTick = 10,
                MaxStrategicDecisionsPerTick = 5,

                // Jobs
                MaxJobReassignmentsPerTick = 15,
                MaxScheduleEvaluationsPerTick = 20,

                // World Sim
                MaxCellUpdatesPerTick = 50,
                MaxPowerFlowSolvesPerTick = 10,
                MaxWorldSimOperationsPerTick = 30,

                // Relations (from RelationPerformanceBudget defaults)
                MaxRelationEventsPerTick = 20,
                MaxSocialInteractionsPerTick = 15,

                // Navigation (from NavPerformanceBudget defaults)
                MaxLocalPathQueriesPerTick = 50,
                MaxStrategicRoutePlansPerTick = 5,

                // Time/Rewind
                MaxHistoryRecordsPerTick = 100,
                MaxRewindOperationsPerTick = 10,

                // Narrative
                MaxSituationUpdatesPerTick = 20,
                MaxTriggerEvaluationsPerTick = 10,

                // Power
                MaxNetworkRebuildsPerTick = 2,

                // Save/Load
                MaxSaveOperationsPerTick = 1,

                // Debug
                MaxDebugLogsPerTick = 50,

                // Warnings
                TotalOperationsWarningThreshold = 500,
                GraphSizeWarningThreshold = 1000
            };
        }
    }

    /// <summary>
    /// Universal performance counters singleton.
    /// Tracks actual usage per tick across all domains for monitoring and enforcement.
    /// </summary>
    public struct UniversalPerformanceCounters : IComponentData
    {
        // Perception & Knowledge
        public int PerceptionChecksThisTick;
        public int AwarenessUpdatesThisTick;
        public int LosRaysAttemptedThisTick;
        public int LosRaysGrantedThisTick;
        public int LosRaysDeferredThisTick;
        public int LosChecksPhysicsThisTick;
        public int LosChecksObstacleGridThisTick;
        public int LosChecksUnknownThisTick;
        public int SignalCellsSampledThisTick;
        public int MiracleEntitiesDetectedThisTick;
        public int AckEventsEmittedThisTick;
        public int AckEventsDroppedThisTick;
        public int CommsMessagesEmittedThisTick;
        public int CommsMessagesDroppedThisTick;
        public int CommsReceiptsThisTick;

        // Combat & Damage
        public int CombatOperationsThisTick;
        public int TargetSelectionsThisTick;
        public int AbilityEvaluationsThisTick;

        // AI Brain Layers
        public int ReflexOperationsThisTick;
        public int TacticalDecisionsThisTick;
        public int OperationalDecisionsThisTick;
        public int StrategicDecisionsThisTick;

        // Jobs & Schedules
        public int JobReassignmentsThisTick;
        public int ScheduleEvaluationsThisTick;

        // World Sim
        public int CellUpdatesThisTick;
        public int PowerFlowSolvesThisTick;
        public int WorldSimOperationsThisTick;

        // Relations & Social
        public int RelationEventsThisTick;
        public int SocialInteractionsThisTick;

        // Navigation
        public int LocalPathQueriesThisTick;
        public int StrategicRouteQueriesThisTick;

        // Time, Rewind & Logging
        public int HistoryRecordsThisTick;
        public int RewindOperationsThisTick;

        // Narrative & Situation Triggers
        public int SituationUpdatesThisTick;
        public int TriggerEvaluationsThisTick;

        // Power & Infrastructure
        public int NetworkRebuildsThisTick;

        // Save/Load
        public int SaveOperationsThisTick;

        // Debug & Metrics
        public int DebugLogsThisTick;

        // Aggregated totals
        public int TotalHotOperationsThisTick;
        public int TotalWarmOperationsThisTick;
        public int TotalColdOperationsThisTick;
        public int TotalOperationsDroppedThisTick;

        // Graph sizes
        public int TotalGraphEdges;
        public int TotalGraphNodes;

        // Tick tracking
        public uint LastResetTick;
    }

    /// <summary>
    /// Helper methods for budget checking.
    /// </summary>
    public static class PerformanceBudgetHelpers
    {
        /// <summary>
        /// Checks if a warm path operation can proceed based on budget.
        /// </summary>
        public static bool CanPerformWarmOperation(
            ref UniversalPerformanceBudget budget,
            ref UniversalPerformanceCounters counters,
            int operationType)
        {
            // Map operation type to budget field
            // This is a simplified version - in practice, use enums or more specific checks
            return counters.TotalWarmOperationsThisTick < budget.TotalOperationsWarningThreshold;
        }

        /// <summary>
        /// Checks if a cold path operation can proceed based on budget.
        /// </summary>
        public static bool CanPerformColdOperation(
            ref UniversalPerformanceBudget budget,
            ref UniversalPerformanceCounters counters,
            int operationType)
        {
            return counters.TotalColdOperationsThisTick < budget.TotalOperationsWarningThreshold;
        }

        /// <summary>
        /// Increments a counter and checks if budget is exceeded.
        /// </summary>
        public static bool IncrementAndCheckBudget(
            ref UniversalPerformanceCounters counters,
            ref int counterField,
            int budgetLimit)
        {
            counterField++;
            counters.TotalWarmOperationsThisTick++; // Aggregate tracking
            return counterField <= budgetLimit;
        }
    }
}

