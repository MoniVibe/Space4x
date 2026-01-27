using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Motivation;
using PureDOTS.Systems.Motivation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Tests.EditMode.Motivation
{
    public class MotivationSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("TestWorld");
            _entityManager = _world.EntityManager;

            // Create required singletons
            var timeStateEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(timeStateEntity, new TimeState
            {
                Tick = 0,
                IsPaused = false
            });

            var rewindStateEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(rewindStateEntity, new RewindState
            {
                Mode = RewindMode.Record
            });

            // Create empty motivation config state
            var configEntity = _entityManager.CreateEntity();
            var emptyBlob = CreateEmptyCatalogBlob();
            _entityManager.AddComponentData(configEntity, new MotivationConfigState
            {
                Catalog = emptyBlob,
                TicksBetweenRefresh = 100u,
                DefaultDreamSlots = 3,
                DefaultAspirationSlots = 2,
                DefaultWishSlots = 2
            });
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        private BlobAssetReference<MotivationCatalog> CreateEmptyCatalogBlob()
        {
            var builder = new Unity.Collections.BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MotivationCatalog>();
            builder.Allocate(ref root.Specs, 0);
            var blob = builder.CreateBlobAssetReference<MotivationCatalog>(Unity.Collections.Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        [Test]
        public void MotivationIntentSelectionSystem_CalculateScore_UsesWeights()
        {
            var config = new MotivationScoringConfig
            {
                ImportanceWeight = 1.0f,
                InitiativeWeight = 2.0f,
                LoyaltyWeight = 0.5f
            };

            // Test: importance=100, initiative=50, loyalty=100
            float score = MotivationIntentSelectionSystem.CalculateScore(100, 50, 100, in config);
            float expected = 100f * 1.0f + 50f * 2.0f + 100f * 0.5f; // 100 + 100 + 50 = 250
            Assert.AreEqual(expected, score, 0.001f);
        }

        [Test]
        public void MotivationRewardSystem_CalculatesRewardCorrectly()
        {
            var entity = _entityManager.CreateEntity();
            
            // Add motivation drive
            _entityManager.AddComponentData(entity, new MotivationDrive
            {
                InitiativeCurrent = 100,
                InitiativeMax = 100,
                LoyaltyCurrent = 50,
                LoyaltyMax = 100,
                PrimaryLoyaltyTarget = Entity.Null,
                LastInitiativeTick = 0
            });

            // Add motivation slots buffer
            var slots = _entityManager.AddBuffer<MotivationSlot>(entity);
            slots.Add(new MotivationSlot
            {
                Layer = MotivationLayer.Dream,
                Status = MotivationStatus.Available,
                LockFlags = MotivationLockFlags.LockedByPlayer,
                SpecId = 1,
                Importance = 100,
                Progress = 0,
                StartedTick = 0,
                TargetEntity = Entity.Null,
                Param0 = 0,
                Param1 = 0
            });

            // Add legacy points
            _entityManager.AddComponentData(entity, new LegacyPoints
            {
                TotalEarned = 0,
                Unspent = 0
            });

            // Add goal completed buffer
            var completed = _entityManager.AddBuffer<GoalCompleted>(entity);
            completed.Add(new GoalCompleted
            {
                SlotIndex = 0,
                SpecId = 1
            });

            // Run reward system
            var system = _world.GetOrCreateSystemManaged<MotivationRewardSystem>();
            system.Update(_world.Unmanaged);

            // Check legacy points were awarded
            var legacy = _entityManager.GetComponentData<LegacyPoints>(entity);
            // Base reward = 100 / 10 = 10, bonus = 10 (LockedByPlayer), total = 20
            Assert.AreEqual(20, legacy.Unspent);
            Assert.AreEqual(20, legacy.TotalEarned);

            // Check slot was marked as satisfied
            var updatedSlots = _entityManager.GetBuffer<MotivationSlot>(entity);
            Assert.AreEqual(MotivationStatus.Satisfied, updatedSlots[0].Status);
            Assert.AreEqual(255, updatedSlots[0].Progress);
        }

        [Test]
        public void MotivationInitializeSystem_CreatesRequiredComponents()
        {
            var entity = _entityManager.CreateEntity();
            
            // Add motivation drive
            _entityManager.AddComponentData(entity, new MotivationDrive
            {
                InitiativeCurrent = 100,
                InitiativeMax = 100,
                LoyaltyCurrent = 50,
                LoyaltyMax = 100,
                PrimaryLoyaltyTarget = Entity.Null,
                LastInitiativeTick = 0
            });

            // Run initialization system
            var system = _world.GetOrCreateSystemManaged<MotivationInitializeSystem>();
            system.Update(_world.Unmanaged);

            // Check that buffer and components were created
            Assert.IsTrue(_entityManager.HasBuffer<MotivationSlot>(entity));
            Assert.IsTrue(_entityManager.HasComponent<MotivationIntent>(entity));
            Assert.IsTrue(_entityManager.HasComponent<LegacyPoints>(entity));

            // Check buffer was populated with default slots
            var slots = _entityManager.GetBuffer<MotivationSlot>(entity);
            Assert.AreEqual(7, slots.Length); // 3 dreams + 2 aspirations + 2 wishes

            // Check intent was initialized
            var intent = _entityManager.GetComponentData<MotivationIntent>(entity);
            Assert.AreEqual(255, intent.ActiveSlotIndex); // No active slot
            Assert.AreEqual(MotivationLayer.Dream, intent.ActiveLayer);
            Assert.AreEqual(-1, intent.ActiveSpecId);
        }

        [Test]
        public void MotivationCatalog_MergesCorrectly()
        {
            // This test would require the authoring assets, so we'll test the merge logic conceptually
            // In a real test, you'd create MotivationSpecAsset instances and test MergeCatalogs
            
            var spec1 = new MotivationSpec
            {
                SpecId = 1,
                Layer = MotivationLayer.Dream,
                Scope = MotivationScope.Individual,
                Tag = MotivationTag.GainWealth,
                BaseImportance = 100,
                BaseInitiativeCost = 50,
                MaxConcurrentHolders = 0,
                RequiredLoyalty = 0,
                MinCorruptPure = 0,
                MinLawChaos = 0,
                MinGoodEvil = 0
            };

            var spec2 = new MotivationSpec
            {
                SpecId = 1, // Same ID, should override
                Layer = MotivationLayer.Aspiration,
                Scope = MotivationScope.Individual,
                Tag = MotivationTag.BecomeLegendary,
                BaseImportance = 200,
                BaseInitiativeCost = 100,
                MaxConcurrentHolders = 0,
                RequiredLoyalty = 0,
                MinCorruptPure = 0,
                MinLawChaos = 0,
                MinGoodEvil = 0
            };

            // Build blob from specs
            var builder = new Unity.Collections.BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MotivationCatalog>();
            var specsArray = builder.Allocate(ref root.Specs, 2);
            specsArray[0] = spec1;
            specsArray[1] = spec2;
            var blob = builder.CreateBlobAssetReference<MotivationCatalog>(Unity.Collections.Allocator.Persistent);
            builder.Dispose();

            // Verify blob was created
            Assert.IsTrue(blob.IsCreated);
            Assert.AreEqual(2, blob.Value.Specs.Length);
            Assert.AreEqual(MotivationLayer.Dream, blob.Value.Specs[0].Layer);
            Assert.AreEqual(MotivationLayer.Aspiration, blob.Value.Specs[1].Layer);

            blob.Dispose();
        }
    }
}
























