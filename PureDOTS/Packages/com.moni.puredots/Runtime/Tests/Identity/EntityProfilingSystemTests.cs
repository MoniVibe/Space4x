#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Identity;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Stats;
using PureDOTS.Runtime.Villagers;
using PureDOTS.Systems.Identity;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.TestTools;
using CoreIndividualStats = PureDOTS.Runtime.Individual.IndividualStats;
using IdentityPersonalityAxes = PureDOTS.Runtime.Identity.PersonalityAxes;

namespace PureDOTS.Tests.Identity
{
    /// <summary>
    /// Integration tests for Entity Profiling systems.
    /// Tests bootstrap, profiling phases, and completion.
    /// </summary>
    public class EntityProfilingSystemTests
    {
        [DisableAutoCreation]
        private sealed partial class BootstrapWrapperSystem : SystemBase
        {
            private EntityProfilingBootstrapSystem _system;

            protected override void OnCreate()
            {
                base.OnCreate();
                _system = new EntityProfilingBootstrapSystem();
                _system.OnCreate(ref CheckedStateRef);
            }

            protected override void OnDestroy()
            {
                _system.OnDestroy(ref CheckedStateRef);
                base.OnDestroy();
            }

            protected override void OnUpdate()
            {
                _system.OnUpdate(ref CheckedStateRef);
            }
        }

        [DisableAutoCreation]
        private sealed partial class OfficerStatsApplicationWrapperSystem : SystemBase
        {
            private OfficerStatsApplicationSystem _system;

            protected override void OnCreate()
            {
                base.OnCreate();
                _system = new OfficerStatsApplicationSystem();
                _system.OnCreate(ref CheckedStateRef);
            }

            protected override void OnDestroy()
            {
                _system.OnDestroy(ref CheckedStateRef);
                base.OnDestroy();
            }

            protected override void OnUpdate()
            {
                _system.OnUpdate(ref CheckedStateRef);
            }
        }

        [DisableAutoCreation]
        private sealed partial class ArchetypeResolutionWrapperSystem : SystemBase
        {
            private ArchetypeResolutionSystem _system;

            protected override void OnCreate()
            {
                base.OnCreate();
                _system = new ArchetypeResolutionSystem();
                _system.OnCreate(ref CheckedStateRef);
            }

            protected override void OnDestroy()
            {
                _system.OnDestroy(ref CheckedStateRef);
                base.OnDestroy();
            }

            protected override void OnUpdate()
            {
                _system.OnUpdate(ref CheckedStateRef);
            }
        }

        private struct TestWorldContext : System.IDisposable
        {
            public World World;
            public BootstrapWrapperSystem BootstrapSystem;
            public OfficerStatsApplicationWrapperSystem OfficerStatsSystem;
            public ArchetypeResolutionWrapperSystem ArchetypeResolutionSystem;

            public TestWorldContext(World world)
            {
                World = world;
                BootstrapSystem = world.GetOrCreateSystemManaged<BootstrapWrapperSystem>();
                OfficerStatsSystem = world.GetOrCreateSystemManaged<OfficerStatsApplicationWrapperSystem>();
                ArchetypeResolutionSystem = world.GetOrCreateSystemManaged<ArchetypeResolutionWrapperSystem>();
            }

            public void Dispose()
            {
                World.Dispose();
                if (Unity.Entities.World.DefaultGameObjectInjectionWorld == World)
                {
                    Unity.Entities.World.DefaultGameObjectInjectionWorld = null;
                }
            }
        }

        private static TestWorldContext CreateContext()
        {
            var world = new World("TestWorld");
            var entityManager = world.EntityManager;

            // Create TimeState singleton
            var timeStateEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(timeStateEntity, new TimeState
            {
                Tick = 0,
                DeltaTime = 0.016f,
                DeltaSeconds = 0.016f,
                FixedDeltaTime = 0.016f,
                CurrentSpeedMultiplier = 1f
            });

            // Create VillagerArchetypeCatalogComponent singleton (empty catalog for testing)
            var catalogEntity = entityManager.CreateEntity();
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref blobBuilder.ConstructRoot<VillagerArchetypeCatalogBlob>();
            var archetypesArray = blobBuilder.Allocate(ref catalogBlob.Archetypes, 1);
            archetypesArray[0] = new VillagerArchetypeData
            {
                ArchetypeName = new FixedString64Bytes("Default"),
                BasePhysique = 50,
                BaseFinesse = 50,
                BaseWillpower = 50,
                MoralAxisLean = 0,
                OrderAxisLean = 0,
                PurityAxisLean = 0
            };
            var blobAsset = blobBuilder.CreateBlobAssetReference<VillagerArchetypeCatalogBlob>(Allocator.Persistent);
            blobBuilder.Dispose();
            entityManager.AddComponentData(catalogEntity, new VillagerArchetypeCatalogComponent { Catalog = blobAsset });

            return new TestWorldContext(world);
        }

        private static void UpdateSystem(ComponentSystemBase system)
        {
            system?.Update();
        }

