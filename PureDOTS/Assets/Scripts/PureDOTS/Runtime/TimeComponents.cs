using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Global time state used by simulation and presentation systems.
    /// Mirrors the deterministic tick progression used in the legacy DOTS stack.
    /// </summary>
    public struct TimeState : IComponentData
    {
        public float FixedDeltaTime;
        public float CurrentSpeedMultiplier;
        public uint Tick;
        public bool IsPaused;
    }

    /// <summary>
    /// Optional authoring/config singleton that sets up <see cref="TimeState"/> at runtime.
    /// </summary>
    public struct TimeSettingsConfig : IComponentData
    {
        public float FixedDeltaTime;
        public float DefaultSpeedMultiplier;
        public bool PauseOnStart;
    }

    public static class TimeSettingsDefaults
    {
        public const float FixedDeltaTime = 1f / 60f;
        public const float DefaultSpeedMultiplier = 1f;
        public const bool PauseOnStart = false;

        public static TimeSettingsConfig CreateDefault() => new TimeSettingsConfig
        {
            FixedDeltaTime = FixedDeltaTime,
            DefaultSpeedMultiplier = DefaultSpeedMultiplier,
            PauseOnStart = PauseOnStart
        };
    }

    public enum RewindMode : byte
    {
        Record = 0,
        Playback = 1,
        CatchUp = 2
    }

    /// <summary>
    /// Tracks the current rewind / playback state for routing simulation groups.
    /// </summary>
    public struct RewindState : IComponentData
    {
        public RewindMode Mode;
        public uint StartTick;
        public uint TargetTick;
        public uint PlaybackTick;
        public float PlaybackTicksPerSecond;
        public sbyte ScrubDirection;
        public float ScrubSpeedMultiplier;
    }
}
