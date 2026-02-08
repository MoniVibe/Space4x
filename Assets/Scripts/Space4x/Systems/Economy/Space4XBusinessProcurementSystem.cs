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
    /// Pulls inputs for businesses from colony stockpiles and markets so jobs can keep running.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XBusinessJobSystem))]
    public partial struct Space4XBusinessProcurementSystem : ISystem
    {
        private const uint ProcurementTickInterval = 10u;
        private const float MaxProcurePerTick = 6f;
        private const float DesiredCycles = 2f;

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
            state.RequireForUpdate<Space4XJobCatalogSingleton>();
            state.RequireForUpdate<Space4XBusinessCatalogSingleton>();

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
            if (ProcurementTickInterval == 0u || (tick % ProcurementTickInterval) != 0u)
            {
                return;
            }

            var jobCatalog = SystemAPI.GetSingleton<Space4XJobCatalogSingleton>().Catalog;
            var businessCatalog = SystemAPI.GetSingleton<Space4XBusinessCatalogSingleton>().Catalog;
            if (!jobCatalog.IsCreated || !businessCatalog.IsCreated)
            {
                return;
            }

            _stockLookup.Update(ref state);
            _marketLookup.Update(ref state);
            _priceLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var needs = new NativeArray<float>((int)ResourceType.Count, Allocator.Temp);

            foreach (var (businessState, storage) in SystemAPI.Query<RefRW<Space4XBusinessState>, DynamicBuffer<ResourceStorage>>())
            {
                var storageBuffer = storage;
                var colony = businessState.ValueRO.Colony;
                if (!IsValidEntity(colony))
                {
                    continue;
                }

                if (!TryResolveBusinessDefinition(businessState.ValueRO.Kind, ref businessCatalog.Value, out var businessIndex))
                {
                    continue;
                }

                ref var businessDef = ref businessCatalog.Value.Businesses[businessIndex];
                for (int i = 0; i < needs.Length; i++)
                {
                    needs[i] = 0f;
                }

                for (int j = 0; j < businessDef.JobIds.Length; j++)
                {
                    var jobId = businessDef.JobIds[j];
                    if (!TryResolveJob(jobId, ref jobCatalog.Value, out var jobIndex))
                    {
                        continue;
                    }

                    ref var jobDef = ref jobCatalog.Value.Jobs[jobIndex];
                    for (int k = 0; k < jobDef.Inputs.Length; k++)
                    {
                        var input = jobDef.Inputs[k];
                        var index = (int)input.Type;
                        if (index < 0 || index >= needs.Length)
                        {
                            continue;
                        }

                        needs[index] = math.max(needs[index], math.max(0f, input.Units));
                    }
                }

                var hasStock = _stockLookup.HasComponent(colony);
                var stock = hasStock ? _stockLookup[colony] : default;
                var hasMarket = _marketLookup.HasComponent(colony) && _priceLookup.HasBuffer(colony);
                DynamicBuffer<MarketPriceEntry> prices = default;
                if (hasMarket)
                {
                    prices = _priceLookup[colony];
                }

                var credits = math.max(0f, businessState.ValueRO.Credits);
                var updatedCredits = false;
                var updatedStock = false;

                for (int i = 0; i < needs.Length; i++)
                {
                    var perCycle = needs[i];
                    if (perCycle <= 0f)
                    {
                        continue;
                    }

                    var type = (ResourceType)i;
                    var desired = perCycle * DesiredCycles;
                    var current = GetStorageAmount(storageBuffer, type);
                    if (current >= desired - 1e-4f)
                    {
                        continue;
                    }

                    var missing = math.min(desired - current, MaxProcurePerTick);
                    if (missing <= 0f)
                    {
                        continue;
                    }

                    var remaining = missing;

                    if (hasStock)
                    {
                        var pulled = PullFromStock(ref stock, type, remaining);
                        if (pulled > 0f)
                        {
                            AddToStorage(storageBuffer, type, pulled);
                            remaining -= pulled;
                            updatedStock = true;
                        }
                    }

                    if (remaining > 0f && hasMarket && TryMapToMarket(type, out var marketType))
                    {
                        var purchased = TryPurchaseFromMarket(ref prices, marketType, remaining, ref credits);
                        if (purchased > 0f)
                        {
                            AddToStorage(storageBuffer, type, purchased);
                            remaining -= purchased;
                            updatedCredits = true;
                        }
                    }
                }

                if (updatedStock)
                {
                    stock.LastUpdateTick = tick;
                    _stockLookup[colony] = stock;
                }

                if (updatedCredits)
                {
                    businessState.ValueRW.Credits = credits;
                }
            }

            needs.Dispose();
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityLookup.Exists(entity);
        }

        private static bool TryResolveBusinessDefinition(Space4XBusinessKind kind, ref Space4XBusinessCatalogBlob catalog, out int index)
        {
            for (int i = 0; i < catalog.Businesses.Length; i++)
            {
                ref var business = ref catalog.Businesses[i];
                if (business.Kind == kind)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static bool TryResolveJob(in FixedString64Bytes jobId, ref Space4XJobCatalogBlob catalog, out int index)
        {
            for (int i = 0; i < catalog.Jobs.Length; i++)
            {
                ref var job = ref catalog.Jobs[i];
                if (job.Id.Equals(jobId))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static float GetStorageAmount(DynamicBuffer<ResourceStorage> storage, ResourceType type)
        {
            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type == type)
                {
                    return storage[i].Amount;
                }
            }

            return 0f;
        }

        private static void AddToStorage(DynamicBuffer<ResourceStorage> storage, ResourceType type, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type != type)
                {
                    continue;
                }

                var entry = storage[i];
                entry.AddAmount(amount);
                storage[i] = entry;
                return;
            }

            var slot = ResourceStorage.Create(type, 5000f);
            slot.AddAmount(amount);
            storage.Add(slot);
        }

        private static float PullFromStock(ref ColonyIndustryStock stock, ResourceType type, float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            switch (type)
            {
                case ResourceType.Ore:
                {
                    var pulled = math.min(stock.OreReserve, amount);
                    stock.OreReserve = math.max(0f, stock.OreReserve - pulled);
                    return pulled;
                }
                case ResourceType.Supplies:
                {
                    var pulled = math.min(stock.SuppliesReserve, amount);
                    stock.SuppliesReserve = math.max(0f, stock.SuppliesReserve - pulled);
                    return pulled;
                }
                case ResourceType.Food:
                {
                    var pulled = math.min(stock.FoodReserve, amount);
                    stock.FoodReserve = math.max(0f, stock.FoodReserve - pulled);
                    return pulled;
                }
                case ResourceType.Water:
                {
                    var pulled = math.min(stock.WaterReserve, amount);
                    stock.WaterReserve = math.max(0f, stock.WaterReserve - pulled);
                    return pulled;
                }
                case ResourceType.Fuel:
                {
                    var pulled = math.min(stock.FuelReserve, amount);
                    stock.FuelReserve = math.max(0f, stock.FuelReserve - pulled);
                    return pulled;
                }
                default:
                    return 0f;
            }
        }

        private static float TryPurchaseFromMarket(
            ref DynamicBuffer<MarketPriceEntry> prices,
            MarketResourceType resource,
            float amount,
            ref float credits)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            for (int i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                if (price.ResourceType != resource)
                {
                    continue;
                }

                var purchasable = math.min(price.Supply, amount);
                if (purchasable <= 0f)
                {
                    return 0f;
                }

                var unitPrice = math.max(0f, price.BuyPrice);
                if (unitPrice > 0f && credits >= 0f)
                {
                    var affordable = credits / unitPrice;
                    purchasable = math.min(purchasable, affordable);
                }

                if (purchasable <= 0f)
                {
                    return 0f;
                }

                price.Supply = math.max(0f, price.Supply - purchasable);
                prices[i] = price;

                if (unitPrice > 0f)
                {
                    credits = math.max(0f, credits - purchasable * unitPrice);
                }

                return purchasable;
            }

            return 0f;
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
                case ResourceType.StrontiumClathrates:
                    market = MarketResourceType.Industrial;
                    return true;
                case ResourceType.ExoticGases:
                case ResourceType.RelicData:
                    market = MarketResourceType.Tech;
                    return true;
                case ResourceType.BoosterGas:
                    market = MarketResourceType.Luxury;
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
