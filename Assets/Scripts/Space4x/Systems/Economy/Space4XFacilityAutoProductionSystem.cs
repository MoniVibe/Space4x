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
        private ComponentLookup<BusinessInventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemsLookup;
        private BufferLookup<ProductionJob> _jobsLookup;

        private FixedString64Bytes _oreToIngot;
        private FixedString64Bytes _oreToAlloy;
        private FixedString64Bytes _partsAssembly;
        private FixedString64Bytes _shipHull;
        private FixedString64Bytes _researchPacket;
        private FixedString64Bytes _engineMk1;
        private FixedString64Bytes _shieldS1;
        private FixedString64Bytes _laserS1;
        private FixedString64Bytes _bridgeMk1;
        private FixedString64Bytes _cockpitMk1;
        private FixedString64Bytes _armorS1;
        private FixedString64Bytes _ammoS1;

        private FixedString64Bytes _oreId;
        private FixedString64Bytes _ingotId;
        private FixedString64Bytes _alloyId;
        private FixedString64Bytes _partsId;
        private FixedString64Bytes _suppliesId;
        private FixedString64Bytes _researchId;

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

            _oreToIngot = new FixedString64Bytes("space4x_ore_to_ingot");
            _oreToAlloy = new FixedString64Bytes("space4x_ore_to_alloy");
            _partsAssembly = new FixedString64Bytes("space4x_parts_assembly");
            _shipHull = new FixedString64Bytes("space4x_ship_lcv_sparrow");
            _researchPacket = new FixedString64Bytes("space4x_research_packet");
            _engineMk1 = new FixedString64Bytes("space4x_module_engine_mk1");
            _shieldS1 = new FixedString64Bytes("space4x_module_shield_s_1");
            _laserS1 = new FixedString64Bytes("space4x_module_laser_s_1");
            _bridgeMk1 = new FixedString64Bytes("space4x_module_bridge_mk1");
            _cockpitMk1 = new FixedString64Bytes("space4x_module_cockpit_mk1");
            _armorS1 = new FixedString64Bytes("space4x_module_armor_s_1");
            _ammoS1 = new FixedString64Bytes("space4x_module_ammo_bay_s_1");

            _oreId = new FixedString64Bytes("space4x_ore");
            _ingotId = new FixedString64Bytes("space4x_ingot");
            _alloyId = new FixedString64Bytes("space4x_alloy");
            _partsId = new FixedString64Bytes("space4x_parts");
            _suppliesId = new FixedString64Bytes("space4x_supplies");
            _researchId = new FixedString64Bytes("space4x_research");
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

                    if (GetItemQuantity(items, _ingotId) < ingotTarget &&
                        GetItemQuantity(items, _oreId) >= 100f)
                    {
                        return _oreToIngot;
                    }

                    if (GetItemQuantity(items, _alloyId) < alloyTarget &&
                        GetItemQuantity(items, _oreId) >= 80f &&
                        GetItemQuantity(items, _suppliesId) >= 10f)
                    {
                        return _oreToAlloy;
                    }
                    break;
                }
                case FacilityBusinessClass.Production:
                case FacilityBusinessClass.ShipFabrication:
                {
                    var partsTarget = math.max(20f, capacity * 0.05f);
                    if (GetItemQuantity(items, _partsId) < partsTarget &&
                        GetItemQuantity(items, _ingotId) >= 10f &&
                        GetItemQuantity(items, _suppliesId) >= 5f)
                    {
                        return _partsAssembly;
                    }
                    break;
                }
                case FacilityBusinessClass.ModuleFacility:
                {
                    if (GetItemQuantity(items, new FixedString64Bytes("engine-mk1")) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return _engineMk1;
                    }

                    if (GetItemQuantity(items, new FixedString64Bytes("shield-s-1")) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return _shieldS1;
                    }

                    if (GetItemQuantity(items, new FixedString64Bytes("laser-s-1")) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return _laserS1;
                    }

                    if (GetItemQuantity(items, new FixedString64Bytes("bridge-mk1")) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return _bridgeMk1;
                    }

                    if (GetItemQuantity(items, new FixedString64Bytes("cockpit-mk1")) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return _cockpitMk1;
                    }

                    if (GetItemQuantity(items, new FixedString64Bytes("armor-s-1")) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return _armorS1;
                    }

                    if (GetItemQuantity(items, new FixedString64Bytes("ammo-bay-s-1")) < 1f &&
                        HasPartsAndAlloy(items))
                    {
                        return _ammoS1;
                    }
                    break;
                }
                case FacilityBusinessClass.Shipyard:
                {
                    if (GetItemQuantity(items, new FixedString64Bytes("lcv-sparrow")) < 1f &&
                        GetItemQuantity(items, _partsId) >= 30f &&
                        GetItemQuantity(items, _alloyId) >= 40f &&
                        GetItemQuantity(items, _ingotId) >= 20f)
                    {
                        return _shipHull;
                    }
                    break;
                }
                case FacilityBusinessClass.Research:
                {
                    var researchTarget = 5f;
                    if (GetItemQuantity(items, _researchId) < researchTarget &&
                        GetItemQuantity(items, _suppliesId) >= 12f)
                    {
                        return _researchPacket;
                    }
                    break;
                }
            }

            return default;
        }

        private static bool HasPartsAndAlloy(DynamicBuffer<InventoryItem> items)
        {
            var parts = new FixedString64Bytes("space4x_parts");
            var alloy = new FixedString64Bytes("space4x_alloy");
            return GetItemQuantity(items, parts) >= 4f && GetItemQuantity(items, alloy) >= 5f;
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
