using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime.Interaction
{
    public struct Space4XCelestialHoldState : IComponentData
    {
        public float3 TargetPosition;
        public float FollowStrength;
        public byte Active;
    }

    public struct Space4XCelestialImpulseRequest : IComponentData
    {
        public float3 DeltaV;
        public uint Tick;
    }
}
