using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Markets
{
    /// <summary>
    /// Good type enum for market pricing.
    /// Maps ItemSpec categories to GoodTypes for pricing.
    /// </summary>
    public enum GoodType : byte
    {
        Food = 0,
        RawMaterials = 1,
        ProcessedMaterials = 2,
        Tools = 3,
        Weapons = 4,
        Armor = 5,
        Luxury = 6,
        Fuel = 7
    }

    /// <summary>
    /// Market price buffer element.
    /// Per-GoodType pricing data for a settlement.
    /// </summary>
    public struct MarketPrice : IBufferElementData
    {
        public GoodType GoodType;
        public float BasePrice;
        public float CurrentPrice;
        public float Supply;
        public float Demand;
        public float SupplyDemandRatio;
        public float SupplyDemandMultiplier;
        public float VillageWealthMultiplier;
        public float EventMultiplier;
    }

    /// <summary>
    /// Market buy intent component.
    /// Request to buy goods at market price.
    /// </summary>
    public struct MarketBuyIntent : IComponentData
    {
        public Entity Buyer;
        public GoodType GoodType;
        public float Quantity;
        public float MaxPrice;
    }

    /// <summary>
    /// Market sell intent component.
    /// Request to sell goods at market price.
    /// </summary>
    public struct MarketSellIntent : IComponentData
    {
        public Entity Seller;
        public GoodType GoodType;
        public float Quantity;
        public float MinPrice;
    }
}

