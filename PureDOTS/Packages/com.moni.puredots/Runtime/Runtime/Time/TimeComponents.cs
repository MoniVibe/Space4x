using System.Runtime.InteropServices;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// High-level time state singleton component.
    /// 
    /// DESIGN INVARIANT: There is exactly one TimeState singleton in a world.
    /// DESIGN INVARIANT: TimeState.Tick is monotonically increasing in real time and is the canonical "world time index".
    /// DESIGN INVARIANT: Rewind is always expressed as playback over history, NOT by decrementing Tick.
    /// DESIGN INVARIANT: All history and snapshots are keyed by Tick, not modified by rewind operations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeState : IComponentData
    {
        /// <summary>Current simulation tick (monotonically increasing, canonical world time index).</summary>
        public uint Tick;
        /// <summary>Fixed delta time per tick (not scaled by speed).</summary>
        public float DeltaTime;
        /// <summary>Delta time in seconds (alias for DeltaTime to aid migration).</summary>
        public float DeltaSeconds;
        /// <summary>Elapsed simulation time (Tick * FixedDeltaTime).</summary>
        public float ElapsedTime;
        /// <summary>World time in seconds (Tick * FixedDeltaTime).</summary>
        public float WorldSeconds;
        /// <summary>Whether the simulation is currently paused.</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool IsPaused;
        /// <summary>Base fixed timestep (e.g., 1/60 seconds).</summary>
        public float FixedDeltaTime;
        /// <summary>Current speed multiplier (0.01-16.0, configured via ScriptableObjects).</summary>
        public float CurrentSpeedMultiplier;
    }

    /// <summary>
    /// Canonical tick time state singleton component.
    /// 
    /// DESIGN INVARIANT: There is exactly one TickTimeState singleton in a world.
    /// DESIGN INVARIANT: TickTimeState.Tick is monotonically increasing in real time and is the canonical tick source.
    /// DESIGN INVARIANT: Rewind operations do NOT decrement Tick; they use playback over history instead.
    /// DESIGN INVARIANT: All history and snapshots are keyed by Tick, providing an index over time.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TickTimeState : IComponentData
    {
        /// <summary>Current simulation tick (monotonically increasing, canonical tick source).</summary>
        public uint Tick;
        /// <summary>Base fixed timestep (e.g., 1/60 seconds).</summary>
        public float FixedDeltaTime;
        /// <summary>Current speed multiplier (0.01-16.0, configured via ScriptableObjects).</summary>
        public float CurrentSpeedMultiplier;
        /// <summary>Target tick for catch-up operations.</summary>
        public uint TargetTick;
        /// <summary>Whether the simulation is currently paused.</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool IsPaused;
        /// <summary>Whether the simulation is currently playing (not paused).</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool IsPlaying;
        /// <summary>World time in seconds (Tick * FixedDeltaTime).</summary>
        public float WorldSeconds;
    }

    /// <summary>
    /// Canonical time context overlay for view/present/target tick semantics.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeContext : IComponentData
    {
        /// <summary>Monotonic max recorded tick (never decreases).</summary>
        public uint PresentTick;
        /// <summary>Tick currently being simulated or displayed.</summary>
        public uint ViewTick;
        /// <summary>Target tick for rewind/scrub.</summary>
        public uint TargetTick;
        /// <summary>Base fixed timestep (seconds per tick).</summary>
        public float FixedDeltaTime;
        /// <summary>Whether simulation is paused.</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool IsPaused;
        /// <summary>Speed multiplier applied to tick rate.</summary>
        public float SpeedMultiplier;
        /// <summary>Current rewind mode.</summary>
        public RewindMode Mode;
    }

    /// <summary>
    /// Fixed-step interpolation alpha for presentation (0..1 between previous/current tick).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FixedStepInterpolationState : IComponentData
    {
        public float Alpha;
    }
}
