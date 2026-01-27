using Unity.Entities;

namespace PureDOTS.Runtime.Resources
{
    /// <summary>
    /// Tracks the next resource request identifier so receipts can map back to requests.
    /// Stored as a singleton so IDs stay unique per world and replay friendly.
    /// </summary>
    public struct ResourceRequestIdGenerator : IComponentData
    {
        public uint NextRequestId;
    }
}



