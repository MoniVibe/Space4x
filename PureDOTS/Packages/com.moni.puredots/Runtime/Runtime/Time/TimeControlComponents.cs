using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Types of time control commands that can be issued.
    /// </summary>
    public enum TimeControlCommandType : byte
    {
        None = 0,
        Pause = 1,
        Resume = 2,
        StepTicks = 3,
        SetSpeed = 4,
        StartRewind = 5,
        StopRewind = 6,
        ScrubTo = 7,
        /// <summary>Add a timescale entry to the schedule.</summary>
        AddTimeScaleEntry = 8,
        /// <summary>Remove a timescale entry from the schedule.</summary>
        RemoveTimeScaleEntry = 9,
        /// <summary>Begin preview rewind - freeze world, start scrubbing ghosts.</summary>
        BeginPreviewRewind = 10,
        /// <summary>Update preview rewind scrub speed while scrubbing.</summary>
        UpdatePreviewRewindSpeed = 11,
        /// <summary>End scrub preview - freeze ghosts at current preview position.</summary>
        EndScrubPreview = 12,
        /// <summary>Commit rewind from preview - apply rewind to world state.</summary>
        CommitRewindFromPreview = 13,
        /// <summary>Cancel rewind preview - abort without changing world state.</summary>
        CancelRewindPreview = 14
    }

    /// <summary>
    /// Scope of a time control command.
    /// 
    /// SCOPE SEMANTICS:
    /// - Single-player uses: Global, LocalBubble
    /// - Multiplayer uses: Player, LocalBubble (Global limited/disabled)
    /// - Territory is reserved for future use
    /// </summary>
    public enum TimeControlScope : byte
    {
        /// <summary>Affects the entire simulation globally.</summary>
        Global = 0,
        /// <summary>Affects entities within a local time bubble.</summary>
        LocalBubble = 1,
        /// <summary>Affects entities within a territory/region.</summary>
        Territory = 2,
        /// <summary>Affects entities owned by a specific player (multiplayer only).</summary>
        Player = 3
    }

    /// <summary>
    /// Source of a time control command for tracking and debugging.
    /// </summary>
    public enum TimeControlSource : byte
    {
        /// <summary>Player input (UI buttons, keyboard).</summary>
        Player = 0,
        /// <summary>Miracle/divine intervention.</summary>
        Miracle = 1,
        /// <summary>Scenario/scripted event.</summary>
        Scenario = 2,
        /// <summary>Developer/debug tool.</summary>
        DevTool = 3,
        /// <summary>Technology/module effect.</summary>
        Technology = 4,
        /// <summary>System-generated (internal).</summary>
        System = 5
    }

    /// <summary>
    /// Buffer element for queuing time control commands.
    /// Processed by RewindCoordinatorSystem and TimeScaleCommandSystem.
    /// 
    /// SCOPE SEMANTICS:
    /// - Single-player uses: Scope = Global (for global commands) or LocalBubble (for bubble commands)
    /// - Single-player uses: PlayerId = 0 (TimePlayerIds.SinglePlayer)
    /// - Multiplayer will use: Scope = Player (for per-player commands) or LocalBubble, Global limited/disabled
    /// - Multiplayer will use: PlayerId = actual player ID, validated by server
    /// 
    /// MULTIPLAYER EXPECTATIONS:
    /// - Scope: In MP, Player scope commands affect only entities owned by PlayerId. Global scope may be restricted.
    /// - PlayerId: Must match the command issuer's player ID. Server validates this before processing.
    /// - Commands with invalid PlayerId are rejected in multiplayer mode.
    /// </summary>
    public struct TimeControlCommand : IBufferElementData
    {
        /// <summary>Type of command to execute.</summary>
        public TimeControlCommandType Type;
        /// <summary>Generic uint parameter (tick count, target tick, etc.).</summary>
        public uint UintParam;
        /// <summary>Generic float parameter (speed multiplier, etc.).</summary>
        public float FloatParam;
        /// <summary>
        /// Scope of the command (Global, LocalBubble, Territory, Player).
        /// In MP: Player scope requires valid PlayerId; Global may be restricted to server-only.
        /// </summary>
        public TimeControlScope Scope;
        /// <summary>Source of the command for tracking.</summary>
        public TimeControlSource Source;
        /// <summary>
        /// Player ID for multiplayer support (0 for SP or system).
        /// In MP: Must match command issuer's player ID. Server validates before processing.
        /// </summary>
        public byte PlayerId;
        /// <summary>Source entity ID (miracle ID, tech ID, etc.) for tracking origin.</summary>
        public uint SourceId;
        /// <summary>Priority for conflict resolution (higher wins).</summary>
        public byte Priority;
    }

    /// <summary>
    /// Tag component marking the time control singleton entity.
    /// </summary>
    public struct TimeControlSingletonTag : IComponentData { }

    /// <summary>
    /// Configuration for time control behavior.
    /// </summary>
    public struct TimeControlConfig : IComponentData
    {
        /// <summary>Speed multiplier for slow-motion mode.</summary>
        public float SlowMotionSpeed;
        /// <summary>Speed multiplier for fast-forward mode.</summary>
        public float FastForwardSpeed;
        /// <summary>Minimum allowed speed multiplier (default 0.01).</summary>
        public float MinSpeedMultiplier;
        /// <summary>Maximum allowed speed multiplier (default 16.0).</summary>
        public float MaxSpeedMultiplier;

        /// <summary>
        /// Creates a default configuration with standard speed limits.
        /// </summary>
        public static TimeControlConfig CreateDefault() => new TimeControlConfig
        {
            SlowMotionSpeed = 0.25f,
            FastForwardSpeed = 4.0f,
            MinSpeedMultiplier = 0.01f,
            MaxSpeedMultiplier = 16.0f
        };
    }

    /// <summary>
    /// Input state for time controls, populated by input systems.
    /// </summary>
    public struct TimeControlInputState : IComponentData
    {
        public uint SampleTick;
        public byte PauseToggleTriggered;
        public byte StepDownTriggered;
        public byte StepUpTriggered;
        public byte RewindPressedThisFrame;
        public byte EnterGhostPreview;
        public byte RewindSpeedLevel;
        public byte RewindHeld;
    }

    /// <summary>
    /// Constants for time control limits.
    /// </summary>
    public static class TimeControlLimits
    {
        /// <summary>Default minimum speed multiplier.</summary>
        public const float DefaultMinSpeed = 0.01f;
        /// <summary>Default maximum speed multiplier.</summary>
        public const float DefaultMaxSpeed = 16.0f;
        /// <summary>Default playback ticks per second during rewind.</summary>
        public const float DefaultPlaybackTicksPerSecond = 60.0f;
    }
}
