using System;
using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Burst-safe helper methods for querying registry buffers from jobs.
    /// </summary>
    [BurstCompile]
    public static class RegistryQueryHelpers
    {
        /// <summary>
        /// Finds all registry entries matching a filter predicate.
        /// </summary>
        [BurstCompile]
        public static void FindMatchingEntries<TEntry>(
            NativeArray<TEntry> entries,
            ref NativeList<TEntry> results,
            RegistryEntryPredicate<TEntry> filter)
            where TEntry : unmanaged, IBufferElementData, IRegistryEntry
        {
            results.Clear();
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (filter(ref entry))
                {
                    results.Add(entry);
                }
            }
        }

        /// <summary>
        /// Finds the nearest entry to a position.
        /// </summary>
        [BurstCompile]
        public static bool TryFindNearest<TEntry>(
            NativeArray<TEntry> entries,
            float3 position,
            out TEntry nearest,
            out float distanceSq)
            where TEntry : unmanaged, IBufferEntry, IRegistryEntry
        {
            nearest = default;
            distanceSq = float.MaxValue;
            bool found = false;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var entryPos = entry.GetPosition();
                var distSq = math.distancesq(position, entryPos);
                if (distSq < distanceSq)
                {
                    distanceSq = distSq;
                    nearest = entry;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Finds entries within a radius of a position.
        /// </summary>
        [BurstCompile]
        public static void FindWithinRadius<TEntry>(
            NativeArray<TEntry> entries,
            float3 position,
            float radius,
            ref NativeList<TEntry> results)
            where TEntry : unmanaged, IBufferEntry, IRegistryEntry
        {
            results.Clear();
            var radiusSq = radius * radius;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var entryPos = entry.GetPosition();
                var distSq = math.distancesq(position, entryPos);
                if (distSq <= radiusSq)
                {
                    results.Add(entry);
                }
            }
        }

        /// <summary>
        /// Counts entries matching a flag mask.
        /// </summary>
        [BurstCompile]
        public static int CountMatchingFlags<TEntry>(
            NativeArray<TEntry> entries,
            byte flagMask)
            where TEntry : unmanaged, IRegistryFlaggedEntry
        {
            int count = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if ((entries[i].RegistryFlags & flagMask) != 0)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Gets entries sorted by distance from a position.
        /// </summary>
        [BurstCompile]
        public static void GetSortedByDistance<TEntry>(
            NativeArray<TEntry> entries,
            float3 position,
            ref NativeList<RegistryDistanceEntry<TEntry>> results)
            where TEntry : unmanaged, IBufferEntry, IRegistryEntry
        {
            results.Clear();
            results.Capacity = math.max(results.Capacity, entries.Length);

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var entryPos = entry.GetPosition();
                var distSq = math.distancesq(position, entryPos);
                results.Add(new RegistryDistanceEntry<TEntry>
                {
                    Entry = entry,
                    DistanceSq = distSq
                });
            }

            // Sort by distance
            results.Sort(new RegistryDistanceComparer<TEntry>());
        }
    }

    /// <summary>
    /// Filter function for registry queries.
    /// </summary>
    public delegate bool RegistryEntryPredicate<TEntry>(ref TEntry entry)
        where TEntry : unmanaged, IBufferElementData, IRegistryEntry;

    /// <summary>
    /// Interface for registry entries that expose a position.
    /// </summary>
    public interface IBufferEntry
    {
        float3 GetPosition();
    }

    /// <summary>
    /// Extension methods for common registry entry types to implement IBufferEntry.
    /// </summary>
    public static class RegistryEntryExtensions
    {
        public static float3 GetPosition(this ResourceRegistryEntry entry) => entry.Position;
        public static float3 GetPosition(this StorehouseRegistryEntry entry) => entry.Position;
        public static float3 GetPosition(this VillagerRegistryEntry entry) => entry.Position;
        public static float3 GetPosition(this ConstructionRegistryEntry entry) => entry.Position;
        public static float3 GetPosition(this SpawnerRegistryEntry entry) => entry.Position;
        public static float3 GetPosition(this CreatureRegistryEntry entry) => entry.Position;
        public static float3 GetPosition(this BandRegistryEntry entry) => entry.Position;
    }

    /// <summary>
    /// Distance-sorted entry wrapper.
    /// </summary>
    public struct RegistryDistanceEntry<TEntry> : IComparable<RegistryDistanceEntry<TEntry>>
        where TEntry : unmanaged
    {
        public TEntry Entry;
        public float DistanceSq;

        public int CompareTo(RegistryDistanceEntry<TEntry> other)
        {
            return DistanceSq.CompareTo(other.DistanceSq);
        }
    }

    /// <summary>
    /// Comparer for distance-sorted entries.
    /// </summary>
    public struct RegistryDistanceComparer<TEntry> : System.Collections.Generic.IComparer<RegistryDistanceEntry<TEntry>>
        where TEntry : unmanaged
    {
        public int Compare(RegistryDistanceEntry<TEntry> x, RegistryDistanceEntry<TEntry> y)
        {
            return x.DistanceSq.CompareTo(y.DistanceSq);
        }
    }
}
