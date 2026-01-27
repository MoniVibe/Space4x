using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Agency
{
    public enum ControlOrderKind : byte
    {
        None = 0,
        Idle = 1,
        Screen = 2,
        Tow = 3,
        Escort = 4,
        Attack = 5,
        Return = 6,
        Hold = 7
    }

    public struct ControlOrderState : IComponentData
    {
        public ControlOrderKind Kind;
        public ControlOrderKind FallbackKind;
        public Entity TargetEntity;
        public Entity AnchorEntity;
        public float3 AnchorPosition;
        public float Radius;
        public uint IssuedTick;
        public uint ExpiryTick;
        public uint LastUpdatedTick;
        public uint Sequence;
        public byte RequiresHeartbeat;
        public byte Reserved0;
        public ushort Reserved1;
    }
}
