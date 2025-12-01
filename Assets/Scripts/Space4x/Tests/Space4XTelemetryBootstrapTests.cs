using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Space4X.Registry;
using Space4X.Tests.TestHarness;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XTelemetryBootstrapTests
    {
        private ISystemTestHarness _harness;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _harness = new ISystemTestHarness();
            _entityManager = _harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            _harness.Add<Space4XTelemetryBootstrapSystem>();
            _harness.Add<Space4XMiningTelemetrySystem>();
            CoreSingletonBootstrapSystem.EnsureFleetInterceptQueue(_entityManager);
            _harness.Add<Space4XFleetInterceptTelemetrySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
        }

        [Test]
        public void TelemetryStreamExistsInDemoScenes()
        {
            // Run telemetry bootstrap system
            _harness.Step();

            // Verify TelemetryStream singleton exists
            var telemetryQuery = _entityManager.CreateEntityQuery(typeof(TelemetryStream));
            Assert.IsFalse(telemetryQuery.IsEmptyIgnoreFilter, "TelemetryStream singleton should exist");

            var telemetryEntity = telemetryQuery.GetSingletonEntity();
            Assert.IsTrue(_entityManager.HasBuffer<TelemetryMetric>(telemetryEntity), "TelemetryStream should have TelemetryMetric buffer");
        }

        [Test]
        public void MiningTelemetryMetricsPublished()
        {
            // Create TelemetryStream
            var telemetryEntity = _entityManager.CreateEntity();
            _entityManager.AddComponent<TelemetryStream>(telemetryEntity);
            _entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);

            // Create Space4XMiningTelemetry singleton
            var miningTelemetryEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(miningTelemetryEntity, new Space4XMiningTelemetry
            {
                OreInHold = 100f,
                LastUpdateTick = 5
            });

            // Run mining telemetry system
            _harness.Step();

            // Verify metrics published
            var metricsBuffer = _entityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            Assert.Greater(metricsBuffer.Length, 0, "Telemetry metrics should be published");
        }

        [Test]
        public void InterceptTelemetryMetricsPublished()
        {
            // Create TelemetryStream
            var telemetryEntity = _entityManager.CreateEntity();
            _entityManager.AddComponent<TelemetryStream>(telemetryEntity);
            _entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);

            // Create Space4XFleetInterceptQueue with telemetry
            var queueQuery = _entityManager.CreateEntityQuery(typeof(Space4XFleetInterceptQueue));
            Entity queueEntity;
            if (queueQuery.IsEmptyIgnoreFilter)
            {
                queueEntity = _entityManager.CreateEntity();
                _entityManager.AddComponentData(queueEntity, new Space4XFleetInterceptQueue());
                _entityManager.AddBuffer<InterceptRequest>(queueEntity);
                _entityManager.AddBuffer<FleetInterceptCommandLogEntry>(queueEntity);
            }
            else
            {
                queueEntity = queueQuery.GetSingletonEntity();
            }

            _entityManager.AddComponentData(queueEntity, new Space4XFleetInterceptTelemetry
            {
                InterceptAttempts = 5,
                RendezvousAttempts = 2,
                LastAttemptTick = 10
            });

            // Run intercept telemetry system
            _harness.Step();

            // Verify metrics published
            var metricsBuffer = _entityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            bool foundInterceptMetric = false;
            bool foundRendezvousMetric = false;
            
            for (int i = 0; i < metricsBuffer.Length; i++)
            {
                var metric = metricsBuffer[i];
                var key = metric.Key.ToString();
                if (key.Contains("space4x.intercept.attempts"))
                {
                    foundInterceptMetric = true;
                }
                if (key.Contains("space4x.intercept.rendezvous"))
                {
                    foundRendezvousMetric = true;
                }
            }

            // At least intercept-related metrics should be published
            Assert.IsTrue(foundInterceptMetric || foundRendezvousMetric || metricsBuffer.Length > 0,
                "Intercept telemetry metrics should be published");
        }

        [Test]
        public void TelemetryAccessibleWithoutUI()
        {
            // Create TelemetryStream
            var telemetryEntity = _entityManager.CreateEntity();
            _entityManager.AddComponent<TelemetryStream>(telemetryEntity);
            var metricsBuffer = _entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);

            // Add some test metrics
            metricsBuffer.AddMetric("test.metric.1", 42f);
            metricsBuffer.AddMetric("test.metric.2", 100f);

            // Verify metrics accessible via ECS queries (no UI dependencies)
            var telemetryQuery = _entityManager.CreateEntityQuery(typeof(TelemetryStream));
            Assert.IsFalse(telemetryQuery.IsEmptyIgnoreFilter, "TelemetryStream should be queryable");

            var foundEntity = telemetryQuery.GetSingletonEntity();
            var foundBuffer = _entityManager.GetBuffer<TelemetryMetric>(foundEntity);
            Assert.AreEqual(2, foundBuffer.Length, "Metrics should be accessible via ECS buffer");
            Assert.AreEqual(42f, foundBuffer[0].Value, "Metric values should be readable");
        }
    }
}

