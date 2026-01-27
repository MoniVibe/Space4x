using PureDOTS.Runtime.Perception;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Detection types for sensor results.
    /// </summary>
    public enum DetectionType : byte
    {
        /// <summary>Visual detection (line of sight).</summary>
        Sight = 0,
        /// <summary>Auditory detection (sound).</summary>
        Sound = 1,
        /// <summary>Olfactory detection (smell).</summary>
        Smell = 2,
        /// <summary>Proximity detection (nearby entities).</summary>
        Proximity = 3,
        /// <summary>Radar/sensor sweep (mechanical).</summary>
        Radar = 4,
        /// <summary>Magical/psychic detection.</summary>
        Psychic = 5
    }

    /// <summary>
    /// Flags for detection masks.
    /// </summary>
    public static class DetectionMask
    {
        public const byte Sight = 1 << 0;
        public const byte Sound = 1 << 1;
        public const byte Smell = 1 << 2;
        public const byte Proximity = 1 << 3;
        public const byte Radar = 1 << 4;
        public const byte Psychic = 1 << 5;
        public const byte All = 0xFF;
    }

    /// <summary>
    /// Detected entity result stored in buffer.
    /// Game-agnostic: works for villagers, ships, creatures, etc.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DetectedEntity : IBufferElementData
    {
        /// <summary>
        /// The detected entity.
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Distance to target.
        /// </summary>
        public float Distance;

        /// <summary>
        /// Direction to target (normalized).
        /// </summary>
        public float3 Direction;

        /// <summary>
        /// How this entity was detected.
        /// </summary>
        public DetectionType DetectionType;

        /// <summary>
        /// Strength of detection (0-1, 1 = certain).
        /// </summary>
        public float Confidence;

        /// <summary>
        /// Tick when detected.
        /// </summary>
        public uint DetectedAtTick;

        /// <summary>
        /// Target's estimated threat level (0-255).
        /// </summary>
        public byte ThreatLevel;

        /// <summary>
        /// Target's relationship (-128 = enemy, 0 = neutral, +127 = ally).
        /// </summary>
        public sbyte Relationship;

        /// <summary>
        /// Relation classification for the detected entity.
        /// </summary>
        public PerceivedRelationKind RelationKind;

        /// <summary>
        /// Flags describing how the relation was derived.
        /// </summary>
        public PerceivedRelationFlags RelationFlags;
    }

    /// <summary>
    /// Sensor configuration component.
    /// Attach to any entity that should detect others.
    /// </summary>
    public struct SensorConfig : IComponentData
    {
        /// <summary>
        /// Maximum detection range.
        /// </summary>
        public float Range;

        /// <summary>
        /// Field of view in degrees (360 = omnidirectional).
        /// </summary>
        public float FieldOfView;

        /// <summary>
        /// Bitmask of enabled detection types.
        /// </summary>
        public byte DetectionMask;

        /// <summary>
        /// Minimum time between sensor updates (seconds).
        /// </summary>
        public float UpdateInterval;

        /// <summary>
        /// Maximum entities to track simultaneously.
        /// </summary>
        public byte MaxTrackedTargets;

        /// <summary>
        /// Detection capability flags.
        /// </summary>
        public SensorCapabilityFlags Flags;

        /// <summary>
        /// Creates default sensor config.
        /// </summary>
        public static SensorConfig Default => new SensorConfig
        {
            Range = 50f,
            FieldOfView = 120f,
            DetectionMask = AI.DetectionMask.Sight | AI.DetectionMask.Sound,
            UpdateInterval = 0.5f,
            MaxTrackedTargets = 8,
            Flags = SensorCapabilityFlags.None
        };

        /// <summary>
        /// Creates omnidirectional sensor.
        /// </summary>
        public static SensorConfig Omnidirectional(float range) => new SensorConfig
        {
            Range = range,
            FieldOfView = 360f,
            DetectionMask = AI.DetectionMask.Proximity,
            UpdateInterval = 0.25f,
            MaxTrackedTargets = 16,
            Flags = SensorCapabilityFlags.None
        };
    }

    /// <summary>
    /// Capability flags for sensors.
    /// </summary>
    [System.Flags]
    public enum SensorCapabilityFlags : byte
    {
        None = 0,
        /// <summary>Can detect through walls/obstacles.</summary>
        SeeThroughWalls = 1 << 0,
        /// <summary>Can detect invisible/cloaked targets.</summary>
        DetectInvisible = 1 << 1,
        /// <summary>Can identify target type (not just "something there").</summary>
        IdentifyTargets = 1 << 2,
        /// <summary>Can track targets that leave sensor range briefly.</summary>
        PersistentTracking = 1 << 3,
        /// <summary>Passive only (doesn't emit signals that could be detected).</summary>
        PassiveOnly = 1 << 4
    }

    /// <summary>
    /// Sensor state tracking.
    /// </summary>
    public struct SensorState : IComponentData
    {
        /// <summary>
        /// Last tick sensors were updated.
        /// </summary>
        public uint LastUpdateTick;

        /// <summary>
        /// Number of entities currently detected.
        /// </summary>
        public byte CurrentDetectionCount;

        /// <summary>
        /// Highest threat level among detected entities.
        /// </summary>
        public byte HighestThreat;

        /// <summary>
        /// Entity with highest threat (for quick access).
        /// </summary>
        public Entity HighestThreatEntity;

        /// <summary>
        /// Nearest detected entity.
        /// </summary>
        public Entity NearestEntity;

        /// <summary>
        /// Distance to nearest entity.
        /// </summary>
        public float NearestDistance;
    }

    /// <summary>
    /// Marker component for entities that can be detected.
    /// </summary>
    public struct Detectable : IComponentData
    {
        /// <summary>
        /// How visible this entity is (0 = invisible, 1 = normal, >1 = conspicuous).
        /// </summary>
        public float Visibility;

        /// <summary>
        /// How loud this entity is (0 = silent, 1 = normal, >1 = noisy).
        /// </summary>
        public float Audibility;

        /// <summary>
        /// Threat level this entity represents (0-255).
        /// </summary>
        public byte ThreatLevel;

        /// <summary>
        /// Detection category for filtering.
        /// </summary>
        public DetectableCategory Category;

        /// <summary>
        /// Creates default detectable config.
        /// </summary>
        public static Detectable Default => new Detectable
        {
            Visibility = 1f,
            Audibility = 1f,
            ThreatLevel = 0,
            Category = DetectableCategory.Neutral
        };
    }

    /// <summary>
    /// Categories for detectable entities.
    /// </summary>
    public enum DetectableCategory : byte
    {
        Neutral = 0,
        Ally = 1,
        Enemy = 2,
        Resource = 3,
        Hazard = 4,
        Objective = 5,
        Structure = 6,
        Wildlife = 7
    }
}
