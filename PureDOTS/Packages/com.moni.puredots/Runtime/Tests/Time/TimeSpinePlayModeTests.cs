using System.Collections;
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Core;
using Unity.Entities;
using Unity.Collections;
using UnityEngine.TestTools;
using EntitiesPresentationSystemGroup = Unity.Entities.PresentationSystemGroup;

namespace PureDOTS.Tests.Time
{
    public partial class TimeSpinePlayModeTests
    {
        private struct TestWorldContext : System.IDisposable
        {
            public World World;
            public InitializationSystemGroup InitGroup;
            public TimeSystemGroup TimeGroup;
            public SimulationSystemGroup SimulationGroup;
            public EntitiesPresentationSystemGroup PresentationGroup;
            public FixedStepSimulationSystemGroup FixedStepGroup;

            public TestWorldContext(World world, InitializationSystemGroup initGroup, TimeSystemGroup timeGroup,
                SimulationSystemGroup simulationGroup, EntitiesPresentationSystemGroup presentationGroup,
                FixedStepSimulationSystemGroup fixedStepGroup)
            {
                World = world;
                InitGroup = initGroup;
                TimeGroup = timeGroup;
                SimulationGroup = simulationGroup;
                PresentationGroup = presentationGroup;
                FixedStepGroup = fixedStepGroup;
            }

            public void Dispose()
            {
                World.Dispose();
                if (Unity.Entities.World.DefaultGameObjectInjectionWorld == World)
                {
                    Unity.Entities.World.DefaultGameObjectInjectionWorld = null;
                }
            }
        }

        [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
        private partial struct FixedStepCounterSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate<FixedStepCounter>();
            }

            public void OnUpdate(ref SystemState state)
            {
                foreach (var counter in SystemAPI.Query<RefRW<FixedStepCounter>>())
                {
                    counter.ValueRW.Count++;
                }
            }
        }

        private struct FixedStepCounter : IComponentData
        {
            public int Count;
        }

