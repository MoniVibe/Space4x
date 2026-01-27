using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// High-level intent for movement targeting.
    /// </summary>
    public enum MoveIntentType : byte
    {
        None = 0,
        Work = 1,
        Need = 2,
        Flee = 3,
        Idle = 4
    }

    /// <summary>
    /// Current movement intent toward a target entity or position.
    /// </summary>
    public struct MoveIntent : IComponentData
    {
        public Entity TargetEntity;
        public float3 TargetPosition;
        public MoveIntentType IntentType;
    }

    /// <summary>
    /// Current movement plan details for debugging.
    /// </summary>
    public enum MovePlanMode : byte
    {
        None = 0,
        Approach = 1,
        Arrive = 2,
        Latch = 3,
        Orbit = 4
    }

    public struct MovePlan : IComponentData
    {
        public MovePlanMode Mode;
        public float3 DesiredVelocity;
        public float MaxAccel;
        public float EtaSeconds;
    }

    /// <summary>
    /// Last decision snapshot for goal selection or movement routing.
    /// </summary>
    public struct DecisionTrace : IComponentData
    {
        public byte ReasonCode;
        public Entity ChosenTarget;
        public float Score;
        public Entity BlockerEntity;
        public uint SinceTick;
    }

    /// <summary>
    /// Lightweight trace event buffer (state transitions only).
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DecisionTraceEvent : IBufferElementData
    {
        public uint Tick;
        public byte ReasonCode;
        public float Score;
        public Entity TargetEntity;
    }
}
