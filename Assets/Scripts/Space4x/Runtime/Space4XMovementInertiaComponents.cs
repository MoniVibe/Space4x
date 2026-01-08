using Unity.Entities;

namespace Space4X.Runtime
{
    public struct Space4XMovementInertiaConfig : IComponentData
    {
        public byte InertialMovementV1;
        public ushort ThrottleRampTicks;

        public static Space4XMovementInertiaConfig Default => new Space4XMovementInertiaConfig
        {
            InertialMovementV1 = 1,
            ThrottleRampTicks = 12
        };
    }

    public struct VesselThrottleState : IComponentData
    {
        public ushort RampTicks;
    }
}
