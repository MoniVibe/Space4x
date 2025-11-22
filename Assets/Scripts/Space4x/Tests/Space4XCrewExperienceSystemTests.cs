using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Tests
{
    public class Space4XCrewExperienceSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("CrewExperienceSystemTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            CoreSingletonBootstrapSystem.EnsureMiningSpine(_entityManager);
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
        public void CrewExperienceFromMiningCommandUpdatesSkills()
        {
            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>()).GetSingletonEntity();
            var timeState = _entityManager.GetComponentData<TimeState>(timeEntity);
            timeState.Tick = 3;
            timeState.IsPaused = false;
            _entityManager.SetComponentData(timeEntity, timeState);

            var rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();
            var rewindState = _entityManager.GetComponentData<RewindState>(rewindEntity);
            rewindState.Mode = RewindMode.Record;
            _entityManager.SetComponentData(rewindEntity, rewindState);

            var spineEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMiningTimeSpine>()).GetSingletonEntity();
            var commandLog = _entityManager.GetBuffer<MiningCommandLogEntry>(spineEntity);

            var miner = _entityManager.CreateEntity(typeof(MiningVessel));

            commandLog.Add(new MiningCommandLogEntry
            {
                Tick = 3,
                CommandType = MiningCommandType.Gather,
                SourceEntity = Entity.Null,
                TargetEntity = miner,
                ResourceType = ResourceType.Minerals,
                Amount = 12f,
                Position = float3.zero
            });

            var system = _world.GetOrCreateSystem<Space4XCrewExperienceSystem>();
            system.Update(_world.Unmanaged);

            Assert.IsTrue(_entityManager.HasComponent<SkillExperienceGain>(miner));
            Assert.IsTrue(_entityManager.HasComponent<CrewSkills>(miner));

            var xp = _entityManager.GetComponentData<SkillExperienceGain>(miner);
            var skills = _entityManager.GetComponentData<CrewSkills>(miner);

            Assert.Greater(xp.MiningXp, 0f);
            Assert.Greater(skills.MiningSkill, 0f);

            var skillLog = _entityManager.GetBuffer<SkillChangeLogEntry>(spineEntity);
            Assert.AreEqual(1, skillLog.Length);
            Assert.AreEqual(miner, skillLog[0].TargetEntity);
            Assert.Greater(skillLog[0].NewSkill, 0f);
        }
    }
}
