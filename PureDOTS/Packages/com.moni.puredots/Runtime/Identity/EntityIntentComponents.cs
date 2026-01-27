using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    public enum EntityIntentPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Metadata for an entity's intent queue (max entries, dequeue policy).
    /// </summary>
    public struct EntityIntentQueue : IComponentData
    {
        public byte Capacity;
        public byte PendingCount;
    }

    [InternalBufferCapacity(4)]
    public struct EntityIntent : IBufferElementData
    {
        public FixedString64Bytes IntentId;
        public Entity Target;
        public EntityIntentPriority Priority;
        public float Strength;
        public uint IssuedTick;
    }
}



