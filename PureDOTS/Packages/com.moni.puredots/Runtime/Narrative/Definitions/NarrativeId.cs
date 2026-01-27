using System;
using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Unique identifier for a narrative event, situation, or arc.
    /// Uses integer hash of string ID for Burst-safe lookups.
    /// </summary>
    public struct NarrativeId : IEquatable<NarrativeId>, IComponentData
    {
        public int Value;

        public readonly bool IsValid => Value != 0;

        public static NarrativeId FromString(string id)
        {
            if (string.IsNullOrEmpty(id))
                return default;

            return new NarrativeId { Value = id.GetHashCode() };
        }

        public readonly bool Equals(NarrativeId other) => Value == other.Value;
        public override readonly int GetHashCode() => Value;
        public override readonly bool Equals(object obj) => obj is NarrativeId other && Equals(other);
    }
}

