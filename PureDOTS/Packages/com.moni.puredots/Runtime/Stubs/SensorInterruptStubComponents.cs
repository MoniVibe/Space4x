// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Sensors
{
    public struct SensorChannelDef : IComponentData
    {
        public byte ChannelId;
        public float Range;
    }

    public struct SensorRigState : IComponentData
    {
        public byte ChannelsMask;
        public uint LastSampleTick;
    }

    public struct InterruptTicket : IComponentData
    {
        public Entity Source;
        public byte Category;
        public uint RaisedTick;
    }

    public struct AlertTrigger : IBufferElementData
    {
        public Entity Target;
        public byte Severity;
    }
}
