using Unity.Entities;

namespace PureDOTS.Runtime.Transport
{
    /// <summary>
    /// Transport service component - defines schedule, capacity, and cost for transport entities.
    /// Attached to ferries, hyperways, warp relays, etc.
    /// </summary>
    public struct TransportService : IComponentData
    {
        /// <summary>
        /// Type of transport service.
        /// </summary>
        public TransportServiceType Type;

        /// <summary>
        /// Estimated travel time for this service.
        /// </summary>
        public float EstimatedTime;

        /// <summary>
        /// Estimated fuel/logistics cost.
        /// </summary>
        public float EstimatedFuel;

        /// <summary>
        /// Estimated risk level (0-1).
        /// </summary>
        public float EstimatedRisk;

        /// <summary>
        /// Base cost to use this service.
        /// </summary>
        public float BaseCost;

        /// <summary>
        /// Maximum capacity (passengers, cargo, etc.).
        /// </summary>
        public float MaxCapacity;

        /// <summary>
        /// Current usage/capacity.
        /// </summary>
        public float CurrentUsage;

        /// <summary>
        /// Whether service requires payment.
        /// </summary>
        public byte RequiresPayment;

        /// <summary>
        /// Schedule interval in ticks (0 = on-demand).
        /// </summary>
        public uint ScheduleIntervalTicks;

        /// <summary>
        /// Last departure tick.
        /// </summary>
        public uint LastDepartureTick;
    }

    /// <summary>
    /// Transport service type.
    /// </summary>
    public enum TransportServiceType : byte
    {
        Ferry = 0,
        Hyperway = 1,
        WarpRelay = 2,
        Airship = 3,
        Road = 4
    }
}






















