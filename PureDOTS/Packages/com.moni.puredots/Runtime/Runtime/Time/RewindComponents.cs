using System;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Defines the current mode of the rewind system.
    /// Canonical baseline: Play / Paused / Rewind / Step.
    /// Legacy aliases remain for compatibility.
    /// </summary>
    public enum RewindMode : byte
    {
        Play = 0,
        Paused = 1,
        Rewind = 2,
        Step = 3,

        // Legacy aliases (mapped to canonical values)
        Record = Play,
        Playback = Rewind,
        CatchUp = Step,
        Idle = Paused
    }

    /// <summary>
    /// Classification tier for how a component/system handles rewind.
    /// Determines whether state is recorded, derived, or ignored during rewind.
    /// </summary>
    public enum RewindTier : byte
    {
        /// <summary>Never rewound, re-derived or ignored. Pure VFX, particles, UI-only stuff.</summary>
        None = 0,
        /// <summary>Deterministic from seed + time; recompute on rewind. Galaxy orbits, wind/weather fields.</summary>
        Derived = 1,
        /// <summary>Coarse snapshot at reduced frequency. Fire/epidemic cells, biome state, economic aggregates.</summary>
        SnapshotLite = 2,
        /// <summary>Full history for critical state. Combat stats, positions, AI phase, orders, inventories.</summary>
        SnapshotFull = 3
    }

    /// <summary>
    /// Direction of scrubbing through time.
    /// </summary>
    public enum ScrubDirection : byte
    {
        None = 0,
        Forward = 1,
        Backward = 2
    }

    /// <summary>
    /// Singleton component tracking the global rewind/playback state.
    /// Baseline fields support play/pause/rewind/step and minimal history settings.
    /// View/current ticks live in TimeState/TimeContext (not here).
    /// </summary>
    public struct RewindState : IComponentData
    {
        // Canonical baseline
        public RewindMode Mode;
        public int TargetTick;
        public float TickDuration;
        public int MaxHistoryTicks;
        public byte PendingStepTicks;
    }

    /// <summary>
    /// Legacy/compat rewind fields retained for existing call sites.
    /// </summary>
    [Obsolete("Use TimeContext and RewindState instead. Will be removed in v2.0.")]
    public struct RewindLegacyState : IComponentData
    {
        public float PlaybackSpeed;
        public int CurrentTick;
        public uint StartTick;
        public uint PlaybackTick;
        public float PlaybackTicksPerSecond;
        public ScrubDirection ScrubDirection;
        public float ScrubSpeedMultiplier;
        public uint RewindWindowTicks;
        public RewindTrackId ActiveTrack;
    }

    /// <summary>
    /// Configuration for seeding RewindState at bootstrap.
    /// </summary>
    public struct RewindConfig : IComponentData
    {
        public float TickDuration;
        public int MaxHistoryTicks;
        public RewindMode InitialMode;
    }

    /// <summary>
    /// Phase of the preview-based rewind system.
    /// Used for preview/scrub rewind where the world stays frozen while ghosts preview the rewind.
    /// </summary>
    public enum RewindPhase : byte
    {
        /// <summary>Normal time, no rewind preview active.</summary>
        Inactive = 0,
        /// <summary>Holding rewind, ghosts scrub backwards through time.</summary>
        ScrubbingPreview = 1,
        /// <summary>Released rewind key, ghosts paused at preview position, world frozen.</summary>
        FrozenPreview = 2,
        /// <summary>One-shot: apply rewind to world, then transition back to Inactive.</summary>
        CommitPlayback = 3
    }

    /// <summary>
    /// Singleton component tracking the preview-based rewind control state.
    /// Manages the preview/scrub rewind flow where the world stays frozen at PresentTickAtStart
    /// while ghosts preview different time positions via PreviewTick.
    /// The RewindControlSystem is the authoritative writer for this component.
    /// </summary>
    public struct RewindControlState : IComponentData
    {
        /// <summary>Current rewind phase (Inactive, ScrubbingPreview, FrozenPreview, CommitPlayback).</summary>
        public RewindPhase Phase;
        /// <summary>Global tick when preview started (current "present" at the moment rewind began).</summary>
        /// <remarks>This is the anchor point - if we cancel, we resume from here.</remarks>
        public int PresentTickAtStart;
        /// <summary>The tick ghosts are previewing (scrub position).</summary>
        /// <remarks>During ScrubbingPreview, this updates based on ScrubSpeed. During FrozenPreview, it stays fixed.</remarks>
        public int PreviewTick;
        /// <summary>Rewind speed multiplier (1-4x, can be float).</summary>
        /// <remarks>Controls how fast ghosts scrub through history during ScrubbingPreview.</remarks>
        public float ScrubSpeed;
    }

    /// <summary>
    /// Component marking an entity's rewind importance tier.
    /// Attach at component type level (design-time contract) or optionally at entity level.
    /// Determines snapshot frequency and playback strategy for this entity.
    /// </summary>
    public struct RewindImportance : IComponentData
    {
        /// <summary>Rewind tier for this entity (None, Derived, SnapshotLite, SnapshotFull).</summary>
        public RewindTier Tier;
    }
}
