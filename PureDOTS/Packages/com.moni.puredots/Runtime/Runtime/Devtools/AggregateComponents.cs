#if DEVTOOLS_ENABLED
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Devtools
{
    /// <summary>
    /// Formation type for aggregate spawns.
    /// </summary>
    public enum FormationType : byte
    {
        Point = 0,
        Circle = 1,
        Grid = 2,
        Line = 3
    }

    /// <summary>
    /// Formation configuration for aggregate groups.
    /// </summary>
    public struct Formation : IComponentData
    {
        public FormationType Type;
        public float Spacing;
    }

    /// <summary>
    /// Aggregate preset member entry (blob asset).
    /// </summary>
    public struct AggregateMemberEntry
    {
        public int PrototypeId;
        public int MinCount;
        public int MaxCount;
        public PrototypeStatsDefault StatsOverrides;
        public Alignment AlignmentOverride; // Optional, use invalid value if not set
        public Outlook OutlookOverride; // Optional, use invalid value if not set
    }

    /// <summary>
    /// Aggregate preset blob asset (created from ScriptableObject).
    /// </summary>
    public struct AggregatePresetBlob
    {
        public FixedString128Bytes Name;
        public FormationType FormationType;
        public float FormationSpacing;
        public BlobArray<AggregateMemberEntry> Members;
    }

    /// <summary>
    /// Aggregate spawn request.
    /// </summary>
    public struct AggregateSpawnRequest : IComponentData
    {
        public int AggregatePresetId;
        public int TotalCount; // Optional; if 0, sum of member MinMax picks
        public float3 Position;
        public quaternion Rotation;
        public uint Seed;
        public SpawnFlags Flags;
        public byte OwnerPlayerId;
    }

    /// <summary>
    /// Aggregate group header entity.
    /// </summary>
    public struct AggregateGroup : IComponentData
    {
        public int AggregatePresetId;
        public byte OwnerPlayerId;
    }

    /// <summary>
    /// Buffer of member entities in an aggregate group.
    /// </summary>
    public struct AggregateMembers : IBufferElementData
    {
        public Entity Member;
    }

    /// <summary>
    /// Component storing aggregate preset blob reference (authoring component creates this).
    /// </summary>
    public struct AggregatePresetBlobReference : IComponentData
    {
        public BlobAssetReference<AggregatePresetBlob> Blob;
    }
}
#endif

