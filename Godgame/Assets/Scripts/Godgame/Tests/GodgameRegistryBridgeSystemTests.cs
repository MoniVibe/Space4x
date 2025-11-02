using System.Collections.Generic;
using Godgame.Registry;
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using PureDOTS.Systems.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Godgame.Tests.Registry
{
    public class GodgameRegistryBridgeSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        private SystemHandle _spatialIndexingHandle;
        private SystemHandle _spatialDirtyHandle;
        private SystemHandle _spatialBuildHandle;
        private SystemHandle _endSimulationEcbHandle;
        private SystemHandle _villagerSyncHandle;
        private SystemHandle _storehouseSyncHandle;
        private SystemHandle _bridgeHandle;
        private SystemHandle _telemetryHandle;
        private SystemHandle _directoryHandle;

        private Entity _gridEntity;
        private Entity _timeEntity;
        private Entity _telemetryEntity;
        private Entity _villagerRegistryEntity;
        private Entity _storehouseRegistryEntity;
        private Entity _villagerTargetEntity;
        private Entity _miracleRegistryEntity;

        private BlobAssetReference<ResourceTypeIndexBlob> _resourceCatalog;

        [SetUp]
        public void SetUp()
        {
            _world = new World("GodgameRegistryBridgeSystemTests");
            _entityManager = _world.EntityManager;

            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            ConfigureTimeState();
            EnsureRegistryDirectory();
            EnsureTelemetryStream();
            EnsureVillagerRegistry();
            EnsureStorehouseRegistry();
            EnsureMiracleRegistry();
            EnsureResourceCatalog();
            ConfigureSpatialGrid();

            _spatialIndexingHandle = _world.GetOrCreateSystem<GodgameSpatialIndexingSystem>();
            _spatialDirtyHandle = _world.GetOrCreateSystem<SpatialGridDirtyTrackingSystem>();
            _spatialBuildHandle = _world.GetOrCreateSystem<SpatialGridBuildSystem>();
            _endSimulationEcbHandle = _world.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _villagerSyncHandle = _world.GetOrCreateSystem<GodgameVillagerSyncSystem>();
            _storehouseSyncHandle = _world.GetOrCreateSystem<GodgameStorehouseSyncSystem>();
            _bridgeHandle = _world.GetOrCreateSystem<GodgameRegistryBridgeSystem>();
            _telemetryHandle = _world.GetOrCreateSystem<GodgameRegistryTelemetrySystem>();
            _directoryHandle = _world.GetOrCreateSystem<RegistryDirectorySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_resourceCatalog.IsCreated)
            {
                _resourceCatalog.Dispose();
                _resourceCatalog = default;
            }

            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void BridgeRegistersVillagersAndStorehousesAndPublishesTelemetry()
        {
            var villager = CreateVillager(
                villagerId: 101,
                factionId: 3,
                position: new float3(2f, 0f, -1f),
                availability: true,
                morale: 70f,
                energy: 80f);

            var storehouse = CreateStorehouse(
                storehouseId: 501,
                capacity: 400f,
                stored: 150f,
                reserved: 20f,
                position: new float3(-4f, 0f, 6f));

            StepSyncSystems();
            StepSpatialSystems();

            UpdateSystem(_bridgeHandle);
            UpdateSystem(_directoryHandle);
            UpdateSystem(_telemetryHandle);

            var spatialState = _entityManager.GetComponentData<SpatialGridState>(_gridEntity);

            var villagerMirror = _entityManager.GetComponentData<GodgameVillager>(villager);
            Assert.AreEqual(101, villagerMirror.VillagerId);
            Assert.AreEqual((byte)VillagerDisciplineType.Forester, (byte)villagerMirror.Discipline);
            Assert.AreEqual(VillagerAIState.State.Working, villagerMirror.AIState);
            Assert.AreEqual(VillagerAIState.Goal.Work, villagerMirror.AIGoal);
            Assert.AreEqual(_villagerTargetEntity, villagerMirror.CurrentTarget);
            Assert.AreEqual(90f, villagerMirror.HealthPercent);
            Assert.AreEqual(70f, villagerMirror.MoralePercent);
            Assert.AreEqual(80f, villagerMirror.EnergyPercent);

            var villagerRegistry = _entityManager.GetComponentData<VillagerRegistry>(_villagerRegistryEntity);
            Assert.AreEqual(1, villagerRegistry.TotalVillagers);
            Assert.AreEqual(1, villagerRegistry.AvailableVillagers);
            Assert.AreEqual(42u, villagerRegistry.LastUpdateTick);
            Assert.AreEqual(1, villagerRegistry.CombatReadyVillagers);
            Assert.Greater(villagerRegistry.AverageMoralePercent, 0f);
            Assert.AreEqual(90f, villagerRegistry.AverageHealthPercent);

            var villagerMetadata = _entityManager.GetComponentData<RegistryMetadata>(_villagerRegistryEntity);
            Assert.AreEqual(GodgameRegistryIds.VillagerArchetype, villagerMetadata.ArchetypeId);
            Assert.AreEqual(1, villagerMetadata.EntryCount);
            Assert.AreEqual(spatialState.Version, villagerMetadata.Continuity.SpatialVersion);

            var villagerEntries = _entityManager.GetBuffer<VillagerRegistryEntry>(_villagerRegistryEntity);
            Assert.AreEqual(1, villagerEntries.Length);
            Assert.AreEqual(101, villagerEntries[0].VillagerId);
            Assert.AreEqual((byte)VillagerDisciplineType.Forester, villagerEntries[0].Discipline);
            Assert.AreEqual((byte)VillagerAIState.State.Working, villagerEntries[0].AIState);
            Assert.AreEqual((byte)VillagerAIState.Goal.Work, villagerEntries[0].AIGoal);
            Assert.AreEqual(_villagerTargetEntity, villagerEntries[0].CurrentTarget);
            Assert.AreEqual(77u, villagerEntries[0].ActiveTicketId);
            Assert.AreEqual((ushort)5, villagerEntries[0].CurrentResourceTypeIndex);

            var storehouseRegistry = _entityManager.GetComponentData<StorehouseRegistry>(_storehouseRegistryEntity);
            Assert.AreEqual(1, storehouseRegistry.TotalStorehouses);
            Assert.AreEqual(400f, storehouseRegistry.TotalCapacity);
            Assert.AreEqual(150f, storehouseRegistry.TotalStored);
            Assert.AreEqual(42u, storehouseRegistry.LastUpdateTick);

            var storehouseMetadata = _entityManager.GetComponentData<RegistryMetadata>(_storehouseRegistryEntity);
            Assert.AreEqual(GodgameRegistryIds.StorehouseArchetype, storehouseMetadata.ArchetypeId);
            Assert.AreEqual(1, storehouseMetadata.EntryCount);
            Assert.AreEqual(spatialState.Version, storehouseMetadata.Continuity.SpatialVersion);

            var storehouseEntries = _entityManager.GetBuffer<StorehouseRegistryEntry>(_storehouseRegistryEntity);
            Assert.AreEqual(1, storehouseEntries.Length);
            Assert.AreEqual(400f, storehouseEntries[0].TotalCapacity);
            Assert.AreEqual(1, storehouseEntries[0].TypeSummaries.Length);
            Assert.AreEqual(150f, storehouseEntries[0].TypeSummaries[0].Stored);
            Assert.AreEqual(20f, storehouseEntries[0].TypeSummaries[0].Reserved, 0.001f);

            Assert.GreaterOrEqual(villagerEntries[0].CellId, 0);
            Assert.GreaterOrEqual(storehouseEntries[0].CellId, 0);
            Assert.AreEqual(spatialState.Version, villagerEntries[0].SpatialVersion);
            Assert.AreEqual(spatialState.Version, storehouseEntries[0].SpatialVersion);

            var directoryEntries = _entityManager.GetBuffer<RegistryDirectoryEntry>(_entityManager.CreateEntityQuery(typeof(RegistryDirectory)).GetSingletonEntity());
            Assert.IsNotEmpty(directoryEntries);

            var snapshot = _entityManager.CreateEntityQuery(typeof(GodgameRegistrySnapshot)).GetSingleton<GodgameRegistrySnapshot>();
            Assert.AreEqual(1, snapshot.VillagerCount);
            Assert.AreEqual(1, snapshot.StorehouseCount);
            Assert.AreEqual(42u, snapshot.LastRegistryTick);
            Assert.AreEqual(1, snapshot.AvailableVillagers);
            Assert.AreEqual(1, snapshot.IdleVillagers);
            Assert.AreEqual(1, snapshot.CombatReadyVillagers);
            Assert.AreEqual(90f, snapshot.AverageVillagerHealth);
            Assert.AreEqual(150f, snapshot.TotalStorehouseStored);
            Assert.AreEqual(20f, snapshot.TotalStorehouseReserved);

            var telemetryBuffer = _entityManager.GetBuffer<TelemetryMetric>(_telemetryEntity);
            var keys = new List<string>(telemetryBuffer.Length);
            for (var i = 0; i < telemetryBuffer.Length; i++)
            {
                keys.Add(telemetryBuffer[i].Key.ToString());
            }

            Assert.Contains("godgame.registry.villagers", keys);
            Assert.Contains("godgame.registry.storehouses", keys);
            Assert.Contains("godgame.registry.villagers.health.avg", keys);
            Assert.Contains("godgame.registry.storehouses.reserved", keys);

            var miracleMetadata = _entityManager.GetComponentData<RegistryMetadata>(_miracleRegistryEntity);
            Assert.AreEqual(21u, miracleMetadata.LastUpdateTick);
            Assert.IsFalse(miracleMetadata.Continuity.HasSpatialData);

            var previousVillagerVersion = villagerMetadata.Version;
            var previousStorehouseVersion = storehouseMetadata.Version;

            MutateVillager(villager, morale: 55f, energy: 60f);
            MutateStorehouse(storehouse, stored: 180f, reserved: 35f);

            StepSyncSystems();
            StepSpatialSystems();

            spatialState = _entityManager.GetComponentData<SpatialGridState>(_gridEntity);

            UpdateSystem(_bridgeHandle);
            UpdateSystem(_directoryHandle);
            UpdateSystem(_telemetryHandle);

            var updatedVillagerMetadata = _entityManager.GetComponentData<RegistryMetadata>(_villagerRegistryEntity);
            Assert.AreEqual(previousVillagerVersion + 1, updatedVillagerMetadata.Version);
            Assert.AreEqual(spatialState.Version, updatedVillagerMetadata.Continuity.SpatialVersion);

            var updatedStorehouseMetadata = _entityManager.GetComponentData<RegistryMetadata>(_storehouseRegistryEntity);
            Assert.AreEqual(previousStorehouseVersion + 1, updatedStorehouseMetadata.Version);
            Assert.AreEqual(spatialState.Version, updatedStorehouseMetadata.Continuity.SpatialVersion);

            villagerRegistry = _entityManager.GetComponentData<VillagerRegistry>(_villagerRegistryEntity);
            Assert.AreEqual(1, villagerRegistry.ReservedVillagers);
            Assert.AreEqual(55f, villagerRegistry.AverageMoralePercent, 0.1f);

            storehouseRegistry = _entityManager.GetComponentData<StorehouseRegistry>(_storehouseRegistryEntity);
            Assert.AreEqual(180f, storehouseRegistry.TotalStored, 0.001f);

            villagerEntries = _entityManager.GetBuffer<VillagerRegistryEntry>(_villagerRegistryEntity);
            Assert.AreEqual((byte)VillagerDisciplineType.Warrior, villagerEntries[0].Discipline);
            Assert.AreEqual(55, villagerEntries[0].MoralePercent);
            Assert.AreNotEqual(0, villagerEntries[0].AvailabilityFlags & VillagerAvailabilityFlags.Reserved);

            storehouseEntries = _entityManager.GetBuffer<StorehouseRegistryEntry>(_storehouseRegistryEntity);
            Assert.AreEqual(1, storehouseEntries[0].TypeSummaries.Length);
            Assert.AreEqual(180f, storehouseEntries[0].TypeSummaries[0].Stored, 0.001f);
            Assert.AreEqual(35f, storehouseEntries[0].TypeSummaries[0].Reserved, 0.001f);

            snapshot = _entityManager.CreateEntityQuery(typeof(GodgameRegistrySnapshot)).GetSingleton<GodgameRegistrySnapshot>();
            Assert.AreEqual(55f, snapshot.AverageVillagerMorale, 0.1f);
            Assert.AreEqual(35f, snapshot.TotalStorehouseReserved, 0.001f);
        }

        private void ConfigureTimeState()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>());
            if (query.IsEmptyIgnoreFilter)
            {
                _timeEntity = _entityManager.CreateEntity(typeof(TimeState));
            }
            else
            {
                _timeEntity = query.GetSingletonEntity();
            }

            _entityManager.SetComponentData(_timeEntity, new TimeState
            {
                Tick = 42,
                FixedDeltaTime = 1f / 60f,
                CurrentSpeedMultiplier = 1f,
                IsPaused = false
            });
        }

        private void EnsureRegistryDirectory()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            Entity entity;

            if (query.IsEmptyIgnoreFilter)
            {
                entity = _entityManager.CreateEntity(typeof(RegistryDirectory));
            }
            else
            {
                entity = query.GetSingletonEntity();
            }

            if (!_entityManager.HasBuffer<RegistryDirectoryEntry>(entity))
            {
                _entityManager.AddBuffer<RegistryDirectoryEntry>(entity);
            }
        }

        private void EnsureTelemetryStream()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            if (query.IsEmptyIgnoreFilter)
            {
                _telemetryEntity = _entityManager.CreateEntity(typeof(TelemetryStream));
            }
            else
            {
                _telemetryEntity = query.GetSingletonEntity();
            }

            if (!_entityManager.HasBuffer<TelemetryMetric>(_telemetryEntity))
            {
                _entityManager.AddBuffer<TelemetryMetric>(_telemetryEntity);
            }
        }

        private void EnsureVillagerRegistry()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerRegistry>());
            if (query.IsEmptyIgnoreFilter)
            {
                _villagerRegistryEntity = _entityManager.CreateEntity(typeof(VillagerRegistry));
            }
            else
            {
                _villagerRegistryEntity = query.GetSingletonEntity();
            }

            if (!_entityManager.HasComponent<RegistryMetadata>(_villagerRegistryEntity))
            {
                _entityManager.AddComponentData(_villagerRegistryEntity, new RegistryMetadata());
            }

            if (!_entityManager.HasBuffer<VillagerRegistryEntry>(_villagerRegistryEntity))
            {
                _entityManager.AddBuffer<VillagerRegistryEntry>(_villagerRegistryEntity);
            }

            if (!_entityManager.HasComponent<RegistryHealth>(_villagerRegistryEntity))
            {
                _entityManager.AddComponentData(_villagerRegistryEntity, default(RegistryHealth));
            }

            var metadata = _entityManager.GetComponentData<RegistryMetadata>(_villagerRegistryEntity);
            metadata.Initialise(RegistryKind.Villager, GodgameRegistryIds.VillagerArchetype, RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries | RegistryHandleFlags.SupportsPathfinding, "VillagerRegistry");
            _entityManager.SetComponentData(_villagerRegistryEntity, metadata);

            _entityManager.SetComponentData(_villagerRegistryEntity, default(VillagerRegistry));
        }

        private void EnsureStorehouseRegistry()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<StorehouseRegistry>());
            if (query.IsEmptyIgnoreFilter)
            {
                _storehouseRegistryEntity = _entityManager.CreateEntity(typeof(StorehouseRegistry));
            }
            else
            {
                _storehouseRegistryEntity = query.GetSingletonEntity();
            }

            if (!_entityManager.HasComponent<RegistryMetadata>(_storehouseRegistryEntity))
            {
                _entityManager.AddComponentData(_storehouseRegistryEntity, new RegistryMetadata());
            }

            if (!_entityManager.HasBuffer<StorehouseRegistryEntry>(_storehouseRegistryEntity))
            {
                _entityManager.AddBuffer<StorehouseRegistryEntry>(_storehouseRegistryEntity);
            }

            if (!_entityManager.HasComponent<RegistryHealth>(_storehouseRegistryEntity))
            {
                _entityManager.AddComponentData(_storehouseRegistryEntity, default(RegistryHealth));
            }

            var metadata = _entityManager.GetComponentData<RegistryMetadata>(_storehouseRegistryEntity);
            metadata.Initialise(RegistryKind.Storehouse, GodgameRegistryIds.StorehouseArchetype, RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries | RegistryHandleFlags.SupportsPathfinding, "StorehouseRegistry");
            _entityManager.SetComponentData(_storehouseRegistryEntity, metadata);

            _entityManager.SetComponentData(_storehouseRegistryEntity, default(StorehouseRegistry));
        }

        private void EnsureMiracleRegistry()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<MiracleRegistry>());
            if (query.IsEmptyIgnoreFilter)
            {
                _miracleRegistryEntity = _entityManager.CreateEntity(typeof(MiracleRegistry));
            }
            else
            {
                _miracleRegistryEntity = query.GetSingletonEntity();
            }

            if (!_entityManager.HasComponent<RegistryMetadata>(_miracleRegistryEntity))
            {
                _entityManager.AddComponentData(_miracleRegistryEntity, new RegistryMetadata());
            }

            if (!_entityManager.HasBuffer<MiracleRegistryEntry>(_miracleRegistryEntity))
            {
                _entityManager.AddBuffer<MiracleRegistryEntry>(_miracleRegistryEntity);
            }

            var metadata = _entityManager.GetComponentData<RegistryMetadata>(_miracleRegistryEntity);
            metadata.Initialise(RegistryKind.Miracle, 0x4703, RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries, "MiracleRegistry");
            metadata.MarkUpdated(0, 21, RegistryContinuitySnapshot.WithoutSpatialData());
            _entityManager.SetComponentData(_miracleRegistryEntity, metadata);

            _entityManager.SetComponentData(_miracleRegistryEntity, default(MiracleRegistry));
        }

        private void EnsureResourceCatalog()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ResourceTypeIndex>());
            if (!query.IsEmptyIgnoreFilter)
            {
                var existing = _entityManager.GetComponentData<ResourceTypeIndex>(query.GetSingletonEntity());
                if (!_resourceCatalog.IsCreated && existing.Catalog.IsCreated)
                {
                    _resourceCatalog = existing.Catalog;
                }
                return;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ResourceTypeIndexBlob>();

            var ids = builder.Allocate(ref root.Ids, 1);
            ids[0] = new FixedString64Bytes("wood");

            var displayNames = builder.Allocate(ref root.DisplayNames, 1);
            builder.AllocateString(ref displayNames[0], "wood");

            var colors = builder.Allocate(ref root.Colors, 1);
            colors[0] = new Color32(128, 96, 64, 255);

            _resourceCatalog = builder.CreateBlobAssetReference<ResourceTypeIndexBlob>(Allocator.Persistent);
            builder.Dispose();

            var catalogEntity = _entityManager.CreateEntity(typeof(ResourceTypeIndex));
            _entityManager.SetComponentData(catalogEntity, new ResourceTypeIndex { Catalog = _resourceCatalog });
        }

        private void ConfigureSpatialGrid()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpatialGridConfig>());
            if (query.IsEmptyIgnoreFilter)
            {
                _gridEntity = _entityManager.CreateEntity(typeof(SpatialGridConfig), typeof(SpatialGridState));
            }
            else
            {
                _gridEntity = query.GetSingletonEntity();
                if (!_entityManager.HasComponent<SpatialGridState>(_gridEntity))
                {
                    _entityManager.AddComponentData(_gridEntity, new SpatialGridState());
                }
            }

            var config = _entityManager.GetComponentData<SpatialGridConfig>(_gridEntity);
            config.WorldMin = new float3(-32f, -8f, -32f);
            config.WorldMax = new float3(32f, 8f, 32f);
            config.CellSize = 4f;
            config.CellCounts = new int3(16, 4, 16);
            config.ProviderId = SpatialGridProviderIds.Hashed;
            _entityManager.SetComponentData(_gridEntity, config);

            var state = _entityManager.GetComponentData<SpatialGridState>(_gridEntity);
            state.ActiveBufferIndex = 0;
            state.TotalEntries = 0;
            state.Version = 0;
            state.LastUpdateTick = 0;
            state.LastDirtyTick = 0;
            state.DirtyVersion = 0;
            state.DirtyAddCount = 0;
            state.DirtyUpdateCount = 0;
            state.DirtyRemoveCount = 0;
            state.LastRebuildMilliseconds = 0f;
            state.LastStrategy = SpatialGridRebuildStrategy.None;
            _entityManager.SetComponentData(_gridEntity, state);

            EnsureBuffer<SpatialGridEntry>(_gridEntity);
            EnsureBuffer<SpatialGridCellRange>(_gridEntity);
            EnsureBuffer<SpatialGridStagingEntry>(_gridEntity);
            EnsureBuffer<SpatialGridStagingCellRange>(_gridEntity);
            EnsureBuffer<SpatialGridDirtyOp>(_gridEntity);
            EnsureBuffer<SpatialGridEntryLookup>(_gridEntity);
        }

        private void EnsureBuffer<T>(Entity entity)
            where T : unmanaged, IBufferElementData
        {
            if (!_entityManager.HasBuffer<T>(entity))
            {
                _entityManager.AddBuffer<T>(entity);
            }
            else
            {
                var buffer = _entityManager.GetBuffer<T>(entity);
                buffer.Clear();
            }
        }

        private Entity CreateVillager(int villagerId, int factionId, float3 position, bool availability, float morale, float energy)
        {
            var entity = _entityManager.CreateEntity(
                typeof(VillagerId),
                typeof(VillagerJob),
                typeof(VillagerAvailability),
                typeof(VillagerNeeds),
                typeof(VillagerMood),
                typeof(VillagerDisciplineState),
                typeof(VillagerAIState),
                typeof(VillagerCombatStats),
                typeof(VillagerJobTicket),
                typeof(LocalTransform));

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));

            _entityManager.SetComponentData(entity, new VillagerId { Value = villagerId, FactionId = factionId });

            _entityManager.SetComponentData(entity, new VillagerJob
            {
                Type = VillagerJob.JobType.Gatherer,
                Phase = VillagerJob.JobPhase.Idle,
                ActiveTicketId = 77,
                Productivity = 0.9f,
                LastStateChangeTick = 12
            });

            _entityManager.SetComponentData(entity, new VillagerAvailability
            {
                IsAvailable = availability ? (byte)1 : (byte)0,
                IsReserved = 0,
                LastChangeTick = 11,
                BusyTime = 0f
            });

            _entityManager.SetComponentData(entity, new VillagerNeeds
            {
                Health = 90f,
                MaxHealth = 100f,
                Hunger = 10f,
                Energy = energy,
                Morale = morale,
                Temperature = 20f
            });

            _entityManager.SetComponentData(entity, new VillagerMood
            {
                Mood = morale,
                TargetMood = morale,
                MoodChangeRate = 0f,
                Wellbeing = morale
            });

            _entityManager.SetComponentData(entity, new VillagerDisciplineState
            {
                Value = VillagerDisciplineType.Forester,
                Level = 2,
                Experience = 10f
            });

            _villagerTargetEntity = _entityManager.CreateEntity();

            _entityManager.SetComponentData(entity, new VillagerAIState
            {
                CurrentState = VillagerAIState.State.Working,
                CurrentGoal = VillagerAIState.Goal.Work,
                TargetEntity = _villagerTargetEntity,
                TargetPosition = position,
                StateTimer = 1f,
                StateStartTick = 5
            });

            _entityManager.SetComponentData(entity, new VillagerCombatStats
            {
                AttackDamage = 10f,
                AttackSpeed = 1f,
                DefenseRating = 2f,
                AttackRange = 1.5f,
                CurrentTarget = _villagerTargetEntity,
                LastAttackTime = 0f
            });

            _entityManager.SetComponentData(entity, new VillagerJobTicket
            {
                TicketId = 77,
                JobType = VillagerJob.JobType.Gatherer,
                ResourceTypeIndex = 5,
                ResourceEntity = Entity.Null,
                StorehouseEntity = Entity.Null,
                Priority = 1,
                Phase = (byte)VillagerJob.JobPhase.Idle,
                ReservedUnits = 0f,
                AssignedTick = 30,
                LastProgressTick = 30
            });

            return entity;
        }

        private Entity CreateStorehouse(int storehouseId, float capacity, float stored, float reserved, float3 position)
        {
            var entity = _entityManager.CreateEntity(
                typeof(StorehouseConfig),
                typeof(StorehouseInventory),
                typeof(StorehouseJobReservation),
                typeof(LocalTransform));

            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));

            _entityManager.SetComponentData(entity, new StorehouseConfig
            {
                ShredRate = 1f,
                MaxShredQueueSize = 4,
                InputRate = 25f,
                OutputRate = 25f
            });

            _entityManager.SetComponentData(entity, new StorehouseInventory
            {
                TotalStored = stored,
                TotalCapacity = capacity,
                ItemTypeCount = 1,
                IsShredding = 0,
                LastUpdateTick = 17
            });

            _entityManager.SetComponentData(entity, new StorehouseJobReservation
            {
                ReservedCapacity = reserved,
                LastMutationTick = 19
            });

            var capacityBuffer = _entityManager.AddBuffer<StorehouseCapacityElement>(entity);
            capacityBuffer.Add(new StorehouseCapacityElement
            {
                ResourceTypeId = new FixedString64Bytes("wood"),
                MaxCapacity = capacity
            });

            var inventoryItems = _entityManager.AddBuffer<StorehouseInventoryItem>(entity);
            inventoryItems.Add(new StorehouseInventoryItem
            {
                ResourceTypeId = new FixedString64Bytes("wood"),
                Amount = stored,
                Reserved = 0f
            });

            var reservationItems = _entityManager.AddBuffer<StorehouseReservationItem>(entity);
            reservationItems.Add(new StorehouseReservationItem
            {
                ResourceTypeIndex = 0,
                Reserved = reserved
            });

            return entity;
        }

        private void MutateVillager(Entity entity, float morale, float energy)
        {
            var availability = _entityManager.GetComponentData<VillagerAvailability>(entity);
            availability.IsReserved = 1;
            availability.LastChangeTick = 50;
            _entityManager.SetComponentData(entity, availability);

            var needs = _entityManager.GetComponentData<VillagerNeeds>(entity);
            needs.Health = 75f;
            needs.Energy = energy;
            needs.Morale = morale;
            _entityManager.SetComponentData(entity, needs);

            var mood = _entityManager.GetComponentData<VillagerMood>(entity);
            mood.Mood = morale;
            mood.TargetMood = morale;
            _entityManager.SetComponentData(entity, mood);

            var discipline = _entityManager.GetComponentData<VillagerDisciplineState>(entity);
            discipline.Value = VillagerDisciplineType.Warrior;
            discipline.Level = 4;
            _entityManager.SetComponentData(entity, discipline);

            var job = _entityManager.GetComponentData<VillagerJob>(entity);
            job.Phase = VillagerJob.JobPhase.Assigned;
            job.ActiveTicketId = 88;
            _entityManager.SetComponentData(entity, job);

            var ticket = _entityManager.GetComponentData<VillagerJobTicket>(entity);
            ticket.TicketId = 88;
            ticket.ResourceTypeIndex = 6;
            _entityManager.SetComponentData(entity, ticket);
        }

        private void MutateStorehouse(Entity entity, float stored, float reserved)
        {
            var inventory = _entityManager.GetComponentData<StorehouseInventory>(entity);
            inventory.TotalStored = stored;
            inventory.LastUpdateTick = 43;
            _entityManager.SetComponentData(entity, inventory);

            var reservation = _entityManager.GetComponentData<StorehouseJobReservation>(entity);
            reservation.ReservedCapacity = reserved;
            reservation.LastMutationTick = 43;
            _entityManager.SetComponentData(entity, reservation);

            var inventoryItems = _entityManager.GetBuffer<StorehouseInventoryItem>(entity);
            inventoryItems[0] = new StorehouseInventoryItem
            {
                ResourceTypeId = new FixedString64Bytes("wood"),
                Amount = stored,
                Reserved = 5f
            };

            var reservationItems = _entityManager.GetBuffer<StorehouseReservationItem>(entity);
            reservationItems[0] = new StorehouseReservationItem
            {
                ResourceTypeIndex = 0,
                Reserved = math.max(0f, reserved - 5f)
            };
        }

        private void StepSpatialSystems()
        {
            UpdateSystem(_spatialIndexingHandle);
            UpdateSystem(_spatialDirtyHandle);
            UpdateSystem(_spatialBuildHandle);
        }

        private void StepSyncSystems()
        {
            UpdateSystem(_villagerSyncHandle);
            UpdateSystem(_storehouseSyncHandle);
            UpdateSystem(_endSimulationEcbHandle);
        }

        private void UpdateSystem(SystemHandle handle)
        {
            handle.Update(_world.Unmanaged);
        }
    }
}
