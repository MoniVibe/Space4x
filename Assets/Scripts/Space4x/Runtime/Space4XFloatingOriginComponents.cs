using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public struct Space4XFloatingOriginConfig : IComponentData
    {
        public float Threshold;
        public uint CooldownTicks;
        public byte Enabled;
    }

    public struct Space4XFloatingOriginState : IComponentData
    {
        public uint LastShiftTick;
        public float3 LastShift;
    }
}
