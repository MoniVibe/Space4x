using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Resources
{
    /// <summary>
    /// Resource/item category enum.
    /// </summary>
    public enum ItemCategory : byte
    {
        Raw = 0,
        Processed = 1,
        Food = 2,
        Fuel = 3,
        Tool = 4,
        Weapon = 5,
        Armor = 6,
        Luxury = 7,
        Artifact = 8,
        CargoContainer = 9
    }

    /// <summary>
    /// Item tags as bitflags.
    /// </summary>
    [System.Flags]
    public enum ItemTags : ushort
    {
        None = 0,
        Food = 1 << 0,
        Flammable = 1 << 1,
        Illegal = 1 << 2,
        Sacred = 1 << 3,
        Contraband = 1 << 4,
        Luxury = 1 << 5,
        MilitaryGrade = 1 << 6,
        Rare = 1 << 7,
        BulkOnly = 1 << 8,
        Perishable = 1 << 9,
        Durable = 1 << 10
    }

    /// <summary>
    /// BlobAsset structure for item/resource specification.
    /// Defines physical properties, stack size, category, tags, and base value.
    /// </summary>
    public struct ItemSpecBlob
    {
        public FixedString64Bytes ItemId;
        public FixedString64Bytes Name;
        public ItemCategory Category;
        public float MassPerUnit;
        public float VolumePerUnit;
        public int StackSize;
        public ItemTags Tags;
        public float BaseValue;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U1)]
        public bool IsPerishable;
        public float PerishRate;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U1)]
        public bool IsDurable;
        public float BaseDurability;
    }

    /// <summary>
    /// Catalog blob containing all item specifications.
    /// </summary>
    public struct ItemSpecCatalogBlob
    {
        public BlobArray<ItemSpecBlob> Items;
    }

    /// <summary>
    /// Singleton component holding the item spec catalog reference.
    /// </summary>
    public struct ItemSpecCatalog : IComponentData
    {
        public BlobAssetReference<ItemSpecCatalogBlob> Catalog;
    }
}

