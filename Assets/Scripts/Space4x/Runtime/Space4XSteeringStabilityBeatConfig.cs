using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public struct Space4XSteeringStabilityBeatConfig : IComponentData
    {
        public FixedString64Bytes FleetId;
        public float3 TargetPosition;
        public float StartSeconds;
        public float SettleSeconds;
        public float MeasureSeconds;
        public byte HoldCarrierMiningIntent;
        public byte Initialized;
        public byte Completed;
        public uint StartTick;
        public uint SettleTicks;
        public uint MeasureTicks;
    }
}
