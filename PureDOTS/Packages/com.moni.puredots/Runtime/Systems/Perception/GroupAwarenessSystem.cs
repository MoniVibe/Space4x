using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Perception
{
    /// <summary>
    /// Updates group awareness via LOS/vision/hearing checks per group or sensor anchor (WARM path).
    /// Squad leaders, watchtowers, ship sensor suites do the sensing and share awareness with group members.
    /// Staggered updates (every N ticks per group), respects budget.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(UniversalPerformanceBudgetSystem))]
    public partial struct GroupAwarenessSystem : ISystem
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
            if (counters.ValueRO.PerceptionChecksThisTick >= budget.MaxPerceptionChecksPerTick)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Process groups with sensor anchors (squad leaders, watchtowers, etc.)
            // In full implementation, would query for GroupMembership and sensor anchor entities
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
                if (counters.ValueRO.PerceptionChecksThisTick >= budget.MaxPerceptionChecksPerTick)
                {
                    break;
                }

                // Only process important groups or groups with sensor anchors
                // In full implementation, would check for sensor anchor component
                if (importance.ValueRO.Level > 2 && cadence.ValueRO.UpdateCadenceValue < 50)
                {
                    continue; // Low importance, skip unless cadence is very long
                }

                // Ensure AwarenessSnapshot exists
                if (!SystemAPI.HasComponent<AwarenessSnapshot>(entity))
                {
                    ecb.AddComponent<AwarenessSnapshot>(entity, new AwarenessSnapshot
                    {
                        Flags = AwarenessFlags.None,
                        ThreatLevel = 0f,
                        AlarmState = 0,
                        LastUpdateTick = timeState.Tick
                    });
                }

                // In full implementation, would:
                // 1. Perform LOS/vision/hearing checks from sensor anchor
                // 2. Update shared awareness buffer for group members
                // 3. Calculate threat level and alarm state
                // 4. Update KnownFact with nearest enemy/ally

                // For now, placeholder that increments counter
                counters.ValueRW.PerceptionChecksThisTick++;
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

