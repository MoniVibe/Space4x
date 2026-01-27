using Unity.Entities;

namespace PureDOTS.Runtime.Economy
{
    /// <summary>
    /// Cached market price snapshot for hot path queries.
    /// Hot path systems read these values directly without recalculation.
    /// </summary>
    public struct MarketPriceSnapshot : IComponentData
    {
        /// <summary>
        /// Resource type ID.
        /// </summary>
        public ushort ResourceId;

        /// <summary>
        /// Buy price (price to purchase from market).
        /// </summary>
        public float BuyPrice;

        /// <summary>
        /// Sell price (price to sell to market).
        /// </summary>
        public float SellPrice;

        /// <summary>
        /// Demand index (0..1 normalized).
        /// 0 = no demand, 1 = maximum demand.
        /// </summary>
        public float DemandIndex;

        /// <summary>
        /// Supply index (0..1 normalized).
        /// 0 = no supply, 1 = maximum supply.
        /// </summary>
        public float SupplyIndex;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }
}

