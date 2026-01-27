using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    public enum AISensorCategory : byte
    {
        None = 0,
        Villager = 1,
        ResourceNode = 2,
        Storehouse = 3,
        TransportUnit = 4,
        Miracle = 5,
        Custom0 = 240
    }

    /// <summary>
    /// Configuration describing how an agent samples its surroundings.
    /// </summary>
    public struct AISensorConfig : IComponentData
    {
        public float UpdateInterval;
        public float Range;
        public byte MaxResults;
        public SpatialQueryOptions QueryOptions;
        public AISensorCategory PrimaryCategory;
        public AISensorCategory SecondaryCategory;
    }

    /// <summary>
    /// Runtime sensor state used for cadence tracking.
    /// </summary>
    public struct AISensorState : IComponentData
    {
        public float Elapsed;
        public uint LastSampleTick;
    }

    /// <summary>
    /// Sensor readings captured during the last spatial sample.
    /// </summary>
    public struct AISensorReading : IBufferElementData
    {
        public Entity Target;
        public float DistanceSq;
        public float NormalizedScore;
        public int CellId;
        public uint SpatialVersion;
        public AISensorCategory Category;
    }

    /// <summary>
    /// Links an agent to a blob containing action/utility curve definitions.
    /// </summary>
    public struct AIBehaviourArchetype : IComponentData
    {
        public BlobAssetReference<AIUtilityArchetypeBlob> UtilityBlob;
    }

    /// <summary>
    /// Stores the current best action produced by the scoring stage.
    /// </summary>
    public struct AIUtilityState : IComponentData
    {
        public byte BestActionIndex;
        public float BestScore;
        public uint LastEvaluationTick;
    }

    /// <summary>
    /// Buffer storing per-action utility scores for observability/debugging.
    /// </summary>
    public struct AIActionState : IBufferElementData
    {
        public float Score;
    }

    /// <summary>
    /// Configuration for steering/path sampling.
    /// </summary>
    public struct AISteeringConfig : IComponentData
    {
        public float MaxSpeed;
        public float Acceleration;
        public float Responsiveness;
        public float FlowFieldWeight;
        public byte DegreesOfFreedom; // 2 = planar, 3 = full
        public float ObstacleLookAhead;
    }

    /// <summary>
    /// Mutable steering state updated each tick.
    /// </summary>
    public struct AISteeringState : IComponentData
    {
        public float3 DesiredDirection;
        public float3 LinearVelocity;
        public float3 LastSampledTarget;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Selected target for the active task/action.
    /// </summary>
    public struct AITargetState : IComponentData
    {
        public Entity TargetEntity;
        public float3 TargetPosition;
        public byte ActionIndex;
        public byte Flags;
    }

    /// <summary>
    /// Command emitted by the task resolution system and consumed by domain-specific logic.
    /// </summary>
    public struct AICommand : IBufferElementData
    {
        public Entity Agent;
        public byte ActionIndex;
        public Entity TargetEntity;
        public float3 TargetPosition;

        // Optional acknowledgement contract (0 token / None flags => no-ack).
        public uint AckToken;
        public AIAckRequestFlags AckFlags;
    }

    /// <summary>
    /// Tag applied to the singleton entity that owns the AI command queue buffer.
    /// </summary>
    public struct AICommandQueueTag : IComponentData { }

    /// <summary>
    /// Blob describing utility curves for a behaviour archetype.
    /// </summary>
    public struct AIUtilityArchetypeBlob
    {
        public BlobArray<AIUtilityActionBlob> Actions;
    }

    /// <summary>
    /// Blob describing the utility curves that contribute to a single action.
    /// </summary>
    public struct AIUtilityActionBlob
    {
        public BlobArray<AIUtilityCurveBlob> Factors;
    }

    /// <summary>
    /// Blob describing a single utility factor sampled from a sensor.
    /// </summary>
    public struct AIUtilityCurveBlob
    {
        public ushort SensorIndex;
        public float Threshold;
        public float Weight;
        public float ResponsePower;
        public float MaxValue;
    }
}
