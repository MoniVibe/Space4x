using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen
{
    public static class WorldGenHashing
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const ulong HashSeedB = 0x9e3779b97f4a7c15UL;

        public static void ComputeOutputHash(
            NativeArray<ResourceNodeSpawn> resources,
            NativeArray<VillageSpawn> villages,
            NativeArray<StarterCacheSpawn> caches,
            out ulong hashLo,
            out ulong hashHi)
        {
            resources.Sort(new ResourceNodeSpawnComparer());
            villages.Sort(new VillageSpawnComparer());
            caches.Sort(new StarterCacheSpawnComparer());

            hashLo = FnvOffsetBasis;
            hashHi = FnvOffsetBasis ^ HashSeedB;

            AppendListHeader(ref hashLo, ref hashHi, 1, resources.Length);
            for (int i = 0; i < resources.Length; i++)
            {
                Append(ref hashLo, ref hashHi, (ulong)resources[i].ResourceType);
                Append(ref hashLo, ref hashHi, unchecked((uint)resources[i].Cell.x));
                Append(ref hashLo, ref hashHi, unchecked((uint)resources[i].Cell.y));
                Append(ref hashLo, ref hashHi, resources[i].LocalOffsetQx);
                Append(ref hashLo, ref hashHi, resources[i].LocalOffsetQy);
                Append(ref hashLo, ref hashHi, resources[i].RichnessQ);
                Append(ref hashLo, ref hashHi, resources[i].ClusterId);
            }

            AppendListHeader(ref hashLo, ref hashHi, 2, villages.Length);
            for (int i = 0; i < villages.Length; i++)
            {
                Append(ref hashLo, ref hashHi, unchecked((uint)villages[i].Cell.x));
                Append(ref hashLo, ref hashHi, unchecked((uint)villages[i].Cell.y));
                Append(ref hashLo, ref hashHi, villages[i].LocalOffsetQx);
                Append(ref hashLo, ref hashHi, villages[i].LocalOffsetQy);
                Append(ref hashLo, ref hashHi, (ulong)villages[i].BiomeId);
                Append(ref hashLo, ref hashHi, villages[i].VillageSeed);
            }

            AppendListHeader(ref hashLo, ref hashHi, 3, caches.Length);
            for (int i = 0; i < caches.Length; i++)
            {
                Append(ref hashLo, ref hashHi, unchecked((uint)caches[i].VillageIndex));
                Append(ref hashLo, ref hashHi, (ulong)caches[i].CacheType);
                Append(ref hashLo, ref hashHi, caches[i].FoodQ);
                Append(ref hashLo, ref hashHi, caches[i].WoodQ);
                Append(ref hashLo, ref hashHi, caches[i].StoneQ);
            }
        }

        private static void AppendListHeader(ref ulong hashLo, ref ulong hashHi, uint listId, int length)
        {
            Append(ref hashLo, ref hashHi, listId);
            Append(ref hashLo, ref hashHi, unchecked((uint)length));
        }

        private static void Append(ref ulong hashLo, ref ulong hashHi, uint value)
        {
            Append(ref hashLo, ref hashHi, (ulong)value);
        }

        private static void Append(ref ulong hashLo, ref ulong hashHi, ushort value)
        {
            Append(ref hashLo, ref hashHi, (ulong)value);
        }

        private static void Append(ref ulong hashLo, ref ulong hashHi, ulong value)
        {
            hashLo ^= value;
            hashLo *= FnvPrime;
            hashHi ^= value;
            hashHi *= FnvPrime;
        }

        private struct ResourceNodeSpawnComparer : IComparer<ResourceNodeSpawn>
        {
            public int Compare(ResourceNodeSpawn x, ResourceNodeSpawn y)
            {
                int cmp = x.ResourceType.CompareTo(y.ResourceType);
                if (cmp != 0) return cmp;
                cmp = x.Cell.x.CompareTo(y.Cell.x);
                if (cmp != 0) return cmp;
                cmp = x.Cell.y.CompareTo(y.Cell.y);
                if (cmp != 0) return cmp;
                cmp = x.LocalOffsetQx.CompareTo(y.LocalOffsetQx);
                if (cmp != 0) return cmp;
                cmp = x.LocalOffsetQy.CompareTo(y.LocalOffsetQy);
                if (cmp != 0) return cmp;
                cmp = x.RichnessQ.CompareTo(y.RichnessQ);
                if (cmp != 0) return cmp;
                return x.ClusterId.CompareTo(y.ClusterId);
            }
        }

        private struct VillageSpawnComparer : IComparer<VillageSpawn>
        {
            public int Compare(VillageSpawn x, VillageSpawn y)
            {
                int cmp = x.Cell.x.CompareTo(y.Cell.x);
                if (cmp != 0) return cmp;
                cmp = x.Cell.y.CompareTo(y.Cell.y);
                if (cmp != 0) return cmp;
                cmp = x.LocalOffsetQx.CompareTo(y.LocalOffsetQx);
                if (cmp != 0) return cmp;
                cmp = x.LocalOffsetQy.CompareTo(y.LocalOffsetQy);
                if (cmp != 0) return cmp;
                cmp = x.BiomeId.CompareTo(y.BiomeId);
                if (cmp != 0) return cmp;
                return x.VillageSeed.CompareTo(y.VillageSeed);
            }
        }

        private struct StarterCacheSpawnComparer : IComparer<StarterCacheSpawn>
        {
            public int Compare(StarterCacheSpawn x, StarterCacheSpawn y)
            {
                int cmp = x.VillageIndex.CompareTo(y.VillageIndex);
                if (cmp != 0) return cmp;
                cmp = x.CacheType.CompareTo(y.CacheType);
                if (cmp != 0) return cmp;
                cmp = x.FoodQ.CompareTo(y.FoodQ);
                if (cmp != 0) return cmp;
                cmp = x.WoodQ.CompareTo(y.WoodQ);
                if (cmp != 0) return cmp;
                return x.StoneQ.CompareTo(y.StoneQ);
            }
        }
    }
}
