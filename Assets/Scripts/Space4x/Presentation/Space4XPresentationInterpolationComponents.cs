using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    public struct SimPoseSnapshot : IComponentData
    {
        public float3 PrevPosition;
        public quaternion PrevRotation;
        public float PrevScale;
        public float3 CurrPosition;
        public quaternion CurrRotation;
        public float CurrScale;
        public uint PrevTick;
        public uint CurrTick;
    }
}
