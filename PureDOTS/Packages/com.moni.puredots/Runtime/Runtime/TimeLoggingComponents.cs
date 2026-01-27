using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Configuration for time debug logs.
    /// Capacities expressed in seconds and expanded to ticks (seconds * 60).
    /// </summary>
    public struct TimeLogSettings : IComponentData
    {
        public int CommandLogSeconds;
        public int SnapshotLogSeconds;
        public int MemoryBudgetBytes;
    }

    public static class TimeLogDefaults
    {
        public const int CommandLogSeconds = 30;
        public const int SnapshotLogSeconds = 30;
        public const int MemoryBudgetBytes = 512 * 1024;

        public static TimeLogSettings CreateDefault() => new TimeLogSettings
        {
            CommandLogSeconds = CommandLogSeconds,
            SnapshotLogSeconds = SnapshotLogSeconds,
            MemoryBudgetBytes = MemoryBudgetBytes
        };
    }

    /// <summary>
    /// Ring buffer entry for time control commands.
    /// </summary>
    [InternalBufferCapacity(128)]
    public struct InputCommandLogEntry : IBufferElementData
    {
        public uint Tick;
        public byte Type;
        public float FloatParam;
        public uint UintParam;
    }

    public struct InputCommandLogState : IComponentData
    {
        public int Capacity;
        public int Count;
        public int StartIndex;
        public uint LastTick;
    }

    /// <summary>
    /// Snapshot of the tick/rewind state for debugging catch-up/rewind timelines.
    /// </summary>
    [InternalBufferCapacity(256)]
    public struct TickSnapshotLogEntry : IBufferElementData
    {
        public uint Tick;
        public uint TargetTick;
        public byte IsPlaying;
        public byte IsPaused;
        public RewindMode RewindMode;
        public uint RewindTargetTick;
        public uint RewindPlaybackTick;
    }

    public struct TickSnapshotLogState : IComponentData
    {
        public int Capacity;
        public int Count;
        public int StartIndex;
        public uint LastTick;
    }
}
