// [TRI-STUB] Stub components for deception system
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Deception
{
    /// <summary>
    /// Deception attempt - active deception being performed.
    /// </summary>
    public struct DeceptionAttempt : IComponentData
    {
        public Entity DeceiverEntity;
        public Entity TargetEntity;
        public DeceptionType Type;
        public float DeceptionSkill;
        public float SuccessChance;
        public uint AttemptStartTick;
    }

    /// <summary>
    /// Deception types.
    /// </summary>
    public enum DeceptionType : byte
    {
        Lie = 0,
        Mislead = 1,
        ConcealIntent = 2,
        FalseIdentity = 3,
        FabricateEvidence = 4
    }

    /// <summary>
    /// Deception state - current deception status.
    /// </summary>
    public struct DeceptionState : IComponentData
    {
        public byte IsDeceiving;
        public float DeceptionLevel;
        public uint LastDeceptionTick;
    }

    /// <summary>
    /// Deception detection - detection attempt.
    /// </summary>
    public struct DeceptionDetection : IComponentData
    {
        public Entity DetectorEntity;
        public Entity SuspectEntity;
        public float DetectionChance;
        public float InsightLevel;
        public uint DetectionStartTick;
    }

    /// <summary>
    /// Deception history - record of past deceptions.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct DeceptionHistory : IBufferElementData
    {
        public Entity TargetEntity;
        public DeceptionType Type;
        public byte WasSuccessful;
        public byte WasDetected;
        public uint EventTick;
    }
}

