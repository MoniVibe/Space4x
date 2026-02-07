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
        private static readonly FixedString64Bytes IdLcvSparrow = BuildIdLcvSparrow();
        private static readonly FixedString64Bytes IdCvMule = BuildIdCvMule();
        private static readonly FixedString64Bytes IdEngineMk1 = BuildIdEngineMk1();
        private static readonly FixedString64Bytes IdEngineMk2 = BuildIdEngineMk2();
        private static readonly FixedString64Bytes IdShieldS1 = BuildIdShieldS1();
        private static readonly FixedString64Bytes IdShieldM1 = BuildIdShieldM1();
        private static readonly FixedString64Bytes IdLaserS1 = BuildIdLaserS1();
        private static readonly FixedString64Bytes IdMissileM1 = BuildIdMissileM1();
        private static readonly FixedString64Bytes IdMissileS1 = BuildIdMissileS1();
        private static readonly FixedString64Bytes IdPdS1 = BuildIdPdS1();
        private static readonly FixedString64Bytes IdBridgeMk1 = BuildIdBridgeMk1();
        private static readonly FixedString64Bytes IdCockpitMk1 = BuildIdCockpitMk1();
        private static readonly FixedString64Bytes IdArmorS1 = BuildIdArmorS1();
        private static readonly FixedString64Bytes IdAmmoBayS1 = BuildIdAmmoBayS1();
        private static readonly FixedString64Bytes IdReactorMk2 = BuildIdReactorMk2();

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

            if (itemId.Equals(IdLcvSparrow) || itemId.Equals(IdCvMule))
            {
                return ReverseEngineeringBlueprintFamily.Hull;
            }

            if (itemId.Equals(IdEngineMk1) || itemId.Equals(IdEngineMk2))
            {
                return ReverseEngineeringBlueprintFamily.Engine;
            }

            if (itemId.Equals(IdShieldS1) || itemId.Equals(IdShieldM1))
            {
                return ReverseEngineeringBlueprintFamily.Shield;
            }

            if (itemId.Equals(IdLaserS1) || itemId.Equals(IdMissileM1) || itemId.Equals(IdMissileS1) || itemId.Equals(IdPdS1))
            {
                return ReverseEngineeringBlueprintFamily.Weapon;
            }

            if (itemId.Equals(IdBridgeMk1) || itemId.Equals(IdCockpitMk1))
            {
                return ReverseEngineeringBlueprintFamily.Command;
            }

            if (itemId.Equals(IdArmorS1))
            {
                return ReverseEngineeringBlueprintFamily.Armor;
            }

            if (itemId.Equals(IdAmmoBayS1))
            {
                return ReverseEngineeringBlueprintFamily.Ammo;
            }

            if (itemId.Equals(IdReactorMk2))
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

        private static FixedString64Bytes BuildIdLcvSparrow()
        {
            FixedString64Bytes value = default;
            value.Append('l');
            value.Append('c');
            value.Append('v');
            value.Append('-');
            value.Append('s');
            value.Append('p');
            value.Append('a');
            value.Append('r');
            value.Append('r');
            value.Append('o');
            value.Append('w');
            return value;
        }

        private static FixedString64Bytes BuildIdCvMule()
        {
            FixedString64Bytes value = default;
            value.Append('c');
            value.Append('v');
            value.Append('-');
            value.Append('m');
            value.Append('u');
            value.Append('l');
            value.Append('e');
            return value;
        }

        private static FixedString64Bytes BuildIdEngineMk1()
        {
            FixedString64Bytes value = default;
            value.Append('e');
            value.Append('n');
            value.Append('g');
            value.Append('i');
            value.Append('n');
            value.Append('e');
            value.Append('-');
            value.Append('m');
            value.Append('k');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdEngineMk2()
        {
            FixedString64Bytes value = default;
            value.Append('e');
            value.Append('n');
            value.Append('g');
            value.Append('i');
            value.Append('n');
            value.Append('e');
            value.Append('-');
            value.Append('m');
            value.Append('k');
            value.Append('2');
            return value;
        }

        private static FixedString64Bytes BuildIdShieldS1()
        {
            FixedString64Bytes value = default;
            value.Append('s');
            value.Append('h');
            value.Append('i');
            value.Append('e');
            value.Append('l');
            value.Append('d');
            value.Append('-');
            value.Append('s');
            value.Append('-');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdShieldM1()
        {
            FixedString64Bytes value = default;
            value.Append('s');
            value.Append('h');
            value.Append('i');
            value.Append('e');
            value.Append('l');
            value.Append('d');
            value.Append('-');
            value.Append('m');
            value.Append('-');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdLaserS1()
        {
            FixedString64Bytes value = default;
            value.Append('l');
            value.Append('a');
            value.Append('s');
            value.Append('e');
            value.Append('r');
            value.Append('-');
            value.Append('s');
            value.Append('-');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdMissileM1()
        {
            FixedString64Bytes value = default;
            value.Append('m');
            value.Append('i');
            value.Append('s');
            value.Append('s');
            value.Append('i');
            value.Append('l');
            value.Append('e');
            value.Append('-');
            value.Append('m');
            value.Append('-');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdMissileS1()
        {
            FixedString64Bytes value = default;
            value.Append('m');
            value.Append('i');
            value.Append('s');
            value.Append('s');
            value.Append('i');
            value.Append('l');
            value.Append('e');
            value.Append('-');
            value.Append('s');
            value.Append('-');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdPdS1()
        {
            FixedString64Bytes value = default;
            value.Append('p');
            value.Append('d');
            value.Append('-');
            value.Append('s');
            value.Append('-');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdBridgeMk1()
        {
            FixedString64Bytes value = default;
            value.Append('b');
            value.Append('r');
            value.Append('i');
            value.Append('d');
            value.Append('g');
            value.Append('e');
            value.Append('-');
            value.Append('m');
            value.Append('k');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdCockpitMk1()
        {
            FixedString64Bytes value = default;
            value.Append('c');
            value.Append('o');
            value.Append('c');
            value.Append('k');
            value.Append('p');
            value.Append('i');
            value.Append('t');
            value.Append('-');
            value.Append('m');
            value.Append('k');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdArmorS1()
        {
            FixedString64Bytes value = default;
            value.Append('a');
            value.Append('r');
            value.Append('m');
            value.Append('o');
            value.Append('r');
            value.Append('-');
            value.Append('s');
            value.Append('-');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdAmmoBayS1()
        {
            FixedString64Bytes value = default;
            value.Append('a');
            value.Append('m');
            value.Append('m');
            value.Append('o');
            value.Append('-');
            value.Append('b');
            value.Append('a');
            value.Append('y');
            value.Append('-');
            value.Append('s');
            value.Append('-');
            value.Append('1');
            return value;
        }

        private static FixedString64Bytes BuildIdReactorMk2()
        {
            FixedString64Bytes value = default;
            value.Append('r');
            value.Append('e');
            value.Append('a');
            value.Append('c');
            value.Append('t');
            value.Append('o');
            value.Append('r');
            value.Append('-');
            value.Append('m');
            value.Append('k');
            value.Append('2');
            return value;
        }
    }
}
