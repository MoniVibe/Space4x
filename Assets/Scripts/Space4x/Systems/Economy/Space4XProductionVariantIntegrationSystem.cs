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
    /// Applies reverse engineering variants to freshly produced inventory items.
    /// </summary>
    [BurstCompile]
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
            _facilityLinkLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _variantLookup.Update(ref state);

            var tick = tickTime.Tick;

            foreach (var (inventory, link, facility) in SystemAPI
                         .Query<RefRO<BusinessInventory>, RefRO<ColonyFacilityLink>>()
                         .WithEntityAccess())
            {
                var inventoryEntity = inventory.ValueRO.InventoryEntity;
                if (inventoryEntity == Entity.Null || !_itemsLookup.HasBuffer(inventoryEntity))
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
            if (colony != Entity.Null && _factionLookup.HasComponent(colony))
            {
                factionEntity = colony;
                return true;
            }

            if (colony != Entity.Null && _affiliationLookup.HasBuffer(colony))
            {
                var affiliations = _affiliationLookup[colony];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var target = affiliations[i].Target;
                    if (target != Entity.Null && _factionLookup.HasComponent(target))
                    {
                        factionEntity = target;
                        return true;
                    }
                }
            }

            factionEntity = Entity.Null;
            return false;
        }

        private static ushort ResolveBlueprintFamily(in FixedString64Bytes itemId)
        {
            if (itemId.IsEmpty)
            {
                return 0;
            }

            if (itemId.Equals("lcv-sparrow") || itemId.Equals("cv-mule"))
            {
                return ReverseEngineeringBlueprintFamily.Hull;
            }

            if (itemId.Equals("engine-mk1") || itemId.Equals("engine-mk2"))
            {
                return ReverseEngineeringBlueprintFamily.Engine;
            }

            if (itemId.Equals("shield-s-1") || itemId.Equals("shield-m-1"))
            {
                return ReverseEngineeringBlueprintFamily.Shield;
            }

            if (itemId.Equals("laser-s-1") || itemId.Equals("missile-m-1") || itemId.Equals("missile-s-1") || itemId.Equals("pd-s-1"))
            {
                return ReverseEngineeringBlueprintFamily.Weapon;
            }

            if (itemId.Equals("bridge-mk1") || itemId.Equals("cockpit-mk1"))
            {
                return ReverseEngineeringBlueprintFamily.Command;
            }

            if (itemId.Equals("armor-s-1"))
            {
                return ReverseEngineeringBlueprintFamily.Armor;
            }

            if (itemId.Equals("ammo-bay-s-1"))
            {
                return ReverseEngineeringBlueprintFamily.Ammo;
            }

            if (itemId.Equals("reactor-mk2"))
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
