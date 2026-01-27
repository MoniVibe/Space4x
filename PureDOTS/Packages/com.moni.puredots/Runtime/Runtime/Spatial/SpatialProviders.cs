using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst.Intrinsics;

namespace PureDOTS.Runtime.Spatial
{
    /// <summary>
    /// Known provider identifiers for spatial grid configurations.
    /// </summary>
    public static class SpatialGridProviderIds
    {
        public const byte Hashed = 0;
        public const byte Uniform = 1;
    }

    /// <summary>
    /// Context passed to spatial providers so they can access shared queries and handles.
    /// </summary>
    public struct SpatialGridProviderContext
    {
        public EntityQuery IndexedQuery;
        public ComponentTypeHandle<LocalTransform> TransformHandle;
        public EntityTypeHandle EntityTypeHandle;
    }

    /// <summary>
    /// Contract implemented by spatial grid providers to support interchangeable rebuild strategies.
    /// </summary>
    public interface ISpatialGridProvider
    {
        bool ValidateConfig(in SpatialGridConfig config, out FixedString128Bytes validationError);
        bool TryApplyPartialRebuild(ref DynamicBuffer<SpatialGridEntry> activeEntries,
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridEntryLookup> lookup,
            in DynamicBuffer<SpatialGridDirtyOp> dirtyOps,
            in SpatialGridConfig config);
        int PerformFullRebuild(ref SystemState state,
            in SpatialGridConfig config,
            in SpatialGridProviderContext context,
            ref DynamicBuffer<SpatialGridEntry> activeEntries,
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridStagingEntry> stagingEntries,
            ref DynamicBuffer<SpatialGridStagingCellRange> stagingRanges);
        void RebuildLookup(ref DynamicBuffer<SpatialGridEntryLookup> lookup, in DynamicBuffer<SpatialGridEntry> entries);
    }

    /// <summary>
    /// Default hashed-grid provider used by the project.
    /// </summary>
    public struct HashedSpatialGridProvider : ISpatialGridProvider
    {
        public bool ValidateConfig(in SpatialGridConfig config, out FixedString128Bytes validationError)
        {
            validationError = default;

            if (config.CellSize <= 0f)
            {
                return false;
            }

            if (config.CellCounts.x <= 0 || config.CellCounts.y <= 0 || config.CellCounts.z <= 0)
            {
                return false;
            }

            if (config.CellCount <= 0)
            {
                return false;
            }

            return true;
        }

        public bool TryApplyPartialRebuild(ref DynamicBuffer<SpatialGridEntry> activeEntries,
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridEntryLookup> lookup,
            in DynamicBuffer<SpatialGridDirtyOp> dirtyOps,
            in SpatialGridConfig config)
        {
            return SpatialGridPartialUpdater.TryApplyPartialRebuild(ref activeEntries, ref activeRanges, ref lookup, in dirtyOps, in config);
        }

        public int PerformFullRebuild(ref SystemState state,
            in SpatialGridConfig config,
            in SpatialGridProviderContext context,
            ref DynamicBuffer<SpatialGridEntry> activeEntries,
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridStagingEntry> stagingEntries,
            ref DynamicBuffer<SpatialGridStagingCellRange> stagingRanges)
        {
            return SpatialGridFullRebuild.Execute(ref state, in config, in context, ref activeEntries, ref activeRanges, ref stagingEntries, ref stagingRanges);
        }

        public void RebuildLookup(ref DynamicBuffer<SpatialGridEntryLookup> lookup, in DynamicBuffer<SpatialGridEntry> entries)
        {
            SpatialGridPartialUpdater.RebuildLookupBuffer(ref lookup, in entries);
        }
    }

    /// <summary>
    /// Uniform grid provider that enforces axis-aligned cell layouts.
    /// </summary>
    public struct UniformSpatialGridProvider : ISpatialGridProvider
    {
        public bool ValidateConfig(in SpatialGridConfig config, out FixedString128Bytes validationError)
        {
            validationError = default;

            if (config.CellSize <= 0f)
            {
                return false;
            }

            if (config.CellCounts.x <= 0 || config.CellCounts.y <= 0 || config.CellCounts.z <= 0)
            {
                return false;
            }

            if (config.CellCount <= 0)
            {
                return false;
            }

            var worldExtent = config.WorldMax - config.WorldMin;
            if (math.any(worldExtent <= 0f))
            {
                return false;
            }

            var expectedExtent = new float3(config.CellCounts) * config.CellSize;
            if (math.any(math.abs(expectedExtent - worldExtent) > 1e-3f))
            {
                return false;
            }

            return true;
        }

