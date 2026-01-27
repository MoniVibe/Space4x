using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Debugging
{
    /// <summary>
    /// Displays universal performance counters and budget warnings across all domains in a debug overlay.
    /// </summary>
    // [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct UniversalPerformanceDebugSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UniversalPerformanceBudget>();
            state.RequireForUpdate<UniversalPerformanceCounters>();
            state.RequireForUpdate<DebugDisplayData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingleton<UniversalPerformanceCounters>();
            var debugDisplay = SystemAPI.GetSingletonRW<DebugDisplayData>();

            // Build universal performance data string
            var text = new Unity.Collections.FixedString512Bytes();
            text.Append("=== Universal Performance ===\n");
            
            // Perception
            text.Append("Perception: ");
            text.Append(counters.PerceptionChecksThisTick);
            text.Append("/");
            text.Append(budget.MaxPerceptionChecksPerTick);
            text.Append("\n");
            
            // Combat
            text.Append("Combat: ");
            text.Append(counters.CombatOperationsThisTick);
            text.Append("/");
            text.Append(budget.MaxCombatOperationsPerTick);
            text.Append("\n");
            text.Append("Target Selection: ");
            text.Append(counters.TargetSelectionsThisTick);
            text.Append("/");
            text.Append(budget.MaxTargetSelectionsPerTick);
            text.Append("\n");
            
            // AI Layers
            text.Append("Tactical: ");
            text.Append(counters.TacticalDecisionsThisTick);
            text.Append("/");
            text.Append(budget.MaxTacticalDecisionsPerTick);
            text.Append("\n");
            text.Append("Operational: ");
            text.Append(counters.OperationalDecisionsThisTick);
            text.Append("/");
            text.Append(budget.MaxOperationalDecisionsPerTick);
            text.Append("\n");
            text.Append("Strategic: ");
            text.Append(counters.StrategicDecisionsThisTick);
            text.Append("/");
            text.Append(budget.MaxStrategicDecisionsPerTick);
            text.Append("\n");
            
            // Jobs
            text.Append("Job Reassignments: ");
            text.Append(counters.JobReassignmentsThisTick);
            text.Append("/");
            text.Append(budget.MaxJobReassignmentsPerTick);
            text.Append("\n");
            
            // World Sim
            text.Append("Cell Updates: ");
            text.Append(counters.CellUpdatesThisTick);
            text.Append("/");
            text.Append(budget.MaxCellUpdatesPerTick);
            text.Append("\n");
            text.Append("World Sim: ");
            text.Append(counters.WorldSimOperationsThisTick);
            text.Append("/");
            text.Append(budget.MaxWorldSimOperationsPerTick);
            text.Append("\n");
            
            // Aggregated
            text.Append("Total Warm: ");
            text.Append(counters.TotalWarmOperationsThisTick);
            text.Append("\n");
            text.Append("Total Cold: ");
            text.Append(counters.TotalColdOperationsThisTick);
            text.Append("\n");
            text.Append("Operations Dropped: ");
            text.Append(counters.TotalOperationsDroppedThisTick);
            text.Append("\n");

            // Warn if budgets exceeded
            if (counters.PerceptionChecksThisTick >= budget.MaxPerceptionChecksPerTick ||
                counters.CombatOperationsThisTick >= budget.MaxCombatOperationsPerTick ||
                counters.TotalWarmOperationsThisTick >= budget.TotalOperationsWarningThreshold ||
                counters.TotalColdOperationsThisTick >= budget.TotalOperationsWarningThreshold)
            {
                text.Append("<color=yellow>WARNING: Budget Exceeded!</color>\n");
            }

            // TODO: PerformanceDebugText field needs to be added to DebugDisplayData
            // debugDisplay.ValueRW.PerformanceDebugText = text;
        }
    }
}
