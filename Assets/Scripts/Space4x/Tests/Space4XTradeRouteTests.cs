using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    /// <summary>
    /// Integration tests for trade/logistics routes and throughput calculation.
    /// </summary>
    public class Space4XTradeRouteTests
    {
        private World _world;
        private EntityManager _entityManager;

        private Entity _timeEntity;
        private SystemHandle _bridgeHandle;

        [SetUp]
        public void SetUp()
        {
            _world = new World("TradeRouteTests");
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
        public void LogisticsRouteCreated_AppearsInRegistry()
        {
            // Create route
            var route = CreateLogisticsRoute("ROUTE-1", "COLONY-A", "COLONY-B", 180f, 0.15f, Space4XLogisticsRouteStatus.Operational, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify route appears in registry
            var logisticsRegistryEntity = FindLogisticsRegistryEntity();
            Assert.IsFalse(logisticsRegistryEntity == Entity.Null, "Logistics registry should exist");

            var registryBuffer = _entityManager.GetBuffer<Space4XLogisticsRegistryEntry>(logisticsRegistryEntity);
            bool found = false;
            for (int i = 0; i < registryBuffer.Length; i++)
            {
                if (registryBuffer[i].RouteId.ToString() == "ROUTE-1")
                {
                    found = true;
                    Assert.AreEqual(180f, registryBuffer[i].DailyThroughput, 0.01f, "Throughput should match");
                    Assert.AreEqual(0.15f, registryBuffer[i].Risk, 0.01f, "Risk should match");
                    Assert.AreEqual(Space4XLogisticsRouteStatus.Operational, registryBuffer[i].Status, "Status should match");
                    break;
                }
            }
            Assert.IsTrue(found, "Route should appear in LogisticsRegistryEntry buffer");
        }

        [Test]
        public void MultipleRoutes_AllAppearInRegistry()
        {
            // Create multiple routes
            var route1 = CreateLogisticsRoute("ROUTE-1", "COLONY-A", "COLONY-B", 180f, 0.15f, Space4XLogisticsRouteStatus.Operational, new float3(0f, 0f, 0f));
            var route2 = CreateLogisticsRoute("ROUTE-2", "COLONY-B", "COLONY-C", 200f, 0.25f, Space4XLogisticsRouteStatus.Operational, new float3(10f, 0f, 10f));
            var route3 = CreateLogisticsRoute("ROUTE-3", "COLONY-C", "COLONY-A", 150f, 0.10f, Space4XLogisticsRouteStatus.Disrupted, new float3(-10f, 0f, -10f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify all routes appear
            var logisticsRegistryEntity = FindLogisticsRegistryEntity();
            var registryBuffer = _entityManager.GetBuffer<Space4XLogisticsRegistryEntry>(logisticsRegistryEntity);

            Assert.AreEqual(3, registryBuffer.Length, "All three routes should appear in registry");

            // Verify registry summary
            var summary = _entityManager.GetComponentData<Space4XLogisticsRegistry>(logisticsRegistryEntity);
            Assert.AreEqual(3, summary.RouteCount, "Route count should be 3");
            Assert.AreEqual(2, summary.ActiveRouteCount, "Active route count should be 2");
            Assert.AreEqual(530f, summary.TotalDailyThroughput, 0.01f, "Total throughput should be sum");
        }

        [Test]
        public void RouteStatus_ReflectedInRegistry()
        {
            // Create route with Disrupted status
            var route = CreateLogisticsRoute("ROUTE-1", "COLONY-A", "COLONY-B", 180f, 0.15f, Space4XLogisticsRouteStatus.Disrupted, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify status
            var logisticsRegistryEntity = FindLogisticsRegistryEntity();
            var registryBuffer = _entityManager.GetBuffer<Space4XLogisticsRegistryEntry>(logisticsRegistryEntity);

            bool found = false;
            for (int i = 0; i < registryBuffer.Length; i++)
            {
                if (registryBuffer[i].RouteId.ToString() == "ROUTE-1")
                {
                    found = true;
                    Assert.AreEqual(Space4XLogisticsRouteStatus.Disrupted, registryBuffer[i].Status, "Status should be Disrupted");
                    break;
                }
            }
            Assert.IsTrue(found, "Route should be found in registry");
        }

        [Test]
        public void RouteRisk_CalculatedInRegistry()
        {
            // Create routes with different risk levels
            var lowRiskRoute = CreateLogisticsRoute("ROUTE-LOW", "COLONY-A", "COLONY-B", 180f, 0.1f, Space4XLogisticsRouteStatus.Operational, new float3(0f, 0f, 0f));
            var highRiskRoute = CreateLogisticsRoute("ROUTE-HIGH", "COLONY-B", "COLONY-C", 200f, 0.8f, Space4XLogisticsRouteStatus.Operational, new float3(10f, 0f, 10f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify risk calculation
            var logisticsRegistryEntity = FindLogisticsRegistryEntity();
            var summary = _entityManager.GetComponentData<Space4XLogisticsRegistry>(logisticsRegistryEntity);

            float expectedAverageRisk = (0.1f + 0.8f) / 2f;
            Assert.AreEqual(expectedAverageRisk, summary.AverageRisk, 0.01f, "Average risk should be calculated correctly");
        }

        [Test]
        public void LogisticsRegistrySnapshot_Updated()
        {
            // Create route
            var route = CreateLogisticsRoute("ROUTE-1", "COLONY-A", "COLONY-B", 180f, 0.15f, Space4XLogisticsRouteStatus.Operational, new float3(0f, 0f, 0f));

            // Update bridge system
            UpdateSystem(_bridgeHandle);

            // Verify snapshot updated
            var snapshotEntity = FindSnapshotEntity();
            if (snapshotEntity != Entity.Null)
            {
                var snapshot = _entityManager.GetComponentData<Space4XRegistrySnapshot>(snapshotEntity);
                Assert.GreaterOrEqual(snapshot.LogisticsRouteCount, 1, "Snapshot should have route count");
                Assert.GreaterOrEqual(snapshot.ActiveLogisticsRouteCount, 1, "Snapshot should have active route count");
            }
        }

        private Entity CreateLogisticsRoute(string routeId, string originColonyId, string destinationColonyId, float dailyThroughput, float risk, Space4XLogisticsRouteStatus status, float3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(Space4XLogisticsRoute), typeof(LocalTransform), typeof(SpatialIndexedTag));
            _entityManager.SetComponentData(entity, new Space4XLogisticsRoute
            {
                RouteId = new FixedString64Bytes(routeId),
                OriginColonyId = new FixedString64Bytes(originColonyId),
                DestinationColonyId = new FixedString64Bytes(destinationColonyId),
                DailyThroughput = dailyThroughput,
                Risk = risk,
                Priority = 1,
                Status = status
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

            // Ensure MiracleRegistry exists
            using var miracleQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<MiracleRegistry>());
            if (miracleQuery.IsEmptyIgnoreFilter)
            {
                var entity = _entityManager.CreateEntity(typeof(MiracleRegistry));
                _entityManager.SetComponentData(entity, new MiracleRegistry
                {
                    TotalMiracles = 0,
                    ActiveMiracles = 0,
                    TotalEnergyCost = 0f,
                    TotalCooldownSeconds = 0f
                });
            }
        }

        private Entity FindLogisticsRegistryEntity()
        {
            using var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XLogisticsRegistry>(),
                ComponentType.ReadOnly<Space4XLogisticsRegistryEntry>());
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

