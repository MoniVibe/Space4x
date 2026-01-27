using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    public struct InventoryCapacity : IComponentData
    {
        public ushort Slots;
        public float MassLimit;
    }

    [InternalBufferCapacity(4)]
    public struct InventorySlot : IBufferElementData
    {
        public FixedString32Bytes SlotId;
        public Entity Item;
        public float Quantity;
        public float Mass;
    }
}



