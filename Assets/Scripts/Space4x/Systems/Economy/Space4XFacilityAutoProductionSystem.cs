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
        private static readonly FixedString64Bytes RecipeOreToAlloy = "space4x_ore_to_alloy";
        private static readonly FixedString64Bytes RecipePartsAssembly = "space4x_parts_assembly";
        private static readonly FixedString64Bytes RecipeShipHull = "space4x_ship_lcv_sparrow";
        private static readonly FixedString64Bytes RecipeResearchPacket = "space4x_research_packet";
        private static readonly FixedString64Bytes RecipeEngineMk1 = "space4x_module_engine_mk1";
        private static readonly FixedString64Bytes RecipeShieldS1 = "space4x_module_shield_s_1";
        private static readonly FixedString64Bytes RecipeLaserS1 = "space4x_module_laser_s_1";
        private static readonly FixedString64Bytes RecipeBridgeMk1 = "space4x_module_bridge_mk1";
        private static readonly FixedString64Bytes RecipeCockpitMk1 = "space4x_module_cockpit_mk1";
        private static readonly FixedString64Bytes RecipeArmorS1 = "space4x_module_armor_s_1";
        private static readonly FixedString64Bytes RecipeAmmoS1 = "space4x_module_ammo_bay_s_1";

        private static readonly FixedString64Bytes ItemOre = "space4x_ore";
        private static readonly FixedString64Bytes ItemIngot = "space4x_ingot";
        private static readonly FixedString64Bytes ItemAlloy = "space4x_alloy";
        private static readonly FixedString64Bytes ItemParts = "space4x_parts";
        private static readonly FixedString64Bytes ItemSupplies = "space4x_supplies";
        private static readonly FixedString64Bytes ItemResearch = "space4x_research";
        private static readonly FixedString64Bytes ItemEngineMk1 = "engine-mk1";
        private static readonly FixedString64Bytes ItemShieldS1 = "shield-s-1";
        private static readonly FixedString64Bytes ItemLaserS1 = "laser-s-1";
        private static readonly FixedString64Bytes ItemBridgeMk1 = "bridge-mk1";
        private static readonly FixedString64Bytes ItemCockpitMk1 = "cockpit-mk1";
        private static readonly FixedString64Bytes ItemArmorS1 = "armor-s-1";
        private static readonly FixedString64Bytes ItemAmmoS1 = "ammo-bay-s-1";
        private static readonly FixedString64Bytes ItemHullLcvSparrow = "lcv-sparrow";

        private ComponentLookup<BusinessInventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemsLookup;
        private BufferLookup<ProductionJob> _jobsLookup;

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
                if (inventoryEntity == Entity.Null || !_itemsLookup.HasBuffer(inventoryEntity))
                {
                    continue;
                }

                if (_jobsLookup.HasBuffer(entity) && _jobsLookup[entity].Length > 0)
                {
                    continue;
                }

                if (SystemAPI.HasComponent<ProductionJobRequest>(entity))
                {
                    continue;
                }

                EnsureThroughput(ref production.ValueRW);

                var items = _itemsLookup[inventoryEntity];
                var recipeId = SelectRecipe(facilityClass.ValueRO.Value, items, production.ValueRO.Capacity);
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

        private FixedString64Bytes SelectRecipe(FacilityBusinessClass role, DynamicBuffer<InventoryItem> items, float capacity)
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
                        return RecipeOreToIngot;
                    }

                    if (GetItemQuantity(items, ItemAlloy) < alloyTarget &&
                        GetItemQuantity(items, ItemOre) >= 80f &&
                        GetItemQuantity(items, ItemSupplies) >= 10f)
                    {
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
                        return RecipePartsAssembly;
                    }
                    break;
                }
                case FacilityBusinessClass.ModuleFacility:
                {
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
