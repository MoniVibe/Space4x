using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Cached cell index and color assignment for sensor/comms scaling.
    /// Augments SpatialGridResidency with color and version tracking.
    /// </summary>
    public struct SensorCellIndex : IComponentData
    {
        /// <summary>Cell ID from spatial grid.</summary>
        public int CellId;

        /// <summary>Color assignment for phased updates (0-3 for 4-color map).</summary>
        public byte Color;

        /// <summary>Version when this index was last updated (matches SpatialGridState.Version).</summary>
        public uint Version;

        /// <summary>Morton key hash for deterministic coloring.</summary>
        public uint MortonKey;
    }

    /// <summary>
    /// Per-cell sensor event emitted during detection phase.
    /// Buffered per cell and processed during aggregation phase.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SensorCellEvent : IBufferElementData
    {
        /// <summary>Cell ID where event occurred.</summary>
        public int CellId;

        /// <summary>Detected entity.</summary>
        public Entity DetectedEntity;

        /// <summary>Detecting entity (sensor).</summary>
        public Entity SensorEntity;

        /// <summary>Channels that detected this entity.</summary>
        public PerceptionChannel DetectedChannels;

        /// <summary>Confidence of detection (0-1).</summary>
        public float Confidence;

        /// <summary>Distance from sensor to detected entity.</summary>
        public float Distance;

        /// <summary>Threat level of detected entity (0-255).</summary>
        public byte ThreatLevel;

        /// <summary>Faction ID of detected entity (if available).</summary>
        public int FactionId;

        /// <summary>Tick when event was emitted.</summary>
        public uint EmittedTick;

        /// <summary>Change version that triggered this event.</summary>
        public uint ChangeVersion;
    }

    /// <summary>
    /// Work entry for awareness aggregation.
    /// Per-thread buffer entries keyed by CellId.
    /// </summary>
    public struct AwarenessWorkEntry
    {
        public int CellId;
        public int EntityCount;
        public byte HighestThreat;
        public Entity HighestThreatEntity;
        public float NearestDistance;
        public Entity NearestEntity;
        public PerceptionChannel MaxThreatChannels;
        public byte MaxThreatPerChannel_Visual;
        public byte MaxThreatPerChannel_Hearing;
        public byte MaxThreatPerChannel_Smell;
        public byte MaxThreatPerChannel_EM;
        public byte MaxThreatPerChannel_Gravitic;
        public byte MaxThreatPerChannel_Exotic;
        public byte MaxThreatPerChannel_Paranormal;
        public int FactionEntityCount;
        public int HostileEntityCount;
        public int AllyEntityCount;
        public int NeutralEntityCount;
    }

    /// <summary>
    /// Aggregated awareness snapshot per cell.
    /// Consumed by hot-path systems instead of re-running expensive perception.
    /// </summary>
    public struct AwarenessCellSnapshot : IComponentData
    {
        /// <summary>Cell ID this snapshot represents.</summary>
        public int CellId;

        /// <summary>Total entities detected in this cell.</summary>
        public int EntityCount;

        /// <summary>Highest threat level among detected entities.</summary>
        public byte HighestThreat;

        /// <summary>Entity with highest threat.</summary>
        public Entity HighestThreatEntity;

        /// <summary>Distance to nearest detected entity.</summary>
        public float NearestDistance;

        /// <summary>Nearest detected entity.</summary>
        public Entity NearestEntity;

        /// <summary>Faction breakdown: total entities per faction.</summary>
        public int FactionEntityCount;

        /// <summary>Hostile entities in this cell.</summary>
        public int HostileEntityCount;

        /// <summary>Ally entities in this cell.</summary>
        public int AllyEntityCount;

        /// <summary>Neutral entities in this cell.</summary>
        public int NeutralEntityCount;

        /// <summary>Last tick this snapshot was updated.</summary>
        public uint LastUpdateTick;

        /// <summary>Version of this snapshot (for invalidation).</summary>
        public uint Version;
    }

    /// <summary>
    /// Maximum threat per channel per cell.
    /// Used for channel-specific threat assessment.
    /// </summary>
    public struct ThreatSnapshot : IComponentData
    {
        /// <summary>Cell ID this snapshot represents.</summary>
        public int CellId;

        /// <summary>Maximum threat per channel.</summary>
        public byte MaxThreat_Visual;
        public byte MaxThreat_Hearing;
        public byte MaxThreat_Smell;
        public byte MaxThreat_EM;
        public byte MaxThreat_Gravitic;
        public byte MaxThreat_Exotic;
        public byte MaxThreat_Paranormal;

        /// <summary>Channels with non-zero threat.</summary>
        public PerceptionChannel ThreatChannels;

        /// <summary>Last tick this snapshot was updated.</summary>
        public uint LastUpdateTick;

        /// <summary>Version of this snapshot (for invalidation).</summary>
        public uint Version;
    }

    /// <summary>
    /// Version tracking for awareness snapshots.
    /// Used to detect stale snapshots and trigger invalidation.
    /// </summary>
    public struct AwarenessCellSnapshotVersion : IComponentData
    {
        /// <summary>Current version of awareness snapshots.</summary>
        public uint Version;

        /// <summary>Last tick when version was incremented.</summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Cell coloring state for phased updates.
    /// Singleton on spatial grid entity.
    /// </summary>
    public struct SensorCellColoringState : IComponentData
    {
        /// <summary>Number of colors used (typically 4).</summary>
        public byte ColorCount;

        /// <summary>Current color being processed (0 to ColorCount-1).</summary>
        public byte CurrentColor;

        /// <summary>Last tick when coloring was updated.</summary>
        public uint LastUpdateTick;

        /// <summary>Version of coloring assignment.</summary>
        public uint Version;
    }

    /// <summary>
    /// Per-cell event version tracking.
    /// Used to skip cells that haven't changed since last aggregation.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SensorCellEventVersion : IBufferElementData
    {
        /// <summary>Cell ID.</summary>
        public int CellId;

        /// <summary>Version of events in this cell.</summary>
        public uint Version;

        /// <summary>Last processed version (for comparison).</summary>
        public uint LastProcessedVersion;
    }

    /// <summary>
    /// Instrumentation counters for sensor/comms scaling system.
    /// Singleton for telemetry and debugging.
    /// </summary>
    public struct SensorCommsScalingTelemetry : IComponentData
    {
        /// <summary>Total cells processed this tick.</summary>
        public int CellsProcessedThisTick;

        /// <summary>Total events emitted this tick.</summary>
        public int EventsEmittedThisTick;

        /// <summary>Total events aggregated this tick.</summary>
        public int EventsAggregatedThisTick;

        /// <summary>Cells skipped due to unchanged version.</summary>
        public int CellsSkippedThisTick;

        /// <summary>Last tick when telemetry was reset.</summary>
        public uint LastResetTick;
    }
}
