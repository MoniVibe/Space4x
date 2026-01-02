using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Headless
{
    public struct HeadlessTurnRateState : IComponentData
    {
        public quaternion LastRotation;
        public float LastAngularSpeed;
        public byte Initialized;
    }
}
