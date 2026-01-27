using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    // Note: This file contains obstacle grid components added in Phase B and Phase B Risk Mitigation.
    // Other perception components (SensorSignature, SenseCapability, etc.) are defined elsewhere.

    /// <summary>
    /// Relation classification for perceived entities.
    /// </summary>
    public enum PerceivedRelationKind : byte
    {
        Unknown = 0,
        Neutral = 1,
        Ally = 2,
        Hostile = 3
    }

    /// <summary>
    /// Flags describing how a perceived relation was determined.
    /// </summary>
    [System.Flags]
    public enum PerceivedRelationFlags : byte
    {
        None = 0,
        FromPersonal = 1 << 0,
        FromFaction = 1 << 1,
        FromCategory = 1 << 2,
        ForcedAlly = 1 << 3,
        ForcedHostile = 1 << 4
    }

    /// <summary>
    /// Cached perception state for an entity.
    /// </summary>
    public struct PerceptionState : IComponentData
    {
        public uint LastUpdateTick;
        public byte PerceivedCount;
        public byte HighestThreat;
        public Entity HighestThreatEntity;
        public Entity NearestEntity;
        public float NearestDistance;
    }

    /// <summary>
    /// Configuration for obstacle grid used for deterministic LOS fallback.
    /// Stored as singleton on spatial grid entity.
    /// </summary>
    public struct ObstacleGridConfig : IComponentData
    {
        /// <summary>Cell size (should match spatial grid cell size).</summary>
        public float CellSize;
        /// <summary>Height threshold above which a cell is considered blocking.</summary>
        public float ObstacleThreshold;
        /// <summary>Whether obstacle grid is enabled (0 = disabled, 1 = enabled).</summary>
        public byte Enabled;
    }

    /// <summary>
    /// Buffer element storing obstacle data for each grid cell.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ObstacleGridCell : IBufferElementData
    {
        /// <summary>Maximum blocking height in this cell.</summary>
        public float BlockingHeight;
        /// <summary>Last tick this cell was updated.</summary>
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Marker component for entities that should block LOS in obstacle grid.
    /// Added by ObstacleGridBootstrapSystem or via authoring.
    /// </summary>
    public struct ObstacleTag : IComponentData
    {
    }

    /// <summary>
    /// Optional explicit height override for obstacle entities.
    /// If not present, height is calculated from collider bounds or default value.
    /// </summary>
    public struct ObstacleHeight : IComponentData
    {
        public float Height;
    }

    /// <summary>
    /// Request component to trigger obstacle grid rebuild.
    /// Added to grid entity to request rebuild on next update.
    /// </summary>
    public struct ObstacleGridRebuildRequest : IComponentData
    {
    }
}
