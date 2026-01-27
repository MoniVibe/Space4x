using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Logistics.Components;
using static PureDOTS.Runtime.Logistics.Components.LogisticsJobKind;

namespace PureDOTS.Runtime.Logistics
{
    /// <summary>
    /// Service for managing resource logistics orders and shipments.
    /// </summary>
    [BurstCompile]
    public static class ResourceLogisticsService
    {
        /// <summary>
        /// Create a logistics order.
        /// </summary>
        [BurstCompile]
        public static void CreateOrder(
            in Entity sourceNode,
            in Entity destinationNode,
            in FixedString64Bytes resourceId,
            ushort resourceTypeIndex,
            float requestedAmount,
            LogisticsJobKind kind,
            uint createdTick,
            byte priority,
            out LogisticsOrder order)
        {
            order = new LogisticsOrder
            {
                SourceNode = sourceNode,
                DestinationNode = destinationNode,
                ResourceId = resourceId,
                ResourceTypeIndex = resourceTypeIndex,
                SourceInventory = new InventoryHandle
                {
                    StorehouseEntity = sourceNode,
                    ResourceTypeIndex = resourceTypeIndex
                },
                DestinationInventory = new InventoryHandle
                {
                    StorehouseEntity = destinationNode,
                    ResourceTypeIndex = resourceTypeIndex
                },
                ContainerHandle = default,
                RequestedAmount = requestedAmount,
                ReservedAmount = 0f,
                Kind = kind,
                Priority = priority,
                Status = LogisticsOrderStatus.Created,
                AssignedTransport = Entity.Null,
                ShipmentEntity = Entity.Null,
                CreatedTick = createdTick,
                EarliestDepartTick = createdTick,
                LatestArrivalTick = 0,
                Constraints = default
            };
        }

        /// <summary>
        /// Plan an order (calculate route, assign transport).
        /// </summary>
        [BurstCompile]
        public static void PlanOrder(
            ref LogisticsOrder order,
            in Entity transportEntity)
        {
            order.Status = LogisticsOrderStatus.Planning;
            order.AssignedTransport = transportEntity;
        }

        /// <summary>
        /// Consolidate multiple orders from same source/destination.
        /// </summary>
        [BurstCompile]
        public static void ConsolidateOrders(
            in NativeList<LogisticsOrder> orders,
            uint currentTick,
            out LogisticsOrder consolidated)
        {
            if (orders.Length == 0)
            {
                consolidated = default;
                return;
            }

            var first = orders[0];
            float totalRequested = 0f;
            for (int i = 0; i < orders.Length; i++)
            {
                totalRequested += orders[i].RequestedAmount;
            }

            first.RequestedAmount = totalRequested;
            first.ReservedAmount = 0f;
            first.Status = LogisticsOrderStatus.Planning;
            first.CreatedTick = currentTick;
            first.EarliestDepartTick = currentTick;
            first.LatestArrivalTick = 0;
            consolidated = first;
        }

        /// <summary>
        /// Assign transport to an order.
        /// </summary>
        [BurstCompile]
        public static void AssignTransport(
            ref LogisticsOrder order,
            in Entity transportEntity)
        {
            order.AssignedTransport = transportEntity;
            order.Status = LogisticsOrderStatus.Dispatched;
        }

        /// <summary>
        /// Create a shipment from an order.
        /// </summary>
        [BurstCompile]
        public static void CreateShipment(
            in LogisticsOrder order,
            in Entity routeEntity,
            int shipmentId,
            uint currentTick,
            float estimatedTransitTicks,
            out Shipment shipment)
        {
            var transit = (uint)math.max(1f, estimatedTransitTicks);
            shipment = new Shipment
            {
                ShipmentId = shipmentId,
                AssignedTransport = order.AssignedTransport,
                RouteEntity = routeEntity,
                Status = ShipmentStatus.InTransit,
                RepresentationMode = ShipmentRepresentationMode.Abstract,
                ResourceTypeIndex = order.ResourceTypeIndex,
                ContainerHandle = order.ContainerHandle,
                AllocatedMass = 0f,
                AllocatedVolume = 0f,
                DepartureTick = currentTick,
                EstimatedArrivalTick = currentTick + transit,
                ActualArrivalTick = 0
            };
        }

        /// <summary>
        /// Update shipment state.
        /// </summary>
        [BurstCompile]
        public static void UpdateShipmentState(
            ref Shipment shipment,
            ShipmentStatus status)
        {
            shipment.Status = status;
        }
    }
}
