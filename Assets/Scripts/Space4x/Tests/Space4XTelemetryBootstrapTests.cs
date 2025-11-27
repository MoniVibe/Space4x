using NUnit.Framework;
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
    }
}

