// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Communication
{
    public struct CommChannel : IComponentData
    {
        public int ChannelId;
        public float Reliability; // 0-1
        public float LatencySeconds;
    }

    public struct CommRelay : IComponentData
    {
        public float3 Position;
        public float Range;
    }

    public struct CommDisruption : IComponentData
    {
        public float Severity; // 0-1
        public float RecoveryRate;
    }

    [InternalBufferCapacity(4)]
    public struct CommMessageElement : IBufferElementData
    {
        public int MessageId;
        public float Timestamp;
        public byte Status; // 0=pending,1=sent,2=failed
    }
}
