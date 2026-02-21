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
    /// Minimal colony-level logistics: pools key outputs and redistributes inputs between facilities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Runtime.Economy.Production.ProductionJobCompletionSystem))]
    [UpdateBefore(typeof(Space4XFacilityAutoProductionSystem))]
    public partial struct Space4XColonyIndustryTransferSystem : ISystem
    {
        private static readonly FixedString64Bytes ItemIngot = "space4x_ingot";
        private static readonly FixedString64Bytes ItemAlloy = "space4x_alloy";
        private static readonly FixedString64Bytes ItemParts = "space4x_parts";
        private static readonly FixedString64Bytes ItemFood = "space4x_food";
        private static readonly FixedString64Bytes ItemWater = "space4x_water";
        private static readonly FixedString64Bytes ItemFuel = "space4x_fuel";
        private static readonly FixedString64Bytes ItemTradeGoods = "space4x_trade_goods";

        private ComponentLookup<ColonyIndustryStock> _stockLookup;
        private ComponentLookup<ColonyIndustryInventory> _colonyInventoryLookup;
        private ComponentLookup<BusinessInventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemsLookup;
        private EntityStorageInfoLookup _entityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();

            _stockLookup = state.GetComponentLookup<ColonyIndustryStock>(false);
            _colonyInventoryLookup = state.GetComponentLookup<ColonyIndustryInventory>(true);
            _inventoryLookup = state.GetComponentLookup<BusinessInventory>(true);
            _itemsLookup = state.GetBufferLookup<InventoryItem>(false);
            _entityLookup = state.GetEntityStorageInfoLookup();
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

            _stockLookup.Update(ref state);
            _colonyInventoryLookup.Update(ref state);
            _inventoryLookup.Update(ref state);
            _itemsLookup.Update(ref state);
            _entityLookup.Update(ref state);

            foreach (var (colonyInventory, colonyEntity) in SystemAPI.Query<RefRO<ColonyIndustryInventory>>().WithEntityAccess())
            {
                if (!IsValidEntity(colonyEntity))
                {
                    continue;
                }

                var poolEntity = colonyInventory.ValueRO.InventoryEntity;
                if (!IsValidEntity(poolEntity) || !_itemsLookup.HasBuffer(poolEntity))
                {
                    continue;
                }

                var poolItems = _itemsLookup[poolEntity];
                var hasStock = _stockLookup.HasComponent(colonyEntity);
                var stock = hasStock ? _stockLookup[colonyEntity] : default;

                // Export pass: pull outputs into the colony pool.
                foreach (var (link, facility) in SystemAPI.Query<RefRO<ColonyFacilityLink>>().WithEntityAccess())
                {
                    if (link.ValueRO.Colony != colonyEntity)
                    {
                        continue;
                    }

                    if (!IsValidEntity(facility) || !_inventoryLookup.HasComponent(facility))
                    {
                        continue;
                    }

                    var facilityInventory = _inventoryLookup[facility].InventoryEntity;
                    if (!IsValidEntity(facilityInventory) || !_itemsLookup.HasBuffer(facilityInventory))
                    {
                        continue;
                    }

                    var facilityItems = _itemsLookup[facilityInventory];

                    switch (link.ValueRO.FacilityClass)
                    {
                        case FacilityBusinessClass.Refinery:
                            TransferAll(ref facilityItems, ref poolItems, ItemIngot);
                            TransferAll(ref facilityItems, ref poolItems, ItemAlloy);
                            break;
                        case FacilityBusinessClass.Production:
                        case FacilityBusinessClass.ShipFabrication:
                            TransferAll(ref facilityItems, ref poolItems, ItemParts);
                            TransferAll(ref facilityItems, ref poolItems, ItemTradeGoods);
                            if (hasStock)
                            {
                                TransferAllToReserve(ref facilityItems, ItemFood, ref stock.FoodReserve);
                                TransferAllToReserve(ref facilityItems, ItemWater, ref stock.WaterReserve);
                                TransferAllToReserve(ref facilityItems, ItemFuel, ref stock.FuelReserve);
                            }
                            break;
                    }
                }

                // Import pass: feed required inputs back to facilities.
                foreach (var (link, facility) in SystemAPI.Query<RefRO<ColonyFacilityLink>>().WithEntityAccess())
                {
                    if (link.ValueRO.Colony != colonyEntity)
                    {
                        continue;
                    }

                    if (!IsValidEntity(facility) || !_inventoryLookup.HasComponent(facility))
                    {
                        continue;
                    }

                    var facilityInventory = _inventoryLookup[facility].InventoryEntity;
                    if (!IsValidEntity(facilityInventory) || !_itemsLookup.HasBuffer(facilityInventory))
                    {
                        continue;
                    }

                    var facilityItems = _itemsLookup[facilityInventory];

                    switch (link.ValueRO.FacilityClass)
                    {
                        case FacilityBusinessClass.Production:
                            EnsureMinimum(ref poolItems, ref facilityItems, ItemIngot, 40f, tickTime.Tick);
                            break;
                        case FacilityBusinessClass.ModuleFacility:
                            EnsureMinimum(ref poolItems, ref facilityItems, ItemParts, 20f, tickTime.Tick);
                            EnsureMinimum(ref poolItems, ref facilityItems, ItemAlloy, 20f, tickTime.Tick);
                            break;
                        case FacilityBusinessClass.Shipyard:
                            EnsureMinimum(ref poolItems, ref facilityItems, ItemParts, 40f, tickTime.Tick);
                            EnsureMinimum(ref poolItems, ref facilityItems, ItemAlloy, 30f, tickTime.Tick);
                            EnsureMinimum(ref poolItems, ref facilityItems, ItemIngot, 20f, tickTime.Tick);
                            break;
                    }
                }

                if (hasStock)
                {
                    _stockLookup[colonyEntity] = stock;
                }
            }
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityLookup.Exists(entity);
        }

        private static void TransferAll(ref DynamicBuffer<InventoryItem> source, ref DynamicBuffer<InventoryItem> destination, in FixedString64Bytes itemId)
        {
            for (int i = source.Length - 1; i >= 0; i--)
            {
                if (!source[i].ItemId.Equals(itemId))
                {
                    continue;
                }

                var item = source[i];
                source.RemoveAt(i);
                AddItem(ref destination, item.ItemId, item.Quantity, item.Quality, item.Durability, item.CreatedTick);
            }
        }

        private static void TransferAllToReserve(ref DynamicBuffer<InventoryItem> source, in FixedString64Bytes itemId, ref float reserve)
        {
            for (int i = source.Length - 1; i >= 0; i--)
            {
                if (!source[i].ItemId.Equals(itemId))
                {
                    continue;
                }

                var item = source[i];
                source.RemoveAt(i);
                reserve += item.Quantity;
            }
        }

        private static void EnsureMinimum(ref DynamicBuffer<InventoryItem> pool, ref DynamicBuffer<InventoryItem> facility, in FixedString64Bytes itemId, float target, uint tick)
        {
            if (target <= 0f)
            {
                return;
            }

            var current = GetItemQuantity(facility, itemId);
            if (current >= target - 1e-4f)
            {
                return;
            }

            var needed = target - current;
            var pulled = TakeItem(ref pool, itemId, needed);
            if (pulled > 0f)
            {
                AddItem(ref facility, itemId, pulled, 1f, 1f, tick);
            }
        }

        private static float GetItemQuantity(DynamicBuffer<InventoryItem> items, in FixedString64Bytes itemId)
        {
            var total = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ItemId.Equals(itemId))
                {
                    total += items[i].Quantity;
                }
            }

            return total;
        }

        private static float TakeItem(ref DynamicBuffer<InventoryItem> items, in FixedString64Bytes itemId, float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            var remaining = amount;
            var taken = 0f;

            for (int i = items.Length - 1; i >= 0 && remaining > 0f; i--)
            {
                if (!items[i].ItemId.Equals(itemId))
                {
                    continue;
                }

                var item = items[i];
                var take = math.min(item.Quantity, remaining);
                item.Quantity -= take;
                remaining -= take;
                taken += take;

                if (item.Quantity <= 0f)
                {
                    items.RemoveAt(i);
                }
                else
                {
                    items[i] = item;
                }
            }

            return taken;
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
