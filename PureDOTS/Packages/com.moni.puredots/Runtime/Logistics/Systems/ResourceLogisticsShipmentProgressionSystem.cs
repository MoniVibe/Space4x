using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Handles shipment status progression: Created → Loading → InTransit → Unloading.
    /// 
    /// <para>
    /// This system operates at the logistics planning level, managing orders, routes, and shipments abstractly.
    /// Shipment status progression uses estimated transit times based on route calculations, not actual hauler
    /// position or movement. This abstraction allows the Resource Slice to function independently of specific
    /// transport implementations.
    /// </para>
    /// 
    /// <para>
    /// Actual transport movement is handled by game-specific systems (e.g., HaulingLoopSystem for Godgame,
    /// TransportMovementSystem for Space4X). These systems manage the physical movement of haulers along routes
    /// and are expected to set <see cref="HaulingLoopState.Phase"/> to <see cref="HaulingLoopPhase.Unloading"/>
    /// once a transport reaches its destination. When that signal is missing the Resource Slice falls back to
    /// distance checks via <see cref="LocalTransform"/> and, ultimately, the estimated arrival tick. Missing
    /// integration triggers periodic warnings so wiring issues surface early during gameplay.
    /// </para>
    /// 
    /// <para>
    /// The Resource Slice focuses on logistics planning: creating orders, calculating routes, managing reservations,
    /// and tracking shipments through their lifecycle. Shipment status transitions are based on estimated times
    /// from route calculations, enabling the system to work without requiring specific transport implementations.
    /// </para>
    /// 
    /// <para>
    /// Future enhancement: This system could optionally integrate with hauler position updates if game-specific
    /// systems provide position data, allowing more accurate arrival detection.
    /// </para>
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResourceRoutingSystem))]
    [UpdateBefore(typeof(ResourceLogisticsDeliverySystem))]
    public partial struct ResourceLogisticsShipmentProgressionSystem : ISystem
    {
        private ComponentLookup<Shipment> _shipmentLookup;
        private ComponentLookup<Route> _routeLookup;
        private ComponentLookup<LogisticsOrder> _orderLookup;
        private ComponentLookup<LogisticsNode> _nodeLookup;
        private BufferLookup<StorehouseInventoryItem> _storehouseInventoryLookup;
        private ComponentLookup<StorehouseInventory> _storehouseInventoryComponentLookup;
        private ComponentLookup<InventoryReservation> _inventoryReservationLookup;
        private ComponentLookup<CapacityReservation> _capacityReservationLookup;
        private BufferLookup<ShipmentCargoAllocation> _shipmentCargoAllocationLookup;
        private ComponentLookup<HaulingLoopState> _haulingLoopStateLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ServiceReservation> _serviceReservationLookup;

        private int _nextServiceReservationId;
        private const float ServiceReservationTTLSeconds = 16.6667f;
        private const float ArrivalDistanceThreshold = 5f; // Meters - hauler considered arrived if within this distance
        private const byte DefaultServiceCapacity = 1; // Default service slot capacity if not specified
        private const float HaulerIntegrationWarningIntervalSeconds = 5f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<Shipment>();
            state.RequireForUpdate<ResourceTypeIndex>();
            _shipmentLookup = state.GetComponentLookup<Shipment>(false);
            _routeLookup = state.GetComponentLookup<Route>(false);
            _orderLookup = state.GetComponentLookup<LogisticsOrder>(false);
            _nodeLookup = state.GetComponentLookup<LogisticsNode>(false);
            _storehouseInventoryLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _storehouseInventoryComponentLookup = state.GetComponentLookup<StorehouseInventory>(false);
            _inventoryReservationLookup = state.GetComponentLookup<InventoryReservation>(false);
            _capacityReservationLookup = state.GetComponentLookup<CapacityReservation>(false);
            _shipmentCargoAllocationLookup = state.GetBufferLookup<ShipmentCargoAllocation>(false);
            _haulingLoopStateLookup = state.GetComponentLookup<HaulingLoopState>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _serviceReservationLookup = state.GetComponentLookup<ServiceReservation>(true);
            _nextServiceReservationId = 1;
        }

        [BurstCompile]
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

            if (!SystemAPI.TryGetSingleton<ResourceTypeIndex>(out var resourceTypeIndex) ||
                !resourceTypeIndex.Catalog.IsCreated)
            {
                return;
            }

            var fixedDeltaTime = math.max(1e-6f, tickTime.FixedDeltaTime);
            var serviceReservationTtlTicks = (uint)math.max(1f, math.ceil(ServiceReservationTTLSeconds / fixedDeltaTime));
            var warningIntervalTicks = (uint)math.max(1f, math.ceil(HaulerIntegrationWarningIntervalSeconds / fixedDeltaTime));

            _shipmentLookup.Update(ref state);
            _routeLookup.Update(ref state);
            _orderLookup.Update(ref state);
            _nodeLookup.Update(ref state);
            _storehouseInventoryLookup.Update(ref state);
            _storehouseInventoryComponentLookup.Update(ref state);
            _inventoryReservationLookup.Update(ref state);
            _capacityReservationLookup.Update(ref state);
            _shipmentCargoAllocationLookup.Update(ref state);
            _haulingLoopStateLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _serviceReservationLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Build shipment→order mapping to find orders for service reservations and inventory withdrawal
            var shipmentToOrderMap = new NativeHashMap<Entity, Entity>(16, Allocator.Temp);
            foreach (var (order, orderEntity) in SystemAPI.Query<RefRO<LogisticsOrder>>()
                .WithEntityAccess())
            {
                if (order.ValueRO.ShipmentEntity != Entity.Null)
                {
                    shipmentToOrderMap.TryAdd(order.ValueRO.ShipmentEntity, orderEntity);
                }
            }

            // Build order→inventoryReservation mapping for quick lookup
            var orderToInventoryReservationMap = new NativeHashMap<Entity, Entity>(16, Allocator.Temp);
            foreach (var (reservation, resEntity) in SystemAPI.Query<RefRO<InventoryReservation>>()
                .WithEntityAccess())
            {
                if (reservation.ValueRO.Status == ReservationStatus.Active)
                {
                    orderToInventoryReservationMap.TryAdd(reservation.ValueRO.OrderEntity, resEntity);
                }
            }

            // Build order→capacityReservation mapping (one capacity reservation per order)
            var orderToCapacityReservationMap = new NativeHashMap<Entity, Entity>(16, Allocator.Temp);
            foreach (var (capacityReservation, capEntity) in SystemAPI.Query<RefRO<CapacityReservation>>()
                         .WithEntityAccess())
            {
                if (capacityReservation.ValueRO.Status == ReservationStatus.Active)
                {
                    orderToCapacityReservationMap.TryAdd(capacityReservation.ValueRO.OrderEntity, capEntity);
                }
            }

            var entityManager = state.EntityManager;

            // Build service reservation counts per node/service type for gating
            var loadServiceCounts = new NativeParallelHashMap<Entity, int>(64, Allocator.Temp);
            var unloadServiceCounts = new NativeParallelHashMap<Entity, int>(64, Allocator.Temp);
            var orderToServiceReservationMap = new NativeParallelMultiHashMap<Entity, Entity>(128, Allocator.Temp);

            foreach (var (serviceReservation, serviceEntity) in SystemAPI.Query<RefRO<ServiceReservation>>()
                         .WithEntityAccess())
            {
                if (serviceReservation.ValueRO.Status != ReservationStatus.Active)
                {
                    continue;
                }

                orderToServiceReservationMap.Add(serviceReservation.ValueRO.OrderEntity, serviceEntity);

                switch (serviceReservation.ValueRO.ServiceType)
                {
                    case ServiceType.Load:
                        IncrementServiceCount(ref loadServiceCounts, serviceReservation.ValueRO.ServiceNode);
                        break;
                    case ServiceType.Unload:
                        IncrementServiceCount(ref unloadServiceCounts, serviceReservation.ValueRO.ServiceNode);
                        break;
                }
            }

            // Process shipments and update status
            foreach (var (shipment, shipmentEntity) in SystemAPI.Query<RefRW<Shipment>>()
                .WithEntityAccess())
            {
                var currentStatus = shipment.ValueRO.Status;

                // Created → Loading: Shipment has route and service available
                if (currentStatus == ShipmentStatus.Created)
                {
                    if (shipment.ValueRO.RouteEntity != Entity.Null)
                    {
                        // Check if Load service is available at source node
                        bool serviceAvailable = true;
                        if (shipmentToOrderMap.TryGetValue(shipmentEntity, out Entity loadOrderEntity) &&
                            _orderLookup.HasComponent(loadOrderEntity))
                        {
                            var order = _orderLookup[loadOrderEntity];
                            Entity sourceNode = order.SourceNode;

                            if (_nodeLookup.HasComponent(sourceNode))
                            {
                                // Count active Load service reservations at source node
                                int activeLoadReservations = GetServiceCount(loadServiceCounts, sourceNode);
                                // Check capacity
                                var nodeServices = _nodeLookup[sourceNode].Services;
                                byte capacity = nodeServices.SlotCapacity > 0 ? nodeServices.SlotCapacity : DefaultServiceCapacity;

                                if (activeLoadReservations >= capacity)
                                {
                                    serviceAvailable = false; // Service capacity full, wait
                                }
                                else
                                {
                                    // Create Load service reservation
                                    var serviceReservation = ResourceReservationService.ReserveService(
                                        sourceNode,
                                        ServiceType.Load,
                                        loadOrderEntity,
                                        tickTime.Tick,
                                        tickTime.Tick,
                                        serviceReservationTtlTicks,
                                        _nextServiceReservationId++);

                                    var serviceResEntity = ecb.CreateEntity();
                                    ecb.AddComponent(serviceResEntity, serviceReservation);
                                    IncrementServiceCount(ref loadServiceCounts, sourceNode);
                                    orderToServiceReservationMap.Add(loadOrderEntity, serviceResEntity);
                                }
                            }
                        }

                        if (serviceAvailable)
                        {
                            shipment.ValueRW.Status = ShipmentStatus.Loading;
                        }
                    }
                    continue;
                }

                // Loading → InTransit: After loading time and inventory withdrawal
                // Note: This uses abstract time-based progression. Actual loading/unloading work is handled
                // by game-specific systems (HaulingLoopSystem, etc.). Resource Slice manages the logistics
                // planning layer, not the physical transport operations.
                if (currentStatus == ShipmentStatus.Loading)
                {
                    bool hasCargo = false;

                    // Withdraw inventory from source storehouse (if not already withdrawn)
                    if (shipmentToOrderMap.TryGetValue(shipmentEntity, out Entity withdrawOrderEntity) &&
                        _orderLookup.HasComponent(withdrawOrderEntity))
                    {
                        var order = _orderLookup[withdrawOrderEntity];
                        Entity sourceNode = order.SourceNode;

                        // Find inventory reservation for this order
                        if (orderToInventoryReservationMap.TryGetValue(withdrawOrderEntity, out Entity invResEntity) &&
                            _inventoryReservationLookup.HasComponent(invResEntity))
                        {
                            var reservation = _inventoryReservationLookup[invResEntity];
                            
                            // Only withdraw if reservation is still active (not already withdrawn)
                            if (reservation.Status == ReservationStatus.Active &&
                                _storehouseInventoryLookup.HasBuffer(sourceNode) &&
                                _storehouseInventoryComponentLookup.HasComponent(sourceNode))
                            {
                                var sourceInventory = _storehouseInventoryLookup[sourceNode];
                                var inventoryComponent = _storehouseInventoryComponentLookup[sourceNode];

                                var resolvedResourceIndex = ResolveResourceTypeIndex(order, resourceTypeIndex.Catalog);
                                if (resolvedResourceIndex == ushort.MaxValue)
                                {
                                    FailOrderAndShipment(
                                        withdrawOrderEntity,
                                        order.SourceNode,
                                        order.DestinationNode,
                                        ShipmentFailureReason.InvalidSource,
                                        ref _orderLookup,
                                        shipment,
                                        orderToInventoryReservationMap,
                                        orderToCapacityReservationMap,
                                        ref _inventoryReservationLookup,
                                        ref _capacityReservationLookup,
                                        ref orderToServiceReservationMap,
                                        ref _serviceReservationLookup,
                                        ref loadServiceCounts,
                                        ref unloadServiceCounts);
                                    continue;
                                }

                                if (StorehouseMutationService.CommitWithdrawReservedOut(
                                        resolvedResourceIndex,
                                        reservation.ReservedAmount,
                                        resourceTypeIndex.Catalog,
                                        ref inventoryComponent,
                                        sourceInventory,
                                        out float withdrawnAmount))
                                {
                                    hasCargo = withdrawnAmount > 0f;

                                    // Write back modified inventory component
                                    _storehouseInventoryComponentLookup[sourceNode] = inventoryComponent;

                                    // Update ShipmentCargoAllocation with actual withdrawn amount
                                    if (_shipmentCargoAllocationLookup.HasBuffer(shipmentEntity))
                                    {
                                        var cargoAllocations = _shipmentCargoAllocationLookup[shipmentEntity];
                                        for (int i = 0; i < cargoAllocations.Length; i++)
                                        {
                                            if (cargoAllocations[i].ResourceTypeIndex == resolvedResourceIndex)
                                            {
                                                var allocation = cargoAllocations[i];
                                                allocation.AllocatedAmount = withdrawnAmount; // Update with actual withdrawn
                                                allocation.ResourceTypeIndex = resolvedResourceIndex;
                                                cargoAllocations[i] = allocation;
                                                break;
                                            }
                                        }
                                    }

                                    // Release inventory reservation
                                    reservation.Status = ReservationStatus.Committed;
                                    reservation.ReservedAmount = withdrawnAmount;
                                    reservation.ReservationFlags |= InventoryReservationFlags.Withdrawn;
                                    _inventoryReservationLookup[invResEntity] = reservation;
                                    orderToInventoryReservationMap.Remove(withdrawOrderEntity);
                                    orderToInventoryReservationMap.TryAdd(withdrawOrderEntity, invResEntity);
                                }
                            }
                        }
                    }

                    // If no cargo was withdrawn, remain in Loading until inventory becomes available
                    bool isAbstractShipment = shipment.ValueRO.RepresentationMode == ShipmentRepresentationMode.Abstract;
                    if (!hasCargo && !isAbstractShipment)
                    {
                        continue;
                    }

                    // Require a transport for physical shipments
                    if (!isAbstractShipment && shipment.ValueRO.AssignedTransport == Entity.Null)
                    {
                        continue;
                    }

                    // Simplified: immediately transition if abstract mode, or after loading time
                    if (shipment.ValueRO.RepresentationMode == ShipmentRepresentationMode.Abstract)
                    {
                        shipment.ValueRW.Status = ShipmentStatus.InTransit;
                        shipment.ValueRW.DepartureTick = tickTime.Tick;
                    }
                    else
                    {
                        // For physical mode, transition immediately based on abstract time
                        // Actual loading work (moving items from source to transport) is handled by
                        // game-specific systems. Resource Slice tracks the logistics state.
                        shipment.ValueRW.Status = ShipmentStatus.InTransit;
                        shipment.ValueRW.DepartureTick = tickTime.Tick;
                    }

                    if (hasCargo &&
                        shipmentToOrderMap.TryGetValue(shipmentEntity, out var postLoadOrderEntity) &&
                        _orderLookup.HasComponent(postLoadOrderEntity))
                    {
                        var order = _orderLookup[postLoadOrderEntity];
                        ReleaseServiceReservationsForOrder(
                            postLoadOrderEntity,
                            ServiceType.Load,
                            ref orderToServiceReservationMap,
                            ref _serviceReservationLookup,
                            ref loadServiceCounts,
                            order.SourceNode);
                    }

                    continue;
                }

                // InTransit → Unloading: Check for arrival via transport integration or time-based fallback
                // Integration points:
                // 1. HaulingLoopState: If hauler has HaulingLoopState with Phase == Unloading, hauler has arrived
                // 2. Position-based: If hauler has LocalTransform and is within arrival distance of destination
                // 3. Time-based fallback: Use EstimatedArrivalTick if no transport components found
                if (currentStatus == ShipmentStatus.InTransit)
                {
                    if (!shipmentToOrderMap.TryGetValue(shipmentEntity, out Entity transitOrderEntity) ||
                        !_orderLookup.HasComponent(transitOrderEntity))
                    {
                        continue;
                    }

                    var order = _orderLookup[transitOrderEntity];
                    Entity transportEntity = shipment.ValueRO.AssignedTransport;
                    bool isAbstractShipment = shipment.ValueRO.RepresentationMode == ShipmentRepresentationMode.Abstract;

                    if (!isAbstractShipment)
                    {
                        if (transportEntity == Entity.Null || !entityManager.Exists(transportEntity))
                        {
                            FailOrderAndShipment(
                                transitOrderEntity,
                                order.SourceNode,
                                order.DestinationNode,
                                ShipmentFailureReason.TransportLost,
                                ref _orderLookup,
                                shipment,
                                orderToInventoryReservationMap,
                                orderToCapacityReservationMap,
                                ref _inventoryReservationLookup,
                                ref _capacityReservationLookup,
                                ref orderToServiceReservationMap,
                                ref _serviceReservationLookup,
                                ref loadServiceCounts,
                                ref unloadServiceCounts);
                            continue;
                        }
                    }

                    bool shouldUnload = false;
                    bool missingHaulingLoopState = !isAbstractShipment &&
                                                  transportEntity != Entity.Null &&
                                                  !_haulingLoopStateLookup.HasComponent(transportEntity);

                    // Integration point 1: HaulingLoopState
                    if (transportEntity != Entity.Null &&
                        _haulingLoopStateLookup.HasComponent(transportEntity))
                    {
                        var loopState = _haulingLoopStateLookup[transportEntity];
                        // Unloading phase indicates hauler has arrived at destination
                        if (loopState.Phase == HaulingLoopPhase.Unloading)
                        {
                            shouldUnload = true;
                        }
                    }

                    // Integration point 2: Position-based (if no HaulingLoopState)
                    if (!shouldUnload &&
                        transportEntity != Entity.Null &&
                        _transformLookup.HasComponent(transportEntity) &&
                        _nodeLookup.HasComponent(order.DestinationNode))
                    {
                        var transportPos = _transformLookup[transportEntity].Position;
                        var destPos = _nodeLookup[order.DestinationNode].Position;
                        float distance = math.distance(transportPos, destPos);

                        if (distance < ArrivalDistanceThreshold)
                        {
                            shouldUnload = true;
                        }
                    }

                    // Fallback: Time-based progression (abstract shipments or transports without state/position)
                    bool allowTimeFallback = false;
                    if (transportEntity == Entity.Null)
                    {
                        allowTimeFallback = isAbstractShipment;
                    }
                    else
                    {
                        bool hasLoop = _haulingLoopStateLookup.HasComponent(transportEntity);
                        bool hasTransform = _transformLookup.HasComponent(transportEntity);
                        allowTimeFallback = !hasLoop && !hasTransform;
                    }

                    if (missingHaulingLoopState &&
                        tickTime.Tick % warningIntervalTicks == 0)
                    {
                        var hasTransform = _transformLookup.HasComponent(transportEntity);
                        var warningMessage = hasTransform
                            ? $"[ResourceLogistics] Shipment {shipmentEntity.Index} transport {transportEntity.Index} lacks HaulingLoopState; using LocalTransform fallback."
                            : $"[ResourceLogistics] Shipment {shipmentEntity.Index} transport {transportEntity.Index} lacks HaulingLoopState and LocalTransform; advancing via ETA.";
                        LogHaulerIntegrationWarning(warningMessage);
                    }

                    if (!shouldUnload && allowTimeFallback && tickTime.Tick >= shipment.ValueRO.EstimatedArrivalTick)
                    {
                        shouldUnload = true;
                    }

                    if (shouldUnload)
                    {
                        // Check if Unload service is available at destination node
                        bool serviceAvailable = true;
                        if (shipmentToOrderMap.TryGetValue(shipmentEntity, out Entity unloadOrderEntity) &&
                            _orderLookup.HasComponent(unloadOrderEntity))
                        {
                            var unloadOrder = _orderLookup[unloadOrderEntity];
                            Entity destinationNode = unloadOrder.DestinationNode;

                            if (_nodeLookup.HasComponent(destinationNode))
                            {
                                // Count active Unload service reservations at destination node
                                int activeUnloadReservations = GetServiceCount(unloadServiceCounts, destinationNode);

                                // Check capacity
                                var nodeServices = _nodeLookup[destinationNode].Services;
                                byte capacity = nodeServices.SlotCapacity > 0 ? nodeServices.SlotCapacity : DefaultServiceCapacity;

                                if (activeUnloadReservations >= capacity)
                                {
                                    serviceAvailable = false; // Service capacity full, wait
                                }
                                else
                                {
                                    // Create Unload service reservation
                                    var serviceReservation = ResourceReservationService.ReserveService(
                                        destinationNode,
                                        ServiceType.Unload,
                                        unloadOrderEntity,
                                        tickTime.Tick,
                                        tickTime.Tick,
                                        serviceReservationTtlTicks,
                                        _nextServiceReservationId++);

                                    var serviceResEntity = ecb.CreateEntity();
                                    ecb.AddComponent(serviceResEntity, serviceReservation);
                                    IncrementServiceCount(ref unloadServiceCounts, destinationNode);
                                    orderToServiceReservationMap.Add(unloadOrderEntity, serviceResEntity);
                                }
                            }
                        }

                        if (serviceAvailable)
                        {
                            shipment.ValueRW.Status = ShipmentStatus.Unloading;
                        }
                    }
                    continue;
                }

                // Unloading status is handled by delivery system
            }

            shipmentToOrderMap.Dispose();
            orderToInventoryReservationMap.Dispose();
            orderToCapacityReservationMap.Dispose();
            loadServiceCounts.Dispose();
            unloadServiceCounts.Dispose();
            orderToServiceReservationMap.Dispose();
            ecb.Playback(state.EntityManager);
        }

        private static void IncrementServiceCount(
            ref NativeParallelHashMap<Entity, int> counts,
            Entity node)
        {
            if (counts.TryGetValue(node, out var count))
            {
                counts.Remove(node);
                counts.TryAdd(node, count + 1);
            }
            else
            {
                counts.TryAdd(node, 1);
            }
        }

        private static void DecrementServiceCount(
            ref NativeParallelHashMap<Entity, int> counts,
            Entity node)
        {
            if (counts.TryGetValue(node, out var count) && count > 0)
            {
                counts.Remove(node);
                counts.TryAdd(node, count - 1);
            }
        }

        private static int GetServiceCount(
            in NativeParallelHashMap<Entity, int> counts,
            Entity node)
        {
            return counts.TryGetValue(node, out var count) ? count : 0;
        }

        private static void ReleaseServiceReservationsForOrder(
            Entity orderEntity,
            ServiceType serviceType,
            ref NativeParallelMultiHashMap<Entity, Entity> orderToServiceReservationMap,
            ref ComponentLookup<ServiceReservation> serviceReservationLookup,
            ref NativeParallelHashMap<Entity, int> serviceCounts,
            Entity serviceNode)
        {
            if (!orderToServiceReservationMap.TryGetFirstValue(orderEntity, out var reservationEntity, out var iterator))
            {
                return;
            }

            do
            {
                if (!serviceReservationLookup.HasComponent(reservationEntity))
                {
                    continue;
                }

                var reservation = serviceReservationLookup[reservationEntity];
                if (reservation.ServiceType != serviceType ||
                    reservation.Status != ReservationStatus.Active)
                {
                    continue;
                }

                ResourceReservationService.ReleaseReservation(ref reservation);
                serviceReservationLookup[reservationEntity] = reservation;
                DecrementServiceCount(ref serviceCounts, serviceNode);
            }
            while (orderToServiceReservationMap.TryGetNextValue(out reservationEntity, ref iterator));
        }

        private static void ReleaseInventoryReservationForOrder(
            Entity orderEntity,
            NativeHashMap<Entity, Entity> orderToInventoryReservationMap,
            ref ComponentLookup<InventoryReservation> inventoryReservationLookup)
        {
            if (orderToInventoryReservationMap.TryGetValue(orderEntity, out var reservationEntity) &&
                inventoryReservationLookup.HasComponent(reservationEntity))
            {
                var reservation = inventoryReservationLookup[reservationEntity];
                ResourceReservationService.ReleaseReservation(ref reservation);
                inventoryReservationLookup[reservationEntity] = reservation;
            }
        }

        private static void ReleaseCapacityReservationForOrder(
            Entity orderEntity,
            NativeHashMap<Entity, Entity> orderToCapacityReservationMap,
            ref ComponentLookup<CapacityReservation> capacityReservationLookup)
        {
            if (orderToCapacityReservationMap.TryGetValue(orderEntity, out var reservationEntity) &&
                capacityReservationLookup.HasComponent(reservationEntity))
            {
                var reservation = capacityReservationLookup[reservationEntity];
                ResourceReservationService.ReleaseReservation(ref reservation);
                capacityReservationLookup[reservationEntity] = reservation;
            }
        }

        private static void FailOrderAndShipment(
            Entity orderEntity,
            Entity sourceNode,
            Entity destinationNode,
            ShipmentFailureReason failureReason,
            ref ComponentLookup<LogisticsOrder> orderLookup,
            RefRW<Shipment> shipment,
            NativeHashMap<Entity, Entity> orderToInventoryReservationMap,
            NativeHashMap<Entity, Entity> orderToCapacityReservationMap,
            ref ComponentLookup<InventoryReservation> inventoryReservationLookup,
            ref ComponentLookup<CapacityReservation> capacityReservationLookup,
            ref NativeParallelMultiHashMap<Entity, Entity> orderToServiceReservationMap,
            ref ComponentLookup<ServiceReservation> serviceReservationLookup,
            ref NativeParallelHashMap<Entity, int> loadServiceCounts,
            ref NativeParallelHashMap<Entity, int> unloadServiceCounts)
        {
            if (orderLookup.HasComponent(orderEntity))
            {
                var order = orderLookup[orderEntity];
                order.Status = LogisticsOrderStatus.Failed;
                order.FailureReason = failureReason;
                orderLookup[orderEntity] = order;
            }

            shipment.ValueRW.Status = ShipmentStatus.Failed;
            shipment.ValueRW.FailureReason = failureReason;
            shipment.ValueRW.ActualArrivalTick = 0;

            ReleaseInventoryReservationForOrder(orderEntity, orderToInventoryReservationMap, ref inventoryReservationLookup);
            ReleaseCapacityReservationForOrder(orderEntity, orderToCapacityReservationMap, ref capacityReservationLookup);

            if (sourceNode != Entity.Null)
            {
                ReleaseServiceReservationsForOrder(
                    orderEntity,
                    ServiceType.Load,
                    ref orderToServiceReservationMap,
                    ref serviceReservationLookup,
                    ref loadServiceCounts,
                    sourceNode);
            }

            if (destinationNode != Entity.Null)
            {
                ReleaseServiceReservationsForOrder(
                    orderEntity,
                    ServiceType.Unload,
                    ref orderToServiceReservationMap,
                    ref serviceReservationLookup,
                    ref unloadServiceCounts,
                    destinationNode);
            }
        }

        [BurstDiscard]
        private static void LogHaulerIntegrationWarning(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(message);
#endif
        }

        private static ushort ResolveResourceTypeIndex(
            in LogisticsOrder order,
            BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            ref var ids = ref catalog.Value.Ids;
            var existingIndex = (int)order.ResourceTypeIndex;
            if (existingIndex >= 0 && existingIndex < ids.Length &&
                ids[existingIndex].Equals(order.ResourceId))
            {
                return order.ResourceTypeIndex;
            }

            var resolvedIndex = catalog.Value.LookupIndex(order.ResourceId);
            return resolvedIndex < 0 ? ushort.MaxValue : (ushort)resolvedIndex;
        }

    }
}

