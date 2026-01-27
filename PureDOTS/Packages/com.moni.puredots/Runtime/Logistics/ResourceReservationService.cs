using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics
{
    /// <summary>
    /// Static service class for resource reservation operations.
    /// Provides methods for reserving inventory, capacity, and services.
    /// </summary>
    [BurstCompile]
    public static class ResourceReservationService
    {
        /// <summary>
        /// Creates an inventory reservation at source node.
        /// </summary>
        public static InventoryReservation ReserveInventory(
            Entity sourceNode,
            Entity containerEntity,
            FixedString64Bytes resourceId,
            float amount,
            Entity orderEntity,
            uint currentTick,
            uint ttlTicks,
            int reservationId)
        {
            return new InventoryReservation
            {
                ReservationId = reservationId,
                SourceNode = sourceNode,
                ContainerEntity = containerEntity,
                ResourceId = resourceId,
                ReservedAmount = amount,
                OrderEntity = orderEntity,
                Status = ReservationStatus.Active,
                CancelReason = ReservationCancelReason.None,
                CreatedTick = currentTick,
                ExpiryTick = currentTick + ttlTicks,
                ReservationFlags = 0
            };
        }

        /// <summary>
        /// Creates a capacity reservation on transport.
        /// </summary>
        public static CapacityReservation ReserveCapacity(
            Entity transportEntity,
            Entity containerEntity,
            FixedString64Bytes resourceId,
            float capacity,
            float mass,
            float volume,
            Entity orderEntity,
            uint currentTick,
            uint ttlTicks,
            int reservationId)
        {
            return new CapacityReservation
            {
                ReservationId = reservationId,
                TransportEntity = transportEntity,
                ContainerEntity = containerEntity,
                ResourceId = resourceId,
                ReservedCapacity = capacity,
                ReservedMass = mass,
                ReservedVolume = volume,
                OrderEntity = orderEntity,
                Status = ReservationStatus.Active,
                CancelReason = ReservationCancelReason.None,
                CreatedTick = currentTick,
                ExpiryTick = currentTick + ttlTicks
            };
        }

        /// <summary>
        /// Creates a service reservation at service node.
        /// </summary>
        public static ServiceReservation ReserveService(
            Entity serviceNode,
            ServiceType serviceType,
            Entity orderEntity,
            uint slotTime,
            uint currentTick,
            uint ttlTicks,
            int reservationId)
        {
            return new ServiceReservation
            {
                ReservationId = reservationId,
                ServiceNode = serviceNode,
                ServiceType = serviceType,
                OrderEntity = orderEntity,
                Status = ReservationStatus.Active,
                CancelReason = ReservationCancelReason.None,
                ReservedSlotTime = slotTime,
                CreatedTick = currentTick,
                ExpiryTick = currentTick + ttlTicks
            };
        }

        /// <summary>
        /// Releases an inventory reservation.
        /// </summary>
        [BurstCompile]
        public static void ReleaseReservation(ref InventoryReservation reservation)
        {
            reservation.Status = ReservationStatus.Released;
        }

        /// <summary>
        /// Releases a capacity reservation.
        /// </summary>
        [BurstCompile]
        public static void ReleaseReservation(ref CapacityReservation reservation)
        {
            reservation.Status = ReservationStatus.Released;
        }

        /// <summary>
        /// Releases a service reservation.
        /// </summary>
        [BurstCompile]
        public static void ReleaseReservation(ref ServiceReservation reservation)
        {
            reservation.Status = ReservationStatus.Released;
        }

        public static void CancelReservation(ref InventoryReservation reservation, ReservationCancelReason reason)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.CancelReason = reason;
        }

        public static void CancelReservation(ref CapacityReservation reservation, ReservationCancelReason reason)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.CancelReason = reason;
        }

        public static void CancelReservation(ref ServiceReservation reservation, ReservationCancelReason reason)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.CancelReason = reason;
        }

        /// <summary>
        /// Checks if reservation is still valid.
        /// </summary>
        public static bool CheckReservationValidity(InventoryReservation reservation, uint currentTick)
        {
            return reservation.Status == ReservationStatus.Active &&
                   currentTick < reservation.ExpiryTick;
        }

        /// <summary>
        /// Checks if capacity reservation is still valid.
        /// </summary>
        public static bool CheckReservationValidity(CapacityReservation reservation, uint currentTick)
        {
            return reservation.Status == ReservationStatus.Active &&
                   currentTick < reservation.ExpiryTick;
        }

        /// <summary>
        /// Checks if service reservation is still valid.
        /// </summary>
        public static bool CheckReservationValidity(ServiceReservation reservation, uint currentTick)
        {
            return reservation.Status == ReservationStatus.Active &&
                   currentTick < reservation.ExpiryTick;
        }

        /// <summary>
        /// Cancels expired reservations (marks as expired).
        /// </summary>
        public static void CancelExpiredReservation(ref InventoryReservation reservation, uint currentTick)
        {
            if (reservation.Status == ReservationStatus.Active && currentTick >= reservation.ExpiryTick)
            {
                reservation.Status = ReservationStatus.Expired;
                reservation.CancelReason = ReservationCancelReason.Timeout;
            }
        }

        /// <summary>
        /// Cancels expired capacity reservation.
        /// </summary>
        public static void CancelExpiredReservation(ref CapacityReservation reservation, uint currentTick)
        {
            if (reservation.Status == ReservationStatus.Active && currentTick >= reservation.ExpiryTick)
            {
                reservation.Status = ReservationStatus.Expired;
                reservation.CancelReason = ReservationCancelReason.Timeout;
            }
        }

        /// <summary>
        /// Cancels expired service reservation.
        /// </summary>
        public static void CancelExpiredReservation(ref ServiceReservation reservation, uint currentTick)
        {
            if (reservation.Status == ReservationStatus.Active && currentTick >= reservation.ExpiryTick)
            {
                reservation.Status = ReservationStatus.Expired;
                reservation.CancelReason = ReservationCancelReason.Timeout;
            }
        }
    }
}

