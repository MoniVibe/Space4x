using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Components
{
    /// <summary>
    /// Inventory reservation component.
    /// Reserves inventory at source nodes when order created.
    /// </summary>
    public struct InventoryReservation : IComponentData
    {
        public int ReservationId;
        public Entity SourceNode;
        public Entity ContainerEntity;
        public FixedString64Bytes ResourceId;
        public float ReservedAmount;
        public Entity OrderEntity;
        public ReservationStatus Status;
        public ReservationCancelReason CancelReason;
        public uint CreatedTick;
        public uint ExpiryTick;
        public byte ReservationFlags;
    }

    /// <summary>
    /// Capacity reservation component.
    /// Reserves transport capacity when shipment assigned.
    /// </summary>
    public struct CapacityReservation : IComponentData
    {
        public int ReservationId;
        public Entity TransportEntity;
        public Entity ContainerEntity;
        public FixedString64Bytes ResourceId;
        public float ReservedCapacity;
        public float ReservedMass;
        public float ReservedVolume;
        public Entity OrderEntity;
        public ReservationStatus Status;
        public ReservationCancelReason CancelReason;
        public uint CreatedTick;
        public uint ExpiryTick;
    }

    /// <summary>
    /// Service reservation component.
    /// Reserves service slots at nodes (docks, loaders).
    /// </summary>
    public struct ServiceReservation : IComponentData
    {
        public int ReservationId;
        public Entity ServiceNode;
        public ServiceType ServiceType;
        public Entity OrderEntity;
        public ReservationStatus Status;
        public ReservationCancelReason CancelReason;
        public uint ReservedSlotTime;
        public uint CreatedTick;
        public uint ExpiryTick;
    }

    public enum ReservationStatus : byte
    {
        Active = 0,
        Committed = 1,
        Released = 2,
        Expired = 3,
        Cancelled = 4
    }

    public enum ReservationCancelReason : byte
    {
        None = 0,
        Timeout = 1,
        Cancelled = 2,
        InvalidTarget = 3,
        TransportLost = 4,
        NoInventory = 5,
        NoCapacity = 6,
        Superseded = 7
    }

    public static class InventoryReservationFlags
    {
        public const byte ReservedApplied = 1 << 0;
        public const byte Withdrawn = 1 << 1;
    }

    /// <summary>
    /// Reservation policy configuration.
    /// </summary>
    public struct ReservationPolicy : IComponentData
    {
        public float DefaultTTLSeconds;  // Default reservation TTL
        public byte AllowPartialReservations;  // 0 = false, 1 = true
        public byte AutoCommitOnDispatch;  // 0 = false, 1 = true
        public byte CancelOnOrderCancel;  // 0 = false, 1 = true
    }
}

