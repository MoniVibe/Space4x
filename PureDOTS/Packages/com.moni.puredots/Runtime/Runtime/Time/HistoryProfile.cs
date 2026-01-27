using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Flags indicating which component categories to record in history.
    /// </summary>
    [System.Flags]
    public enum HistoryRecordFlags : uint
    {
        None = 0,
        /// <summary>Record transform/position data.</summary>
        Transform = 1 << 0,
        /// <summary>Record AI state data.</summary>
        AI = 1 << 1,
        /// <summary>Record resource/inventory data.</summary>
        Resources = 1 << 2,
        /// <summary>Record health/status data.</summary>
        Health = 1 << 3,
        /// <summary>Record job/task data.</summary>
        Jobs = 1 << 4,
        /// <summary>Record needs/morale data.</summary>
        Needs = 1 << 5,
        /// <summary>Record navigation/pathfinding data.</summary>
        Navigation = 1 << 6,
        /// <summary>Record combat data.</summary>
        Combat = 1 << 7,
        /// <summary>Record vegetation/growth data.</summary>
        Vegetation = 1 << 8,
        /// <summary>Record climate/environment data.</summary>
        Climate = 1 << 9,
        /// <summary>Record custom game-specific data.</summary>
        Custom1 = 1 << 16,
        Custom2 = 1 << 17,
        Custom3 = 1 << 18,
        Custom4 = 1 << 19,
        /// <summary>Record all categories.</summary>
        All = uint.MaxValue
    }

    /// <summary>
    /// Per-archetype configuration for history recording.
    /// Attach to entities that should participate in the history/rewind system.
    /// </summary>
    public struct HistoryProfile : IComponentData
    {
        /// <summary>Profile identifier for debugging and categorization.</summary>
        public FixedString32Bytes ProfileId;
        /// <summary>How often to sample (in ticks). 1 = every tick, 30 = every 30 ticks.</summary>
        public uint SamplingFrequencyTicks;
        /// <summary>Maximum ticks of history to retain. Older samples are pruned.</summary>
        public uint HorizonTicks;
        /// <summary>Flags indicating which component categories to record.</summary>
        public HistoryRecordFlags RecordFlags;
        /// <summary>Priority for memory allocation (higher = less likely to be pruned under pressure).</summary>
        public byte Priority;
        /// <summary>Whether this entity is currently enabled for recording.</summary>
        public bool IsEnabled;
        /// <summary>Last tick at which a sample was recorded.</summary>
        public uint LastSampleTick;

        /// <summary>
        /// Creates a default history profile with standard settings.
        /// </summary>
        public static HistoryProfile CreateDefault() => new HistoryProfile
        {
            ProfileId = new FixedString32Bytes("default"),
            SamplingFrequencyTicks = 1,
            HorizonTicks = 3600, // ~1 minute at 60 TPS
            RecordFlags = HistoryRecordFlags.Transform | HistoryRecordFlags.AI | HistoryRecordFlags.Resources,
            Priority = 100,
            IsEnabled = true,
            LastSampleTick = 0
        };

        /// <summary>
        /// Creates a profile optimized for villager entities.
        /// </summary>
        public static HistoryProfile CreateVillager() => new HistoryProfile
        {
            ProfileId = new FixedString32Bytes("villager"),
            SamplingFrequencyTicks = 1,
            HorizonTicks = 7200, // ~2 minutes at 60 TPS
            RecordFlags = HistoryRecordFlags.Transform | HistoryRecordFlags.AI | 
                         HistoryRecordFlags.Jobs | HistoryRecordFlags.Needs | HistoryRecordFlags.Health,
            Priority = 150,
            IsEnabled = true,
            LastSampleTick = 0
        };

        /// <summary>
        /// Creates a profile optimized for resource nodes.
        /// </summary>
        public static HistoryProfile CreateResource() => new HistoryProfile
        {
            ProfileId = new FixedString32Bytes("resource"),
            SamplingFrequencyTicks = 30, // Less frequent for static-ish entities
            HorizonTicks = 3600,
            RecordFlags = HistoryRecordFlags.Resources,
            Priority = 80,
            IsEnabled = true,
            LastSampleTick = 0
        };

        /// <summary>
        /// Creates a profile optimized for vegetation.
        /// </summary>
        public static HistoryProfile CreateVegetation() => new HistoryProfile
        {
            ProfileId = new FixedString32Bytes("vegetation"),
            SamplingFrequencyTicks = 60, // Even less frequent
            HorizonTicks = 1800,
            RecordFlags = HistoryRecordFlags.Vegetation | HistoryRecordFlags.Health,
            Priority = 50,
            IsEnabled = true,
            LastSampleTick = 0
        };

        /// <summary>
        /// Creates a profile for ships/fleets in Space4X.
        /// </summary>
        public static HistoryProfile CreateShip() => new HistoryProfile
        {
            ProfileId = new FixedString32Bytes("ship"),
            SamplingFrequencyTicks = 1,
            HorizonTicks = 7200,
            RecordFlags = HistoryRecordFlags.Transform | HistoryRecordFlags.AI | 
                         HistoryRecordFlags.Combat | HistoryRecordFlags.Navigation,
            Priority = 150,
            IsEnabled = true,
            LastSampleTick = 0
        };
    }

    /// <summary>
    /// Tag component marking an entity as actively participating in history recording.
    /// Added by TimeHistoryRecordSystem when HistoryProfile.IsEnabled is true.
    /// </summary>
    public struct HistoryActiveTag : IComponentData { }

    /// <summary>
    /// Singleton state for the history recording system.
    /// </summary>
    public struct TimeHistoryState : IComponentData
    {
        /// <summary>Total number of entities currently being recorded.</summary>
        public int ActiveEntityCount;
        /// <summary>Estimated total memory usage in bytes.</summary>
        public long EstimatedMemoryBytes;
        /// <summary>Last tick at which global cleanup was performed.</summary>
        public uint LastCleanupTick;
        /// <summary>Number of samples pruned in the last cleanup.</summary>
        public int LastCleanupPrunedCount;
        /// <summary>Whether the system is under memory pressure.</summary>
        public bool IsUnderMemoryPressure;
    }

    /// <summary>
    /// Generic history buffer for storing timestamped component snapshots.
    /// </summary>
    /// <typeparam name="T">The component type to snapshot.</typeparam>
    public struct ComponentHistory<T> : IBufferElementData where T : unmanaged
    {
        /// <summary>Tick at which this snapshot was recorded.</summary>
        public uint Tick;
        /// <summary>The component value at this tick.</summary>
        public T Value;
    }

    /// <summary>
    /// Metadata for a component history buffer, stored alongside the buffer.
    /// </summary>
    public struct ComponentHistoryMeta : IComponentData
    {
        /// <summary>Maximum number of entries to keep.</summary>
        public int MaxEntries;
        /// <summary>Current number of valid entries.</summary>
        public int Count;
        /// <summary>Oldest tick in the buffer.</summary>
        public uint OldestTick;
        /// <summary>Newest tick in the buffer.</summary>
        public uint NewestTick;
    }
}

