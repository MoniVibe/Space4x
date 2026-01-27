using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Service contract component for tracking employment agreements.
    /// </summary>
    public struct ServiceContract : IComponentData
    {
        public FixedString64Bytes EmployerId;
        public byte Type;
        public uint StartTick;
        public uint DurationTicks;
        public uint ExpiryTick;
        public byte IsActive;
    }

    /// <summary>
    /// Stat display binding for presentation layer.
    /// Allows HUDs to subscribe to stat data.
    /// </summary>
    public struct StatDisplayBinding : IComponentData
    {
        public FixedString64Bytes EntityId;
        public byte Mode;
        public byte VisibleStatsMask;
    }
}
