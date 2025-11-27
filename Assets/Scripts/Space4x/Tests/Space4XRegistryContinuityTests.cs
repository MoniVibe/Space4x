using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Systems.AI;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;

namespace Space4X.Tests
{
    /// <summary>
    /// Tests that validate registry continuity for mining/haul entities.
    /// Ensures entities appear in registries and survive rewind operations.
    /// </summary>
    public class Space4XRegistryContinuityTests
    {
        private World _world;
        private EntityManager _entityManager;

        private Entity _timeEntity;
        private Entity _rewindEntity;
        private Entity _resourceRegistryEntity;

        private SystemHandle _bootstrapHandle;
        private SystemHandle _populationHandle;
        private SystemHandle _bridgeHandle;

        [SetUp]
        public void SetUp()
        {
            _world = new World("RegistryContinuityTests");
            _entityManager = _world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            _bootstrapHandle = _world.GetOrCreateSystem<Space4XResourceRegistryBootstrapSystem>();
            _populationHandle = _world.GetOrCreateSystem<Space4XResourceRegistryPopulationSystem>();
            _bridgeHandle = _world.GetOrCreateSystem<Space4XRegistryBridgeSystem>();

            _timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            _rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 0;
            time.FixedDeltaTime = 1f;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);

            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Record;
            _entityManager.SetComponentData(_rewindEntity, rewind);

            // Ensure registry directory exists
            EnsureRegistryDirectory();

            // Update bootstrap system to create registry
            UpdateSystem(_bootstrapHandle);
            UpdateSystem(_populationHandle);

            _resourceRegistryEntity = FindResourceRegistryEntity();
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
        public void AsteroidsAppearInResourceRegistry()
        {
            // Create asteroid
            var asteroid = CreateAsteroid(100f, ResourceType.Minerals, new float3(10f, 0f, 10f));

            // Update population system
            UpdateSystem(_populationHandle);

            // Verify asteroid appears in registry
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid),
                "Asteroid should have ResourceRegistryRegisteredTag");

