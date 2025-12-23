#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Systems.AI;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;

namespace Space4X.Tests.PlayMode
{
    public class Space4XMiningTimeSpineTests
    {
        private World _world;
        private EntityManager _entityManager;

        private Entity _timeEntity;
        private Entity _rewindEntity;
        private Entity _telemetryStream;

        private SystemHandle _rewindableHandle;
        private SystemHandle _gatherHandle;
        private SystemHandle _spawnHandle;
        private SystemHandle _pickupHandle;
        private SystemHandle _telemetryHandle;
        private SystemHandle _recordHandle;
        private SystemHandle _playbackHandle;

        [SetUp]
        public void SetUp()
        {
            _world = new World("MiningTimeSpineTests");
            _entityManager = _world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            // Ensure mining spine via gameplay bootstrap system (available in gameplay assembly)
            var spineBootstrap = _world.GetOrCreateSystem<Space4XMiningTimeSpineBootstrapSystem>();
            spineBootstrap.Update(_world.Unmanaged);

            _rewindableHandle = _world.GetOrCreateSystem<Space4XMiningRewindableSystem>();
            _gatherHandle = _world.GetOrCreateSystem<VesselGatheringSystem>();
            _spawnHandle = _world.GetOrCreateSystem<MiningResourceSpawnSystem>();
            _pickupHandle = _world.GetOrCreateSystem<CarrierPickupSystem>();
            _telemetryHandle = _world.GetOrCreateSystem<Space4XMiningTelemetrySystem>();
            _recordHandle = _world.GetOrCreateSystem<Space4XMiningTimeSpineRecordSystem>();
            _playbackHandle = _world.GetOrCreateSystem<Space4XMiningTimeSpinePlaybackSystem>();

            _timeEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>()).GetSingletonEntity();
            _rewindEntity = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<RewindState>()).GetSingletonEntity();

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 0;
            time.FixedDeltaTime = 1f;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);

            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Record;
            _entityManager.SetComponentData(_rewindEntity, rewind);
            var legacy = _entityManager.GetComponentData<RewindLegacyState>(_rewindEntity);
            legacy.PlaybackTicksPerSecond = HistorySettingsDefaults.DefaultTicksPerSecond;
            _entityManager.SetComponentData(_rewindEntity, legacy);

            EnsureTelemetryStream();
            ValidateContinuity();
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
        public void MiningTickRecordsSpawnAndCommands()
        {
            var asteroid = CreateAsteroid(50f, ResourceType.Minerals, float3.zero);
            CreateVessel(20f, asteroid, new float3(1f, 0f, 0f));

            AdvanceTick();
            UpdateSystem(_rewindableHandle);
            UpdateSystem(_gatherHandle);
            UpdateSystem(_spawnHandle);
            UpdateSystem(_recordHandle);

            using var spawnQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpawnResource>());
            Assert.AreEqual(1, spawnQuery.CalculateEntityCount());

            var spineEntity = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMiningTimeSpine>()).GetSingletonEntity();
            var commands = _entityManager.GetBuffer<MiningCommandLogEntry>(spineEntity);

            Assert.IsTrue(HasCommand(commands, MiningCommandType.Gather), "Expected gather command in log");
            Assert.IsTrue(HasCommand(commands, MiningCommandType.Spawn), "Expected spawn command in log");

            var snapshots = _entityManager.GetBuffer<MiningSnapshot>(spineEntity);
            Assert.IsTrue(HasSnapshotForEntity(snapshots, asteroid, MiningSnapshotType.Asteroid), "Asteroid snapshot missing");
        }

        [Test]
        public void CarrierPickupUpdatesTelemetryAndStorage()
        {
            var carrier = CreateCarrier(new float3(0f, 0f, 0f));
            CreateSpawn(ResourceType.Minerals, 15f, new float3(1f, 0f, 0f));

            AdvanceTick();
            UpdateSystem(_rewindableHandle);
            UpdateSystem(_pickupHandle);
            UpdateSystem(_recordHandle);
            UpdateSystem(_telemetryHandle);

            var storage = _entityManager.GetBuffer<ResourceStorage>(carrier);
            Assert.AreEqual(15f, storage[0].Amount, 1e-3f);

            var telemetry = _entityManager.GetComponentData<Space4XMiningTelemetry>(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMiningTelemetry>()).GetSingletonEntity());
            Assert.AreEqual(15f, telemetry.OreInHold, 1e-3f);

            var metrics = _entityManager.GetBuffer<TelemetryMetric>(_telemetryStream);
            Assert.IsTrue(MetricsContain(metrics, "space4x.mining.oreInHold"));
        }

        [Test]
        public void HaulingSkillExtendsPickupRadius()
        {
            var carrier = CreateCarrier(new float3(0f, 0f, 0f));
            _entityManager.AddComponentData(carrier, new CrewSkills
            {
                HaulingSkill = 1f
            });

            CreateSpawn(ResourceType.Minerals, 5f, new float3(9f, 0f, 0f));

            AdvanceTick();
            UpdateSystem(_pickupHandle);

            var storage = _entityManager.GetBuffer<ResourceStorage>(carrier);
            Assert.AreEqual(5f, storage[0].Amount, 1e-3f);
        }

        [Test]
        public void RewindRestoresMiningStateFromSpine()
        {
            var asteroid = CreateAsteroid(80f, ResourceType.Minerals, float3.zero);
            var carrier = CreateCarrier(new float3(0f, 0f, 0f));
            CreateVessel(20f, asteroid, new float3(0.5f, 0f, 0f));

            var oreAfterTick1 = SimulateMiningTick();
            var oreAfterTick2 = SimulateMiningTick();

            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Playback;
            rewind.TargetTick = 1;
            _entityManager.SetComponentData(_rewindEntity, rewind);
            var legacy = _entityManager.GetComponentData<RewindLegacyState>(_rewindEntity);
            legacy.StartTick = 2;
            legacy.PlaybackTick = 1;
            _entityManager.SetComponentData(_rewindEntity, legacy);

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 1;
            _entityManager.SetComponentData(_timeEntity, time);

            UpdateSystem(_playbackHandle);

            var storage = _entityManager.GetBuffer<ResourceStorage>(carrier);
            Assert.AreEqual(oreAfterTick1, SumStorage(storage), 1e-3f);

            var timeState = _entityManager.GetComponentData<TimeState>(_timeEntity);
            timeState.Tick = 2;
            _entityManager.SetComponentData(_timeEntity, timeState);

            rewind.Mode = RewindMode.CatchUp;
            rewind.TargetTick = 2;
            _entityManager.SetComponentData(_rewindEntity, rewind);

            UpdateSystem(_playbackHandle);

            storage = _entityManager.GetBuffer<ResourceStorage>(carrier);
            Assert.AreEqual(oreAfterTick2, SumStorage(storage), 1e-3f);
        }

        private float SimulateMiningTick()
        {
            AdvanceTick();
            UpdateSystem(_rewindableHandle);
            UpdateSystem(_gatherHandle);
            UpdateSystem(_spawnHandle);
            UpdateSystem(_pickupHandle);
            UpdateSystem(_recordHandle);
            UpdateSystem(_telemetryHandle);

            var telemetryQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMiningTelemetry>());
            if (telemetryQuery.IsEmptyIgnoreFilter)
            {
                return 0f;
            }

            var telemetry = telemetryQuery.GetSingleton<Space4XMiningTelemetry>();
            return telemetry.OreInHold;
        }

        private Entity CreateAsteroid(float units, ResourceType resourceType, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(Asteroid),
                typeof(ResourceSourceState),
                typeof(ResourceSourceConfig),
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

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            _entityManager.SetComponentData(entity, new LastRecordedTick { Tick = 0 });
            _entityManager.AddComponent<RewindableTag>(entity);
            return entity;
        }

        private Entity CreateVessel(float cargoCapacity, Entity targetAsteroid, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(MiningVessel),
                typeof(VesselAIState),
                typeof(LocalTransform),
                typeof(SpawnResourceRequest));

            _entityManager.SetComponentData(entity, new MiningVessel
            {
                VesselId = new FixedString64Bytes("VES-1"),
                CarrierEntity = Entity.Null,
                MiningEfficiency = 1f,
                Speed = 0f,
                CargoCapacity = cargoCapacity,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
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

        private void AdvanceTick()
        {
            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick += 1;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);
        }

        private void EnsureTelemetryStream()
        {
            if (_telemetryStream != Entity.Null && _entityManager.Exists(_telemetryStream))
            {
                TelemetryStreamUtility.EnsureEventStream(_entityManager);
                return;
            }

            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            if (query.IsEmptyIgnoreFilter)
            {
                _telemetryStream = _entityManager.CreateEntity(typeof(TelemetryStream));
                _entityManager.SetComponentData(_telemetryStream, new TelemetryStream
                {
                    Version = 0,
                    LastTick = 0
                });
            }
            else
            {
                _telemetryStream = query.GetSingletonEntity();
            }

            if (!_entityManager.HasBuffer<TelemetryMetric>(_telemetryStream))
            {
                _entityManager.AddBuffer<TelemetryMetric>(_telemetryStream);
            }

            TelemetryStreamUtility.EnsureEventStream(_entityManager);
        }

        private void UpdateSystem(SystemHandle handle)
        {
            handle.Update(_world.Unmanaged);
        }

        private void ValidateContinuity()
        {
            Assert.IsFalse(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XMiningTimeSpine>()).IsEmptyIgnoreFilter, "Missing mining time spine singleton");
            Assert.IsFalse(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>()).IsEmptyIgnoreFilter, "Missing time state singleton");
            Assert.IsFalse(_entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>()).IsEmptyIgnoreFilter, "Missing rewind state singleton");
        }

        private static bool HasCommand(DynamicBuffer<MiningCommandLogEntry> buffer, MiningCommandType commandType)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].CommandType == commandType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSnapshotForEntity(DynamicBuffer<MiningSnapshot> snapshots, Entity entity, MiningSnapshotType type)
        {
            for (var i = 0; i < snapshots.Length; i++)
            {
                if (snapshots[i].Entity == entity && snapshots[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MetricsContain(DynamicBuffer<TelemetryMetric> buffer, string key)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Key.ToString() == key)
                {
                    return true;
                }
            }

            return false;
        }

        private static float SumStorage(DynamicBuffer<ResourceStorage> storage)
        {
            var total = 0f;
            for (var i = 0; i < storage.Length; i++)
            {
                total += storage[i].Amount;
            }

            return total;
        }
    }
}
#endif
