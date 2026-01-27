using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Knowledge
{
    public enum GroupKnowledgeClaimKind : byte
    {
        None = 0,
        ThreatSeen = 1,
        ResourceSeen = 2,
        StockpileLow = 3,
        ObjectiveSeen = 4,
        HazardSeen = 5,
        Rumor = 6,
        Custom0 = 240
    }

    public static class GroupKnowledgeFlags
    {
        public const byte Unreliable = 1 << 0;
        public const byte FromComms = 1 << 1;
        public const byte FromPerception = 1 << 2;
    }

    /// <summary>
    /// Runtime state for bounded group knowledge caches.
    /// </summary>
    public struct GroupKnowledgeCache : IComponentData
    {
        public uint LastUpdateTick;
        public uint LastPruneTick;
    }

    /// <summary>
    /// Configuration for group knowledge cache behavior.
    /// </summary>
    public struct GroupKnowledgeConfig : IComponentData
    {
        public int MaxEntries;
        public uint StaleAfterTicks;
        public float MinConfidence;
        public byte Enabled;

        public static GroupKnowledgeConfig Default => new GroupKnowledgeConfig
        {
            MaxEntries = 32,
            StaleAfterTicks = 900,
            MinConfidence = 0.25f,
            Enabled = 1
        };
    }

    [InternalBufferCapacity(16)]
    public struct GroupKnowledgeEntry : IBufferElementData
    {
        public GroupKnowledgeClaimKind Kind;
        public Entity Subject;
        public Entity Source;
        public float3 Position;
        public float Confidence;
        public uint LastSeenTick;
        public FixedString64Bytes PayloadId;
        public byte Flags;
    }

    /// <summary>
    /// Per-entity throttle state for emitting group knowledge updates.
    /// </summary>
    public struct GroupKnowledgeEmitterState : IComponentData
    {
        public uint LastEmitTick;
    }
}
