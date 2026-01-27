using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Spatial
{
    /// <summary>
    /// Snapshot of spatial grid state at a specific tick, used for rewind/replay validation.
    /// Contains all fields from SpatialGridState plus buffers metadata.
    /// </summary>
    public struct SpatialGridSnapshot : IComponentData
    {
        public uint CapturedTick;
        public int ActiveBufferIndex;
        public int TotalEntries;
        public uint Version;
        public uint LastUpdateTick;
        public uint LastDirtyTick;
        public uint DirtyVersion;
        public int DirtyAddCount;
        public int DirtyUpdateCount;
        public int DirtyRemoveCount;
        public float LastRebuildMilliseconds;
        public SpatialGridRebuildStrategy LastStrategy;

        /// <summary>
        /// Creates a snapshot from current SpatialGridState.
        /// </summary>
        public static SpatialGridSnapshot FromState(SpatialGridState state, uint tick)
        {
            return new SpatialGridSnapshot
            {
                CapturedTick = tick,
                ActiveBufferIndex = state.ActiveBufferIndex,
                TotalEntries = state.TotalEntries,
                Version = state.Version,
                LastUpdateTick = state.LastUpdateTick,
                LastDirtyTick = state.LastDirtyTick,
                DirtyVersion = state.DirtyVersion,
                DirtyAddCount = state.DirtyAddCount,
                DirtyUpdateCount = state.DirtyUpdateCount,
                DirtyRemoveCount = state.DirtyRemoveCount,
                LastRebuildMilliseconds = state.LastRebuildMilliseconds,
                LastStrategy = state.LastStrategy
            };
        }

        /// <summary>
        /// Compares this snapshot with current state and returns true if they match.
        /// </summary>
        public bool Matches(SpatialGridState state, out string difference)
        {
            difference = string.Empty;

            if (ActiveBufferIndex != state.ActiveBufferIndex)
            {
                difference = $"ActiveBufferIndex mismatch: {ActiveBufferIndex} != {state.ActiveBufferIndex}";
                return false;
            }

            if (TotalEntries != state.TotalEntries)
            {
                difference = $"TotalEntries mismatch: {TotalEntries} != {state.TotalEntries}";
                return false;
            }

            if (Version != state.Version)
            {
                difference = $"Version mismatch: {Version} != {state.Version}";
                return false;
            }

            if (LastUpdateTick != state.LastUpdateTick)
            {
                difference = $"LastUpdateTick mismatch: {LastUpdateTick} != {state.LastUpdateTick}";
                return false;
            }

            if (DirtyVersion != state.DirtyVersion)
            {
                difference = $"DirtyVersion mismatch: {DirtyVersion} != {state.DirtyVersion}";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Snapshot of spatial grid buffers (entries and cell ranges) for rewind/replay.
    /// Contains copies of buffer data at a specific tick.
    /// </summary>
    public struct SpatialGridBufferSnapshot
    {
        public uint CapturedTick;
        public NativeList<SpatialGridEntry> Entries;
        public NativeList<SpatialGridCellRange> CellRanges;
        public NativeList<SpatialGridDirtyOp> DirtyOps;

        public SpatialGridBufferSnapshot(Allocator allocator)
        {
            CapturedTick = 0;
            Entries = new NativeList<SpatialGridEntry>(16, allocator);
            CellRanges = new NativeList<SpatialGridCellRange>(16, allocator);
            DirtyOps = new NativeList<SpatialGridDirtyOp>(16, allocator);
        }

        public void Dispose()
        {
            if (Entries.IsCreated)
            {
                Entries.Dispose();
            }

            if (CellRanges.IsCreated)
            {
                CellRanges.Dispose();
            }

            if (DirtyOps.IsCreated)
            {
                DirtyOps.Dispose();
            }
        }

        /// <summary>
        /// Creates a snapshot from current spatial grid buffers.
        /// </summary>
        public static SpatialGridBufferSnapshot Capture(
            DynamicBuffer<SpatialGridEntry> entries,
            DynamicBuffer<SpatialGridCellRange> cellRanges,
            DynamicBuffer<SpatialGridDirtyOp> dirtyOps,
            uint tick,
            Allocator allocator)
        {
            var snapshot = new SpatialGridBufferSnapshot(allocator);
            snapshot.CapturedTick = tick;

            snapshot.Entries.Capacity = math.max(entries.Length, 16);
            for (int i = 0; i < entries.Length; i++)
            {
                snapshot.Entries.Add(entries[i]);
            }

            snapshot.CellRanges.Capacity = math.max(cellRanges.Length, 16);
            for (int i = 0; i < cellRanges.Length; i++)
            {
                snapshot.CellRanges.Add(cellRanges[i]);
            }

            snapshot.DirtyOps.Capacity = math.max(dirtyOps.Length, 16);
            for (int i = 0; i < dirtyOps.Length; i++)
            {
                snapshot.DirtyOps.Add(dirtyOps[i]);
            }

            return snapshot;
        }

        /// <summary>
        /// Compares this snapshot with current buffers and returns true if they match.
        /// </summary>
        public bool Matches(
            DynamicBuffer<SpatialGridEntry> entries,
            DynamicBuffer<SpatialGridCellRange> cellRanges,
            DynamicBuffer<SpatialGridDirtyOp> dirtyOps,
            out string difference)
        {
            difference = string.Empty;

            if (Entries.Length != entries.Length)
            {
                difference = $"Entries length mismatch: {Entries.Length} != {entries.Length}";
                return false;
            }

            for (int i = 0; i < Entries.Length; i++)
            {
                if (!Entries[i].Entity.Equals(entries[i].Entity))
                {
                    difference = $"Entry {i} entity mismatch";
                    return false;
                }

                if (math.distancesq(Entries[i].Position, entries[i].Position) > 0.001f)
                {
                    difference = $"Entry {i} position mismatch";
                    return false;
                }

                if (Entries[i].CellId != entries[i].CellId)
                {
                    difference = $"Entry {i} cell ID mismatch";
                    return false;
                }
            }

            if (CellRanges.Length != cellRanges.Length)
            {
                difference = $"CellRanges length mismatch: {CellRanges.Length} != {cellRanges.Length}";
                return false;
            }

            for (int i = 0; i < CellRanges.Length; i++)
            {
                if (CellRanges[i].StartIndex != cellRanges[i].StartIndex)
                {
                    difference = $"CellRange {i} start index mismatch";
                    return false;
                }

                if (CellRanges[i].Count != cellRanges[i].Count)
                {
                    difference = $"CellRange {i} count mismatch";
                    return false;
                }
            }

            if (DirtyOps.Length != dirtyOps.Length)
            {
                difference = $"DirtyOps length mismatch: {DirtyOps.Length} != {dirtyOps.Length}";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Diff between two spatial grid snapshots, describing what changed.
    /// Used for incremental replay validation.
    /// </summary>
    public struct SpatialGridDiff
    {
        public uint FromTick;
        public uint ToTick;
        public int EntryDelta;
        public int CellRangeDelta;
        public int DirtyOpDelta;
        public uint VersionDelta;

        public static SpatialGridDiff Compute(
            SpatialGridSnapshot from,
            SpatialGridBufferSnapshot fromBuffers,
            SpatialGridSnapshot to,
            SpatialGridBufferSnapshot toBuffers)
        {
            return new SpatialGridDiff
            {
                FromTick = from.CapturedTick,
                ToTick = to.CapturedTick,
                EntryDelta = toBuffers.Entries.Length - fromBuffers.Entries.Length,
                CellRangeDelta = toBuffers.CellRanges.Length - fromBuffers.CellRanges.Length,
                DirtyOpDelta = toBuffers.DirtyOps.Length - fromBuffers.DirtyOps.Length,
                VersionDelta = to.Version - from.Version
            };
        }
    }
}


