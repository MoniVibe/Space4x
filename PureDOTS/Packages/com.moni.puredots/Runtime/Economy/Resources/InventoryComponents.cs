using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Resources
{
    /// <summary>
    /// Inventory component with capacity constraints.
    /// All physical goods must exist in inventories.
    /// </summary>
    public struct Inventory : IComponentData
    {
        public float MaxMass;
        public float MaxVolume;
        public float CurrentMass;
        public float CurrentVolume;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Inventory item buffer element.
    /// Represents a stack of items in an inventory.
    /// </summary>
    public struct InventoryItem : IBufferElementData
    {
        public FixedString64Bytes ItemId;
        public float Quantity;
        public float Quality; // 0-100 or normalized 0-1
        public float Durability; // For equipment, 0-1
        public uint CreatedTick;
    }

    /// <summary>
    /// Reference to an inventory entity.
    /// Used by businesses and other entities that need inventory access.
    /// </summary>
    public struct InventoryReference : IComponentData
    {
        public Entity InventoryEntity;
    }
}

