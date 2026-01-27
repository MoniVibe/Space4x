// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Navigation
{
    public struct NavSurfaceId : IComponentData
    {
        public int Surface;
    }

    public struct PathfinderTicket : IComponentData
    {
        public Entity Requester;
        public float3 Start;
        public float3 End;
        public byte Flags;
    }

    public struct PathSolutionElement : IBufferElementData
    {
        public float3 Position;
        public byte CornerKind;
    }

    public struct PathResultState : IComponentData
    {
        public byte Status;
        public uint CompletedTick;
    }
}
