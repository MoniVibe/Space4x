using PureDOTS.Runtime.Steering;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum StrikeCraftDogfightPhase : byte
    {
        None = 0,
        Approach = 1,
        FireWindow = 2,
        BreakOff = 3,
        Rejoin = 4
    }

    public struct StrikeCraftDogfightTag : IComponentData
    {
    }

    public struct StrikeCraftDogfightConfig : IComponentData
    {
        public float NavConstantN;
        public float ApproachMaxSpeedMultiplier;
        public float FireConeDegrees;
        public float TargetAcquireRadius;
        public float BreakOffDistance;
        public uint BreakOffTicks;
        public float3 RejoinOffset;
        public float RejoinRadius;
        public float JinkStrength;
        public float MaxLateralAccel;
        public float MaxTurnRate;
        public float SeparationRadius;
        public float SeparationStrength;
        public uint TelemetrySampleTicks;

        public static StrikeCraftDogfightConfig Default => new StrikeCraftDogfightConfig
        {
            NavConstantN = 3.5f,
            ApproachMaxSpeedMultiplier = 1.1f,
            FireConeDegrees = 45f,
            TargetAcquireRadius = 200f,
            BreakOffDistance = 20f,
            BreakOffTicks = 90,
            RejoinOffset = new float3(0f, 0f, -12f),
            RejoinRadius = 6f,
            JinkStrength = 0.15f,
            MaxLateralAccel = 12f,
            MaxTurnRate = 4.5f,
            SeparationRadius = 6f,
            SeparationStrength = 2.5f,
            TelemetrySampleTicks = 30
        };
    }

    public struct StrikeCraftDogfightMetrics : IComponentData
    {
        public uint EngagementStartTick;
        public uint FirstFireTick;
        public uint LastFireTick;
        public uint LastKillTick;
        public uint LastPhaseTick;
        public uint LastTelemetryTick;
    }

    public struct StrikeCraftDogfightSteering : IComponentData
    {
        public SteeringOutput Output;
    }

    [InternalBufferCapacity(8)]
    public struct StrikeCraftDogfightSample : IBufferElementData
    {
        public uint Tick;
        public StrikeCraftDogfightPhase Phase;
        public Entity Target;
        public float Distance;
        public float ClosingSpeed;
        public float ConeDot;
        public float PnOmega;
    }
}
