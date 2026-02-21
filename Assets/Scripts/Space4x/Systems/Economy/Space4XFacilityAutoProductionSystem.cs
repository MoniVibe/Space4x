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
    /// Simple auto-scheduler that requests production jobs based on facility role and stock levels.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XColonyIndustryFeedSystem))]
    [UpdateBefore(typeof(ProductionJobSchedulingSystem))]
    public partial struct Space4XFacilityAutoProductionSystem : ISystem
    {
        private static readonly FixedString64Bytes RecipeOreToIngot = "space4x_ore_to_ingot";
        private static readonly FixedString64Bytes RecipeOreToIngotMk2 = "space4x_ore_to_ingot_mk2";
        private static readonly FixedString64Bytes RecipeOreToAlloy = "space4x_ore_to_alloy";
        private static readonly FixedString64Bytes RecipeOreToAlloyMk2 = "space4x_ore_to_alloy_mk2";
        private static readonly FixedString64Bytes RecipePartsAssembly = "space4x_parts_assembly";
        private static readonly FixedString64Bytes RecipePartsAssemblyMk2 = "space4x_parts_assembly_mk2";
        private static readonly FixedString64Bytes RecipeShipHull = "space4x_ship_lcv_sparrow";
        private static readonly FixedString64Bytes RecipeShipHullMule = "space4x_ship_cv_mule";
        private static readonly FixedString64Bytes RecipeResearchPacket = "space4x_research_packet";
        private static readonly FixedString64Bytes RecipeSuppliesToFood = "space4x_supplies_to_food";
        private static readonly FixedString64Bytes RecipeSuppliesToWater = "space4x_supplies_to_water";
        private static readonly FixedString64Bytes RecipeSuppliesToFuel = "space4x_supplies_to_fuel";
        private static readonly FixedString64Bytes RecipeTradeGoodsAssembly = "space4x_trade_goods_assembly";
        private static readonly FixedString64Bytes RecipeEngineMk1 = "space4x_module_engine_mk1";
        private static readonly FixedString64Bytes RecipeEngineMk2 = "space4x_module_engine_mk2";
        private static readonly FixedString64Bytes RecipeShieldS1 = "space4x_module_shield_s_1";
        private static readonly FixedString64Bytes RecipeShieldM1 = "space4x_module_shield_m_1";
        private static readonly FixedString64Bytes RecipeLaserS1 = "space4x_module_laser_s_1";
        private static readonly FixedString64Bytes RecipeMissileM1 = "space4x_module_missile_m_1";
        private static readonly FixedString64Bytes RecipeBridgeMk1 = "space4x_module_bridge_mk1";
        private static readonly FixedString64Bytes RecipeCockpitMk1 = "space4x_module_cockpit_mk1";
        private static readonly FixedString64Bytes RecipeArmorS1 = "space4x_module_armor_s_1";
        private static readonly FixedString64Bytes RecipeAmmoS1 = "space4x_module_ammo_bay_s_1";
        private static readonly FixedString64Bytes RecipeReactorMk2 = "space4x_module_reactor_mk2";

        private static readonly FixedString64Bytes ItemOre = "space4x_ore";
        private static readonly FixedString64Bytes ItemIngot = "space4x_ingot";
        private static readonly FixedString64Bytes ItemAlloy = "space4x_alloy";
        private static readonly FixedString64Bytes ItemParts = "space4x_parts";
        private static readonly FixedString64Bytes ItemSupplies = "space4x_supplies";
        private static readonly FixedString64Bytes ItemResearch = "space4x_research";
        private static readonly FixedString64Bytes ItemFood = "space4x_food";
        private static readonly FixedString64Bytes ItemWater = "space4x_water";
        private static readonly FixedString64Bytes ItemFuel = "space4x_fuel";
        private static readonly FixedString64Bytes ItemTradeGoods = "space4x_trade_goods";
        private static readonly FixedString64Bytes ItemEngineMk1 = "engine-mk1";
        private static readonly FixedString64Bytes ItemEngineMk2 = "engine-mk2";
        private static readonly FixedString64Bytes ItemShieldS1 = "shield-s-1";
        private static readonly FixedString64Bytes ItemShieldM1 = "shield-m-1";
        private static readonly FixedString64Bytes ItemLaserS1 = "laser-s-1";
        private static readonly FixedString64Bytes ItemMissileM1 = "missile-m-1";
        private static readonly FixedString64Bytes ItemBridgeMk1 = "bridge-mk1";
        private static readonly FixedString64Bytes ItemCockpitMk1 = "cockpit-mk1";
        private static readonly FixedString64Bytes ItemArmorS1 = "armor-s-1";
        private static readonly FixedString64Bytes ItemAmmoS1 = "ammo-bay-s-1";
        private static readonly FixedString64Bytes ItemHullLcvSparrow = "lcv-sparrow";
        private static readonly FixedString64Bytes ItemHullCvMule = "cv-mule";
        private static readonly FixedString64Bytes ItemReactorMk2 = "reactor-mk2";

        private ComponentLookup<BusinessInventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemsLookup;
        private BufferLookup<ProductionJob> _jobsLookup;
        private ComponentLookup<ColonyFacilityLink> _facilityLinkLookup;
        private ComponentLookup<Space4XResearchUnlocks> _unlockLookup;
        private ComponentLookup<ProductionQueueCapacity> _queueCapacityLookup;
        private EntityStorageInfoLookup _entityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ProductionRecipeCatalog>();

            _inventoryLookup = state.GetComponentLookup<BusinessInventory>(true);
            _itemsLookup = state.GetBufferLookup<InventoryItem>(false);
            _jobsLookup = state.GetBufferLookup<ProductionJob>(true);
            _facilityLinkLookup = state.GetComponentLookup<ColonyFacilityLink>(true);
            _unlockLookup = state.GetComponentLookup<Space4XResearchUnlocks>(true);
            _queueCapacityLookup = state.GetComponentLookup<ProductionQueueCapacity>(true);
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

            _inventoryLookup.Update(ref state);
            _itemsLookup.Update(ref state);
            _jobsLookup.Update(ref state);
            _facilityLinkLookup.Update(ref state);
            _unlockLookup.Update(ref state);
            _queueCapacityLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (production, facilityClass, entity) in SystemAPI
                         .Query<RefRW<BusinessProduction>, RefRO<FacilityBusinessClassComponent>>()
                         .WithEntityAccess())
            {
                if (!_inventoryLookup.HasComponent(entity))
                {
                    continue;
                }

                var inventoryEntity = _inventoryLookup[entity].InventoryEntity;
                if (!IsValidEntity(inventoryEntity) || !_itemsLookup.HasBuffer(inventoryEntity))
                {
                    continue;
                }

                var maxQueued = 1;
                if (_queueCapacityLookup.HasComponent(entity))
                {
                    maxQueued = math.max(1, _queueCapacityLookup[entity].MaxQueuedJobs);
                }

                if (_jobsLookup.HasBuffer(entity) && _jobsLookup[entity].Length >= maxQueued)
                {
                    continue;
                }

                if (SystemAPI.HasComponent<ProductionJobRequest>(entity))
                {
                    continue;
                }

                EnsureThroughput(ref production.ValueRW);

                var items = _itemsLookup[inventoryEntity];
                var unlocks = default(Space4XResearchUnlocks);
                if (_facilityLinkLookup.HasComponent(entity))
                {
                    var colony = _facilityLinkLookup[entity].Colony;
                    if (IsValidEntity(colony) && _unlockLookup.HasComponent(colony))
                    {
                        unlocks = _unlockLookup[colony];
                    }
                }

                var recipeId = SelectRecipe(facilityClass.ValueRO.Value, items, production.ValueRO.Capacity, unlocks);
                if (recipeId.IsEmpty)
                {
                    continue;
                }

                ecb.AddComponent(entity, new ProductionJobRequest
                {
                    RecipeId = recipeId,
                    Worker = Entity.Null
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityLookup.Exists(entity);
        }

        private FixedString64Bytes SelectRecipe(FacilityBusinessClass role, DynamicBuffer<InventoryItem> items, float capacity, in Space4XResearchUnlocks unlocks)
        {
            switch (role)
            {
                case FacilityBusinessClass.Refinery:
                {
                    var ingotTarget = math.max(20f, capacity * 0.08f);
                    var alloyTarget = math.max(20f, capacity * 0.06f);

                    if (GetItemQuantity(items, ItemIngot) < ingotTarget &&
                        GetItemQuantity(items, ItemOre) >= 100f)
                    {
                        if (unlocks.ProcessingTier >= 2 && GetItemQuantity(items, ItemOre) >= 120f)
                        {
                            return RecipeOreToIngotMk2;
                        }

                        return RecipeOreToIngot;
                    }

                    if (GetItemQuantity(items, ItemAlloy) < alloyTarget &&
                        GetItemQuantity(items, ItemOre) >= 80f &&
                        GetItemQuantity(items, ItemSupplies) >= 10f)
                    {
                        if (unlocks.ProcessingTier >= 2 && GetItemQuantity(items, ItemOre) >= 100f)
                        {
                            return RecipeOreToAlloyMk2;
                        }

                        return RecipeOreToAlloy;
                    }
                    break;
                }
                case FacilityBusinessClass.Production:
                case FacilityBusinessClass.ShipFabrication:
                {
                    var partsTarget = math.max(20f, capacity * 0.05f);
                    if (GetItemQuantity(items, ItemParts) < partsTarget &&
                        GetItemQuantity(items, ItemIngot) >= 10f &&
                        GetItemQuantity(items, ItemSupplies) >= 5f)
                    {
                        if (unlocks.ProductionTier >= 2 && GetItemQuantity(items, ItemIngot) >= 14f)
                        {
                            return RecipePartsAssemblyMk2;
                        }

                        return RecipePartsAssembly;
                    }

                    var essentialsTarget = math.max(12f, capacity * 0.04f);
                    if (GetItemQuantity(items, ItemFood) < essentialsTarget &&
                        GetItemQuantity(items, ItemSupplies) >= 8f)
                    {
                        return RecipeSuppliesToFood;
                    }

                    if (GetItemQuantity(items, ItemWater) < essentialsTarget &&
                        GetItemQuantity(items, ItemSupplies) >= 6f)
                    {
                        return RecipeSuppliesToWater;
                    }

                    if (GetItemQuantity(items, ItemFuel) < essentialsTarget &&
                        GetItemQuantity(items, ItemSupplies) >= 12f)
                    {
                        return RecipeSuppliesToFuel;
                    }

                    var tradeTarget = math.max(6f, capacity * 0.02f);
                    if (GetItemQuantity(items, ItemTradeGoods) < tradeTarget &&
                        GetItemQuantity(items, ItemSupplies) >= 10f &&
                        GetItemQuantity(items, ItemParts) >= 2f)
                    {
                        return RecipeTradeGoodsAssembly;
                    }
                    break;
                }
                case FacilityBusinessClass.ModuleFacility:
                {
                    if (unlocks.ModuleTier >= 2)
                    {
                        if (GetItemQuantity(items, ItemReactorMk2) < 1f &&
                            HasAdvancedPartsAndAlloy(items))
                        {
                            return RecipeReactorMk2;
                        }

                        if (GetItemQuantity(items, ItemEngineMk2) < 1f &&
                            HasAdvancedPartsAndAlloy(items))
                        {
                            return RecipeEngineMk2;
                        }

                        if (GetItemQuantity(items, ItemShieldM1) < 1f &&
                            HasAdvancedPartsAndAlloy(items))
                        {
                            return RecipeShieldM1;
                        }

                        if (GetItemQuantity(items, ItemMissileM1) < 1f &&
                            HasAdvancedPartsAndAlloy(items))
                        {
                            return RecipeMissileM1;
                        }
                    }

                    if (GetItemQuantity(items, ItemEngineMk1) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return RecipeEngineMk1;
                    }

                    if (GetItemQuantity(items, ItemShieldS1) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return RecipeShieldS1;
                    }

                    if (GetItemQuantity(items, ItemLaserS1) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return RecipeLaserS1;
                    }

                    if (GetItemQuantity(items, ItemBridgeMk1) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return RecipeBridgeMk1;
                    }

                    if (GetItemQuantity(items, ItemCockpitMk1) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return RecipeCockpitMk1;
                    }

                    if (GetItemQuantity(items, ItemArmorS1) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return RecipeArmorS1;
                    }

                    if (GetItemQuantity(items, ItemAmmoS1) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return RecipeAmmoS1;
                    }
                    break;
                }
                case FacilityBusinessClass.Shipyard:
                {
                    if (unlocks.ShipClassTier >= 2 &&
                        GetItemQuantity(items, ItemHullCvMule) < 1f &&
                        GetItemQuantity(items, ItemParts) >= 60f &&
                        GetItemQuantity(items, ItemAlloy) >= 80f &&
                        GetItemQuantity(items, ItemIngot) >= 40f)
                    {
                        return RecipeShipHullMule;
                    }

                    if (GetItemQuantity(items, ItemHullLcvSparrow) < 1f &&
                        GetItemQuantity(items, ItemParts) >= 30f &&
                        GetItemQuantity(items, ItemAlloy) >= 40f &&
                        GetItemQuantity(items, ItemIngot) >= 20f)
                    {
                        return RecipeShipHull;
                    }
                    break;
                }
                case FacilityBusinessClass.Research:
                {
                    var researchTarget = 5f;
                    if (GetItemQuantity(items, ItemResearch) < researchTarget &&
                        GetItemQuantity(items, ItemSupplies) >= 12f)
                    {
                        return RecipeResearchPacket;
                    }
                    break;
                }
            }

            return default;
        }

        private static bool HasPartsAndAlloy(DynamicBuffer<InventoryItem> items)
        {
            return GetItemQuantity(items, ItemParts) >= 4f && GetItemQuantity(items, ItemAlloy) >= 5f;
        }

        private static bool HasAdvancedPartsAndAlloy(DynamicBuffer<InventoryItem> items)
        {
            return GetItemQuantity(items, ItemParts) >= 8f && GetItemQuantity(items, ItemAlloy) >= 10f;
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

        private static void EnsureThroughput(ref BusinessProduction production)
        {
            if (production.Throughput > 0.01f)
            {
                return;
            }

            production.Throughput = math.max(1f, production.Capacity * 0.02f);
        }
    }
}
