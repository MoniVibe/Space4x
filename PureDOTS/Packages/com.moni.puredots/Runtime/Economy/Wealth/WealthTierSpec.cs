using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Wealth
{
    /// <summary>
    /// BlobAsset structure for wealth tier specification.
    /// Defines thresholds and social effects for each tier.
    /// </summary>
    public struct WealthTierSpecBlob
    {
        public FixedString64Bytes TierName;
        public float MinWealth;
        public float MaxWealth;
        public FixedString64Bytes Title;
        public float BaseRespect;
        public float BaseFear;
        public float BaseEnvy;
        public bool CourtEligible;
    }

    /// <summary>
    /// Catalog blob containing all wealth tier specifications.
    /// </summary>
    public struct WealthTierSpecCatalogBlob
    {
        public BlobArray<WealthTierSpecBlob> Tiers;
    }

    /// <summary>
    /// Singleton component holding the wealth tier catalog reference.
    /// </summary>
    public struct WealthTierSpecCatalog : IComponentData
    {
        public BlobAssetReference<WealthTierSpecCatalogBlob> Catalog;
    }
}

