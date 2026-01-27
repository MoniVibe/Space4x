using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Stable identifier for any entity (individuals, buildings, items, aggregates).
    /// Intended for save/load keys, deterministic RNG seeds, and cross-system correlation.
    /// </summary>
    public struct EntityStableId : IComponentData
    {
        public ulong Lo;
        public ulong Hi;

        public readonly bool IsValid => Lo != 0ul || Hi != 0ul;
    }

    /// <summary>
    /// Helpers for hashing stable ids into compact deterministic seeds.
    /// </summary>
    public static class EntityStableIdUtility
    {
        public static uint ToSeed32(in EntityStableId id, uint salt = 0u)
        {
            var lo0 = (uint)id.Lo;
            var lo1 = (uint)(id.Lo >> 32);
            var hi0 = (uint)id.Hi;
            var hi1 = (uint)(id.Hi >> 32);
            return math.hash(new uint4(lo0, lo1, hi0, hi1)) ^ salt;
        }
    }
}


