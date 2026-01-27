using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rendering;
using PureDOTS.Runtime.Villagers;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Systems.Aggregates
{
    /// <summary>
    /// System that updates collective aggregate summaries (villages, etc.) based on member entities.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct CollectiveAggregateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CollectiveAggregate>();
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

            foreach (var (collective, members, entity) in SystemAPI
                         .Query<RefRW<CollectiveAggregate>, DynamicBuffer<CollectiveAggregateMember>>()
                         .WithEntityAccess())
            {
                var collectiveValue = collective.ValueRO;

                // Optional: some aggregates also carry VillageState/VillageRenderSummary for presentation.
                var hasVillageState = SystemAPI.HasComponent<VillageState>(entity);
                var hasVillageRenderSummary = SystemAPI.HasComponent<VillageRenderSummary>(entity);

                // Early-out if we updated recently (use VillageState interval if available, otherwise default).
                uint updateInterval = 60;
                uint lastUpdate = collectiveValue.LastStateChangeTick;

                if (hasVillageState)
                {
                    var villageState = SystemAPI.GetComponent<VillageState>(entity);
                    updateInterval = math.max(1u, villageState.UpdateInterval);
                    lastUpdate = villageState.LastUpdateTick;
                }

                if (currentTick - lastUpdate < updateInterval)
                {
                    continue;
                }

                int memberCount = 0;
                float3 totalPosition = float3.zero;
                float3 minBounds = new float3(float.MaxValue);
                float3 maxBounds = new float3(float.MinValue);
                float totalWealth = 0f;
                float totalMorale = 0f;
                float totalFaith = 0f;
                float totalHealth = 0f;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    var memberEntity = member.MemberEntity;
                    if (memberEntity == Entity.Null)
                    {
                        continue;
                    }

                    if (SystemAPI.HasComponent<LocalTransform>(memberEntity))
                    {
                        var transform = SystemAPI.GetComponent<LocalTransform>(memberEntity);
                        totalPosition += transform.Position;
                        minBounds = math.min(minBounds, transform.Position);
                        maxBounds = math.max(maxBounds, transform.Position);
                    }

                    if (SystemAPI.HasComponent<VillagerNeeds>(memberEntity))
                    {
                        var needs = SystemAPI.GetComponent<VillagerNeeds>(memberEntity);
                        totalHealth += needs.Health;
                        totalMorale += needs.Morale;
                    }

                    if (SystemAPI.HasComponent<VillagerBeliefOptimized>(memberEntity))
                    {
                        var belief = SystemAPI.GetComponent<VillagerBeliefOptimized>(memberEntity);
                        totalFaith += belief.FaithNormalized;
                    }

                    totalWealth += (member.Flags & CollectiveAggregateMemberFlags.IsWorker) != 0 ? 1f : 0f;
                    memberCount++;
                }

                if (memberCount == 0)
                {
                    continue;
                }

                float3 avgPosition = totalPosition / memberCount;
                float3 boundsCenter = (minBounds + maxBounds) * 0.5f;
                float boundsRadius = math.length(maxBounds - minBounds) * 0.5f;

                // Update optional VillageState/VillageRenderSummary if present
                if (hasVillageState)
                {
                    var villageState = SystemAPI.GetComponent<VillageState>(entity);
                    villageState.PopulationCount = memberCount;
                    villageState.CenterPosition = avgPosition;
                    villageState.BoundsMin = minBounds;
                    villageState.BoundsMax = maxBounds;
                    villageState.TotalWealth = totalWealth;
                    villageState.AverageMorale = totalMorale / memberCount;
                    villageState.AverageFaith = totalFaith / memberCount;
                    villageState.LastUpdateTick = currentTick;
                    SystemAPI.SetComponent(entity, villageState);
                }

                if (hasVillageRenderSummary)
                {
                    var renderSummary = SystemAPI.GetComponent<VillageRenderSummary>(entity);
                    renderSummary.PopulationCount = memberCount;
                    renderSummary.CenterPosition = avgPosition;
                    renderSummary.BoundsCenter = boundsCenter;
                    renderSummary.BoundsRadius = boundsRadius;
                    renderSummary.TotalWealth = totalWealth;
                    renderSummary.AverageMorale = totalMorale / memberCount;
                    renderSummary.AverageFaith = totalFaith / memberCount;
                    renderSummary.LastUpdateTick = currentTick;
                    SystemAPI.SetComponent(entity, renderSummary);
                }

                    // Update generic aggregate state and summary if available
                if (SystemAPI.HasComponent<AggregateState>(entity))
                {
                    var aggregateState = SystemAPI.GetComponent<AggregateState>(entity);
                    aggregateState.MemberCount = memberCount;
                    aggregateState.AveragePosition = avgPosition;
                    aggregateState.BoundsMin = minBounds;
                    aggregateState.BoundsMax = maxBounds;
                    aggregateState.TotalHealth = totalHealth;
                    aggregateState.AverageMorale = totalMorale / memberCount;
                    aggregateState.LastAggregationTick = currentTick;
                    SystemAPI.SetComponent(entity, aggregateState);
                }

                if (SystemAPI.HasComponent<AggregateRenderSummary>(entity))
                {
                    var aggregateSummary = SystemAPI.GetComponent<AggregateRenderSummary>(entity);
                    aggregateSummary.MemberCount = memberCount;
                    aggregateSummary.AveragePosition = avgPosition;
                    aggregateSummary.BoundsCenter = boundsCenter;
                    aggregateSummary.BoundsRadius = boundsRadius;
                    aggregateSummary.TotalHealth = totalHealth;
                    aggregateSummary.AverageMorale = totalMorale / memberCount;
                    aggregateSummary.LastUpdateTick = currentTick;
                    SystemAPI.SetComponent(entity, aggregateSummary);
                }

                collective.ValueRW.MemberCount = memberCount;
                collective.ValueRW.LastStateChangeTick = currentTick;
            }
        }
    }
}
