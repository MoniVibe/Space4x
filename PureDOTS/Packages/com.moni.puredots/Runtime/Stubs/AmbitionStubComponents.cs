// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Motivation
{
    public struct AmbitionState : IComponentData
    {
        public int AmbitionId;
        public byte Priority;
        public float Progress;
    }

    [InternalBufferCapacity(2)]
    public struct DesireElement : IBufferElementData
    {
        public int DesireId;
        public byte Priority;
    }

    public struct IntentState : IComponentData
    {
        public int IntentId;
        public byte Status;
    }

    [InternalBufferCapacity(2)]
    public struct TaskElement : IBufferElementData
    {
        public int TaskId;
        public byte Status;
    }
}
