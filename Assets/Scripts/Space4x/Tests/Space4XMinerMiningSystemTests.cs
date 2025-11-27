using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Systems.AI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PlayEffectRequest = Space4X.Registry.PlayEffectRequest;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;

namespace Space4X.Tests
{
    public class Space4XMinerMiningSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XMinerMiningSystemTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            CoreSingletonBootstrapSystem.EnsureMiningSpine(_entityManager);
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
        public void MinerProcessesScriptedOrderAndEmitsEffect()
        {
            var resourceId = new FixedString64Bytes("space4x.resource.minerals");
            ConfigureTimeAndRewind(0.5f, RewindMode.Record);
            var resource = CreateResourceSource(resourceId, 12f, 6f, float3.zero);
            var miner = CreateMiner(resourceId, 10f, 0.5f, float3.zero);

            var miningSystem = _world.GetOrCreateSystem<Space4XMinerMiningSystem>();
            miningSystem.Update(_world.Unmanaged);

            var updatedOrder = _entityManager.GetComponentData<MiningOrder>(miner);
            Assert.AreEqual(MiningOrderStatus.Active, updatedOrder.Status);
            Assert.AreEqual(resource, updatedOrder.TargetEntity);

            var state = _entityManager.GetComponentData<MiningState>(miner);
            Assert.AreEqual(MiningPhase.Mining, state.Phase);

            var vessel = _entityManager.GetComponentData<MiningVessel>(miner);
            Assert.Greater(vessel.CurrentCargo, 0f);

            var yield = _entityManager.GetComponentData<MiningYield>(miner);
            Assert.Greater(yield.PendingAmount, 0f);
            Assert.AreEqual(0, yield.SpawnReady);

            var resourceState = _entityManager.GetComponentData<ResourceSourceState>(resource);
            Assert.Less(resourceState.UnitsRemaining, 12f);

            var effectStreamEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XEffectRequestStream>()).GetSingletonEntity();
            var effects = _entityManager.GetBuffer<PlayEffectRequest>(effectStreamEntity);
            Assert.Greater(effects.Length, 0);
            Assert.AreEqual("FX.Mining.Sparks", effects[0].EffectId.ToString());
            Assert.AreEqual(miner, effects[0].AttachTo);
            Assert.Greater(effects[0].Lifetime, 0f);
        }

        [Test]
        public void MinerSkipsTicksWhenRewindIsNotRecording()
        {
            var resourceId = new FixedString64Bytes("space4x.resource.minerals");
            ConfigureTimeAndRewind(0.25f, RewindMode.CatchUp);
            var resource = CreateResourceSource(resourceId, 12f, 6f, float3.zero);
            var miner = CreateMiner(resourceId, 10f, 0.5f, float3.zero);

            var miningSystem = _world.GetOrCreateSystem<Space4XMinerMiningSystem>();
            miningSystem.Update(_world.Unmanaged);

            var vessel = _entityManager.GetComponentData<MiningVessel>(miner);
            Assert.AreEqual(0f, vessel.CurrentCargo);

            var order = _entityManager.GetComponentData<MiningOrder>(miner);
            Assert.AreEqual(MiningOrderStatus.Pending, order.Status);

            var state = _entityManager.GetComponentData<MiningState>(miner);
            Assert.AreEqual(MiningPhase.Idle, state.Phase);

            var resourceState = _entityManager.GetComponentData<ResourceSourceState>(resource);
            Assert.AreEqual(12f, resourceState.UnitsRemaining);

            var effectStreamEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XEffectRequestStream>()).GetSingletonEntity();
            var effects = _entityManager.GetBuffer<PlayEffectRequest>(effectStreamEntity);
            Assert.AreEqual(0, effects.Length);
        }

        private void ConfigureTimeAndRewind(float fixedDeltaTime, RewindMode rewindMode, uint tick = 0)
        {
            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var timeState = _entityManager.GetComponentData<TimeState>(timeEntity);
            timeState.FixedDeltaTime = fixedDeltaTime;
            timeState.IsPaused = false;
            timeState.Tick = tick;
            _entityManager.SetComponentData(timeEntity, timeState);
            _entityManager.SetComponentData(timeEntity, new GameplayFixedStep { FixedDeltaTime = fixedDeltaTime });

            var rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();
            var rewindState = _entityManager.GetComponentData<RewindState>(rewindEntity);
            rewindState.Mode = rewindMode;
            _entityManager.SetComponentData(rewindEntity, rewindState);
        }

        private Entity CreateResourceSource(FixedString64Bytes resourceId, float unitsRemaining, float gatherRate, float3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(ResourceSourceState), typeof(ResourceSourceConfig), typeof(ResourceTypeId), typeof(LocalTransform));
            _entityManager.SetComponentData(entity, new ResourceSourceState { UnitsRemaining = unitsRemaining });
            _entityManager.SetComponentData(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = gatherRate,
                MaxSimultaneousWorkers = 1,
                RespawnSeconds = 0f,
                Flags = 0
            });
            _entityManager.SetComponentData(entity, new ResourceTypeId { Value = resourceId });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private Entity CreateMiner(FixedString64Bytes resourceId, float cargoCapacity, float tickInterval, float3 position, bool addSpawnRequestBuffer = false)
        {
            var entity = _entityManager.CreateEntity(typeof(MiningVessel), typeof(MiningOrder), typeof(MiningState), typeof(MiningYield), typeof(LocalTransform));
            _entityManager.SetComponentData(entity, new MiningVessel
            {
                VesselId = new FixedString64Bytes("TEST-MINER"),
                CarrierEntity = Entity.Null,
                MiningEfficiency = 1f,
                Speed = 0f,
                CargoCapacity = cargoCapacity,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });
            _entityManager.SetComponentData(entity, new MiningOrder
            {
                ResourceId = resourceId,
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = Entity.Null,
                TargetEntity = Entity.Null,
                IssuedTick = 0
            });
            _entityManager.SetComponentData(entity, new MiningState
            {
                Phase = MiningPhase.Idle,
                ActiveTarget = Entity.Null,
                MiningTimer = 0f,
                TickInterval = tickInterval
            });
            _entityManager.SetComponentData(entity, new MiningYield
            {
                ResourceId = resourceId,
                PendingAmount = 0f,
                SpawnThreshold = 2f,
                SpawnReady = 0
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            if (addSpawnRequestBuffer && !_entityManager.HasBuffer<SpawnResourceRequest>(entity))
            {
                _entityManager.AddBuffer<SpawnResourceRequest>(entity);
            }
            return entity;
        }

        [Test]
        public void MinerLogsCommandAndQueuesSpawnRequest()
        {
            ConfigureTimeAndRewind(0.5f, RewindMode.Record);
            var spineEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMiningTimeSpine>()).GetSingletonEntity();

            var resourceId = new FixedString64Bytes("space4x.resource.minerals");

            var resource = CreateResourceSource(resourceId, 20f, 6f, float3.zero);
            var miner = CreateMiner(resourceId, 10f, 0.5f, float3.zero, addSpawnRequestBuffer: true);

            var miningSystem = _world.GetOrCreateSystem<Space4XMinerMiningSystem>();
            var bridgeSystem = _world.GetOrCreateSystem<Space4XMiningYieldSpawnBridgeSystem>();
            var spawnSystem = _world.GetOrCreateSystem<MiningResourceSpawnSystem>();

            miningSystem.Update(_world.Unmanaged);
            bridgeSystem.Update(_world.Unmanaged);
            spawnSystem.Update(_world.Unmanaged);

            var commandLog = _entityManager.GetBuffer<MiningCommandLogEntry>(spineEntity);
            Assert.IsTrue(HasGatherCommand(commandLog, miner), "Expected gather command to be logged for miner.");

            var spawnQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpawnResource>());
            Assert.AreEqual(1, spawnQuery.CalculateEntityCount(), "Expected spawn pickup to be created from MiningYield.");
            var spawn = spawnQuery.GetSingleton<SpawnResource>();
            Assert.AreEqual(ResourceType.Minerals, spawn.Type);
            Assert.Greater(spawn.Amount, 0f);
        }

        [Test]
        public void SpawnSystemUsesMiningYieldThresholdAndResourceId()
        {
            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var timeState = _entityManager.GetComponentData<TimeState>(timeEntity);
            timeState.Tick = 1;
            timeState.IsPaused = false;
            _entityManager.SetComponentData(timeEntity, timeState);

            var rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();
            var rewindState = _entityManager.GetComponentData<RewindState>(rewindEntity);
            rewindState.Mode = RewindMode.Record;
            _entityManager.SetComponentData(rewindEntity, rewindState);

            var yieldResourceId = new FixedString64Bytes("space4x.resource.energy_crystals");
            var miner = _entityManager.CreateEntity(typeof(MiningVessel), typeof(MiningYield), typeof(SpawnResourceRequest), typeof(LocalTransform));

            _entityManager.SetComponentData(miner, new MiningVessel
            {
                VesselId = new FixedString64Bytes("YIELD-MINER"),
                CarrierEntity = Entity.Null,
                MiningEfficiency = 1f,
                Speed = 0f,
                CargoCapacity = 50f,
                CurrentCargo = 25f,
                CargoResourceType = ResourceType.Minerals
            });

            _entityManager.SetComponentData(miner, new MiningYield
            {
                ResourceId = yieldResourceId,
                PendingAmount = 25f,
                SpawnThreshold = 10f,
                SpawnReady = 1
            });

            _entityManager.SetComponentData(miner, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            _entityManager.GetBuffer<SpawnResourceRequest>(miner);

            var spawnSystem = _world.GetOrCreateSystem<MiningResourceSpawnSystem>();
            spawnSystem.Update(_world.Unmanaged);

            var updatedVessel = _entityManager.GetComponentData<MiningVessel>(miner);
            Assert.AreEqual(ResourceType.EnergyCrystals, updatedVessel.CargoResourceType);
            Assert.AreEqual(5f, updatedVessel.CurrentCargo, 1e-3f);

            var updatedYield = _entityManager.GetComponentData<MiningYield>(miner);
            Assert.AreEqual(5f, updatedYield.PendingAmount, 1e-3f);
            Assert.AreEqual(0, updatedYield.SpawnReady);

            using var spawnQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpawnResource>());
            Assert.AreEqual(2, spawnQuery.CalculateEntityCount());

            using var spawnEntities = spawnQuery.ToEntityArray(Allocator.Temp);
            foreach (var spawnEntity in spawnEntities)
            {
                var spawnData = _entityManager.GetComponentData<SpawnResource>(spawnEntity);
                Assert.AreEqual(ResourceType.EnergyCrystals, spawnData.Type);
                Assert.AreEqual(10f, spawnData.Amount, 1e-3f);
            }
        }

        [Test]
        public void MiningSkillAmplifiesMiningTick()
        {
            var timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            var timeState = _entityManager.GetComponentData<TimeState>(timeEntity);
            timeState.FixedDeltaTime = 0.5f;
            timeState.IsPaused = false;
            _entityManager.SetComponentData(timeEntity, timeState);
            _entityManager.SetComponentData(timeEntity, new GameplayFixedStep { FixedDeltaTime = 0.5f });

            var rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();
            var rewindState = _entityManager.GetComponentData<RewindState>(rewindEntity);
            rewindState.Mode = RewindMode.Record;
            _entityManager.SetComponentData(rewindEntity, rewindState);

            var resourceId = new FixedString64Bytes("space4x.resource.minerals");

            var resource = _entityManager.CreateEntity(typeof(ResourceSourceState), typeof(ResourceSourceConfig), typeof(ResourceTypeId), typeof(LocalTransform));
            _entityManager.SetComponentData(resource, new ResourceSourceState { UnitsRemaining = 50f });
            _entityManager.SetComponentData(resource, new ResourceSourceConfig
            {
                GatherRatePerWorker = 6f,
                MaxSimultaneousWorkers = 1,
                RespawnSeconds = 0f,
                Flags = 0
            });
            _entityManager.SetComponentData(resource, new ResourceTypeId { Value = resourceId });
            _entityManager.SetComponentData(resource, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

            var miner = _entityManager.CreateEntity(typeof(MiningVessel), typeof(MiningOrder), typeof(MiningState), typeof(MiningYield), typeof(LocalTransform), typeof(CrewSkills));
            _entityManager.SetComponentData(miner, new MiningVessel
            {
                VesselId = new FixedString64Bytes("SKILL-MINER"),
                CarrierEntity = Entity.Null,
                MiningEfficiency = 1f,
                Speed = 0f,
                CargoCapacity = 10f,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });
            _entityManager.SetComponentData(miner, new MiningOrder
            {
                ResourceId = resourceId,
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = Entity.Null,
                TargetEntity = Entity.Null,
                IssuedTick = 0
            });
            _entityManager.SetComponentData(miner, new MiningState
            {
                Phase = MiningPhase.Idle,
                ActiveTarget = Entity.Null,
                MiningTimer = 0f,
                TickInterval = 0.5f
            });
            _entityManager.SetComponentData(miner, new MiningYield
            {
                ResourceId = resourceId,
                PendingAmount = 0f,
                SpawnThreshold = 5f,
                SpawnReady = 0
            });
            _entityManager.SetComponentData(miner, new CrewSkills
            {
                MiningSkill = 1f
            });
            _entityManager.SetComponentData(miner, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

            var miningSystem = _world.GetOrCreateSystem<Space4XMinerMiningSystem>();
            miningSystem.Update(_world.Unmanaged);

            var vessel = _entityManager.GetComponentData<MiningVessel>(miner);
            Assert.AreEqual(4.5f, vessel.CurrentCargo, 1e-3f);
        }

        private static bool HasGatherCommand(DynamicBuffer<MiningCommandLogEntry> buffer, Entity miner)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].CommandType == MiningCommandType.Gather && buffer[i].TargetEntity == miner)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
