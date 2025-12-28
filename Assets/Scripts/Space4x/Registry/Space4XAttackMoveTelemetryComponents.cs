using Space4X.Runtime;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum AttackMoveTargetChangeReason : byte
    {
        EngageTarget = 0,
        TargetPriority = 1,
        EngagementReacquire = 2,
        LostTarget = 3
    }

    public enum AttackMoveCompletionResult : byte
    {
        DestinationReached = 0,
        Cancelled = 1,
        SupersededByOrder = 2
    }

    public struct Space4XAttackMoveTelemetry : IComponentData
    {
        public uint SampleStrideTicks;
        public uint MaxEventEntries;
        public uint MaxSampleEntries;
        public byte EnableLeadAimDiagnostics;
        public uint LeadAimSampleEveryNthShot;

        public static Space4XAttackMoveTelemetry Default => new Space4XAttackMoveTelemetry
        {
            SampleStrideTicks = 30,
            MaxEventEntries = 512,
            MaxSampleEntries = 256,
            EnableLeadAimDiagnostics = 0,
            LeadAimSampleEveryNthShot = 4
        };
    }

    [InternalBufferCapacity(8)]
    public struct AttackMoveStartedEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Ship;
        public AttackMoveSource Source;
        public float3 Destination;
        public float DestinationRadius;
        public Entity InitialTarget;
    }

    [InternalBufferCapacity(16)]
    public struct AttackMoveTargetChangedEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Ship;
        public Entity PreviousTarget;
        public Entity NewTarget;
        public AttackMoveTargetChangeReason Reason;
    }

    [InternalBufferCapacity(8)]
    public struct AttackMoveFiringWindowEvent : IBufferElementData
    {
        public uint TickEnter;
        public uint TickExit;
        public Entity Ship;
        public uint TimeInRangeTicks;
        public uint TimeInConeTicks;
        public uint ShotsFired;
        public byte MountsThatFiredCount;
    }

    [InternalBufferCapacity(8)]
    public struct AttackMoveCompletedEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Ship;
        public AttackMoveCompletionResult Result;
        public byte DestinationReached;
        public byte PatrolResumed;
        public uint PatrolResumeCount;
        public float3 Destination;
        public float DestinationRadius;
    }

    [InternalBufferCapacity(8)]
    public struct AttackMoveClarityStateEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Ship;
        public byte DestInRange;
        public uint TicksTrue;
        public uint TicksFalse;
        public uint LastFlipTick;
    }

    [InternalBufferCapacity(8)]
    public struct AttackMoveKiteQualityEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Ship;
        public uint KiteTicks;
        public uint EngagementTicks;
        public uint TimeInRangeTicks;
        public uint TimeInConeTicks;
        public uint ShotsFired;
        public byte MountsThatFiredCount;
        public float AvgConeErrorDeg;
        public float AvgSpeedWhileFiring;
    }

    [InternalBufferCapacity(8)]
    public struct AttackMoveSummaryEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Ship;
        public AttackMoveSource Source;
        public AttackMoveCompletionResult Result;
        public uint TimeToFirstShotTicks;
        public float ConeWhileInRangeRatio;
        public float KiteRatio;
        public uint TargetChangeCount;
        public uint PatrolResumeCount;
    }

    [InternalBufferCapacity(8)]
    public struct AttackMoveWeaponLeadAimEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Ship;
        public byte MountIndex;
        public byte UsedLeadAim;
        public float ProjectileSpeed;
        public float PredictedTimeOfFlight;
    }

    [InternalBufferCapacity(16)]
    public struct AttackMoveSample : IBufferElementData
    {
        public uint Tick;
        public Entity Ship;
        public float DistToDest;
        public float DistToTarget;
        public byte InRange;
        public byte InCone;
        public byte DestInRange;
        public float AimAngleDeg;
        public float ClosingSpeed;
    }

    public struct AttackMoveTelemetryState : IComponentData
    {
        public byte IsActive;
        public AttackMoveSource Source;
        public float3 Destination;
        public float DestinationRadius;
        public Entity CurrentTarget;
        public AttackMoveTargetChangeReason CurrentTargetReason;
        public byte InRange;
        public byte InCone;
        public byte InFiringWindow;
        public byte DestInRange;
        public uint FiringWindowStartTick;
        public uint TimeInRangeTicks;
        public uint TimeInConeTicks;
        public uint ShotsFired;
        public ulong MountsFiredMask;
        public uint LastSampleTick;
        public uint StartTick;
        public uint PatrolResumeCount;
        public byte WasPatrolling;
        public byte LastArrived;
        public uint LastArrivedTick;
        public uint ActiveTicks;
        public uint KiteTicks;
        public float ConeErrorSum;
        public uint ConeErrorSamples;
        public float SpeedWhileFiringSum;
        public uint SpeedWhileFiringSamples;
        public uint FirstShotTick;
        public uint TargetChangeCount;
        public uint DestInRangeTicks;
        public uint DestOutOfRangeTicks;
        public uint LastDestInRangeFlipTick;
    }

    [InternalBufferCapacity(4)]
    public struct AttackMoveWeaponCooldownState : IBufferElementData
    {
        public ushort LastCooldown;
    }
}
