#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4x.Fleetcrawl;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XFleetcrawlHeatContractsTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XFleetcrawlHeatContractsTests");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void ResolveAggregate_IsDeterministic_AndIncludesCoolingSource()
        {
            var host = _entityManager.CreateEntity();
            var rolledLimbs = _entityManager.AddBuffer<FleetcrawlRolledLimbBufferElement>(host);
            var ownedItems = _entityManager.AddBuffer<FleetcrawlOwnedItem>(host);
            var heatDefs = _entityManager.AddBuffer<FleetcrawlHeatModifierDefinition>(host);

            rolledLimbs.Add(new FleetcrawlRolledLimbBufferElement
            {
                Value = new FleetcrawlRolledLimb
                {
                    LimbId = new FixedString64Bytes("limb_reactor_flux_core"),
                    AffixId = new FixedString64Bytes("affix_overclocked"),
                    ModuleType = FleetcrawlModuleType.Reactor,
                    Slot = FleetcrawlLimbSlot.Core,
                    ComboTags = FleetcrawlComboTag.Flux
                }
            });
            rolledLimbs.Add(new FleetcrawlRolledLimbBufferElement
            {
                Value = new FleetcrawlRolledLimb
                {
                    LimbId = new FixedString64Bytes("limb_coolant_radiator"),
                    AffixId = default,
                    ModuleType = FleetcrawlModuleType.Utility,
                    Slot = FleetcrawlLimbSlot.Cooling,
                    ComboTags = FleetcrawlComboTag.Support
                }
            });
            ownedItems.Add(new FleetcrawlOwnedItem
            {
                Value = new FleetcrawlRolledItem
                {
                    Archetype = FleetcrawlLootArchetype.GeneralItem,
                    ItemId = new FixedString64Bytes("item_flux_capsule"),
                    SetId = new FixedString64Bytes("set_prism"),
                    ComboTags = FleetcrawlComboTag.Flux
                }
            });

            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("by_limb"),
                SourceKind = FleetcrawlHeatModifierSourceKind.LimbId,
                SourceId = new FixedString64Bytes("limb_reactor_flux_core"),
                HeatGenerationMultiplier = 1.2f,
                HeatDamageBonusPerHeat01 = 0.15f
            });
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("by_affix"),
                SourceKind = FleetcrawlHeatModifierSourceKind.AffixId,
                SourceId = new FixedString64Bytes("affix_overclocked"),
                HeatGenerationMultiplier = 1.1f,
                HeatCooldownBonusPerHeat01 = 0.1f
            });
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("by_cooling_slot"),
                SourceKind = FleetcrawlHeatModifierSourceKind.LimbSlot,
                Slot = FleetcrawlLimbSlot.Cooling,
                HeatGenerationMultiplier = 0.9f,
                HeatDissipationMultiplier = 1.5f,
                OverheatThresholdOffset01 = 0.06f
            });
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("by_item"),
                SourceKind = FleetcrawlHeatModifierSourceKind.ItemId,
                SourceId = new FixedString64Bytes("item_flux_capsule"),
                HeatCapacityMultiplier = 1.2f
            });

            var first = FleetcrawlHeatResolver.ResolveAggregate(rolledLimbs, ownedItems, heatDefs);
            var second = FleetcrawlHeatResolver.ResolveAggregate(rolledLimbs, ownedItems, heatDefs);

            Assert.AreEqual(first.HeatGenerationMultiplier, second.HeatGenerationMultiplier);
            Assert.AreEqual(first.HeatDissipationMultiplier, second.HeatDissipationMultiplier);
            Assert.AreEqual(first.HeatCapacityMultiplier, second.HeatCapacityMultiplier);
            Assert.Greater(first.HeatDissipationMultiplier, 1f);
            Assert.Greater(first.OverheatThresholdOffset01, 0f);
            Assert.Greater(first.HeatDamageBonusPerHeat01, 0f);
            Assert.Greater(first.HeatCooldownBonusPerHeat01, 0f);
        }

        [Test]
        public void Tick_ProvidesHeatBonuses_WhenBelowOverheatThreshold()
        {
            var host = _entityManager.CreateEntity();
            var actions = _entityManager.AddBuffer<FleetcrawlHeatActionEvent>(host);

            var heatStats = FleetcrawlResolvedHeatStats.Identity;
            heatStats.HeatGenerationMultiplier = 1f;
            heatStats.HeatDissipationMultiplier = 1f;
            heatStats.HeatCapacityMultiplier = 1f;
            heatStats.HeatDamageBonusPerHeat01 = 0.4f;
            heatStats.HeatCooldownBonusPerHeat01 = 0.2f;
            heatStats.OverheatDamagePenaltyMultiplier = 0.75f;
            heatStats.OverheatCooldownPenaltyMultiplier = 1.25f;

            var runtime = new FleetcrawlHeatRuntimeState
            {
                CurrentHeat = 40f,
                BaseHeatCapacity = 100f,
                BaseDissipationPerTick = 20f,
                BaseOverheatThreshold01 = 0.85f,
                BaseRecoveryThreshold01 = 0.5f,
                IsOverheated = 0
            };

            actions.Add(new FleetcrawlHeatActionEvent
            {
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Barrel,
                BaseHeat = 20f,
                Scale = 1f
            });

            FleetcrawlHeatResolver.Tick(1u, actions, heatStats, ref runtime, out var output);

            Assert.AreEqual(0, runtime.IsOverheated);
            Assert.AreEqual(0, output.IsOverheated);
            Assert.Greater(output.Heat01, 0.39f);
            Assert.Less(output.Heat01, 0.41f);
            Assert.Greater(output.DamageMultiplier, 1f);
            Assert.Less(output.CooldownMultiplier, 1f);
            Assert.AreEqual(0, actions.Length);
        }

        [Test]
        public void Tick_EntersAndRecoversFromOverheat_WithCooling()
        {
            var host = _entityManager.CreateEntity();
            var actions = _entityManager.AddBuffer<FleetcrawlHeatActionEvent>(host);
            var heatStats = FleetcrawlResolvedHeatStats.Identity;
            heatStats.HeatGenerationMultiplier = 1f;
            heatStats.HeatDissipationMultiplier = 2f;
            heatStats.HeatCapacityMultiplier = 1f;
            heatStats.HeatDamageBonusPerHeat01 = 0.25f;
            heatStats.HeatCooldownBonusPerHeat01 = 0.1f;
            heatStats.OverheatDamagePenaltyMultiplier = 0.7f;
            heatStats.OverheatCooldownPenaltyMultiplier = 1.3f;

            var runtime = new FleetcrawlHeatRuntimeState
            {
                CurrentHeat = 0f,
                BaseHeatCapacity = 100f,
                BaseDissipationPerTick = 10f,
                BaseOverheatThreshold01 = 0.8f,
                BaseRecoveryThreshold01 = 0.45f,
                IsOverheated = 0
            };

            actions.Add(new FleetcrawlHeatActionEvent
            {
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Barrel,
                BaseHeat = 110f,
                Scale = 1f
            });

            FleetcrawlHeatResolver.Tick(10u, actions, heatStats, ref runtime, out var overheatOutput);
            Assert.AreEqual(1, runtime.IsOverheated);
            Assert.AreEqual(1, overheatOutput.IsOverheated);
            Assert.Less(overheatOutput.DamageMultiplier, 1f);
            Assert.Greater(overheatOutput.CooldownMultiplier, 1f);

            for (var tick = 11u; tick < 20u; tick++)
            {
                FleetcrawlHeatResolver.Tick(tick, actions, heatStats, ref runtime, out _);
            }

            Assert.AreEqual(0, runtime.IsOverheated);
            Assert.LessOrEqual(runtime.CurrentHeat, 45f);
        }
    }
}
#endif
