using Unity.Entities;
using Unity.Burst;
using PureDOTS.Runtime.Economy;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.Economy
{
    /// <summary>
    /// System that updates market prices based on supply/demand.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct MarketPriceUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            MarketConfig config = MarketHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<MarketConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            // Update market prices
            foreach (var (market, events, history, entity) in 
                SystemAPI.Query<RefRW<MarketPrice>, DynamicBuffer<MarketEvent>, DynamicBuffer<PriceHistoryEntry>>()
                    .WithEntityAccess())
            {
                var historyBuffer = history;

                // Check update interval
                if (currentTick - market.ValueRO.LastUpdateTick < (uint)config.PriceUpdateInterval)
                    continue;

                // Check for active market events
                if (MarketHelpers.TryGetActiveEvent(events, market.ValueRO.ResourceTypeId, currentTick, out var eventType, out var magnitude))
                {
                    // Calculate price with event
                    float newPrice = MarketHelpers.CalculatePriceWithEvents(
                        market.ValueRO.BasePrice,
                        market.ValueRO.Supply,
                        market.ValueRO.Demand,
                        (float)market.ValueRO.Elasticity,
                        eventType,
                        magnitude);
                    
                    market.ValueRW.CurrentPrice = Unity.Mathematics.math.clamp(
                        newPrice,
                        market.ValueRO.BasePrice * config.MinPriceMultiplier,
                        market.ValueRO.BasePrice * config.MaxPriceMultiplier);
                    market.ValueRW.LastUpdateTick = currentTick;
                }
                else
                {
                    // Normal price update
                    MarketHelpers.UpdateMarketPrice(ref market.ValueRW, currentTick, config);
                }

                // Record history
                MarketHelpers.RecordPriceHistory(ref historyBuffer, market.ValueRO, currentTick, config);
            }

            // Handle markets without event buffers
            foreach (var (market, history, entity) in 
                SystemAPI.Query<RefRW<MarketPrice>, DynamicBuffer<PriceHistoryEntry>>()
                    .WithNone<MarketEvent>()
                    .WithEntityAccess())
            {
                var historyBuffer = history;

                if (currentTick - market.ValueRO.LastUpdateTick < (uint)config.PriceUpdateInterval)
                    continue;

                MarketHelpers.UpdateMarketPrice(ref market.ValueRW, currentTick, config);
                MarketHelpers.RecordPriceHistory(ref historyBuffer, market.ValueRO, currentTick, config);
            }
        }
    }

    /// <summary>
    /// System that processes trade requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MarketPriceUpdateSystem))]
    [BurstCompile]
    public partial struct TradeExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process trade requests
            foreach (var (request, entity) in 
                SystemAPI.Query<RefRO<TradeRequest>>()
                    .WithEntityAccess())
            {
                var req = request.ValueRO;
                
                // Find market
                if (!SystemAPI.HasComponent<MarketPrice>(req.MarketEntity))
                {
                    ecb.RemoveComponent<TradeRequest>(entity);
                    continue;
                }

                var market = SystemAPI.GetComponent<MarketPrice>(req.MarketEntity);
                
                // Check if resource matches
                if (market.ResourceTypeId != req.ResourceTypeId)
                {
                    ecb.RemoveComponent<TradeRequest>(entity);
                    continue;
                }

                bool tradeExecuted = false;

                if (req.Type == TradeOfferType.Buy)
                {
                    // Check if price is acceptable
                    if (market.CurrentPrice <= req.MaxPrice && market.Supply >= req.Quantity)
                    {
                        // Execute buy
                        market.Supply -= req.Quantity;
                        market.Demand += req.Quantity * 0.1f;
                        tradeExecuted = true;
                    }
                }
                else // Sell
                {
                    // Check if price is acceptable
                    if (market.CurrentPrice >= req.MinPrice)
                    {
                        // Execute sell
                        market.Supply += req.Quantity;
                        market.Demand -= req.Quantity * 0.1f;
                        tradeExecuted = true;
                    }
                }

                if (tradeExecuted)
                {
                    market.Supply = Unity.Mathematics.math.max(0, market.Supply);
                    market.Demand = Unity.Mathematics.math.max(0.1f, market.Demand);
                    SystemAPI.SetComponent(req.MarketEntity, market);

                    // Record transaction
                    if (SystemAPI.HasBuffer<TradeTransaction>(req.MarketEntity))
                    {
                        var transactions = SystemAPI.GetBuffer<TradeTransaction>(req.MarketEntity);
                        transactions.Add(new TradeTransaction
                        {
                            ResourceTypeId = req.ResourceTypeId,
                            Quantity = req.Quantity,
                            TotalPrice = market.CurrentPrice * req.Quantity,
                            BuyerEntity = req.Type == TradeOfferType.Buy ? entity : Entity.Null,
                            SellerEntity = req.Type == TradeOfferType.Sell ? entity : Entity.Null,
                            Tick = currentTick
                        });
                    }
                }

                ecb.RemoveComponent<TradeRequest>(entity);
            }
        }
    }

    /// <summary>
    /// System that expires old market events.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MarketPriceUpdateSystem))]
    [BurstCompile]
    public partial struct MarketEventExpirySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Remove expired events
            foreach (var (events, entity) in 
                SystemAPI.Query<DynamicBuffer<MarketEvent>>()
                    .WithEntityAccess())
            {
                for (int i = events.Length - 1; i >= 0; i--)
                {
                    var evt = events[i];
                    if (currentTick >= evt.StartTick + evt.DurationTicks)
                    {
                        events.RemoveAt(i);
                    }
                }
            }
        }
    }
}

