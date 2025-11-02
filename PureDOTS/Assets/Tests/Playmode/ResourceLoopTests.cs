using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Tests
{
    public class ResourceLoopTests
    {
        private World _world;
        private EntityManager _entityManager;
        private ResourceGatheringTestSystem _gatherSystem;
        private ResourceDepositTestSystem _depositSystem;
        private StorehouseInventoryTestSystem _storeSystem;
        private VillagerJobAssignmentTestSystem _jobSystem;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Resource Loop World");
            _entityManager = _world.EntityManager;

            _world.GetOrCreateSystemManaged<CoreSingletonBootstrapSystem>();
            _gatherSystem = _world.CreateSystemManaged<ResourceGatheringTestSystem>();
            _depositSystem = _world.CreateSystemManaged<ResourceDepositTestSystem>();
            _storeSystem = _world.CreateSystemManaged<StorehouseInventoryTestSystem>();
            _jobSystem = _world.CreateSystemManaged<VillagerJobAssignmentTestSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void ResourceGatheringSystem_DepositsIntoStorehouse()
        {
            // Create time/rewind singletons
            CoreSingletonBootstrapSystem bootstrap = _world.GetExistingSystemManaged<CoreSingletonBootstrapSystem>();
            bootstrap.Update();

            var villager = _entityManager.CreateEntity(typeof(VillagerJob), typeof(VillagerAIState), typeof(VillagerNeeds), typeof(VillagerInventoryItem));
            _entityManager.SetComponentData(villager, new VillagerJob
            {
                Type = VillagerJob.JobType.Gatherer,
                WorksiteEntity = Entity.Null,
                WorkProgress = 0f,
                Productivity = 1f
            });
            _entityManager.SetComponentData(villager, new VillagerAIState
            {
                CurrentGoal = VillagerAIState.Goal.Work,
                CurrentState = VillagerAIState.State.Working,
                TargetEntity = Entity.Null
            });
            _entityManager.SetComponentData(villager, new VillagerNeeds
            {
                Health = 100f,
                MaxHealth = 100f,
                Hunger = 10f,
                Energy = 80f,
                Morale = 70f
            });
            var inventory = _entityManager.AddBuffer<VillagerInventoryItem>(villager);
            inventory.Add(new VillagerInventoryItem
            {
                ResourceTypeId = new FixedString64Bytes("wood"),
                Amount = 50f,
                MaxCarryCapacity = 100f
            });

            var resource = _entityManager.CreateEntity(typeof(ResourceSourceState), typeof(ResourceSourceConfig), typeof(ResourceTypeId), typeof(LocalTransform));
            _entityManager.SetComponentData(resource, new ResourceSourceState { UnitsRemaining = 100f });
            _entityManager.SetComponentData(resource, new ResourceSourceConfig
            {
                GatherRatePerWorker = 10f,
                MaxSimultaneousWorkers = 3,
                RespawnSeconds = 30f,
                Flags = 0
            });
            _entityManager.SetComponentData(resource, new ResourceTypeId { Value = new FixedString64Bytes("wood") });
            _entityManager.SetComponentData(resource, LocalTransform.FromPosition(float3.zero));

            var storehouse = _entityManager.CreateEntity(typeof(StorehouseConfig), typeof(StorehouseInventory), typeof(LocalTransform));
            _entityManager.SetComponentData(storehouse, new StorehouseConfig
            {
                InputRate = 25f,
                OutputRate = 15f,
                ShredRate = 0f,
                MaxShredQueueSize = 4
            });
            _entityManager.SetComponentData(storehouse, new StorehouseInventory
            {
                TotalStored = 0f,
                TotalCapacity = 500f,
                ItemTypeCount = 0,
                IsShredding = 0,
                LastUpdateTick = 0
            });
            _entityManager.SetComponentData(storehouse, LocalTransform.FromPosition(new float3(5f, 0f, 0f)));
            var storeCap = _entityManager.AddBuffer<StorehouseCapacityElement>(storehouse);
            storeCap.Add(new StorehouseCapacityElement
            {
                ResourceTypeId = new FixedString64Bytes("wood"),
                MaxCapacity = 500f
            });
            _entityManager.AddBuffer<StorehouseInventoryItem>(storehouse);

            // Hook villager worksite/target
            var job = _entityManager.GetComponentData<VillagerJob>(villager);
            job.WorksiteEntity = resource;
            _entityManager.SetComponentData(villager, job);

            var ai = _entityManager.GetComponentData<VillagerAIState>(villager);
            ai.TargetEntity = resource;
            _entityManager.SetComponentData(villager, ai);

            // Prepare systems
            _gatherSystem.Update();
            _depositSystem.Update();
            _storeSystem.Update();

            var updatedInventory = _entityManager.GetComponentData<StorehouseInventory>(storehouse);
            Assert.Greater(updatedInventory.TotalStored, 0f);
        }

        [Test]
        public void VillagerJobAssignment_AssignsNearestResource()
        {
            _world.GetExistingSystemManaged<CoreSingletonBootstrapSystem>().Update();

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
            _entityManager.SetComponentData(villager, LocalTransform.FromPosition(new float3(2f, 0f, 0f)));

            var resource = _entityManager.CreateEntity(typeof(ResourceSourceConfig), typeof(ResourceSourceState), typeof(ResourceTypeId), typeof(LocalTransform));
            _entityManager.SetComponentData(resource, new ResourceSourceConfig
            {
                GatherRatePerWorker = 5f,
                MaxSimultaneousWorkers = 3,
                RespawnSeconds = 30f,
                Flags = 0
            });
            _entityManager.SetComponentData(resource, new ResourceSourceState { UnitsRemaining = 200f });
            _entityManager.SetComponentData(resource, new ResourceTypeId { Value = new FixedString64Bytes("wood") });
            _entityManager.SetComponentData(resource, LocalTransform.FromPosition(float3.zero));

            _jobSystem.Update();

            var updatedJob = _entityManager.GetComponentData<VillagerJob>(villager);
            Assert.AreEqual(resource, updatedJob.WorksiteEntity);
        }
    }
}

