#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Telemetry;
using Space4X.Registry;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XHazardMitigationSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HazardMitigationSystemTests");
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
        public void HazardResistanceReducesDamageAndPublishesTelemetry()
        {
            var telemetryEntity = _entityManager.CreateEntity(typeof(TelemetryStream));
            _entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);

            var entity = _entityManager.CreateEntity();
            var resistances = _entityManager.AddBuffer<HazardResistance>(entity);
            resistances.Add(new HazardResistance
            {
                HazardType = HazardTypeId.Radiation,
                ResistanceMultiplier = 0.5f
            });

            var events = _entityManager.AddBuffer<HazardDamageEvent>(entity);
            events.Add(new HazardDamageEvent
            {
                HazardType = HazardTypeId.Radiation,
                Amount = 20f
            });

            var system = _world.GetOrCreateSystem<Space4XHazardMitigationSystem>();
            system.Update(_world.Unmanaged);

            var updatedEvents = _entityManager.GetBuffer<HazardDamageEvent>(entity);
            Assert.AreEqual(1, updatedEvents.Length);
            Assert.AreEqual(10f, updatedEvents[0].Amount, 1e-3f);

            var metrics = _entityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            Assert.IsTrue(metrics.Length > 0);
        }
    }
}
#endif
