#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XTechDiffusionSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XTechDiffusionSystemTests");
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
        public void DiffusionProgressesAndUpgradesTech()
        {
            ConfigureTime(1f, 10, RewindMode.Record, isPaused: false);

            var entity = _entityManager.CreateEntity(typeof(TechLevel), typeof(TechDiffusionState));
            _entityManager.SetComponentData(entity, new TechLevel
            {
                MiningTech = 1,
                CombatTech = 0,
                HaulingTech = 2,
                ProcessingTech = 0,
                LastUpgradeTick = 0
            });
            _entityManager.SetComponentData(entity, new TechDiffusionState
            {
                DiffusionDurationSeconds = 2f,
                DiffusionProgressSeconds = 0f,
                TargetMiningTech = 3,
                TargetCombatTech = 2,
                TargetHaulingTech = 2,
                TargetProcessingTech = 1,
                Active = 1,
                SourceEntity = Entity.Null,
                DiffusionStartTick = 0
            });

            var system = _world.GetOrCreateSystem<Space4XTechDiffusionSystem>();
            system.Update(_world.Unmanaged);

            var diffusion = _entityManager.GetComponentData<TechDiffusionState>(entity);
            Assert.AreEqual(1f, diffusion.DiffusionProgressSeconds, 1e-3f);
            Assert.AreEqual(1, diffusion.Active, "Diffusion should remain active until duration is met.");
            Assert.AreEqual(10u, diffusion.DiffusionStartTick);

            system.Update(_world.Unmanaged);

            diffusion = _entityManager.GetComponentData<TechDiffusionState>(entity);
            var tech = _entityManager.GetComponentData<TechLevel>(entity);

            Assert.AreEqual(0, diffusion.Active, "Diffusion should deactivate on completion.");
            Assert.AreEqual(2f, diffusion.DiffusionProgressSeconds, 1e-3f);
            Assert.AreEqual(3, tech.MiningTech);
            Assert.AreEqual(2, tech.CombatTech);
            Assert.AreEqual(2, tech.HaulingTech, "Existing or higher tech levels must be preserved.");
            Assert.AreEqual(1, tech.ProcessingTech);
            Assert.AreEqual(10u, tech.LastUpgradeTick);
        }

        [Test]
        public void DiffusionSkipsWhenPausedOrPlayback()
        {
            ConfigureTime(1f, 5, RewindMode.Playback, isPaused: true);

            var entity = _entityManager.CreateEntity(typeof(TechLevel), typeof(TechDiffusionState));
            _entityManager.SetComponentData(entity, new TechLevel
            {
                MiningTech = 2,
                CombatTech = 1,
                HaulingTech = 1,
                ProcessingTech = 0,
                LastUpgradeTick = 0
            });
            _entityManager.SetComponentData(entity, new TechDiffusionState
            {
                DiffusionDurationSeconds = 1f,
                DiffusionProgressSeconds = 0f,
                TargetMiningTech = 4,
                TargetCombatTech = 3,
                TargetHaulingTech = 2,
                TargetProcessingTech = 1,
                Active = 1,
                SourceEntity = Entity.Null,
                DiffusionStartTick = 0
            });

            var system = _world.GetOrCreateSystem<Space4XTechDiffusionSystem>();
            system.Update(_world.Unmanaged);

            var diffusion = _entityManager.GetComponentData<TechDiffusionState>(entity);
            var tech = _entityManager.GetComponentData<TechLevel>(entity);

            Assert.AreEqual(0f, diffusion.DiffusionProgressSeconds, 1e-3f, "Progress should not advance when paused or in playback.");
            Assert.AreEqual(1, diffusion.Active);
            Assert.AreEqual(2, tech.MiningTech, "Tech levels should remain unchanged when skipped.");
            Assert.AreEqual(0u, tech.LastUpgradeTick);
        }

        [Test]
        public void DiffusionUpdatesTelemetryAndLogsCompletion()
        {
            ConfigureTime(0.5f, 2, RewindMode.Record, isPaused: false);
            CoreSingletonBootstrapSystem.EnsureTechDiffusionTelemetry(_entityManager);

            var target = _entityManager.CreateEntity(typeof(TechLevel), typeof(TechDiffusionState));
            _entityManager.SetComponentData(target, new TechLevel
            {
                MiningTech = 0,
                CombatTech = 0,
                HaulingTech = 0,
                ProcessingTech = 0,
                LastUpgradeTick = 0
            });
            _entityManager.SetComponentData(target, new TechDiffusionState
            {
                DiffusionDurationSeconds = 1f,
                DiffusionProgressSeconds = 0f,
                TargetMiningTech = 1,
                TargetCombatTech = 2,
                TargetHaulingTech = 1,
                TargetProcessingTech = 1,
                Active = 1,
                SourceEntity = Entity.Null,
                DiffusionStartTick = 0
            });

            var system = _world.GetOrCreateSystem<Space4XTechDiffusionSystem>();
            system.Update(_world.Unmanaged);
            system.Update(_world.Unmanaged);

            var telemetryEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TechDiffusionTelemetry>()).GetSingletonEntity();
            var telemetry = _entityManager.GetComponentData<TechDiffusionTelemetry>(telemetryEntity);
            var tech = _entityManager.GetComponentData<TechLevel>(target);
            var log = _entityManager.GetBuffer<TechDiffusionCommandLogEntry>(telemetryEntity);

            Assert.AreEqual(0, telemetry.ActiveDiffusions);
            Assert.AreEqual(1u, telemetry.CompletedUpgrades);
            Assert.AreEqual(2u, telemetry.LastUpgradeTick);
            Assert.AreEqual(1, tech.MiningTech);
            Assert.AreEqual(2, tech.CombatTech);
            Assert.AreEqual(1, log.Length);
            Assert.AreEqual(target, log[0].TargetEntity);
        }

        private void ConfigureTime(float fixedDeltaTime, uint tick, RewindMode mode, bool isPaused)
        {
            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var time = _entityManager.GetComponentData<TimeState>(timeEntity);
            time.FixedDeltaTime = fixedDeltaTime;
            time.Tick = tick;
            time.IsPaused = isPaused;
            _entityManager.SetComponentData(timeEntity, time);
            _entityManager.SetComponentData(timeEntity, new GameplayFixedStep
            {
                FixedDeltaTime = fixedDeltaTime
            });

            var rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();
            var rewind = _entityManager.GetComponentData<RewindState>(rewindEntity);
            rewind.Mode = mode;
            _entityManager.SetComponentData(rewindEntity, rewind);
        }
    }
}
#endif