        [UnityTest]
        public IEnumerator PausePlayAndStepTicksRespectState()
        {
            using var ctx = CreateContext();
            var initialTick = GetTick(ctx.World);

            AdvanceFrame(ctx);
            var tickAfterPlay = GetTick(ctx.World);
            Assert.Greater(tickAfterPlay, initialTick);

            AddCommand(ctx.World, TimeControlCommandType.Pause);
            AdvanceFrame(ctx);
            var pausedTick = GetTick(ctx.World);
            AdvanceFrame(ctx);
            Assert.AreEqual(pausedTick, GetTick(ctx.World));

            AddCommand(ctx.World, TimeControlCommandType.StepTicks, 1);
            AdvanceFrame(ctx);
            Assert.AreEqual(pausedTick + 1, GetTick(ctx.World));

            AddCommand(ctx.World, TimeControlCommandType.Resume);
            AdvanceFrame(ctx);
            Assert.Greater(GetTick(ctx.World), pausedTick + 1);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedStepRunsOnlyWhilePlaying()
        {
            using var ctx = CreateContext(includeFixedStepCounter: true);
            var counterEntity = ctx.World.EntityManager.CreateEntity(typeof(FixedStepCounter));

            AdvanceFrame(ctx);
            var initialCount = ctx.World.EntityManager.GetComponentData<FixedStepCounter>(counterEntity).Count;
            Assert.Greater(initialCount, 0);

            AddCommand(ctx.World, TimeControlCommandType.Pause);
            AdvanceFrame(ctx);
            var pausedCount = ctx.World.EntityManager.GetComponentData<FixedStepCounter>(counterEntity).Count;
            AdvanceFrame(ctx);
            Assert.AreEqual(pausedCount, ctx.World.EntityManager.GetComponentData<FixedStepCounter>(counterEntity).Count);

            AddCommand(ctx.World, TimeControlCommandType.Resume);
            AdvanceFrame(ctx);
            Assert.Greater(ctx.World.EntityManager.GetComponentData<FixedStepCounter>(counterEntity).Count, pausedCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SimulationDisablesDuringPlayback()
        {
            using var ctx = CreateContext();
            AdvanceSeveralFrames(ctx, 8);
            AddCommand(ctx.World, TimeControlCommandType.StartRewind, 1);
            AdvanceFrame(ctx);
            Assert.IsFalse(ctx.SimulationGroup.Enabled);
            Assert.IsFalse(ctx.FixedStepGroup.Enabled);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RewindMovesTowardTargetThenCatchesUp()
        {
            using var ctx = CreateContext();
            AdvanceSeveralFrames(ctx, 180); // warm up ~3 seconds
            var currentTick = GetTick(ctx.World);
            var targetTick = currentTick > 120 ? currentTick - 120 : 0;

            AddCommand(ctx.World, TimeControlCommandType.StartRewind, targetTick);
            var playbackProgressed = false;
            for (int i = 0; i < 240 && GetRewindState(ctx.World).Mode == RewindMode.Playback; i++)
            {
                AdvanceFrame(ctx, 1d / 30d);
                playbackProgressed |= GetTick(ctx.World) != currentTick;
            }

            Assert.IsTrue(playbackProgressed);
            var rewindState = GetRewindState(ctx.World);
            Assert.AreNotEqual(RewindMode.Playback, rewindState.Mode);
            Assert.AreEqual(rewindState.TargetTick, GetTick(ctx.World));
            yield return null;
        }

        [UnityTest]
        public IEnumerator VariableFrameRatesRemainDeterministic()
        {
            var sequenceA = new[] { 1d / 30d, 1d / 120d, 1d / 45d, 1d / 20d };
            var sequenceB = new[] { 1d / 60d, 1d / 60d, 1d / 30d, 1d / 20d };

            var ticksA = RunSequence(sequenceA);
            var ticksB = RunSequence(sequenceB);

            Assert.AreEqual(ticksA, ticksB);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LogsTrackCommandsAndSnapshots()
        {
            using var ctx = CreateContext();
            AddCommand(ctx.World, TimeControlCommandType.Pause);
            AdvanceFrame(ctx);
            AddCommand(ctx.World, TimeControlCommandType.Resume);
            AdvanceSeveralFrames(ctx, 3);

            var commandLog = ctx.World.EntityManager.CreateEntityQuery(typeof(InputCommandLogState)).GetSingleton<InputCommandLogState>();
            var snapshotLog = ctx.World.EntityManager.CreateEntityQuery(typeof(TickSnapshotLogState)).GetSingleton<TickSnapshotLogState>();

            Assert.GreaterOrEqual(commandLog.Count, 2);
            Assert.GreaterOrEqual(snapshotLog.Count, 4);
            Assert.LessOrEqual(commandLog.LastTick, GetTick(ctx.World));
            Assert.AreEqual(snapshotLog.LastTick, GetTick(ctx.World));
            yield return null;
        }

        private static uint RunSequence(double[] deltas)
        {
            using var ctx = CreateContext();
            foreach (var delta in deltas)
            {
                AdvanceFrame(ctx, delta);
            }

            return GetTick(ctx.World);
        }

        private static TestWorldContext CreateContext(bool includeFixedStepCounter = false)
        {
            var world = new World("TimeTestWorld", WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = world;

            var initGroup = world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            var timeGroup = world.GetExistingSystemManaged<TimeSystemGroup>();
            var simulationGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            var presentationGroup = world.GetExistingSystemManaged<EntitiesPresentationSystemGroup>();
            var fixedStepGroup = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            fixedStepGroup.Timestep = 1f / 60f;

            world.CreateSystem<CoreSingletonBootstrapSystem>();
            world.CreateSystem<TimeSettingsConfigSystem>();
            world.CreateSystem<HistorySettingsConfigSystem>();
            world.CreateSystem<TimeLogConfigSystem>();
            world.CreateSystem<RewindCoordinatorSystem>();
            world.CreateSystem<SimulationTickGateSystem>();
            world.CreateSystem<TimeTickSystem>();
            world.CreateSystem<GameplayFixedStepSyncSystem>();
            world.CreateSystem<TickSnapshotLogSystem>();
            if (includeFixedStepCounter)
            {
                world.CreateSystem<FixedStepCounterSystem>();
            }

            initGroup.SortSystems();
            timeGroup.SortSystems();
            simulationGroup.SortSystems();
            fixedStepGroup.SortSystems();
            presentationGroup.SortSystems();

            return new TestWorldContext(world, initGroup, timeGroup, simulationGroup, presentationGroup, fixedStepGroup);
        }

        private static void AdvanceFrame(TestWorldContext ctx, double deltaTime = 1d / 60d)
        {
            var timeData = ctx.World.Time;
            var newElapsed = (float)(timeData.ElapsedTime + deltaTime);
            ctx.World.SetTime(new TimeData((float)deltaTime, newElapsed));

            ctx.InitGroup.Update();
            if (ctx.FixedStepGroup.Enabled)
            {
                ctx.FixedStepGroup.Update();
            }

            if (ctx.SimulationGroup.Enabled)
            {
                ctx.SimulationGroup.Update();
            }

            if (ctx.PresentationGroup.Enabled)
            {
                ctx.PresentationGroup.Update();
            }
        }

        private static void AdvanceSeveralFrames(TestWorldContext ctx, int frameCount, double deltaTime = 1d / 60d)
        {
            for (int i = 0; i < frameCount; i++)
            {
                AdvanceFrame(ctx, deltaTime);
            }
        }

        private static void AddCommand(World world, TimeControlCommandType type, uint uintParam = 0, float floatParam = 0f)
        {
            var rewindEntity = world.EntityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();
            var buffer = world.EntityManager.GetBuffer<TimeControlCommand>(rewindEntity);
            buffer.Add(new TimeControlCommand
            {
                Type = type,
                UintParam = uintParam,
                FloatParam = floatParam
            });
        }

        private static uint GetTick(World world)
        {
            return world.EntityManager.CreateEntityQuery(typeof(TickTimeState)).GetSingleton<TickTimeState>().Tick;
        }

        private static RewindState GetRewindState(World world)
        {
            return world.EntityManager.CreateEntityQuery(typeof(RewindState)).GetSingleton<RewindState>();
        }
    }
}