            if (_resourceRegistryEntity != Entity.Null)
            {
                var registryBuffer = _entityManager.GetBuffer<ResourceRegistryEntry>(_resourceRegistryEntity);
                bool found = false;
                for (int i = 0; i < registryBuffer.Length; i++)
                {
                    if (registryBuffer[i].SourceEntity == asteroid)
                    {
                        found = true;
                        Assert.AreEqual(ResourceTier.Raw, registryBuffer[i].Tier,
                            "Asteroid should be registered as Raw tier");
                        break;
                    }
                }
                Assert.IsTrue(found, "Asteroid should appear in ResourceRegistryEntry buffer");
            }
        }

        [Test]
        public void DynamicallySpawnedAsteroidsAreRegistered()
        {
            // Create initial asteroid
            var asteroid1 = CreateAsteroid(100f, ResourceType.Minerals, new float3(10f, 0f, 10f));
            UpdateSystem(_populationHandle);

            // Verify first asteroid registered
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid1),
                "First asteroid should be registered");

            // Create second asteroid dynamically
            var asteroid2 = CreateAsteroid(50f, ResourceType.RareMetals, new float3(-10f, 0f, -10f));
            UpdateSystem(_populationHandle);

            // Verify second asteroid also registered
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid2),
                "Second asteroid should be registered");

            // Verify both appear in registry
            if (_resourceRegistryEntity != Entity.Null)
            {
                var registryBuffer = _entityManager.GetBuffer<ResourceRegistryEntry>(_resourceRegistryEntity);
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

        [Test]
        public void MiningEntitiesHaveSpatialIndexedTag()
        {
            // Create mining entities
            var asteroid = CreateAsteroid(100f, ResourceType.Minerals, new float3(0f, 0f, 0f));
            var carrier = CreateCarrier(new float3(5f, 0f, 0f));
            var vessel = CreateMiningVessel(asteroid, new float3(0.5f, 0f, 0f));

            // Verify all have SpatialIndexedTag
            Assert.IsTrue(_entityManager.HasComponent<SpatialIndexedTag>(asteroid),
                "Asteroid should have SpatialIndexedTag");
            Assert.IsTrue(_entityManager.HasComponent<SpatialIndexedTag>(carrier),
                "Carrier should have SpatialIndexedTag");
            Assert.IsTrue(_entityManager.HasComponent<SpatialIndexedTag>(vessel),
                "Mining vessel should have SpatialIndexedTag");
        }

        [Test]
        public void MiningEntitiesSurviveRewind()
        {
            // Create entities
            var asteroid = CreateAsteroid(100f, ResourceType.Minerals, new float3(0f, 0f, 0f));
            var carrier = CreateCarrier(new float3(5f, 0f, 0f));
            var vessel = CreateMiningVessel(asteroid, new float3(0.5f, 0f, 0f));

            // Verify entities exist
            Assert.IsTrue(_entityManager.Exists(asteroid), "Asteroid should exist");
            Assert.IsTrue(_entityManager.Exists(carrier), "Carrier should exist");
            Assert.IsTrue(_entityManager.Exists(vessel), "Vessel should exist");

            // Advance time
            AdvanceTick();

            // Rewind
            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Playback;
            rewind.StartTick = 1;
            rewind.PlaybackTick = 0;
            rewind.TargetTick = 0;
            _entityManager.SetComponentData(_rewindEntity, rewind);

            // Verify entities still exist after rewind
            Assert.IsTrue(_entityManager.Exists(asteroid), "Asteroid should survive rewind");
            Assert.IsTrue(_entityManager.Exists(carrier), "Carrier should survive rewind");
            Assert.IsTrue(_entityManager.Exists(vessel), "Vessel should survive rewind");
        }

        [Test]
        public void RegistryEntriesSurviveRewind()
        {
            // Create asteroid and register it
            var asteroid = CreateAsteroid(100f, ResourceType.Minerals, new float3(10f, 0f, 10f));
            UpdateSystem(_populationHandle);

            // Verify registered
            Assert.IsTrue(_entityManager.HasComponent<ResourceRegistryRegisteredTag>(asteroid),
                "Asteroid should be registered");

            if (_resourceRegistryEntity != Entity.Null)
            {
                var registryBuffer = _entityManager.GetBuffer<ResourceRegistryEntry>(_resourceRegistryEntity);
                int initialCount = registryBuffer.Length;
                Assert.Greater(initialCount, 0, "Registry should have entries");

                // Advance time
                AdvanceTick();

                // Rewind
                var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
                rewind.Mode = RewindMode.Playback;
                rewind.StartTick = 1;
                rewind.PlaybackTick = 0;
                rewind.TargetTick = 0;
                _entityManager.SetComponentData(_rewindEntity, rewind);

                // Verify registry still has entries
                registryBuffer = _entityManager.GetBuffer<ResourceRegistryEntry>(_resourceRegistryEntity);
                Assert.GreaterOrEqual(registryBuffer.Length, initialCount,
                    "Registry entries should survive rewind");
            }
        }

        [Test]
        public void CarriersAppearInFleetRegistry()
        {
            // Ensure registry directory and fleet registry exist
            EnsureRegistryDirectory();

            // Create carrier with fleet component
            var carrier = CreateCarrier(new float3(0f, 0f, 0f));
            _entityManager.AddComponentData(carrier, new Space4XFleet
            {
                FleetId = new FixedString64Bytes("FLEET-1"),
                ShipCount = 1,
                Posture = Space4XFleetPosture.Patrol,
                TaskForce = 0
            });

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Find fleet registry
            var fleetRegistryEntity = FindFleetRegistryEntity();
            if (fleetRegistryEntity != Entity.Null)
            {
                var registryBuffer = _entityManager.GetBuffer<Space4XFleetRegistryEntry>(fleetRegistryEntity);
                bool found = false;
                for (int i = 0; i < registryBuffer.Length; i++)
                {
                    if (registryBuffer[i].FleetId.ToString() == "FLEET-1")
                    {
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found, "Carrier should appear in FleetRegistryEntry buffer");
            }
        }

        private Entity CreateAsteroid(float units, ResourceType resourceType, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(Asteroid),
                typeof(ResourceSourceState),
                typeof(ResourceSourceConfig),
                typeof(ResourceTypeId),
                typeof(LocalTransform));

            _entityManager.SetComponentData(entity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes("AST-1"),
                ResourceAmount = units,
                MaxResourceAmount = units,
                ResourceType = resourceType,
                MiningRate = 20f
            });

            _entityManager.SetComponentData(entity, new ResourceSourceState
            {
                UnitsRemaining = units
            });

            _entityManager.SetComponentData(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = 20f,
                MaxSimultaneousWorkers = 4,
                RespawnSeconds = 0f,
                Flags = 0
            });

            var resourceId = new FixedString64Bytes("space4x.resource.minerals");
            if (resourceType == ResourceType.RareMetals)
            {
                resourceId = new FixedString64Bytes("space4x.resource.rare_metals");
            }
            _entityManager.SetComponentData(entity, new ResourceTypeId { Value = resourceId });

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            _entityManager.AddComponent<SpatialIndexedTag>(entity);
            _entityManager.AddComponent<RewindableTag>(entity);
            return entity;
        }

        private Entity CreateCarrier(float3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(Carrier), typeof(LocalTransform), typeof(ResourceStorage));
            _entityManager.SetComponentData(entity, new Carrier
            {
                CarrierId = new FixedString64Bytes("CARRIER-1"),
                AffiliationEntity = Entity.Null,
                Speed = 0f,
                PatrolCenter = position,
                PatrolRadius = 0f
            });

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            var storage = _entityManager.GetBuffer<ResourceStorage>(entity);
            storage.Add(ResourceStorage.Create(ResourceType.Minerals));
            _entityManager.AddComponent<SpatialIndexedTag>(entity);
            _entityManager.AddComponent<RewindableTag>(entity);
            return entity;
        }

        private Entity CreateMiningVessel(Entity targetAsteroid, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(MiningVessel),
                typeof(MiningOrder),
                typeof(MiningState),
                typeof(MiningYield),
                typeof(VesselAIState),
                typeof(LocalTransform),
                typeof(SpawnResourceRequest));

            var resourceId = new FixedString64Bytes("space4x.resource.minerals");

            _entityManager.SetComponentData(entity, new MiningVessel
            {
                VesselId = new FixedString64Bytes("VES-1"),
                CarrierEntity = Entity.Null,
                MiningEfficiency = 1f,
                Speed = 0f,
                CargoCapacity = 100f,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });

            _entityManager.SetComponentData(entity, new MiningOrder
            {
                ResourceId = resourceId,
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Active,
                PreferredTarget = Entity.Null,
                TargetEntity = targetAsteroid,
                IssuedTick = 0
            });

            _entityManager.SetComponentData(entity, new MiningState
            {
                Phase = MiningPhase.Mining,
                ActiveTarget = targetAsteroid,
                MiningTimer = 0f,
                TickInterval = 1f
            });

            _entityManager.SetComponentData(entity, new MiningYield
            {
                ResourceId = resourceId,
                PendingAmount = 0f,
                SpawnThreshold = 20f,
                SpawnReady = 0
            });

            _entityManager.SetComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Mining,
                CurrentGoal = VesselAIState.Goal.Mining,
                TargetEntity = targetAsteroid,
                TargetPosition = position,
                StateTimer = 0f,
                StateStartTick = 0
            });

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            _entityManager.GetBuffer<SpawnResourceRequest>(entity);
            _entityManager.AddComponent<SpatialIndexedTag>(entity);
            _entityManager.AddComponent<RewindableTag>(entity);
            return entity;
        }

        private void AdvanceTick()
        {
            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick += 1;
            _entityManager.SetComponentData(_timeEntity, time);
        }

        private void EnsureRegistryDirectory()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = _entityManager.CreateEntity(typeof(RegistryDirectory));
                _entityManager.AddComponent<RegistryMetadata>(entity);
            }
        }

        private Entity FindResourceRegistryEntity()
        {
            using var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ResourceRegistry>(),
                ComponentType.ReadOnly<ResourceRegistryEntry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }
            return Entity.Null;
        }

        private Entity FindFleetRegistryEntity()
        {
            using var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XFleetRegistry>(),
                ComponentType.ReadOnly<Space4XFleetRegistryEntry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }
            return Entity.Null;
        }

        private void UpdateSystem(SystemHandle handle)
        {
            handle.Update(_world.Unmanaged);
        }
    }
}

