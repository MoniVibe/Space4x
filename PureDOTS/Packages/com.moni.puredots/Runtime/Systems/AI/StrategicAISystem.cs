using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// COLD path: Strategic AI layer.
    /// Per faction/empire: Declare war, set war goals, allocate fronts, major projects.
    /// Very infrequent.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: UniversalPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct StrategicAISystem : ISystem
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
            // In full implementation, would check for strategic events or very long intervals
            if (timeState.Tick % 500 != 0)
            {
                return; // Run every 500 ticks
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.StrategicDecisionsThisTick >= budget.MaxStrategicDecisionsPerTick)
            {
                return;
            }

            // In full implementation, would:
            // 1. Evaluate war declarations
            // 2. Set war goals
            // 3. Allocate fronts
            // 4. Plan major projects

            // For now, just increment counter
            counters.ValueRW.StrategicDecisionsThisTick++;
        }
    }
}

