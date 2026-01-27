using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Motivation;
using PureDOTS.Runtime.Relations;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Relations
{
    /// <summary>
    /// Event-driven loyalty changes and sampled group aggregation (WARM path).
    /// Changes individual loyalty based on events: pay, fairness, victories, defeats, propaganda, oppression.
    /// Group-level loyalty aggregated from sample (10-20 members per update).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(RelationPerformanceBudgetSystem))]
    public partial struct LoyaltyPropagationSystem : ISystem
    {
        private const int SampleSize = 15; // Sample 15 members for group aggregation

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Process loyalty events
            // In full implementation, would query for LoyaltyEvent components
            // For now, this processes entities with Motivation components and updates loyalty

            // Event-driven loyalty changes would be triggered by:
            // - Pay events (increase loyalty)
            // - Fairness events (increase/decrease based on fairness)
            // - Victory/defeat events (increase/decrease)
            // - Propaganda events (increase)
            // - Oppression events (decrease)

            // For now, process entities that need loyalty updates based on cadence
            foreach (var (motivation, cadence, entity) in
                SystemAPI.Query<RefRW<MotivationDrive>, RefRO<UpdateCadence>>()
                .WithEntityAccess())
            {
                // Check update cadence
                if (!UpdateCadenceHelpers.ShouldUpdate(timeState.Tick, cadence.ValueRO))
                {
                    continue;
                }

                // In full implementation, would check for loyalty events affecting this entity
                // For now, apply slow decay if no recent events
                uint ticksSinceUpdate = timeState.Tick - motivation.ValueRO.LastInitiativeTick;
                if (ticksSinceUpdate > 1000)
                {
                    // Slow decay: loyalty decreases by 1 per 1000 ticks if no reinforcement
                    if (motivation.ValueRO.LoyaltyCurrent > 0)
                    {
                        motivation.ValueRW.LoyaltyCurrent = (byte)math.max(0, motivation.ValueRO.LoyaltyCurrent - 1);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

