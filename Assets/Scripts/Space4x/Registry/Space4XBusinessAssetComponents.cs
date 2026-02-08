using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public enum Space4XBusinessAssetType : byte
    {
        Facility = 0,
        Market = 1,
        Ship = 2,
        Module = 3,
        Inventory = 4
    }

    /// <summary>
    /// Marks an asset (facility/market/ship) as owned by a business.
    /// </summary>
    public struct Space4XBusinessAssetOwner : IComponentData
    {
        public Entity Business;
        public Space4XBusinessAssetType AssetType;
        public uint AssignedTick;
        public FixedString64Bytes CatalogId;
    }

    /// <summary>
    /// Tracks assets owned by a business.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct Space4XBusinessAssetLink : IBufferElementData
    {
        public Entity Asset;
        public Space4XBusinessAssetType AssetType;
        public uint AssignedTick;
        public FixedString64Bytes CatalogId;
    }
}
