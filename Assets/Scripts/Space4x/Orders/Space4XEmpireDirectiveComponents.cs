using System;
using Unity.Collections;
using Unity.Entities;

namespace Space4x.Orders
{
    public enum DirectiveScope : byte
    {
        Empire = 0,
        Faction = 1,
        Fleet = 2
    }

    public enum DirectiveSource : byte
    {
        AI = 0,
        Player = 1,
        Scripted = 2
    }

    [Flags]
    public enum DirectiveFlags : byte
    {
        None = 0,
        Persistent = 1 << 0,
        Urgent = 1 << 1
    }

    public struct EmpireDirective : IBufferElementData
    {
        public FixedString32Bytes DirectiveType;
        public float BasePriority;
        public float PriorityWeight;
        public uint IssuedTick;
        public uint ExpiryTick;
        public Entity TargetEntity;
        public Entity Issuer;
        public DirectiveScope Scope;
        public DirectiveSource Source;
        public DirectiveFlags Flags;
    }
}
