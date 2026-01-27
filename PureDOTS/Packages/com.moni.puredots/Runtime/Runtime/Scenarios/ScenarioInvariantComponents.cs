using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Scenarios
{
    public struct HeadlessInvariantState : IComponentData
    {
        public float3 LastPosition;
        public quaternion LastRotation;
        public float LastAngularSpeed;
        public uint LastProgressTick;
        public byte Initialized;
    }
}
