using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Transport;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Economy
{
    /// <summary>
    /// Converts surfaced trade opportunities into logistics requests routed through the transport pipeline.
    /// COLD path: Runs rarely, uses budgets & batching, only for important goods/hubs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    // Removed invalid UpdateAfter: TradeOpportunitySystem runs in SimulationSystemGroup.
    public partial struct TradeRoutingSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TradeOpportunityState>();
            state.RequireForUpdate<ResourceTypeIndex>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<RelationPerformanceBudget>();
            state.RequireForUpdate<RelationPerformanceCounters>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);

            var tradeStateEntity = SystemAPI.GetSingletonEntity<TradeOpportunityState>();
            if (!SystemAPI.HasComponent<TradeRoutingState>(tradeStateEntity))
            {
                var initEcb = new EntityCommandBuffer(state.WorldUpdateAllocator);
                initEcb.AddComponent(tradeStateEntity, new TradeRoutingState { LastProcessedVersion = 0 });
                initEcb.Playback(state.EntityManager);
                initEcb.Dispose();
            }

            var routingState = SystemAPI.GetComponentRW<TradeRoutingState>(tradeStateEntity);
            var tradeState = SystemAPI.GetComponent<TradeOpportunityState>(tradeStateEntity);

            if (tradeState.Version == routingState.ValueRO.LastProcessedVersion)
            {
                return;
            }

            routingState.ValueRW.LastProcessedVersion = tradeState.Version;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (tag, entity) in SystemAPI.Query<TradeRouteRequestTag>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            var catalog = SystemAPI.GetSingleton<ResourceTypeIndex>().Catalog;
            if (!catalog.IsCreated)
            {
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                return;
            }

            if (!SystemAPI.HasBuffer<TradeOpportunity>(tradeStateEntity))
            {
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                return;
            }

            var budget = SystemAPI.GetSingleton<RelationPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<RelationPerformanceCounters>();

            // Process only important goods/hubs, respect budget
            var opportunities = state.EntityManager.GetBuffer<TradeOpportunity>(tradeStateEntity);
            int routesCreated = 0;
            const int maxRoutesPerTick = 5; // Budget for trade route creation

            for (int i = 0; i < opportunities.Length && routesCreated < maxRoutesPerTick; i++)
            {
                var opp = opportunities[i];
                
                // Only process important goods or major hubs
                // In full implementation, would check resource importance and hub size
                if (opp.AvailableUnits < 10f) // Skip small opportunities
                {
                    continue;
                }

                var resourceIndex = catalog.Value.LookupIndex(opp.ResourceId);
                if (resourceIndex < 0)
                {
                    continue;
                }

                var sourcePos = ResolvePosition(opp.Source, float3.zero);
                var destPos = ResolvePosition(opp.Destination, float3.zero);

                var requestEntity = ecb.CreateEntity();
                ecb.AddComponent(requestEntity, new TradeRouteRequestTag { Version = tradeState.Version });
                ecb.AddComponent(requestEntity, new LogisticsRequest
                {
                    SourceEntity = opp.Source,
                    DestinationEntity = opp.Destination,
                    SourcePosition = sourcePos,
                    DestinationPosition = destPos,
                    ResourceTypeIndex = (ushort)resourceIndex,
                    RequestedUnits = math.max(0.01f, opp.AvailableUnits),
                    FulfilledUnits = 0f,
                    Priority = LogisticsRequestPriority.Normal,
                    Flags = LogisticsRequestFlags.None,
                    CreatedTick = timeState.Tick,
                    LastUpdateTick = timeState.Tick
                });
                ecb.AddComponent(requestEntity, new LogisticsRequestProgress
                {
                    AssignedUnits = 0f,
                    AssignedTransportCount = 0,
                    LastAssignmentTick = 0
                });

                routesCreated++;
            }

            // Update counters
            counters.ValueRW.MarketUpdatesThisTick += routesCreated;

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private float3 ResolvePosition(Entity entity, float3 fallback)
        {
            if (entity == Entity.Null || !_transformLookup.HasComponent(entity))
            {
                return fallback;
            }

            return _transformLookup[entity].Position;
        }
    }
}
