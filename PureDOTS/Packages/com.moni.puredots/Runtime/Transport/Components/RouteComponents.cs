using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Components
{
    /// <summary>
    /// Hyperway route element buffer.
    /// Represents a single node in the route path.
    /// </summary>
    public struct HyperwayRouteElement : IBufferElementData
    {
        public int NodeId;
    }

    /// <summary>
    /// Hyperway route component.
    /// Contains the planned path through the hyperway network.
    /// </summary>
    public struct HyperwayRoute : IComponentData
    {
        public int BookingId;
        public int CurrentIndex; // current position in route
    }
}

