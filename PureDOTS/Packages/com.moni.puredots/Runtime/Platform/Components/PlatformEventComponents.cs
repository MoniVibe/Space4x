using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Platform
{
    /// <summary>
    /// Event emitted when a platform segment is destroyed.
    /// </summary>
    public struct PlatformSegmentDestroyedEvent : IBufferElementData
    {
        public Entity PlatformEntity;
        public int SegmentIndex;
        public float3 WorldPosition;
    }

    /// <summary>
    /// Event emitted when a reactor goes critical/meltdown.
    /// </summary>
    public struct PlatformReactorMeltdownEvent : IBufferElementData
    {
        public Entity PlatformEntity;
        public int SegmentIndex;
        public float3 WorldPosition;
        public float DamageAmount;
        public float Radius;
    }

    /// <summary>
    /// Event emitted when a platform is captured via boarding.
    /// </summary>
    public struct PlatformCapturedEvent : IBufferElementData
    {
        public Entity PlatformEntity;
        public int PreviousFactionId;
        public int NewFactionId;
    }
}