namespace PureDOTS.Tests
{
    [DisableAutoCreation]
    partial class ResourceGatheringTestSystem : SystemBase
    {
        private ResourceGatheringSystem _impl;

        protected override void OnCreate()
        {
            _impl = new ResourceGatheringSystem();
            _impl.OnCreate(ref CheckedStateRef);
        }

        protected override void OnUpdate()
        {
            _impl.OnUpdate(ref CheckedStateRef);
        }

    }

    [DisableAutoCreation]
    partial class ResourceDepositTestSystem : SystemBase
    {
        private ResourceDepositSystem _impl;

        protected override void OnCreate()
        {
            _impl = new ResourceDepositSystem();
            _impl.OnCreate(ref CheckedStateRef);
        }

        protected override void OnUpdate()
        {
            _impl.OnUpdate(ref CheckedStateRef);
        }

    }

    [DisableAutoCreation]
    partial class StorehouseInventoryTestSystem : SystemBase
    {
        private StorehouseInventorySystem _impl;

        protected override void OnCreate()
        {
            _impl = new StorehouseInventorySystem();
            _impl.OnCreate(ref CheckedStateRef);
        }

        protected override void OnUpdate()
        {
            _impl.OnUpdate(ref CheckedStateRef);
        }
    }

    [DisableAutoCreation]
    partial class VillagerJobAssignmentTestSystem : SystemBase
    {
        private VillagerJobAssignmentSystem _impl;

        protected override void OnCreate()
        {
            _impl = new VillagerJobAssignmentSystem();
            _impl.OnCreate(ref CheckedStateRef);
        }

        protected override void OnUpdate()
        {
            _impl.OnUpdate(ref CheckedStateRef);
        }
    }
}
