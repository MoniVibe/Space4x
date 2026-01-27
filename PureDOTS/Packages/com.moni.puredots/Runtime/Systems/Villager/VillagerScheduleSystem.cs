using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Villager;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Villager
{
    /// <summary>
    /// WARM path: Work/sleep/eat schedule evaluation and job reassignment.
    /// Only when time-of-day crosses thresholds or needs cross thresholds.
    /// Reassign jobs when work done/workplace changed/skills changed.
    /// Staggered updates per villager.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(UniversalPerformanceBudgetSystem))]
    public partial struct VillagerScheduleSystem : ISystem
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
            if (counters.ValueRO.JobReassignmentsThisTick >= budget.MaxJobReassignmentsPerTick)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process villagers that need schedule evaluation
            foreach (var (needs, cadence, importance, entity) in
                SystemAPI.Query<RefRO<VillagerNeeds>, RefRO<UpdateCadence>, RefRO<AIImportance>>()
                .WithEntityAccess())
            {
                // Check update cadence
                if (!UpdateCadenceHelpers.ShouldUpdate(timeState.Tick, cadence.ValueRO))
                {
                    continue;
                }

                // Check budget
                if (counters.ValueRO.JobReassignmentsThisTick >= budget.MaxJobReassignmentsPerTick)
                {
                    break;
                }

                // Only evaluate schedule when thresholds cross
                // In full implementation, would check time-of-day and needs thresholds
                bool needsReevaluation = false;

                // Check if needs crossed thresholds
                if (needs.ValueRO.HungerFloat < 20f || needs.ValueRO.EnergyFloat < 20f)
                {
                    needsReevaluation = true;
                }

                // Check if time-of-day crossed threshold (e.g., night time for sleep)
                // In full implementation, would check time-of-day from TimeState

                if (needsReevaluation)
                {
                    // In full implementation, would:
                    // 1. Evaluate work/sleep/eat schedule
                    // 2. Reassign job if needed
                    // 3. Update JobStateSnapshot

                    counters.ValueRW.JobReassignmentsThisTick++;
                    counters.ValueRW.ScheduleEvaluationsThisTick++;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