        public bool TryApplyPartialRebuild(ref DynamicBuffer<SpatialGridEntry> activeEntries,
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridEntryLookup> lookup,
            in DynamicBuffer<SpatialGridDirtyOp> dirtyOps,
            in SpatialGridConfig config)
        {
            return SpatialGridPartialUpdater.TryApplyPartialRebuild(ref activeEntries, ref activeRanges, ref lookup, in dirtyOps, in config);
        }

        public int PerformFullRebuild(ref SystemState state,
            in SpatialGridConfig config,
            in SpatialGridProviderContext context,
            ref DynamicBuffer<SpatialGridEntry> activeEntries,
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridStagingEntry> stagingEntries,
            ref DynamicBuffer<SpatialGridStagingCellRange> stagingRanges)
        {
            return SpatialGridFullRebuild.Execute(ref state, in config, in context, ref activeEntries, ref activeRanges, ref stagingEntries, ref stagingRanges);
        }

        public void RebuildLookup(ref DynamicBuffer<SpatialGridEntryLookup> lookup, in DynamicBuffer<SpatialGridEntry> entries)
        {
            SpatialGridPartialUpdater.RebuildLookupBuffer(ref lookup, in entries);
        }
    }

    internal static class SpatialGridPartialUpdater
    {
        public static bool TryApplyPartialRebuild(ref DynamicBuffer<SpatialGridEntry> activeEntries,
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridEntryLookup> lookup,
            in DynamicBuffer<SpatialGridDirtyOp> dirtyOps,
            in SpatialGridConfig config)
        {
            if (dirtyOps.Length == 0)
            {
                return true;
            }

            var cellCount = math.max(config.CellCount, 0);
            if (cellCount == 0)
            {
                return false;
            }

            var counts = new NativeArray<int>(cellCount, Allocator.Temp);
            var starts = new NativeArray<int>(cellCount, Allocator.Temp);

            var running = 0;
            for (var cell = 0; cell < cellCount; cell++)
            {
                var count = cell < activeRanges.Length ? math.max(activeRanges[cell].Count, 0) : 0;
                counts[cell] = count;
                starts[cell] = running;
                running += count;
            }

            if (activeEntries.Length != running)
            {
                counts.Dispose();
                starts.Dispose();
                return false;
            }

            var success = true;

            for (var i = 0; i < dirtyOps.Length && success; i++)
            {
                var op = dirtyOps[i];
                switch (op.Operation)
                {
                    case SpatialGridDirtyOpType.Add:
                        success = TryApplyAdd(in op, ref activeEntries, counts, starts);
                        break;
                    case SpatialGridDirtyOpType.Update:
                        success = TryApplyUpdate(in op, ref activeEntries, counts, starts);
                        break;
                    case SpatialGridDirtyOpType.Remove:
                        success = TryApplyRemove(in op, ref activeEntries, counts, starts);
                        break;
                }
            }

            if (!success)
            {
                counts.Dispose();
                starts.Dispose();
                return false;
            }

            activeRanges.Clear();
            activeRanges.ResizeUninitialized(cellCount);

            running = 0;
            for (var cell = 0; cell < cellCount; cell++)
            {
                var count = counts[cell];
                activeRanges[cell] = new SpatialGridCellRange
                {
                    StartIndex = running,
                    Count = count
                };
                running += count;
            }

            RebuildLookupBuffer(ref lookup, in activeEntries);

            counts.Dispose();
            starts.Dispose();

            return true;
        }

        private static bool TryApplyAdd(in SpatialGridDirtyOp op, ref DynamicBuffer<SpatialGridEntry> entries, NativeArray<int> counts, NativeArray<int> starts)
        {
            var cellCount = counts.Length;
            if (!IsValidCell(op.NewCellId, cellCount))
            {
                return false;
            }

            var existingIndex = FindEntityIndex(in op.Entity, op.NewCellId, entries, counts, starts);
            if (existingIndex >= 0)
            {
                var existing = entries[existingIndex];
                existing.Position = op.Position;
                existing.CellId = op.NewCellId;
                entries[existingIndex] = existing;
                return true;
            }

            var start = starts[op.NewCellId];
            var count = counts[op.NewCellId];
            var insertIndex = start + count;

            for (var i = start; i < start + count; i++)
            {
                if (IsEntityBefore(op.Entity, entries[i].Entity))
                {
                    insertIndex = i;
                    break;
                }
            }

            entries.Add(default);
            for (var i = entries.Length - 1; i > insertIndex; i--)
            {
                entries[i] = entries[i - 1];
            }

            entries[insertIndex] = new SpatialGridEntry
            {
                Entity = op.Entity,
                Position = op.Position,
                CellId = op.NewCellId
            };

            counts[op.NewCellId] = count + 1;
            for (var cell = op.NewCellId + 1; cell < cellCount; cell++)
            {
                starts[cell] += 1;
            }

            return true;
        }

