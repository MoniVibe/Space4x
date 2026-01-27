// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Operations
{
    public struct ExplorationOrder : IComponentData
    {
        public int OrderId;
        public float3 TargetPosition;
        public byte Depth;
    }

    public struct ThreatSignature : IComponentData
    {
        public float Strength;
        public byte Category;
    }

    public struct IntelSample : IComponentData
    {
        public int SampleId;
        public uint Timestamp;
    }
}
