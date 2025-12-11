using Unity.Entities;

namespace PureDOTS.Runtime.AI
{
    // STUB: behavior tree + perception placeholders to unblock AI wiring.

    public struct BehaviorTreeHandle : IComponentData
    {
        public int TreeId;
        public byte Version;
    }

    public struct BehaviorTaskState : IComponentData
    {
        public int ActiveNodeId;
        public byte Phase; // running/success/failure/wait
    }

    public struct BehaviorNodeState : IBufferElementData
    {
        public int NodeId;
        public byte Status; // unknown/running/success/failure
        public byte Flags;  // decorator bits
    }

    public struct PerceptionConfig : IComponentData
    {
        public float Range;
        public float CooldownSeconds;
        public byte ChannelsMask; // bitmask of stimulus channels
        public byte MaxStimuli;
    }

    public struct PerceptionStimulus : IBufferElementData
    {
        public Entity Source;
        public float Strength;
        public byte Channel;
        public uint Timestamp;
    }
}
