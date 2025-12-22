using PureDOTS.Runtime.Communication;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    /// <summary>
    /// Intent emitted from accepted comm orders on the Space4X side.
    /// </summary>
    public struct Space4XCommOrderIntent : IComponentData
    {
        public CommOrderVerb Verb;
        public Entity Sender;
        public Entity Target;
        public float3 TargetPosition;
        public CommOrderSide Side;
        public CommOrderPriority Priority;
        public uint TimingWindowTicks;
        public uint ContextHash;
        public uint SourceMessageId;
        public uint ReceivedTick;
        public float Confidence;
        public byte Inferred;
    }
}
