using System;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    public enum DivineHandEventType : byte
    {
        StateChanged = 0,
        TypeChanged = 1,
        AmountChanged = 2
    }

    public static class DivineHandConstants
    {
        public const ushort NoResourceType = 0;
    }

    public struct DivineHandEvent : IBufferElementData
    {
        public DivineHandEventType Type;
        public HandState FromState;
        public HandState ToState;
        public ushort ResourceTypeIndex;
        public int Amount;
        public int Capacity;
    }
}
