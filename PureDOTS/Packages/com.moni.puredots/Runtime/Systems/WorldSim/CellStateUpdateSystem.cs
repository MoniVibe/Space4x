using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.WorldSim
{
    /// <summary>
    /// WARM path: Update cell-level state (fire spread, disease spread, pollution, flood levels).
    /// Power grid coverage and outages.
    /// Work in chunks, update only "active" cells.
    /// Use "next update tick" per chunk.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(UniversalPerformanceBudgetSystem))]
    public partial struct CellStateUpdateSystem : ISystem
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

            // Check budget
            if (counters.ValueRO.CellUpdatesThisTick >= budget.MaxCellUpdatesPerTick)
            {
                return;
            }

            // Process cells in chunks, only "active" cells
            // In full implementation, would:
            // 1. Query cells with non-zero fire/disease/pollution/flood values
            // 2. Update cell state based on spread rules
            // 3. Use "next update tick" per chunk for staggering
            // 4. Update WorldStateSnapshot for entities in affected cells

            int processedCount = 0;
            foreach (var (cadence, entity) in
                SystemAPI.Query<RefRO<UpdateCadence>>()
                .WithEntityAccess())
            {
                // Check update cadence
                if (!UpdateCadenceHelpers.ShouldUpdate(timeState.Tick, cadence.ValueRO))
                {
                    continue;
                }

                // Check budget
                if (processedCount >= budget.MaxCellUpdatesPerTick)
                {
                    break;
                }

                // In full implementation, would update cell state
                processedCount++;
            }

            counters.ValueRW.CellUpdatesThisTick += processedCount;
            counters.ValueRW.WorldSimOperationsThisTick += processedCount;
        }
    }
}

