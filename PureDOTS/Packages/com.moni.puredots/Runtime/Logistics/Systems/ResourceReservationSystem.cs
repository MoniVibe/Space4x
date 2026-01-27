using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Resource;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Manages resource reservations: inventory, capacity, and service reservations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResourceLogisticsPlanningSystem))]
    [UpdateBefore(typeof(ResourceLogisticsDispatchSystem))]
    public partial struct ResourceReservationSystem : ISystem
    {
        private const float DefaultReservationTTLSeconds = 16.6667f;
        private ComponentLookup<LogisticsOrder> _orderLookup;
        private ComponentLookup<InventoryReservation> _inventoryReservationLookup;
        private ComponentLookup<CapacityReservation> _capacityReservationLookup;
        private ComponentLookup<ServiceReservation> _serviceReservationLookup;
        private ComponentLookup<ReservationPolicy> _policyLookup;
        private BufferLookup<StorehouseInventoryItem> _storehouseInventoryItems;
        private ComponentLookup<StorehouseInventory> _storehouseInventoryLookup;

        private int _nextReservationId;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<LogisticsOrder>();
            state.RequireForUpdate<ResourceTypeIndex>();
            _orderLookup = state.GetComponentLookup<LogisticsOrder>(false);
            _inventoryReservationLookup = state.GetComponentLookup<InventoryReservation>(false);
            _capacityReservationLookup = state.GetComponentLookup<CapacityReservation>(false);
            _serviceReservationLookup = state.GetComponentLookup<ServiceReservation>(false);
            _policyLookup = state.GetComponentLookup<ReservationPolicy>(false);
            _storehouseInventoryItems = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _storehouseInventoryLookup = state.GetComponentLookup<StorehouseInventory>(true);
            _nextReservationId = 1;
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

            _orderLookup.Update(ref state);
            _inventoryReservationLookup.Update(ref state);
            _capacityReservationLookup.Update(ref state);
            _serviceReservationLookup.Update(ref state);
            _policyLookup.Update(ref state);
            _storehouseInventoryItems.Update(ref state);
            _storehouseInventoryLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var fixedDeltaTime = math.max(1e-6f, tickTime.FixedDeltaTime);
            var defaultTTLSeconds = DefaultReservationTTLSeconds;
            var allowPartialReservations = false;
            if (SystemAPI.TryGetSingleton<ReservationPolicy>(out var policy))
            {
                defaultTTLSeconds = policy.DefaultTTLSeconds;
                allowPartialReservations = policy.AllowPartialReservations != 0;
            }
            var defaultTTL = (uint)math.max(1f, math.ceil(defaultTTLSeconds / fixedDeltaTime));

            // Create reservations for orders in Planning status
            foreach (var (order, orderEntity) in SystemAPI.Query<RefRW<LogisticsOrder>>()
                .WithEntityAccess())
            {
                if (order.ValueRO.Status != LogisticsOrderStatus.Planning)
                {
                    continue;
                }

                var resourceTypeIndexResolved = ResolveResourceTypeIndex(order.ValueRO, resourceTypeIndex.Catalog);
                if (resourceTypeIndexResolved == ushort.MaxValue)
                {
                    order.ValueRW.Status = LogisticsOrderStatus.Failed;
                    order.ValueRW.FailureReason = ShipmentFailureReason.InvalidSource;
                    order.ValueRW.ReservedAmount = 0f;
                    continue;
                }

                if (order.ValueRO.ResourceTypeIndex != resourceTypeIndexResolved)
                {
                    order.ValueRW.ResourceTypeIndex = resourceTypeIndexResolved;
                }

                order.ValueRW.SourceInventory = new InventoryHandle
                {
                    StorehouseEntity = order.ValueRO.SourceNode,
                    ResourceTypeIndex = resourceTypeIndexResolved
                };
                order.ValueRW.DestinationInventory = new InventoryHandle
                {
                    StorehouseEntity = order.ValueRO.DestinationNode,
                    ResourceTypeIndex = resourceTypeIndexResolved
                };

                if (!TryReserveInventory(order.ValueRO.SourceNode,
                        resourceTypeIndexResolved,
                        order.ValueRO.RequestedAmount,
                        allowPartialReservations,
                        resourceTypeIndex.Catalog,
                        out var reservedAmount,
                        out var failureReason))
                {
                    order.ValueRW.Status = LogisticsOrderStatus.Failed;
                    order.ValueRW.FailureReason = failureReason;
                    order.ValueRW.ReservedAmount = 0f;
                    continue;
                }

                // Reserve inventory at source
                var invReservation = ResourceReservationService.ReserveInventory(
                    order.ValueRO.SourceNode,
                    Entity.Null, // Container would be determined from node
                    order.ValueRO.ResourceId,
                    reservedAmount,
                    orderEntity,
                    tickTime.Tick,
                    defaultTTL,
                    _nextReservationId++);
                invReservation.ReservationFlags |= InventoryReservationFlags.ReservedApplied;

                var invResEntity = ecb.CreateEntity();
                ecb.AddComponent(invResEntity, invReservation);

                order.ValueRW.Status = LogisticsOrderStatus.Reserved;
                order.ValueRW.ReservedAmount = reservedAmount;
                order.ValueRW.FailureReason = ShipmentFailureReason.None;
            }

            // Cancel expired reservations and remove released/expired ones
            foreach (var (reservation, entity) in SystemAPI.Query<RefRW<InventoryReservation>>()
                .WithEntityAccess())
            {
                ApplyOrderCancellation(reservation.ValueRO, ref reservation.ValueRW);
                ResourceReservationService.CancelExpiredReservation(ref reservation.ValueRW, tickTime.Tick);
                if (reservation.ValueRO.Status == ReservationStatus.Expired)
                {
                    FailOrderForExpiredReservation(reservation.ValueRO);
                }
                if (reservation.ValueRO.Status == ReservationStatus.Released ||
                    reservation.ValueRO.Status == ReservationStatus.Expired ||
                    reservation.ValueRO.Status == ReservationStatus.Cancelled)
                {
                    ReleaseInventoryHold(reservation.ValueRO, resourceTypeIndex.Catalog);
                    ecb.DestroyEntity(entity);
                }
            }

            foreach (var (reservation, entity) in SystemAPI.Query<RefRW<CapacityReservation>>()
                .WithEntityAccess())
            {
                ApplyOrderCancellation(reservation.ValueRO, ref reservation.ValueRW);
                ResourceReservationService.CancelExpiredReservation(ref reservation.ValueRW, tickTime.Tick);
                if (reservation.ValueRO.Status == ReservationStatus.Expired)
                {
                    FailOrderForExpiredReservation(reservation.ValueRO);
                }
                if (reservation.ValueRO.Status == ReservationStatus.Released ||
                    reservation.ValueRO.Status == ReservationStatus.Expired ||
                    reservation.ValueRO.Status == ReservationStatus.Cancelled)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            foreach (var (reservation, entity) in SystemAPI.Query<RefRW<ServiceReservation>>()
                .WithEntityAccess())
            {
                ApplyOrderCancellation(reservation.ValueRO, ref reservation.ValueRW);
                ResourceReservationService.CancelExpiredReservation(ref reservation.ValueRW, tickTime.Tick);
                if (reservation.ValueRO.Status == ReservationStatus.Expired)
                {
                    FailOrderForExpiredReservation(reservation.ValueRO);
                }
                if (reservation.ValueRO.Status == ReservationStatus.Released ||
                    reservation.ValueRO.Status == ReservationStatus.Expired ||
                    reservation.ValueRO.Status == ReservationStatus.Cancelled)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
        }

        private bool TryReserveInventory(
            Entity sourceNode,
            ushort resourceTypeIndex,
            float requestedAmount,
            bool allowPartial,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            out float reservedAmount,
            out ShipmentFailureReason failureReason)
        {
            reservedAmount = 0f;
            failureReason = ShipmentFailureReason.None;

            if (resourceTypeIndex == ushort.MaxValue ||
                resourceTypeIndex >= catalog.Value.Ids.Length)
            {
                failureReason = ShipmentFailureReason.InvalidSource;
                return false;
            }

            if (sourceNode == Entity.Null ||
                !_storehouseInventoryLookup.HasComponent(sourceNode) ||
                !_storehouseInventoryItems.HasBuffer(sourceNode))
            {
                failureReason = ShipmentFailureReason.InvalidSource;
                return false;
            }

            if (requestedAmount <= 0f)
            {
                failureReason = ShipmentFailureReason.NoInventory;
                return false;
            }

            var items = _storehouseInventoryItems[sourceNode];
            if (StorehouseMutationService.TryReserveOut(
                    resourceTypeIndex,
                    requestedAmount,
                    allowPartial,
                    catalog,
                    items,
                    out reservedAmount))
            {
                return true;
            }

            failureReason = ShipmentFailureReason.NoInventory;
            return false;
        }

        private void ReleaseInventoryHold(
            InventoryReservation reservation,
            BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            if ((reservation.ReservationFlags & InventoryReservationFlags.ReservedApplied) == 0)
            {
                return;
            }

            if ((reservation.ReservationFlags & InventoryReservationFlags.Withdrawn) != 0)
            {
                return;
            }

            if (reservation.SourceNode == Entity.Null ||
                !_storehouseInventoryItems.HasBuffer(reservation.SourceNode))
            {
                return;
            }

            var items = _storehouseInventoryItems[reservation.SourceNode];
            var resourceIndex = catalog.Value.LookupIndex(reservation.ResourceId);
            if (resourceIndex < 0)
            {
                return;
            }

            StorehouseMutationService.CancelReserveOut(
                (ushort)resourceIndex,
                reservation.ReservedAmount,
                catalog,
                items);
        }

        private void ApplyOrderCancellation(
            InventoryReservation reservation,
            ref InventoryReservation reservationMutable)
        {
            if (reservation.Status != ReservationStatus.Active)
            {
                return;
            }

            if (reservation.OrderEntity == Entity.Null ||
                !_orderLookup.HasComponent(reservation.OrderEntity))
            {
                return;
            }

            var order = _orderLookup[reservation.OrderEntity];
            if (order.Status == LogisticsOrderStatus.Cancelled)
            {
                ResourceReservationService.CancelReservation(ref reservationMutable, ReservationCancelReason.Cancelled);
            }
            else if (order.Status == LogisticsOrderStatus.Failed)
            {
                var reason = MapCancelReason(order.FailureReason);
                ResourceReservationService.CancelReservation(ref reservationMutable, reason);
            }
        }

        private void ApplyOrderCancellation(
            CapacityReservation reservation,
            ref CapacityReservation reservationMutable)
        {
            if (reservation.Status != ReservationStatus.Active)
            {
                return;
            }

            if (reservation.OrderEntity == Entity.Null ||
                !_orderLookup.HasComponent(reservation.OrderEntity))
            {
                return;
            }

            var order = _orderLookup[reservation.OrderEntity];
            if (order.Status == LogisticsOrderStatus.Cancelled)
            {
                ResourceReservationService.CancelReservation(ref reservationMutable, ReservationCancelReason.Cancelled);
            }
            else if (order.Status == LogisticsOrderStatus.Failed)
            {
                var reason = MapCancelReason(order.FailureReason);
                ResourceReservationService.CancelReservation(ref reservationMutable, reason);
            }
        }

        private void ApplyOrderCancellation(
            ServiceReservation reservation,
            ref ServiceReservation reservationMutable)
        {
            if (reservation.Status != ReservationStatus.Active)
            {
                return;
            }

            if (reservation.OrderEntity == Entity.Null ||
                !_orderLookup.HasComponent(reservation.OrderEntity))
            {
                return;
            }

            var order = _orderLookup[reservation.OrderEntity];
            if (order.Status == LogisticsOrderStatus.Cancelled)
            {
                ResourceReservationService.CancelReservation(ref reservationMutable, ReservationCancelReason.Cancelled);
            }
            else if (order.Status == LogisticsOrderStatus.Failed)
            {
                var reason = MapCancelReason(order.FailureReason);
                ResourceReservationService.CancelReservation(ref reservationMutable, reason);
            }
        }

        private void FailOrderForExpiredReservation(InventoryReservation reservation)
        {
            if (reservation.OrderEntity == Entity.Null ||
                !_orderLookup.HasComponent(reservation.OrderEntity))
            {
                return;
            }

            var order = _orderLookup[reservation.OrderEntity];
            if (order.Status == LogisticsOrderStatus.Delivered ||
                order.Status == LogisticsOrderStatus.Failed ||
                order.Status == LogisticsOrderStatus.Cancelled)
            {
                return;
            }

            order.Status = LogisticsOrderStatus.Failed;
            order.FailureReason = ShipmentFailureReason.ReservationExpired;
            _orderLookup[reservation.OrderEntity] = order;
        }

        private void FailOrderForExpiredReservation(CapacityReservation reservation)
        {
            if (reservation.OrderEntity == Entity.Null ||
                !_orderLookup.HasComponent(reservation.OrderEntity))
            {
                return;
            }

            var order = _orderLookup[reservation.OrderEntity];
            if (order.Status == LogisticsOrderStatus.Delivered ||
                order.Status == LogisticsOrderStatus.Failed ||
                order.Status == LogisticsOrderStatus.Cancelled)
            {
                return;
            }

            order.Status = LogisticsOrderStatus.Failed;
            order.FailureReason = ShipmentFailureReason.ReservationExpired;
            _orderLookup[reservation.OrderEntity] = order;
        }

        private void FailOrderForExpiredReservation(ServiceReservation reservation)
        {
            if (reservation.OrderEntity == Entity.Null ||
                !_orderLookup.HasComponent(reservation.OrderEntity))
            {
                return;
            }

            var order = _orderLookup[reservation.OrderEntity];
            if (order.Status == LogisticsOrderStatus.Delivered ||
                order.Status == LogisticsOrderStatus.Failed ||
                order.Status == LogisticsOrderStatus.Cancelled)
            {
                return;
            }

            order.Status = LogisticsOrderStatus.Failed;
            order.FailureReason = ShipmentFailureReason.ReservationExpired;
            _orderLookup[reservation.OrderEntity] = order;
        }

        private static ReservationCancelReason MapCancelReason(ShipmentFailureReason failureReason)
        {
            switch (failureReason)
            {
                case ShipmentFailureReason.Cancelled:
                    return ReservationCancelReason.Cancelled;
                case ShipmentFailureReason.InvalidSource:
                case ShipmentFailureReason.InvalidDestination:
                case ShipmentFailureReason.InvalidContainer:
                    return ReservationCancelReason.InvalidTarget;
                case ShipmentFailureReason.TransportLost:
                    return ReservationCancelReason.TransportLost;
                case ShipmentFailureReason.NoInventory:
                    return ReservationCancelReason.NoInventory;
                case ShipmentFailureReason.NoCapacity:
                case ShipmentFailureReason.CapacityFull:
                    return ReservationCancelReason.NoCapacity;
                case ShipmentFailureReason.ReservationExpired:
                    return ReservationCancelReason.Timeout;
                default:
                    return ReservationCancelReason.Cancelled;
            }
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

