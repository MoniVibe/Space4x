using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Market resource type for trading.
    /// </summary>
    public enum MarketResourceType : byte
    {
        Ore = 0,
        RefinedMetal = 1,
        RareEarth = 2,
        Energy = 3,
        Food = 4,
        Water = 5,
        Consumer = 6,
        Industrial = 7,
        Military = 8,
        Luxury = 9,
        Medical = 10,
        Tech = 11
    }

    /// <summary>
    /// Price entry for a single resource in a market.
    /// </summary>
    [InternalBufferCapacity(12)]
    public struct MarketPriceEntry : IBufferElementData
    {
        /// <summary>
        /// Resource type.
        /// </summary>
        public MarketResourceType ResourceType;

        /// <summary>
        /// Current buy price.
        /// </summary>
        public float BuyPrice;

        /// <summary>
        /// Current sell price.
        /// </summary>
        public float SellPrice;

        /// <summary>
        /// Available supply.
        /// </summary>
        public float Supply;

        /// <summary>
        /// Current demand.
        /// </summary>
        public float Demand;

        /// <summary>
        /// Price volatility [0, 1].
        /// </summary>
        public half Volatility;

        /// <summary>
        /// Base price for this resource.
        /// </summary>
        public float BasePrice;
    }

    /// <summary>
    /// Market state for a location (station, colony, etc).
    /// </summary>
    public struct Space4XMarket : IComponentData
    {
        /// <summary>
        /// Market location type.
        /// </summary>
        public MarketLocationType LocationType;

        /// <summary>
        /// Market size affecting price stability.
        /// </summary>
        public MarketSize Size;

        /// <summary>
        /// Tax rate on transactions.
        /// </summary>
        public half TaxRate;

        /// <summary>
        /// Black market availability [0, 1].
        /// </summary>
        public half BlackMarketAccess;

        /// <summary>
        /// Overall market health [0, 1].
        /// </summary>
        public half MarketHealth;

        /// <summary>
        /// Whether market is under embargo.
        /// </summary>
        public byte IsEmbargoed;

        /// <summary>
        /// Owning faction ID.
        /// </summary>
        public ushort OwnerFactionId;

        /// <summary>
        /// Last update tick.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Type of market location.
    /// </summary>
    public enum MarketLocationType : byte
    {
        Colony = 0,
        Station = 1,
        TradeHub = 2,
        Outpost = 3,
        PirateHaven = 4,
        BlackMarket = 5
    }

    /// <summary>
    /// Market size affecting stability and volume.
    /// </summary>
    public enum MarketSize : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        Major = 3,
        Capital = 4
    }

    /// <summary>
    /// Trade offer in a market.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct TradeOffer : IBufferElementData
    {
        /// <summary>
        /// Offer type.
        /// </summary>
        public TradeOfferType Type;

        /// <summary>
        /// Resource being traded.
        /// </summary>
        public MarketResourceType ResourceType;

        /// <summary>
        /// Quantity offered/requested.
        /// </summary>
        public float Quantity;

        /// <summary>
        /// Price per unit.
        /// </summary>
        public float PricePerUnit;

        /// <summary>
        /// Currency used for the offer.
        /// </summary>
        public FixedString64Bytes CurrencyId;

        /// <summary>
        /// Entity making the offer.
        /// </summary>
        public Entity OfferingEntity;

        /// <summary>
        /// Faction ID of offerer.
        /// </summary>
        public ushort OfferingFactionId;

        /// <summary>
        /// Tick when offer expires.
        /// </summary>
        public uint ExpirationTick;

        /// <summary>
        /// Whether offer is fulfilled.
        /// </summary>
        public byte IsFulfilled;
    }

    /// <summary>
    /// Type of trade offer.
    /// </summary>
    public enum TradeOfferType : byte
    {
        Buy = 0,
        Sell = 1,
        Contract = 2  // Long-term agreement
    }

    /// <summary>
    /// Trade route between two markets.
    /// </summary>
    public struct Space4XTradeRoute : IComponentData
    {
        /// <summary>
        /// Source market entity.
        /// </summary>
        public Entity SourceMarket;

        /// <summary>
        /// Destination market entity.
        /// </summary>
        public Entity DestinationMarket;

        /// <summary>
        /// Primary traded resource.
        /// </summary>
        public MarketResourceType PrimaryResource;

        /// <summary>
        /// Volume per trip.
        /// </summary>
        public float VolumePerTrip;

        /// <summary>
        /// Trip frequency in ticks.
        /// </summary>
        public uint TripFrequency;

        /// <summary>
        /// Last trip tick.
        /// </summary>
        public uint LastTripTick;

        /// <summary>
        /// Current profit margin.
        /// </summary>
        public float ProfitMargin;

        /// <summary>
        /// Route risk level [0, 1].
        /// </summary>
        public half RiskLevel;

        /// <summary>
        /// Total profit generated.
        /// </summary>
        public float TotalProfit;

        /// <summary>
        /// Whether route is active.
        /// </summary>
        public byte IsActive;

        /// <summary>
        /// Assigned convoy entity.
        /// </summary>
        public Entity AssignedConvoy;
    }

    /// <summary>
    /// Trade route status tracking.
    /// </summary>
    public struct TradeRouteStatus : IComponentData
    {
        /// <summary>
        /// Current phase of trade run.
        /// </summary>
        public TradeRoutePhase Phase;

        /// <summary>
        /// Cargo currently being transported.
        /// </summary>
        public MarketResourceType CargoType;

        /// <summary>
        /// Cargo quantity.
        /// </summary>
        public float CargoQuantity;

        /// <summary>
        /// Purchase price paid.
        /// </summary>
        public float PurchasePrice;

        /// <summary>
        /// Progress toward destination [0, 1].
        /// </summary>
        public half Progress;

        /// <summary>
        /// Number of successful trips.
        /// </summary>
        public uint SuccessfulTrips;

        /// <summary>
        /// Number of failed/raided trips.
        /// </summary>
        public uint FailedTrips;
    }

    /// <summary>
    /// Trade route phase.
    /// </summary>
    public enum TradeRoutePhase : byte
    {
        Idle = 0,
        TravelingToSource = 1,
        Loading = 2,
        TravelingToDestination = 3,
        Unloading = 4,
        Returning = 5
    }

    /// <summary>
    /// Economic policy for a faction.
    /// </summary>
    public struct Space4XEconomicPolicy : IComponentData
    {
        /// <summary>
        /// General tariff rate.
        /// </summary>
        public half TariffRate;

        /// <summary>
        /// Subsidy rate for domestic production.
        /// </summary>
        public half SubsidyRate;

        /// <summary>
        /// Tax rate on profits.
        /// </summary>
        public half TaxRate;

        /// <summary>
        /// Import restriction level [0, 1].
        /// </summary>
        public half ImportRestriction;

        /// <summary>
        /// Export restriction level [0, 1].
        /// </summary>
        public half ExportRestriction;

        /// <summary>
        /// Currency exchange rate multiplier.
        /// </summary>
        public float ExchangeRate;
    }

    /// <summary>
    /// Embargo entry against another faction.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct EmbargoEntry : IBufferElementData
    {
        /// <summary>
        /// Embargoed faction ID.
        /// </summary>
        public ushort TargetFactionId;

        /// <summary>
        /// Embargo severity [0, 1].
        /// </summary>
        public half Severity;

        /// <summary>
        /// Tick when embargo started.
        /// </summary>
        public uint StartTick;

        /// <summary>
        /// Tick when embargo ends (0 = indefinite).
        /// </summary>
        public uint EndTick;

        /// <summary>
        /// Resources specifically embargoed (bitmask).
        /// </summary>
        public ushort EmbargoedResources;
    }

    /// <summary>
    /// Market event affecting prices.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct MarketEvent : IBufferElementData
    {
        /// <summary>
        /// Event type.
        /// </summary>
        public MarketEventType Type;

        /// <summary>
        /// Affected resource (if specific).
        /// </summary>
        public MarketResourceType AffectedResource;

        /// <summary>
        /// Price modifier.
        /// </summary>
        public float PriceModifier;

        /// <summary>
        /// Duration in ticks.
        /// </summary>
        public uint Duration;

        /// <summary>
        /// Remaining ticks.
        /// </summary>
        public uint RemainingTicks;
    }

    /// <summary>
    /// Type of market event.
    /// </summary>
    public enum MarketEventType : byte
    {
        None = 0,
        Shortage = 1,
        Surplus = 2,
        Crash = 3,
        Boom = 4,
        Speculation = 5,
        Cartel = 6,
        Disaster = 7,
        Discovery = 8
    }

    /// <summary>
    /// Trader entity component.
    /// </summary>
    public struct TraderProfile : IComponentData
    {
        /// <summary>
        /// Cargo capacity.
        /// </summary>
        public float CargoCapacity;

        /// <summary>
        /// Travel speed modifier.
        /// </summary>
        public half SpeedModifier;

        /// <summary>
        /// Negotiation skill [0, 1].
        /// </summary>
        public half NegotiationSkill;

        /// <summary>
        /// Risk tolerance [0, 1].
        /// </summary>
        public half RiskTolerance;

        /// <summary>
        /// Credits available for trading.
        /// </summary>
        public float AvailableCredits;

        /// <summary>
        /// Total profits earned.
        /// </summary>
        public float TotalProfits;

        /// <summary>
        /// Home market entity.
        /// </summary>
        public Entity HomeMarket;
    }

    /// <summary>
    /// Math utilities for market calculations (candidates for PureDOTS).
    /// </summary>
    public static class MarketMath
    {
        /// <summary>
        /// Calculates price based on supply and demand.
        /// </summary>
        public static float CalculatePrice(float basePrice, float supply, float demand, float volatility)
        {
            if (supply <= 0) return basePrice * 10f; // Extreme scarcity

            float ratio = demand / supply;
            float priceChange = (ratio - 1f) * volatility;

            return math.max(basePrice * 0.1f, basePrice * (1f + priceChange));
        }

        /// <summary>
        /// Calculates trade route profit margin.
        /// </summary>
        public static float CalculateProfitMargin(float buyPrice, float sellPrice, float taxRate, float riskPremium)
        {
            float grossProfit = sellPrice - buyPrice;
            float taxCost = sellPrice * taxRate;
            float riskCost = sellPrice * riskPremium;

            return (grossProfit - taxCost - riskCost) / buyPrice;
        }

        /// <summary>
        /// Calculates route efficiency score.
        /// </summary>
        public static float CalculateRouteEfficiency(float profitMargin, float tripDuration, float riskLevel)
        {
            if (tripDuration <= 0) return 0;

            float profitPerTick = profitMargin / tripDuration;
            float riskPenalty = 1f - riskLevel * 0.5f;

            return profitPerTick * riskPenalty;
        }

        /// <summary>
        /// Applies market event modifier to price.
        /// </summary>
        public static float ApplyEventModifier(float basePrice, MarketEventType eventType, float eventModifier)
        {
            return eventType switch
            {
                MarketEventType.Shortage => basePrice * (1f + eventModifier),
                MarketEventType.Surplus => basePrice * (1f - eventModifier * 0.5f),
                MarketEventType.Crash => basePrice * (1f - eventModifier * 0.8f),
                MarketEventType.Boom => basePrice * (1f + eventModifier * 0.5f),
                MarketEventType.Speculation => basePrice * (1f + eventModifier * math.sin(eventModifier * 10f)),
                MarketEventType.Cartel => basePrice * (1f + eventModifier * 0.3f),
                MarketEventType.Disaster => basePrice * (1f + eventModifier * 2f),
                MarketEventType.Discovery => basePrice * (1f - eventModifier * 0.6f),
                _ => basePrice
            };
        }

        /// <summary>
        /// Calculates tariff cost.
        /// </summary>
        public static float CalculateTariff(float transactionValue, float tariffRate, bool isImport)
        {
            return transactionValue * tariffRate * (isImport ? 1f : 0.5f);
        }

        /// <summary>
        /// Determines if trade is allowed given embargo.
        /// </summary>
        public static bool IsTradeAllowed(ushort buyerFactionId, ushort sellerFactionId, MarketResourceType resource, in EmbargoEntry embargo)
        {
            if (embargo.TargetFactionId != buyerFactionId && embargo.TargetFactionId != sellerFactionId)
            {
                return true;
            }

            // Check if specific resource is embargoed
            ushort resourceBit = (ushort)(1 << (int)resource);
            if ((embargo.EmbargoedResources & resourceBit) != 0)
            {
                return false;
            }

            // Full embargo
            return (float)embargo.Severity < 1f;
        }
    }
}
