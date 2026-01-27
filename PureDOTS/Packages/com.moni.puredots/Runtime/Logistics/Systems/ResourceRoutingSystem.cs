using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Calculates routes for shipments and caches route data.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResourceLogisticsDispatchSystem))]
    [UpdateBefore(typeof(ResourceRoutingRerouteSystem))]
    public partial struct ResourceRoutingSystem : ISystem
    {
        private ComponentLookup<Shipment> _shipmentLookup;
        private ComponentLookup<Route> _routeLookup;
        private ComponentLookup<LogisticsOrder> _orderLookup;
        private ComponentLookup<LogisticsNode> _nodeLookup;

        private int _nextRouteId;
        private const float DefaultTransportSpeedMetersPerSecond = 10f;
        private const float DefaultBaseCostPerMeter = 0.1f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<Shipment>();
            _shipmentLookup = state.GetComponentLookup<Shipment>(false);
            _routeLookup = state.GetComponentLookup<Route>(false);
            _orderLookup = state.GetComponentLookup<LogisticsOrder>(false);
            _nodeLookup = state.GetComponentLookup<LogisticsNode>(false);
            _nextRouteId = 1;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return;
            }

            _shipmentLookup.Update(ref state);
            _routeLookup.Update(ref state);
            _orderLookup.Update(ref state);
            _nodeLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Build shipment→order mapping to avoid quadratic scan
            var shipmentToOrderMap = new NativeHashMap<Entity, Entity>(16, Allocator.Temp);
            foreach (var (order, orderEntity) in SystemAPI.Query<RefRO<LogisticsOrder>>()
                .WithEntityAccess())
            {
                if (order.ValueRO.ShipmentEntity != Entity.Null)
                {
                    shipmentToOrderMap.TryAdd(order.ValueRO.ShipmentEntity, orderEntity);
                }
            }

            // Calculate routes for shipments without routes
            foreach (var (shipment, shipmentEntity) in SystemAPI.Query<RefRW<Shipment>>()
                .WithEntityAccess())
            {
                if (shipment.ValueRO.RouteEntity != Entity.Null)
                {
                    continue; // Already has route
                }

                // Find order for this shipment using map
                if (!shipmentToOrderMap.TryGetValue(shipmentEntity, out Entity orderEntity))
                {
                    continue;
                }

                if (!_orderLookup.HasComponent(orderEntity))
                {
                    continue;
                }

                var order = _orderLookup[orderEntity];

                // Get node positions
                float3 sourcePos = float3.zero;
                float3 destPos = float3.zero;

                if (_nodeLookup.HasComponent(order.SourceNode))
                {
                    sourcePos = _nodeLookup[order.SourceNode].Position;
                }

                if (_nodeLookup.HasComponent(order.DestinationNode))
                {
                    destPos = _nodeLookup[order.DestinationNode].Position;
                }

                // Calculate route
                var profile = new RouteProfile
                {
                    RiskTolerance = 0.5f,
                    CostWeight = 1f,
                    TimeWeight = 1f,
                    LegalityFlags = order.Constraints.LegalityFlags,
                    SecrecyFlags = order.Constraints.SecrecyFlags,
                    RequiredServices = order.Constraints.RequiredServices
                };

                var route = ResourceRoutingService.CalculateRoute(
                    sourcePos,
                    destPos,
                    profile,
                    tickTime.Tick,
                    _nextRouteId++,
                    DefaultTransportSpeedMetersPerSecond,
                    DefaultBaseCostPerMeter);

                // Set node references
                route.SourceNode = order.SourceNode;
                route.DestinationNode = order.DestinationNode;
                route.CacheKey.SourceNode = order.SourceNode;
                route.CacheKey.DestinationNode = order.DestinationNode;

                // Update shipment estimated arrival
                shipment.ValueRW.EstimatedArrivalTick = tickTime.Tick + (uint)math.ceil(route.EstimatedTransitTime);

                var routeEntity = ecb.CreateEntity();
                ecb.AddComponent(routeEntity, route);
                ecb.AddBuffer<RouteEdge>(routeEntity);

                shipment.ValueRW.RouteEntity = routeEntity;
            }

            shipmentToOrderMap.Dispose();

            // Update route cache (invalidate expired routes)
            uint cacheTTL = 10000; // 10 seconds in ticks
            foreach (var (route, entity) in SystemAPI.Query<RefRW<Route>>()
                .WithEntityAccess())
            {
                ResourceRoutingService.UpdateRouteCache(ref route.ValueRW, tickTime.Tick, cacheTTL);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

