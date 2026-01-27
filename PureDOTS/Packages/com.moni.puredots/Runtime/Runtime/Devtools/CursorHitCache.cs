using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Runtime.Devtools
{
    /// <summary>
    /// Cached cursor raycast hit data updated per tick by input systems.
    /// Used by devtools for @cursor spawn placement.
    /// </summary>
    public struct CursorHitCache : IComponentData
    {
        public uint SampleTick;
        public float3 RayOrigin;
        public float3 RayDirection;
        public bool HasHit;
        public float3 HitPoint;
        public float3 HitNormal;
        public Entity HitEntity;
        public byte ModifierKeys; // Bit flags: Shift=1, Ctrl=2, Alt=4
    }
}























