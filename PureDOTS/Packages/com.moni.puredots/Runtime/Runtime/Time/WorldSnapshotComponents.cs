using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Type of compression used for snapshot data.
    /// </summary>
    public enum SnapshotCompressionType : byte
    {
        /// <summary>No compression, raw bytes.</summary>
        None = 0,
        /// <summary>LZ4 compression (future).</summary>
        LZ4 = 1,
        /// <summary>Delta encoding from previous snapshot (future).</summary>
        Delta = 2
    }

    /// <summary>
    /// Priority level for snapshot groups, affecting memory allocation and pruning.
    /// </summary>
    public enum SnapshotImportance : byte
    {
        /// <summary>Can be pruned first under memory pressure.</summary>
        Low = 0,
        /// <summary>Normal importance, standard retention.</summary>
        Normal = 1,
        /// <summary>Higher priority, retained longer.</summary>
        High = 2,
        /// <summary>Critical data, retained as long as possible.</summary>
        Critical = 3
    }

    /// <summary>
    /// Singleton state for the global world snapshot system.
    /// </summary>
    public struct WorldSnapshotState : IComponentData
    {
        /// <summary>How often to capture snapshots (in ticks).</summary>
        public uint SnapshotIntervalTicks;
        /// <summary>Maximum number of snapshots to retain (ring buffer size).</summary>
        public int MaxSnapshots;
        /// <summary>Current number of valid snapshots.</summary>
        public int CurrentSnapshotCount;
        /// <summary>Next available snapshot index in ring buffer.</summary>
        public int NextSnapshotIndex;
        /// <summary>Last tick at which a snapshot was captured.</summary>
        public uint LastSnapshotTick;
        /// <summary>Total bytes used by all snapshots.</summary>
        public long TotalMemoryBytes;
        /// <summary>Maximum memory budget in bytes.</summary>
        public long MemoryBudgetBytes;
        /// <summary>Whether snapshots are currently enabled.</summary>
        public bool IsEnabled;

        /// <summary>
        /// Creates default snapshot state configuration.
        /// </summary>
        public static WorldSnapshotState CreateDefault() => new WorldSnapshotState
        {
            SnapshotIntervalTicks = 30, // ~0.5 seconds at 60 TPS
            MaxSnapshots = 100,
            CurrentSnapshotCount = 0,
            NextSnapshotIndex = 0,
            LastSnapshotTick = 0,
            TotalMemoryBytes = 0,
            MemoryBudgetBytes = 256 * 1024 * 1024, // 256 MB default
            IsEnabled = true
        };
    }

    /// <summary>
    /// Tag component marking the world snapshot singleton entity.
    /// </summary>
    public struct WorldSnapshotTag : IComponentData { }

    /// <summary>
    /// Metadata for a single world snapshot.
    /// 
    /// CONCEPT: Snapshots represent rare, coarse "checkpoints" in time, not continuous rewind frames.
    /// They provide a baseline for rewinding, with per-component histories handling fine-grained playback.
    /// 
    /// MULTIPLAYER: OwnerPlayerId and Scope fields are reserved for future MP support.
    /// Single-player uses OwnerPlayerId = 0 (global) and Scope = Global.
    /// </summary>
    public struct WorldSnapshotMeta : IBufferElementData
    {
        /// <summary>Tick at which this snapshot was captured (checkpoint tick).</summary>
        public uint Tick;
        /// <summary>Whether this snapshot contains valid data.</summary>
        public bool IsValid;
        /// <summary>Byte offset into the snapshot data buffer.</summary>
        public int ByteOffset;
        /// <summary>Length of this snapshot's data in bytes.</summary>
        public int ByteLength;
        /// <summary>Compression type used for this snapshot.</summary>
        public SnapshotCompressionType CompressionType;
        /// <summary>Number of entities captured in this snapshot.</summary>
        public int EntityCount;
        /// <summary>Checksum for data integrity (optional).</summary>
        public uint Checksum;
        /// <summary>Owner player ID (0 = global world snapshot, >0 = reserved for player/faction-specific snapshots in MP).</summary>
        public byte OwnerPlayerId;
        /// <summary>Scope of this snapshot (Global vs Player vs LocalArea) for MP documentation. Single-player uses Global only.</summary>
        public TimeControlScope Scope;
    }

    /// <summary>
    /// Configuration for a snapshot group defining what to capture.
    /// </summary>
    public struct WorldSnapshotGroup : IComponentData
    {
        /// <summary>Unique identifier for this group.</summary>
        public uint GroupId;
        /// <summary>Label for debugging.</summary>
        public FixedString32Bytes Label;
        /// <summary>Multiplier for snapshot frequency (1 = every snapshot, 2 = every other, etc.).</summary>
        public uint FrequencyMultiplier;
        /// <summary>Importance level for memory management.</summary>
        public SnapshotImportance Importance;
        /// <summary>Component types to include (flags).</summary>
        public HistoryRecordFlags ComponentFlags;
        /// <summary>Whether this group is enabled for capture.</summary>
        public bool IsEnabled;
    }

    /// <summary>
    /// Tag component marking entities that should be included in world snapshots.
    /// </summary>
    public struct WorldSnapshotIncludeTag : IComponentData
    {
        /// <summary>Group ID this entity belongs to (0 = default group).</summary>
        public uint GroupId;
    }

    /// <summary>
    /// Raw snapshot data buffer stored on the snapshot singleton entity.
    /// </summary>
    public struct WorldSnapshotData : IBufferElementData
    {
        public byte Value;
    }

    /// <summary>
    /// Request to restore to a specific snapshot tick.
    /// </summary>
    public struct WorldSnapshotRestoreRequest : IComponentData
    {
        /// <summary>Target tick to restore to.</summary>
        public uint TargetTick;
        /// <summary>Whether to use interpolation if exact tick not available.</summary>
        public bool AllowInterpolation;
        /// <summary>Whether the request is pending.</summary>
        public bool IsPending;
    }

    /// <summary>
    /// Result of a snapshot restore operation.
    /// </summary>
    public struct WorldSnapshotRestoreResult : IComponentData
    {
        /// <summary>Tick that was actually restored.</summary>
        public uint RestoredTick;
        /// <summary>Number of entities restored.</summary>
        public int EntitiesRestored;
        /// <summary>Whether the restore was successful.</summary>
        public bool Success;
        /// <summary>Error message if failed.</summary>
        public FixedString64Bytes ErrorMessage;
    }

    /// <summary>
    /// Serialized entity header in snapshot data.
    /// </summary>
    public struct SnapshotEntityHeader
    {
        /// <summary>Entity index.</summary>
        public int EntityIndex;
        /// <summary>Entity version.</summary>
        public int EntityVersion;
        /// <summary>Byte offset to component data.</summary>
        public int DataOffset;
        /// <summary>Number of components serialized.</summary>
        public int ComponentCount;
    }

    /// <summary>
    /// Serialized component header in snapshot data.
    /// </summary>
    public struct SnapshotComponentHeader
    {
        /// <summary>Component type hash for identification.</summary>
        public ulong TypeHash;
        /// <summary>Size of component data in bytes.</summary>
        public int DataSize;
    }
}

