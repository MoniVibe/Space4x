using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Enables console-based instrumentation for registry snapshots. Attach to a singleton to request logging.
    /// </summary>
    public struct RegistryConsoleInstrumentation : IComponentData
    {
        public const byte FlagLogOnlyOnChange = 1 << 0;

        /// <summary>
        /// Minimum number of ticks between log emissions. Zero logs every eligible update.
        /// </summary>
        public uint MinTickDelta;

        /// <summary>
        /// Tick when the last log was emitted.
        /// </summary>
        public uint LastLoggedTick;

        /// <summary>
        /// Registry directory version captured the last time a log was emitted.
        /// </summary>
        public uint LastDirectoryVersion;

        /// <summary>
        /// Behaviour flags (see Flag constants above).
        /// </summary>
        public byte Flags;

        public readonly bool ShouldLogOnlyOnChange => (Flags & FlagLogOnlyOnChange) != 0;
    }

    /// <summary>
    /// Captures a per-registry instrumentation snapshot for debug HUDs and telemetry streams.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RegistryInstrumentationSample : IBufferElementData
    {
        public RegistryHandle Handle;
        public RegistryHealthLevel HealthLevel;
        public RegistryHealthFlags HealthFlags;
        public int EntryCount;
        public uint Version;
        public uint LastUpdateTick;
        public uint SpatialVersion;
        public uint SpatialVersionDelta;
        public RegistryTelemetryKey TelemetryKey;
        public FixedString64Bytes Label;
    }

    /// <summary>
    /// Tracks instrumentation change detection for consumers.
    /// </summary>
    public struct RegistryInstrumentationState : IComponentData
    {
        public uint Version;
        public uint LastUpdateTick;
        public int SampleCount;
        public int HealthyCount;
        public int WarningCount;
        public int CriticalCount;
        public int FailureCount;
    }

    /// <summary>
    /// Continuity alert level for registries that depend on spatial data.
    /// </summary>
    public enum RegistryContinuityStatus : byte
    {
        Nominal = 0,
        Warning = 1,
        Failure = 2
    }

    /// <summary>
    /// Recorded when a registry's spatial continuity drifts from the published spatial sync state.
    /// Stored on the <see cref="RegistrySpatialSyncState"/> singleton.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct RegistryContinuityAlert : IBufferElementData
    {
        public RegistryHandle Handle;
        public RegistryContinuityStatus Status;
        public uint SpatialVersion;
        public uint RegistrySpatialVersion;
        public uint Delta;
        public RegistryHealthFlags Flags;
        public FixedString64Bytes Label;
    }

    /// <summary>
    /// Aggregated continuity state surfaced for debug display and telemetry.
    /// </summary>
    public struct RegistryContinuityState : IComponentData
    {
        public uint Version;
        public uint LastCheckTick;
        public int WarningCount;
        public int FailureCount;
    }
}
