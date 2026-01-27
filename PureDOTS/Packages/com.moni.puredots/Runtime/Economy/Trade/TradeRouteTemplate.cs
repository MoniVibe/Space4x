using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Trade
{
    /// <summary>
    /// Transport type specification blob.
    /// Speed, capacity, cost per km/day.
    /// </summary>
    public struct TransportTypeSpecBlob
    {
        public TransportMode Mode;
        public FixedString64Bytes Name;
        public float BaseSpeed;
        public float Capacity;
        public float BaseCostPerDay;
        public float CostPerKm;
    }

    /// <summary>
    /// Trade route template blob asset.
    /// Route definitions with nodes, distance, terrain, risk.
    /// </summary>
    public struct TradeRouteTemplateBlob
    {
        public FixedString64Bytes RouteId;
        public FixedString64Bytes NodeAName;
        public FixedString64Bytes NodeBName;
        public float Distance;
        public TerrainType TerrainType;
        public float TerrainDifficulty;
        public BlobArray<TransportMode> SupportedModes;
        public float BaselineTravelTime; // Days
        public float BanditChance;
        public float AccidentChance;
        public float WeatherHazardChance;
    }

    /// <summary>
    /// Catalog blob containing all trade route templates.
    /// </summary>
    public struct TradeRouteTemplateCatalogBlob
    {
        public BlobArray<TradeRouteTemplateBlob> Routes;
    }

    /// <summary>
    /// Catalog blob containing transport type specifications.
    /// </summary>
    public struct TransportTypeSpecCatalogBlob
    {
        public BlobArray<TransportTypeSpecBlob> Types;
    }

    /// <summary>
    /// Singleton components holding catalog references.
    /// </summary>
    public struct TradeRouteTemplateCatalog : IComponentData
    {
        public BlobAssetReference<TradeRouteTemplateCatalogBlob> Catalog;
    }

    public struct TransportTypeSpecCatalog : IComponentData
    {
        public BlobAssetReference<TransportTypeSpecCatalogBlob> Catalog;
    }
}

