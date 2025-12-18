#if SPACE4X_MIRACLES_WIP
// TODO: Update these tests to the new miracle API in PureDOTS.Runtime.Miracles and re-enable SPACE4X_MIRACLES_WIP.
using System.Collections.Generic;
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry.Tests
{
    public class Space4XRegistryBridgeSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        private SystemHandle _bridgeHandle;
        private SystemHandle _telemetryHandle;
        private SystemHandle _directoryHandle;
        private SystemHandle _miracleRegistryHandle;

        private Entity _telemetryEntity;
        private SpatialGridConfig _gridConfig;
        private uint _gridVersion;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XRegistryBridgeSystemTests");
            _entityManager = _world.EntityManager;

            EnsureTimeState();
            EnsureRegistryDirectory();
            EnsureTelemetryStream();
            EnsureSpatialGrid();
            EnsureRegistrySpatialSync();
            EnsureMiracleRegistry();
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            _bridgeHandle = _world.GetOrCreateSystem<Space4XRegistryBridgeSystem>();
            _telemetryHandle = _world.GetOrCreateSystem<Space4XRegistryTelemetrySystem>();
            _directoryHandle = _world.GetOrCreateSystem<RegistryDirectorySystem>();
            // Note: MiracleRegistrySystem is managed by PureDOTS runtime, not Space4x
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
        public void BridgeRegistersColoniesAndFleetsAndEmitsTelemetry()
        {
            CreateColony("SOL-1", 150_000f, 800f, new float3(-30f, 0f, 20f), Space4XColonyStatus.Growing, addResidency: true, residencyCellId: 42);
            CreateFleet("FLEET-ALPHA", 6, Space4XFleetPosture.Engaging, new float3(-5f, 0f, 20f));
            CreateLogisticsRoute("ROUTE-SOL-ALPHA", "SOL-1", "ALPHA-2", 200f, 0.72f, new float3(12f, 0f, 18f), Space4XLogisticsRouteStatus.Operational, priority: 2, addResidency: true, residencyCellId: -1);
            CreateAnomaly("ANOM-PRIME", "Gravitic Rift", Space4XAnomalySeverity.Critical, Space4XAnomalyState.Active, 0.92f, 9, new float3(-22f, 0f, 30f));
            CreateMiracle(MiracleType.Fireball, MiracleCastingMode.Sustained, MiracleLifecycleState.Active, 25f, 5f, 3f, new float3(0f, 0f, -10f));
            CreateMiracle(MiracleType.Shield, MiracleCastingMode.Instant, MiracleLifecycleState.CoolingDown, 15f, 0f, 12f, new float3(8f, 0f, -6f));

            UpdateSystem(_miracleRegistryHandle);
            UpdateSystem(_bridgeHandle);
            UpdateSystem(_directoryHandle);
            UpdateSystem(_telemetryHandle);

            var colonyRegistryEntity = _entityManager.CreateEntityQuery(typeof(Space4XColonyRegistry)).GetSingletonEntity();
            var colonyRegistry = _entityManager.GetComponentData<Space4XColonyRegistry>(colonyRegistryEntity);
            Assert.AreEqual(1, colonyRegistry.ColonyCount);
            Assert.Greater(colonyRegistry.TotalPopulation, 0f);
            Assert.AreEqual(42u, colonyRegistry.LastUpdateTick);
            Assert.AreEqual(_gridVersion, colonyRegistry.LastSpatialVersion);
            Assert.AreEqual(1, colonyRegistry.SpatialResolvedCount);
            Assert.AreEqual(0, colonyRegistry.SpatialFallbackCount);
            Assert.AreEqual(0, colonyRegistry.SpatialUnmappedCount);

            var colonyBuffer = _entityManager.GetBuffer<Space4XColonyRegistryEntry>(colonyRegistryEntity);
            Assert.AreEqual(1, colonyBuffer.Length);
            Assert.AreEqual("SOL-1", colonyBuffer[0].ColonyId.ToString());
            Assert.AreEqual(42, colonyBuffer[0].CellId);
            Assert.AreEqual(_gridVersion, colonyBuffer[0].SpatialVersion);

            var fleetRegistryEntity = _entityManager.CreateEntityQuery(typeof(Space4XFleetRegistry)).GetSingletonEntity();
            var fleetRegistry = _entityManager.GetComponentData<Space4XFleetRegistry>(fleetRegistryEntity);
            Assert.AreEqual(1, fleetRegistry.FleetCount);
            Assert.AreEqual(1, fleetRegistry.ActiveEngagementCount);
            Assert.AreEqual(42u, fleetRegistry.LastUpdateTick);
            Assert.AreEqual(_gridVersion, fleetRegistry.LastSpatialVersion);
            Assert.AreEqual(0, fleetRegistry.SpatialResolvedCount);
            Assert.AreEqual(1, fleetRegistry.SpatialFallbackCount);
            Assert.AreEqual(0, fleetRegistry.SpatialUnmappedCount);

            var fleetBuffer = _entityManager.GetBuffer<Space4XFleetRegistryEntry>(fleetRegistryEntity);
            Assert.AreEqual(1, fleetBuffer.Length);
            Assert.AreEqual("FLEET-ALPHA", fleetBuffer[0].FleetId.ToString());
            var expectedFleetCell = QuantizeCellId(new float3(-5f, 0f, 20f));
            Assert.AreEqual(expectedFleetCell, fleetBuffer[0].CellId);
            Assert.AreEqual(_gridVersion, fleetBuffer[0].SpatialVersion);

            var logisticsRegistryEntity = _entityManager.CreateEntityQuery(typeof(Space4XLogisticsRegistry)).GetSingletonEntity();
            var logisticsRegistry = _entityManager.GetComponentData<Space4XLogisticsRegistry>(logisticsRegistryEntity);
            Assert.AreEqual(1, logisticsRegistry.RouteCount);
            Assert.AreEqual(1, logisticsRegistry.HighRiskRouteCount);
            Assert.Greater(logisticsRegistry.TotalDailyThroughput, 0f);
            Assert.AreEqual(42u, logisticsRegistry.LastUpdateTick);
            Assert.AreEqual(_gridVersion, logisticsRegistry.LastSpatialVersion);
            Assert.AreEqual(0, logisticsRegistry.SpatialResolvedCount);
            Assert.AreEqual(1, logisticsRegistry.SpatialFallbackCount);
            Assert.AreEqual(0, logisticsRegistry.SpatialUnmappedCount);

            var logisticsBuffer = _entityManager.GetBuffer<Space4XLogisticsRegistryEntry>(logisticsRegistryEntity);
            Assert.AreEqual(1, logisticsBuffer.Length);
            Assert.AreEqual("ROUTE-SOL-ALPHA", logisticsBuffer[0].RouteId.ToString());
            Assert.AreEqual(Space4XLogisticsRouteStatus.Operational, logisticsBuffer[0].Status);
            var expectedRouteCell = QuantizeCellId(new float3(12f, 0f, 18f));
            Assert.AreEqual(expectedRouteCell, logisticsBuffer[0].CellId);
            Assert.AreEqual(_gridVersion, logisticsBuffer[0].SpatialVersion);

            var anomalyRegistryEntity = _entityManager.CreateEntityQuery(typeof(Space4XAnomalyRegistry)).GetSingletonEntity();
            var anomalyRegistry = _entityManager.GetComponentData<Space4XAnomalyRegistry>(anomalyRegistryEntity);
            Assert.AreEqual(1, anomalyRegistry.AnomalyCount);
            Assert.AreEqual(1, anomalyRegistry.ActiveAnomalyCount);
            Assert.AreEqual(Space4XAnomalySeverity.Critical, anomalyRegistry.HighestSeverity);
            Assert.AreEqual(42u, anomalyRegistry.LastUpdateTick);
            Assert.AreEqual(_gridVersion, anomalyRegistry.LastSpatialVersion);
            Assert.AreEqual(0, anomalyRegistry.SpatialResolvedCount);
            Assert.AreEqual(1, anomalyRegistry.SpatialFallbackCount);
            Assert.AreEqual(0, anomalyRegistry.SpatialUnmappedCount);

            var anomalyBuffer = _entityManager.GetBuffer<Space4XAnomalyRegistryEntry>(anomalyRegistryEntity);
            Assert.AreEqual(1, anomalyBuffer.Length);
            Assert.AreEqual("ANOM-PRIME", anomalyBuffer[0].AnomalyId.ToString());
            var expectedAnomalyCell = QuantizeCellId(new float3(-22f, 0f, 30f));
            Assert.AreEqual(expectedAnomalyCell, anomalyBuffer[0].CellId);
            Assert.AreEqual(_gridVersion, anomalyBuffer[0].SpatialVersion);

            var miracleRegistryEntity = _entityManager.CreateEntityQuery(typeof(PureDOTS.Runtime.Registry.MiracleRegistry)).GetSingletonEntity();
            var miracleRegistry = _entityManager.GetComponentData<PureDOTS.Runtime.Registry.MiracleRegistry>(miracleRegistryEntity);
            Assert.AreEqual(2, miracleRegistry.TotalMiracles);
            Assert.AreEqual(1, miracleRegistry.ActiveMiracles);
            Assert.AreEqual(30f, miracleRegistry.TotalEnergyCost);
            Assert.AreEqual(15f, miracleRegistry.TotalCooldownSeconds);

            var directoryEntity = _entityManager.CreateEntityQuery(typeof(RegistryDirectory)).GetSingletonEntity();
            var directoryEntries = _entityManager.GetBuffer<RegistryDirectoryEntry>(directoryEntity);
            Assert.IsTrue(directoryEntries.Length >= 4, "Expected custom registries to appear in the neutral directory");

            var snapshot = _entityManager.CreateEntityQuery(typeof(Space4XRegistrySnapshot)).GetSingleton<Space4XRegistrySnapshot>();
            Assert.AreEqual(1, snapshot.ColonyCount);
            Assert.AreEqual(1, snapshot.FleetCount);
            Assert.AreEqual(1, snapshot.FleetEngagementCount);
            Assert.AreEqual(1, snapshot.LogisticsRouteCount);
            Assert.AreEqual(1, snapshot.ActiveLogisticsRouteCount);
            Assert.AreEqual(1, snapshot.HighRiskRouteCount);
            var expectedDemand = Space4XColonySupply.ComputeDemand(150_000f);
            Assert.AreEqual(expectedDemand, snapshot.ColonySupplyDemandTotal);
            Assert.AreEqual(0f, snapshot.ColonySupplyShortageTotal);
            Assert.Greater(snapshot.ColonyAverageSupplyRatio, 1f);
            Assert.AreEqual(0, snapshot.ColonyBottleneckCount);
            Assert.AreEqual(0, snapshot.ColonyCriticalCount);
            Assert.AreEqual(1, snapshot.AnomalyCount);
            Assert.AreEqual(1, snapshot.ActiveAnomalyCount);
            Assert.AreEqual(Space4XAnomalySeverity.Critical, snapshot.HighestAnomalySeverity);
            Assert.AreEqual(2, snapshot.MiracleCount);
            Assert.AreEqual(1, snapshot.ActiveMiracleCount);
            Assert.AreEqual(30f, snapshot.MiracleTotalEnergyCost);
            Assert.AreEqual(15f, snapshot.MiracleTotalCooldownSeconds);
            Assert.AreEqual(0.625f, snapshot.MiracleAverageChargePercent, 1e-4f);
            Assert.AreEqual(1f / 60f, snapshot.MiracleAverageCastLatencySeconds, 1e-4f);
            Assert.AreEqual(1, snapshot.MiracleCancellationCount);
            Assert.AreEqual(42u, snapshot.LastRegistryTick);

            var telemetryBuffer = _entityManager.GetBuffer<TelemetryMetric>(_telemetryEntity);
            Assert.GreaterOrEqual(telemetryBuffer.Length, 15);
            var telemetryKeys = new List<string>(telemetryBuffer.Length);
            for (int i = 0; i < telemetryBuffer.Length; i++)
            {
                telemetryKeys.Add(telemetryBuffer[i].Key.ToString());
            }

            Assert.Contains("space4x.registry.colonies", telemetryKeys);
            Assert.Contains("space4x.registry.colonies.supply.demand", telemetryKeys);
            Assert.Contains("space4x.registry.colonies.supply.avgRatio", telemetryKeys);
            Assert.Contains("space4x.registry.fleets", telemetryKeys);
            Assert.Contains("space4x.registry.fleets.engaging", telemetryKeys);
            Assert.Contains("space4x.registry.logisticsRoutes", telemetryKeys);
            Assert.Contains("space4x.registry.logisticsRoutes.highRisk", telemetryKeys);
            Assert.Contains("space4x.registry.anomalies", telemetryKeys);
            Assert.Contains("space4x.registry.anomalies.highestSeverity", telemetryKeys);
            Assert.Contains("space4x.miracles.total", telemetryKeys);
            Assert.Contains("space4x.miracles.active", telemetryKeys);
            Assert.Contains("space4x.miracles.energy", telemetryKeys);
            Assert.Contains("space4x.miracles.cooldownSeconds", telemetryKeys);
            Assert.Contains("space4x.miracles.averageCharge", telemetryKeys);
            Assert.Contains("space4x.miracles.castLatencyMs", telemetryKeys);
            Assert.Contains("space4x.miracles.cancellations", telemetryKeys);
            Assert.Contains("space4x.registry.techdiffusion.active", telemetryKeys);
            Assert.Contains("space4x.registry.techdiffusion.completed", telemetryKeys);

            AssertMetadataHasSpatialData(colonyRegistryEntity, resolved: 1, fallback: 0, unmapped: 0);
            AssertMetadataHasSpatialData(fleetRegistryEntity, resolved: 0, fallback: 1, unmapped: 0);
            AssertMetadataHasSpatialData(logisticsRegistryEntity, resolved: 0, fallback: 1, unmapped: 0);
            AssertMetadataHasSpatialData(anomalyRegistryEntity, resolved: 0, fallback: 1, unmapped: 0);
        }

        private void EnsureTimeState()
        {
            var timeEntity = _entityManager.CreateEntity(typeof(TimeState));
            _entityManager.SetComponentData(timeEntity, new TimeState
            {
                Tick = 42,
                FixedDeltaTime = 1f / 60f,
                CurrentSpeedMultiplier = 1f,
                IsPaused = false
            });
        }

        private void EnsureRegistryDirectory()
        {
            var directoryEntity = _entityManager.CreateEntity(typeof(RegistryDirectory));
            _entityManager.AddBuffer<RegistryDirectoryEntry>(directoryEntity);
        }

        private void EnsureTelemetryStream()
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            if (query.IsEmptyIgnoreFilter)
            {
                _telemetryEntity = _entityManager.CreateEntity(typeof(TelemetryStream));
                _entityManager.SetComponentData(_telemetryEntity, new TelemetryStream
                {
                    Version = 0,
                    LastTick = 0
                });
            }
            else
            {
                _telemetryEntity = query.GetSingletonEntity();
            }

            if (!_entityManager.HasBuffer<TelemetryMetric>(_telemetryEntity))
            {
                _entityManager.AddBuffer<TelemetryMetric>(_telemetryEntity);
            }

            TelemetryStreamUtility.EnsureEventStream(_entityManager);
        }

        private void EnsureSpatialGrid()
        {
            _gridConfig = new SpatialGridConfig
            {
                CellSize = 10f,
                WorldMin = new float3(-50f, -50f, -50f),
                WorldMax = new float3(50f, 50f, 50f),
                CellCounts = new int3(10, 10, 10),
                HashSeed = 17u,
                ProviderId = 1
            };

            _gridVersion = 7u;

            var entity = _entityManager.CreateEntity(typeof(SpatialGridConfig), typeof(SpatialGridState));
            _entityManager.SetComponentData(entity, _gridConfig);
            _entityManager.SetComponentData(entity, new SpatialGridState
            {
                ActiveBufferIndex = 0,
                TotalEntries = 0,
                Version = _gridVersion,
                LastUpdateTick = 40u,
                LastDirtyTick = 39u,
                DirtyVersion = 1u,
                DirtyAddCount = 0,
                DirtyUpdateCount = 0,
                DirtyRemoveCount = 0,
                LastRebuildMilliseconds = 0f,
                LastStrategy = SpatialGridRebuildStrategy.Partial
            });
        }

        private void EnsureRegistrySpatialSync()
        {
            var entity = _entityManager.CreateEntity(typeof(RegistrySpatialSyncState));
            _entityManager.SetComponentData(entity, new RegistrySpatialSyncState
            {
                SpatialVersion = _gridVersion,
                LastPublishedTick = 40u,
                HasSpatialDataFlag = 1
            });
        }

        private void EnsureMiracleRegistry()
        {
            using var query = _entityManager.CreateEntityQuery(typeof(PureDOTS.Runtime.Registry.MiracleRegistry));
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = _entityManager.CreateEntity(typeof(PureDOTS.Runtime.Registry.MiracleRegistry));
            _entityManager.SetComponentData(entity, new PureDOTS.Runtime.Registry.MiracleRegistry
            {
                TotalMiracles = 0,
                ActiveMiracles = 0,
                TotalEnergyCost = 0f,
                TotalCooldownSeconds = 0f
            });
        }

        private void CreateColony(string id, float population, float storedResources, float3 position, Space4XColonyStatus status, bool addResidency = false, int residencyCellId = 0)
        {
            var entity = _entityManager.CreateEntity(typeof(Space4XColony), typeof(LocalTransform), typeof(SpatialIndexedTag));
            _entityManager.SetComponentData(entity, new Space4XColony
            {
                ColonyId = new FixedString64Bytes(id),
                Population = population,
                StoredResources = storedResources,
                SectorId = 7,
                Status = status
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            if (addResidency)
            {
                _entityManager.AddComponentData(entity, new SpatialGridResidency
                {
                    CellId = residencyCellId,
                    LastPosition = position,
                    Version = _gridVersion
                });
            }
        }

        private void CreateFleet(string id, int ships, Space4XFleetPosture posture, float3 position, bool addResidency = false, int residencyCellId = 0)
        {
            var entity = _entityManager.CreateEntity(typeof(Space4XFleet), typeof(LocalTransform), typeof(SpatialIndexedTag));
            _entityManager.SetComponentData(entity, new Space4XFleet
            {
                FleetId = new FixedString64Bytes(id),
                ShipCount = ships,
                Posture = posture,
                TaskForce = 101
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            if (addResidency)
            {
                _entityManager.AddComponentData(entity, new SpatialGridResidency
                {
                    CellId = residencyCellId,
                    LastPosition = position,
                    Version = _gridVersion
                });
            }
        }

        private void CreateLogisticsRoute(string id, string origin, string destination, float throughput, float risk, float3 position, Space4XLogisticsRouteStatus status, int priority, bool addResidency = false, int residencyCellId = 0)
        {
            var entity = _entityManager.CreateEntity(typeof(Space4XLogisticsRoute), typeof(LocalTransform), typeof(SpatialIndexedTag));
            _entityManager.SetComponentData(entity, new Space4XLogisticsRoute
            {
                RouteId = new FixedString64Bytes(id),
                OriginColonyId = new FixedString64Bytes(origin),
                DestinationColonyId = new FixedString64Bytes(destination),
                DailyThroughput = throughput,
                Risk = risk,
                Priority = priority,
                Status = status
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            if (addResidency)
            {
                _entityManager.AddComponentData(entity, new SpatialGridResidency
                {
                    CellId = residencyCellId,
                    LastPosition = position,
                    Version = _gridVersion
                });
            }
        }

        private void CreateAnomaly(string id, string classification, Space4XAnomalySeverity severity, Space4XAnomalyState state, float instability, int sectorId, float3 position, bool addResidency = false, int residencyCellId = 0)
        {
            var entity = _entityManager.CreateEntity(typeof(Space4XAnomaly), typeof(LocalTransform), typeof(SpatialIndexedTag));
            _entityManager.SetComponentData(entity, new Space4XAnomaly
            {
                AnomalyId = new FixedString64Bytes(id),
                Classification = new FixedString64Bytes(classification),
                Severity = severity,
                State = state,
                Instability = instability,
                SectorId = sectorId
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            if (addResidency)
            {
                _entityManager.AddComponentData(entity, new SpatialGridResidency
                {
                    CellId = residencyCellId,
                    LastPosition = position,
                    Version = _gridVersion
                });
            }
        }

        private void CreateMiracle(
            MiracleType type,
            MiracleCastingMode castingMode,
            MiracleLifecycleState lifecycle,
            float baseCost,
            float sustainedCostPerSecond,
            float cooldownSecondsRemaining,
            float3 position)
        {
            var entity = _entityManager.CreateEntity(typeof(MiracleDefinition), typeof(MiracleRuntimeState), typeof(LocalTransform));
            _entityManager.SetComponentData(entity, new MiracleDefinition
            {
                Type = type,
                CastingMode = castingMode,
                BaseRadius = 10f,
                BaseIntensity = 1f,
                BaseCost = baseCost,
                SustainedCostPerSecond = sustainedCostPerSecond
            });
            _entityManager.SetComponentData(entity, new MiracleRuntimeState
            {
                Lifecycle = lifecycle,
                ChargePercent = lifecycle == MiracleLifecycleState.Active ? 1f : 0.25f,
                CurrentRadius = 10f,
                CurrentIntensity = 1f,
                CooldownSecondsRemaining = cooldownSecondsRemaining,
                LastCastTick = 41,
                AlignmentDelta = 0
            });
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            _entityManager.AddComponentData(entity, new MiracleTarget
            {
                TargetPosition = position,
                TargetEntity = Entity.Null
            });
        }

        private void UpdateSystem(SystemHandle handle)
        {
            handle.Update(_world.Unmanaged);
        }

        private int QuantizeCellId(float3 position)
        {
            SpatialHash.Quantize(position, _gridConfig, out var coords);
            return SpatialHash.Flatten(in coords, in _gridConfig);
        }

        private void AssertMetadataHasSpatialData(Entity registryEntity, int resolved, int fallback, int unmapped)
        {
            var metadata = _entityManager.GetComponentData<RegistryMetadata>(registryEntity);
            Assert.IsTrue(metadata.SupportsSpatialQueries, "Registry should support spatial queries");
            Assert.IsTrue(metadata.Continuity.HasSpatialData, "Registry should publish spatial continuity");
            Assert.IsTrue(metadata.Continuity.RequiresSpatialSync, "Registry should require spatial sync when spatial data is present");
            Assert.AreEqual(_gridVersion, metadata.Continuity.SpatialVersion);
            Assert.AreEqual(resolved, metadata.Continuity.SpatialResolvedCount);
            Assert.AreEqual(fallback, metadata.Continuity.SpatialFallbackCount);
            Assert.AreEqual(unmapped, metadata.Continuity.SpatialUnmappedCount);
        }
    }
}
#endif
