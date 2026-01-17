using Unity.Collections;
using Unity.Entities;

namespace Space4X.Runtime
{
    public struct Space4XSensorsBeatConfig : IComponentData
    {
        public FixedString64Bytes ObserverCarrierId;
        public FixedString64Bytes TargetCarrierId;
        public float AcquireStartSeconds;
        public float AcquireDurationSeconds;
        public float DropStartSeconds;
        public float DropDurationSeconds;
        public float ObserverRange;
        public float ObserverUpdateInterval;
        public byte ObserverMaxTrackedTargets;
        public byte SensorsEnsured;
        public byte Initialized;
        public byte Completed;
        public uint AcquireStartTick;
        public uint AcquireEndTick;
        public uint DropStartTick;
        public uint DropEndTick;
    }
}
