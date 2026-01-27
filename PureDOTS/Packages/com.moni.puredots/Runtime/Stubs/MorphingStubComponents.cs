// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Morphing
{
    public struct TerrainMorphState : IComponentData
    {
        public float3 OriginalPosition;
        public float Deformation;
        public float RecoveryRate;
    }

    public struct BreakableSurface : IComponentData
    {
        public float Integrity;
        public float MaxIntegrity;
    }

    public struct BurnState : IComponentData
    {
        public float Intensity;
        public float ExtinguishRate;
    }
}
