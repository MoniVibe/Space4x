using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Handles rerouting shipments when routes become invalid.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResourceRoutingSystem))]
    [UpdateBefore(typeof(ServiceNodeSystem))]
    public partial struct ResourceRoutingRerouteSystem : ISystem
    {
        private ComponentLookup<Shipment> _shipmentLookup;
        private ComponentLookup<Route> _routeLookup;
        private ComponentLookup<LogisticsOrder> _orderLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<Shipment>();
            _shipmentLookup = state.GetComponentLookup<Shipment>(false);
            _routeLookup = state.GetComponentLookup<Route>(false);
            _orderLookup = state.GetComponentLookup<LogisticsOrder>(false);
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

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Check for routes that need rerouting
            foreach (var (shipment, shipmentEntity) in SystemAPI.Query<RefRW<Shipment>>()
                .WithEntityAccess())
            {
                if (shipment.ValueRO.Status != ShipmentStatus.InTransit &&
                    shipment.ValueRO.Status != ShipmentStatus.Rerouting)
                {
                    continue;
                }

                if (shipment.ValueRO.RouteEntity == Entity.Null)
                {
                    continue;
                }

                var route = _routeLookup[shipment.ValueRO.RouteEntity];

                // Check if route is invalid or expired
                if (route.Status == RouteStatus.Invalid || route.Status == RouteStatus.Expired)
                {
                    // Build shipment→order mapping (reuse pattern from routing system)
                    Entity orderEntity = Entity.Null;
                    foreach (var (orderInfo, oEntity) in SystemAPI.Query<RefRO<LogisticsOrder>>()
                        .WithEntityAccess())
                    {
                        if (orderInfo.ValueRO.ShipmentEntity == shipmentEntity)
                        {
                            orderEntity = oEntity;
                            break;
                        }
                    }

                    if (orderEntity == Entity.Null || !_orderLookup.HasComponent(orderEntity))
                    {
                        continue;
                    }

                    var orderData = _orderLookup[orderEntity];

                    // Reroute
                    var profile = new RouteProfile
                    {
                        RiskTolerance = 0.5f,
                        CostWeight = 1f,
                        TimeWeight = 1f,
                        LegalityFlags = orderData.Constraints.LegalityFlags,
                        SecrecyFlags = orderData.Constraints.SecrecyFlags,
                        RequiredServices = orderData.Constraints.RequiredServices
                    };

                    var rerouted = ResourceRoutingService.RerouteShipment(
                        route,
                        RouteRerouteReason.RouteBlocked,
                        profile,
                        tickTime.Tick);

                    // Create new route entity
                    var newRouteEntity = ecb.CreateEntity();
                    ecb.AddComponent(newRouteEntity, rerouted);
                    ecb.AddBuffer<RouteEdge>(newRouteEntity);

                    shipment.ValueRW.RouteEntity = newRouteEntity;
                    shipment.ValueRW.Status = ShipmentStatus.Rerouting;
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

