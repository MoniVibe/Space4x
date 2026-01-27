using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Bitmask for narrative tags (64 tag slots).
    /// Tags are used for filtering and matching narrative content.
    /// </summary>
    public struct NarrativeTagMask : IComponentData
    {
        public ulong Bits;

        public static NarrativeTagMask None => default;

        public static NarrativeTagMask FromTag(int tagIndex)
        {
            if (tagIndex < 0 || tagIndex >= 64)
                return default;

            return new NarrativeTagMask { Bits = 1UL << tagIndex };
        }

        public readonly bool HasTag(int tagIndex)
        {
            if (tagIndex < 0 || tagIndex >= 64)
                return false;

            return (Bits & (1UL << tagIndex)) != 0;
        }

        public readonly bool HasAny(NarrativeTagMask other) => (Bits & other.Bits) != 0;
        public readonly bool HasAll(NarrativeTagMask other) => (Bits & other.Bits) == other.Bits;

        public static NarrativeTagMask operator |(NarrativeTagMask a, NarrativeTagMask b) =>
            new NarrativeTagMask { Bits = a.Bits | b.Bits };

        public static NarrativeTagMask operator &(NarrativeTagMask a, NarrativeTagMask b) =>
            new NarrativeTagMask { Bits = a.Bits & b.Bits };
    }
}

