#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using Space4x.Fleetcrawl;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XFleetcrawlLootShopFoundationsTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XFleetcrawlLootShopFoundationsTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            var seeded = _entityManager.CreateEntity(typeof(Space4XFleetcrawlSeeded));
            _entityManager.SetComponentData(seeded, new Space4XFleetcrawlSeeded
            {
                ScenarioId = new FixedString64Bytes("space4x_fleetcrawl_loot_shop_foundations")
            });

            var director = _entityManager.CreateEntity(typeof(Space4XFleetcrawlDirectorState));
            _entityManager.SetComponentData(director, new Space4XFleetcrawlDirectorState
            {
                Seed = 123456u,
                CurrentRoomIndex = 2,
                Initialized = 1
            });
            _entityManager.AddComponentData(director, new FleetcrawlRunLevelState { Level = 5 });
            _entityManager.AddComponentData(director, new FleetcrawlRunExperience { Value = 220 });
            _entityManager.AddComponentData(director, new FleetcrawlRunShardWallet { Shards = 145 });
            _entityManager.AddComponentData(director, new FleetcrawlRunChallengeState { Challenge = 3 });
            _entityManager.AddComponentData(director, new Space4XRunRerollTokens { Value = 2 });
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
        public void LimbRoll_IsDeterministic_ForSameSeedRoomAndLevel()
        {
            var runtime = _entityManager.CreateEntity();
            var limbs = _entityManager.AddBuffer<FleetcrawlModuleLimbDefinition>(runtime);
            limbs.Add(new FleetcrawlModuleLimbDefinition
            {
                LimbId = new FixedString64Bytes("limb_alpha"),
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Barrel,
                SharingMode = FleetcrawlLimbSharingMode.Unique,
                ComboTags = FleetcrawlComboTag.Siege,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 10,
                MinLevel = 1,
                TurnRateMultiplier = 1f,
                AccelerationMultiplier = 1f,
                DecelerationMultiplier = 1f,
                MaxSpeedMultiplier = 1f,
                CooldownMultiplier = 1f,
                DamageMultiplier = 1.1f
            });
            limbs.Add(new FleetcrawlModuleLimbDefinition
            {
                LimbId = new FixedString64Bytes("limb_beta"),
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Barrel,
                SharingMode = FleetcrawlLimbSharingMode.Shared,
                ComboTags = FleetcrawlComboTag.Agile,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 12,
                MinLevel = 1,
                TurnRateMultiplier = 1f,
                AccelerationMultiplier = 1f,
                DecelerationMultiplier = 1f,
                MaxSpeedMultiplier = 1f,
                CooldownMultiplier = 1f,
                DamageMultiplier = 1f
            });

            var affixes = _entityManager.AddBuffer<FleetcrawlLimbAffixDefinition>(runtime);
            affixes.Add(new FleetcrawlLimbAffixDefinition
            {
                AffixId = new FixedString64Bytes("affix_precision"),
                Slot = FleetcrawlLimbSlot.Barrel,
                ComboTags = FleetcrawlComboTag.Siege,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 8,
                TurnRateMultiplier = 1f,
                AccelerationMultiplier = 1f,
                DecelerationMultiplier = 1f,
                MaxSpeedMultiplier = 1f,
                CooldownMultiplier = 1f,
                DamageMultiplier = 1.05f
            });

            var first = FleetcrawlDeterministicLimbRollService.RollLimb(
                seed: 7788u,
                roomIndex: 3,
                level: 6,
                moduleType: FleetcrawlModuleType.Weapon,
                slot: FleetcrawlLimbSlot.Barrel,
                stream: 0,
                limbDefinitions: limbs,
                affixDefinitions: affixes);

            var second = FleetcrawlDeterministicLimbRollService.RollLimb(
                seed: 7788u,
                roomIndex: 3,
                level: 6,
                moduleType: FleetcrawlModuleType.Weapon,
                slot: FleetcrawlLimbSlot.Barrel,
                stream: 0,
                limbDefinitions: limbs,
                affixDefinitions: affixes);

            Assert.AreEqual(first.RollHash, second.RollHash);
            Assert.AreEqual(first.Quality, second.Quality);
            Assert.AreEqual(first.LimbId, second.LimbId);
            Assert.AreEqual(first.AffixId, second.AffixId);
        }

        [Test]
        public void OfferGeneration_IsDeterministic_FromRunState()
        {
            var bootstrap = _world.GetOrCreateSystem<Space4XFleetcrawlLootShopBootstrapSystem>();
            var generation = _world.GetOrCreateSystem<Space4XFleetcrawlOfferGenerationSystem>();
            bootstrap.Update(_world.Unmanaged);
            generation.Update(_world.Unmanaged);

            var runtime = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<FleetcrawlOfferRuntimeTag>()).GetSingletonEntity();
            var currencyA = _entityManager.GetBuffer<FleetcrawlCurrencyShopOfferEntry>(runtime);
            var lootA = _entityManager.GetBuffer<FleetcrawlLootOfferEntry>(runtime);
            Assert.Greater(currencyA.Length, 0);
            Assert.Greater(lootA.Length, 0);

            var firstCurrencyHash = currencyA[0].RollHash;
            var firstCurrencyOffer = currencyA[0].OfferId;
            var firstLootHash = lootA[0].RollHash;
            var firstLootLimb = lootA[0].RolledLimb.LimbId;

            var refreshRequest = _entityManager.CreateEntity(typeof(FleetcrawlOfferRefreshRequest));
            _entityManager.SetComponentData(refreshRequest, new FleetcrawlOfferRefreshRequest
            {
                Nonce = 0u,
                SourceTag = new FixedString32Bytes("repeat")
            });
            generation.Update(_world.Unmanaged);

            var currencyB = _entityManager.GetBuffer<FleetcrawlCurrencyShopOfferEntry>(runtime);
            var lootB = _entityManager.GetBuffer<FleetcrawlLootOfferEntry>(runtime);

            Assert.AreEqual(firstCurrencyHash, currencyB[0].RollHash);
            Assert.AreEqual(firstCurrencyOffer, currencyB[0].OfferId);
            Assert.AreEqual(firstLootHash, lootB[0].RollHash);
            Assert.AreEqual(firstLootLimb, lootB[0].RolledLimb.LimbId);
        }

        [Test]
        public void LimbCompatibility_SharedAllowsDuplicates_UniqueBlocksDuplicates()
        {
            var host = _entityManager.CreateEntity();
            var equipped = _entityManager.AddBuffer<FleetcrawlRolledLimbBufferElement>(host);
            equipped.Add(new FleetcrawlRolledLimbBufferElement
            {
                Value = new FleetcrawlRolledLimb
                {
                    LimbId = new FixedString64Bytes("limb_shared"),
                    SharingMode = FleetcrawlLimbSharingMode.Shared,
                    ModuleType = FleetcrawlModuleType.Weapon,
                    Slot = FleetcrawlLimbSlot.Stabilizer
                }
            });

            var sharedCandidate = new FleetcrawlRolledLimb
            {
                LimbId = new FixedString64Bytes("limb_shared"),
                SharingMode = FleetcrawlLimbSharingMode.Shared,
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Scope
            };
            Assert.IsFalse(FleetcrawlModuleLimbCompatibility.HasLimbConflict(sharedCandidate, equipped));

            var uniqueCandidate = new FleetcrawlRolledLimb
            {
                LimbId = new FixedString64Bytes("limb_shared"),
                SharingMode = FleetcrawlLimbSharingMode.Unique,
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Scope
            };
            Assert.IsTrue(FleetcrawlModuleLimbCompatibility.HasLimbConflict(uniqueCandidate, equipped));
        }
    }
}
#endif
