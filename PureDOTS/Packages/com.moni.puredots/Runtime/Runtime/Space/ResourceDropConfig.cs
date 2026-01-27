using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    public struct ResourceDropConfig : IComponentData
    {
        public FixedString64Bytes ResourceTypeId;
        public float DropRadiusMeters;
        public float DecaySeconds;
        public float MaxStack;
        public float DropIntervalSeconds;
        public float TimeSinceLastDrop;
    }
}
