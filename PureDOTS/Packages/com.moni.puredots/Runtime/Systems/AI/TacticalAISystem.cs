using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// WARM path: Tactical AI layer.
    /// Per-unit or per-group: Choose stance, retarget, use ability, reposition.
    /// Every few ticks or when context changes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(UniversalPerformanceBudgetSystem))]
    public partial struct TacticalAISystem : ISystem
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
            if (counters.ValueRO.TacticalDecisionsThisTick >= budget.MaxTacticalDecisionsPerTick)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Process entities that need tactical decisions
            foreach (var (cadence, importance, entity) in
                SystemAPI.Query<RefRO<UpdateCadence>, RefRO<AIImportance>>()
                .WithEntityAccess())
            {
                // Check update cadence
                if (!UpdateCadenceHelpers.ShouldUpdate(timeState.Tick, cadence.ValueRO))
                {
                    continue;
                }

                // Check budget
                if (counters.ValueRO.TacticalDecisionsThisTick >= budget.MaxTacticalDecisionsPerTick)
                {
                    break;
                }

                // In full implementation, would:
                // 1. Choose stance (advance, hold, retreat)
                // 2. Retarget if current target lost
                // 3. Evaluate ability usage
                // 4. Reposition if needed

                counters.ValueRW.TacticalDecisionsThisTick++;
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

