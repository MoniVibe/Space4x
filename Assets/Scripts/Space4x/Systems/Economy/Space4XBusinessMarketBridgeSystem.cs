using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Bridges business output into colony stockpiles and markets.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XBusinessJobSystem))]
    public partial struct Space4XBusinessMarketBridgeSystem : ISystem
    {
        private const uint TransferTickInterval = 10u;
        private const float MaxTransferPerTick = 4f;

        private ComponentLookup<ColonyIndustryStock> _stockLookup;
        private ComponentLookup<Space4XMarket> _marketLookup;
        private BufferLookup<MarketPriceEntry> _priceLookup;
        private EntityStorageInfoLookup _entityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();

            _stockLookup = state.GetComponentLookup<ColonyIndustryStock>(false);
            _marketLookup = state.GetComponentLookup<Space4XMarket>(true);
            _priceLookup = state.GetBufferLookup<MarketPriceEntry>(false);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            var tickTime = SystemAPI.GetSingleton<TickTimeState>();
            if (tickTime.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = tickTime.Tick;
            if (TransferTickInterval == 0u || tick % TransferTickInterval != 0u)
            {
                return;
            }

            _stockLookup.Update(ref state);
            _marketLookup.Update(ref state);
            _priceLookup.Update(ref state);
            _entityLookup.Update(ref state);

            foreach (var (business, storage) in SystemAPI.Query<RefRW<Space4XBusinessState>, DynamicBuffer<ResourceStorage>>())
            {
                var storageBuffer = storage;
                var colony = business.ValueRO.Colony;
                if (!IsValidEntity(colony))
                {
                    continue;
                }

                var hasStock = _stockLookup.HasComponent(colony);
                var hasMarket = _marketLookup.HasComponent(colony) && _priceLookup.HasBuffer(colony);
                if (!hasStock && !hasMarket)
                {
                    continue;
                }

                var stock = hasStock ? _stockLookup[colony] : default;
                DynamicBuffer<MarketPriceEntry> prices = default;
                if (hasMarket)
                {
                    prices = _priceLookup[colony];
                }

                for (int i = 0; i < storageBuffer.Length; i++)
                {
                    var entry = storageBuffer[i];
                    var reserve = ResolveReserve(entry.Type);
                    var transferable = entry.Amount - reserve;
                    if (transferable <= 0.0001f)
                    {
                        continue;
                    }

                    var transfer = math.min(transferable, MaxTransferPerTick);
                    if (transfer <= 0f)
                    {
                        continue;
                    }

                    var consumed = false;
                    if (hasStock)
                    {
                        consumed = TryDepositStock(ref stock, entry.Type, transfer);
                    }

                    if (!consumed && hasMarket && TryMapToMarket(entry.Type, out var marketType))
                    {
                        if (TryDepositMarket(ref prices, marketType, transfer))
                        {
                            consumed = true;
                        }
                    }

                    if (consumed)
                    {
                        entry.Amount = math.max(0f, entry.Amount - transfer);
                        storageBuffer[i] = entry;
                    }
                }

                if (hasStock)
                {
                    _stockLookup[colony] = stock;
                }
            }
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityLookup.Exists(entity);
        }

        private static float ResolveReserve(ResourceType type)
        {
            return type switch
            {
                ResourceType.Food => 4f,
                ResourceType.Water => 4f,
                ResourceType.Supplies => 4f,
                ResourceType.Fuel => 4f,
                _ => 0f
            };
        }

        private static bool TryDepositStock(ref ColonyIndustryStock stock, ResourceType type, float amount)
        {
            switch (type)
            {
                case ResourceType.Ore:
                    stock.OreReserve += amount;
                    return true;
                case ResourceType.Supplies:
                    stock.SuppliesReserve += amount;
                    return true;
                case ResourceType.Food:
                    stock.FoodReserve += amount;
                    return true;
                case ResourceType.Water:
                    stock.WaterReserve += amount;
                    return true;
                case ResourceType.Fuel:
                    stock.FuelReserve += amount;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryDepositMarket(ref DynamicBuffer<MarketPriceEntry> prices, MarketResourceType resource, float amount)
        {
            for (int i = 0; i < prices.Length; i++)
            {
                if (prices[i].ResourceType != resource)
                {
                    continue;
                }

                var entry = prices[i];
                entry.Supply = math.max(0f, entry.Supply + amount);
                prices[i] = entry;
                return true;
            }

            return false;
        }

        private static bool TryMapToMarket(ResourceType type, out MarketResourceType market)
        {
            switch (type)
            {
                case ResourceType.Ore:
                    market = MarketResourceType.Ore;
                    return true;
                case ResourceType.Minerals:
                    market = MarketResourceType.RefinedMetal;
                    return true;
                case ResourceType.RareMetals:
                case ResourceType.TransplutonicOre:
                    market = MarketResourceType.RareEarth;
                    return true;
                case ResourceType.EnergyCrystals:
                case ResourceType.Fuel:
                case ResourceType.Isotopes:
                    market = MarketResourceType.Energy;
                    return true;
                case ResourceType.Food:
                    market = MarketResourceType.Food;
                    return true;
                case ResourceType.Water:
                case ResourceType.HeavyWater:
                    market = MarketResourceType.Water;
                    return true;
                case ResourceType.Supplies:
                case ResourceType.VolatileMotes:
                case ResourceType.IndustrialCrystals:
                case ResourceType.Volatiles:
                case ResourceType.LiquidOzone:
                case ResourceType.SalvageComponents:
                    market = MarketResourceType.Industrial;
                    return true;
                case ResourceType.ExoticGases:
                case ResourceType.RelicData:
                    market = MarketResourceType.Tech;
                    return true;
                case ResourceType.BoosterGas:
                    market = MarketResourceType.Luxury;
                    return true;
                case ResourceType.StrontiumClathrates:
                    market = MarketResourceType.Military;
                    return true;
                case ResourceType.OrganicMatter:
                    market = MarketResourceType.Consumer;
                    return true;
                default:
                    market = MarketResourceType.Consumer;
                    return false;
            }
        }
    }
}
