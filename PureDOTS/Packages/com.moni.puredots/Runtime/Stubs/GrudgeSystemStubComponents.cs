// [TRI-STUB] Stub components for grudge system
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Grudge - grudge held by entity against another.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct Grudge : IBufferElementData
    {
        public Entity TargetEntity;
        public GrudgeType Type;
        public float Intensity;
        public uint CreatedTick;
        public byte IsResolved;
    }

    /// <summary>
    /// Grudge types.
    /// </summary>
    public enum GrudgeType : byte
    {
        Betrayal = 0,
        Murder = 1,
        Theft = 2,
        Insult = 3,
        Harm = 4
    }

    /// <summary>
    /// Grudge event - grudge-related event.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct GrudgeEvent : IBufferElementData
    {
        public Entity SourceEntity;
        public Entity TargetEntity;
        public GrudgeEventType EventType;
        public uint EventTick;
    }

    /// <summary>
    /// Grudge event types.
    /// </summary>
    public enum GrudgeEventType : byte
    {
        GrudgeCreated = 0,
        GrudgeResolved = 1,
        RevengeTaken = 2,
        GrudgeIntensified = 3
    }
}

