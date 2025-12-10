using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Systems.AI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Runtime;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;

namespace Space4X.Tests.PlayMode
{
    /// <summary>
    /// Tests that verify rewind determinism for the mining → haul loop.
    /// Ensures that resource counts and state replay identically after rewinding.
    /// </summary>
    public class Space4XRewindDeterminismTests
    {
        private World _world;
        private EntityManager _entityManager;

        private Entity _timeEntity;
        private Entity _rewindEntity;
        private Entity _telemetryStream;
        private Entity _spineEntity;

        private SystemHandle _rewindableHandle;
        private SystemHandle _miningSystemHandle;
        private SystemHandle _yieldSpawnBridgeHandle;
        private SystemHandle _spawnHandle;
        private SystemHandle _pickupHandle;
        private SystemHandle _telemetryHandle;
        private SystemHandle _recordHandle;
        private SystemHandle _playbackHandle;

        [SetUp]
        public void SetUp()
        {
            _world = new World("RewindDeterminismTests");
            _entityManager = _world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            var spineBootstrap = _world.GetOrCreateSystem<Space4XMiningTimeSpineBootstrapSystem>();
            spineBootstrap.Update(_world.Unmanaged);

            _rewindableHandle = _world.GetOrCreateSystem<Space4XMiningRewindableSystem>();
            _miningSystemHandle = _world.GetOrCreateSystem<Space4XMinerMiningSystem>();
            _yieldSpawnBridgeHandle = _world.GetOrCreateSystem<Space4XMiningYieldSpawnBridgeSystem>();
            _spawnHandle = _world.GetOrCreateSystem<MiningResourceSpawnSystem>();
            _pickupHandle = _world.GetOrCreateSystem<CarrierPickupSystem>();
            _telemetryHandle = _world.GetOrCreateSystem<Space4XMiningTelemetrySystem>();
            _recordHandle = _world.GetOrCreateSystem<Space4XMiningTimeSpineRecordSystem>();
            _playbackHandle = _world.GetOrCreateSystem<Space4XMiningTimeSpinePlaybackSystem>();

            _timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            _rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();
            _spineEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<Space4XMiningTimeSpine>()).GetSingletonEntity();

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 0;
            time.FixedDeltaTime = 1f;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);

            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Record;
            rewind.PlaybackTicksPerSecond = HistorySettingsDefaults.DefaultTicksPerSecond;
            _entityManager.SetComponentData(_rewindEntity, rewind);

            EnsureTelemetryStream();
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
        public void RewindDuringTransfer_ResourceCountsReplayIdentically()
        {
            // Setup: Create asteroid, mining vessel, and carrier
            var asteroid = CreateAsteroid(100f, ResourceType.Minerals, new float3(0f, 0f, 0f));
            var carrier = CreateCarrier(new float3(5f, 0f, 0f));
            var vessel = CreateMiningVessel(asteroid, new float3(0.5f, 0f, 0f));

            // Record initial state
            float initialCarrierStorage = GetCarrierStorage(carrier);
            float initialVesselCargo = GetVesselCargo(vessel);
            float initialTelemetry = GetTelemetryOreInHold();

            // Simulate mining for 3 ticks
            AdvanceAndUpdate(3);

            // Capture state after 3 ticks
            float after3TicksCarrierStorage = GetCarrierStorage(carrier);
            float after3TicksVesselCargo = GetVesselCargo(vessel);
            float after3TicksTelemetry = GetTelemetryOreInHold();

            // Verify state changed
            Assert.Greater(after3TicksCarrierStorage, initialCarrierStorage, "Carrier storage should increase after mining");
            Assert.Greater(after3TicksTelemetry, initialTelemetry, "Telemetry should increase after mining");

            // Rewind to tick 1
            RewindToTick(1);

            // Verify state matches tick 1
            float rewindCarrierStorage = GetCarrierStorage(carrier);
            float rewindVesselCargo = GetVesselCargo(vessel);
            float rewindTelemetry = GetTelemetryOreInHold();

            // Advance forward again to tick 3
            AdvanceToTick(3);

            // Verify state matches original tick 3 state
            float finalCarrierStorage = GetCarrierStorage(carrier);
            float finalVesselCargo = GetVesselCargo(vessel);
            float finalTelemetry = GetTelemetryOreInHold();

            Assert.AreEqual(after3TicksCarrierStorage, finalCarrierStorage, 0.01f, 
                "Carrier storage after rewind+replay should match original");
            Assert.AreEqual(after3TicksVesselCargo, finalVesselCargo, 0.01f,
                "Vessel cargo after rewind+replay should match original");
            Assert.AreEqual(after3TicksTelemetry, finalTelemetry, 0.01f,
                "Telemetry after rewind+replay should match original");
        }

