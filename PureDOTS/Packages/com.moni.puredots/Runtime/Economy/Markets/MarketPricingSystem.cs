using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy.Markets
{
    /// <summary>
    /// Updates CurrentPrice from BasePrice × multipliers.
    /// Formula: CurrentPrice = BasePrice × SupplyDemandMultiplier × WealthMultiplier × EventMultiplier
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MarketPricingSystem : ISystem
    {
        private BufferLookup<MarketPrice> _marketPriceLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<BasePriceCatalog>();
            state.RequireForUpdate<MarketPricingConfig>();
            _marketPriceLookup = state.GetBufferLookup<MarketPrice>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<BasePriceCatalog>(out var basePriceCatalog))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<MarketPricingConfig>(out var pricingConfig))
            {
                return;
            }

            ref var config = ref pricingConfig.Config.Value;
            _marketPriceLookup.Update(ref state);

            foreach (var (market, entity) in SystemAPI.Query<RefRO<Market>>().WithEntityAccess())
            {
                if (!_marketPriceLookup.HasBuffer(entity))
                {
                    continue;
                }

                var prices = _marketPriceLookup[entity];

                for (int i = 0; i < prices.Length; i++)
                {
                    var price = prices[i];
                    
                    // Calculate supply/demand multiplier
                    if (price.Demand > 0f)
                    {
                        price.SupplyDemandRatio = price.Supply / price.Demand;
                        price.SupplyDemandMultiplier = math.pow(price.SupplyDemandRatio, -config.SupplyDemandExponent);
                        price.SupplyDemandMultiplier = math.clamp(price.SupplyDemandMultiplier, config.MinMultiplier, config.MaxMultiplier);
                    }
                    else
                    {
                        price.SupplyDemandMultiplier = config.MaxMultiplier;
                    }

                    // Calculate current price
                    price.CurrentPrice = price.BasePrice * price.SupplyDemandMultiplier * price.VillageWealthMultiplier * price.EventMultiplier;

                    prices[i] = price;
                }
            }
        }
    }
}

