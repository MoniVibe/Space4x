// [TRI-STUB] Stub components for group knowledge sharing
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Cooperation
{
    /// <summary>
    /// Group knowledge - knowledge shared within a group.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct GroupKnowledge : IBufferElementData
    {
        public FixedString64Bytes KnowledgeId;
        public KnowledgeType Type;
        public float KnowledgeValue;
        public uint AcquiredTick;
    }

    /// <summary>
    /// Knowledge types.
    /// </summary>
    public enum KnowledgeType : byte
    {
        Location = 0,
        Threat = 1,
        Resource = 2,
        Tactic = 3,
        Technology = 4,
        Cultural = 5
    }

    /// <summary>
    /// Knowledge diffusion - knowledge spreading between groups.
    /// </summary>
    public struct KnowledgeDiffusion : IComponentData
    {
        public Entity SourceGroup;
        public Entity TargetGroup;
        public FixedString64Bytes KnowledgeId;
        public float DiffusionProgress;
        public float DiffusionRate;
    }
}