        [Test]
        public void RewindDuringPickup_StateReplaysIdentically()
        {
            // Setup: Create carrier and spawned resource ready for pickup
            var carrier = CreateCarrier(new float3(0f, 0f, 0f));
            var spawn = CreateSpawn(ResourceType.Minerals, 25f, new float3(1f, 0f, 0f));

            // Record initial state
            float initialCarrierStorage = GetCarrierStorage(carrier);
            int initialSpawnCount = GetSpawnResourceCount();

            // Advance one tick to trigger pickup
            AdvanceAndUpdate(1);

            // Capture state after pickup
            float afterPickupCarrierStorage = GetCarrierStorage(carrier);
            int afterPickupSpawnCount = GetSpawnResourceCount();

            // Verify pickup occurred
            Assert.Greater(afterPickupCarrierStorage, initialCarrierStorage, "Carrier should have picked up resources");
            Assert.Less(afterPickupSpawnCount, initialSpawnCount, "Spawn resource should be consumed");

            // Rewind to tick 0
            RewindToTick(0);

            // Verify state restored
            float rewindCarrierStorage = GetCarrierStorage(carrier);
            int rewindSpawnCount = GetSpawnResourceCount();

            Assert.AreEqual(initialCarrierStorage, rewindCarrierStorage, 0.01f,
                "Carrier storage should be restored after rewind");
            Assert.AreEqual(initialSpawnCount, rewindSpawnCount,
                "Spawn resource count should be restored after rewind");

            // Advance forward again
            AdvanceToTick(1);

            // Verify state matches original
            float finalCarrierStorage = GetCarrierStorage(carrier);
            int finalSpawnCount = GetSpawnResourceCount();

            Assert.AreEqual(afterPickupCarrierStorage, finalCarrierStorage, 0.01f,
                "Carrier storage after rewind+replay should match original");
            Assert.AreEqual(afterPickupSpawnCount, finalSpawnCount,
                "Spawn count after rewind+replay should match original");
        }

        [Test]
        public void MultipleRewinds_StateRemainsDeterministic()
        {
            // Setup: Create asteroid, vessel, and carrier
            var asteroid = CreateAsteroid(200f, ResourceType.Minerals, new float3(0f, 0f, 0f));
            var carrier = CreateCarrier(new float3(5f, 0f, 0f));
            var vessel = CreateMiningVessel(asteroid, new float3(0.5f, 0f, 0f));

            // Advance to tick 5
            AdvanceAndUpdate(5);
            float stateAtTick5 = GetCarrierStorage(carrier);

            // Rewind to tick 2
            RewindToTick(2);
            AdvanceToTick(5);
            float stateAfterRewind1 = GetCarrierStorage(carrier);

            // Rewind again to tick 3
            RewindToTick(3);
            AdvanceToTick(5);
            float stateAfterRewind2 = GetCarrierStorage(carrier);

            // All states should match
            Assert.AreEqual(stateAtTick5, stateAfterRewind1, 0.01f,
                "State after first rewind should match original");
            Assert.AreEqual(stateAtTick5, stateAfterRewind2, 0.01f,
                "State after second rewind should match original");
        }

