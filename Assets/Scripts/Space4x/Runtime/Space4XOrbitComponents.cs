using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public struct Space4XOrbitAnchor : IComponentData
    {
        public Entity ParentStar;
        public float Radius;
        public float AngularSpeed;
        public float Phase;
        public float Height;
        public uint EpochTick;
    }

    public struct Space4XOrbitAnchorState : IComponentData
    {
        public float3 LastPosition;
        public byte Initialized;
    }

    public struct Space4XOrbitStarTag : IComponentData { }
    public struct Space4XOrbitCenterTag : IComponentData { }
    public struct Space4XRogueOrbitTag : IComponentData { }
    public struct Space4XMicroImpulseTag : IComponentData { }
}
