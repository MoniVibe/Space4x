using PureDOTS.Runtime.Perception;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Runtime
{
    public struct Space4XCommsBeatConfig : IComponentData
    {
        public FixedString64Bytes SenderCarrierId;
        public FixedString64Bytes ReceiverCarrierId;
        public FixedString64Bytes PayloadId;
        public PerceptionChannel TransportMask;
        public float StartSeconds;
        public float DurationSeconds;
        public float SendIntervalSeconds;
        public byte RequireAck;
        public byte CommsEnsured;
        public byte Initialized;
        public byte Completed;
        public uint StartTick;
        public uint EndTick;
        public uint SendIntervalTicks;
    }
}
