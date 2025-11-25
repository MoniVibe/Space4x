using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    public class Space4XVesselMovementTests
    {
        private World _world;
        private EntityManager _entityManager;
        private Entity _timeEntity;
        private Entity _rewindEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XVesselMovementTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            CoreSingletonBootstrapSystem.EnsureMiningSpine(_entityManager);

            _timeEntity = _entityManager.CreateEntityQuery(typeof(TimeState)).GetSingletonEntity();
            _rewindEntity = _entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        private Entity CreateAsteroid(float resourceAmount, ResourceType resourceType, float3 position)
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.AddComponentData(entity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes("ASTEROID-1"),
                ResourceType = resourceType,
                ResourceAmount = resourceAmount,
                MaxResourceAmount = resourceAmount,
                MiningRate = 10f
            });
            _entityManager.AddComponentData(entity, new ResourceSourceState
            {
                UnitsRemaining = resourceAmount
            });
            _entityManager.AddComponentData(entity, new ResourceTypeId
            {
                Value = resourceType
            });
            return entity;
        }

        private Entity CreateVessel(float cargoCapacity, Entity carrierEntity, float3 position)
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.AddComponentData(entity, new MiningVessel
            {
                VesselId = new FixedString64Bytes("VESSEL-1"),
                CarrierEntity = carrierEntity,
                MiningEfficiency = 1f,
                Speed = 5f,
                CargoCapacity = cargoCapacity,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });
            _entityManager.AddComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.Idle,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });
            _entityManager.AddComponentData(entity, new VesselMovement
            {
                BaseSpeed = 5f,
                CurrentSpeed = 5f,
                Velocity = float3.zero,
                IsMoving = 0,
                LastMoveTick = 0
            });
            _entityManager.AddComponentData(entity, new MiningOrder
            {
                ResourceId = new FixedString64Bytes("space4x.resource.minerals"),
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = Entity.Null,
                TargetEntity = Entity.Null,
                IssuedTick = 0
            });
            return entity;
        }

        private Entity CreateCarrier(float3 position)
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            _entityManager.AddComponentData(entity, new Carrier
            {
                CarrierId = new FixedString64Bytes("CARRIER-1"),
                AffiliationEntity = Entity.Null,
                Speed = 2f,
                PatrolCenter = position,
                PatrolRadius = 50f
            });
            _entityManager.AddBuffer<ResourceStorage>(entity);
            return entity;
        }

        [Test]
        public void VesselMovesToAsteroidWithMiningOrder()
        {
            var asteroid = CreateAsteroid(100f, ResourceType.Minerals, new float3(20f, 0f, 0f));
            var carrier = CreateCarrier(new float3(0f, 0f, 0f));
            var vessel = CreateVessel(50f, carrier, new float3(0f, 0f, 0f));

            // Set MiningOrder target
            var miningOrder = _entityManager.GetComponentData<MiningOrder>(vessel);
            miningOrder.TargetEntity = asteroid;
            miningOrder.Status = MiningOrderStatus.Active;
            _entityManager.SetComponentData(vessel, miningOrder);

            // Register asteroid in resource registry
            var registryEntity = _entityManager.CreateEntity();
            _entityManager.AddComponent<ResourceRegistry>(registryEntity);
            var registryBuffer = _entityManager.AddBuffer<ResourceRegistryEntry>(registryEntity);
            registryBuffer.Add(new ResourceRegistryEntry
            {
                SourceEntity = asteroid,
                Position = new float3(20f, 0f, 0f),
                ResourceTypeIndex = 0,
                Tier = ResourceTier.Raw
            });

            var vesselAISystem = _world.GetOrCreateSystemManaged<VesselAISystem>();
            var vesselTargetingSystem = _world.GetOrCreateSystemManaged<VesselTargetingSystem>();
            var vesselMovementSystem = _world.GetOrCreateSystemManaged<VesselMovementSystem>();

            // Run systems
            vesselAISystem.Update(_world.Unmanaged);
            vesselTargetingSystem.Update(_world.Unmanaged);
            vesselMovementSystem.Update(_world.Unmanaged);

            // Advance time
            var timeState = _entityManager.GetComponentData<TimeState>(_timeEntity);
            timeState.Tick = 1;
            timeState.FixedDeltaTime = 0.1f;
            _entityManager.SetComponentData(_timeEntity, timeState);

            // Run movement again
            vesselMovementSystem.Update(_world.Unmanaged);

            // Verify vessel moved toward asteroid
            var transform = _entityManager.GetComponentData<LocalTransform>(vessel);
            Assert.Greater(transform.Position.x, 0f, "Vessel should have moved toward asteroid");
        }

        [Test]
        public void VesselReturnsToCarrierAfterCargoFull()
        {
            var carrier = CreateCarrier(new float3(0f, 0f, 0f));
            var vessel = CreateVessel(50f, carrier, new float3(10f, 0f, 0f));

            // Set vessel cargo to full
            var miningVessel = _entityManager.GetComponentData<MiningVessel>(vessel);
            miningVessel.CurrentCargo = miningVessel.CargoCapacity * 0.95f;
            _entityManager.SetComponentData(vessel, miningVessel);

            // Set AI state to returning
            var aiState = _entityManager.GetComponentData<VesselAIState>(vessel);
            aiState.CurrentState = VesselAIState.State.Returning;
            aiState.CurrentGoal = VesselAIState.Goal.Returning;
            aiState.TargetEntity = carrier;
            _entityManager.SetComponentData(vessel, aiState);

            var vesselTargetingSystem = _world.GetOrCreateSystemManaged<VesselTargetingSystem>();
            var vesselMovementSystem = _world.GetOrCreateSystemManaged<VesselMovementSystem>();

            // Run systems
            vesselTargetingSystem.Update(_world.Unmanaged);
            vesselMovementSystem.Update(_world.Unmanaged);

            // Advance time
            var timeState = _entityManager.GetComponentData<TimeState>(_timeEntity);
            timeState.Tick = 1;
            timeState.FixedDeltaTime = 0.1f;
            _entityManager.SetComponentData(_timeEntity, timeState);

            // Run movement again
            vesselMovementSystem.Update(_world.Unmanaged);

            // Verify vessel moved toward carrier
            var transform = _entityManager.GetComponentData<LocalTransform>(vessel);
            Assert.Less(transform.Position.x, 10f, "Vessel should have moved toward carrier");
        }
    }
}

