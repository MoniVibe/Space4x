using Unity.Entities;

namespace PureDOTS.Runtime.Mining
{
    /// <summary>
    /// Diagnostics component for mining systems.
    /// Only populated in editor builds or when explicitly enabled via config.
    /// </summary>
#if UNITY_EDITOR
    public struct MiningDiagnostics : IComponentData
    {
        /// <summary>
        /// Number of active MiningSession components currently in the world.
        /// </summary>
        public int ActiveSessionCount;

        /// <summary>
        /// Total resources mined per second (rolling average).
        /// </summary>
        public float MinedPerSecond;

        /// <summary>
        /// Number of times miners had to reset state due to invalid source.
        /// </summary>
        public int InvalidSourceResets;

        /// <summary>
        /// Number of times miners had to reset state due to invalid carrier.
        /// </summary>
        public int InvalidCarrierResets;

        /// <summary>
        /// Number of times miners had to reset state due to physics disruptions (pickup/throw).
        /// </summary>
        public int PhysicsDisruptionResets;

        /// <summary>
        /// Total resources mined since last reset (for per-second calculation).
        /// </summary>
        public float TotalMined;

        /// <summary>
        /// Time accumulator for per-second calculations.
        /// </summary>
        public float TimeAccumulator;

        /// <summary>
        /// Last update tick (for tracking).
        /// </summary>
        public uint LastUpdateTick;
    }
#endif

    /// <summary>
    /// Configuration for enabling/disabling mining diagnostics.
    /// </summary>
    public struct MiningDiagnosticsConfig : IComponentData
    {
        /// <summary>
        /// Whether diagnostics are enabled (default: true in editor, false in builds).
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// Interval in seconds for updating per-second metrics.
        /// </summary>
        public float UpdateIntervalSeconds;
    }
}

























