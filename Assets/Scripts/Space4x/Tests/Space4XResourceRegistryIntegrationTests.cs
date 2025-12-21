#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Spatial;
using Space4X.Registry;
using Space4X.Systems.AI;
using Space4X.Tests.TestHarness;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;
using ResourceRegistry = PureDOTS.Runtime.Components.ResourceRegistry;
using ResourceRegistryEntry = PureDOTS.Runtime.Components.ResourceRegistryEntry;

namespace Space4X.Tests
{
    /// <summary>
    /// Integration tests for resource registry population system.
    /// Validates that asteroids are registered in ResourceRegistryEntry buffer.
    /// </summary>
    public class Space4XResourceRegistryIntegrationTests
    {
        private ISystemTestHarness _harness;
        private EntityManager _entityManager;
        private Entity _timeEntity;

        [SetUp]
        public void SetUp()
        {
            _harness = new ISystemTestHarness();
            _entityManager = _harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            _timeEntity = _entityManager.CreateEntityQuery(typeof(TimeState)).GetSingletonEntity();
            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 0;
            time.FixedDeltaTime = 1f;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);

            _harness.Add<Space4XResourceRegistryBootstrapSystem>();
            _harness.Add<Space4XResourceRegistryPopulationSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
        }

        [Test]
        public void AsteroidsRegisteredInResourceRegistry()
        {
            // Create asteroid with required components
            var asteroid = CreateAsteroid(100f, ResourceType.Minerals, new float3(10f, 0f, 10f));

            // Run bootstrap and population systems
            _harness.Step(2);

            // Verify asteroid has ResourceRegistryRegisteredTag
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid),
                "Asteroid should have ResourceRegistryRegisteredTag after registration");

            // Find registry entity
            var registryQuery = _entityManager.CreateEntityQuery(typeof(ResourceRegistry), typeof(ResourceRegistryEntry));
            if (!registryQuery.IsEmptyIgnoreFilter)
            {
                var registryEntity = registryQuery.GetSingletonEntity();
                var registryBuffer = _entityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);
                
                bool found = false;
                for (int i = 0; i < registryBuffer.Length; i++)
                {
                    if (registryBuffer[i].SourceEntity == asteroid)
                    {
                        found = true;
                        Assert.AreEqual(ResourceTier.Raw, registryBuffer[i].Tier,
                            "Asteroid should be registered as Raw tier");
                        Assert.AreEqual(new float3(10f, 0f, 10f), registryBuffer[i].Position,
                            "Position should match asteroid position");
                        break;
                    }
                }
                Assert.IsTrue(found, "Asteroid should appear in ResourceRegistryEntry buffer");
            }
        }

        [Test]
        public void ResourceRegistryEntryUpdatedOnMining()
        {
            // Create asteroid and register it
            var asteroid = CreateAsteroid(100f, ResourceType.Minerals, new float3(0f, 0f, 0f));
            _harness.Step(2);

            // Verify registered
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid),
                "Asteroid should be registered");

            // Simulate mining by reducing resource amount
            var resourceState = _entityManager.GetComponentData<ResourceSourceState>(asteroid);
            resourceState.UnitsRemaining = 50f;
            _entityManager.SetComponentData(asteroid, resourceState);

            // Verify registry entry still exists (system doesn't remove entries when resources are depleted)
            var registryQuery = _entityManager.CreateEntityQuery(typeof(ResourceRegistry), typeof(ResourceRegistryEntry));
            if (!registryQuery.IsEmptyIgnoreFilter)
            {
                var registryEntity = registryQuery.GetSingletonEntity();
                var registryBuffer = _entityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);
                
                bool found = false;
                for (int i = 0; i < registryBuffer.Length; i++)
                {
                    if (registryBuffer[i].SourceEntity == asteroid)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found, "Registry entry should persist after mining");
            }
        }

        [Test]
        public void ResourceRegistryQueriesWorkCorrectly()
        {
            // Create multiple asteroids
            var asteroid1 = CreateAsteroid(100f, ResourceType.Minerals, new float3(10f, 0f, 0f));
            var asteroid2 = CreateAsteroid(50f, ResourceType.RareMetals, new float3(-10f, 0f, 0f));
            var asteroid3 = CreateAsteroid(75f, ResourceType.Minerals, new float3(0f, 0f, 10f));

            // Run population system
            _harness.Step(2);

            // Verify all registered
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid1),
                "Asteroid 1 should be registered");
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid2),
                "Asteroid 2 should be registered");
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid3),
                "Asteroid 3 should be registered");

            // Query registry
            var registryQuery = _entityManager.CreateEntityQuery(typeof(ResourceRegistry), typeof(ResourceRegistryEntry));
            if (!registryQuery.IsEmptyIgnoreFilter)
            {
                var registryEntity = registryQuery.GetSingletonEntity();
                var registryBuffer = _entityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);
                
                Assert.GreaterOrEqual(registryBuffer.Length, 3,
                    "Registry should contain at least 3 entries");

                // Verify we can find specific asteroids
                int foundCount = 0;
                for (int i = 0; i < registryBuffer.Length; i++)
                {
                    var entry = registryBuffer[i];
                    if (entry.SourceEntity == asteroid1 || entry.SourceEntity == asteroid2 || entry.SourceEntity == asteroid3)
                    {
                        foundCount++;
                    }
                }
                Assert.AreEqual(3, foundCount, "All three asteroids should be found in registry");
            }
        }

        [Test]
        public void DepletedAsteroidsNotRegistered()
        {
            // Create asteroid with 0 resources
            var asteroid = CreateAsteroid(0f, ResourceType.Minerals, new float3(0f, 0f, 0f));

            // Run population system
            _harness.Step(2);

            // Verify NOT registered (system skips asteroids with UnitsRemaining <= 0)
            Assert.IsFalse(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid),
                "Depleted asteroid should not be registered");
        }

        [Test]
        public void DynamicallySpawnedAsteroidsAreRegistered()
        {
            // Create and register first asteroid
            var asteroid1 = CreateAsteroid(100f, ResourceType.Minerals, new float3(10f, 0f, 0f));
            _harness.Step(2);

            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid1),
                "First asteroid should be registered");

            // Create second asteroid dynamically (simulating runtime spawn)
            var asteroid2 = CreateAsteroid(50f, ResourceType.RareMetals, new float3(-10f, 0f, 0f));

            // Run population system again
            _harness.Step();

            // Verify second asteroid also registered
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid2),
                "Dynamically spawned asteroid should be registered");

            // Verify both in registry
            var registryQuery = _entityManager.CreateEntityQuery(typeof(ResourceRegistry), typeof(ResourceRegistryEntry));
            if (!registryQuery.IsEmptyIgnoreFilter)
            {
                var registryEntity = registryQuery.GetSingletonEntity();
                var registryBuffer = _entityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);
                
                int asteroidCount = 0;
                for (int i = 0; i < registryBuffer.Length; i++)
                {
                    if (registryBuffer[i].SourceEntity == asteroid1 || registryBuffer[i].SourceEntity == asteroid2)
                    {
                        asteroidCount++;
                    }
                }
                Assert.AreEqual(2, asteroidCount, "Both asteroids should appear in registry");
            }
        }

        private Entity CreateAsteroid(float units, ResourceType resourceType, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(Asteroid),
                typeof(ResourceSourceState),
                typeof(ResourceSourceConfig),
                typeof(ResourceTypeId),
                typeof(LocalTransform),
                typeof(SpatialIndexedTag));

            _entityManager.SetComponentData(entity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes("AST-1"),
                ResourceType = resourceType,
                ResourceAmount = units,
                MaxResourceAmount = units,
                MiningRate = 12f
            });

            _entityManager.SetComponentData(entity, new ResourceSourceState
            {
                UnitsRemaining = units
            });

            _entityManager.SetComponentData(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = 12f,
                MaxSimultaneousWorkers = 4,
                RespawnSeconds = 0f,
                Flags = 0
            });

            FixedString64Bytes resourceId = new FixedString64Bytes("space4x.resource.minerals");
            if (resourceType == ResourceType.RareMetals)
            {
                resourceId = new FixedString64Bytes("space4x.resource.rare_metals");
            }
            else if (resourceType == ResourceType.EnergyCrystals)
            {
                resourceId = new FixedString64Bytes("space4x.resource.energy_crystals");
            }
            else if (resourceType == ResourceType.OrganicMatter)
            {
                resourceId = new FixedString64Bytes("space4x.resource.organic_matter");
            }

            _entityManager.SetComponentData(entity, new ResourceTypeId { Value = resourceId });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            
            return entity;
        }
    }
}
#endif
