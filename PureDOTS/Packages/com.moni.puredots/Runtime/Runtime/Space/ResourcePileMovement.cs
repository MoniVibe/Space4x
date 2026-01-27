using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Space
{
    public struct ResourcePileVelocity : IComponentData
    {
        public float3 Velocity;
    }
}
