using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Health status levels for registry validation and alerting.
    /// </summary>
    public enum RegistryHealthLevel : byte
    {
        /// <summary>All metrics within healthy thresholds.</summary>
        Healthy = 0,
        
        /// <summary>Some metrics approaching warning thresholds but operational.</summary>
        Warning = 1,
        
        /// <summary>Metrics exceeding thresholds; may impact gameplay.</summary>
        Critical = 2,
        
        /// <summary>Registry in severely degraded state; gameplay likely impacted.</summary>
        Failure = 3
    }

    /// <summary>
    /// Singleton configuration defining acceptable thresholds for registry health metrics.
    /// Attach to a config entity to override defaults.
    /// </summary>
    public struct RegistryHealthThresholds : IComponentData
    {
        /// <summary>
        /// Maximum acceptable tick age before an entry is considered stale (0 = no limit).
        /// </summary>
        public uint MaxStaleTickAge;

        /// <summary>
        /// Warning threshold: percentage of stale entries (0.0-1.0).
        /// </summary>
        public float StaleEntryWarningRatio;

        /// <summary>
        /// Critical threshold: percentage of stale entries (0.0-1.0).
        /// </summary>
        public float StaleEntryCriticalRatio;

        /// <summary>
        /// Maximum acceptable version delta between registry and spatial grid before warning.
        /// </summary>
        public uint SpatialVersionMismatchWarning;

        /// <summary>
        /// Maximum acceptable version delta between registry and spatial grid before critical.
        /// </summary>
        public uint SpatialVersionMismatchCritical;

        /// <summary>
        /// Minimum acceptable update frequency in ticks (0 = no minimum).
        /// </summary>
        public uint MinUpdateFrequencyTicks;

        /// <summary>
        /// Maximum acceptable gap between registry and directory versions before warning.
        /// </summary>
        public uint DirectoryVersionMismatchWarning;

        /// <summary>
        /// Creates default thresholds suitable for most scenarios.
        /// </summary>
        public static RegistryHealthThresholds CreateDefaults()
        {
            return new RegistryHealthThresholds
            {
                MaxStaleTickAge = 300,                    // 5 seconds at 60 ticks/sec
                StaleEntryWarningRatio = 0.15f,           // 15% stale entries triggers warning
                StaleEntryCriticalRatio = 0.35f,          // 35% stale entries triggers critical
                SpatialVersionMismatchWarning = 5,        // Allow up to 5 version delta
                SpatialVersionMismatchCritical = 15,      // Critical at 15 version delta
                MinUpdateFrequencyTicks = 10,             // Registry should update at least every 10 ticks
                DirectoryVersionMismatchWarning = 10      // Directory-registry delta warning
            };
        }
    }

    /// <summary>
    /// Per-registry health metrics computed by <see cref="PureDOTS.Systems.RegistryHealthSystem"/>.
    /// Attached to each registry entity to track its current health state.
    /// </summary>
    public struct RegistryHealth : IComponentData
    {
        /// <summary>
        /// Overall health level computed from current metrics and thresholds.
        /// </summary>
        public RegistryHealthLevel HealthLevel;

        /// <summary>
        /// Number of entries considered stale based on tick age.
        /// </summary>
        public int StaleEntryCount;

        /// <summary>
        /// Ratio of stale entries to total entries (0.0-1.0).
        /// </summary>
        public float StaleEntryRatio;

        /// <summary>
        /// Version delta between this registry and the spatial grid (0 if no spatial dependency).
        /// </summary>
        public uint SpatialVersionDelta;

        /// <summary>
        /// Tick count since last registry update.
        /// </summary>
        public uint TicksSinceLastUpdate;

        /// <summary>
        /// Version delta between this registry's metadata version and directory version.
        /// </summary>
        public uint DirectoryVersionDelta;

        /// <summary>
        /// Total entry count at time of last health check.
        /// </summary>
        public int TotalEntryCount;

        /// <summary>
        /// Tick when this health check was computed.
        /// </summary>
        public uint LastHealthCheckTick;

        /// <summary>
        /// Flags indicating which specific health checks failed (for debugging).
        /// </summary>
        public RegistryHealthFlags FailureFlags;

        /// <summary>
        /// Returns true if this registry has spatial dependencies (non-zero spatial version delta is meaningful).
        /// </summary>
        public readonly bool HasSpatialDependency => SpatialVersionDelta > 0;

        /// <summary>
        /// Returns true if health level indicates operational concerns.
        /// </summary>
        public readonly bool IsHealthy => HealthLevel == RegistryHealthLevel.Healthy;

        /// <summary>
        /// Returns true if health level requires attention.
        /// </summary>
        public readonly bool NeedsAttention => HealthLevel >= RegistryHealthLevel.Warning;
    }

    /// <summary>
    /// Bit flags indicating which specific health checks failed.
    /// </summary>
    [System.Flags]
    public enum RegistryHealthFlags : byte
    {
        None = 0,
        StaleEntriesWarning = 1 << 0,
        StaleEntriesCritical = 1 << 1,
        SpatialMismatchWarning = 1 << 2,
        SpatialMismatchCritical = 1 << 3,
        UpdateFrequencyWarning = 1 << 4,
        DirectoryMismatchWarning = 1 << 5,
        SpatialContinuityMissing = 1 << 6,
        DefinitionMismatch = 1 << 7
    }

    /// <summary>
    /// Singleton component requesting periodic registry health checks and telemetry emission.
    /// Attach to enable health monitoring.
    /// </summary>
    public struct RegistryHealthMonitoring : IComponentData
    {
        /// <summary>
        /// Minimum ticks between health checks (0 = check every frame).
        /// </summary>
        public uint MinCheckIntervalTicks;

        /// <summary>
        /// Tick when last health check was performed.
        /// </summary>
        public uint LastCheckTick;

        /// <summary>
        /// If true, emit telemetry metrics for all registries each check.
        /// </summary>
        public bool EmitTelemetry;

        /// <summary>
        /// If true, log warnings to console when registries degrade to Warning or worse.
        /// </summary>
        public bool LogWarnings;

        /// <summary>
        /// Overall worst health level across all registries.
        /// </summary>
        public RegistryHealthLevel WorstHealthLevel;

        /// <summary>
        /// Total number of registries in warning or worse state.
        /// </summary>
        public int UnhealthyRegistryCount;

        public static RegistryHealthMonitoring CreateDefaults()
        {
            return new RegistryHealthMonitoring
            {
                MinCheckIntervalTicks = 30,     // Check every 30 ticks (~0.5 sec at 60 tps)
                LastCheckTick = 0,
                EmitTelemetry = true,
                LogWarnings = true,
                WorstHealthLevel = RegistryHealthLevel.Healthy,
                UnhealthyRegistryCount = 0
            };
        }
    }
}


