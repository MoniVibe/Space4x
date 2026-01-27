using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Orders
{
    public enum OrderStatus : byte
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3
    }

    public enum OrderEventType : byte
    {
        Started = 0,
        Completed = 1,
        Failed = 2
    }

    public enum OrderPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Lightweight order descriptor used by gameplay and scenario bootstraps.
    /// </summary>
    public struct Order : IComponentData
    {
        public FixedString64Bytes OrderType;
        public Entity Requester;
        public Entity Target;
        public OrderStatus Status;
        public OrderPriority Priority;
        public uint CreatedTick;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Append-only event stream for order lifecycle transitions (start/complete/fail).
    /// </summary>
    public struct OrderEvent : IBufferElementData
    {
        public Entity OrderEntity;
        public OrderEventType EventType;
        public FixedString64Bytes OrderType;
        public FixedString128Bytes Payload;
        public float Value;
        public uint Tick;
    }

    /// <summary>
    /// Singleton component tracking the order event stream state for HUD/telemetry.
    /// </summary>
    public struct OrderEventStream : IComponentData
    {
        public uint Version;
        public int EventCount;
        public int DroppedEvents;
        public uint LastWriteTick;
    }

    public struct OrderEventStreamConfig : IComponentData
    {
        public int MaxEvents;

        public static OrderEventStreamConfig CreateDefault(int maxEvents = 128)
        {
            return new OrderEventStreamConfig
            {
                MaxEvents = maxEvents <= 0 ? 128 : maxEvents
            };
        }
    }
}