        private static bool TryApplyRemove(in SpatialGridDirtyOp op, ref DynamicBuffer<SpatialGridEntry> entries, NativeArray<int> counts, NativeArray<int> starts)
        {
            var cellCount = counts.Length;
            if (!IsValidCell(op.OldCellId, cellCount))
            {
                return true;
            }

            var index = FindEntityIndex(in op.Entity, op.OldCellId, entries, counts, starts);
            if (index < 0)
            {
                return false;
            }

            entries.RemoveAt(index);

            counts[op.OldCellId] = math.max(0, counts[op.OldCellId] - 1);
            for (var cell = op.OldCellId + 1; cell < cellCount; cell++)
            {
                starts[cell] -= 1;
            }

            return true;
        }

        private static bool TryApplyUpdate(in SpatialGridDirtyOp op, ref DynamicBuffer<SpatialGridEntry> entries, NativeArray<int> counts, NativeArray<int> starts)
        {
            if (op.NewCellId < 0)
            {
                return TryApplyRemove(in op, ref entries, counts, starts);
            }

            if (op.OldCellId < 0)
            {
                return TryApplyAdd(in op, ref entries, counts, starts);
            }

            if (op.OldCellId == op.NewCellId)
            {
                var index = FindEntityIndex(in op.Entity, op.NewCellId, entries, counts, starts);
                if (index < 0)
                {
                    return false;
                }

                var entry = entries[index];
                entry.Position = op.Position;
                entries[index] = entry;
                return true;
            }

            if (!TryApplyRemove(in op, ref entries, counts, starts))
            {
                return false;
            }

            var addOp = new SpatialGridDirtyOp
            {
                Entity = op.Entity,
                Position = op.Position,
                OldCellId = -1,
                NewCellId = op.NewCellId,
                Operation = SpatialGridDirtyOpType.Add
            };

            return TryApplyAdd(in addOp, ref entries, counts, starts);
        }

        private static int FindEntityIndex(in Entity entity, int cellId, DynamicBuffer<SpatialGridEntry> entries, NativeArray<int> counts, NativeArray<int> starts)
        {
            if (!IsValidCell(cellId, counts.Length))
            {
                return -1;
            }

            var count = counts[cellId];
            if (count <= 0)
            {
                return -1;
            }

            var start = starts[cellId];
            var end = start + count;

            for (var i = start; i < end && i < entries.Length; i++)
            {
                if (entries[i].Entity == entity)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsEntityBefore(Entity lhs, Entity rhs)
        {
            if (lhs.Index != rhs.Index)
            {
                return lhs.Index < rhs.Index;
            }

            return lhs.Version < rhs.Version;
        }

        private static bool IsValidCell(int cellId, int cellCount)
        {
            return (uint)cellId < (uint)cellCount;
        }

        public static void RebuildLookupBuffer(ref DynamicBuffer<SpatialGridEntryLookup> lookup, in DynamicBuffer<SpatialGridEntry> entries)
        {
            lookup.Clear();

            if (entries.Length == 0)
            {
                return;
            }

            lookup.ResizeUninitialized(entries.Length);

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                lookup[i] = new SpatialGridEntryLookup
                {
                    Entity = entry.Entity,
                    EntryIndex = i,
                    CellId = entry.CellId
                };
            }
        }
    }

