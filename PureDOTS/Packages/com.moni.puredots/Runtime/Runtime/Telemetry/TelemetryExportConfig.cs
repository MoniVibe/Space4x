using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Flags controlling which telemetry streams are exported.
    /// </summary>
    [System.Flags]
    public enum TelemetryExportFlags : byte
    {
        None = 0,
        IncludeTelemetryMetrics = 1 << 0,
        IncludeFrameTiming = 1 << 1,
        IncludeBehaviorTelemetry = 1 << 2,
        IncludeReplayEvents = 1 << 3,
        IncludeTelemetryEvents = 1 << 4
    }

    /// <summary>
    /// Detail levels for telemetry export filtering.
    /// </summary>
    public enum TelemetryExportLod : byte
    {
        Minimal = 0,
        Standard = 1,
        Full = 2
    }

    /// <summary>
    /// Flags selecting which gameplay loops to include.
    /// </summary>
    [System.Flags]
    public enum TelemetryLoopFlags : byte
    {
        None = 0,
        Extract = 1 << 0,
        Logistics = 1 << 1,
        Construction = 1 << 2,
        Exploration = 1 << 3,
        Combat = 1 << 4,
        Rewind = 1 << 5,
        Time = 1 << 6,
        All = Extract | Logistics | Construction | Exploration | Combat | Rewind | Time
    }

    /// <summary>
    /// Configuration singleton consumed by <see cref="TelemetryExportSystem"/>.
    /// </summary>
    public struct TelemetryExportConfig : IComponentData
    {
        /// <summary>Absolute or project-relative path to the NDJSON export file.</summary>
        public FixedString512Bytes OutputPath;
        /// <summary>Identifier associated with the current run (auto-generated when empty).</summary>
        public FixedString128Bytes RunId;
        /// <summary>Active export flags.</summary>
        public TelemetryExportFlags Flags;
        /// <summary>Whether exporting is enabled.</summary>
        public byte Enabled;
        /// <summary>Tick cadence for emitting schema metrics (0 uses default).</summary>
        public uint CadenceTicks;
        /// <summary>Telemetry level of detail.</summary>
        public TelemetryExportLod Lod;
        /// <summary>Gameplay loops to include.</summary>
        public TelemetryLoopFlags Loops;
        /// <summary>Maximum telemetry events to emit per tick.</summary>
        public ushort MaxEventsPerTick;
        /// <summary>Maximum bytes to write to the telemetry output file (0 = unlimited).</summary>
        public ulong MaxOutputBytes;
        /// <summary>Version counter so systems can detect config changes.</summary>
        public uint Version;

        public static TelemetryExportConfig CreateDisabled()
        {
            return new TelemetryExportConfig
            {
                OutputPath = default,
                RunId = default,
                Flags = TelemetryExportFlags.IncludeTelemetryMetrics |
                        TelemetryExportFlags.IncludeFrameTiming |
                        TelemetryExportFlags.IncludeBehaviorTelemetry |
                        TelemetryExportFlags.IncludeReplayEvents |
                        TelemetryExportFlags.IncludeTelemetryEvents,
                Enabled = 0,
                CadenceTicks = 30,
                Lod = TelemetryExportLod.Minimal,
                Loops = TelemetryLoopFlags.All,
                MaxEventsPerTick = 64,
                MaxOutputBytes = 0,
                Version = 1
            };
        }
    }

    /// <summary>
    /// Scenario-driven overrides for telemetry export configuration.
    /// </summary>
    public struct TelemetryScenarioOverride
    {
        public sbyte EnabledOverride;
        public FixedString512Bytes OutputPath;
        public FixedString128Bytes RunId;
        public TelemetryExportFlags Flags;
        public uint CadenceTicks;
        public sbyte LodOverride;
        public TelemetryLoopFlags Loops;
        public ushort MaxEventsPerTick;
        public ulong MaxOutputBytes;

        public static TelemetryScenarioOverride CreateSentinel()
        {
            return new TelemetryScenarioOverride
            {
                EnabledOverride = -1,
                OutputPath = default,
                RunId = default,
                Flags = TelemetryExportFlags.None,
                CadenceTicks = 0,
                LodOverride = -1,
                Loops = TelemetryLoopFlags.None,
                MaxEventsPerTick = 0,
                MaxOutputBytes = 0
            };
        }
    }

    /// <summary>
    /// Marker component carrying scenario telemetry overrides.
    /// </summary>
    public struct TelemetryScenarioOverrideComponent : IComponentData
    {
        public TelemetryScenarioOverride Value;
    }
}
