using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Components
{
    /// <summary>
    /// Hyperway network reference component.
    /// Attached to nodes and regions/systems that belong to a network.
    /// </summary>
    public struct HyperwayNetworkRef : IComponentData
    {
        public int NetworkId; // choose network if you have multiple
    }
}

