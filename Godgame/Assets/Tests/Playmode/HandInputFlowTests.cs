using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Tests
{
    public class HandInputFlowTests
    {
        private World _world;
        private EntityManager _em;
        private SystemHandle _bootstrapHandle;
        private SystemHandle _commandHandle;
        private SystemHandle _hoverHandle;
        private SystemHandle _interactionHandle;
        private SystemHandle _miracleHandle;
        private SystemHandle _miracleDecayHandle;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Hand Flow World");
            _em = _world.EntityManager;

            _bootstrapHandle = _world.Unmanaged.GetOrCreateSystem<HandBootstrapSystem>();
            _commandHandle = _world.Unmanaged.GetOrCreateSystem<HandCommandProcessingSystem>();
            _hoverHandle = _world.Unmanaged.GetOrCreateSystem<HandHoverSystem>();
            _interactionHandle = _world.Unmanaged.GetOrCreateSystem<HandInteractionSystem>();
            _miracleHandle = _world.Unmanaged.GetOrCreateSystem<HandMiracleSystem>();
            _miracleDecayHandle = _world.Unmanaged.GetOrCreateSystem<HandMiracleDecaySystem>();

            EnsureTimeSingletons();
            UpdateSystem(_bootstrapHandle);
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void HandCommands_UpdateHandState()
        {
            using var handQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<HandSingletonTag>());
            Assert.IsTrue(handQuery.TryGetSingletonEntity<HandSingletonTag>(out var hand));

            var buffer = _em.GetBuffer<HandCommand>(hand);
            buffer.Add(new HandCommand { Type = HandCommand.CommandType.PrimaryDown });

            UpdateSystem(_commandHandle);

            var state = _em.GetComponentData<HandState>(hand);
            Assert.AreEqual(1, state.PrimaryPressed);
            Assert.AreEqual(1, state.PrimaryJustPressed);
        }

        [Test]
        public void HandGrab_RepositionsChunk()
        {
            using var handQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<HandSingletonTag>());
            Assert.IsTrue(handQuery.TryGetSingletonEntity<HandSingletonTag>(out var hand));

            var chunk = _em.CreateEntity(typeof(LocalTransform), typeof(HandInteractable));
            _em.SetComponentData(chunk, LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 0f), quaternion.identity, 1f));
            _em.SetComponentData(chunk, new HandInteractable
            {
                Type = HandInteractableType.ResourceChunk,
                Radius = 2f
            });

            var buffer = _em.GetBuffer<HandCommand>(hand);
            buffer.Add(new HandCommand
            {
                Type = HandCommand.CommandType.SetWorldPosition,
                Float3Param = new float3(0f, 0f, 0f)
            });
            buffer.Add(new HandCommand { Type = HandCommand.CommandType.PrimaryDown });

            UpdateSystem(_commandHandle);
            UpdateSystem(_hoverHandle);
            UpdateSystem(_interactionHandle);

            var handState = _em.GetComponentData<HandState>(hand);
            Assert.AreEqual(chunk, handState.HeldEntity);

            buffer = _em.GetBuffer<HandCommand>(hand);
            buffer.Add(new HandCommand
            {
                Type = HandCommand.CommandType.SetWorldPosition,
                Float3Param = new float3(5f, 0f, 0f)
            });

            UpdateSystem(_commandHandle);
            UpdateSystem(_hoverHandle);
            UpdateSystem(_interactionHandle);

            var transform = _em.GetComponentData<LocalTransform>(chunk);
            Assert.AreEqual(new float3(5f, 0f, 0f), transform.Position);

            buffer = _em.GetBuffer<HandCommand>(hand);
            buffer.Add(new HandCommand { Type = HandCommand.CommandType.PrimaryUp });

            UpdateSystem(_commandHandle);
            UpdateSystem(_interactionHandle);

            handState = _em.GetComponentData<HandState>(hand);
            Assert.AreEqual(Entity.Null, handState.HeldEntity);
        }

        [Test]
        public void HandMiracle_BoostsVillagerMoodAndSpawnsEffect()
        {
            using var handQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<HandSingletonTag>());
            Assert.IsTrue(handQuery.TryGetSingletonEntity<HandSingletonTag>(out var hand));

            var villager = _em.CreateEntity(typeof(LocalTransform), typeof(VillagerMood), typeof(HandInteractable));
            _em.SetComponentData(villager, LocalTransform.FromPositionRotationScale(new float3(2f, 0f, 0f), quaternion.identity, 1f));
            _em.SetComponentData(villager, new VillagerMood { Mood = 20f, TargetMood = 20f, MoodChangeRate = 1f, Wellbeing = 40f });
            _em.SetComponentData(villager, new HandInteractable { Type = HandInteractableType.Villager, Radius = 3f });

            var buffer = _em.GetBuffer<HandCommand>(hand);
            buffer.Add(new HandCommand
            {
                Type = HandCommand.CommandType.SetWorldPosition,
                Float3Param = new float3(2f, 0f, 0f)
            });
            buffer.Add(new HandCommand { Type = HandCommand.CommandType.SecondaryDown });

            UpdateSystem(_commandHandle);
            UpdateSystem(_hoverHandle);
            UpdateSystem(_miracleHandle);

            var mood = _em.GetComponentData<VillagerMood>(villager);
            Assert.Greater(mood.Mood, 20f);

            UpdateSystem(_miracleDecayHandle);

            var effectQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<MiracleEffect>());
            Assert.IsTrue(effectQuery.CalculateEntityCount() > 0);
        }

        private void UpdateSystem(SystemHandle handle)
        {
            _world.Unmanaged.UpdateSystem(handle);
        }

        private void EnsureTimeSingletons()
        {
            if (!_em.CreateEntityQuery(ComponentType.ReadOnly<TimeState>()).TryGetSingletonEntity<TimeState>(out _))
            {
                var time = _em.CreateEntity(typeof(TimeState));
                _em.SetComponentData(time, new TimeState
                {
                    FixedDeltaTime = 1f / 60f,
                    CurrentSpeedMultiplier = 1f,
                    Tick = 0,
                    IsPaused = false
                });
            }

            if (!_em.CreateEntityQuery(ComponentType.ReadOnly<RewindState>()).TryGetSingletonEntity<RewindState>(out _))
            {
                var rewind = _em.CreateEntity(typeof(RewindState));
                _em.SetComponentData(rewind, new RewindState
                {
                    Mode = RewindMode.Record,
                    PlaybackTicksPerSecond = 60f,
                    StartTick = 0,
                    TargetTick = 0,
                    PlaybackTick = 0,
                    ScrubDirection = 0,
                    ScrubSpeedMultiplier = 1f
                });
            }
        }
    }
}


