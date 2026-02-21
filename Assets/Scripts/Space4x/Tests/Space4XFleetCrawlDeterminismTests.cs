#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Systems;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XFleetCrawlDeterminismTests
    {
        [Test]
        public void FleetCrawlDigest_FixedSeedIsStableAcrossRuns()
        {
            var first = RunFleetCrawl(seed: 12345u, ticks: 2400);
            var second = RunFleetCrawl(seed: 12345u, ticks: 2400);
            TestContext.WriteLine($"FleetCrawl digest(first)={first.Digest} digest(second)={second.Digest} roomIndex={first.RoomIndex}");

            Assert.AreEqual(first.Digest, second.Digest, "Fleet Crawl digest drifted for fixed seed.");
            Assert.AreEqual(first.RoomIndex, second.RoomIndex, "Room progression drifted for fixed seed.");
            Assert.AreEqual(first.RewardCount, second.RewardCount, "Reward progression drifted for fixed seed.");
            Assert.Greater(first.RoomIndex, 4, "Run did not advance enough rooms.");
            Assert.Greater(first.RewardCount, 4, "Rewards were not applied across room transitions.");
        }

        [Test]
        public void FleetCrawlDigest_SameSeedProducesSameDigest()
        {
            var first = RunFleetCrawl(seed: 98765u, ticks: 2200);
            var second = RunFleetCrawl(seed: 98765u, ticks: 2200);
            Assert.AreEqual(first.Digest, second.Digest, "Deterministic digest mismatch for identical seed.");
            Assert.AreEqual(first.RoomIndex, second.RoomIndex, "Room index mismatch for identical seed.");
            Assert.AreEqual(first.RewardCount, second.RewardCount, "Reward count mismatch for identical seed.");
        }

        [Test]
        public void FleetCrawlDigest_DifferentSeedChangesDigest()
        {
            var first = RunFleetCrawl(seed: 11111u, ticks: 2200);
            var second = RunFleetCrawl(seed: 22222u, ticks: 2200);
            Assert.AreNotEqual(first.Digest, second.Digest, "Different seeds should diverge gate/reward digest.");
        }

        private static FleetCrawlSummary RunFleetCrawl(uint seed, int ticks)
        {
            using var world = new World("FleetCrawlDeterminism");
            var entityManager = world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);

            var initGroup = world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();

            initGroup.AddSystemToUpdateList(world.GetOrCreateSystem<Space4XFleetCrawlRunBootstrapSystem>());
            simGroup.AddSystemToUpdateList(world.GetOrCreateSystem<Space4XFleetCrawlRoomCompletionSystem>());
            simGroup.AddSystemToUpdateList(world.GetOrCreateSystem<Space4XFleetCrawlGateResolveSystem>());

            var timeEntity = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var timeState = entityManager.GetComponentData<TimeState>(timeEntity);
            timeState.Tick = 0;
            timeState.FixedDeltaTime = 1f / 60f;
            timeState.DeltaTime = timeState.FixedDeltaTime;
            timeState.DeltaSeconds = timeState.FixedDeltaTime;
            timeState.ElapsedTime = 0f;
            timeState.WorldSeconds = 0f;
            timeState.CurrentSpeedMultiplier = 1f;
            timeState.IsPaused = false;
            entityManager.SetComponentData(timeEntity, timeState);

            var rewindEntity = entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();
            var rewind = entityManager.GetComponentData<RewindState>(rewindEntity);
            rewind.Mode = RewindMode.Record;
            rewind.TickDuration = timeState.FixedDeltaTime;
            entityManager.SetComponentData(rewindEntity, rewind);

            var scenarioEntity = entityManager.CreateEntity(typeof(ScenarioInfo));
            entityManager.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = Space4XFleetCrawlScenario.ScenarioId,
                Seed = seed,
                RunTicks = ticks
            });

            world.EntityManager.WorldUnmanaged.Time = new TimeData(0f, timeState.FixedDeltaTime);

            for (var i = 0; i < ticks; i++)
            {
                var frameTime = entityManager.GetComponentData<TimeState>(timeEntity);
                frameTime.Tick += 1;
                frameTime.ElapsedTime = frameTime.Tick * frameTime.FixedDeltaTime;
                frameTime.WorldSeconds = frameTime.ElapsedTime;
                frameTime.DeltaTime = frameTime.FixedDeltaTime;
                frameTime.DeltaSeconds = frameTime.FixedDeltaTime;
                entityManager.SetComponentData(timeEntity, frameTime);

                world.EntityManager.WorldUnmanaged.Time = new TimeData(frameTime.ElapsedTime, frameTime.FixedDeltaTime);

                initGroup.Update();
                simGroup.Update();
            }

            var runEntity = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XFleetCrawlRunTag>()).GetSingletonEntity();
            var progress = entityManager.GetComponentData<Space4XFleetCrawlRunProgress>(runEntity);
            var rewards = entityManager.GetBuffer<Space4XFleetCrawlRewardApplied>(runEntity);
            return new FleetCrawlSummary
            {
                Digest = progress.Digest,
                RoomIndex = progress.RoomIndex,
                RewardCount = rewards.Length
            };
        }

        private struct FleetCrawlSummary
        {
            public uint Digest;
            public int RoomIndex;
            public int RewardCount;
        }
    }
}
#endif
