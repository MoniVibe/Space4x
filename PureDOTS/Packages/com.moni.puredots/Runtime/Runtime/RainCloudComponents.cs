using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    public struct RainCloudTag : IComponentData { }

    public struct RainCloudConfig : IComponentData
    {
        public float BaseRadius;
        public float MinRadius;
        public float RadiusPerHeight;
        public float MoisturePerSecond;
        public float MoistureFalloff;
        public float MoistureCapacity;
        public float3 DefaultVelocity;
        public float DriftNoiseStrength;
        public float DriftNoiseFrequency;
        public float FollowLerp;
    }

    public struct RainCloudState : IComponentData
    {
        public float MoistureRemaining;
        public float ActiveRadius;
        public float3 Velocity;
        public float AgeSeconds;
        public byte Flags;
    }

    public struct RainCloudLifetime : IComponentData
    {
        public float SecondsRemaining;
    }

    public struct RainCloudMoistureHistory : IBufferElementData
    {
        public uint Tick;
        public float MoistureApplied;
        public float RadiusAtTick;
    }
}
