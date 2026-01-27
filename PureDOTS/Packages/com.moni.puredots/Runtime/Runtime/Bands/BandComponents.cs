using System;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Bands
{
    public struct BandId : IComponentData
    {
        public int Value;
        public int FactionId;
        public Entity Leader;
    }

    [Flags]
    public enum BandStatusFlags : byte
    {
        None = 0,
        Idle = 1 << 0,
        Moving = 1 << 1,
        Engaged = 1 << 2,
        Routing = 1 << 3,
        NeedsSupply = 1 << 4,
        Resting = 1 << 5
    }

    public struct BandStats : IComponentData
    {
        public int MemberCount;
        public float AverageDiscipline;
        public float Morale;
        public float Cohesion;
        public float Fatigue;
        public BandStatusFlags Flags;
        public uint LastUpdateTick;
    }

    public enum BandFormationType : byte
    {
        Column = 0,
        Line = 1,
        Wedge = 2,
        Circle = 3
    }

    public struct BandFormation : IComponentData
    {
        public BandFormationType Formation;
        public float Spacing;
        public float Width;
        public float Depth;
        public float3 Facing;
        public float3 Anchor;
        public float Stability;
        public uint LastSolveTick;
    }

    public struct BandMember : IBufferElementData
    {
        public Entity Villager;
        public byte Role;
    }

    public struct BandIntent : IComponentData
    {
        public byte DesiredAction;
        public float IntentWeight;
    }
}
