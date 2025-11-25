using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XTelemetryBootstrapTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XTelemetryBootstrapTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
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
        public void TelemetryStreamExistsInDemoScenes()
        {
            // Run telemetry bootstrap system
            var bootstrapSystem = _world.GetOrCreateSystemManaged<Space4XTelemetryBootstrapSystem>();
            bootstrapSystem.Update(_world.Unmanaged);

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
            var telemetrySystem = _world.GetOrCreateSystemManaged<Space4XMiningTelemetrySystem>();
            telemetrySystem.Update(_world.Unmanaged);

            // Verify metrics published
            var metricsBuffer = _entityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            Assert.Greater(metricsBuffer.Length, 0, "Telemetry metrics should be published");
        }
    }
}

