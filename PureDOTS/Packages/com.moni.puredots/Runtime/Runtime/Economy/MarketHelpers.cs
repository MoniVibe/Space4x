using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Economy
{
    /// <summary>
    /// Static helpers for market calculations.
    /// </summary>
    [BurstCompile]
    public static class MarketHelpers
    {
        /// <summary>
        /// Default market configuration.
        /// </summary>
        public static MarketConfig DefaultConfig => new MarketConfig
        {
            PriceUpdateInterval = 60f,
            MaxPriceChange = 0.1f,
            MinPriceMultiplier = 0.1f,
            MaxPriceMultiplier = 10f,
            HistoryLength = 32
        };

        /// <summary>
        /// Calculates price from supply/demand ratio.
        /// </summary>
        public static float CalculatePrice(float basePrice, float supply, float demand, float elasticity)
        {
            if (supply <= 0.001f)
            {
                // Extreme scarcity
                return basePrice * 10f;
            }

            float ratio = demand / supply;
            float modifier = math.pow(ratio, elasticity);
            return basePrice * math.clamp(modifier, 0.1f, 10f);
        }

        /// <summary>
        /// Calculates price with market event effects.
        /// </summary>
        public static float CalculatePriceWithEvents(
            float basePrice,
            float supply,
            float demand,
            float elasticity,
            MarketEventType eventType,
            float eventMagnitude)
        {
            float price = CalculatePrice(basePrice, supply, demand, elasticity);

            switch (eventType)
            {
                case MarketEventType.Shortage:
                    price *= (1f + eventMagnitude);
                    break;
                case MarketEventType.Glut:
                    price *= (1f - eventMagnitude * 0.5f);
                    break;
                case MarketEventType.Embargo:
                    price *= (1f + eventMagnitude * 2f);
                    break;
                case MarketEventType.Subsidy:
                    price *= (1f - eventMagnitude);
                    break;
                case MarketEventType.Tariff:
                    price *= (1f + eventMagnitude);
                    break;
                case MarketEventType.Discovery:
                    price *= (1f - eventMagnitude * 0.3f);
                    break;
                case MarketEventType.Disaster:
                    price *= (1f + eventMagnitude * 1.5f);
                    break;
            }

            return price;
        }

        /// <summary>
        /// Calculates trade route profitability.
        /// </summary>
        public static float CalculateRouteProfitability(
            float buyPrice,
            float sellPrice,
            float transportCostPerUnit,
            float riskFactor)
        {
            float grossMargin = sellPrice - buyPrice;
            float netMargin = grossMargin - transportCostPerUnit;
            float riskAdjusted = netMargin * (1f - riskFactor);
            
            // Return as percentage of buy price
            return buyPrice > 0 ? riskAdjusted / buyPrice : 0;
        }

        /// <summary>
        /// Calculates price trend from history.
        /// </summary>
        public static float CalculatePriceTrend(in DynamicBuffer<PriceHistoryEntry> history, int sampleCount)
        {
            if (history.Length < 2) return 0;

            int startIdx = math.max(0, history.Length - sampleCount);
            float firstPrice = history[startIdx].Price;
            float lastPrice = history[history.Length - 1].Price;

            return firstPrice > 0 ? (lastPrice - firstPrice) / firstPrice : 0;
        }

        /// <summary>
        /// Updates supply/demand from trade activity.
        /// </summary>
        public static void ApplyTrade(ref MarketPrice price, float quantity, TradeOfferType tradeType)
        {
            if (tradeType == TradeOfferType.Buy)
            {
                price.Supply -= quantity;
                price.Demand += quantity * 0.1f; // Buying signals demand
            }
            else
            {
                price.Supply += quantity;
                price.Demand -= quantity * 0.1f; // Selling reduces pressure
            }

            price.Supply = math.max(0, price.Supply);
            price.Demand = math.max(0.1f, price.Demand);
        }

        /// <summary>
        /// Gets the active market event effect for a resource.
        /// </summary>
        public static bool TryGetActiveEvent(
            in DynamicBuffer<MarketEvent> events,
            ushort resourceId,
            uint currentTick,
            out MarketEventType eventType,
            out float magnitude)
        {
            eventType = MarketEventType.None;
            magnitude = 0f;

            for (int i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.AffectedResourceId != resourceId)
                    continue;
                
                // Check if event is still active
                if (currentTick >= evt.StartTick && currentTick < evt.StartTick + evt.DurationTicks)
                {
                    eventType = evt.Type;
                    magnitude = evt.Magnitude;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculates equilibrium price where supply meets demand.
        /// </summary>
        public static float CalculateEquilibriumPrice(float basePrice, float supply, float demand)
        {
            if (supply <= 0 || demand <= 0)
                return basePrice;
            
            // Simple equilibrium: price adjusts until S = D
            float ratio = demand / supply;
            return basePrice * math.sqrt(ratio);
        }

        /// <summary>
        /// Updates market price based on supply/demand.
        /// </summary>
        public static void UpdateMarketPrice(ref MarketPrice market, uint currentTick, in MarketConfig config)
        {
            float newPrice = CalculatePrice(
                market.BasePrice,
                market.Supply,
                market.Demand,
                (float)market.Elasticity);
            
            // Limit price change per update
            float maxChange = market.CurrentPrice * config.MaxPriceChange;
            float priceDelta = math.clamp(newPrice - market.CurrentPrice, -maxChange, maxChange);
            
            market.CurrentPrice = math.clamp(
                market.CurrentPrice + priceDelta,
                market.BasePrice * config.MinPriceMultiplier,
                market.BasePrice * config.MaxPriceMultiplier);
            
            market.LastUpdateTick = currentTick;
        }

        /// <summary>
        /// Records price history entry.
        /// </summary>
        public static void RecordPriceHistory(
            ref DynamicBuffer<PriceHistoryEntry> history,
            in MarketPrice market,
            uint currentTick,
            in MarketConfig config)
        {
            // Remove old entries if at capacity
            while (history.Length >= config.HistoryLength)
            {
                history.RemoveAt(0);
            }

            history.Add(new PriceHistoryEntry
            {
                Price = market.CurrentPrice,
                Supply = market.Supply,
                Demand = market.Demand,
                Tick = currentTick
            });
        }

        /// <summary>
        /// Creates default market price.
        /// </summary>
        public static MarketPrice CreateDefault(ushort resourceId, float basePrice, float initialSupply = 100f, float initialDemand = 100f)
        {
            return new MarketPrice
            {
                ResourceTypeId = resourceId,
                CurrentPrice = basePrice,
                BasePrice = basePrice,
                Supply = initialSupply,
                Demand = initialDemand,
                Elasticity = (half)1f
            };
        }

        /// <summary>
        /// Calculates total value of goods at current market price.
        /// </summary>
        public static float CalculateValue(in MarketPrice market, float quantity)
        {
            return market.CurrentPrice * quantity;
        }

        /// <summary>
        /// Gets price volatility from history.
        /// </summary>
        public static float CalculateVolatility(in DynamicBuffer<PriceHistoryEntry> history, int sampleCount)
        {
            if (history.Length < 2) return 0f;

            int startIdx = math.max(0, history.Length - sampleCount);
            float sum = 0f;
            float mean = 0f;
            int count = history.Length - startIdx;

            // Calculate mean
            for (int i = startIdx; i < history.Length; i++)
            {
                mean += history[i].Price;
            }
            mean /= count;

            // Calculate variance
            for (int i = startIdx; i < history.Length; i++)
            {
                float diff = history[i].Price - mean;
                sum += diff * diff;
            }

            return math.sqrt(sum / count) / mean; // Coefficient of variation
        }

        /// <summary>
        /// Applies a market event, modifying supply/demand.
        /// </summary>
        public static void ApplyMarketEvent(
            ref MarketPrice market,
            MarketEventType eventType,
            float magnitude,
            uint currentTick)
        {
            switch (eventType)
            {
                case MarketEventType.Shortage:
                    // Reduce supply
                    market.Supply = math.max(1f, market.Supply * (1f - magnitude));
                    break;

                case MarketEventType.Glut:
                    // Increase supply
                    market.Supply *= (1f + magnitude);
                    break;

                case MarketEventType.Embargo:
                    // Drastically reduce supply, slightly reduce demand
                    market.Supply = math.max(1f, market.Supply * (1f - magnitude * 0.8f));
                    market.Demand *= (1f - magnitude * 0.2f);
                    break;

                case MarketEventType.Subsidy:
                    // Increase demand due to lower effective prices
                    market.Demand *= (1f + magnitude * 0.5f);
                    break;

                case MarketEventType.Tariff:
                    // Reduce demand due to higher prices
                    market.Demand *= (1f - magnitude * 0.3f);
                    break;

                case MarketEventType.Discovery:
                    // New source, increase supply
                    market.Supply *= (1f + magnitude);
                    break;

                case MarketEventType.Disaster:
                    // Reduce supply, may increase demand (hoarding)
                    market.Supply = math.max(1f, market.Supply * (1f - magnitude * 0.7f));
                    market.Demand *= (1f + magnitude * 0.2f);
                    break;
            }

            market.LastUpdateTick = currentTick;
        }

        /// <summary>
        /// Finds arbitrage opportunity between two markets.
        /// Returns positive value if profitable to buy from marketA and sell to marketB.
        /// </summary>
        public static float FindArbitrageOpportunity(
            in MarketPrice marketA,
            in MarketPrice marketB,
            float transportCost,
            float riskFactor)
        {
            if (marketA.ResourceTypeId != marketB.ResourceTypeId)
                return 0f;

            // Check A -> B direction
            float profitAB = CalculateArbitrageProfit(
                marketA.CurrentPrice, // Buy price
                marketB.CurrentPrice, // Sell price
                transportCost,
                riskFactor);

            // Check B -> A direction
            float profitBA = CalculateArbitrageProfit(
                marketB.CurrentPrice, // Buy price
                marketA.CurrentPrice, // Sell price
                transportCost,
                riskFactor);

            // Return the more profitable direction (positive = A->B, negative = B->A)
            if (profitAB > profitBA && profitAB > 0)
                return profitAB;
            if (profitBA > profitAB && profitBA > 0)
                return -profitBA;

            return 0f;
        }

        /// <summary>
        /// Calculates arbitrage profit margin.
        /// </summary>
        private static float CalculateArbitrageProfit(
            float buyPrice,
            float sellPrice,
            float transportCost,
            float riskFactor)
        {
            if (buyPrice <= 0)
                return 0f;

            float grossProfit = sellPrice - buyPrice;
            float netProfit = grossProfit - transportCost;
            float riskAdjusted = netProfit * (1f - riskFactor);

            return riskAdjusted / buyPrice; // Return as percentage
        }

        /// <summary>
        /// Calculates market depth (liquidity).
        /// </summary>
        public static float CalculateMarketDepth(in MarketPrice market)
        {
            // Higher supply and demand means more liquid market
            return (market.Supply + market.Demand) * 0.5f;
        }

        /// <summary>
        /// Calculates price impact of a trade.
        /// </summary>
        public static float CalculatePriceImpact(
            in MarketPrice market,
            float tradeQuantity,
            TradeOfferType tradeType)
        {
            float depth = CalculateMarketDepth(market);
            if (depth <= 0)
                return 1f; // Max impact

            // Larger trades relative to depth have bigger impact
            float relativeSize = tradeQuantity / depth;
            float elasticity = (float)market.Elasticity;

            float impact = relativeSize * elasticity;

            // Buys push price up, sells push down
            return tradeType == TradeOfferType.Buy ? impact : -impact;
        }

        /// <summary>
        /// Estimates clearing price for a trade.
        /// </summary>
        public static float EstimateClearingPrice(
            in MarketPrice market,
            float quantity,
            TradeOfferType tradeType)
        {
            float impact = CalculatePriceImpact(market, quantity, tradeType);
            return market.CurrentPrice * (1f + impact);
        }

        /// <summary>
        /// Calculates supply/demand ratio.
        /// </summary>
        public static float CalculateSupplyDemandRatio(in MarketPrice market)
        {
            if (market.Demand <= 0)
                return float.MaxValue;
            return market.Supply / market.Demand;
        }

        /// <summary>
        /// Determines if market is in shortage.
        /// </summary>
        public static bool IsInShortage(in MarketPrice market, float threshold = 0.5f)
        {
            return CalculateSupplyDemandRatio(market) < threshold;
        }

        /// <summary>
        /// Determines if market has surplus.
        /// </summary>
        public static bool HasSurplus(in MarketPrice market, float threshold = 2f)
        {
            return CalculateSupplyDemandRatio(market) > threshold;
        }

        /// <summary>
        /// Calculates trade route viability score.
        /// </summary>
        public static float CalculateRouteViability(
            in TradeRoute route,
            in MarketPrice sourceMarket,
            in MarketPrice destMarket)
        {
            if (route.IsActive == 0)
                return 0f;

            float profitability = CalculateRouteProfitability(
                sourceMarket.CurrentPrice,
                destMarket.CurrentPrice,
                route.TransportCost,
                route.RiskFactor);

            // Factor in volume and risk
            float volumeFactor = math.min(1f, route.Volume / 100f);
            float riskPenalty = 1f - route.RiskFactor;

            return profitability * volumeFactor * riskPenalty;
        }

        /// <summary>
        /// Decays supply over time (consumption/spoilage).
        /// </summary>
        public static void DecaySupply(
            ref MarketPrice market,
            float decayRate,
            float deltaTime)
        {
            float decay = market.Supply * decayRate * deltaTime;
            market.Supply = math.max(1f, market.Supply - decay);
        }

        /// <summary>
        /// Regenerates demand over time (natural consumption needs).
        /// </summary>
        public static void RegenerateDemand(
            ref MarketPrice market,
            float baseDemand,
            float regenerationRate,
            float deltaTime)
        {
            float targetDemand = baseDemand;
            float diff = targetDemand - market.Demand;
            market.Demand += diff * regenerationRate * deltaTime;
            market.Demand = math.max(0.1f, market.Demand);
        }
    }
}

