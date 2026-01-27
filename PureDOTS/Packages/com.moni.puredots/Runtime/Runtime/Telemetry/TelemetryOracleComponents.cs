using PureDOTS.Runtime.Space;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Aggregated telemetry counters collected between export cadence ticks.
    /// </summary>
    public struct TelemetryOracleAccumulator : IComponentData
    {
        public uint SampleTicks;
        public float SampleSeconds;
        public uint WorkAvailableCount;
        public uint IdleWithWorkCount;
        public uint IntentUpdates;
        public uint IntentSamples;
        public ulong IntentAgeSumTicks;
        public uint IntentAgeMaxTicks;
        public uint MoveStuckTicks;
        public uint MoveModeFlipCount;
        public uint CollisionDamageEvents;
        public float PowerDeficitSeconds;
        public float PowerMinSoc;
        public byte PowerSocInitialized;
        public uint ModuleSpoolTransitions;
        public float ModuleNotReadySeconds;

        public static TelemetryOracleAccumulator CreateDefault()
        {
            return new TelemetryOracleAccumulator
            {
                PowerMinSoc = 1f,
                PowerSocInitialized = 0
            };
        }
    }

    /// <summary>
    /// Latency samples captured between export ticks to estimate quantiles.
    /// </summary>
    [InternalBufferCapacity(256)]
    public struct TelemetryOracleLatencySample : IBufferElementData
    {
        public uint Value;
    }

    /// <summary>
    /// Per-entity movement mode tracking to detect flip counts.
    /// </summary>
    public struct TelemetryOracleMovementModeState : IComponentData
    {
        public byte Initialized;
        public byte LastMode;
    }

    /// <summary>
    /// Per-module state tracking to detect spool transitions.
    /// </summary>
    public struct TelemetryOracleModuleState : IComponentData
    {
        public byte Initialized;
        public ModuleState LastState;
    }
}
