using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Updates market prices based on supply and demand.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XMarketPriceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMarket>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (market, prices, events, entity) in
                SystemAPI.Query<RefRW<Space4XMarket>, DynamicBuffer<MarketPriceEntry>, DynamicBuffer<MarketEvent>>()
                    .WithEntityAccess())
            {
                var pricesBuffer = prices;
                var eventsBuffer = events;
                // Skip embargoed markets
                if (market.ValueRO.IsEmbargoed != 0)
                {
                    continue;
                }

                market.ValueRW.LastUpdateTick = currentTick;

                // Update each resource price
                for (int i = 0; i < pricesBuffer.Length; i++)
                {
                    var price = pricesBuffer[i];

                    // Calculate base price from supply/demand
                    float newPrice = MarketMath.CalculatePrice(
                        price.BasePrice,
                        price.Supply,
                        price.Demand,
                        (float)price.Volatility
                    );

                    // Apply market events
                    for (int e = 0; e < eventsBuffer.Length; e++)
                    {
                        var evt = eventsBuffer[e];
                        if (evt.RemainingTicks > 0 &&
                            (evt.AffectedResource == price.ResourceType || evt.Type == MarketEventType.Crash || evt.Type == MarketEventType.Boom))
                        {
                            newPrice = MarketMath.ApplyEventModifier(newPrice, evt.Type, evt.PriceModifier);
                        }
                    }

                    // Apply market size stability
                    float stabilityFactor = market.ValueRO.Size switch
                    {
                        MarketSize.Small => 0.7f,
                        MarketSize.Medium => 0.85f,
                        MarketSize.Large => 0.95f,
                        MarketSize.Major => 0.98f,
                        MarketSize.Capital => 1.0f,
                        _ => 0.8f
                    };

                    // Blend toward new price
                    newPrice = math.lerp(price.BuyPrice, newPrice, 1f - stabilityFactor);

                    // Set buy/sell prices with spread
                    float spread = 0.05f + (1f - (float)market.ValueRO.MarketHealth) * 0.1f;
                    price.BuyPrice = newPrice;
                    price.SellPrice = newPrice * (1f - spread);

                    // Decay supply/demand toward equilibrium
                    price.Supply = math.lerp(price.Supply, price.Demand, 0.01f);
                    price.Demand = math.lerp(price.Demand, price.Supply, 0.01f);

                    pricesBuffer[i] = price;
                }

                // Process market events
                for (int e = eventsBuffer.Length - 1; e >= 0; e--)
                {
                    var evt = eventsBuffer[e];
                    if (evt.RemainingTicks > 0)
                    {
                        evt.RemainingTicks--;
                        eventsBuffer[e] = evt;
                    }
                    else if (evt.RemainingTicks == 0 && evt.Duration > 0)
                    {
                        eventsBuffer.RemoveAt(e);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Processes trade offers and matches buyers with sellers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XMarketPriceSystem))]
    public partial struct Space4XTradeMatchingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMarket>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (market, prices, offers, entity) in
                SystemAPI.Query<RefRO<Space4XMarket>, DynamicBuffer<MarketPriceEntry>, DynamicBuffer<TradeOffer>>()
                    .WithEntityAccess())
            {
                var pricesBuffer = prices;
                var offersBuffer = offers;
                // Remove expired and fulfilled offers
                for (int i = offersBuffer.Length - 1; i >= 0; i--)
                {
                    var offer = offersBuffer[i];
                    if (offer.IsFulfilled != 0 || (offer.ExpirationTick > 0 && currentTick > offer.ExpirationTick))
                    {
                        offersBuffer.RemoveAt(i);
                    }
                }

                // Match buy and sell offers
                for (int b = 0; b < offersBuffer.Length; b++)
                {
                    var buyOffer = offersBuffer[b];
                    if (buyOffer.Type != TradeOfferType.Buy || buyOffer.IsFulfilled != 0)
                    {
                        continue;
                    }

                    for (int s = 0; s < offersBuffer.Length; s++)
                    {
                        if (b == s) continue;

                        var sellOffer = offersBuffer[s];
                        if (sellOffer.Type != TradeOfferType.Sell ||
                            sellOffer.IsFulfilled != 0 ||
                            sellOffer.ResourceType != buyOffer.ResourceType)
                        {
                            continue;
                        }

                        // Price match
                        if (buyOffer.PricePerUnit >= sellOffer.PricePerUnit)
                        {
                            // Execute trade
                            float tradeQuantity = math.min(buyOffer.Quantity, sellOffer.Quantity);
                            float tradePrice = (buyOffer.PricePerUnit + sellOffer.PricePerUnit) * 0.5f;

                            // Update supply/demand in market
                            for (int p = 0; p < pricesBuffer.Length; p++)
                            {
                                var price = pricesBuffer[p];
                                if (price.ResourceType == buyOffer.ResourceType)
                                {
                                    price.Supply -= tradeQuantity;
                                    price.Demand -= tradeQuantity;
                                    pricesBuffer[p] = price;
                                    break;
                                }
                            }

                            // Update offers
                            buyOffer.Quantity -= tradeQuantity;
                            sellOffer.Quantity -= tradeQuantity;

                            if (buyOffer.Quantity <= 0)
                            {
                                buyOffer.IsFulfilled = 1;
                            }
                            if (sellOffer.Quantity <= 0)
                            {
                                sellOffer.IsFulfilled = 1;
                            }

                            offersBuffer[b] = buyOffer;
                            offersBuffer[s] = sellOffer;

                            if (buyOffer.IsFulfilled != 0) break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates trade routes and calculates profits.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XTradeRouteSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XTradeRoute>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (route, status, entity) in
                SystemAPI.Query<RefRW<Space4XTradeRoute>, RefRW<TradeRouteStatus>>()
                    .WithEntityAccess())
            {
                if (route.ValueRO.IsActive == 0)
                {
                    continue;
                }

                // Progress trade run
                switch (status.ValueRO.Phase)
                {
                    case TradeRoutePhase.Idle:
                        // Check if it's time for a new trip
                        if (currentTick >= route.ValueRO.LastTripTick + route.ValueRO.TripFrequency)
                        {
                            status.ValueRW.Phase = TradeRoutePhase.TravelingToSource;
                            status.ValueRW.Progress = (half)0f;
                        }
                        break;

                    case TradeRoutePhase.TravelingToSource:
                        status.ValueRW.Progress = (half)((float)status.ValueRO.Progress + 0.01f);
                        if ((float)status.ValueRO.Progress >= 1f)
                        {
                            status.ValueRW.Phase = TradeRoutePhase.Loading;
                            status.ValueRW.Progress = (half)0f;
                        }
                        break;

                    case TradeRoutePhase.Loading:
                        // Would load cargo from source market
                        status.ValueRW.CargoType = route.ValueRO.PrimaryResource;
                        status.ValueRW.CargoQuantity = route.ValueRO.VolumePerTrip;

                        // Get purchase price from source market
                        if (SystemAPI.HasBuffer<MarketPriceEntry>(route.ValueRO.SourceMarket))
                        {
                            var prices = SystemAPI.GetBuffer<MarketPriceEntry>(route.ValueRO.SourceMarket);
                            for (int i = 0; i < prices.Length; i++)
                            {
                                if (prices[i].ResourceType == route.ValueRO.PrimaryResource)
                                {
                                    status.ValueRW.PurchasePrice = prices[i].BuyPrice;
                                    break;
                                }
                            }
                        }

                        status.ValueRW.Phase = TradeRoutePhase.TravelingToDestination;
                        status.ValueRW.Progress = (half)0f;
                        break;

                    case TradeRoutePhase.TravelingToDestination:
                        status.ValueRW.Progress = (half)((float)status.ValueRO.Progress + 0.01f);
                        if ((float)status.ValueRO.Progress >= 1f)
                        {
                            status.ValueRW.Phase = TradeRoutePhase.Unloading;
                            status.ValueRW.Progress = (half)0f;
                        }
                        break;

                    case TradeRoutePhase.Unloading:
                        // Calculate and record profit
                        if (SystemAPI.HasBuffer<MarketPriceEntry>(route.ValueRO.DestinationMarket))
                        {
                            var prices = SystemAPI.GetBuffer<MarketPriceEntry>(route.ValueRO.DestinationMarket);
                            for (int i = 0; i < prices.Length; i++)
                            {
                                if (prices[i].ResourceType == route.ValueRO.PrimaryResource)
                                {
                                    float sellPrice = prices[i].SellPrice;
                                    float profit = (sellPrice - status.ValueRO.PurchasePrice) * status.ValueRO.CargoQuantity;

                                    route.ValueRW.TotalProfit += profit;
                                    route.ValueRW.ProfitMargin = MarketMath.CalculateProfitMargin(
                                        status.ValueRO.PurchasePrice,
                                        sellPrice,
                                        0.05f, // Tax
                                        (float)route.ValueRO.RiskLevel
                                    );
                                    break;
                                }
                            }
                        }

                        status.ValueRW.CargoQuantity = 0;
                        status.ValueRW.Phase = TradeRoutePhase.Returning;
                        status.ValueRW.Progress = (half)0f;
                        status.ValueRW.SuccessfulTrips++;
                        break;

                    case TradeRoutePhase.Returning:
                        status.ValueRW.Progress = (half)((float)status.ValueRO.Progress + 0.01f);
                        if ((float)status.ValueRO.Progress >= 1f)
                        {
                            status.ValueRW.Phase = TradeRoutePhase.Idle;
                            route.ValueRW.LastTripTick = currentTick;
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Updates faction economic resources.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFactionEconomySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FactionResources>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (resources, faction) in SystemAPI.Query<RefRW<FactionResources>, RefRO<Space4XFaction>>())
            {
                // Apply income and expenses
                float netIncome = resources.ValueRO.IncomeRate - resources.ValueRO.ExpenseRate;
                resources.ValueRW.Credits = math.max(0, resources.ValueRO.Credits + netIncome);

                // Small passive income for player factions
                if (faction.ValueRO.Type == FactionType.Player)
                {
                    resources.ValueRW.Credits += 0.1f;
                }
            }
        }
    }

    /// <summary>
    /// Processes embargo effects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XEmbargoSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EmbargoEntry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var embargoes in SystemAPI.Query<DynamicBuffer<EmbargoEntry>>())
            {
                var embargoBuffer = embargoes;

                // Remove expired embargoes
                for (int i = embargoBuffer.Length - 1; i >= 0; i--)
                {
                    var embargo = embargoBuffer[i];
                    if (embargo.EndTick > 0 && currentTick > embargo.EndTick)
                    {
                        embargoBuffer.RemoveAt(i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Spawns market events based on conditions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XMarketEventSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMarket>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            // Only check periodically
            if (currentTick % 1000 != 0)
            {
                return;
            }

            foreach (var (market, prices, events, entity) in
                SystemAPI.Query<RefRO<Space4XMarket>, DynamicBuffer<MarketPriceEntry>, DynamicBuffer<MarketEvent>>()
                    .WithEntityAccess())
            {
                var pricesBuffer = prices;
                var eventsBuffer = events;

                // Check for extreme supply/demand conditions
                for (int i = 0; i < pricesBuffer.Length; i++)
                {
                    var price = pricesBuffer[i];

                    // Shortage event
                    if (price.Supply < price.Demand * 0.3f && !HasEventForResource(eventsBuffer, price.ResourceType))
                    {
                        if (eventsBuffer.Length < eventsBuffer.Capacity)
                        {
                            eventsBuffer.Add(new MarketEvent
                            {
                                Type = MarketEventType.Shortage,
                                AffectedResource = price.ResourceType,
                                PriceModifier = 0.5f,
                                Duration = 500,
                                RemainingTicks = 500
                            });
                        }
                    }
                    // Surplus event
                    else if (price.Supply > price.Demand * 3f && !HasEventForResource(eventsBuffer, price.ResourceType))
                    {
                        if (eventsBuffer.Length < eventsBuffer.Capacity)
                        {
                            eventsBuffer.Add(new MarketEvent
                            {
                                Type = MarketEventType.Surplus,
                                AffectedResource = price.ResourceType,
                                PriceModifier = 0.3f,
                                Duration = 300,
                                RemainingTicks = 300
                            });
                        }
                    }
                }
            }
        }

        private bool HasEventForResource(DynamicBuffer<MarketEvent> events, MarketResourceType resource)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].AffectedResource == resource && events[i].RemainingTicks > 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Telemetry for economy system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XEconomyTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMarket>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int marketCount = 0;
            int activeRoutes = 0;
            float totalTradeProfit = 0f;
            int activeEvents = 0;

            foreach (var market in SystemAPI.Query<RefRO<Space4XMarket>>())
            {
                marketCount++;
            }

            foreach (var (route, status) in SystemAPI.Query<RefRO<Space4XTradeRoute>, RefRO<TradeRouteStatus>>())
            {
                if (route.ValueRO.IsActive != 0)
                {
                    activeRoutes++;
                    totalTradeProfit += route.ValueRO.TotalProfit;
                }
            }

            foreach (var events in SystemAPI.Query<DynamicBuffer<MarketEvent>>())
            {
                for (int i = 0; i < events.Length; i++)
                {
                    if (events[i].RemainingTicks > 0)
                    {
                        activeEvents++;
                    }
                }
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Economy] Markets: {marketCount}, Routes: {activeRoutes}, Profit: {totalTradeProfit:F0}, Events: {activeEvents}");
        }
    }
}
