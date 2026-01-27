using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Components
{
    /// <summary>
    /// Type of logistics job.
    /// </summary>
    public enum LogisticsJobKind : byte
    {
        Supply = 0, // move resource to unit/army/fleet/colony
        Delivery = 1, // deliver finished goods to market/customer
        Evacuation = 2, // move personnel/living cargo away
        RedeployStock = 3 // balance stock between depots
    }

    /// <summary>
    /// Destination mode for logistics jobs.
    /// </summary>
    public enum DestinationMode : byte
    {
        StaticLocation = 0, // coordinates / depot / village
        FollowEntity = 1, // follow current position
        Rendezvous = 2 // meet moving target at predicted point
    }

    /// <summary>
    /// Logistics job status.
    /// </summary>
    public enum LogisticsJobStatus : byte
    {
        Requested = 0,
        Assigned = 1,
        InTransit = 2,
        Completed = 3,
        Failed = 4,
        Cancelled = 5
    }

    /// <summary>
    /// Logistics job component.
    /// Represents a request to move cargo from origin to destination.
    /// </summary>
    public struct LogisticsJob : IComponentData
    {
        public int JobId;
        public LogisticsJobKind Kind;
        public Entity Origin; // depot, mine, factory, colony
        public Entity Destination; // depot, army, fleet, colony, etc.
        public DestinationMode DestMode;
        public FixedString64Bytes ResourceId; // -1 for generic / multi
        public float Amount;
        public uint EarliestDepartTick;
        public uint LatestArrivalTick; // soft constraint at first
        public byte Priority; // 0 = highest
        public LogisticsJobStatus Status;
    }
}

