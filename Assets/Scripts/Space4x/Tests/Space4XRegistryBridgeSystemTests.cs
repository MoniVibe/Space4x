using System.Collections.Generic;
using Unity.Collections;
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry.Tests
{
    public class Space4XRegistryBridgeSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        private SystemHandle _bridgeHandle;
        private SystemHandle _telemetryHandle;
        private SystemHandle _directoryHandle;

        private Entity _telemetryEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XRegistryBridgeSystemTests");
            _entityManager = _world.EntityManager;

            EnsureTimeState();
            EnsureRegistryDirectory();
            EnsureTelemetryStream();

            _bridgeHandle = _world.GetOrCreateSystem<Space4XRegistryBridgeSystem>();
            _telemetryHandle = _world.GetOrCreateSystem<Space4XRegistryTelemetrySystem>();
            _directoryHandle = _world.GetOrCreateSystem<RegistryDirectorySystem>();
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
        public void BridgeRegistersColoniesAndFleetsAndEmitsTelemetry()
        {
            CreateColony("SOL-1", 150_000f, 800f, new float3(10f, 0f, 4f), Space4XColonyStatus.Growing);
            CreateFleet("FLEET-ALPHA", 6, Space4XFleetPosture.Engaging, new float3(-5f, 0f, 20f));

            UpdateSystem(_bridgeHandle);
            UpdateSystem(_directoryHandle);
            UpdateSystem(_telemetryHandle);

            var colonyRegistryEntity = _entityManager.CreateEntityQuery(typeof(Space4XColonyRegistry)).GetSingletonEntity();
            var colonyRegistry = _entityManager.GetComponentData<Space4XColonyRegistry>(colonyRegistryEntity);
            Assert.AreEqual(1, colonyRegistry.ColonyCount);
            Assert.Greater(colonyRegistry.TotalPopulation, 0f);
            Assert.AreEqual(42u, colonyRegistry.LastUpdateTick);

            var colonyBuffer = _entityManager.GetBuffer<Space4XColonyRegistryEntry>(colonyRegistryEntity);
            Assert.AreEqual(1, colonyBuffer.Length);
            Assert.AreEqual("SOL-1", colonyBuffer[0].ColonyId.ToString());

            var fleetRegistryEntity = _entityManager.CreateEntityQuery(typeof(Space4XFleetRegistry)).GetSingletonEntity();
            var fleetRegistry = _entityManager.GetComponentData<Space4XFleetRegistry>(fleetRegistryEntity);
            Assert.AreEqual(1, fleetRegistry.FleetCount);
            Assert.AreEqual(1, fleetRegistry.ActiveEngagementCount);
            Assert.AreEqual(42u, fleetRegistry.LastUpdateTick);

            var fleetBuffer = _entityManager.GetBuffer<Space4XFleetRegistryEntry>(fleetRegistryEntity);
            Assert.AreEqual(1, fleetBuffer.Length);
            Assert.AreEqual("FLEET-ALPHA", fleetBuffer[0].FleetId.ToString());

            var directoryEntity = _entityManager.CreateEntityQuery(typeof(RegistryDirectory)).GetSingletonEntity();
            var directoryEntries = _entityManager.GetBuffer<RegistryDirectoryEntry>(directoryEntity);
            Assert.IsTrue(directoryEntries.Length >= 2, "Expected custom registries to appear in the neutral directory");

            var snapshot = _entityManager.CreateEntityQuery(typeof(Space4XRegistrySnapshot)).GetSingleton<Space4XRegistrySnapshot>();
            Assert.AreEqual(1, snapshot.ColonyCount);
            Assert.AreEqual(1, snapshot.FleetCount);
            Assert.AreEqual(1, snapshot.FleetEngagementCount);
            Assert.AreEqual(42u, snapshot.LastRegistryTick);

            var telemetryBuffer = _entityManager.GetBuffer<TelemetryMetric>(_telemetryEntity);
            var telemetryKeys = new List<string>(telemetryBuffer.Length);
            for (int i = 0; i < telemetryBuffer.Length; i++)
            {
                telemetryKeys.Add(telemetryBuffer[i].Key.ToString());
            }

            Assert.Contains("space4x.registry.colonies", telemetryKeys);
            Assert.Contains("space4x.registry.fleets", telemetryKeys);
            Assert.Contains("space4x.registry.fleets.engaging", telemetryKeys);
        }

        private void EnsureTimeState()
        {
            var timeEntity = _entityManager.CreateEntity(typeof(TimeState));
            _entityManager.SetComponentData(timeEntity, new TimeState
            {
                Tick = 42,
                FixedDeltaTime = 1f / 60f,
                CurrentSpeedMultiplier = 1f,
                IsPaused = false
            });
        }

        private void EnsureRegistryDirectory()
        {
            var directoryEntity = _entityManager.CreateEntity(typeof(RegistryDirectory));
            _entityManager.AddBuffer<RegistryDirectoryEntry>(directoryEntity);
        }

        private void EnsureTelemetryStream()
        {
            _telemetryEntity = _entityManager.CreateEntity(typeof(TelemetryStream));
            _entityManager.AddBuffer<TelemetryMetric>(_telemetryEntity);
        }

        private void CreateColony(string id, float population, float storedResources, float3 position, Space4XColonyStatus status)
        {
            var entity = _entityManager.CreateEntity(typeof(Space4XColony), typeof(LocalTransform));
            _entityManager.SetComponentData(entity, new Space4XColony
            {
                ColonyId = new FixedString64Bytes(id),
                Population = population,
                StoredResources = storedResources,
                SectorId = 7,
                Status = status
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
        }

        private void CreateFleet(string id, int ships, Space4XFleetPosture posture, float3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(Space4XFleet), typeof(LocalTransform));
            _entityManager.SetComponentData(entity, new Space4XFleet
            {
                FleetId = new FixedString64Bytes(id),
                ShipCount = ships,
                Posture = posture,
                TaskForce = 101
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
        }

        private void UpdateSystem(SystemHandle handle)
        {
            handle.Update(_world.Unmanaged);
        }
    }
}

