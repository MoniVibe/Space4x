using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Completes deliveries and releases reservations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResourceLogisticsShipmentProgressionSystem))]
    public partial struct ResourceLogisticsDeliverySystem : ISystem
    {
        private ComponentLookup<Shipment> _shipmentLookup;
        private ComponentLookup<LogisticsOrder> _orderLookup;
        private ComponentLookup<InventoryReservation> _inventoryReservationLookup;
        private ComponentLookup<CapacityReservation> _capacityReservationLookup;
        private ComponentLookup<ServiceReservation> _serviceReservationLookup;
        private BufferLookup<ConstructionDeliveredElement> _constructionDeliveredLookup;
        private BufferLookup<StorehouseInventoryItem> _storehouseInventoryLookup;
        private ComponentLookup<StorehouseInventory> _storehouseInventoryComponentLookup;
        private BufferLookup<ShipmentCargoAllocation> _shipmentCargoAllocationLookup;
        private BufferLookup<StorehouseCapacityElement> _storehouseCapacityLookup;
        private BufferLookup<StorehouseReservationItem> _storehouseReservationLookup;
        private BufferLookup<DeliveryReceipt> _deliveryReceiptLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<Shipment>();
            state.RequireForUpdate<ResourceTypeIndex>();
            _shipmentLookup = state.GetComponentLookup<Shipment>(false);
            _orderLookup = state.GetComponentLookup<LogisticsOrder>(false);
            _capacityReservationLookup = state.GetComponentLookup<CapacityReservation>(false);
            _serviceReservationLookup = state.GetComponentLookup<ServiceReservation>(false);
            _inventoryReservationLookup = state.GetComponentLookup<InventoryReservation>(false);
            _constructionDeliveredLookup = state.GetBufferLookup<ConstructionDeliveredElement>(false);
            _storehouseInventoryLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _storehouseInventoryComponentLookup = state.GetComponentLookup<StorehouseInventory>(false);
            _shipmentCargoAllocationLookup = state.GetBufferLookup<ShipmentCargoAllocation>(true);
            _storehouseCapacityLookup = state.GetBufferLookup<StorehouseCapacityElement>(true);
            _storehouseReservationLookup = state.GetBufferLookup<StorehouseReservationItem>(true);
            _deliveryReceiptLookup = state.GetBufferLookup<DeliveryReceipt>(false);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
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

            _shipmentLookup.Update(ref state);
            _orderLookup.Update(ref state);
            _inventoryReservationLookup.Update(ref state);
            _capacityReservationLookup.Update(ref state);
            _serviceReservationLookup.Update(ref state);
            _constructionDeliveredLookup.Update(ref state);
            _storehouseInventoryLookup.Update(ref state);
            _storehouseInventoryComponentLookup.Update(ref state);
            _shipmentCargoAllocationLookup.Update(ref state);
            _storehouseCapacityLookup.Update(ref state);
            _storehouseReservationLookup.Update(ref state);
            _deliveryReceiptLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Build shipment→order mapping for O(1) lookup (reversed from order→shipment)
            var shipmentToOrderMap = new NativeHashMap<Entity, Entity>(16, Allocator.Temp);
            foreach (var (order, orderEntity) in SystemAPI.Query<RefRO<LogisticsOrder>>()
                .WithEntityAccess())
            {
                if (order.ValueRO.ShipmentEntity != Entity.Null)
                {
                    shipmentToOrderMap.TryAdd(order.ValueRO.ShipmentEntity, orderEntity);
                }
            }

            var orderToInventoryReservationMap = new NativeHashMap<Entity, Entity>(16, Allocator.Temp);
            foreach (var (reservation, resEntity) in SystemAPI.Query<RefRO<InventoryReservation>>()
                .WithEntityAccess())
            {
                orderToInventoryReservationMap.TryAdd(reservation.ValueRO.OrderEntity, resEntity);
            }

            var orderToCapacityReservationMap = new NativeHashMap<Entity, Entity>(16, Allocator.Temp);
            foreach (var (reservation, resEntity) in SystemAPI.Query<RefRO<CapacityReservation>>()
                .WithEntityAccess())
            {
                orderToCapacityReservationMap.TryAdd(reservation.ValueRO.OrderEntity, resEntity);
            }

            var orderToServiceReservationMap = new NativeParallelMultiHashMap<Entity, Entity>(128, Allocator.Temp);
            foreach (var (reservation, resEntity) in SystemAPI.Query<RefRO<ServiceReservation>>()
                .WithEntityAccess())
            {
                orderToServiceReservationMap.Add(reservation.ValueRO.OrderEntity, resEntity);
            }

            // Complete deliveries for shipments that have arrived
            foreach (var (shipment, shipmentEntity) in SystemAPI.Query<RefRW<Shipment>>()
                .WithEntityAccess())
            {
                if (shipment.ValueRO.Status != ShipmentStatus.InTransit &&
                    shipment.ValueRO.Status != ShipmentStatus.Unloading)
                {
                    continue;
                }

                // Check if shipment has arrived (simplified: check estimated arrival)
                if (tickTime.Tick >= shipment.ValueRO.EstimatedArrivalTick)
                {
                    // Find order for this shipment using O(1) map lookup
                    if (!shipmentToOrderMap.TryGetValue(shipmentEntity, out Entity orderEntity) ||
                        !_orderLookup.HasComponent(orderEntity))
                    {
                        continue;
                    }

                    var order = _orderLookup[orderEntity];
                    Entity destination = order.DestinationNode;
                    var resolvedResourceIndex = ResolveResourceTypeIndex(order, resourceTypeIndex.Catalog);
                    if (resolvedResourceIndex == ushort.MaxValue)
                    {
                        order.Status = LogisticsOrderStatus.Failed;
                        order.FailureReason = ShipmentFailureReason.InvalidDestination;
                        _orderLookup[orderEntity] = order;
                        ResourceLogisticsService.UpdateShipmentState(ref shipment.ValueRW, ShipmentStatus.Failed);
                        shipment.ValueRW.FailureReason = ShipmentFailureReason.InvalidDestination;
                        ReleaseInventoryReservationForOrder(orderEntity, orderToInventoryReservationMap, ref _inventoryReservationLookup);
                        ReleaseCapacityReservationForOrder(orderEntity, orderToCapacityReservationMap, ref _capacityReservationLookup);
                        ReleaseServiceReservationsForOrder(orderEntity, ref orderToServiceReservationMap, ref _serviceReservationLookup);
                        continue;
                    }

                    // Verify source was already withdrawn (safety check to prevent double-withdrawal)
                    bool sourceAlreadyWithdrawn = false;
                    if (orderToInventoryReservationMap.TryGetValue(orderEntity, out var inventoryReservationEntity) &&
                        _inventoryReservationLookup.HasComponent(inventoryReservationEntity))
                    {
                        var inventoryReservation = _inventoryReservationLookup[inventoryReservationEntity];
                        sourceAlreadyWithdrawn = inventoryReservation.Status == ReservationStatus.Committed &&
                                                 inventoryReservation.ReservedAmount > 0f;
                    }

                    // Calculate actual delivered amount from ShipmentCargoAllocation buffer
                    float deliveredAmount = 0f;
                    if (_shipmentCargoAllocationLookup.HasBuffer(shipmentEntity))
                    {
                        var cargoAllocations = _shipmentCargoAllocationLookup[shipmentEntity];
                        for (int i = 0; i < cargoAllocations.Length; i++)
                        {
                            if (cargoAllocations[i].ResourceTypeIndex == resolvedResourceIndex)
                            {
                                deliveredAmount += cargoAllocations[i].AllocatedAmount;
                            }
                        }
                    }

                    // Clamp to requested amount (shouldn't exceed)
                    deliveredAmount = math.min(deliveredAmount, order.RequestedAmount);

                    if (deliveredAmount <= 0f || !sourceAlreadyWithdrawn)
                    {
                        order.Status = LogisticsOrderStatus.Failed;
                        order.FailureReason = ShipmentFailureReason.NoInventory;
                        _orderLookup[orderEntity] = order;
                        ResourceLogisticsService.UpdateShipmentState(ref shipment.ValueRW, ShipmentStatus.Failed);
                        shipment.ValueRW.FailureReason = ShipmentFailureReason.NoInventory;
                        ReleaseInventoryReservationForOrder(orderEntity, orderToInventoryReservationMap, ref _inventoryReservationLookup);
                        ReleaseCapacityReservationForOrder(orderEntity, orderToCapacityReservationMap, ref _capacityReservationLookup);
                        ReleaseServiceReservationsForOrder(orderEntity, ref orderToServiceReservationMap, ref _serviceReservationLookup);
                        continue;
                    }

                    float creditedAmount = deliveredAmount;

                    bool requiresStorehouseDeposit =
                        _storehouseInventoryLookup.HasBuffer(destination) &&
                        _storehouseInventoryComponentLookup.HasComponent(destination);

                    if (requiresStorehouseDeposit)
                    {
                        if (!_storehouseCapacityLookup.HasBuffer(destination) ||
                            !_storehouseReservationLookup.HasBuffer(destination))
                        {
                            order.Status = LogisticsOrderStatus.Failed;
                            order.FailureReason = ShipmentFailureReason.InvalidDestination;
                            _orderLookup[orderEntity] = order;
                            ResourceLogisticsService.UpdateShipmentState(ref shipment.ValueRW, ShipmentStatus.Failed);
                            shipment.ValueRW.FailureReason = ShipmentFailureReason.InvalidDestination;
                            ReleaseInventoryReservationForOrder(orderEntity, orderToInventoryReservationMap, ref _inventoryReservationLookup);
                            ReleaseCapacityReservationForOrder(orderEntity, orderToCapacityReservationMap, ref _capacityReservationLookup);
                            ReleaseServiceReservationsForOrder(orderEntity, ref orderToServiceReservationMap, ref _serviceReservationLookup);
                            continue;
                        }

                        var inventoryBuffer = _storehouseInventoryLookup[destination];
                        var inventoryComponent = _storehouseInventoryComponentLookup[destination];
                        var capacityBuffer = _storehouseCapacityLookup[destination];
                        var reservationBuffer = _storehouseReservationLookup[destination];

                        if (!StorehouseMutationService.TryDepositWithPerTypeCapacity(
                                resolvedResourceIndex,
                                creditedAmount,
                                resourceTypeIndex.Catalog,
                                ref inventoryComponent,
                                inventoryBuffer,
                                capacityBuffer,
                                reservationBuffer,
                                out float depositedAmount) ||
                            depositedAmount <= 0f)
                        {
                            order.Status = LogisticsOrderStatus.Failed;
                            order.FailureReason = ShipmentFailureReason.CapacityFull;
                            _orderLookup[orderEntity] = order;
                            ResourceLogisticsService.UpdateShipmentState(ref shipment.ValueRW, ShipmentStatus.Failed);
                            shipment.ValueRW.FailureReason = ShipmentFailureReason.CapacityFull;
                            ReleaseInventoryReservationForOrder(orderEntity, orderToInventoryReservationMap, ref _inventoryReservationLookup);
                            ReleaseCapacityReservationForOrder(orderEntity, orderToCapacityReservationMap, ref _capacityReservationLookup);
                            ReleaseServiceReservationsForOrder(orderEntity, ref orderToServiceReservationMap, ref _serviceReservationLookup);
                            continue;
                        }

                        creditedAmount = depositedAmount;
                        _storehouseInventoryComponentLookup[destination] = inventoryComponent;
                    }
                    else if (_storehouseInventoryLookup.HasBuffer(destination))
                    {
                        // Storehouse buffer is present but inventory component missing – treat as failure
                        order.Status = LogisticsOrderStatus.Failed;
                        order.FailureReason = ShipmentFailureReason.InvalidDestination;
                        _orderLookup[orderEntity] = order;
                        ResourceLogisticsService.UpdateShipmentState(ref shipment.ValueRW, ShipmentStatus.Failed);
                        shipment.ValueRW.FailureReason = ShipmentFailureReason.InvalidDestination;
                        ReleaseInventoryReservationForOrder(orderEntity, orderToInventoryReservationMap, ref _inventoryReservationLookup);
                        ReleaseCapacityReservationForOrder(orderEntity, orderToCapacityReservationMap, ref _capacityReservationLookup);
                        ReleaseServiceReservationsForOrder(orderEntity, ref orderToServiceReservationMap, ref _serviceReservationLookup);
                        continue;
                    }

                    // Deliver to construction site
                    if (_constructionDeliveredLookup.HasBuffer(destination))
                    {
                        var deliveredBuffer = _constructionDeliveredLookup[destination];
                        bool found = false;

                        for (int i = 0; i < deliveredBuffer.Length; i++)
                        {
                            if (deliveredBuffer[i].ResourceTypeId.Equals(order.ResourceId))
                            {
                                var delivered = deliveredBuffer[i];
                                delivered.UnitsDelivered += creditedAmount;
                                deliveredBuffer[i] = delivered;
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            deliveredBuffer.Add(new ConstructionDeliveredElement
                            {
                                ResourceTypeId = order.ResourceId,
                                UnitsDelivered = creditedAmount
                            });
                        }
                    }

                    AppendDeliveryReceipt(
                        destination,
                        order,
                        creditedAmount,
                        tickTime.Tick,
                        ref _deliveryReceiptLookup,
                        ref ecb);

                    // Update order status
                    order.Status = LogisticsOrderStatus.Delivered;
                    order.FailureReason = ShipmentFailureReason.None;
                    _orderLookup[orderEntity] = order;

                    ResourceLogisticsService.UpdateShipmentState(ref shipment.ValueRW, ShipmentStatus.Delivered);
                    shipment.ValueRW.FailureReason = ShipmentFailureReason.None;
                    shipment.ValueRW.ActualArrivalTick = tickTime.Tick;

                    // Release reservations (mark as released, will be cleaned up later)
                    ReleaseInventoryReservationForOrder(orderEntity, orderToInventoryReservationMap, ref _inventoryReservationLookup);
                    ReleaseCapacityReservationForOrder(orderEntity, orderToCapacityReservationMap, ref _capacityReservationLookup);
                    ReleaseServiceReservationsForOrder(orderEntity, ref orderToServiceReservationMap, ref _serviceReservationLookup);
                }
            }

            shipmentToOrderMap.Dispose();
            orderToInventoryReservationMap.Dispose();
            orderToCapacityReservationMap.Dispose();
            orderToServiceReservationMap.Dispose();

            // Cleanup completed orders and shipments (optional: keep for history)
            // For now, we'll keep them but could add cleanup logic here if needed
            // foreach (var (order, orderEntity) in SystemAPI.Query<RefRO<LogisticsOrder>>()
            //     .WithEntityAccess())
            // {
            //     if (order.ValueRO.Status == LogisticsOrderStatus.Delivered)
            //     {
            //         ecb.DestroyEntity(orderEntity);
            //     }
            // }

            // Cleanup completed shipments
            foreach (var (shipment, shipmentEntity) in SystemAPI.Query<RefRO<Shipment>>()
                .WithEntityAccess())
            {
                if (shipment.ValueRO.Status == ShipmentStatus.Delivered ||
                    shipment.ValueRO.Status == ShipmentStatus.Failed)
                {
                    // Keep for now, but could cleanup after a delay
                    // ecb.DestroyEntity(shipmentEntity);
                }
            }

            ecb.Playback(state.EntityManager);
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

    private static void ReleaseServiceReservationsForOrder(
        Entity orderEntity,
        ref NativeParallelMultiHashMap<Entity, Entity> orderToServiceReservationMap,
        ref ComponentLookup<ServiceReservation> serviceReservationLookup)
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
            ResourceReservationService.ReleaseReservation(ref reservation);
            serviceReservationLookup[reservationEntity] = reservation;
        }
        while (orderToServiceReservationMap.TryGetNextValue(out reservationEntity, ref iterator));
    }

    private static void AppendDeliveryReceipt(
        Entity destination,
        in LogisticsOrder order,
        float deliveredAmount,
        uint deliveryTick,
        ref BufferLookup<DeliveryReceipt> receiptLookup,
        ref EntityCommandBuffer ecb)
    {
        if (destination == Entity.Null || deliveredAmount <= 0f)
        {
            return;
        }

        DynamicBuffer<DeliveryReceipt> receipts;
        if (receiptLookup.HasBuffer(destination))
        {
            receipts = receiptLookup[destination];
        }
        else
        {
            receipts = ecb.AddBuffer<DeliveryReceipt>(destination);
        }

        receipts.Add(new DeliveryReceipt
        {
            RequestId = order.OrderId < 0 ? 0u : (uint)order.OrderId,
            DeliveredAmount = deliveredAmount,
            DelivererEntity = order.AssignedTransport,
            RecipientEntity = destination,
            DeliveryTick = deliveryTick,
            ResourceTypeId = ToFixed32(order.ResourceId)
        });
    }

    private static FixedString32Bytes ToFixed32(FixedString64Bytes value)
    {
        FixedString32Bytes result = default;
        for (int i = 0; i < value.Length && result.Length < result.Capacity; i++)
        {
            result.Append((char)value[i]);
        }
        return result;
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

