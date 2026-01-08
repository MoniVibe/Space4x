using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public enum MoveIntentType : byte
    {
        None = 0,
        MoveTo = 1,
        Orbit = 2,
        Latch = 3,
        Dock = 4,
        Hold = 5
    }

    public enum MovePlanMode : byte
    {
        Approach = 0,
        Arrive = 1,
        Latch = 2,
        Orbit = 3,
        Dock = 4
    }

    public enum DecisionReasonCode : byte
    {
        None = 0,
        NoTarget = 1,
        MiningHold = 2,
        Arrived = 3,
        Moving = 4
    }

    public enum MoveTraceEventKind : byte
    {
        IntentChanged = 0,
        PlanChanged = 1,
        DecisionChanged = 2
    }

    public struct MoveIntent : IComponentData
    {
        public Entity TargetEntity;
        public float3 TargetPosition;
        public MoveIntentType IntentType;
    }

    public struct MovePlan : IComponentData
    {
        public MovePlanMode Mode;
        public float3 DesiredVelocity;
        public float MaxAccel;
        public float EstimatedTime;
    }

    public struct DecisionTrace : IComponentData
    {
        public DecisionReasonCode ReasonCode;
        public Entity ChosenTarget;
        public float Score;
        public Entity BlockerEntity;
        public uint SinceTick;
    }

    public struct MovementDebugState : IComponentData
    {
        public const int TraceCapacity = 16;
        public float3 LastPosition;
        public float LastDistanceToTarget;
        public float LastSpeed;
        public float MaxSpeedDelta;
        public float MaxTeleportDistance;
        public uint LastProgressTick;
        public uint LastSampleTick;
        public uint NaNInfCount;
        public uint SpeedClampCount;
        public uint AccelClampCount;
        public uint AccelSpikeCount;
        public uint TeleportCount;
        public uint StuckCount;
        public uint StateFlipCount;
        public uint LastIntentChangeTick;
        public uint LastPlanChangeTick;
        public byte Initialized;
    }

    [InternalBufferCapacity(MovementDebugState.TraceCapacity)]
    public struct MoveTraceEvent : IBufferElementData
    {
        public MoveTraceEventKind Kind;
        public uint Tick;
        public Entity Target;
    }
}