        [Test]
        public void Bootstrap_CreatesEntityProfile_ForVillagerId()
        {
            // Arrange
            using var ctx = CreateContext();
            var entityManager = ctx.World.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent(entity, new VillagerId { Value = 1, FactionId = 0 });
            
            // Act
            UpdateSystem(ctx.BootstrapSystem);
            UpdateSystem(ctx.ArchetypeResolutionSystem);
            
            // Assert
            Assert.IsTrue(entityManager.HasComponent<EntityProfile>(entity), "EntityProfile should be created");
            var profile = entityManager.GetComponentData<EntityProfile>(entity);
            Assert.AreEqual("Default", profile.ArchetypeName.ToString(), "Archetype should be Default");
            Assert.IsTrue(entityManager.HasComponent<ProfileApplicationState>(entity), "ProfileApplicationState should be created");
        }

        [Test]
        public void Bootstrap_SkipsEntity_WithSkipEntityProfiling()
        {
            // Arrange
            using var ctx = CreateContext();
            var entityManager = ctx.World.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent(entity, new VillagerId { Value = 1, FactionId = 0 });
            entityManager.AddComponent<SkipEntityProfiling>(entity);
            
            // Act
            UpdateSystem(ctx.BootstrapSystem);
            
            // Assert
            Assert.IsFalse(entityManager.HasComponent<EntityProfile>(entity), "EntityProfile should NOT be created when SkipEntityProfiling is present");
        }

        [Test]
        public void Bootstrap_SkipsEntity_WithAllComponents()
        {
            // Arrange
            using var ctx = CreateContext();
            var entityManager = ctx.World.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent(entity, new VillagerId { Value = 1, FactionId = 0 });
            entityManager.AddComponent(entity, new CoreIndividualStats
            {
                Physique = 50f,
                Finesse = 50f,
                Will = 50f,
                Agility = 0f,
                Intellect = 0f,
                Social = 0f,
                Faith = 0f
            });
            entityManager.AddComponent(entity, new EntityAlignment { Moral = 0f, Order = 0f, Purity = 0f, Strength = 0.5f });
            entityManager.AddComponent(entity, new EntityOutlook { Primary = OutlookType.Pragmatic });
            entityManager.AddComponent(entity, new IdentityPersonalityAxes { VengefulForgiving = 0f, CravenBold = 0f });
            entityManager.AddComponent(entity, new DerivedAttributes { Strength = 50f, Agility = 50f, Intelligence = 50f, WisdomDerived = 50f });
            entityManager.AddComponent(entity, new SocialStats { Fame = 0f, Wealth = 0f, Reputation = 0f, Glory = 0f, Renown = 0f });
            
            // Act
            UpdateSystem(ctx.BootstrapSystem);
            
            // Assert
            Assert.IsFalse(entityManager.HasComponent<EntityProfile>(entity), "EntityProfile should NOT be created when all components exist");
        }

        [Test]
        public void Bootstrap_UpdatesCreatedTick_ForExistingEntityProfile()
        {
            // Arrange
            using var ctx = CreateContext();
            var entityManager = ctx.World.EntityManager;
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponent(entity, new VillagerId { Value = 1, FactionId = 0 });
            entityManager.AddComponent(entity, new EntityProfile
            {
                ArchetypeName = new FixedString64Bytes("Default"),
                Source = EntityProfileSource.Generated,
                CreatedTick = 0, // Stale tick
                IsResolved = 0
            });
            
            // Act
            UpdateSystem(ctx.BootstrapSystem);
            
            // Assert
            var profile = entityManager.GetComponentData<EntityProfile>(entity);
            Assert.Greater(profile.CreatedTick, 0u, "CreatedTick should be updated from 0");
        }

        [Test]
        public void CreateIndividual_WithExpertiseAndTraits_PopulatesBuffers()
        {
            // Arrange
            using var ctx = CreateContext();
            var entityManager = ctx.World.EntityManager;
            var entity = entityManager.CreateEntity();
            
            var profile = new IndividualProfileData
            {
                BasePhysique = 50f,
                BaseFinesse = 50f,
                BaseWill = 50f,
                Command = (half)75f,
                Tactics = (half)80f,
                Logistics = (half)70f,
                Diplomacy = (half)65f,
                Engineering = (half)85f,
                Resolve = (half)90f
            };
            
            FixedList128Bytes<FixedString32Bytes> initialExpertise = default;
            initialExpertise.Add(new FixedString32Bytes("CarrierCommand"));
            initialExpertise.Add(new FixedString32Bytes("Espionage"));
            
            FixedList128Bytes<FixedString32Bytes> initialTraits = default;
            initialTraits.Add(new FixedString32Bytes("ReactorWhisperer"));
            
            // Act
            EntityProfilingService.CreateIndividualWithOfficerStats(
                ref entityManager,
                entity,
                profile,
                default,
                0u,
                default,
                initialExpertise,
                initialTraits);
            
            // Run OfficerStatsApplicationSystem to populate buffers
            UpdateSystem(ctx.OfficerStatsSystem);
            
            // Assert
            Assert.IsTrue(entityManager.HasBuffer<ExpertiseEntry>(entity), "ExpertiseEntry buffer should exist");
            var expertiseBuffer = entityManager.GetBuffer<ExpertiseEntry>(entity);
            Assert.Greater(expertiseBuffer.Length, 0, "Expertise buffer should be populated");
            
            Assert.IsTrue(entityManager.HasBuffer<ServiceTrait>(entity), "ServiceTrait buffer should exist");
            var traitBuffer = entityManager.GetBuffer<ServiceTrait>(entity);
            Assert.Greater(traitBuffer.Length, 0, "ServiceTrait buffer should be populated");
        }
    }
}
#endif

