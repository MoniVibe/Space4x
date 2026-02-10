using Unity.Entities;

namespace Space4X.Runtime
{
    public struct Space4XMovementInertiaConfig : IComponentData
    {
        public byte InertialMovementV1;
        public ushort ThrottleRampTicks;
        public byte GravityEnabled;
        public float GravityQueryRadius;
        public float GravityScale;
        public float GravityMinDistance;
        public ushort GravityMaxSources;
        public byte GravityMaxCellRadius;
        public float GravityMaxAccel;

        public static Space4XMovementInertiaConfig Default => new Space4XMovementInertiaConfig
        {
            InertialMovementV1 = 1,
            ThrottleRampTicks = 12,
            GravityEnabled = 1,
            GravityQueryRadius = 800f,
            GravityScale = 1f,
            GravityMinDistance = 1f,
            GravityMaxSources = 4,
            GravityMaxCellRadius = 4,
            GravityMaxAccel = 0f
        };
    }

    public struct VesselThrottleState : IComponentData
    {
        public ushort RampTicks;
    }
}
