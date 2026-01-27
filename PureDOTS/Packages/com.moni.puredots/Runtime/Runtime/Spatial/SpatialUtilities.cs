using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Spatial
{
    /// <summary>
    /// Utility methods for spatial hashing and coordinate transforms.
    /// </summary>
    [BurstCompile]
    public static class SpatialHash
    {
        [BurstCompile]
        public static void Quantize(in float3 position, in SpatialGridConfig config, out int3 cell)
        {
            var local = (position - config.WorldMin) / math.max(config.CellSize, 1e-3f);
            var maxCell = (float3)(config.CellCounts - 1);
            var wrapped = math.clamp(local, float3.zero, maxCell);
            cell = (int3)math.floor(wrapped + 1e-4f);
        }

        [BurstCompile]
        public static int Flatten(in int3 cell, in SpatialGridConfig config)
        {
            return cell.x * config.CellCounts.y * config.CellCounts.z
                + cell.y * config.CellCounts.z
                + cell.z;
        }

        [BurstCompile]
        public static void Unflatten(int cellId, in SpatialGridConfig config, out int3 cell)
        {
            if (cellId < 0)
            {
                cell = new int3(-1, -1, -1);
                return;
            }

            var yz = config.CellCounts.z;
            var xy = config.CellCounts.y * yz;
            if (xy <= 0 || yz <= 0)
            {
                cell = new int3(-1, -1, -1);
                return;
            }

            var x = cellId / xy;
            var remainder = cellId - x * xy;
            var y = remainder / yz;
            var z = remainder - y * yz;
            cell = new int3(x, y, z);
        }

        [BurstCompile]
        public static uint MortonKey(in int3 cell, uint seed = 0u)
        {
            var x = (uint)cell.x;
            var y = (uint)cell.y;
            var z = (uint)cell.z;

            x = Part1By2(x);
            y = Part1By2(y);
            z = Part1By2(z);

            var morton = x | (y << 1) | (z << 2);
            return morton ^ seed;
        }

        private static uint Part1By2(uint x)
        {
            x = (x | (x << 16)) & 0x030000FF;
            x = (x | (x << 8)) & 0x0300F00F;
            x = (x | (x << 4)) & 0x030C30C3;
            x = (x | (x << 2)) & 0x09249249;
            return x;
        }
    }

    /// <summary>
    /// Deterministic key representing a grid cell.
    /// </summary>
    public struct GridCellKey : System.IEquatable<GridCellKey>
    {
        public int3 Coordinates;
        public uint Hash;

        public GridCellKey(int3 coords, uint hash)
        {
            Coordinates = coords;
            Hash = hash;
        }

        public bool Equals(GridCellKey other)
        {
            return math.all(Coordinates == other.Coordinates) && Hash == other.Hash;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCellKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)math.hash(new uint4((uint)Coordinates.x, (uint)Coordinates.y, (uint)Coordinates.z, Hash));
        }
    }

    /// <summary>
    /// Generic filter contract used by spatial batch queries.
    /// </summary>
    public interface ISpatialQueryFilter
    {
        bool Accept(int descriptorIndex, in SpatialQueryDescriptor descriptor, in SpatialGridEntry entry);
    }

    /// <summary>
    /// Fallback filter that accepts all entities.
    /// </summary>
    public struct SpatialAcceptAllFilter : ISpatialQueryFilter
    {
        public bool Accept(int descriptorIndex, in SpatialQueryDescriptor descriptor, in SpatialGridEntry entry)
        {
            return true;
        }
    }

    /// <summary>
    /// Filter that limits results to a whitelist of entities.
    /// </summary>
    public struct SpatialWhitelistFilter : ISpatialQueryFilter
    {
        public NativeParallelHashSet<Entity> Whitelist;

        public bool Accept(int descriptorIndex, in SpatialQueryDescriptor descriptor, in SpatialGridEntry entry)
        {
            if (!Whitelist.IsCreated)
            {
                return true;
            }

            return Whitelist.Contains(entry.Entity);
        }
    }

    /// <summary>
    /// Burst-friendly helpers for common spatial queries.
    /// All vector and array parameters use ref for Burst compatibility.
    /// </summary>
    [BurstCompile]
    public static class SpatialQueryHelper
    {
        /// <summary>
        /// Gets all entities within a radius of a position.
        /// </summary>
        [BurstCompile]
        public static void GetEntitiesWithinRadius(
            ref float3 position,
            float radius,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<Entity> results)
        {
            CollectEntitiesInRadius(ref position, radius, config, ranges, entries, ref results);
        }

        /// <summary>
        /// Finds the nearest entity to a position, optionally filtered by EntityQuery criteria.
        /// Note: EntityQuery filtering must be done externally; this method searches all entities in the grid.
        /// </summary>
        [BurstCompile]
        public static bool FindNearestEntity(
            ref float3 position,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            out Entity nearest,
            out float distance)
        {
            if (TryFindClosest(ref position, config, ranges, entries, out nearest, out var distanceSq))
            {
                distance = math.sqrt(distanceSq);
                return true;
            }

            nearest = Entity.Null;
            distance = float.MaxValue;
            return false;
        }

        /// <summary>
        /// Gets all entities in a specific grid cell.
        /// </summary>
        [BurstCompile]
        public static void GetCellEntities(
            ref int3 cellCoords,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<Entity> results)
        {
            GetCellEntitiesInternal(ref cellCoords, config, ranges, entries, ref results);
        }

        /// <summary>
        /// Finds entities overlapping an axis-aligned bounding box.
        /// Note: Parameters use ref for Burst compatibility.
        /// </summary>
        [BurstCompile]
        public static void OverlapAABB(
            ref float3 min,
            ref float3 max,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<Entity> results)
        {
            OverlapAABBInternal(ref min, ref max, config, ranges, entries, ref results);
        }

        /// <summary>
        /// Finds k nearest entities within a radius, with optional filter.
        /// </summary>
        [BurstCompile]
        public static void FindKNearestInRadius<TFilter>(
            ref float3 position,
            float radius,
            int k,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<KNearestResult> results,
            in TFilter filter)
            where TFilter : struct, ISpatialQueryFilter
        {
            results.Clear();
            if (k <= 0 || radius <= 0f)
            {
                return;
            }

            var entryArray = entries.AsNativeArray();
            if (entryArray.Length == 0)
            {
                return;
            }

            var descriptor = new SpatialQueryDescriptor
            {
                Origin = position,
                Radius = radius,
                MaxResults = k,
                Options = SpatialQueryOptions.RequireDeterministicSorting,
                Tolerance = 1e-4f,
                ExcludedEntity = Entity.Null
            };

            var capacity = math.min(k, entryArray.Length);
            if (capacity <= 0)
            {
                return;
            }

            results.ResizeUninitialized(capacity);
            var slice = new NativeSlice<KNearestResult>(results.AsArray(), 0, capacity);
            var count = CollectKNearest(0, in descriptor, in config, ranges.AsNativeArray(), entryArray, slice, in filter);
            results.ResizeUninitialized(count);
        }

        /// <summary>
        /// Batch query for multiple radius searches from different origins.
        /// Returns results grouped by query index.
        /// </summary>
        [BurstCompile]
        public static void BatchRadiusQueries(
            ref NativeArray<float3> origins,
            ref NativeArray<float> radii,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<BatchRadiusResult> results)
        {
            results.Clear();
            if (!origins.IsCreated || !radii.IsCreated || origins.Length != radii.Length || origins.Length == 0)
            {
                return;
            }

            var entryArray = entries.AsNativeArray();
            if (entryArray.Length == 0)
            {
                return;
            }

            results.ResizeUninitialized(origins.Length);
            for (int i = 0; i < origins.Length; i++)
            {
                var queryResults = new NativeList<Entity>(64, Allocator.Temp);
                var origin = origins[i];
                CollectEntitiesInRadius(ref origin, radii[i], config, ranges, entries, ref queryResults);
                
                results[i] = new BatchRadiusResult
                {
                    QueryIndex = i,
                    Origin = origins[i],
                    Radius = radii[i],
                    ResultCount = queryResults.Length
                };
                
                queryResults.Dispose();
            }
        }

        public static bool TryGetCellSlice(in DynamicBuffer<SpatialGridCellRange> ranges, in DynamicBuffer<SpatialGridEntry> entries, int cellId, out NativeSlice<SpatialGridEntry> slice)
        {
            if ((uint)cellId >= ranges.Length)
            {
                slice = default;
                return false;
            }

            var range = ranges[cellId];
            if (range.Count <= 0)
            {
                slice = default;
                return false;
            }

            var entryArray = entries.AsNativeArray();
            slice = entryArray.Slice(range.StartIndex, range.Count);
            return true;
        }

        public static void CollectEntitiesInRadius(
            ref float3 position,
            float radius,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<Entity> results)
        {
            var descriptor = new SpatialQueryDescriptor
            {
                Origin = position,
                Radius = radius,
                MaxResults = int.MaxValue,
                Options = SpatialQueryOptions.RequireDeterministicSorting,
                Tolerance = 1e-4f,
                ExcludedEntity = Entity.Null
            };

            var filter = new SpatialAcceptAllFilter();
            CollectEntities(in descriptor, in config, in ranges, in entries, ref results, ref filter, 0);
        }

        public static void CollectEntitiesInRadiusFiltered(
            ref float3 position,
            float radius,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<Entity> results,
            NativeParallelHashSet<Entity> whitelist)
        {
            var descriptor = new SpatialQueryDescriptor
            {
                Origin = position,
                Radius = radius,
                MaxResults = int.MaxValue,
                Options = SpatialQueryOptions.RequireDeterministicSorting,
                Tolerance = 1e-4f,
                ExcludedEntity = Entity.Null
            };

            if (!whitelist.IsCreated)
            {
                var filterAll = new SpatialAcceptAllFilter();
                CollectEntities(in descriptor, in config, in ranges, in entries, ref results, ref filterAll, 0);
                return;
            }

            var filter = new SpatialWhitelistFilter
            {
                Whitelist = whitelist
            };
            CollectEntities(in descriptor, in config, in ranges, in entries, ref results, ref filter, 0);
        }

        public static void CollectEntities<TFilter>(
            in SpatialQueryDescriptor descriptor,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<Entity> results,
            ref TFilter filter,
            int descriptorIndex = 0)
            where TFilter : struct, ISpatialQueryFilter
        {
            var rangesArray = ranges.AsNativeArray();
            var entryArray = entries.AsNativeArray();
            if (rangesArray.Length == 0 || entryArray.Length == 0)
            {
                return;
            }

            var tolerance = math.max(1e-5f, descriptor.Tolerance);
            var radiusSq = descriptor.Radius > 0f
                ? descriptor.Radius * descriptor.Radius + tolerance
                : float.MaxValue;
            var limit = descriptor.MaxResults > 0 ? descriptor.MaxResults : int.MaxValue;
            var added = 0;
            var continueSearch = true;

            SpatialHash.Quantize(descriptor.Origin, config, out var cellCoords);
            var maxOffset = descriptor.Radius > 0f
                ? (int)math.ceil(descriptor.Radius / math.max(config.CellSize, 1e-3f))
                : math.max(math.max(config.CellCounts.x, config.CellCounts.y), config.CellCounts.z);

            for (var dx = -maxOffset; dx <= maxOffset && continueSearch; dx++)
            {
                for (var dy = -maxOffset; dy <= maxOffset && continueSearch; dy++)
                {
                    for (var dz = -maxOffset; dz <= maxOffset; dz++)
                    {
                        var neighbor = cellCoords + new int3(dx, dy, dz);
                        if (!IsWithinBounds(neighbor, config.CellCounts))
                        {
                            continue;
                        }

                        var cellId = SpatialHash.Flatten(in neighbor, in config);
                        if ((uint)cellId >= rangesArray.Length)
                        {
                            continue;
                        }

                        var range = rangesArray[cellId];
                        if (range.Count <= 0)
                        {
                            continue;
                        }

                        for (var i = 0; i < range.Count; i++)
                        {
                            var entryIndex = range.StartIndex + i;
                            if ((uint)entryIndex >= entryArray.Length)
                            {
                                continue;
                            }

                            var entry = entryArray[entryIndex];

                            if ((descriptor.Options & SpatialQueryOptions.IgnoreSelf) != 0 &&
                                entry.Entity == descriptor.ExcludedEntity)
                            {
                                continue;
                            }

                            if (!filter.Accept(descriptorIndex, in descriptor, in entry))
                            {
                                continue;
                            }

                            var distanceSq = ComputeDistanceSq(entry.Position, descriptor.Origin, in descriptor);
                            if (distanceSq > radiusSq)
                            {
                                continue;
                            }

                            results.Add(entry.Entity);
                            added++;

                            if (added >= limit)
                            {
                                continueSearch = false;
                                break;
                            }
                        }

                        if (!continueSearch)
                        {
                            break;
                        }
                    }
                }
            }

            if ((descriptor.Options & SpatialQueryOptions.RequireDeterministicSorting) != 0 && added > 0)
            {
                results.Sort(new EntityDeterministicComparer());
            }
        }

        private static void GetCellEntitiesInternal(
            ref int3 cellCoords,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<Entity> results)
        {
            if (!IsWithinBounds(cellCoords, config.CellCounts))
            {
                return;
            }

            var cellId = SpatialHash.Flatten(in cellCoords, in config);
            if ((uint)cellId >= ranges.Length)
            {
                return;
            }

            var range = ranges[cellId];
            if (range.Count <= 0)
            {
                return;
            }

            var entryArray = entries.AsNativeArray();
            for (var i = 0; i < range.Count; i++)
            {
                results.Add(entryArray[range.StartIndex + i].Entity);
            }
        }

        private static void OverlapAABBInternal(
            ref float3 aabbMin,
            ref float3 aabbMax,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<Entity> results)
        {
            SpatialHash.Quantize(in aabbMin, config, out var minCell);
            SpatialHash.Quantize(in aabbMax, config, out var maxCell);

            minCell = math.clamp(minCell, int3.zero, config.CellCounts - 1);
            maxCell = math.clamp(maxCell, int3.zero, config.CellCounts - 1);

            if (math.any(maxCell < minCell))
            {
                return;
            }

            var entryArray = entries.AsNativeArray();

            for (var x = minCell.x; x <= maxCell.x; x++)
            {
                for (var y = minCell.y; y <= maxCell.y; y++)
                {
                    for (var z = minCell.z; z <= maxCell.z; z++)
                    {
                        var coords = new int3(x, y, z);
                        var cellId = SpatialHash.Flatten(in coords, in config);
                        if ((uint)cellId >= ranges.Length)
                        {
                            continue;
                        }

                        var range = ranges[cellId];
                        if (range.Count <= 0)
                        {
                            continue;
                        }

                        for (var i = 0; i < range.Count; i++)
                        {
                            var entry = entryArray[range.StartIndex + i];
                            if (entry.Position.x < aabbMin.x || entry.Position.x > aabbMax.x ||
                                entry.Position.y < aabbMin.y || entry.Position.y > aabbMax.y ||
                                entry.Position.z < aabbMin.z || entry.Position.z > aabbMax.z)
                            {
                                continue;
                            }

                            results.Add(entry.Entity);
                        }
                    }
                }
            }

            results.Sort(new EntityDeterministicComparer());
        }

        public static bool TryFindClosest(
            ref float3 position,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            out Entity closestEntity,
            out float closestDistanceSq)
        {
            var maxOffset = 1;
            SpatialHash.Quantize(in position, config, out var cellCoords);
            var entryArray = entries.AsNativeArray();

            closestEntity = Entity.Null;
            closestDistanceSq = float.MaxValue;

            while (closestEntity == Entity.Null && maxOffset < math.max(config.CellCounts.x, math.max(config.CellCounts.y, config.CellCounts.z)))
            {
                var found = false;

                for (var dx = -maxOffset; dx <= maxOffset; dx++)
                {
                    for (var dy = -maxOffset; dy <= maxOffset; dy++)
                    {
                        for (var dz = -maxOffset; dz <= maxOffset; dz++)
                        {
                            var neighbor = cellCoords + new int3(dx, dy, dz);
                            if (!IsWithinBounds(neighbor, config.CellCounts))
                            {
                                continue;
                            }

                            var cellId = SpatialHash.Flatten(in neighbor, in config);
                            if ((uint)cellId >= ranges.Length)
                            {
                                continue;
                            }

                            var range = ranges[cellId];
                            for (var i = 0; i < range.Count; i++)
                            {
                                var entry = entryArray[range.StartIndex + i];
                                var distSq = math.lengthsq(entry.Position - position);
                                if (distSq < closestDistanceSq)
                                {
                                    closestDistanceSq = distSq;
                                    closestEntity = entry.Entity;
                                    found = true;
                                }
                            }
                        }
                    }
                }

                if (found)
                {
                    return true;
                }

                maxOffset++;
            }

            return closestEntity != Entity.Null;
        }

        public static int CollectKNearest<TFilter>(
            int descriptorIndex,
            in SpatialQueryDescriptor descriptor,
            in SpatialGridConfig config,
            NativeArray<SpatialGridCellRange> ranges,
            NativeArray<SpatialGridEntry> entries,
            NativeSlice<KNearestResult> results,
            in TFilter filter)
            where TFilter : struct, ISpatialQueryFilter
        {
            if (!ranges.IsCreated || !entries.IsCreated || results.Length == 0)
            {
                return 0;
            }

            if (ranges.Length == 0 || entries.Length == 0)
            {
                return 0;
            }

            var capacity = descriptor.MaxResults > 0
                ? math.min(descriptor.MaxResults, results.Length)
                : results.Length;

            if (capacity <= 0)
            {
                return 0;
            }

            var options = descriptor.Options;
            var tolerance = math.max(1e-5f, descriptor.Tolerance);
            var radius = descriptor.Radius;
            var radiusSq = radius > 0f && !float.IsInfinity(radius)
                ? radius * radius + tolerance
                : float.MaxValue;

            var maxOffset = radius > 0f && !float.IsInfinity(radius)
                ? (int)math.ceil(radius / math.max(config.CellSize, 1e-3f))
                : math.max(math.max(config.CellCounts.x, config.CellCounts.y), config.CellCounts.z);

            var count = 0;

            if (radiusSq >= float.MaxValue * 0.5f)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if ((options & SpatialQueryOptions.IgnoreSelf) != 0 && entry.Entity == descriptor.ExcludedEntity)
                    {
                        continue;
                    }

                    if (!filter.Accept(descriptorIndex, in descriptor, in entry))
                    {
                        continue;
                    }

                    var distanceSq = ComputeDistanceSq(entry.Position, descriptor.Origin, in descriptor);
                    InsertKNearest(results, ref count, capacity, entry.Entity, distanceSq);
                }

                return math.min(count, capacity);
            }

            SpatialHash.Quantize(descriptor.Origin, config, out var cellCoords);
            var maxCellSpan = math.max(math.max(config.CellCounts.x, config.CellCounts.y), config.CellCounts.z);
            maxOffset = math.min(maxOffset, maxCellSpan);

            for (var dx = -maxOffset; dx <= maxOffset; dx++)
            {
                for (var dy = -maxOffset; dy <= maxOffset; dy++)
                {
                    for (var dz = -maxOffset; dz <= maxOffset; dz++)
                    {
                        var neighbor = cellCoords + new int3(dx, dy, dz);
                        if (!IsWithinBounds(neighbor, config.CellCounts))
                        {
                            continue;
                        }

                        var cellId = SpatialHash.Flatten(in neighbor, in config);
                        if ((uint)cellId >= ranges.Length)
                        {
                            continue;
                        }

                        var range = ranges[cellId];
                        if (range.Count <= 0)
                        {
                            continue;
                        }

                        for (var i = 0; i < range.Count; i++)
                        {
                            var entryIndex = range.StartIndex + i;
                            if ((uint)entryIndex >= entries.Length)
                            {
                                continue;
                            }

                            var entry = entries[entryIndex];
                            if ((options & SpatialQueryOptions.IgnoreSelf) != 0 && entry.Entity == descriptor.ExcludedEntity)
                            {
                                continue;
                            }

                            if (!filter.Accept(descriptorIndex, in descriptor, in entry))
                            {
                                continue;
                            }

                            var distanceSq = ComputeDistanceSq(entry.Position, descriptor.Origin, in descriptor);
                            if (distanceSq > radiusSq)
                            {
                                continue;
                            }

                            InsertKNearest(results, ref count, capacity, entry.Entity, distanceSq);
                        }
                    }
                }
            }

            return math.min(count, capacity);
        }

        public static void FindKNearest(
            float3 position,
            int k,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<KNearestResult> results)
        {
            results.Clear();
            if (k <= 0)
            {
                return;
            }

            var entryArray = entries.AsNativeArray();
            if (entryArray.Length == 0)
            {
                return;
            }

            var descriptor = new SpatialQueryDescriptor
            {
                Origin = position,
                Radius = float.MaxValue,
                MaxResults = k,
                Options = SpatialQueryOptions.RequireDeterministicSorting,
                Tolerance = 1e-4f,
                ExcludedEntity = Entity.Null
            };

            var filter = new SpatialAcceptAllFilter();
            var capacity = math.min(k, entryArray.Length);
            if (capacity <= 0)
            {
                return;
            }

            results.ResizeUninitialized(capacity);

            var slice = new NativeSlice<KNearestResult>(results.AsArray(), 0, capacity);
            var count = CollectKNearest(0, in descriptor, in config, ranges.AsNativeArray(), entryArray, slice, in filter);
            results.ResizeUninitialized(count);
        }

        public static void BatchOverlapAABB(
            NativeArray<MinMaxAABB> queries,
            in SpatialGridConfig config,
            in DynamicBuffer<SpatialGridCellRange> ranges,
            in DynamicBuffer<SpatialGridEntry> entries,
            ref NativeList<BatchOverlapResult> results)
        {
            results.Clear();
            if (!queries.IsCreated || queries.Length == 0)
            {
                return;
            }

            var entryArray = entries.AsNativeArray();
            if (entryArray.Length == 0)
            {
                return;
            }

            for (var qi = 0; qi < queries.Length; qi++)
            {
                var query = queries[qi];
                SpatialHash.Quantize(query.Min, config, out var minCell);
                SpatialHash.Quantize(query.Max, config, out var maxCell);

                minCell = math.clamp(minCell, int3.zero, config.CellCounts - 1);
                maxCell = math.clamp(maxCell, int3.zero, config.CellCounts - 1);

                if (math.any(maxCell < minCell))
                {
                    continue;
                }

                for (var x = minCell.x; x <= maxCell.x; x++)
                {
                    for (var y = minCell.y; y <= maxCell.y; y++)
                    {
                        for (var z = minCell.z; z <= maxCell.z; z++)
                        {
                            var coords = new int3(x, y, z);
                            var cellId = SpatialHash.Flatten(in coords, in config);
                            if ((uint)cellId >= ranges.Length)
                            {
                                continue;
                            }

                            var range = ranges[cellId];
                            if (range.Count <= 0)
                            {
                                continue;
                            }

                            for (var i = 0; i < range.Count; i++)
                            {
                                var entry = entryArray[range.StartIndex + i];
                                if (!math.all(entry.Position >= query.Min & entry.Position <= query.Max))
                                {
                                    continue;
                                }

                                results.Add(new BatchOverlapResult
                                {
                                    Entity = entry.Entity,
                                    QueryIndex = qi
                                });
                            }
                        }
                    }
                }
            }

            results.Sort();
        }

        /// <summary>
        /// Parallel batch job that resolves multiple k-nearest queries against the shared grid.
        /// </summary>
        [BurstCompile]
        public struct SpatialKNearestBatchJob<TFilter> : IJobParallelFor
            where TFilter : struct, ISpatialQueryFilter
        {
            [ReadOnly] public SpatialGridConfig Config;
            [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<SpatialGridCellRange> CellRanges;
            [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<SpatialGridEntry> Entries;
            [ReadOnly] public NativeArray<SpatialQueryDescriptor> Descriptors;
            public NativeArray<SpatialQueryRange> Ranges;
            [NativeDisableParallelForRestriction] public NativeArray<KNearestResult> Results;
            public TFilter Filter;

            public void Execute(int index)
            {
                var descriptor = Descriptors[index];
                var range = Ranges[index];
                var slice = new NativeSlice<KNearestResult>(Results, range.Start, range.Capacity);
                var filter = Filter;
                var count = CollectKNearest(index, in descriptor, in Config, CellRanges, Entries, slice, in filter);
                range.Count = count;
                Ranges[index] = range;
            }
        }

        private static float ComputeDistanceSq(float3 position, float3 origin, in SpatialQueryDescriptor descriptor)
        {
            var delta = position - origin;
            var options = descriptor.Options;
            var mode = descriptor.ProjectionMode;
            if (mode == SpatialProjectionMode.None && (options & SpatialQueryOptions.ProjectToXZ) != 0)
            {
                mode = SpatialProjectionMode.WorldPlane;
            }

            if (mode != SpatialProjectionMode.None)
            {
                var normal = ResolveProjectionNormal(in descriptor);
                delta -= normal * math.dot(delta, normal);
            }

            return math.lengthsq(delta);
        }

        private static float3 ResolveProjectionNormal(in SpatialQueryDescriptor descriptor)
        {
            var normal = descriptor.ProjectionPlaneNormal;
            if (math.lengthsq(normal) <= 1e-6f)
            {
                return new float3(0f, 1f, 0f);
            }

            return math.normalize(normal);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWithinBounds(in int3 coords, in int3 maxCounts)
        {
            return coords.x >= 0 && coords.y >= 0 && coords.z >= 0
                && coords.x < maxCounts.x
                && coords.y < maxCounts.y
                && coords.z < maxCounts.z;
        }

        private static void InsertKNearest(NativeSlice<KNearestResult> results, ref int count, int capacity, Entity entity, float distanceSq)
        {
            if (capacity <= 0 || results.Length == 0)
            {
                return;
            }

            var candidate = new KNearestResult
            {
                Entity = entity,
                DistanceSq = distanceSq
            };

            var limit = math.min(capacity, results.Length);
            var currentCount = math.min(count, limit);

            if (currentCount < limit)
            {
                var insertIndex = currentCount;
                while (insertIndex > 0 && candidate.CompareTo(results[insertIndex - 1]) < 0)
                {
                    results[insertIndex] = results[insertIndex - 1];
                    insertIndex--;
                }

                results[insertIndex] = candidate;
                count = currentCount + 1;
                return;
            }

            var furthest = results[limit - 1];
            if (candidate.CompareTo(furthest) >= 0)
            {
                return;
            }

            var idx = limit - 1;
            while (idx > 0 && candidate.CompareTo(results[idx - 1]) < 0)
            {
                results[idx] = results[idx - 1];
                idx--;
            }

            results[idx] = candidate;
            count = limit;
        }
    }

    /// <summary>
    /// Ensures deterministic ordering when collecting entities from the grid.
    /// </summary>
    public struct EntityDeterministicComparer : IComparer<Entity>
    {
        public int Compare(Entity x, Entity y)
        {
            return x.Index.CompareTo(y.Index);
        }
    }

    /// <summary>
    /// Result entry describing a nearest-neighbour query.
    /// </summary>
    public struct KNearestResult : System.IComparable<KNearestResult>
    {
        public Entity Entity;
        public float DistanceSq;

        public int CompareTo(KNearestResult other)
        {
            var distanceCompare = DistanceSq.CompareTo(other.DistanceSq);
            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            return Entity.Index.CompareTo(other.Entity.Index);
        }
    }

    /// <summary>
    /// Result entry emitted when batching AABB overlaps.
    /// </summary>
    public struct BatchOverlapResult : System.IComparable<BatchOverlapResult>
    {
        public Entity Entity;
        public int QueryIndex;

        public int CompareTo(BatchOverlapResult other)
        {
            var indexCompare = QueryIndex.CompareTo(other.QueryIndex);
            if (indexCompare != 0)
            {
                return indexCompare;
            }

            return Entity.Index.CompareTo(other.Entity.Index);
        }
    }

    /// <summary>
    /// Result entry emitted when batching radius queries.
    /// </summary>
    public struct BatchRadiusResult : System.IComparable<BatchRadiusResult>
    {
        public int QueryIndex;
        public float3 Origin;
        public float Radius;
        public int ResultCount;

        public int CompareTo(BatchRadiusResult other)
        {
            return QueryIndex.CompareTo(other.QueryIndex);
        }
    }
}
