using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Perception
{
    /// <summary>
    /// COLD path: Big map visibility recalculations (fog-of-war for the player).
    /// Global detection networks (spy networks, sensor constellations).
    /// Rebuilding spatial indexes.
    /// Runs rarely, event-driven.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: UniversalPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct FogOfWarSystem : ISystem
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

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Only run on long intervals or event-driven triggers
            // In full implementation, would check for fog-of-war dirty flags or events
            if (timeState.Tick % 200 != 0)
            {
                return; // Run every 200 ticks
            }

            // Check budget
            if (counters.ValueRO.PerceptionChecksThisTick >= budget.MaxPerceptionChecksPerTick)
            {
                return;
            }

            // In full implementation, would:
            // 1. Recalculate fog-of-war visibility for player
            // 2. Update global detection networks
            // 3. Rebuild spatial indexes if needed

            // For now, just increment counter
            counters.ValueRW.PerceptionChecksThisTick++;
        }
    }
}

