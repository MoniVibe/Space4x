using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Production;
using PureDOTS.Runtime.Economy.Resources;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Applies reverse engineering variants to freshly produced inventory items.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Runtime.Economy.Production.ProductionJobCompletionSystem))]
    [UpdateBefore(typeof(Space4XColonyIndustryTransferSystem))]
    public partial struct Space4XProductionVariantIntegrationSystem : ISystem
    {
        private ComponentLookup<BusinessInventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemsLookup;
        private ComponentLookup<ColonyFacilityLink> _facilityLinkLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<ReverseEngineeringBlueprintVariant> _variantLookup;
        private EntityStorageInfoLookup _entityLookup;
        private FixedString64Bytes _lcvSparrowId;
        private FixedString64Bytes _cvMuleId;
        private FixedString64Bytes _engineMk1Id;
        private FixedString64Bytes _engineMk2Id;
        private FixedString64Bytes _shieldS1Id;
        private FixedString64Bytes _shieldM1Id;
        private FixedString64Bytes _laserS1Id;
        private FixedString64Bytes _missileM1Id;
        private FixedString64Bytes _missileS1Id;
        private FixedString64Bytes _pdS1Id;
        private FixedString64Bytes _bridgeMk1Id;
        private FixedString64Bytes _cockpitMk1Id;
        private FixedString64Bytes _armorS1Id;
        private FixedString64Bytes _ammoBayS1Id;
        private FixedString64Bytes _reactorMk2Id;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();

            _inventoryLookup = state.GetComponentLookup<BusinessInventory>(true);
            _itemsLookup = state.GetBufferLookup<InventoryItem>(false);
            _facilityLinkLookup = state.GetComponentLookup<ColonyFacilityLink>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _variantLookup = state.GetBufferLookup<ReverseEngineeringBlueprintVariant>(false);
            _entityLookup = state.GetEntityStorageInfoLookup();

            _lcvSparrowId = new FixedString64Bytes("lcv-sparrow");
            _cvMuleId = new FixedString64Bytes("cv-mule");
            _engineMk1Id = new FixedString64Bytes("engine-mk1");
            _engineMk2Id = new FixedString64Bytes("engine-mk2");
            _shieldS1Id = new FixedString64Bytes("shield-s-1");
            _shieldM1Id = new FixedString64Bytes("shield-m-1");
            _laserS1Id = new FixedString64Bytes("laser-s-1");
            _missileM1Id = new FixedString64Bytes("missile-m-1");
            _missileS1Id = new FixedString64Bytes("missile-s-1");
            _pdS1Id = new FixedString64Bytes("pd-s-1");
            _bridgeMk1Id = new FixedString64Bytes("bridge-mk1");
            _cockpitMk1Id = new FixedString64Bytes("cockpit-mk1");
            _armorS1Id = new FixedString64Bytes("armor-s-1");
            _ammoBayS1Id = new FixedString64Bytes("ammo-bay-s-1");
            _reactorMk2Id = new FixedString64Bytes("reactor-mk2");
        }

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
            _facilityLinkLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _variantLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var tick = tickTime.Tick;

            foreach (var (inventory, link, facility) in SystemAPI
                         .Query<RefRO<BusinessInventory>, RefRO<ColonyFacilityLink>>()
                         .WithEntityAccess())
            {
                if (!IsValidEntity(facility))
                {
                    continue;
                }

                var inventoryEntity = inventory.ValueRO.InventoryEntity;
                if (!IsValidEntity(inventoryEntity) || !_itemsLookup.HasBuffer(inventoryEntity))
                {
                    continue;
                }

                if (!TryResolveFaction(link.ValueRO.Colony, out var factionEntity))
                {
                    continue;
                }

                if (!_variantLookup.HasBuffer(factionEntity))
                {
                    continue;
                }

                var variants = _variantLookup[factionEntity];
                if (variants.Length == 0)
                {
                    continue;
                }

                var items = _itemsLookup[inventoryEntity];
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    if (item.CreatedTick != tick)
                    {
                        continue;
                    }

                    var blueprintId = ResolveBlueprintFamily(item.ItemId);
                    if (blueprintId == 0)
                    {
                        continue;
                    }

                    if (!TrySelectVariant(ref variants, blueprintId, out var variantIndex))
                    {
                        continue;
                    }

                    var variant = variants[variantIndex];
                    if (variant.RemainingRuns == 0)
                    {
                        continue;
                    }

                    var qualityMultiplier = ResolveQualityMultiplier(variant);
                    var durabilityMultiplier = ResolveDurabilityMultiplier(variant);

                    item.Quality = math.clamp(item.Quality * qualityMultiplier, 0.05f, 1f);
                    item.Durability = math.clamp(item.Durability * durabilityMultiplier, 0.05f, 1f);
                    items[i] = item;

                    var runsUsed = math.min((int)variant.RemainingRuns, math.max(1, (int)math.round(item.Quantity)));
                    variant.RemainingRuns = (byte)math.max(0, variant.RemainingRuns - runsUsed);
                    variants[variantIndex] = variant;
                }
            }
        }

        private bool TryResolveFaction(Entity colony, out Entity factionEntity)
        {
            if (IsValidEntity(colony) && _factionLookup.HasComponent(colony))
            {
                factionEntity = colony;
                return true;
            }

            if (IsValidEntity(colony) && _affiliationLookup.HasBuffer(colony))
            {
                var affiliations = _affiliationLookup[colony];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var target = affiliations[i].Target;
                    if (IsValidEntity(target) && _factionLookup.HasComponent(target))
                    {
                        factionEntity = target;
                        return true;
                    }
                }
            }

            factionEntity = Entity.Null;
            return false;
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityLookup.Exists(entity);
        }

        private ushort ResolveBlueprintFamily(in FixedString64Bytes itemId)
        {
            if (itemId.IsEmpty)
            {
                return 0;
            }

            if (itemId.Equals(_lcvSparrowId) || itemId.Equals(_cvMuleId))
            {
                return ReverseEngineeringBlueprintFamily.Hull;
            }

            if (itemId.Equals(_engineMk1Id) || itemId.Equals(_engineMk2Id))
            {
                return ReverseEngineeringBlueprintFamily.Engine;
            }

            if (itemId.Equals(_shieldS1Id) || itemId.Equals(_shieldM1Id))
            {
                return ReverseEngineeringBlueprintFamily.Shield;
            }

            if (itemId.Equals(_laserS1Id) || itemId.Equals(_missileM1Id) || itemId.Equals(_missileS1Id) || itemId.Equals(_pdS1Id))
            {
                return ReverseEngineeringBlueprintFamily.Weapon;
            }

            if (itemId.Equals(_bridgeMk1Id) || itemId.Equals(_cockpitMk1Id))
            {
                return ReverseEngineeringBlueprintFamily.Command;
            }

            if (itemId.Equals(_armorS1Id))
            {
                return ReverseEngineeringBlueprintFamily.Armor;
            }

            if (itemId.Equals(_ammoBayS1Id))
            {
                return ReverseEngineeringBlueprintFamily.Ammo;
            }

            if (itemId.Equals(_reactorMk2Id))
            {
                return ReverseEngineeringBlueprintFamily.Reactor;
            }

            return 0;
        }

        private static bool TrySelectVariant(ref DynamicBuffer<ReverseEngineeringBlueprintVariant> variants, ushort blueprintId, out int index)
        {
            index = -1;
            var bestQuality = -1;
            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                if (variant.BlueprintId != blueprintId || variant.RemainingRuns == 0)
                {
                    continue;
                }

                if (variant.Quality > bestQuality)
                {
                    bestQuality = variant.Quality;
                    index = i;
                }
            }

            return index >= 0;
        }

        private static float ResolveQualityMultiplier(in ReverseEngineeringBlueprintVariant variant)
        {
            var qualityFactor = math.saturate(variant.Quality / 100f);
            var qualityBias = (qualityFactor - 0.5f) * 0.4f;
            var efficiencyBias = (math.clamp(variant.EfficiencyScalar, 0.6f, 1.4f) - 1f) * 0.3f;
            return math.clamp(1f + qualityBias + efficiencyBias, 0.7f, 1.3f);
        }

        private static float ResolveDurabilityMultiplier(in ReverseEngineeringBlueprintVariant variant)
        {
            return math.clamp(variant.DurabilityScalar, 0.7f, 1.3f);
        }
    }
}
