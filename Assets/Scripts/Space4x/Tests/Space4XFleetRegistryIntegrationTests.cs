#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Space4X.Registry;
using Space4X.Tests.TestHarness;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    public class Space4XFleetRegistryIntegrationTests
    {
        private ISystemTestHarness _harness;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _harness = new ISystemTestHarness();
            _entityManager = _harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            _harness.Add<Space4XRegistryBridgeSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
        }

        private Entity CreateCarrierWithFleet(float3 position, string fleetId, Space4XFleetPosture posture)
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.AddComponentData(entity, new Carrier
            {
                CarrierId = new FixedString64Bytes("CARRIER-1"),
                AffiliationEntity = Entity.Null,
                Speed = 2f,
                PatrolCenter = position,
                PatrolRadius = 50f
            });
            _entityManager.AddComponentData(entity, new Space4XFleet
            {
                FleetId = new FixedString64Bytes(fleetId),
                ShipCount = 1,
                Posture = posture,
                TaskForce = 0
            });
            _entityManager.AddComponent<SpatialIndexedTag>(entity);
            return entity;
        }

        [Test]
        public void CarriersAppearInFleetRegistry()
        {
            var carrier = CreateCarrierWithFleet(new float3(0f, 0f, 0f), "FLEET-1", Space4XFleetPosture.Patrol);

            // Run registry bridge system
            _harness.Step();

            // Query for fleet registry
            var fleetRegistryQuery = _entityManager.CreateEntityQuery(typeof(Space4XFleetRegistry));
            if (!fleetRegistryQuery.IsEmptyIgnoreFilter)
            {
                var fleetRegistry = fleetRegistryQuery.GetSingleton<Space4XFleetRegistry>();
                Assert.GreaterOrEqual(fleetRegistry.FleetCount, 1, "Fleet registry should contain at least one fleet");

                var entriesBuffer = _entityManager.GetBuffer<Space4XFleetRegistryEntry>(fleetRegistryQuery.GetSingletonEntity());
                bool found = false;
                for (int i = 0; i < entriesBuffer.Length; i++)
                {
                    if (entriesBuffer[i].FleetEntity == carrier)
                    {
                        found = true;
                        Assert.AreEqual("FLEET-1", entriesBuffer[i].FleetId.ToString());
                        Assert.AreEqual(Space4XFleetPosture.Patrol, entriesBuffer[i].Posture);
                        break;
                    }
                }
                Assert.IsTrue(found, "Carrier should appear in fleet registry entries");
            }
        }

        [Test]
        public void CarriersRegisteredInFleetRegistryAfterAddingFleetComponent()
        {
            // Create carrier without fleet component
            var carrier = _entityManager.CreateEntity();
            _entityManager.AddComponentData(carrier, new LocalTransform
            {
                Position = new float3(0f, 0f, 0f),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.AddComponentData(carrier, new Carrier
            {
                CarrierId = new FixedString64Bytes("CARRIER-1"),
                AffiliationEntity = Entity.Null,
                Speed = 2f,
                PatrolCenter = float3.zero,
                PatrolRadius = 50f
            });
            _entityManager.AddComponent<SpatialIndexedTag>(carrier);

            // Run registry bridge - should not see carrier
            _harness.Step();

            var fleetRegistryQuery = _entityManager.CreateEntityQuery(typeof(Space4XFleetRegistry));
            int fleetCountBefore = 0;
            if (!fleetRegistryQuery.IsEmptyIgnoreFilter)
            {
                fleetCountBefore = fleetRegistryQuery.GetSingleton<Space4XFleetRegistry>().FleetCount;
            }

            // Add fleet component
            _entityManager.AddComponentData(carrier, new Space4XFleet
            {
                FleetId = new FixedString64Bytes("FLEET-1"),
                ShipCount = 1,
                Posture = Space4XFleetPosture.Patrol,
                TaskForce = 0
            });

            // Run registry bridge again - should now see carrier
            _harness.Step();

            if (!fleetRegistryQuery.IsEmptyIgnoreFilter)
            {
                var fleetCountAfter = fleetRegistryQuery.GetSingleton<Space4XFleetRegistry>().FleetCount;
                Assert.Greater(fleetCountAfter, fleetCountBefore, "Fleet count should increase after adding Space4XFleet component");
            }
        }
    }
}
#endif
