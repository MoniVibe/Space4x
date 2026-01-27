using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Launch;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Systems.Launch;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Tests.Launch
{
    /// <summary>
    /// Unit tests for the Launch systems.
    /// </summary>
    public class LaunchSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void Setup()
        {
            _world = new World("LaunchTestWorld");
            _entityManager = _world.EntityManager;

            // Create required singletons
            var timeEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(timeEntity, new TimeState { Tick = 0, DeltaTime = 1f / 60f });

            var rewindEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(rewindEntity, new RewindState { Mode = RewindMode.Record });
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void LaunchComponents_CreateDefault_HasValidValues()
        {
            var config = LauncherConfig.CreateDefault();

            Assert.AreEqual(8, config.MaxQueueSize);
            Assert.AreEqual(10u, config.CooldownTicks);
            Assert.AreEqual(10f, config.DefaultSpeed);
        }

        [Test]
        public void LaunchRequest_Buffer_CanAddEntries()
        {
            var launcher = _entityManager.CreateEntity();
            _entityManager.AddComponent<LauncherTag>(launcher);
            _entityManager.AddComponentData(launcher, LauncherConfig.CreateDefault());
            _entityManager.AddComponentData(launcher, new LauncherState());
            var requestBuffer = _entityManager.AddBuffer<LaunchRequest>(launcher);
            _entityManager.AddBuffer<LaunchQueueEntry>(launcher);

            var payload = _entityManager.CreateEntity();

            requestBuffer.Add(new LaunchRequest
            {
                SourceEntity = launcher,
                PayloadEntity = payload,
                LaunchTick = 0,
                InitialVelocity = new float3(10, 5, 0),
                Flags = 0
            });

            Assert.AreEqual(1, requestBuffer.Length);
            Assert.AreEqual(payload, requestBuffer[0].PayloadEntity);
        }

        [Test]
        public void LaunchQueueEntry_StateTransitions_AreValid()
        {
            var entry = new LaunchQueueEntry
            {
                PayloadEntity = Entity.Null,
                ScheduledTick = 10,
                InitialVelocity = float3.zero,
                State = LaunchEntryState.Pending
            };

            Assert.AreEqual(LaunchEntryState.Pending, entry.State);

            entry.State = LaunchEntryState.Launched;
            Assert.AreEqual(LaunchEntryState.Launched, entry.State);

            entry.State = LaunchEntryState.Consumed;
            Assert.AreEqual(LaunchEntryState.Consumed, entry.State);
        }

        [Test]
        public void LaunchedProjectileTag_StoresLaunchInfo()
        {
            var launcher = _entityManager.CreateEntity();
            var projectile = _entityManager.CreateEntity();

            _entityManager.AddComponentData(projectile, new LaunchedProjectileTag
            {
                LaunchTick = 42,
                SourceLauncher = launcher
            });

            var tag = _entityManager.GetComponentData<LaunchedProjectileTag>(projectile);
            Assert.AreEqual(42u, tag.LaunchTick);
            Assert.AreEqual(launcher, tag.SourceLauncher);
        }

        [Test]
        public void SlingshotScenario_ParsesSuccessfully()
        {
            var path = Path.Combine("Packages", "com.moni.puredots", "Runtime", "Runtime", "Scenarios", "Samples", "slingshot_launch.json");

            // Skip if file doesn't exist (may not be imported yet)
            if (!File.Exists(path))
            {
                Assert.Inconclusive($"Scenario file not found: {path}");
                return;
            }

            var json = File.ReadAllText(path);
            Assert.IsTrue(ScenarioRunner.TryParse(json, out var data, out var error), error.ToString());

            using var scenario = BuildScenario(data);
            Assert.AreEqual("scenario.puredots.slingshot_launch", scenario.ScenarioId.ToString());
            Assert.AreEqual(300, scenario.RunTicks);
            Assert.AreEqual(2, scenario.EntityCounts.Length);
            Assert.AreEqual(6, scenario.InputCommands.Length);
        }

        private static ResolvedScenario BuildScenario(ScenarioDefinitionData data)
        {
            Assert.IsTrue(ScenarioRunner.TryBuild(data, Allocator.Temp, out var scenario, out var error), error.ToString());
            return scenario;
        }
    }
}




