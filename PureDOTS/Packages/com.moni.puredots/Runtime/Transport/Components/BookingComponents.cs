using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Components
{
    /// <summary>
    /// Warp booking state.
    /// </summary>
    public enum WarpBookingState : byte
    {
        Requested = 0,
        QueuedAtOriginNode = 1,
        Loading = 2,
        InTransit = 3,
        Arrived = 4,
        Failed = 5
    }

    /// <summary>
    /// Warp booking component.
    /// Represents a request to travel via hyperway network.
    /// </summary>
    public struct WarpBooking : IComponentData
    {
        public int BookingId;
        public Entity Traveller; // ship, caravan, band, etc.
        public int OriginNodeId;
        public int DestinationNodeId;
        public int CurrentLinkId; // which link we're on
        public int NextNodeIndex; // step in route path
        public uint RequestedTick;
        public uint ExpectedDepartureTick;
        public uint ExpectedArrivalTick;
        public float TotalMass;
        public float TotalVolume;
        public float TotalValue;
        public WarpBookingState State;
    }
}

