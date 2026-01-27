using Unity.Entities;

namespace PureDOTS.Runtime.Power
{
    /// <summary>
    /// Reference to a power network shared by all participants.
    /// </summary>
    public struct PowerNetworkRef : IComponentData
    {
        public int NetworkId;   // index into a PowerNetwork registry table
        public PowerDomain Domain;
    }

    /// <summary>
    /// Singleton component tracking all active power networks per domain.
    /// </summary>
    public struct PowerNetworkRegistry : IComponentData
    {
        // Network entities are tracked via PowerNetwork component + DynamicBuffer<PowerEdge>
        // This singleton exists for system queries and metadata
    }

    /// <summary>
    /// Component on the network entity itself, holding edge buffer.
    /// </summary>
    public struct PowerNetwork : IComponentData
    {
        public int NetworkId;
        public PowerDomain Domain;
    }
}

