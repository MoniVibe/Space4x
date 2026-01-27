using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Narrative
{
    /// <summary>
    /// COLD path: Trigger discovery.
    /// "Is it time to spawn a new cult cell?", "Has an area become unstable enough for a riot?"
    /// Use precomputed "instability scores" per region/Org to reduce checks.
    /// Make triggers subscribed or region-based, not "along the entire entity list".
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: UniversalPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct TriggerDiscoverySystem : ISystem
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
            // In full implementation, would check for trigger discovery events or long intervals
            if (timeState.Tick % 200 != 0)
            {
                return; // Run every 200 ticks
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.TriggerEvaluationsThisTick >= budget.MaxTriggerEvaluationsPerTick)
            {
                return;
            }

            // In full implementation, would:
            // 1. Check precomputed instability scores per region/Org
            // 2. Evaluate trigger conditions (cult cell spawn, riot conditions, etc.)
            // 3. Use region-based or subscribed triggers, not entity list scan
            // 4. Create trigger events for warm path systems to process

            // For now, just increment counter
            counters.ValueRW.TriggerEvaluationsThisTick++;
        }
    }
}

