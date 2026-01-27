using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Hand
{
    /// <summary>
    /// Current hover target information from raycast.
    /// Updated by HandRaycastSystem each tick.
    /// </summary>
    public struct HandHover : IComponentData
    {
        public Entity TargetEntity;
        public float3 HitPosition;
        public float3 HitNormal;
        public float Distance;
    }
}

