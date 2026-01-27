using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Villager
{
    /// <summary>
    /// COLD path: Large-scale job market balancing.
    /// Guild apprentice allocation, workforce reallocation across village/colony.
    /// Policy-driven reorganization.
    /// Runs rarely, event-driven.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: UniversalPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct JobMarketBalancingSystem : ISystem
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

            // Only run on long intervals or event-driven triggers
            // In full implementation, would check for job market events or policy changes
            if (timeState.Tick % 500 != 0)
            {
                return; // Run every 500 ticks
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.JobReassignmentsThisTick >= budget.MaxJobReassignmentsPerTick)
            {
                return;
            }

            // In full implementation, would:
            // 1. Analyze job market across village/colony
            // 2. Allocate guild apprentices based on demand
            // 3. Reallocate workforce based on policy
            // 4. Balance job assignments across regions

            // For now, just increment counter
            counters.ValueRW.JobReassignmentsThisTick++;
        }
    }
}

