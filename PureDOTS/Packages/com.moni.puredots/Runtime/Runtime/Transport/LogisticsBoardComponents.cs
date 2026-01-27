using Unity.Collections;
using Unity.Entities;
namespace PureDOTS.Runtime.Transport
{
    /// <summary>
    /// Marks an entity as a logistics board that aggregates demand and issues reservations.
    /// </summary>
    public struct LogisticsBoard : IComponentData
    {
        public FixedString64Bytes BoardId;
        public Entity AuthorityEntity;
        public Entity DomainEntity;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Contract-level demand ledger marker. Represents discoverable knowledge, not truth.
    /// </summary>
    public struct DemandLedger : IComponentData
    {
        public Entity ScopeEntity;
    }

    /// <summary>
    /// Contract-level dispatcher metadata for demand allocation.
    /// </summary>
    public struct TaskDispatcher : IComponentData
    {
        public Entity ScopeEntity;
        public uint LastDispatchTick;
        public int ActiveWorkers;
        public int AvailableWorkers;
    }

    /// <summary>
    /// Configuration tuning for logistics board behavior.
    /// </summary>
    public struct LogisticsBoardConfig : IComponentData
    {
        public float MinBatchUnits;
        public float MaxBatchUnits;
        public uint ReservationExpiryTicks;
        public uint BroadcastIntervalTicks;
        public byte MaxClaimsPerTick;

        public static LogisticsBoardConfig Default => new LogisticsBoardConfig
        {
            MinBatchUnits = 5f,
            MaxBatchUnits = 50f,
            ReservationExpiryTicks = 120,
            BroadcastIntervalTicks = 60,
            MaxClaimsPerTick = 4
        };
    }

    /// <summary>
    /// Aggregated demand entry (per site/resource).
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct LogisticsDemandEntry : IBufferElementData
    {
        public Entity SiteEntity;
        public ushort ResourceTypeIndex;
        public float RequiredUnits;
        public float DeliveredUnits;
        public float ReservedUnits;
        public float OutstandingUnits;
        public byte Priority;
        public uint LastUpdateTick;
        public uint ContextHash;
    }

    public enum LogisticsReservationStatus : byte
    {
        Pending = 0,
        Active = 1,
        Fulfilled = 2,
        Expired = 3,
        Cancelled = 4
    }

    /// <summary>
    /// Reservation entry issued by the board to avoid double hauling.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct LogisticsReservationEntry : IBufferElementData
    {
        public uint ReservationId;
        public Entity HaulerEntity;
        public Entity SiteEntity;
        public Entity SourceEntity;
        public ushort ResourceTypeIndex;
        public float Units;
        public uint CreatedTick;
        public uint ExpiryTick;
        public LogisticsReservationStatus Status;
    }

    /// <summary>
    /// Request from a hauler/agent asking the board for a reservation.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct LogisticsClaimRequest : IBufferElementData
    {
        public Entity Requester;
        public ushort ResourceTypeIndex;
        public float DesiredMinUnits;
        public float DesiredMaxUnits;
        public float CarryCapacity;
        public Entity SiteFilter;
        public uint RequestTick;
        public byte Priority;
    }
}