        [Test]
        public void RewindPreservesAsteroidResourceAmount()
        {
            // Setup: Create asteroid and vessel
            var asteroid = CreateAsteroid(100f, ResourceType.Minerals, new float3(0f, 0f, 0f));
            var vessel = CreateMiningVessel(asteroid, new float3(0.5f, 0f, 0f));

            // Record initial asteroid amount
            float initialAmount = GetAsteroidResourceAmount(asteroid);

            // Mine for 3 ticks
            AdvanceAndUpdate(3);

            // Verify asteroid depleted
            float afterMiningAmount = GetAsteroidResourceAmount(asteroid);
            Assert.Less(afterMiningAmount, initialAmount, "Asteroid should be depleted after mining");

            // Rewind to tick 0
            RewindToTick(0);

            // Verify asteroid restored
            float rewindAmount = GetAsteroidResourceAmount(asteroid);
            Assert.AreEqual(initialAmount, rewindAmount, 0.01f,
                "Asteroid resource amount should be restored after rewind");

            // Advance forward again
            AdvanceToTick(3);

            // Verify asteroid matches original depleted state
            float finalAmount = GetAsteroidResourceAmount(asteroid);
            Assert.AreEqual(afterMiningAmount, finalAmount, 0.01f,
                "Asteroid resource amount after rewind+replay should match original");
        }

        private Entity CreateAsteroid(float units, ResourceType resourceType, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(Asteroid),
                typeof(ResourceSourceState),
                typeof(ResourceSourceConfig),
                typeof(ResourceTypeId),
                typeof(LocalTransform),
                typeof(LastRecordedTick));

