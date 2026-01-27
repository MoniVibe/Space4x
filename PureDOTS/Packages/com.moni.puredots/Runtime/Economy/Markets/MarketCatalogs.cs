using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Markets
{
    /// <summary>
    /// Base price blob asset.
    /// Base prices per GoodType.
    /// </summary>
    public struct BasePriceBlob
    {
        public GoodType GoodType;
        public float Price;
    }

    /// <summary>
    /// Catalog blob containing base prices.
    /// </summary>
    public struct BasePriceCatalogBlob
    {
        public BlobArray<BasePriceBlob> Prices;
    }

    /// <summary>
    /// Market pricing configuration blob.
    /// Supply/demand exponent, caps, wealth breakpoints, event multipliers.
    /// </summary>
    public struct MarketPricingConfigBlob
    {
        public float SupplyDemandExponent;
        public float MinMultiplier;
        public float MaxMultiplier;
        public BlobArray<WealthBreakpointBlob> WealthBreakpoints;
        public BlobArray<EventMultiplierBlob> EventMultipliers;
    }

    /// <summary>
    /// Wealth breakpoint for price multiplier.
    /// </summary>
    public struct WealthBreakpointBlob
    {
        public float Wealth;
        public float Multiplier;
    }

    /// <summary>
    /// Event multiplier for price adjustment.
    /// </summary>
    public struct EventMultiplierBlob
    {
        public FixedString64Bytes EventType;
        public float Multiplier;
    }

    /// <summary>
    /// GoodType mapping blob.
    /// Maps ItemSpec categories to GoodTypes.
    /// </summary>
    public struct GoodTypeMappingBlob
    {
        public FixedString64Bytes ItemId;
        public GoodType GoodType;
    }

    /// <summary>
    /// Catalog blob containing GoodType mappings.
    /// </summary>
    public struct GoodTypeMappingCatalogBlob
    {
        public BlobArray<GoodTypeMappingBlob> Mappings;
    }

    /// <summary>
    /// Singleton components holding catalog references.
    /// </summary>
    public struct BasePriceCatalog : IComponentData
    {
        public BlobAssetReference<BasePriceCatalogBlob> Catalog;
    }

    public struct MarketPricingConfig : IComponentData
    {
        public BlobAssetReference<MarketPricingConfigBlob> Config;
    }

    public struct GoodTypeMappingCatalog : IComponentData
    {
        public BlobAssetReference<GoodTypeMappingCatalogBlob> Catalog;
    }
}

