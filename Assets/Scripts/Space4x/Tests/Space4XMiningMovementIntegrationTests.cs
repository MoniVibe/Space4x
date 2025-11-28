using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Space4X.Tests.TestHarness;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;

namespace Space4X.Tests
{
    /// <summary>
    /// End-to-end mining integration that validates MiningOrder-driven movement, gathering, and deposit.
    /// </summary>
    public class Space4XMiningMovementIntegrationTests
    {
        private ISystemTestHarness _harness;
        private EntityManager _entityManager;
        private Entity _timeEntity;
        private Entity _rewindEntity;

        [SetUp]
        public void SetUp()
        {
            _harness = new ISystemTestHarness(fixedDelta: 0.2f);
            _entityManager = _harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            CoreSingletonBootstrapSystem.EnsureMiningSpine(_entityManager);

            _timeEntity = _entityManager.CreateEntityQuery(typeof(TimeState)).GetSingletonEntity();
            _rewindEntity = _entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.FixedDeltaTime = 0.2f;
            time.Tick = 0;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);

            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Record;
            _entityManager.SetComponentData(_rewindEntity, rewind);

            _harness.Add<Space4XResourceRegistryPopulationSystem>();
            _harness.Add<VesselAISystem>();
            _harness.Add<VesselTargetingSystem>();
            _harness.Add<VesselMovementSystem>();
            _harness.Add<VesselGatheringSystem>();
            _harness.Add<VesselDepositSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
        }

        [Test]
        public void MiningOrderDrivesMovementGatherAndDeposit()
        {
            var asteroid = CreateAsteroid(120f, ResourceType.Minerals, new float3(12f, 0f, 0f));
            var carrier = CreateCarrier(new float3(0f, 0f, 0f));
            var vessel = CreateVessel(carrier, new float3(-2f, 0f, 0f));

            // Allow registry population and AI assignment
            _harness.Step(4);

            var aiState = _entityManager.GetComponentData<VesselAIState>(vessel);
            Assert.AreNotEqual(Entity.Null, aiState.TargetEntity, "MiningOrder should assign an asteroid target");

            // Run long enough for travel, gathering, and deposit
            _harness.Step(120);

            var storage = _entityManager.GetBuffer<ResourceStorage>(carrier);
            Assert.Greater(storage.Length, 0, "Carrier should have resource storage");
            Assert.Greater(storage[0].Amount, 0f, "Carrier should have received deposited minerals");

            var vesselData = _entityManager.GetComponentData<MiningVessel>(vessel);
            Assert.Less(vesselData.CurrentCargo, 1f, "Vessel cargo should be emptied after deposit");

            aiState = _entityManager.GetComponentData<VesselAIState>(vessel);
            Assert.AreEqual(VesselAIState.Goal.Idle, aiState.CurrentGoal, "Vessel should return to idle after deposit");
        }

        private Entity CreateAsteroid(float units, ResourceType resourceType, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(Asteroid),
                typeof(ResourceSourceState),
                typeof(ResourceSourceConfig),
                typeof(ResourceTypeId),
                typeof(LocalTransform));

            _entityManager.SetComponentData(entity, new Asteroid
            {
                AsteroidId = new Unity.Collections.FixedString64Bytes("AST-1"),
                ResourceType = resourceType,
                ResourceAmount = units,
                MaxResourceAmount = units,
                MiningRate = 12f
            });

            _entityManager.SetComponentData(entity, new ResourceSourceState
            {
                UnitsRemaining = units
            });

            _entityManager.SetComponentData(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = 12f,
                MaxSimultaneousWorkers = 4,
                RespawnSeconds = 0f,
                Flags = 0
            });

            _entityManager.SetComponentData(entity, new ResourceTypeId
            {
                Value = new Unity.Collections.FixedString64Bytes("space4x.resource.minerals")
            });

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private Entity CreateCarrier(float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(Carrier),
                typeof(LocalTransform));

            _entityManager.SetComponentData(entity, new Carrier
            {
                CarrierId = new Unity.Collections.FixedString64Bytes("CARRIER-1"),
                AffiliationEntity = Entity.Null,
                Speed = 4f,
                PatrolCenter = position,
                PatrolRadius = 50f
            });

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));

            var storage = _entityManager.AddBuffer<ResourceStorage>(entity);
            storage.Add(ResourceStorage.Create(ResourceType.Minerals, 200f));
            return entity;
        }

        private Entity CreateVessel(Entity carrierEntity, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(MiningVessel),
                typeof(MiningOrder),
                typeof(VesselAIState),
                typeof(VesselMovement),
                typeof(LocalTransform));

            _entityManager.SetComponentData(entity, new MiningVessel
            {
                VesselId = new Unity.Collections.FixedString64Bytes("VESSEL-1"),
                CarrierEntity = carrierEntity,
                MiningEfficiency = 1f,
                Speed = 6f,
                CargoCapacity = 40f,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });

            _entityManager.SetComponentData(entity, new MiningOrder
            {
                ResourceId = new Unity.Collections.FixedString64Bytes("space4x.resource.minerals"),
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = Entity.Null,
                TargetEntity = Entity.Null,
                IssuedTick = 0
            });

            _entityManager.SetComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.Idle,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            _entityManager.SetComponentData(entity, new VesselMovement
            {
                BaseSpeed = 6f,
                CurrentSpeed = 0f,
                Velocity = float3.zero,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }
    }
}
