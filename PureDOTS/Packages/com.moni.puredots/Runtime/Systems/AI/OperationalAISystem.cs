using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// COLD-ish path: Operational AI layer.
    /// Per band/army/fleet: Where to patrol, which town to besiege, which front to reinforce.
    /// Every tens/hundreds of ticks, plus event-driven.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: UniversalPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct OperationalAISystem : ISystem
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
            // In full implementation, would check for operational events or long intervals
            if (timeState.Tick % 100 != 0)
            {
                return; // Run every 100 ticks
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.OperationalDecisionsThisTick >= budget.MaxOperationalDecisionsPerTick)
            {
                return;
            }

            // In full implementation, would:
            // 1. Evaluate patrol routes for bands/armies/fleets
            // 2. Decide which towns to besiege
            // 3. Allocate fronts to reinforce
            // 4. Coordinate group movements

            // For now, just increment counter
            counters.ValueRW.OperationalDecisionsThisTick++;
        }
    }
}

