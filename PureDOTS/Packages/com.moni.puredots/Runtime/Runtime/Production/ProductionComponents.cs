using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Production
{
    public enum ProductionPolicyGoal : byte
    {
        MaximizeQuality = 0,
        MaximizeThroughput = 1,
        MaximizeSurvival = 2
    }

    public enum ProductionJobState : byte
    {
        Planned = 0,
        Allocated = 1,
        Executing = 2,
        Delivering = 3,
        Done = 4,
        Stalled = 5,
        Cancelled = 6
    }

    public enum ProductionJobStallReason : byte
    {
        None = 0,
        MissingInputs = 1,
        MissingCapacity = 2,
        MissingPower = 3,
        MissingSeats = 4,
        MissingStorage = 5,
        ReservationFailed = 6,
        OutputBlocked = 7
    }

    public enum ProductionOutputKind : byte
    {
        Primary = 0,
        Waste = 1,
        Byproduct = 2
    }

    public enum ProductionReservationStatus : byte
    {
        Active = 0,
        Released = 1,
        Expired = 2,
        Cancelled = 3,
        Consumed = 4
    }

    public struct ProductionFacility : IComponentData
    {
        public Entity InputStorage;
        public Entity OutputStorage;
        public byte LaneCapacity;
        public byte SeatCapacity;
        public float PowerCapacity;
        public float TechLevel01;
        public ProductionPolicyGoal PolicyGoal;
        public uint LastUpdateTick;
    }

    public struct ProductionFacilityUsage : IComponentData
    {
        public byte LanesInUse;
        public byte SeatsInUse;
        public float PowerInUse;
    }

    public struct ProductionJob : IComponentData
    {
        public int JobId;
        public FixedString64Bytes RecipeId;
        public Entity Facility;
        public Entity InputStorage;
        public Entity OutputStorage;
        public ProductionJobState State;
        public ProductionJobStallReason StallReason;
        public ProductionPolicyGoal PolicyGoal;
        public uint CreatedTick;
        public uint StartTick;
        public uint LastUpdateTick;
        public uint RemainingTicks;
        public uint TotalTicks;
        public byte RequiredLanes;
        public byte RequiredSeats;
        public float RequiredPower;
        public float BaseValue;
        public float Quality01;
        public byte Flags;
    }

    public static class ProductionJobFlags
    {
        public const byte UsageAllocated = 1 << 0;
        public const byte InputsReserved = 1 << 1;
    }

    [InternalBufferCapacity(16)]
    public struct ProductionJobQueueEntry : IBufferElementData
    {
        public Entity Job;
        public byte Priority;
        public uint EnqueuedTick;
    }

    [InternalBufferCapacity(8)]
    public struct ProductionJobInput : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float Amount;
        public ushort MinQuality;
    }

    [InternalBufferCapacity(8)]
    public struct ProductionJobOutput : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float Amount;
        public ProductionOutputKind Kind;
        public ushort AverageQuality;
    }

    [InternalBufferCapacity(8)]
    public struct ProductionInputReservation : IBufferElementData
    {
        public Entity Storage;
        public ushort ResourceTypeIndex;
        public float ReservedAmount;
        public uint ExpiryTick;
        public ProductionReservationStatus Status;
    }

    public struct ProductionReservationPolicy : IComponentData
    {
        public uint DefaultExpiryTicks;
        public byte AllowPartialReservations;
    }

    public struct ProductionJobScore : IComponentData
    {
        public float Score;
        public float BaseValue;
        public float DeliveredAmount;
        public float Quality01;
        public uint TimeCostTicks;
        public uint ScoredTick;
    }
}
