using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace PureDOTS.Runtime.Space
{
    public struct ResourcePile : IComponentData
    {
        public float Amount;
        public float3 Position;
    }

    public struct ResourcePileMeta : IComponentData
    {
        public FixedString64Bytes ResourceTypeId;
        public float DecaySeconds;
        public float MaxCapacity;
    }
}
