using System;
using Unity.Entities;

namespace Space4X.Orders
{
    public enum EmpireDirectiveType : byte
    {
        Unknown = 0,
        SecureResources = 1,
        Expand = 2,
        ResearchFocus = 3,
        MilitaryPosture = 4,
        TradeBias = 5
    }

    public enum DirectiveScope : byte
    {
        Unknown = 0,
        Empire = 1,
        Sector = 2,
        Colony = 3,
        Fleet = 4
    }

    public enum DirectiveSource : byte
    {
        Unknown = 0,
        Player = 1,
        AI = 2,
        Scenario = 3
    }

    [Flags]
    public enum DirectiveFlags : byte
    {
        None = 0,
        Persistent = 1 << 0,
        Hidden = 1 << 1,
        Critical = 1 << 2
    }

    public struct EmpireDirective : IBufferElementData
    {
        public EmpireDirectiveType DirectiveType;
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
