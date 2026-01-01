#if SPACE4X_MIRACLES_WIP && (UNITY_EDITOR || UNITY_INCLUDE_TESTS)
// TODO: Update these tests to the new miracle API in PureDOTS.Runtime.Miracles and re-enable SPACE4X_MIRACLES_WIP.
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4x.Miracles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    /// <summary>
    /// Integration tests for fleet spawning and registry registration.
    /// </summary>
    public class Space4XFleetSpawningTests
    {
        private World _world;
        private EntityManager _entityManager;

        private Entity _timeEntity;
        private SystemHandle _bridgeHandle;

        [SetUp]
        public void SetUp()
        {
            _world = new World("FleetSpawningTests");
            _entityManager = _world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            EnsureRegistryDirectory();

            _bridgeHandle = _world.GetOrCreateSystem<Space4XRegistryBridgeSystem>();

            _timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 0;
            time.FixedDeltaTime = 1f;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);
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
        public void FleetSpawned_AppearsInFleetRegistry()
        {
            // Create fleet
            var fleet = CreateFleet("FLEET-ALPHA", 5, Space4XFleetPosture.Patrol, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify fleet appears in registry
            var fleetRegistryEntity = FindFleetRegistryEntity();
            Assert.IsFalse(fleetRegistryEntity == Entity.Null, "Fleet registry should exist");

            var registryBuffer = _entityManager.GetBuffer<Space4XFleetRegistryEntry>(fleetRegistryEntity);
            bool found = false;
            for (int i = 0; i < registryBuffer.Length; i++)
            {
                if (registryBuffer[i].FleetId.ToString() == "FLEET-ALPHA")
                {
                    found = true;
                    Assert.AreEqual(5, registryBuffer[i].ShipCount, "Fleet ship count should match");
                    Assert.AreEqual(Space4XFleetPosture.Patrol, registryBuffer[i].Posture, "Fleet posture should match");
                    break;
                }
            }
            Assert.IsTrue(found, "Fleet should appear in FleetRegistryEntry buffer");
        }

        [Test]
        public void MultipleFleets_AllAppearInRegistry()
        {
            // Create multiple fleets
            var fleet1 = CreateFleet("FLEET-ALPHA", 5, Space4XFleetPosture.Patrol, new float3(0f, 0f, 0f));
            var fleet2 = CreateFleet("FLEET-BETA", 3, Space4XFleetPosture.Engaging, new float3(10f, 0f, 10f));
            var fleet3 = CreateFleet("FLEET-GAMMA", 7, Space4XFleetPosture.Idle, new float3(-10f, 0f, -10f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify all fleets appear
            var fleetRegistryEntity = FindFleetRegistryEntity();
            var registryBuffer = _entityManager.GetBuffer<Space4XFleetRegistryEntry>(fleetRegistryEntity);

            Assert.AreEqual(3, registryBuffer.Length, "All three fleets should appear in registry");

            // Verify registry summary
            var summary = _entityManager.GetComponentData<Space4XFleetRegistry>(fleetRegistryEntity);
            Assert.AreEqual(3, summary.FleetCount, "Fleet count should be 3");
            Assert.AreEqual(15, summary.TotalShips, "Total ships should be 15");
        }

        [Test]
        public void FleetPosture_ReflectedInRegistryFlags()
        {
            // Create fleet with Engaging posture
            var fleet = CreateFleet("FLEET-ALPHA", 5, Space4XFleetPosture.Engaging, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify flags
            var fleetRegistryEntity = FindFleetRegistryEntity();
            var registryBuffer = _entityManager.GetBuffer<Space4XFleetRegistryEntry>(fleetRegistryEntity);

            bool found = false;
            for (int i = 0; i < registryBuffer.Length; i++)
            {
                if (registryBuffer[i].FleetId.ToString() == "FLEET-ALPHA")
                {
                    found = true;
                    var flags = registryBuffer[i].Flags;
                    Assert.IsTrue((flags & Space4XRegistryFlags.FleetActive) != 0, "Fleet should have Active flag");
                    Assert.IsTrue((flags & Space4XRegistryFlags.FleetEngaging) != 0, "Fleet should have Engaging flag");
                    break;
                }
            }
            Assert.IsTrue(found, "Fleet should be found in registry");
        }

        [Test]
        public void FleetRegistrySnapshot_Updated()
        {
            // Create fleet
            var fleet = CreateFleet("FLEET-ALPHA", 5, Space4XFleetPosture.Patrol, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify snapshot updated
            var snapshotEntity = FindSnapshotEntity();
            if (snapshotEntity != Entity.Null)
            {
                var snapshot = _entityManager.GetComponentData<Space4XRegistrySnapshot>(snapshotEntity);
                Assert.GreaterOrEqual(snapshot.FleetCount, 1, "Snapshot should have fleet count");
            }
        }

        private Entity CreateFleet(string fleetId, int shipCount, Space4XFleetPosture posture, float3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(Space4XFleet), typeof(LocalTransform), typeof(SpatialIndexedTag));
            _entityManager.SetComponentData(entity, new Space4XFleet
            {
                FleetId = new FixedString64Bytes(fleetId),
                ShipCount = shipCount,
                Posture = posture,
                TaskForce = 0
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private void EnsureRegistryDirectory()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = _entityManager.CreateEntity(typeof(RegistryDirectory));
                _entityManager.AddComponent<RegistryMetadata>(entity);
            }

            // Ensure MiracleRegistry exists (required by bridge system)
            using var miracleQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PureDOTS.Runtime.Registry.MiracleRegistry>());
            if (miracleQuery.IsEmptyIgnoreFilter)
            {
                var entity = _entityManager.CreateEntity(typeof(PureDOTS.Runtime.Registry.MiracleRegistry));
                _entityManager.SetComponentData(entity, new PureDOTS.Runtime.Registry.MiracleRegistry
                {
                    TotalMiracles = 0,
                    ActiveMiracles = 0,
                    TotalEnergyCost = 0f,
                    TotalCooldownSeconds = 0f
                });
            }
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

        private Entity FindSnapshotEntity()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XRegistrySnapshot>());
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
#endif
