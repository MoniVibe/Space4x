using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy
{
    /// <summary>
    /// Trade offer types.
    /// </summary>
    public enum TradeOfferType : byte
    {
        Buy = 0,
        Sell = 1
    }

    /// <summary>
    /// Market event types affecting prices.
    /// </summary>
    public enum MarketEventType : byte
    {
        None = 0,
        Shortage = 1,       // Reduced supply
        Glut = 2,           // Excess supply
        Embargo = 3,        // Trade restriction
        Subsidy = 4,        // Price reduction
        Tariff = 5,         // Price increase
        Discovery = 6,      // New source found
        Disaster = 7        // Production disrupted
    }

    /// <summary>
    /// Market price for a resource type at a location.
    /// </summary>
    public struct MarketPrice : IComponentData
    {
        public ushort ResourceTypeId;
        public float CurrentPrice;
        public float BasePrice;         // Natural equilibrium price
        public float Supply;            // Available quantity
        public float Demand;            // Desired quantity
        public half Elasticity;         // Price sensitivity (0.5 = inelastic, 2.0 = elastic)
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Price history for trend analysis.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct PriceHistoryEntry : IBufferElementData
    {
        public float Price;
        public float Supply;
        public float Demand;
        public uint Tick;
    }

    /// <summary>
    /// Trade offer from a merchant or market.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct TradeOffer : IBufferElementData
    {
        public ushort ResourceTypeId;
        public float Quantity;
        public float PricePerUnit;
        public Entity OffererEntity;
        public TradeOfferType Type;
        public uint ExpiryTick;
    }

    /// <summary>
    /// Trade route definition.
    /// </summary>
    public struct TradeRoute : IComponentData
    {
        public Entity SourceMarket;
        public Entity DestinationMarket;
        public ushort ResourceTypeId;
        public float Volume;            // Units per trip
        public float TransportCost;     // Cost per unit
        public float RiskFactor;        // Loss chance (pirates, hazards)
        public half Profitability;      // Calculated profit margin
        public byte IsActive;
    }

    /// <summary>
    /// Market event affecting prices.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct MarketEvent : IBufferElementData
    {
        public MarketEventType Type;
        public ushort AffectedResourceId;
        public float Magnitude;         // Effect strength
        public uint StartTick;
        public uint DurationTicks;
    }

    /// <summary>
    /// Configuration for market simulation.
    /// </summary>
    public struct MarketConfig : IComponentData
    {
        public float PriceUpdateInterval;    // Ticks between price updates
        public float MaxPriceChange;         // Max % change per update
        public float MinPriceMultiplier;     // Floor price multiplier
        public float MaxPriceMultiplier;     // Ceiling price multiplier
        public byte HistoryLength;           // Price history entries to keep
    }

    /// <summary>
    /// Trade transaction record.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct TradeTransaction : IBufferElementData
    {
        public ushort ResourceTypeId;
        public float Quantity;
        public float TotalPrice;
        public Entity BuyerEntity;
        public Entity SellerEntity;
        public uint Tick;
    }

    /// <summary>
    /// Request to execute a trade.
    /// </summary>
    public struct TradeRequest : IComponentData
    {
        public Entity MarketEntity;
        public ushort ResourceTypeId;
        public float Quantity;
        public TradeOfferType Type;     // Buy or Sell
        public float MaxPrice;          // Max willing to pay (for Buy)
        public float MinPrice;          // Min willing to accept (for Sell)
    }
}

