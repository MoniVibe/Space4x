using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Components
{
    /// <summary>
    /// Hyperway schedule mode.
    /// </summary>
    public enum HyperwayScheduleMode : byte
    {
        Interval = 0, // depart every N ticks
        ValueThreshold = 1, // depart when aggregated cargo/passenger value >= X
        Manual = 2 // relay captain / AI triggers departure
    }

    /// <summary>
    /// Hyperway service definition component.
    /// Defines schedule and pricing for a link service.
    /// </summary>
    public struct HyperwayServiceDef : IComponentData
    {
        public int LinkId;
        public HyperwayScheduleMode Mode;
        public uint IntervalTicks; // used if Interval
        public float ValueThreshold; // used if ValueThreshold
        public float BaseTicketPricePerMass;
        public float BaseTicketPricePerPassenger;
    }

    /// <summary>
    /// Warp relay service state component.
    /// Runtime state for a service on a link.
    /// </summary>
    public struct WarpRelayServiceState : IComponentData
    {
        public int LinkId;
        public uint LastDepartureTick;
        public float QueuedValue; // value of queued bookings
    }

    /// <summary>
    /// Warp relay queue element buffer.
    /// Tracks bookings queued at a node.
    /// </summary>
    public struct WarpRelayQueueElement : IBufferElementData
    {
        public int BookingId;
    }
}

