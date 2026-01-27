using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rendering;
using PureDOTS.Runtime.Space;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Systems.Aggregates
{
    /// <summary>
    /// System that updates fleet aggregate summaries based on member carriers/crafts.
    /// Runs at configurable intervals to minimize overhead.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct FleetAggregateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<FleetTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Update fleet aggregates
            foreach (var (fleetState, renderSummary, aggregateState, aggregateSummary, members, entity) in
                SystemAPI.Query<RefRW<FleetState>, RefRW<FleetRenderSummary>,
                                RefRW<AggregateState>, RefRW<AggregateRenderSummary>,
                                DynamicBuffer<AggregateMemberElement>>()
                    .WithAll<FleetTag>()
                    .WithEntityAccess())
            {
                // Check update interval
                if (currentTick - fleetState.ValueRO.LastUpdateTick < fleetState.ValueRO.UpdateInterval)
                {
                    continue;
                }

                // Calculate aggregate statistics
                int memberCount = 0;
                float3 totalPosition = float3.zero;
                float3 minBounds = new float3(float.MaxValue);
                float3 maxBounds = new float3(float.MinValue);
                float totalStrength = 0f;
                float totalHealth = 0f;
                float totalCargoCapacity = 0f;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (member.MemberEntity == Entity.Null)
                    {
                        continue;
                    }

                    // Get member position
                    if (SystemAPI.HasComponent<LocalTransform>(member.MemberEntity))
                    {
                        var transform = SystemAPI.GetComponent<LocalTransform>(member.MemberEntity);
                        totalPosition += transform.Position;
                        minBounds = math.min(minBounds, transform.Position);
                        maxBounds = math.max(maxBounds, transform.Position);
                    }

                    // Get member module stats if available
                    if (SystemAPI.HasComponent<CarrierModuleStatTotals>(member.MemberEntity))
                    {
                        var stats = SystemAPI.GetComponent<CarrierModuleStatTotals>(member.MemberEntity);
                        totalCargoCapacity += stats.TotalCargoCapacity;
                    }

                    totalStrength += member.StrengthContribution;
                    totalHealth += member.Health;
                    memberCount++;
                }

                if (memberCount == 0)
                {
                    continue;
                }

                float3 avgPosition = totalPosition / memberCount;
                float3 boundsCenter = (minBounds + maxBounds) / 2f;
                float boundsRadius = math.length(maxBounds - minBounds) / 2f;

                // Update fleet state
                fleetState.ValueRW.MemberCount = memberCount;
                fleetState.ValueRW.AveragePosition = avgPosition;
                fleetState.ValueRW.BoundsMin = minBounds;
                fleetState.ValueRW.BoundsMax = maxBounds;
                fleetState.ValueRW.TotalStrength = totalStrength;
                fleetState.ValueRW.TotalHealth = totalHealth;
                fleetState.ValueRW.TotalCargoCapacity = totalCargoCapacity;
                fleetState.ValueRW.LastUpdateTick = currentTick;

                // Update render summary
                renderSummary.ValueRW.MemberCount = memberCount;
                renderSummary.ValueRW.AveragePosition = avgPosition;
                renderSummary.ValueRW.BoundsCenter = boundsCenter;
                renderSummary.ValueRW.BoundsRadius = boundsRadius;
                renderSummary.ValueRW.TotalStrength = totalStrength;
                renderSummary.ValueRW.TotalHealth = totalHealth;
                renderSummary.ValueRW.LastUpdateTick = currentTick;

                // Update generic aggregate state
                aggregateState.ValueRW.MemberCount = memberCount;
                aggregateState.ValueRW.AveragePosition = avgPosition;
                aggregateState.ValueRW.BoundsMin = minBounds;
                aggregateState.ValueRW.BoundsMax = maxBounds;
                aggregateState.ValueRW.TotalHealth = totalHealth;
                aggregateState.ValueRW.TotalStrength = totalStrength;
                aggregateState.ValueRW.LastAggregationTick = currentTick;

                // Update generic render summary
                aggregateSummary.ValueRW.MemberCount = memberCount;
                aggregateSummary.ValueRW.AveragePosition = avgPosition;
                aggregateSummary.ValueRW.BoundsCenter = boundsCenter;
                aggregateSummary.ValueRW.BoundsRadius = boundsRadius;
                aggregateSummary.ValueRW.TotalHealth = totalHealth;
                aggregateSummary.ValueRW.TotalStrength = totalStrength;
                aggregateSummary.ValueRW.LastUpdateTick = currentTick;
            }
        }
    }
}

