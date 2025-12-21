#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    public class FleetInterceptSystemsTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("FleetInterceptSystemsTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var timeState = _entityManager.GetComponentData<TimeState>(timeEntity);
            timeState.FixedDeltaTime = 1f;
            timeState.IsPaused = false;
            timeState.Tick = 0;
            _entityManager.SetComponentData(timeEntity, timeState);

            var rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();
            var rewindState = _entityManager.GetComponentData<RewindState>(rewindEntity);
            rewindState.Mode = RewindMode.Record;
            _entityManager.SetComponentData(rewindEntity, rewindState);

            CoreSingletonBootstrapSystem.EnsureFleetInterceptQueue(_entityManager);
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
        public void FleetBroadcastUpdatesTickVelocityAndResidency()
        {
            var target = _entityManager.CreateEntity(typeof(FleetMovementBroadcast), typeof(LocalTransform), typeof(SpatialGridResidency));
            _entityManager.SetComponentData(target, new FleetMovementBroadcast
            {
                Position = float3.zero,
                Velocity = float3.zero,
                LastUpdateTick = 0,
                AllowsInterception = 1,
                TechTier = 1
            });
            _entityManager.SetComponentData(target, LocalTransform.FromPositionRotationScale(new float3(2f, 0f, 0f), quaternion.identity, 1f));
            _entityManager.SetComponentData(target, new SpatialGridResidency
            {
                CellId = 0,
                LastPosition = float3.zero,
                Version = 1
            });
            _entityManager.SetComponentData(target, new FleetKinematics { Velocity = new float3(0f, 0f, 4f) });

            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var time = _entityManager.GetComponentData<TimeState>(timeEntity);
            time.Tick = 10;
            _entityManager.SetComponentData(timeEntity, time);

            _world.GetOrCreateSystem<FleetBroadcastSystem>().Update(_world.Unmanaged);

            var broadcast = _entityManager.GetComponentData<FleetMovementBroadcast>(target);
            Assert.AreEqual(10u, broadcast.LastUpdateTick);
            Assert.AreEqual(new float3(0f, 0f, 4f), broadcast.Velocity);
            Assert.AreEqual(new float3(2f, 0f, 0f), broadcast.Position);

            var residency = _entityManager.GetComponentData<SpatialGridResidency>(target);
            Assert.AreEqual(new float3(2f, 0f, 0f), residency.LastPosition);
            Assert.AreEqual(1, residency.Version);
        }

        [Test]
        public void InterceptPathfindingComputesPredictiveCourseWhenTechAllows()
        {
            var target = _entityManager.CreateEntity(typeof(FleetMovementBroadcast), typeof(LocalTransform));
            _entityManager.SetComponentData(target, new FleetMovementBroadcast
            {
                Position = float3.zero,
                Velocity = new float3(1f, 0f, 0f),
                LastUpdateTick = 0,
                AllowsInterception = 1,
                TechTier = 1
            });
            _entityManager.SetComponentData(target, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

            var requester = _entityManager.CreateEntity(typeof(InterceptCapability), typeof(LocalTransform));
            _entityManager.SetComponentData(requester, new InterceptCapability
            {
                MaxSpeed = 5f,
                TechTier = 1,
                AllowIntercept = 1
            });
            _entityManager.SetComponentData(requester, LocalTransform.FromPositionRotationScale(new float3(-10f, 0f, 0f), quaternion.identity, 1f));

            var queueEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XFleetInterceptQueue>()).GetSingletonEntity();
            var requests = _entityManager.GetBuffer<InterceptRequest>(queueEntity);
            requests.Add(new InterceptRequest
            {
                Requester = requester,
                Target = target,
                Priority = 1,
                RequestTick = 0,
                RequireRendezvous = 0
            });

            _world.GetOrCreateSystem<FleetBroadcastSystem>().Update(_world.Unmanaged);
            _world.GetOrCreateSystem<InterceptPathfindingSystem>().Update(_world.Unmanaged);

            var course = _entityManager.GetComponentData<InterceptCourse>(requester);
            Assert.AreEqual((byte)1, course.UsesInterception);
            Assert.AreEqual(target, course.TargetFleet);
            Assert.AreEqual(3u, course.EstimatedInterceptTick); // 10 units gap with speed delta 4 => 2.5s => ceil to 3 ticks
            Assert.That(course.InterceptPoint.x, Is.GreaterThan(2.4f).And.LessThan(2.6f));

            var log = _entityManager.GetBuffer<FleetInterceptCommandLogEntry>(queueEntity);
            Assert.AreEqual(1, log.Length);
            Assert.AreEqual(InterceptMode.Intercept, log[0].Mode);
            Assert.AreEqual(requester, log[0].Requester);
            Assert.AreEqual(target, log[0].Target);
        }

        [Test]
        public void InterceptPathfindingFallsBackToRendezvousWhenInterceptDisabled()
        {
            var target = _entityManager.CreateEntity(typeof(FleetMovementBroadcast), typeof(LocalTransform));
            _entityManager.SetComponentData(target, new FleetMovementBroadcast
            {
                Position = new float3(5f, 0f, 0f),
                Velocity = float3.zero,
                LastUpdateTick = 0,
                AllowsInterception = 0,
                TechTier = 1
            });
            _entityManager.SetComponentData(target, LocalTransform.FromPositionRotationScale(new float3(5f, 0f, 0f), quaternion.identity, 1f));

            var requester = _entityManager.CreateEntity(typeof(InterceptCapability), typeof(LocalTransform));
            _entityManager.SetComponentData(requester, new InterceptCapability
            {
                MaxSpeed = 2f,
                TechTier = 1,
                AllowIntercept = 1
            });
            _entityManager.SetComponentData(requester, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

            var queueEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XFleetInterceptQueue>()).GetSingletonEntity();
            var requests = _entityManager.GetBuffer<InterceptRequest>(queueEntity);
            requests.Add(new InterceptRequest
            {
                Requester = requester,
                Target = target,
                Priority = 1,
                RequestTick = 0,
                RequireRendezvous = 0
            });

            _world.GetOrCreateSystem<FleetBroadcastSystem>().Update(_world.Unmanaged);
            _world.GetOrCreateSystem<InterceptPathfindingSystem>().Update(_world.Unmanaged);
            _world.GetOrCreateSystem<RendezvousCoordinationSystem>().Update(_world.Unmanaged);

            var course = _entityManager.GetComponentData<InterceptCourse>(requester);
            Assert.AreEqual((byte)0, course.UsesInterception);
            Assert.AreEqual(target, course.TargetFleet);
            Assert.AreEqual(new float3(5f, 0f, 0f), course.InterceptPoint);
            Assert.AreEqual(0u, course.EstimatedInterceptTick);

            var log = _entityManager.GetBuffer<FleetInterceptCommandLogEntry>(queueEntity);
            Assert.AreEqual(1, log.Length);
            Assert.AreEqual(InterceptMode.Rendezvous, log[0].Mode);
        }

        [Test]
        public void FleetInterceptRequestSystemSelectsNearestFleetAndCreatesCourse()
        {
            // Setup targets
            var nearTarget = _entityManager.CreateEntity(typeof(FleetMovementBroadcast), typeof(LocalTransform));
            _entityManager.SetComponentData(nearTarget, new FleetMovementBroadcast
            {
                Position = new float3(10f, 0f, 0f),
                Velocity = new float3(0f, 0f, 0f),
                LastUpdateTick = 0,
                AllowsInterception = 1,
                TechTier = 1
            });
            _entityManager.SetComponentData(nearTarget, LocalTransform.FromPositionRotationScale(new float3(10f, 0f, 0f), quaternion.identity, 1f));

            var farTarget = _entityManager.CreateEntity(typeof(FleetMovementBroadcast), typeof(LocalTransform));
            _entityManager.SetComponentData(farTarget, new FleetMovementBroadcast
            {
                Position = new float3(100f, 0f, 0f),
                Velocity = float3.zero,
                LastUpdateTick = 0,
                AllowsInterception = 1,
                TechTier = 1
            });
            _entityManager.SetComponentData(farTarget, LocalTransform.FromPositionRotationScale(new float3(100f, 0f, 0f), quaternion.identity, 1f));

            // Requester with intercept capability
            var requester = _entityManager.CreateEntity(typeof(InterceptCapability), typeof(LocalTransform));
            _entityManager.SetComponentData(requester, new InterceptCapability
            {
                MaxSpeed = 6f,
                TechTier = 1,
                AllowIntercept = 1
            });
            _entityManager.SetComponentData(requester, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

            // Run request + path systems
            var queueEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XFleetInterceptQueue>()).GetSingletonEntity();
            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var time = _entityManager.GetComponentData<TimeState>(timeEntity);
            time.Tick = 5;
            _entityManager.SetComponentData(timeEntity, time);

            _world.GetOrCreateSystem<FleetInterceptRequestSystem>().Update(_world.Unmanaged);
            _world.GetOrCreateSystem<InterceptPathfindingSystem>().Update(_world.Unmanaged);

            var course = _entityManager.GetComponentData<InterceptCourse>(requester);
            Assert.AreEqual(nearTarget, course.TargetFleet);
            Assert.AreEqual((byte)1, course.UsesInterception);
            Assert.Greater(course.EstimatedInterceptTick, 0u);

            var log = _entityManager.GetBuffer<FleetInterceptCommandLogEntry>(queueEntity);
            Assert.AreEqual(1, log.Length);
            Assert.AreEqual(nearTarget, log[0].Target);
            Assert.AreEqual(requester, log[0].Requester);
        }
    }
}
#endif
