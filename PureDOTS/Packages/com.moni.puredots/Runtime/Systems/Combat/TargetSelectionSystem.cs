using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// WARM path: Target selection for combat.
    /// Only for units "ready to act" (initiative above threshold).
    /// Small local neighbor set (spatial grid/cell lists).
    /// Ability/special action selection with bounded options.
    /// Cap on re-evaluation frequency.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(UniversalPerformanceBudgetSystem))]
    public partial struct TargetSelectionSystem : ISystem
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
            if (counters.ValueRO.TargetSelectionsThisTick >= budget.MaxTargetSelectionsPerTick)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Process units that are "ready to act" (initiative above threshold)
            // In full implementation, would query for Initiative component and check threshold
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
                if (counters.ValueRO.TargetSelectionsThisTick >= budget.MaxTargetSelectionsPerTick)
                {
                    break;
                }

                // In full implementation, would:
                // 1. Query spatial grid for nearby enemies (small local neighbor set)
                // 2. Evaluate targets from candidate list
                // 3. Select best target
                // 4. Evaluate ability/special action options (bounded)
                // 5. Cap re-evaluation frequency

                counters.ValueRW.TargetSelectionsThisTick++;
                counters.ValueRW.CombatOperationsThisTick++;
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
