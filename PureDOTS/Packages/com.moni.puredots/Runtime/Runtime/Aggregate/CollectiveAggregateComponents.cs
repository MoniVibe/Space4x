using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Aggregate
{
    /// <summary>
    /// Lifecycle state for a reusable collective aggregate entity (villages, guilds, crews, dynasties, etc.).
    /// </summary>
    public enum CollectiveAggregateState : byte
    {
        Active = 0,
        Recovering = 1,
        Abandoned = 2,
        Corpse = 3
    }

    /// <summary>
    /// Feature flags describing which services are exposed by the aggregate.
    /// </summary>
    [System.Flags]
    public enum CollectiveAggregateFlags : byte
    {
        None = 0,
        HasWorkOrders = 1 << 0,
        HasHaulingNetwork = 1 << 1,
        HasConstructionOffice = 1 << 2,
        HasSocialVenues = 1 << 3,
        TracksHistory = 1 << 4
    }

    /// <summary>
    /// Core aggregate component shared by any collective (villages, crews, guilds, bands, armies, dynasties).
    /// </summary>
    public struct CollectiveAggregate : IComponentData
    {
        public Entity Owner;                 // Settlement, carrier, guild hall, dynasty root, etc.
        public Entity Anchor;                // Primary building, ship hull, flagship, keep
        public CollectiveAggregateState State;
        public CollectiveAggregateFlags Flags;
        public uint EstablishedTick;
        public uint LastStateChangeTick;
        public int MemberCount;
        public int BuildingCount;
        public int DependentStructureCount;
        public int PendingWorkOrders;
        public int PendingHaulingRoutes;
        public int PendingApprovals;
    }

    /// <summary>
    /// Defines how long a corpse/abandoned record should stay queryable before cleanup.
    /// </summary>
    public struct CollectiveAggregateCorpseWindow : IComponentData
    {
        public uint ExpireTick;
    }

    [System.Flags]
    public enum CollectiveAggregateMemberFlags : byte
    {
        None = 0,
        IsResident = 1 << 0,
        IsWorker = 1 << 1,
        IsCombatant = 1 << 2,
        IsLeadership = 1 << 3,
        IsHauler = 1 << 4,
        IsBuilder = 1 << 5
    }

    [InternalBufferCapacity(32)]
    public struct CollectiveAggregateMember : IBufferElementData
    {
        public Entity MemberEntity;
        public FixedString32Bytes RoleId;
        public CollectiveAggregateMemberFlags Flags;
        public uint JoinedTick;
        public uint LastSeenTick;
    }

    public enum CollectiveWorkOrderType : byte
    {
        Gather = 0,
        Deliver = 1,
        Construct = 2,
        Repair = 3,
        Craft = 4,
        Patrol = 5,
        Ritual = 6,
        Custom = 255
    }

    public enum CollectiveWorkOrderStatus : byte
    {
        Pending = 0,
        Assigned = 1,
        InProgress = 2,
        Completed = 3,
        Failed = 4,
        Cancelled = 5
    }

    [InternalBufferCapacity(32)]
    public struct CollectiveWorkOrder : IBufferElementData
    {
        public FixedString64Bytes WorkId;
        public CollectiveWorkOrderType Type;
        public CollectiveWorkOrderStatus Status;
        public Entity Target;
        public Entity RequestedBy;
        public float Priority;
        public float Quantity;
        public uint RequestedTick;
        public uint LastUpdateTick;
    }

    public enum CollectiveRouteState : byte
    {
        Idle = 0,
        Queued = 1,
        Dispatching = 2,
        InTransit = 3,
        Completed = 4,
        Failed = 5
    }

    [InternalBufferCapacity(16)]
    public struct CollectiveHaulingRoute : IBufferElementData
    {
        public Entity Source;
        public Entity Destination;
        public FixedString32Bytes ResourceId;
        public float ReservedAmount;
        public float AssignedAmount;
        public CollectiveRouteState State;
        public uint LastDispatchTick;
    }

    public enum CollectiveConstructionApprovalStatus : byte
    {
        Pending = 0,
        Approved = 1,
        Denied = 2,
        Deferred = 3,
        Cancelled = 4
    }

    [InternalBufferCapacity(16)]
    public struct CollectiveConstructionApproval : IBufferElementData
    {
        public FixedString64Bytes RequestId;
        public FixedString64Bytes BlueprintId;
        public Entity RequestedBy;
        public Entity TargetPlot;
        public float EstimatedCost;
        public float ApprovedBudget;
        public CollectiveConstructionApprovalStatus Status;
        public uint RequestedTick;
        public uint DecidedTick;
    }

    public enum CollectiveSocialVenueType : byte
    {
        Hearth = 0,
        Market = 1,
        Shrine = 2,
        Tavern = 3,
        TrainingYard = 4,
        CouncilHall = 5,
        Custom = 254
    }

    [InternalBufferCapacity(8)]
    public struct CollectiveSocialVenue : IBufferElementData
    {
        public FixedString32Bytes VenueId;
        public CollectiveSocialVenueType Type;
        public Entity Building;
        public byte Capacity;
        public byte Occupancy;
        public byte Priority;
        public uint LastActivityTick;
    }

    public enum CollectiveHistoryEventType : byte
    {
        Unknown = 0,
        Birth = 1,
        Death = 2,
        Migration = 3,
        BuildingPlaced = 4,
        BuildingLost = 5,
        WorkOrderCompleted = 6,
        Siege = 7,
        Abandoned = 8,
        Revived = 9
    }

    [InternalBufferCapacity(64)]
    public struct CollectiveAggregateHistoryEntry : IBufferElementData
    {
        public uint Tick;
        public CollectiveHistoryEventType EventType;
        public float Magnitude;
        public Entity RelatedEntity;
        public FixedString64Bytes Context;
    }
}



