#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Space4X.Modes;
using Space4X.Runtime;
using Space4X.Systems.StateSpine;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Tests.PlayMode
{
    public class Space4XSimulationStateSpineTransitionTests
    {
        [Test]
        public void SpineTransitions_LogLifecycleModeScenario_InSingleBuffer()
        {
            var originalMode = Space4XModeSelectionState.CurrentMode;
            Space4XModeSelectionState.SetMode(Space4XModeKind.FleetCrawl);

            try
            {
                using var world = new World("SpineTransitionTests-Log");
                var entityManager = world.EntityManager;

                CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);
                var timeEntity = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
                SetTimeState(entityManager, timeEntity, tick: 0u, isPaused: false);

                var scenarioEntity = entityManager.CreateEntity(typeof(ScenarioInfo));
                entityManager.SetComponentData(scenarioEntity, new ScenarioInfo
                {
                    ScenarioId = new FixedString64Bytes("space4x_m2_transition_probe"),
                    Seed = 777u,
                    RunTicks = 240
                });

                var bootstrap = world.GetOrCreateSystem<Space4XSimulationStateSpineBootstrapSystem>();
                var tickSystem = world.GetOrCreateSystem<Space4XSimulationStateSpineTickSystem>();
                bootstrap.Update(world.Unmanaged);

                var root = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XSimulationStateSpineRootTag>()).GetSingletonEntity();

                AdvanceTick(world, entityManager, timeEntity, tickSystem, tick: 1u, isPaused: false);
                AdvanceTick(world, entityManager, timeEntity, tickSystem, tick: 2u, isPaused: false);

                var runtime = entityManager.GetComponentData<Space4XSimulationStateSpineState>(root);
                var transitions = entityManager.GetBuffer<Space4XSimulationStateSpineTransition>(root);

                Assert.AreEqual(Space4XSimulationLifecycle.Running, runtime.Lifecycle, "Expected Boot->Ready->Running progression.");
                Assert.AreEqual((byte)Space4XModeKind.FleetCrawl, runtime.Mode, "Mode should resolve from mode selection state.");
                Assert.Greater(runtime.ScenarioHash32, 0u, "Scenario hash should be observed.");
                Assert.Greater(transitions.Length, 0, "Transition buffer should capture state changes.");

                Assert.IsTrue(HasTransition(transitions, Space4XSimulationStateTransitionKind.Lifecycle, valid: true),
                    "Lifecycle transitions must be logged in spine transition buffer.");
                Assert.IsTrue(HasTransition(transitions, Space4XSimulationStateTransitionKind.Mode, valid: true),
                    "Mode transitions must be logged in spine transition buffer.");
                Assert.IsTrue(HasTransition(transitions, Space4XSimulationStateTransitionKind.Scenario, valid: true),
                    "Scenario transitions must be logged in spine transition buffer.");
            }
            finally
            {
                Space4XModeSelectionState.SetMode(originalMode);
            }
        }

        [Test]
        public void SpineTransitions_BlockInvalidModeAndScenarioJumps_WhileRunning()
        {
            var originalMode = Space4XModeSelectionState.CurrentMode;
            Space4XModeSelectionState.SetMode(Space4XModeKind.FleetCrawl);

            try
            {
                using var world = new World("SpineTransitionTests-Legality");
                var entityManager = world.EntityManager;

                CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);
                var timeEntity = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
                SetTimeState(entityManager, timeEntity, tick: 0u, isPaused: false);

                var scenarioEntity = entityManager.CreateEntity(typeof(ScenarioInfo));
                entityManager.SetComponentData(scenarioEntity, new ScenarioInfo
                {
                    ScenarioId = new FixedString64Bytes("space4x_m2_transition_probe"),
                    Seed = 900u,
                    RunTicks = 300
                });

                var bootstrap = world.GetOrCreateSystem<Space4XSimulationStateSpineBootstrapSystem>();
                var tickSystem = world.GetOrCreateSystem<Space4XSimulationStateSpineTickSystem>();
                bootstrap.Update(world.Unmanaged);

                var root = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XSimulationStateSpineRootTag>()).GetSingletonEntity();

                // Reach Running first.
                AdvanceTick(world, entityManager, timeEntity, tickSystem, tick: 1u, isPaused: false);
                AdvanceTick(world, entityManager, timeEntity, tickSystem, tick: 2u, isPaused: false);
                var baselineRuntime = entityManager.GetComponentData<Space4XSimulationStateSpineState>(root);
                Assert.AreEqual(Space4XSimulationLifecycle.Running, baselineRuntime.Lifecycle, "Expected running state before invalid jump checks.");

                // Request mode/scenario changes while running (illegal by M2 table).
                Space4XModeSelectionState.SetMode(Space4XModeKind.Classic);
                entityManager.SetComponentData(scenarioEntity, new ScenarioInfo
                {
                    ScenarioId = new FixedString64Bytes("space4x_m2_transition_probe_alt"),
                    Seed = 901u,
                    RunTicks = 300
                });
                AdvanceTick(world, entityManager, timeEntity, tickSystem, tick: 3u, isPaused: false);

                var afterInvalid = entityManager.GetComponentData<Space4XSimulationStateSpineState>(root);
                var guardAfterInvalid = entityManager.GetComponentData<Space4XSimulationStateSpineTransitionGuard>(root);
                var transitionsAfterInvalid = entityManager.GetBuffer<Space4XSimulationStateSpineTransition>(root);

                Assert.AreEqual((byte)Space4XModeKind.FleetCrawl, afterInvalid.Mode, "Illegal running-time mode jump should be blocked.");
                Assert.AreEqual(baselineRuntime.ScenarioSeed, afterInvalid.ScenarioSeed, "Illegal running-time scenario jump should be blocked.");
                Assert.GreaterOrEqual(guardAfterInvalid.InvalidModeTransitions, 1u, "Invalid mode transition should increment guard counter.");
                Assert.GreaterOrEqual(guardAfterInvalid.InvalidScenarioTransitions, 1u, "Invalid scenario transition should increment guard counter.");
                Assert.IsTrue(HasTransition(transitionsAfterInvalid, Space4XSimulationStateTransitionKind.Mode, valid: false),
                    "Invalid mode transition attempt should be logged.");
                Assert.IsTrue(HasTransition(transitionsAfterInvalid, Space4XSimulationStateTransitionKind.Scenario, valid: false),
                    "Invalid scenario transition attempt should be logged.");

                // Pause first, then same requests should become legal and apply.
                AdvanceTick(world, entityManager, timeEntity, tickSystem, tick: 4u, isPaused: true);
                var afterPause = entityManager.GetComponentData<Space4XSimulationStateSpineState>(root);
                Assert.AreEqual(Space4XSimulationLifecycle.Paused, afterPause.Lifecycle, "Expected paused state.");
                Assert.AreEqual((byte)Space4XModeKind.Classic, afterPause.Mode, "Paused mode transition should apply.");
                Assert.AreEqual(901u, afterPause.ScenarioSeed, "Paused scenario transition should apply.");
            }
            finally
            {
                Space4XModeSelectionState.SetMode(originalMode);
            }
        }

        private static bool HasTransition(
            DynamicBuffer<Space4XSimulationStateSpineTransition> transitions,
            Space4XSimulationStateTransitionKind kind,
            bool valid)
        {
            var validByte = valid ? (byte)1 : (byte)0;
            for (var i = 0; i < transitions.Length; i++)
            {
                var transition = transitions[i];
                if (transition.Kind == kind && transition.Valid == validByte)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetTimeState(EntityManager entityManager, Entity timeEntity, uint tick, bool isPaused)
        {
            var time = entityManager.GetComponentData<TimeState>(timeEntity);
            time.Tick = tick;
            time.FixedDeltaTime = 1f / 60f;
            time.DeltaTime = time.FixedDeltaTime;
            time.DeltaSeconds = time.FixedDeltaTime;
            time.ElapsedTime = tick * time.FixedDeltaTime;
            time.WorldSeconds = time.ElapsedTime;
            time.CurrentSpeedMultiplier = 1f;
            time.IsPaused = isPaused;
            entityManager.SetComponentData(timeEntity, time);
        }

        private static void AdvanceTick(
            World world,
            EntityManager entityManager,
            Entity timeEntity,
            SystemHandle tickSystem,
            uint tick,
            bool isPaused)
        {
            SetTimeState(entityManager, timeEntity, tick, isPaused);
            var time = entityManager.GetComponentData<TimeState>(timeEntity);
            world.EntityManager.WorldUnmanaged.Time = new TimeData(time.ElapsedTime, time.FixedDeltaTime);
            tickSystem.Update(world.Unmanaged);
        }
    }
}
#endif
