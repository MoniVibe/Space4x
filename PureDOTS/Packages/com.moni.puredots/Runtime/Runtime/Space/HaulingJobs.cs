using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    public enum HaulingJobPriority : byte
    {
        Low,
        Normal,
        High
    }

    public struct HaulingJob : IComponentData
    {
        public HaulingJobPriority Priority;
        public Entity SourceEntity;
        public Entity DestinationEntity;
        public float RequestedAmount;
        public float Urgency;
        public float ResourceValue;
    }

    [InternalBufferCapacity(32)]
    public struct HaulingJobQueueEntry : IBufferElementData
    {
        public HaulingJobPriority Priority;
        public Entity SourceEntity;
        public Entity DestinationEntity;
        public float RequestedAmount;
        public float Urgency;
        public float ResourceValue;
    }

    public struct HaulerRole : IComponentData
    {
        public byte IsDedicatedFreighter;
    }
}
