using Unity.Collections;
using Unity.Entities;

namespace Space4X.Runtime
{
    public enum Space4XEntityLodTierKind : byte
    {
        Lod0 = 0,
        Lod1 = 1,
        Lod2 = 2
    }

    public struct Space4XEntityId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public struct Space4XEntityLodTier : IComponentData
    {
        public byte Tier;
    }

    public struct Space4XEntityLedgerTag : IComponentData
    {
    }

    [InternalBufferCapacity(32)]
    public struct Space4XEntityLedgerEntry : IBufferElementData
    {
        public FixedString64Bytes EntityId;
        public FixedString64Bytes CarrierId;
        public byte LodTier;
        public uint LastSeenTick;
    }
}
