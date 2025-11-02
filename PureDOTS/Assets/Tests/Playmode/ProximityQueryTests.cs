using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Tests
{
    public class ProximityQueryTests
    {
        private World _world;
        private EntityManager _entityManager;

        private ProximityQuerySetupTestSystem _setupSystem;
        private ResourceProximityFallbackTestSystem _fallbackSystem;
        private VillagerJobAssignmentTestSystem _jobSystem;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Proximity Query World");
            _entityManager = _world.EntityManager;

            _world.GetOrCreateSystemManaged<CoreSingletonBootstrapSystem>();
            _setupSystem = _world.CreateSystemManaged<ProximityQuerySetupTestSystem>();
            _fallbackSystem = _world.CreateSystemManaged<ResourceProximityFallbackTestSystem>();
            _jobSystem = _world.CreateSystemManaged<VillagerJobAssignmentTestSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void ProximityFallback_AssignsNearestResourceToVillager()
        {
            _world.GetExistingSystemManaged<CoreSingletonBootstrapSystem>().Update();

            // Villager at (10,0,0)
            var villager = _entityManager.CreateEntity(typeof(VillagerJob), typeof(VillagerAIState), typeof(LocalTransform));
            _entityManager.SetComponentData(villager, new VillagerJob
            {
                Type = VillagerJob.JobType.Gatherer,
                WorksiteEntity = Entity.Null,
                WorkProgress = 0f,
                Productivity = 1f
            });
            _entityManager.SetComponentData(villager, new VillagerAIState
            {
                CurrentGoal = VillagerAIState.Goal.None,
                CurrentState = VillagerAIState.State.Idle,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero
            });
            _entityManager.SetComponentData(villager, LocalTransform.FromPosition(new float3(10f, 0f, 0f)));

            // Two resources: one close at (12,0,0) and one far at (100,0,0)
            var near = _entityManager.CreateEntity(typeof(ResourceSourceConfig), typeof(ResourceSourceState), typeof(ResourceTypeId), typeof(LocalTransform));
            _entityManager.SetComponentData(near, new ResourceSourceConfig { GatherRatePerWorker = 5f, MaxSimultaneousWorkers = 1, RespawnSeconds = 0f, Flags = 0 });
            _entityManager.SetComponentData(near, new ResourceSourceState { UnitsRemaining = 50f });
            _entityManager.SetComponentData(near, new ResourceTypeId { Value = new FixedString64Bytes("wood") });
            _entityManager.SetComponentData(near, LocalTransform.FromPosition(new float3(12f, 0f, 0f)));

            var far = _entityManager.CreateEntity(typeof(ResourceSourceConfig), typeof(ResourceSourceState), typeof(ResourceTypeId), typeof(LocalTransform));
            _entityManager.SetComponentData(far, new ResourceSourceConfig { GatherRatePerWorker = 5f, MaxSimultaneousWorkers = 1, RespawnSeconds = 0f, Flags = 0 });
            _entityManager.SetComponentData(far, new ResourceSourceState { UnitsRemaining = 50f });
            _entityManager.SetComponentData(far, new ResourceTypeId { Value = new FixedString64Bytes("wood") });
            _entityManager.SetComponentData(far, LocalTransform.FromPosition(new float3(100f, 0f, 0f)));

            // Run setup (adds proximity components), then fallback provider, then assignment
            _setupSystem.Update();
            _fallbackSystem.Update();
            _jobSystem.Update();

            var job = _entityManager.GetComponentData<VillagerJob>(villager);
            Assert.AreEqual(near, job.WorksiteEntity);
        }
    }

    // Test wrappers for ISystem implementations
    [DisableAutoCreation]
    partial class ProximityQuerySetupTestSystem : SystemBase
    {
        private ProximityQuerySetupSystem _impl;

        protected override void OnCreate()
        {
            _impl = new ProximityQuerySetupSystem();
            _impl.OnCreate(ref CheckedStateRef);
        }

        protected override void OnUpdate()
        {
            _impl.OnUpdate(ref CheckedStateRef);
        }
    }

    [DisableAutoCreation]
    partial class ResourceProximityFallbackTestSystem : SystemBase
    {
        private ResourceProximityFallbackSystem _impl;

        protected override void OnCreate()
        {
            _impl = new ResourceProximityFallbackSystem();
            _impl.OnCreate(ref CheckedStateRef);
        }

        protected override void OnUpdate()
        {
            _impl.OnUpdate(ref CheckedStateRef);
        }
    }
}


