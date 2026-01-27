// [TRI-STUB] Stub components for perception channel integration
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Sense capability - sensor configuration with enabled channels.
    /// </summary>
    public struct SenseCapability : IComponentData
    {
        public PerceptionChannel EnabledChannels;
        public float Range;
        public float FieldOfView;
        public float Acuity;
        public float UpdateInterval;
        public byte MaxTrackedTargets;
        public byte Flags;
    }

    /// <summary>
    /// Sensor signature - detectability per channel.
    /// </summary>
    public struct SensorSignature : IComponentData
    {
        public float VisualSignature;
        public float AuditorySignature;
        public float OlfactorySignature;
        public float EMSignature;
        public float GraviticSignature;
        public float ExoticSignature;
        public float ParanormalSignature;

        public static SensorSignature Default => new SensorSignature
        {
            VisualSignature = 1f,
            AuditorySignature = 1f,
            OlfactorySignature = 1f,
            EMSignature = 1f,
            GraviticSignature = 1f,
            ExoticSignature = 0f,
            ParanormalSignature = 0f
        };

        public float GetSignature(PerceptionChannel channel)
        {
            return channel switch
            {
                PerceptionChannel.Vision => VisualSignature,
                PerceptionChannel.Hearing => AuditorySignature,
                PerceptionChannel.Smell => OlfactorySignature,
                PerceptionChannel.EM => EMSignature,
                PerceptionChannel.Gravitic => GraviticSignature,
                PerceptionChannel.Exotic => ExoticSignature,
                PerceptionChannel.Paranormal => ParanormalSignature,
                PerceptionChannel.Proximity => math.max(VisualSignature, AuditorySignature),
                _ => 0f
            };
        }
    }

    /// <summary>
    /// Perceived entity - detected entity with channel information.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct PerceivedEntity : IBufferElementData
    {
        public Entity TargetEntity;
        public PerceptionChannel DetectedChannels;
        public float Confidence;
        public float Distance;
        public float3 Direction;
        public uint FirstDetectedTick;
        public uint LastSeenTick;
        public byte ThreatLevel;
        public sbyte Relationship;
        public PerceivedRelationKind RelationKind;
        public PerceivedRelationFlags RelationFlags;
    }
}

