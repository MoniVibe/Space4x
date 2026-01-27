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
    /// Aggregates lower-level loyalty data into LoyaltyState snapshots (WARM path).
    /// Samples N members instead of iterating all for large groups.
    /// Runs every 20-100 ticks, staggered per group.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(RelationPerformanceBudgetSystem))]
    public partial struct LoyaltyAggregationSystem : ISystem
    {
        private const int SampleSize = 20; // Sample 20 members for large groups

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

            // Process entities that need loyalty aggregation
            // For now, we'll aggregate from MotivationComponents.LoyaltyCurrent
            // In a full implementation, this would query band/faction/empire membership
            foreach (var (motivation, cadence, importance, entity) in
                SystemAPI.Query<RefRO<MotivationDrive>, RefRO<UpdateCadence>, RefRO<AIImportance>>()
                .WithEntityAccess())
            {
                // Check update cadence
                if (!UpdateCadenceHelpers.ShouldUpdate(timeState.Tick, cadence.ValueRO))
                {
                    continue;
                }

                // Only update if importance level warrants it or if significant events occurred
                // High importance (0-1) updates more frequently
                if (importance.ValueRO.Level > 2 && cadence.ValueRO.UpdateCadenceValue < 50)
                {
                    continue; // Low importance, skip unless cadence is very long
                }

                // Ensure LoyaltyState exists
                if (!SystemAPI.HasComponent<LoyaltyState>(entity))
                {
                    ecb.AddComponent<LoyaltyState>(entity);
                }

                var loyaltyState = SystemAPI.GetComponentRW<LoyaltyState>(entity);

                // Aggregate loyalty from Motivation component
                // In full implementation, would sample from band/faction members
                float loyaltyValue = motivation.ValueRO.LoyaltyCurrent / (float)math.max(motivation.ValueRO.LoyaltyMax, 1);
                loyaltyValue = math.clamp(loyaltyValue, 0f, 1f);

                // For now, set all loyalty values to the same (would be differentiated by group membership)
                loyaltyState.ValueRW.ToBand = loyaltyValue;
                loyaltyState.ValueRW.ToFaction = loyaltyValue;
                loyaltyState.ValueRW.ToEmpire = loyaltyValue;

                // Calculate betrayal risk (inverse of loyalty, with some variance)
                loyaltyState.ValueRW.BetrayalRisk = math.clamp(1f - loyaltyValue, 0f, 1f);

                loyaltyState.ValueRW.LastUpdateTick = timeState.Tick;
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

