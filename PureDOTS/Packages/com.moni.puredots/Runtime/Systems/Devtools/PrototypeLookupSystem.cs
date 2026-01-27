#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Devtools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Devtools
{
    /// <summary>
    /// Provides O(1) lookup of prefab Entity from PrototypeId.
    /// </summary>
    [BurstCompile]
    public static class PrototypeLookup
    {
        /// <summary>
        /// Looks up prefab entity by PrototypeId.
        /// </summary>
        public static bool TryGetPrefab(BlobAssetReference<BlobArray<PrototypeRegistry.PrototypeEntry>> registry, int prototypeId, out Entity prefab)
        {
            if (!registry.IsCreated)
            {
                prefab = Entity.Null;
                return false;
            }

            var entries = registry.Value;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].PrototypeId == prototypeId)
                {
                    prefab = entries[i].PrefabEntity;
                    return true;
                }
            }

            prefab = Entity.Null;
            return false;
        }

        /// <summary>
        /// Gets default stats for a prototype.
        /// </summary>
        public static bool TryGetStatsDefault(BlobAssetReference<BlobArray<PrototypeRegistry.PrototypeEntry>> registry, int prototypeId, out PrototypeStatsDefault stats)
        {
            if (!registry.IsCreated)
            {
                stats = default;
                return false;
            }

            var entries = registry.Value;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].PrototypeId == prototypeId)
                {
                    stats = entries[i].StatsDefault;
                    return true;
                }
            }

            stats = default;
            return false;
        }

        /// <summary>
        /// Gets default alignment for a prototype.
        /// </summary>
        public static bool TryGetAlignmentDefault(BlobAssetReference<BlobArray<PrototypeRegistry.PrototypeEntry>> registry, int prototypeId, out Alignment alignment)
        {
            if (!registry.IsCreated)
            {
                alignment = Alignment.Neutral;
                return false;
            }

            var entries = registry.Value;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].PrototypeId == prototypeId)
                {
                    alignment = entries[i].AlignmentDefault;
                    return true;
                }
            }

            alignment = Alignment.Neutral;
            return false;
        }

        /// <summary>
        /// Gets default outlook for a prototype.
        /// </summary>
        public static bool TryGetOutlookDefault(BlobAssetReference<BlobArray<PrototypeRegistry.PrototypeEntry>> registry, int prototypeId, out Outlook outlook)
        {
            if (!registry.IsCreated)
            {
                outlook = Outlook.Default;
                return false;
            }

            var entries = registry.Value;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].PrototypeId == prototypeId)
                {
                    outlook = entries[i].OutlookDefault;
                    return true;
                }
            }

            outlook = Outlook.Default;
            return false;
        }
    }
}
#endif























