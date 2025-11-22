using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Entities;

namespace Space4X.Tests
{
    public class CrewGrowthSystemsTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("CrewGrowthSystemsTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            var time = _entityManager.GetComponentData<TimeState>(_entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity());
            time.FixedDeltaTime = 1f;
            time.IsPaused = false;
            time.Tick = 5;
            _entityManager.SetComponentData(_entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity(), time);

            var rewind = _entityManager.GetComponentData<RewindState>(_entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity());
            rewind.Mode = RewindMode.Record;
            _entityManager.SetComponentData(_entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity(), rewind);

            CoreSingletonBootstrapSystem.EnsureCrewGrowthTelemetry(_entityManager);
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
        public void DisabledSettingsDoNotChangeCrewState()
        {
            var entity = _entityManager.CreateEntity(typeof(CrewGrowthSettings), typeof(CrewGrowthState));
            _entityManager.SetComponentData(entity, new CrewGrowthSettings
            {
                BreedingEnabled = 0,
                CloningEnabled = 0,
                BreedingRatePerTick = 0f,
                CloningRatePerTick = 0f,
                CloningResourceCost = 0f,
                DoctrineAllowsBreeding = 0,
                DoctrineAllowsCloning = 0,
                LastConfiguredTick = 0
            });
            _entityManager.SetComponentData(entity, new CrewGrowthState
            {
                CurrentCrew = 5f,
                Capacity = 10f
            });

            var system = _world.GetOrCreateSystem<Space4XCrewGrowthSystem>();
            system.Update(_world.Unmanaged);

            var state = _entityManager.GetComponentData<CrewGrowthState>(entity);
            Assert.AreEqual(5f, state.CurrentCrew);
            Assert.AreEqual(10f, state.Capacity);

            var telemetryEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<CrewGrowthTelemetry>()).GetSingletonEntity();
            var telemetry = _entityManager.GetComponentData<CrewGrowthTelemetry>(telemetryEntity);
            Assert.AreEqual(1u, telemetry.GrowthSkipped);

            var log = _entityManager.GetBuffer<CrewGrowthCommandLogEntry>(telemetryEntity);
            Assert.AreEqual(0, log.Length);
        }

        [Test]
        public void SanitizeInvalidRatesDisablesFeatures()
        {
            var settings = new CrewGrowthSettings
            {
                BreedingEnabled = 1,
                CloningEnabled = 1,
                BreedingRatePerTick = -5f,
                CloningRatePerTick = -2f,
                CloningResourceCost = -1f,
                DoctrineAllowsBreeding = 1,
                DoctrineAllowsCloning = 1
            };

            var result = CrewGrowthSettingsUtility.Sanitize(settings);
            Assert.IsTrue(result.HadError);
            Assert.AreEqual(0, result.Settings.BreedingEnabled);
            Assert.AreEqual(0, result.Settings.CloningEnabled);
            Assert.GreaterOrEqual(result.Settings.CloningResourceCost, 0f);
        }
    }
}