    [BurstCompile]
    internal static class SpatialGridFullRebuild
    {
        public static int Execute(ref SystemState state,
            in SpatialGridConfig config,
            in SpatialGridProviderContext context,
            ref DynamicBuffer<SpatialGridEntry> activeEntries,
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridStagingEntry> stagingEntries,
            ref DynamicBuffer<SpatialGridStagingCellRange> stagingRanges)
        {
            var indexedCount = context.IndexedQuery.CalculateEntityCount();

            stagingEntries.Clear();
            stagingRanges.Clear();

            var gatherList = new NativeList<SpatialGridStagingEntry>(Allocator.TempJob)
            {
                Capacity = math.max(0, indexedCount)
            };

            var gatherJob = new GatherSpatialEntriesJob
            {
                TransformType = context.TransformHandle,
                EntityType = context.EntityTypeHandle,
                Config = config,
                Writer = gatherList.AsParallelWriter()
            };

            var gatherHandle = gatherJob.ScheduleParallel(context.IndexedQuery, state.Dependency);
            gatherHandle.Complete();

            gatherList.Sort(new SpatialGridEntryCellComparer());
            stagingEntries.EnsureCapacity(gatherList.Length);

            var tempRanges = new NativeArray<SpatialGridStagingCellRange>(config.CellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            var currentCell = -1;
            var cellStart = 0;

            for (var i = 0; i < gatherList.Length; i++)
            {
                var entry = gatherList[i];
                if (entry.CellId != currentCell)
                {
                    if (currentCell >= 0)
                    {
                        tempRanges[currentCell] = new SpatialGridStagingCellRange
                        {
                            StartIndex = cellStart,
                            Count = i - cellStart
                        };
                    }

                    currentCell = entry.CellId;
                    cellStart = i;
                }

                stagingEntries.Add(entry);
            }

            if (currentCell >= 0)
            {
                tempRanges[currentCell] = new SpatialGridStagingCellRange
                {
                    StartIndex = cellStart,
                    Count = gatherList.Length - cellStart
                };
            }

            stagingRanges.ResizeUninitialized(config.CellCount);
            for (var i = 0; i < config.CellCount; i++)
            {
                stagingRanges[i] = tempRanges[i];
            }

            CopyStagingToActive(ref activeRanges, ref activeEntries, in stagingRanges, in stagingEntries);

            var totalEntries = gatherList.Length;

            gatherList.Dispose();
            tempRanges.Dispose();

            return totalEntries;
        }

        [BurstCompile]
        private struct GatherSpatialEntriesJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformType;
            [ReadOnly] public EntityTypeHandle EntityType;
            public SpatialGridConfig Config;
            public NativeList<SpatialGridStagingEntry>.ParallelWriter Writer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var transforms = chunk.GetNativeArray(ref TransformType);
                var entities = chunk.GetNativeArray(EntityType);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var position = transforms[i].Position;
                    SpatialHash.Quantize(position, Config, out var coords);
                    var cellId = SpatialHash.Flatten(in coords, in Config);

                    if ((uint)cellId >= (uint)Config.CellCount)
                    {
                        continue;
                    }

                    Writer.AddNoResize(new SpatialGridStagingEntry
                    {
                        Entity = entities[i],
                        Position = position,
                        CellId = cellId
                    });
                }
            }
        }

        [BurstCompile]
        internal static void CopyStagingToActive(ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridEntry> activeEntries,
            in DynamicBuffer<SpatialGridStagingCellRange> stagingRanges,
            in DynamicBuffer<SpatialGridStagingEntry> stagingEntries)
        {
            activeRanges.Clear();
            activeRanges.ResizeUninitialized(stagingRanges.Length);

            for (var i = 0; i < stagingRanges.Length; i++)
            {
                var range = stagingRanges[i];
                activeRanges[i] = new SpatialGridCellRange
                {
                    StartIndex = range.StartIndex,
                    Count = range.Count
                };
            }

            activeEntries.Clear();
            activeEntries.ResizeUninitialized(stagingEntries.Length);

            for (var i = 0; i < stagingEntries.Length; i++)
            {
                var entry = stagingEntries[i];
                activeEntries[i] = new SpatialGridEntry
                {
                    Entity = entry.Entity,
                    Position = entry.Position,
                    CellId = entry.CellId
                };
            }
        }

        internal struct SpatialGridEntryCellComparer : IComparer<SpatialGridStagingEntry>
        {
            public int Compare(SpatialGridStagingEntry x, SpatialGridStagingEntry y)
            {
                var cellCompare = x.CellId.CompareTo(y.CellId);
                if (cellCompare != 0)
                {
                    return cellCompare;
                }

                if (x.Entity.Index != y.Entity.Index)
                {
                    return x.Entity.Index.CompareTo(y.Entity.Index);
                }

                return x.Entity.Version.CompareTo(y.Entity.Version);
            }
        }
    }
}
