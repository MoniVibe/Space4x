using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Systems;
using Space4x.Scenario;
using Space4X.Registry;
using Space4X.Systems.AI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests.PlayMode
{
    /// <summary>
    /// PlayMode coverage for the mining/haul/combat scenario timeline.
    /// Ensures ScenarioRunner actions execute and remain deterministic.
    /// </summary>
    public class Space4XScenarioTimelineTests
    {
        private World _world;
        private EntityManager _em;
        private Entity _timeEntity;
        private Entity _rewindEntity;

        private SystemHandle _scenarioSystem;
        private SystemHandle _actionProcessor;
        private SystemHandle _populationSystem;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ScenarioTimeline");
            _em = _world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(_em);

            _scenarioSystem = _world.GetOrCreateSystem<Space4XMiningScenarioSystem>();
            _actionProcessor = _world.GetOrCreateSystem<Space4XMiningScenarioActionProcessor>();
            _populationSystem = _world.GetOrCreateSystem<Space4XResourceRegistryPopulationSystem>();

            _timeEntity = _em.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            _rewindEntity = _em.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();

            var time = _em.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 0;
            time.FixedDeltaTime = 1f;
            time.IsPaused = false;
            _em.SetComponentData(_timeEntity, time);

            var rewind = _em.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Record;
            _em.SetComponentData(_rewindEntity, rewind);

            // Seed ScenarioRunner
            var scenarioEntity = _em.CreateEntity(typeof(ScenarioInfo));
            _em.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = new Unity.Collections.FixedString64Bytes("space4x_mining_haul_timeline"),
                RunTicks = 24,
                Seed = 1
            });
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
        public void TimelineActionsExecuteAndAreDeterministic()
        {
            // Load scenario and populate registry for spawned asteroids
            _scenarioSystem.Update(_world.Unmanaged);
            _populationSystem.Update(_world.Unmanaged);

            // Prime carrier storage so drain action has an effect
            using (var carriers = _em.CreateEntityQuery(ComponentType.ReadOnly<Carrier>(), ComponentType.ReadWrite<ResourceStorage>()).ToEntityArray(Allocator.Temp))
            {
                foreach (var carrier in carriers)
                {
                    var storage = _em.GetBuffer<ResourceStorage>(carrier);
                    if (storage.Length > 0)
                    {
                        var first = storage[0];
                        first.Amount = 50f;
                        storage[0] = first;
                        break;
                    }
                }
            }

            // Advance time to cover all scheduled actions
            var time = _em.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 15;
            _em.SetComponentData(_timeEntity, time);
            _actionProcessor.Update(_world.Unmanaged);

            // Verify pickup was spawned
            var spawnQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<SpawnResource>());
            Assert.Greater(spawnQuery.CalculateEntityCount(), 0, "Scenario should spawn pickups via actions");

            // Verify miner was forced to return
            bool minerReturning = false;
            using (var minerEntities = _em.CreateEntityQuery(ComponentType.ReadOnly<MiningVessel>(), ComponentType.ReadOnly<VesselAIState>()).ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in minerEntities)
                {
                    var vessel = _em.GetComponentData<MiningVessel>(entity);
                    if (vessel.VesselId != new Unity.Collections.FixedString64Bytes("miner-0"))
                    {
                        continue;
                    }

                    var ai = _em.GetComponentData<VesselAIState>(entity);
                    minerReturning = ai.CurrentGoal == VesselAIState.Goal.Returning;
                    break;
                }
            }
            Assert.IsTrue(minerReturning, "Timeline should force miner-0 to return");

            // Verify carrier storage was drained
            float remaining = 0f;
            using (var carriers = _em.CreateEntityQuery(ComponentType.ReadOnly<Carrier>(), ComponentType.ReadOnly<ResourceStorage>()).ToEntityArray(Allocator.Temp))
            {
                foreach (var carrier in carriers)
                {
                    var storage = _em.GetBuffer<ResourceStorage>(carrier);
                    for (int i = 0; i < storage.Length; i++)
                    {
                        remaining += storage[i].Amount;
                    }
                    break;
                }
            }
            Assert.Less(remaining, 50f, "Carrier storage should be reduced by drain action");

            // Reset scheduler manually to mimic rewind and re-run deterministically
            using (var schedulerQuery = _em.CreateEntityQuery(ComponentType.ReadWrite<ScenarioActionScheduler>()))
            {
                var schedulerEntity = schedulerQuery.GetSingletonEntity();
                var scheduler = _em.GetComponentData<ScenarioActionScheduler>(schedulerEntity);
                scheduler.LastProcessedTime = -1f;
                _em.SetComponentData(schedulerEntity, scheduler);
            }

            time = _em.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 0;
            _em.SetComponentData(_timeEntity, time);
            _actionProcessor.Update(_world.Unmanaged);

            time.Tick = 15;
            _em.SetComponentData(_timeEntity, time);
            _actionProcessor.Update(_world.Unmanaged);

            float remainingAfterReplay = 0f;
            using (var carriers = _em.CreateEntityQuery(ComponentType.ReadOnly<Carrier>(), ComponentType.ReadOnly<ResourceStorage>()).ToEntityArray(Allocator.Temp))
            {
                foreach (var carrier in carriers)
                {
                    var storage = _em.GetBuffer<ResourceStorage>(carrier);
                    for (int i = 0; i < storage.Length; i++)
                    {
                        remainingAfterReplay += storage[i].Amount;
                    }
                    break;
                }
            }

            Assert.AreEqual(remaining, remainingAfterReplay, 0.01f, "Drain result should be deterministic after rewind/replay");
        }
    }
}

