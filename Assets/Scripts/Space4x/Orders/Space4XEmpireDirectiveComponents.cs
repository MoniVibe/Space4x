using System;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Orders
{
    public static class EmpireDirectiveKeys
    {
        public static readonly FixedString32Bytes SecureResources = new FixedString32Bytes("SECURE_RESOURCES");
        public static readonly FixedString32Bytes Expand = new FixedString32Bytes("EXPAND");
        public static readonly FixedString32Bytes ResearchFocus = new FixedString32Bytes("RESEARCH_FOCUS");
        public static readonly FixedString32Bytes MilitaryPosture = new FixedString32Bytes("MILITARY_POSTURE");
        public static readonly FixedString32Bytes TradeBias = new FixedString32Bytes("TRADE_BIAS");
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

