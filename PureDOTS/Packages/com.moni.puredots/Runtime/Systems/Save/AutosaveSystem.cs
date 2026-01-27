using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Save
{
    /// <summary>
    /// WARM/COLD path: Autosave system.
    /// Full save offloaded to separate threads/job system if possible.
    /// Snapshot only authoritative data.
    /// Periodic backups infrequent, milestone-triggered.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: UniversalPerformanceBudgetSystem runs in WarmPathSystemGroup.
    public partial struct AutosaveSystem : ISystem
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

            // Only run on very long intervals or milestone triggers
            // In full implementation, would check for save triggers or milestones
            if (timeState.Tick % 10000 != 0)
            {
                return; // Run every 10000 ticks (very infrequent)
            }

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();

            // Check budget
            if (counters.ValueRO.SaveOperationsThisTick >= budget.MaxSaveOperationsPerTick)
            {
                return;
            }

            // In full implementation, would:
            // 1. Snapshot only authoritative data (respecting rewind/history decisions)
            // 2. Offload full save to separate thread/job system
            // 3. Create periodic backups on milestones
            // 4. Mark what data is authoritative vs recomputable

            // For now, just increment counter
            counters.ValueRW.SaveOperationsThisTick++;
        }
    }
}

