using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Simulation mode for the time system.
    /// Determines how time commands are processed and validated.
    /// </summary>
    public enum TimeSimulationMode : byte
    {
        /// <summary>Single-player mode. All commands execute immediately.</summary>
        SinglePlayer = 0,
        /// <summary>Multiplayer server mode. Validates and broadcasts commands.</summary>
        MultiplayerServer = 1,
        /// <summary>Multiplayer client mode. Commands require server authority.</summary>
        MultiplayerClient = 2
    }

    /// <summary>
    /// Multiplayer mode for time system features.
    /// Determines which time features are allowed in multiplayer sessions.
    /// </summary>
    public enum TimeMultiplayerMode : byte
    {
        /// <summary>Single-player only mode. Full time features enabled.</summary>
        SinglePlayerOnly = 0,
        /// <summary>Multiplayer mode with rewind disabled. Only forward/pause/scale allowed.</summary>
        MP_DisableRewind,
        /// <summary>Multiplayer mode allowing snapshots only (coarse rollback to agreed snapshots).</summary>
        MP_SnapshotsOnly,
        /// <summary>Placeholder for future experimental multiplayer time features.</summary>
        MP_FullExperimental
    }

    /// <summary>
    /// Feature flags for the time system.
    /// Allows enabling/disabling features for backward compatibility or performance.
    /// </summary>
    public struct TimeSystemFeatureFlags : IComponentData
    {
        /// <summary>Current simulation mode (SinglePlayer, MultiplayerServer, MultiplayerClient).</summary>
        public TimeSimulationMode SimulationMode;
        /// <summary>Whether global rewind is enabled (true in SP, false in MP).</summary>
        public bool EnableGlobalRewind;
        /// <summary>Whether local bubble rewind is enabled (SP: true, MP: reserved/experimental).</summary>
        public bool EnableLocalBubbleRewind;
        /// <summary>Whether world snapshots are enabled (SP: true, MP: reserved).</summary>
        public bool EnableWorldSnapshots;
        /// <summary>Whether the timescale scheduling system is enabled.</summary>
        public bool EnableTimeScaleScheduling;
        /// <summary>Whether the global snapshot system is enabled.</summary>
        public bool EnableGlobalSnapshots;
        /// <summary>Whether per-component history recording is enabled.</summary>
        public bool EnableComponentHistory;
        /// <summary>Whether local time bubbles are enabled.</summary>
        public bool EnableTimeBubbles;
        /// <summary>Whether local rewind (bubble-based rewind) is enabled.</summary>
        public bool EnableLocalRewind;
        /// <summary>Whether stasis mode is enabled.</summary>
        public bool EnableStasis;
        /// <summary>Whether multiplayer-friendly features are enforced.</summary>
        public bool EnforceMultiplayerCompatibility;
        /// <summary>Whether to use legacy speed clamping (0.1-5.0 instead of 0.01-16.0).</summary>
        public bool UseLegacySpeedLimits;
        /// <summary>
        /// Whether this is a multiplayer session (true) or single-player (false).
        /// If true, time control commands *must* come from the server.
        /// </summary>
        public bool IsMultiplayerSession;
        /// <summary>
        /// Multiplayer mode determining which time features are allowed.
        /// For MP we expect:
        /// - Server is authoritative for TickTimeState, TimeState and RewindState.
        /// - Clients send TimeControlCommand with Scope=Player, PlayerId set.
        /// </summary>
        public TimeMultiplayerMode MultiplayerMode;

        /// <summary>
        /// Creates default feature flags with all features enabled for single-player.
        /// </summary>
        public static TimeSystemFeatureFlags CreateDefault() => new TimeSystemFeatureFlags
        {
            SimulationMode = TimeSimulationMode.SinglePlayer,
            EnableGlobalRewind = true,
            EnableLocalBubbleRewind = true,
            EnableWorldSnapshots = true,
            EnableTimeScaleScheduling = true,
            EnableGlobalSnapshots = true,
            EnableComponentHistory = true,
            EnableTimeBubbles = true,
            EnableLocalRewind = false, // Disabled by default as it's experimental
            EnableStasis = true,
            EnforceMultiplayerCompatibility = false,
            UseLegacySpeedLimits = false,
            IsMultiplayerSession = false,
            MultiplayerMode = TimeMultiplayerMode.SinglePlayerOnly
        };

        /// <summary>
        /// Creates minimal feature flags for basic time control only.
        /// </summary>
        public static TimeSystemFeatureFlags CreateMinimal() => new TimeSystemFeatureFlags
        {
            SimulationMode = TimeSimulationMode.SinglePlayer,
            EnableGlobalRewind = false,
            EnableLocalBubbleRewind = false,
            EnableWorldSnapshots = false,
            EnableTimeScaleScheduling = false,
            EnableGlobalSnapshots = false,
            EnableComponentHistory = false,
            EnableTimeBubbles = false,
            EnableLocalRewind = false,
            EnableStasis = false,
            EnforceMultiplayerCompatibility = false,
            UseLegacySpeedLimits = true,
            IsMultiplayerSession = false,
            MultiplayerMode = TimeMultiplayerMode.SinglePlayerOnly
        };

        /// <summary>
        /// Creates feature flags optimized for multiplayer server.
        /// </summary>
        public static TimeSystemFeatureFlags CreateMultiplayer() => new TimeSystemFeatureFlags
        {
            SimulationMode = TimeSimulationMode.MultiplayerServer,
            EnableGlobalRewind = false,
            EnableLocalBubbleRewind = false, // Reserved/experimental in MP
            EnableWorldSnapshots = false, // Reserved in MP
            EnableTimeScaleScheduling = true,
            EnableGlobalSnapshots = true,
            EnableComponentHistory = true,
            EnableTimeBubbles = false, // Local bubbles disabled in MP
            EnableLocalRewind = false,
            EnableStasis = false,
            EnforceMultiplayerCompatibility = true,
            UseLegacySpeedLimits = false,
            IsMultiplayerSession = true,
            MultiplayerMode = TimeMultiplayerMode.MP_DisableRewind
        };

        /// <summary>
        /// Creates feature flags for multiplayer client.
        /// </summary>
        public static TimeSystemFeatureFlags CreateMultiplayerClient() => new TimeSystemFeatureFlags
        {
            SimulationMode = TimeSimulationMode.MultiplayerClient,
            EnableGlobalRewind = false,
            EnableLocalBubbleRewind = false,
            EnableWorldSnapshots = false,
            EnableTimeScaleScheduling = true,
            EnableGlobalSnapshots = true,
            EnableComponentHistory = true,
            EnableTimeBubbles = false,
            EnableLocalRewind = false,
            EnableStasis = false,
            EnforceMultiplayerCompatibility = true,
            UseLegacySpeedLimits = false,
            IsMultiplayerSession = true,
            MultiplayerMode = TimeMultiplayerMode.MP_DisableRewind
        };

        /// <summary>
        /// Creates feature flags for Godgame with miracles.
        /// </summary>
        public static TimeSystemFeatureFlags CreateGodgame() => new TimeSystemFeatureFlags
        {
            SimulationMode = TimeSimulationMode.SinglePlayer,
            EnableGlobalRewind = true,
            EnableLocalBubbleRewind = true,
            EnableWorldSnapshots = true,
            EnableTimeScaleScheduling = true,
            EnableGlobalSnapshots = true,
            EnableComponentHistory = true,
            EnableTimeBubbles = true,
            EnableLocalRewind = false, // Can enable for advanced miracles
            EnableStasis = true,
            EnforceMultiplayerCompatibility = false,
            UseLegacySpeedLimits = false,
            IsMultiplayerSession = false,
            MultiplayerMode = TimeMultiplayerMode.SinglePlayerOnly
        };

        /// <summary>
        /// Creates feature flags for Space4X.
        /// </summary>
        public static TimeSystemFeatureFlags CreateSpace4X() => new TimeSystemFeatureFlags
        {
            SimulationMode = TimeSimulationMode.SinglePlayer,
            EnableGlobalRewind = true,
            EnableLocalBubbleRewind = true,
            EnableWorldSnapshots = true,
            EnableTimeScaleScheduling = true,
            EnableGlobalSnapshots = true,
            EnableComponentHistory = true,
            EnableTimeBubbles = true,
            EnableLocalRewind = false,
            EnableStasis = true,
            EnforceMultiplayerCompatibility = false,
            UseLegacySpeedLimits = false,
            IsMultiplayerSession = false,
            MultiplayerMode = TimeMultiplayerMode.SinglePlayerOnly
        };
    }

    /// <summary>
    /// Tag component marking that time system features have been configured.
    /// </summary>
    public struct TimeSystemFeaturesConfiguredTag : IComponentData { }
}

