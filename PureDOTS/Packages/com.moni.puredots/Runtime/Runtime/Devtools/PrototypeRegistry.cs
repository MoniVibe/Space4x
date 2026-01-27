using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Devtools
{
    /// <summary>
    /// Stable hash-based prototype identifier.
    /// </summary>
    public struct PrototypeId : IComponentData
    {
        public int Value;

        public static PrototypeId FromString(string name)
        {
            return new PrototypeId { Value = name.GetHashCode() };
        }
    }

    /// <summary>
    /// Link component on prefab entities, storing their PrototypeId.
    /// </summary>
    public struct PrototypeLink : IComponentData
    {
        public int PrototypeId;
    }

    /// <summary>
    /// Default stats for a prototype (health, speed, etc.).
    /// </summary>
    public struct PrototypeStatsDefault : IComponentData
    {
        public float Health;
        public float Speed;
        public float Mass;
        public float Damage;
        public float Range;
        // Extend as needed
    }

    /// <summary>
    /// Default alignment for a prototype (Friendly|Neutral|Hostile).
    /// </summary>
    public struct Alignment : IComponentData
    {
        public byte Value; // 0=Friendly, 1=Neutral, 2=Hostile

        public static Alignment Friendly => new Alignment { Value = 0 };
        public static Alignment Neutral => new Alignment { Value = 1 };
        public static Alignment Hostile => new Alignment { Value = 2 };
    }

    /// <summary>
    /// Default outlook/temperament for a prototype.
    /// </summary>
    public struct Outlook : IComponentData
    {
        public byte Value; // Theme-specific enum values

        public static Outlook Default => new Outlook { Value = 0 };
    }

    /// <summary>
    /// Blob asset entry mapping PrototypeId to prefab reference.
    /// </summary>
    public struct PrototypeEntry
    {
        public int PrototypeId;
        public Entity PrefabEntity; // Runtime entity reference (set during conversion)
        public FixedString128Bytes Name;
        public PrototypeStatsDefault StatsDefault;
        public Alignment AlignmentDefault;
        public Outlook OutlookDefault;
    }

    /// <summary>
    /// Singleton blob asset reference containing all prototype mappings.
    /// </summary>
    public struct PrototypeRegistryBlob : IComponentData
    {
        public BlobAssetReference<BlobArray<PrototypeEntry>> Entries;
    }
}























