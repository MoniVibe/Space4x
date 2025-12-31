using NUnit.Framework;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Launch;
using PureDOTS.Runtime.Physics;
using PureDOTS.Systems;
using Space4X.Adapters.Launch;
using Space4X.Authoring;
using Space4X.Runtime;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Tests.PlayMode
{
    public class Space4XLaunchCollisionAdapterTests
    {
        private World _world;
        private EntityManager _entityManager;
        private EndSimulationEntityCommandBufferSystem _endSimEcb;
        private SystemHandle _adapterHandle;
        private Entity _timeEntity;
        private Entity _rewindEntity;
        private Entity _physicsConfigEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World(nameof(Space4XLaunchCollisionAdapterTests));
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            _endSimEcb = _world.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            _adapterHandle = _world.GetOrCreateSystem<Space4XLauncherCollisionAdapter>();

            EnsureTimeState();
            EnsureRewindState();
            EnsurePhysicsConfig();
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
        public void TorpedoImpact_EmitsDamageAndConsumesPayload()
        {
            var launcher = _entityManager.CreateEntity(typeof(Space4XLauncherConfig));
            _entityManager.SetComponentData(launcher, new Space4XLauncherConfig
            {
                LaunchType = Space4XLaunchType.Torpedo,
                MaxRange = 100f
            });

            var target = _entityManager.CreateEntity();
            var payload = _entityManager.CreateEntity(typeof(LaunchedProjectileTag));
            _entityManager.SetComponentData(payload, new LaunchedProjectileTag
            {
                LaunchTick = 1,
                SourceLauncher = launcher
            });

            var collisions = _entityManager.AddBuffer<PhysicsCollisionEventElement>(payload);
            collisions.Add(new PhysicsCollisionEventElement
            {
                OtherEntity = target,
                ContactPoint = float3.zero,
                ContactNormal = new float3(0f, 1f, 0f),
                Impulse = 12f,
                Tick = 1,
                EventType = PhysicsCollisionEventType.Collision
            });

            UpdateSystem(_adapterHandle);
            _endSimEcb.Update();

            Assert.IsFalse(_entityManager.HasComponent<LaunchedProjectileTag>(payload), "Payload should be consumed.");
            Assert.IsTrue(_entityManager.HasBuffer<DamageEvent>(target), "Target should receive damage.");

            var damageBuffer = _entityManager.GetBuffer<DamageEvent>(target);
            Assert.AreEqual(1, damageBuffer.Length, "Exactly one damage event expected.");
            Assert.AreEqual(launcher, damageBuffer[0].SourceEntity);
            Assert.AreEqual(target, damageBuffer[0].TargetEntity);
            Assert.Greater(damageBuffer[0].RawDamage, 0f);
        }

        [Test]
        public void CargoPodTrigger_EmitsDeliveryRequestAndConsumesPayload()
        {
            var launcher = _entityManager.CreateEntity(typeof(Space4XLauncherConfig));
            _entityManager.SetComponentData(launcher, new Space4XLauncherConfig
            {
                LaunchType = Space4XLaunchType.CargoPod,
                MaxRange = 100f
            });

            var target = _entityManager.CreateEntity();
            var payload = _entityManager.CreateEntity(typeof(LaunchedProjectileTag));
            _entityManager.SetComponentData(payload, new LaunchedProjectileTag
            {
                LaunchTick = 1,
                SourceLauncher = launcher
            });

            var collisions = _entityManager.AddBuffer<PhysicsCollisionEventElement>(payload);
            collisions.Add(new PhysicsCollisionEventElement
            {
                OtherEntity = target,
                ContactPoint = float3.zero,
                ContactNormal = new float3(0f, 1f, 0f),
                Impulse = 0f,
                Tick = 1,
                EventType = PhysicsCollisionEventType.TriggerEnter
            });

            UpdateSystem(_adapterHandle);
            _endSimEcb.Update();

            Assert.IsFalse(_entityManager.HasComponent<LaunchedProjectileTag>(payload), "Payload should be consumed.");

            var streamEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XLaunchRequestStream>())
                .GetSingletonEntity();
            var requests = _entityManager.GetBuffer<Space4XCargoDeliveryRequest>(streamEntity);

            Assert.AreEqual(1, requests.Length, "One cargo delivery request expected.");
            Assert.AreEqual(launcher, requests[0].SourceLauncher);
            Assert.AreEqual(payload, requests[0].Payload);
            Assert.AreEqual(target, requests[0].Target);
            Assert.AreEqual(1u, requests[0].Tick);
        }

        private void EnsureTimeState()
        {
            var query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>());
            _timeEntity = query.GetSingletonEntity();
            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.IsPaused = false;
            time.FixedDeltaTime = 1f / 60f;
            time.Tick = 1;
            _entityManager.SetComponentData(_timeEntity, time);
        }

        private void EnsureRewindState()
        {
            var query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>());
            _rewindEntity = query.GetSingletonEntity();
            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Record;
            _entityManager.SetComponentData(_rewindEntity, rewind);
        }

        private void EnsurePhysicsConfig()
        {
            var query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<PhysicsConfig>());
            if (query.IsEmptyIgnoreFilter)
            {
                _physicsConfigEntity = _entityManager.CreateEntity(typeof(PhysicsConfig));
            }
            else
            {
                _physicsConfigEntity = query.GetSingletonEntity();
            }

            var config = _entityManager.GetComponentData<PhysicsConfig>(_physicsConfigEntity);
            config.ProviderId = PhysicsProviderIds.Entities;
            config.EnableSpace4XPhysics = 1;
            config.PostRewindSettleFrames = 0;
            config.LastRewindCompleteTick = 0;
            _entityManager.SetComponentData(_physicsConfigEntity, config);
        }

        private void UpdateSystem(SystemHandle handle)
        {
            handle.Update(_world.Unmanaged);
        }
    }
}
