using Unity.Entities;

namespace PureDOTS.Runtime.Agency
{
    public struct ControlLinkHealthConfig : IComponentData
    {
        public uint HeartbeatTimeoutTicks;
        public float MinCommsQuality;
    }

    public struct ControlLinkState : IComponentData
    {
        public Entity ControllerEntity;
        public Entity CompromiseSource;
        public uint LastHeartbeatTick;
        public float CommsQuality01;
        public byte IsCompromised;
        public byte IsLost;
        public ushort Reserved0;
    }

    public struct ControllerIntegrityState : IComponentData
    {
        public float Integrity01;
        public Entity CompromisedBy;
        public uint LastIntegrityTick;
        public byte IsCompromised;
        public byte Reserved0;
        public ushort Reserved1;
    }

    public enum RogueToolReason : byte
    {
        LostControl = 0,
        HostileOverride = 1,
        SpoofedController = 2
    }

    public struct RogueToolState : IComponentData
    {
        public RogueToolReason Reason;
        public uint SinceTick;
        public byte AllowFriendlyDestructionNoPenalty;
        public byte Hackable;
        public ushort Reserved0;
    }
}