            _entityManager.SetComponentData(entity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes("AST-1"),
                ResourceAmount = units,
                MaxResourceAmount = units,
                ResourceType = resourceType,
                MiningRate = 20f
            });

            _entityManager.SetComponentData(entity, new ResourceSourceState
            {
                UnitsRemaining = units
            });

            _entityManager.SetComponentData(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = 20f,
                MaxSimultaneousWorkers = 4,
                RespawnSeconds = 0f,
                Flags = 0
            });

            var resourceId = new FixedString64Bytes("space4x.resource.minerals");
            _entityManager.SetComponentData(entity, new ResourceTypeId { Value = resourceId });

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            _entityManager.SetComponentData(entity, new LastRecordedTick { Tick = 0 });
            _entityManager.AddComponent<RewindableTag>(entity);
            return entity;
        }

        private Entity CreateMiningVessel(Entity targetAsteroid, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(MiningVessel),
                typeof(MiningOrder),
                typeof(MiningState),
                typeof(MiningYield),
                typeof(VesselAIState),
                typeof(LocalTransform),
                typeof(SpawnResourceRequest));

            var resourceId = new FixedString64Bytes("space4x.resource.minerals");

            _entityManager.SetComponentData(entity, new MiningVessel
            {
                VesselId = new FixedString64Bytes("VES-1"),
                CarrierEntity = Entity.Null,
                MiningEfficiency = 1f,
                Speed = 0f,
                CargoCapacity = 100f,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });

            _entityManager.SetComponentData(entity, new MiningOrder
            {
                ResourceId = resourceId,
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Active,
                PreferredTarget = Entity.Null,
                TargetEntity = targetAsteroid,
                IssuedTick = 0
            });

            _entityManager.SetComponentData(entity, new MiningState
            {
                Phase = MiningPhase.Mining,
                ActiveTarget = targetAsteroid,
                MiningTimer = 0f,
                TickInterval = 1f
            });

            _entityManager.SetComponentData(entity, new MiningYield
            {
                ResourceId = resourceId,
                PendingAmount = 0f,
                SpawnThreshold = 20f,
                SpawnReady = 0
            });

            _entityManager.SetComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Mining,
                CurrentGoal = VesselAIState.Goal.Mining,
                TargetEntity = targetAsteroid,
                TargetPosition = position,
                StateTimer = 0f,
                StateStartTick = 0
            });

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            _entityManager.GetBuffer<SpawnResourceRequest>(entity);
            _entityManager.AddComponent<RewindableTag>(entity);
            return entity;
        }

        private Entity CreateCarrier(float3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(Carrier), typeof(LocalTransform), typeof(ResourceStorage));
            _entityManager.SetComponentData(entity, new Carrier
            {
                CarrierId = new FixedString64Bytes("CARRIER-1"),
                AffiliationEntity = Entity.Null,
                Speed = 0f,
                PatrolCenter = position,
                PatrolRadius = 0f
            });

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            var storage = _entityManager.GetBuffer<ResourceStorage>(entity);
            storage.Add(ResourceStorage.Create(ResourceType.Minerals));
            _entityManager.AddComponent<RewindableTag>(entity);
            return entity;
        }

        private Entity CreateSpawn(ResourceType type, float amount, float3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(SpawnResource), typeof(LocalTransform));
            _entityManager.SetComponentData(entity, new SpawnResource
            {
                Type = type,
                Amount = amount,
                SourceEntity = Entity.Null,
                SpawnTick = 0
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private void AdvanceAndUpdate(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                AdvanceTick();
                UpdateSystem(_rewindableHandle);
                UpdateSystem(_miningSystemHandle);
                UpdateSystem(_yieldSpawnBridgeHandle);
                UpdateSystem(_spawnHandle);
                UpdateSystem(_pickupHandle);
                UpdateSystem(_recordHandle);
                UpdateSystem(_telemetryHandle);
            }
        }

        private void AdvanceTick()
        {
            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick += 1;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);
        }

        private void RewindToTick(uint targetTick)
        {
            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Rewind;
            rewind.StartTick = _entityManager.GetComponentData<TimeState>(_timeEntity).Tick;
            rewind.PlaybackTick = targetTick;
            rewind.TargetTick = (int)targetTick;
            _entityManager.SetComponentData(_rewindEntity, rewind);

            UpdateSystem(_playbackHandle);

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = targetTick;
            _entityManager.SetComponentData(_timeEntity, time);

            rewind.Mode = RewindMode.Step;
            rewind.TargetTick = (int)targetTick;
            _entityManager.SetComponentData(_rewindEntity, rewind);

            UpdateSystem(_playbackHandle);
        }

        private void AdvanceToTick(uint targetTick)
        {
            var currentTick = _entityManager.GetComponentData<TimeState>(_timeEntity).Tick;
            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Record;
            _entityManager.SetComponentData(_rewindEntity, rewind);

            while (currentTick < targetTick)
            {
                AdvanceTick();
                UpdateSystem(_rewindableHandle);
                UpdateSystem(_miningSystemHandle);
                UpdateSystem(_yieldSpawnBridgeHandle);
                UpdateSystem(_spawnHandle);
                UpdateSystem(_pickupHandle);
                UpdateSystem(_recordHandle);
                UpdateSystem(_telemetryHandle);
                currentTick = _entityManager.GetComponentData<TimeState>(_timeEntity).Tick;
            }
        }

        private float GetCarrierStorage(Entity carrier)
        {
            var storage = _entityManager.GetBuffer<ResourceStorage>(carrier);
            float total = 0f;
            for (int i = 0; i < storage.Length; i++)
            {
                total += storage[i].Amount;
            }
            return total;
        }

        private float GetVesselCargo(Entity vessel)
        {
            return _entityManager.GetComponentData<MiningVessel>(vessel).CurrentCargo;
        }

        private float GetTelemetryOreInHold()
        {
            var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMiningTelemetry>());
            if (query.IsEmptyIgnoreFilter)
            {
                return 0f;
            }
            return query.GetSingleton<Space4XMiningTelemetry>().OreInHold;
        }

        private int GetSpawnResourceCount()
        {
            var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpawnResource>());
            return query.CalculateEntityCount();
        }

        private float GetAsteroidResourceAmount(Entity asteroid)
        {
            return _entityManager.GetComponentData<ResourceSourceState>(asteroid).UnitsRemaining;
        }

        private void EnsureTelemetryStream()
        {
            if (_telemetryStream != Entity.Null && _entityManager.Exists(_telemetryStream))
            {
                return;
            }

            _telemetryStream = _entityManager.CreateEntity(typeof(TelemetryStream));
            _entityManager.AddBuffer<TelemetryMetric>(_telemetryStream);
        }

        private void UpdateSystem(SystemHandle handle)
        {
            handle.Update(_world.Unmanaged);
        }
    }
}

