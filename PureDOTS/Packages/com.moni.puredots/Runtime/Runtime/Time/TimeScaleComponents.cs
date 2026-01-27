using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Source type for timescale entries, used for conflict resolution and debugging.
    /// </summary>
    public enum TimeScaleSource : byte
    {
        /// <summary>Default/baseline timescale.</summary>
        Default = 0,
        /// <summary>Player-initiated speed change.</summary>
        Player = 1,
        /// <summary>Miracle/divine intervention.</summary>
        Miracle = 2,
        /// <summary>Scenario/scripted event.</summary>
        Scenario = 3,
        /// <summary>Developer/debug tool.</summary>
        DevTool = 4,
        /// <summary>Technology/module effect.</summary>
        Technology = 5,
        /// <summary>System pause (highest priority).</summary>
        SystemPause = 6
    }

    /// <summary>
    /// Entry in the timescale schedule buffer.
    /// Active entries are resolved by priority to determine effective timescale.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeScaleEntry : IBufferElementData
    {
        /// <summary>Unique identifier for this entry.</summary>
        public uint EntryId;
        /// <summary>Tick at which this entry becomes active (0 = immediate).</summary>
        public uint StartTick;
        /// <summary>Tick at which this entry expires (uint.MaxValue = permanent until removed).</summary>
        public uint EndTick;
        /// <summary>Target timescale (0.01-16.0, 0 = pause).</summary>
        public float Scale;
        /// <summary>Source of this entry for debugging and conflict resolution.</summary>
        public TimeScaleSource Source;
        /// <summary>Priority for conflict resolution (higher wins, equal = newest wins).</summary>
        public byte Priority;
        /// <summary>Source entity ID (miracle ID, tech ID, player ID, etc.).</summary>
        public uint SourceId;
        /// <summary>Whether this entry pauses the simulation (overrides scale).</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool IsPause;

        /// <summary>
        /// Creates a pause entry with highest priority.
        /// </summary>
        public static TimeScaleEntry CreatePause(uint entryId, TimeScaleSource source, uint sourceId, byte priority = 255)
        {
            return new TimeScaleEntry
            {
                EntryId = entryId,
                StartTick = 0,
                EndTick = uint.MaxValue,
                Scale = 0f,
                Source = source,
                Priority = priority,
                SourceId = sourceId,
                IsPause = true
            };
        }

        /// <summary>
        /// Creates a speed entry with specified parameters.
        /// </summary>
        public static TimeScaleEntry CreateSpeed(uint entryId, float scale, TimeScaleSource source, 
            uint sourceId, byte priority, uint startTick = 0, uint endTick = uint.MaxValue)
        {
            return new TimeScaleEntry
            {
                EntryId = entryId,
                StartTick = startTick,
                EndTick = endTick,
                Scale = scale,
                Source = source,
                Priority = priority,
                SourceId = sourceId,
                IsPause = false
            };
        }
    }

    /// <summary>
    /// Tag component marking the timescale schedule singleton entity.
    /// The entity with this tag holds the DynamicBuffer&lt;TimeScaleEntry&gt;.
    /// </summary>
    public struct TimeScaleScheduleTag : IComponentData { }

    /// <summary>
    /// Singleton state for the timescale scheduling system.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeScaleScheduleState : IComponentData
    {
        /// <summary>Next available entry ID for new entries.</summary>
        public uint NextEntryId;
        /// <summary>Current resolved effective timescale.</summary>
        public float ResolvedScale;
        /// <summary>Whether the simulation is currently paused via schedule.</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool IsPaused;
        /// <summary>Entry ID of the entry that determined the current state (for debugging).</summary>
        public uint ActiveEntryId;
        /// <summary>Source of the active entry (for debugging).</summary>
        public TimeScaleSource ActiveSource;
    }

    /// <summary>
    /// Configuration for timescale resolution behavior.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeScaleConfig : IComponentData
    {
        /// <summary>Minimum allowed timescale (default 0.01).</summary>
        public float MinScale;
        /// <summary>Maximum allowed timescale (default 16.0).</summary>
        public float MaxScale;
        /// <summary>Default timescale when no entries are active.</summary>
        public float DefaultScale;
        /// <summary>Whether to allow entries to stack multiplicatively (false = highest priority wins).</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool AllowStacking;

        /// <summary>
        /// Creates a default configuration.
        /// </summary>
        public static TimeScaleConfig CreateDefault() => new TimeScaleConfig
        {
            MinScale = TimeControlLimits.DefaultMinSpeed,
            MaxScale = TimeControlLimits.DefaultMaxSpeed,
            DefaultScale = 1.0f,
            AllowStacking = false
        };
    }

    /// <summary>
    /// Helper struct for timescale preset values.
    /// </summary>
    public static class TimeScalePresets
    {
        public const float SuperSlow = 0.01f;
        public const float VerySlow = 0.1f;
        public const float Slow = 0.25f;
        public const float HalfSpeed = 0.5f;
        public const float Normal = 1.0f;
        public const float Fast = 2.0f;
        public const float VeryFast = 4.0f;
        public const float SuperFast = 8.0f;
        public const float Maximum = 16.0f;
    }
}

