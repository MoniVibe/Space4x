using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Production;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Telemetry;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Feeds colony-generated ore/supplies into facility inventories so production can run.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ProductionJobSchedulingSystem))]
    public partial struct Space4XColonyIndustryFeedSystem : ISystem
    {
        private static readonly FixedString64Bytes ItemOre = "space4x_ore";
        private static readonly FixedString64Bytes ItemSupplies = "space4x_supplies";
        private static readonly FixedString64Bytes MetricFoodShortage = "space4x.colony.shortfall.food";
        private static readonly FixedString64Bytes MetricWaterShortage = "space4x.colony.shortfall.water";
        private static readonly FixedString64Bytes MetricFuelShortage = "space4x.colony.shortfall.fuel";
        private static readonly FixedString64Bytes MetricSuppliesShortage = "space4x.colony.shortfall.supplies";
        private static readonly FixedString64Bytes MetricEssentialsShortage = "space4x.colony.shortfall.essentials";
        private const float OrePerPopPerSecond = 0.0005f;
        private const float SuppliesPerPopPerSecond = 0.00025f;
        private const float ResearchPerPopPerSecond = 0.00005f;
        private const float FoodPerPopPerSecond = 0.0003f;
        private const float WaterPerPopPerSecond = 0.0003f;
        private const float FuelPerPopPerSecond = 0.00015f;
        private const float SuppliesConsumptionPerPopPerSecond = 0.0001f;
        private const float FacilityFeedRate = 150f;

        private ComponentLookup<Space4XColony> _colonyLookup;
        private ComponentLookup<ColonyIndustryStock> _stockLookup;
        private ComponentLookup<BusinessInventory> _businessInventoryLookup;
        private BufferLookup<InventoryItem> _itemBufferLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();

            _colonyLookup = state.GetComponentLookup<Space4XColony>(false);
            _stockLookup = state.GetComponentLookup<ColonyIndustryStock>(false);
            _businessInventoryLookup = state.GetComponentLookup<BusinessInventory>(true);
            _itemBufferLookup = state.GetBufferLookup<InventoryItem>(false);
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
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

            var deltaTime = math.max(0f, tickTime.FixedDeltaTime * math.max(0.01f, tickTime.CurrentSpeedMultiplier));

            _colonyLookup.Update(ref state);
            _stockLookup.Update(ref state);
            _businessInventoryLookup.Update(ref state);
            _itemBufferLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            var foodPerPop = FoodPerPopPerSecond;
            var waterPerPop = WaterPerPopPerSecond;
            var fuelPerPop = FuelPerPopPerSecond;
            var suppliesConsumptionPerPop = SuppliesConsumptionPerPopPerSecond;
            if (SystemAPI.TryGetSingleton(out Space4X.SimServer.Space4XSimServerConfig simConfig))
            {
                foodPerPop = math.max(0f, simConfig.FoodPerPopPerSecond);
                waterPerPop = math.max(0f, simConfig.WaterPerPopPerSecond);
                fuelPerPop = math.max(0f, simConfig.FuelPerPopPerSecond);
                suppliesConsumptionPerPop = math.max(0f, simConfig.SuppliesConsumptionPerPopPerSecond);
            }

            float foodShortageTotal = 0f;
            float waterShortageTotal = 0f;
            float fuelShortageTotal = 0f;
            float suppliesShortageTotal = 0f;

            foreach (var (colony, entity) in SystemAPI.Query<RefRW<Space4XColony>>().WithEntityAccess())
            {
                if (!_stockLookup.HasComponent(entity))
                {
                    continue;
                }

                var stock = _stockLookup[entity];
                var population = math.max(0f, colony.ValueRO.Population);

                stock.OreReserve += population * OrePerPopPerSecond * deltaTime;
                stock.SuppliesReserve += population * SuppliesPerPopPerSecond * deltaTime;
                stock.ResearchReserve += population * ResearchPerPopPerSecond * deltaTime;
                stock.FoodReserve = math.max(0f, stock.FoodReserve - population * foodPerPop * deltaTime);
                stock.WaterReserve = math.max(0f, stock.WaterReserve - population * waterPerPop * deltaTime);
                stock.FuelReserve = math.max(0f, stock.FuelReserve - population * fuelPerPop * deltaTime);
                stock.SuppliesReserve = math.max(0f, stock.SuppliesReserve - population * suppliesConsumptionPerPop * deltaTime);
                stock.LastUpdateTick = tickTime.Tick;

                _stockLookup[entity] = stock;

                colony.ValueRW.StoredResources = math.max(0f,
                    stock.OreReserve
                    + stock.SuppliesReserve
                    + stock.ResearchReserve
                    + stock.FoodReserve
                    + stock.WaterReserve
                    + stock.FuelReserve);

                var desired = math.max(200f, population * 0.002f);
                var perEssential = desired * 0.25f;
                foodShortageTotal += math.max(0f, perEssential - stock.FoodReserve);
                waterShortageTotal += math.max(0f, perEssential - stock.WaterReserve);
                fuelShortageTotal += math.max(0f, perEssential - stock.FuelReserve);
                suppliesShortageTotal += math.max(0f, perEssential - stock.SuppliesReserve);
            }

            if ((foodShortageTotal > 0f || waterShortageTotal > 0f || fuelShortageTotal > 0f || suppliesShortageTotal > 0f) &&
                SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var exportConfig) &&
                exportConfig.Enabled != 0 &&
                (exportConfig.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) != 0 &&
                SystemAPI.TryGetSingletonBuffer<TelemetryMetric>(out var telemetry))
            {
                var cadence = exportConfig.CadenceTicks > 0 ? exportConfig.CadenceTicks : 30u;
                if (tickTime.Tick % cadence == 0)
                {
                    telemetry.AddMetric(MetricFoodShortage, foodShortageTotal, TelemetryMetricUnit.Custom);
                    telemetry.AddMetric(MetricWaterShortage, waterShortageTotal, TelemetryMetricUnit.Custom);
                    telemetry.AddMetric(MetricFuelShortage, fuelShortageTotal, TelemetryMetricUnit.Custom);
                    telemetry.AddMetric(MetricSuppliesShortage, suppliesShortageTotal, TelemetryMetricUnit.Custom);
                    telemetry.AddMetric(MetricEssentialsShortage,
                        foodShortageTotal + waterShortageTotal + fuelShortageTotal + suppliesShortageTotal,
                        TelemetryMetricUnit.Custom);
                }
            }

            foreach (var (link, facility) in SystemAPI.Query<RefRO<ColonyFacilityLink>>().WithEntityAccess())
            {
                var colonyEntity = link.ValueRO.Colony;
                if (!IsValidEntity(colonyEntity) || !_stockLookup.HasComponent(colonyEntity))
                {
                    continue;
                }

                if (!_businessInventoryLookup.HasComponent(facility))
                {
                    continue;
                }

                var inventoryEntity = _businessInventoryLookup[facility].InventoryEntity;
                if (!IsValidEntity(inventoryEntity) || !_itemBufferLookup.HasBuffer(inventoryEntity))
                {
                    continue;
                }

                var stock = _stockLookup[colonyEntity];
                var items = _itemBufferLookup[inventoryEntity];

                var feedAmount = FacilityFeedRate * deltaTime;
                if (feedAmount <= 0f)
                {
                    continue;
                }

                switch (link.ValueRO.FacilityClass)
                {
                    case FacilityBusinessClass.Refinery:
                        Feed(ref stock.OreReserve, feedAmount, ItemOre, ref items, tickTime.Tick);
                        break;
                    case FacilityBusinessClass.Production:
                    case FacilityBusinessClass.ModuleFacility:
                    case FacilityBusinessClass.ShipFabrication:
                    case FacilityBusinessClass.Shipyard:
                    case FacilityBusinessClass.Construction:
                        Feed(ref stock.SuppliesReserve, feedAmount, ItemSupplies, ref items, tickTime.Tick);
                        break;
                    case FacilityBusinessClass.Research:
                        Feed(ref stock.SuppliesReserve, feedAmount, ItemSupplies, ref items, tickTime.Tick);
                        break;
                }

                _stockLookup[colonyEntity] = stock;
            }
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityInfoLookup.Exists(entity);
        }

        private static void Feed(ref float reserve, float amount, in FixedString64Bytes itemId, ref DynamicBuffer<InventoryItem> items, uint tick)
        {
            if (reserve <= 0.0001f)
            {
                return;
            }

            var transfer = math.min(reserve, amount);
            reserve = math.max(0f, reserve - transfer);
            AddItem(ref items, itemId, transfer, 1f, 1f, tick);
        }

        private static void AddItem(ref DynamicBuffer<InventoryItem> items, in FixedString64Bytes itemId, float quantity, float quality, float durability, uint tick)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i].ItemId.Equals(itemId))
                {
                    continue;
                }

                var item = items[i];
                item.Quantity += quantity;
                item.Quality = math.max(item.Quality, quality);
                item.Durability = math.max(item.Durability, durability);
                items[i] = item;
                return;
            }

            items.Add(new InventoryItem
            {
                ItemId = itemId,
                Quantity = quantity,
                Quality = quality,
                Durability = durability,
                CreatedTick = tick
            });
        }
    }
}
