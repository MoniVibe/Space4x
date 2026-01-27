using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Components
{
    public struct InventoryHandle
    {
        public Entity StorehouseEntity;
        public ushort ResourceTypeIndex;
    }

    public struct ContainerHandle
    {
        public Entity ContainerEntity;
        public int BufferIndex;
    }

    /// <summary>
    /// Extended logistics order with routing and reservation support.
    /// Extends basic LogisticsJob with additional fields.
    /// </summary>
    public struct LogisticsOrder : IComponentData
    {
        public int OrderId;
        public LogisticsJobKind Kind;
        public Entity SourceNode;
        public Entity DestinationNode;
        public FixedString64Bytes ResourceId;
        public ushort ResourceTypeIndex;
        public InventoryHandle SourceInventory;
        public InventoryHandle DestinationInventory;
        public ContainerHandle ContainerHandle;
        public float RequestedAmount;
        public float ReservedAmount;  // Amount currently reserved
        public LogisticsOrderStatus Status;
        public ShipmentFailureReason FailureReason;
        public Entity AssignedTransport;
        public Entity ShipmentEntity;
        public uint CreatedTick;
        public uint EarliestDepartTick;
        public uint LatestArrivalTick;
        public byte Priority;
        public RouteConstraints Constraints;
    }

    public enum LogisticsOrderStatus : byte
    {
        Created = 0,
        Planning = 1,
        Reserved = 2,
        Dispatched = 3,
        InTransit = 4,
        Delivered = 5,
        Failed = 6,
        Cancelled = 7
    }

    public struct RouteConstraints : IComponentData
    {
        public float MaxRisk;
        public float MaxRouteLength;
        public float MaxCost;
        public byte LegalityFlags;
        public byte SecrecyFlags;
        public FixedList64Bytes<ServiceType> RequiredServices;
    }

    /// <summary>
    /// Shipment tracking component.
    /// Created from logistics order when transport is assigned.
    /// </summary>
    public struct Shipment : IComponentData
    {
        public int ShipmentId;
        public Entity AssignedTransport;
        public Entity RouteEntity;
        public ShipmentStatus Status;
        public ShipmentFailureReason FailureReason;
        public ShipmentRepresentationMode RepresentationMode;
        public ushort ResourceTypeIndex;
        public ContainerHandle ContainerHandle;
        public float AllocatedMass;
        public float AllocatedVolume;
        public uint DepartureTick;
        public uint EstimatedArrivalTick;
        public uint ActualArrivalTick;
    }

    public enum ShipmentStatus : byte
    {
        Created = 0,
        Loading = 1,
        InTransit = 2,
        Unloading = 3,
        Delivered = 4,
        Failed = 5,
        Rerouting = 6
    }

    public enum ShipmentFailureReason : byte
    {
        None = 0,
        InvalidSource = 1,
        InvalidDestination = 2,
        NoInventory = 3,
        NoCapacity = 4,
        StorageFull = 5,
        ReservationExpired = 6,
        TransportLost = 7,
        Cancelled = 8,
        RouteUnavailable = 9,
        NoCarrier = 10,
        InvalidContainer = 11,
        CapacityFull = 12
    }

    public enum ShipmentRepresentationMode : byte
    {
        Physical = 0,  // Entity exists
        Abstract = 1   // May materialize on triggers
    }

    [InternalBufferCapacity(8)]
    public struct ShipmentOrderRef : IBufferElementData
    {
        public Entity OrderEntity;
        public float AllocatedAmount;
    }

    [InternalBufferCapacity(16)]
    public struct ShipmentCargoAllocation : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public ushort ResourceTypeIndex;
        public Entity ContainerEntity;
        public ContainerHandle ContainerHandle;
        public float AllocatedAmount;
        public Entity BatchEntity;  // BatchId reference
    }

    /// <summary>
    /// Logistics node component.
    /// Represents a location that can send/receive resources.
    /// </summary>
    public struct LogisticsNode : IComponentData
    {
        public int NodeId;
        public NodeKind Kind;
        public Entity OwnerFaction;
        public float3 Position;
        public int SpatialCellId;
        public NodeServices Services;
    }

    public enum NodeKind : byte
    {
        TileCell = 0,
        District = 1,
        Settlement = 2,
        Station = 3,
        Warehouse = 4,
        MobileTransport = 5,
        EntityInventory = 6
    }

    public struct NodeServices : IComponentData
    {
        public byte ServiceFlags;  // Bitmask of available services
        public byte SlotCapacity;
        public float ThroughputBudget;
        public int QueuePolicyId;
    }

    [InternalBufferCapacity(8)]
    public struct NodeContainerRef : IBufferElementData
    {
        public Entity ContainerEntity;
        public float CapacityMass;
        public float CapacityVolume;
    }

    /// <summary>
    /// Logistics container component.
    /// Represents storage capacity at a node.
    /// </summary>
    public struct LogisticsContainer : IComponentData
    {
        public int ContainerId;
        public Entity ParentNode;
        public ContainerType ContainerType;
        public float CapacityMass;
        public float CapacityVolume;
        public int SlotCount;
        public byte AllowedTagMask;
        public int MixingPolicyId;
        public byte SpecialFacilityFlags;
        public float LoadRate;
        public float UnloadRate;
    }

    public enum ContainerType : byte
    {
        Generic = 0,
        Refrigerated = 1,
        Hazardous = 2,
        Liquid = 3,
        Bulk = 4,
        Containerized = 5
    }

    public enum ServiceType : byte
    {
        Dock = 0,
        Load = 1,
        Unload = 2,
        Customs = 3,
        Refuel = 4,
        Repair = 5,
        GateJump = 6
    }
}

