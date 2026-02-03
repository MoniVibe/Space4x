using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Production;
using PureDOTS.Runtime.Economy.Resources;
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
        private const float OrePerPopPerSecond = 0.0005f;
        private const float SuppliesPerPopPerSecond = 0.00025f;
        private const float ResearchPerPopPerSecond = 0.00005f;
        private const float FacilityFeedRate = 150f;

        private ComponentLookup<Space4XColony> _colonyLookup;
        private ComponentLookup<ColonyIndustryStock> _stockLookup;
        private ComponentLookup<BusinessInventory> _businessInventoryLookup;
        private BufferLookup<InventoryItem> _itemBufferLookup;

        private FixedString64Bytes _oreId;
        private FixedString64Bytes _suppliesId;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();

            _colonyLookup = state.GetComponentLookup<Space4XColony>(true);
            _stockLookup = state.GetComponentLookup<ColonyIndustryStock>(false);
            _businessInventoryLookup = state.GetComponentLookup<BusinessInventory>(true);
            _itemBufferLookup = state.GetBufferLookup<InventoryItem>(false);

            _oreId = new FixedString64Bytes("space4x_ore");
            _suppliesId = new FixedString64Bytes("space4x_supplies");
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

            foreach (var (colony, entity) in SystemAPI.Query<RefRO<Space4XColony>>().WithEntityAccess())
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
                stock.LastUpdateTick = tickTime.Tick;

                _stockLookup[entity] = stock;
            }

            foreach (var (link, facility) in SystemAPI.Query<RefRO<ColonyFacilityLink>>().WithEntityAccess())
            {
                var colonyEntity = link.ValueRO.Colony;
                if (colonyEntity == Entity.Null || !_stockLookup.HasComponent(colonyEntity))
                {
                    continue;
                }

                if (!_businessInventoryLookup.HasComponent(facility))
                {
                    continue;
                }

                var inventoryEntity = _businessInventoryLookup[facility].InventoryEntity;
                if (inventoryEntity == Entity.Null || !_itemBufferLookup.HasBuffer(inventoryEntity))
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
                        Feed(ref stock.OreReserve, feedAmount, _oreId, ref items, tickTime.Tick);
                        break;
                    case FacilityBusinessClass.Production:
                    case FacilityBusinessClass.ModuleFacility:
                    case FacilityBusinessClass.ShipFabrication:
                    case FacilityBusinessClass.Shipyard:
                    case FacilityBusinessClass.Construction:
                        Feed(ref stock.SuppliesReserve, feedAmount, _suppliesId, ref items, tickTime.Tick);
                        break;
                    case FacilityBusinessClass.Research:
                        Feed(ref stock.SuppliesReserve, feedAmount, _suppliesId, ref items, tickTime.Tick);
                        break;
                }

                _stockLookup[colonyEntity] = stock;
            }
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
