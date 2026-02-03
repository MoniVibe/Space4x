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
        private ComponentLookup<PrimaryCurrency> _primaryLookup;
        private ComponentLookup<CurrencyIssuer> _issuerLookup;
        private BufferLookup<GuildMembershipEntry> _guildMembershipLookup;
        private EntityQuery _factionQuery;
        private EntityQuery _issuerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XMarket>();
            _primaryLookup = state.GetComponentLookup<PrimaryCurrency>(true);
            _issuerLookup = state.GetComponentLookup<CurrencyIssuer>(true);
            _guildMembershipLookup = state.GetBufferLookup<GuildMembershipEntry>(true);
            _factionQuery = state.GetEntityQuery(ComponentType.ReadOnly<Space4XFaction>());
            _issuerQuery = state.GetEntityQuery(ComponentType.ReadOnly<CurrencyIssuer>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            _primaryLookup.Update(ref state);
            _issuerLookup.Update(ref state);
            _guildMembershipLookup.Update(ref state);

            var factionMap = BuildFactionMap(ref state);
            var issuerMap = BuildIssuerMap(ref state);

            foreach (var (market, prices, offers, entity) in
                SystemAPI.Query<RefRO<Space4XMarket>, DynamicBuffer<MarketPriceEntry>, DynamicBuffer<TradeOffer>>()
                    .WithEntityAccess())
            {
                var pricesBuffer = prices;
                var offersBuffer = offers;
                ResolveOfferCurrencies(ref offersBuffer, market.ValueRO.OwnerFactionId, factionMap);
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

                        if (buyOffer.CurrencyId.Length == 0 ||
                            sellOffer.CurrencyId.Length == 0 ||
                            !buyOffer.CurrencyId.Equals(sellOffer.CurrencyId))
                        {
                            continue;
                        }

                        float discount = ResolveGuildDiscount(buyOffer, buyOffer.CurrencyId, issuerMap, factionMap);
                        float effectiveSellPrice = sellOffer.PricePerUnit * (1f - discount);

                        // Price match
                        if (buyOffer.PricePerUnit >= effectiveSellPrice)
                        {
                            // Execute trade
                            float tradeQuantity = math.min(buyOffer.Quantity, sellOffer.Quantity);
                            float tradePrice = (buyOffer.PricePerUnit + effectiveSellPrice) * 0.5f;

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

            factionMap.Dispose();
            issuerMap.Dispose();
        }

        private struct CurrencyIssuerRecord
        {
            public Entity Issuer;
            public CurrencyIssuerType Type;
            public float Discount;
            public float RequiredStanding;
        }

        private NativeHashMap<ushort, Entity> BuildFactionMap(ref SystemState state)
        {
            int capacity = math.max(_factionQuery.CalculateEntityCount(), 1);
            var map = new NativeHashMap<ushort, Entity>(capacity, Allocator.Temp);
            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                map[faction.ValueRO.FactionId] = entity;
            }
            return map;
        }

        private NativeHashMap<FixedString64Bytes, CurrencyIssuerRecord> BuildIssuerMap(ref SystemState state)
        {
            int capacity = math.max(_issuerQuery.CalculateEntityCount(), 1);
            var map = new NativeHashMap<FixedString64Bytes, CurrencyIssuerRecord>(capacity, Allocator.Temp);
            foreach (var (issuer, entity) in SystemAPI.Query<RefRO<CurrencyIssuer>>().WithEntityAccess())
            {
                map[issuer.ValueRO.CurrencyId] = new CurrencyIssuerRecord
                {
                    Issuer = entity,
                    Type = issuer.ValueRO.IssuerType,
                    Discount = (float)issuer.ValueRO.MemberDiscount,
                    RequiredStanding = (float)issuer.ValueRO.RequiredStanding
                };
            }
            return map;
        }

        private void ResolveOfferCurrencies(
            ref DynamicBuffer<TradeOffer> offers,
            ushort marketOwnerFactionId,
            in NativeHashMap<ushort, Entity> factionMap)
        {
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                if (offer.CurrencyId.Length > 0)
                {
                    continue;
                }

                offer.CurrencyId = ResolveOfferCurrency(offer, marketOwnerFactionId, factionMap);
                offers[i] = offer;
            }
        }

        private FixedString64Bytes ResolveOfferCurrency(
            in TradeOffer offer,
            ushort marketOwnerFactionId,
            in NativeHashMap<ushort, Entity> factionMap)
        {
            if (offer.OfferingEntity != Entity.Null && _primaryLookup.HasComponent(offer.OfferingEntity))
            {
                var currency = _primaryLookup[offer.OfferingEntity].CurrencyId;
                if (currency.Length > 0)
                {
                    return currency;
                }
            }

            if (offer.OfferingFactionId != 0 &&
                factionMap.TryGetValue(offer.OfferingFactionId, out var factionEntity) &&
                _primaryLookup.HasComponent(factionEntity))
            {
                var currency = _primaryLookup[factionEntity].CurrencyId;
                if (currency.Length > 0)
                {
                    return currency;
                }
            }

            if (marketOwnerFactionId != 0 &&
                factionMap.TryGetValue(marketOwnerFactionId, out var ownerEntity) &&
                _primaryLookup.HasComponent(ownerEntity))
            {
                var currency = _primaryLookup[ownerEntity].CurrencyId;
                if (currency.Length > 0)
                {
                    return currency;
                }
            }

            return default;
        }

        private float ResolveGuildDiscount(
            in TradeOffer buyOffer,
            in FixedString64Bytes currencyId,
            in NativeHashMap<FixedString64Bytes, CurrencyIssuerRecord> issuerMap,
            in NativeHashMap<ushort, Entity> factionMap)
        {
            if (!issuerMap.TryGetValue(currencyId, out var issuer))
            {
                return 0f;
            }

            if (issuer.Type != CurrencyIssuerType.Guild || issuer.Discount <= 0f)
            {
                return 0f;
            }

            float requiredStanding = math.clamp(issuer.RequiredStanding, 0f, 1f);
            if (requiredStanding <= 0f)
            {
                return issuer.Discount;
            }

            if (HasGuildStanding(buyOffer.OfferingEntity, buyOffer.OfferingFactionId, issuer.Issuer, requiredStanding, factionMap))
            {
                return issuer.Discount;
            }

            return 0f;
        }

        private bool HasGuildStanding(
            Entity memberEntity,
            ushort memberFactionId,
            Entity guildEntity,
            float requiredStanding,
            in NativeHashMap<ushort, Entity> factionMap)
        {
            if (guildEntity == Entity.Null)
            {
                return false;
            }

            if (memberEntity != Entity.Null && HasGuildStanding(memberEntity, guildEntity, requiredStanding))
            {
                return true;
            }

            if (memberFactionId != 0 &&
                factionMap.TryGetValue(memberFactionId, out var factionEntity) &&
                HasGuildStanding(factionEntity, guildEntity, requiredStanding))
            {
                return true;
            }

            return false;
        }

        private bool HasGuildStanding(Entity memberEntity, Entity guildEntity, float requiredStanding)
        {
            if (!_guildMembershipLookup.HasBuffer(memberEntity))
            {
                return false;
            }

            var memberships = _guildMembershipLookup[memberEntity];
            for (int i = 0; i < memberships.Length; i++)
            {
                if (memberships[i].Guild != guildEntity)
                {
                    continue;
                }

                if ((float)memberships[i].Loyalty >= requiredStanding)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Updates trade routes and calculates profits.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XTradeRouteSystem : ISystem
    {
        private BufferLookup<MarketPriceEntry> _priceLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XTradeRoute>();
            _priceLookup = state.GetBufferLookup<MarketPriceEntry>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            _priceLookup.Update(ref state);

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
                        // Load cargo from source market inventory.
                        status.ValueRW.CargoType = route.ValueRO.PrimaryResource;
                        status.ValueRW.CargoQuantity = 0f;
                        status.ValueRW.PurchasePrice = 0f;

                        if (TryLoadMarketCargo(ref _priceLookup, route.ValueRO.SourceMarket, route.ValueRO.PrimaryResource, route.ValueRO.VolumePerTrip, out var purchasePrice, out var loadedAmount))
                        {
                            status.ValueRW.PurchasePrice = purchasePrice;
                            status.ValueRW.CargoQuantity = loadedAmount;
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
                        if (TryUnloadMarketCargo(ref _priceLookup, route.ValueRO.DestinationMarket, route.ValueRO.PrimaryResource, status.ValueRO.CargoQuantity, out var sellPrice))
                        {
                            float profit = (sellPrice - status.ValueRO.PurchasePrice) * status.ValueRO.CargoQuantity;

                            route.ValueRW.TotalProfit += profit;
                            route.ValueRW.ProfitMargin = MarketMath.CalculateProfitMargin(
                                status.ValueRO.PurchasePrice,
                                sellPrice,
                                0.05f, // Tax
                                (float)route.ValueRO.RiskLevel
                            );
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

        private static bool TryLoadMarketCargo(
            ref BufferLookup<MarketPriceEntry> priceLookup,
            Entity market,
            MarketResourceType resource,
            float requestedAmount,
            out float purchasePrice,
            out float loadedAmount)
        {
            purchasePrice = 0f;
            loadedAmount = 0f;

            if (!priceLookup.HasBuffer(market))
            {
                return false;
            }

            var prices = priceLookup[market];
            for (int i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                if (price.ResourceType != resource)
                {
                    continue;
                }

                purchasePrice = price.BuyPrice;
                loadedAmount = math.min(price.Supply, requestedAmount);
                if (loadedAmount > 0f)
                {
                    price.Supply = math.max(0f, price.Supply - loadedAmount);
                    prices[i] = price;
                }

                return true;
            }

            return false;
        }

        private static bool TryUnloadMarketCargo(
            ref BufferLookup<MarketPriceEntry> priceLookup,
            Entity market,
            MarketResourceType resource,
            float amount,
            out float sellPrice)
        {
            sellPrice = 0f;

            if (!priceLookup.HasBuffer(market))
            {
                return false;
            }

            var prices = priceLookup[market];
            for (int i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                if (price.ResourceType != resource)
                {
                    continue;
                }

                sellPrice = price.SellPrice;
                if (amount > 0f)
                {
                    price.Supply += amount;
                    price.Demand = math.max(0f, price.Demand - amount);
                    prices[i] = price;
                }

                return true;
            }

            return false;
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
