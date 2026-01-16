using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Headless
{
    public struct HeadlessTurnRateState : IComponentData
    {
        public byte Initialized;
        public byte WasMoving;
        public quaternion LastRotation;
        public float LastAngularSpeed;
    }
}
