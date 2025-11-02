using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Entities;

namespace PureDOTS.Tests
{
    public class TimeStateTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("PureDOTS Test World");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void CoreSingletonBootstrapSystem_CreatesTimeHistoryAndRewindSingletons()
        {
            var bootstrap = _world.GetOrCreateSystemManaged<CoreSingletonBootstrapSystem>();
            bootstrap.Update();

            Assert.IsTrue(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>()).TryGetSingleton(out TimeState _));
            Assert.IsTrue(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<HistorySettings>()).TryGetSingleton(out HistorySettings _));
            Assert.IsTrue(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>()).TryGetSingleton(out RewindState _));
        }

        [Test]
        public void TimeSettingsConfigSystem_AppliesOverrides()
        {
            var timeEntity = _entityManager.CreateEntity(typeof(TimeState));
            _entityManager.SetComponentData(timeEntity, new TimeState
            {
                FixedDeltaTime = 0.02f,
                CurrentSpeedMultiplier = 1f,
                Tick = 0,
                IsPaused = false
            });

            var overrideEntity = _entityManager.CreateEntity(typeof(TimeSettingsConfig));
            _entityManager.SetComponentData(overrideEntity, new TimeSettingsConfig
            {
                FixedDeltaTime = 0.1f,
                DefaultSpeedMultiplier = 2f,
                PauseOnStart = true
            });

            TimeSettingsConfigSystem.ApplyOverrides(_entityManager);

            var updated = _entityManager.GetComponentData<TimeState>(timeEntity);
            Assert.AreEqual(0.1f, updated.FixedDeltaTime);
            Assert.AreEqual(2f, updated.CurrentSpeedMultiplier);
            Assert.IsTrue(updated.IsPaused);
        }
    }
}
