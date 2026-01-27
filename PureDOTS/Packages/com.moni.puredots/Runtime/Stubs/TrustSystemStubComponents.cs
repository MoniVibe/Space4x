// [TRI-STUB] Stub components for trust system
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Trust level - trust score between entities.
    /// </summary>
    public struct TrustLevel : IComponentData
    {
        public Entity TrustedEntity;
        public float TrustScore;
        public float ReliabilityScore;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Trust event - event that affects trust.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct TrustEvent : IBufferElementData
    {
        public Entity SourceEntity;
        public Entity TargetEntity;
        public TrustEventType EventType;
        public float TrustDelta;
        public uint EventTick;
    }

    /// <summary>
    /// Trust event types.
    /// </summary>
    public enum TrustEventType : byte
    {
        PromiseKept = 0,
        PromiseBroken = 1,
        Betrayal = 2,
        ReliableAction = 3,
        UnreliableAction = 4
    }
}

