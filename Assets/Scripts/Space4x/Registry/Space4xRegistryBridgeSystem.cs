using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Aligns the Space4X project with the shared DOTS registries by mirroring colony and fleet data.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(RegistrySpatialSyncSystem))]
    public partial struct Space4XRegistryBridgeSystem : ISystem
    {
        private EntityQuery _colonyQuery;
        private EntityQuery _fleetQuery;
        private EntityQuery _logisticsRouteQuery;
        private EntityQuery _anomalyQuery;

        private Entity _colonyRegistryEntity;
        private Entity _fleetRegistryEntity;
        private Entity _logisticsRegistryEntity;
        private Entity _anomalyRegistryEntity;
        private Entity _snapshotEntity;

        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RegistryDirectory>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<MiracleRegistry>();

            _colonyQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XColony, LocalTransform, SpatialIndexedTag>()
                .Build();

            _fleetQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XFleet, LocalTransform, SpatialIndexedTag>()
                .Build();

            _logisticsRouteQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XLogisticsRoute, LocalTransform, SpatialIndexedTag>()
                .Build();

            _anomalyQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XAnomaly, LocalTransform, SpatialIndexedTag>()
                .Build();

            var colonyLabel = new FixedString64Bytes("Space4X Colonies");
            var fleetLabel = new FixedString64Bytes("Space4X Fleets");
            var logisticsLabel = new FixedString64Bytes("Space4X Logistics Routes");
            var anomalyLabel = new FixedString64Bytes("Space4X Anomalies");

            _colonyRegistryEntity = EnsureRegistryEntity<Space4XColonyRegistry, Space4XColonyRegistryEntry>(ref state, colonyLabel, Space4XRegistryIds.ColonyArchetype, RegistryHandleFlags.SupportsSpatialQueries);
            _fleetRegistryEntity = EnsureRegistryEntity<Space4XFleetRegistry, Space4XFleetRegistryEntry>(ref state, fleetLabel, Space4XRegistryIds.FleetArchetype, RegistryHandleFlags.SupportsSpatialQueries);
            _logisticsRegistryEntity = EnsureRegistryEntity<Space4XLogisticsRegistry, Space4XLogisticsRegistryEntry>(ref state, logisticsLabel, Space4XRegistryIds.LogisticsRouteArchetype, RegistryHandleFlags.SupportsSpatialQueries);
            _anomalyRegistryEntity = EnsureRegistryEntity<Space4XAnomalyRegistry, Space4XAnomalyRegistryEntry>(ref state, anomalyLabel, Space4XRegistryIds.AnomalyArchetype, RegistryHandleFlags.SupportsSpatialQueries);

            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(true);

            using var snapshotQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XRegistrySnapshot>());
            if (snapshotQuery.IsEmptyIgnoreFilter)
            {
                _snapshotEntity = state.EntityManager.CreateEntity(typeof(Space4XRegistrySnapshot));
            }
            else
            {
                _snapshotEntity = snapshotQuery.GetSingletonEntity();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var tick = timeState.Tick;

            _residencyLookup.Update(ref state);

            var hasGridConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasGridState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasGridConfig && hasGridState && gridConfig.CellCount > 0 && gridConfig.CellSize > 0f;
            var hasSyncState = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState syncState);

            UpdateColonyRegistry(ref state, tick, hasSpatial, gridConfig, gridState, hasSyncState, syncState);
            UpdateFleetRegistry(ref state, tick, hasSpatial, gridConfig, gridState, hasSyncState, syncState);
            UpdateLogisticsRegistry(ref state, tick, hasSpatial, gridConfig, gridState, hasSyncState, syncState);
            UpdateAnomalyRegistry(ref state, tick, hasSpatial, gridConfig, gridState, hasSyncState, syncState);
            UpdateMiracleSnapshot(ref state, tick, timeState.FixedDeltaTime);
            UpdateTechDiffusionSnapshot(ref state, tick);
        }

        private void UpdateColonyRegistry(ref SystemState state, uint tick, bool hasSpatial, in SpatialGridConfig gridConfig, in SpatialGridState gridState, bool hasSyncState, in RegistrySpatialSyncState syncState)
        {
            var colonyCount = _colonyQuery.CalculateEntityCount();

            using var builder = new DeterministicRegistryBuilder<Space4XColonyRegistryEntry>(colonyCount, Allocator.Temp);

            float totalPopulation = 0f;
            float totalResources = 0f;
            float totalSupplyDemand = 0f;
            float totalSupplyShortage = 0f;
            float supplyRatioSum = 0f;
            int supplySamples = 0;
            int bottleneckCount = 0;
            int criticalCount = 0;
            int resolvedCount = 0;
            int fallbackCount = 0;
            int unmappedCount = 0;
            var syncHasData = hasSyncState && syncState.HasSpatialData;

            foreach (var (colony, transform, entity) in SystemAPI.Query<RefRO<Space4XColony>, RefRO<LocalTransform>>().WithAll<SpatialIndexedTag>().WithEntityAccess())
            {
                var colonyData = colony.ValueRO;
                var position = transform.ValueRO.Position;
                var demand = Space4XColonySupply.ComputeDemand(colonyData.Population);
                var supplyRatio = Space4XColonySupply.ComputeSupplyRatio(colonyData.StoredResources, demand);
                var supplyShortage = Space4XColonySupply.ComputeShortage(colonyData.StoredResources, demand);
                var flags = Space4XRegistryFlags.FromColonyStatus(colonyData.Status);
                flags |= Space4XRegistryFlags.ApplyColonySupply(supplyRatio);

                var cellId = -1;
                var usedResidency = false;

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)gridConfig.CellCount)
                    {
                        cellId = residency.CellId;
                        resolvedCount++;
                        usedResidency = true;
                    }
                }

                if (!usedResidency && hasSpatial)
                {
                    SpatialHash.Quantize(position, gridConfig, out var coords);
                    var flattened = SpatialHash.Flatten(in coords, in gridConfig);
                    if ((uint)flattened < (uint)gridConfig.CellCount)
                    {
                        cellId = flattened;
                        fallbackCount++;
                    }
                    else
                    {
                        unmappedCount++;
                    }
                }
                else if (!usedResidency && !hasSpatial)
                {
                    unmappedCount++;
                }

                builder.Add(new Space4XColonyRegistryEntry
                {
                    ColonyEntity = entity,
                    ColonyId = colonyData.ColonyId,
                    Population = colonyData.Population,
                    StoredResources = colonyData.StoredResources,
                    SupplyDemand = demand,
                    SupplyRatio = supplyRatio,
                    SupplyShortage = supplyShortage,
                    WorldPosition = position,
                    SectorId = colonyData.SectorId,
                    Status = colonyData.Status,
                    Flags = flags,
                    CellId = cellId,
                    SpatialVersion = hasSpatial ? gridState.Version : (syncHasData ? syncState.SpatialVersion : 0u)
                });

                totalPopulation += colonyData.Population;
                totalResources += colonyData.StoredResources;
                totalSupplyDemand += demand;
                totalSupplyShortage += supplyShortage;
                supplyRatioSum += supplyRatio;
                supplySamples++;

                if (supplyRatio < Space4XColonySupply.BottleneckThreshold)
                {
                    bottleneckCount++;
                }

                if (supplyRatio < Space4XColonySupply.CriticalThreshold)
                {
                    criticalCount++;
                }
            }

            var buffer = state.EntityManager.GetBuffer<Space4XColonyRegistryEntry>(_colonyRegistryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(_colonyRegistryEntity).ValueRW;
            var requireSpatialSync = metadata.SupportsSpatialQueries && syncHasData;
            var spatialVersion = hasSpatial ? gridState.Version : (syncHasData ? syncState.SpatialVersion : 0u);
            var provideSpatialData = hasSpatial || syncHasData;
            var continuity = provideSpatialData
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);
            builder.ApplyTo(ref buffer, ref metadata, tick, continuity);

            ref var summary = ref SystemAPI.GetComponentRW<Space4XColonyRegistry>(_colonyRegistryEntity).ValueRW;
            summary.ColonyCount = buffer.Length;
            summary.TotalPopulation = totalPopulation;
            summary.TotalStoredResources = totalResources;
            summary.TotalSupplyDemand = totalSupplyDemand;
            summary.TotalSupplyShortage = totalSupplyShortage;
            summary.AverageSupplyRatio = supplySamples > 0 ? supplyRatioSum / supplySamples : 0f;
            summary.BottleneckColonyCount = bottleneckCount;
            summary.CriticalColonyCount = criticalCount;
            summary.LastUpdateTick = tick;
            summary.LastSpatialVersion = spatialVersion;
            summary.SpatialResolvedCount = resolvedCount;
            summary.SpatialFallbackCount = fallbackCount;
            summary.SpatialUnmappedCount = unmappedCount;

            ref var snapshot = ref SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(_snapshotEntity).ValueRW;
            snapshot.ColonyCount = buffer.Length;
            snapshot.ColonySupplyDemandTotal = totalSupplyDemand;
            snapshot.ColonySupplyShortageTotal = totalSupplyShortage;
            snapshot.ColonyAverageSupplyRatio = summary.AverageSupplyRatio;
            snapshot.ColonyBottleneckCount = bottleneckCount;
            snapshot.ColonyCriticalCount = criticalCount;
            snapshot.LastRegistryTick = math.max(snapshot.LastRegistryTick, tick);
        }

        private void UpdateTechDiffusionSnapshot(ref SystemState state, uint tick)
        {
            if (!SystemAPI.TryGetSingleton<TechDiffusionTelemetry>(out var diffusionTelemetry))
            {
                return;
            }

            ref var snapshot = ref SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(_snapshotEntity).ValueRW;
            snapshot.TechDiffusionActiveCount = diffusionTelemetry.ActiveDiffusions;
            snapshot.TechDiffusionCompletedCount = (int)diffusionTelemetry.CompletedUpgrades;
            snapshot.TechDiffusionLastUpgradeTick = diffusionTelemetry.LastUpgradeTick;
            snapshot.LastRegistryTick = math.max(snapshot.LastRegistryTick, tick);
        }

        private void UpdateFleetRegistry(ref SystemState state, uint tick, bool hasSpatial, in SpatialGridConfig gridConfig, in SpatialGridState gridState, bool hasSyncState, in RegistrySpatialSyncState syncState)
        {
            var fleetCount = _fleetQuery.CalculateEntityCount();

            using var builder = new DeterministicRegistryBuilder<Space4XFleetRegistryEntry>(fleetCount, Allocator.Temp);

            int totalShips = 0;
            int engagementCount = 0;
            int resolvedCount = 0;
            int fallbackCount = 0;
            int unmappedCount = 0;
            var syncHasData = hasSyncState && syncState.HasSpatialData;

            foreach (var (fleet, transform, entity) in SystemAPI.Query<RefRO<Space4XFleet>, RefRO<LocalTransform>>().WithAll<SpatialIndexedTag>().WithEntityAccess())
            {
                var fleetData = fleet.ValueRO;
                var position = transform.ValueRO.Position;
                var flags = Space4XRegistryFlags.FromFleetPosture(fleetData.Posture);

                var cellId = -1;
                var usedResidency = false;

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)gridConfig.CellCount)
                    {
                        cellId = residency.CellId;
                        resolvedCount++;
                        usedResidency = true;
                    }
                }

                if (!usedResidency && hasSpatial)
                {
                    SpatialHash.Quantize(position, gridConfig, out var coords);
                    var flattened = SpatialHash.Flatten(in coords, in gridConfig);
                    if ((uint)flattened < (uint)gridConfig.CellCount)
                    {
                        cellId = flattened;
                        fallbackCount++;
                    }
                    else
                    {
                        unmappedCount++;
                    }
                }
                else if (!usedResidency && !hasSpatial)
                {
                    unmappedCount++;
                }

                builder.Add(new Space4XFleetRegistryEntry
                {
                    FleetEntity = entity,
                    FleetId = fleetData.FleetId,
                    ShipCount = fleetData.ShipCount,
                    Posture = fleetData.Posture,
                    WorldPosition = position,
                    Flags = flags,
                    CellId = cellId,
                    SpatialVersion = hasSpatial ? gridState.Version : (syncHasData ? syncState.SpatialVersion : 0u)
                });

                totalShips += fleetData.ShipCount;
                if ((flags & Space4XRegistryFlags.FleetEngaging) != 0)
                {
                    engagementCount++;
                }
            }

            var buffer = state.EntityManager.GetBuffer<Space4XFleetRegistryEntry>(_fleetRegistryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(_fleetRegistryEntity).ValueRW;
            var requireSpatialSync = metadata.SupportsSpatialQueries && syncHasData;
            var spatialVersion = hasSpatial ? gridState.Version : (syncHasData ? syncState.SpatialVersion : 0u);
            var provideSpatialData = hasSpatial || syncHasData;
            var continuity = provideSpatialData
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);
            builder.ApplyTo(ref buffer, ref metadata, tick, continuity);

            ref var summary = ref SystemAPI.GetComponentRW<Space4XFleetRegistry>(_fleetRegistryEntity).ValueRW;
            summary.FleetCount = buffer.Length;
            summary.TotalShips = totalShips;
            summary.ActiveEngagementCount = engagementCount;
            summary.LastUpdateTick = tick;
            summary.LastSpatialVersion = spatialVersion;
            summary.SpatialResolvedCount = resolvedCount;
            summary.SpatialFallbackCount = fallbackCount;
            summary.SpatialUnmappedCount = unmappedCount;

            ref var snapshot = ref SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(_snapshotEntity).ValueRW;
            snapshot.FleetCount = buffer.Length;
            snapshot.FleetEngagementCount = engagementCount;
            snapshot.LastRegistryTick = math.max(snapshot.LastRegistryTick, tick);
        }

        private void UpdateLogisticsRegistry(ref SystemState state, uint tick, bool hasSpatial, in SpatialGridConfig gridConfig, in SpatialGridState gridState, bool hasSyncState, in RegistrySpatialSyncState syncState)
        {
            var routeCount = _logisticsRouteQuery.CalculateEntityCount();

            using var builder = new DeterministicRegistryBuilder<Space4XLogisticsRegistryEntry>(routeCount, Allocator.Temp);

            int activeRoutes = 0;
            int highRiskRoutes = 0;
            float totalThroughput = 0f;
            float totalRisk = 0f;
            int resolvedCount = 0;
            int fallbackCount = 0;
            int unmappedCount = 0;
            var syncHasData = hasSyncState && syncState.HasSpatialData;

            foreach (var (route, transform, entity) in SystemAPI.Query<RefRO<Space4XLogisticsRoute>, RefRO<LocalTransform>>().WithAll<SpatialIndexedTag>().WithEntityAccess())
            {
                var routeData = route.ValueRO;
                var position = transform.ValueRO.Position;
                var flags = Space4XLogisticsRegistryFlags.FromRoute(routeData.Status, routeData.Risk);

                var cellId = -1;
                var usedResidency = false;

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)gridConfig.CellCount)
                    {
                        cellId = residency.CellId;
                        resolvedCount++;
                        usedResidency = true;
                    }
                }

                if (!usedResidency && hasSpatial)
                {
                    SpatialHash.Quantize(position, gridConfig, out var coords);
                    var flattened = SpatialHash.Flatten(in coords, in gridConfig);
                    if ((uint)flattened < (uint)gridConfig.CellCount)
                    {
                        cellId = flattened;
                        fallbackCount++;
                    }
                    else
                    {
                        unmappedCount++;
                    }
                }
                else if (!usedResidency && !hasSpatial)
                {
                    unmappedCount++;
                }

                builder.Add(new Space4XLogisticsRegistryEntry
                {
                    RouteEntity = entity,
                    RouteId = routeData.RouteId,
                    OriginColonyId = routeData.OriginColonyId,
                    DestinationColonyId = routeData.DestinationColonyId,
                    DailyThroughput = math.max(0f, routeData.DailyThroughput),
                    Risk = math.clamp(routeData.Risk, 0f, 1f),
                    Priority = routeData.Priority,
                    Status = routeData.Status,
                    WorldPosition = position,
                    Flags = flags,
                    CellId = cellId,
                    SpatialVersion = hasSpatial ? gridState.Version : (syncHasData ? syncState.SpatialVersion : 0u)
                });

                if ((flags & Space4XLogisticsRegistryFlags.RouteActive) != 0)
                {
                    activeRoutes++;
                }

                if ((flags & Space4XLogisticsRegistryFlags.RouteHighRisk) != 0)
                {
                    highRiskRoutes++;
                }

                totalThroughput += math.max(0f, routeData.DailyThroughput);
                totalRisk += math.clamp(routeData.Risk, 0f, 1f);
            }

            var buffer = state.EntityManager.GetBuffer<Space4XLogisticsRegistryEntry>(_logisticsRegistryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(_logisticsRegistryEntity).ValueRW;
            var requireSpatialSync = metadata.SupportsSpatialQueries && syncHasData;
            var spatialVersion = hasSpatial ? gridState.Version : (syncHasData ? syncState.SpatialVersion : 0u);
            var provideSpatialData = hasSpatial || syncHasData;
            var continuity = provideSpatialData
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);
            builder.ApplyTo(ref buffer, ref metadata, tick, continuity);

            ref var summary = ref SystemAPI.GetComponentRW<Space4XLogisticsRegistry>(_logisticsRegistryEntity).ValueRW;
            summary.RouteCount = buffer.Length;
            summary.ActiveRouteCount = activeRoutes;
            summary.HighRiskRouteCount = highRiskRoutes;
            summary.TotalDailyThroughput = totalThroughput;
            summary.AverageRisk = buffer.Length > 0 ? totalRisk / buffer.Length : 0f;
            summary.LastUpdateTick = tick;
            summary.LastSpatialVersion = spatialVersion;
            summary.SpatialResolvedCount = resolvedCount;
            summary.SpatialFallbackCount = fallbackCount;
            summary.SpatialUnmappedCount = unmappedCount;

            ref var snapshot = ref SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(_snapshotEntity).ValueRW;
            snapshot.LogisticsRouteCount = buffer.Length;
            snapshot.ActiveLogisticsRouteCount = activeRoutes;
            snapshot.HighRiskRouteCount = highRiskRoutes;
            snapshot.LogisticsTotalThroughput = totalThroughput;
            snapshot.LogisticsAverageRisk = buffer.Length > 0 ? totalRisk / buffer.Length : 0f;
            snapshot.LastRegistryTick = math.max(snapshot.LastRegistryTick, tick);
        }

        private void UpdateAnomalyRegistry(ref SystemState state, uint tick, bool hasSpatial, in SpatialGridConfig gridConfig, in SpatialGridState gridState, bool hasSyncState, in RegistrySpatialSyncState syncState)
        {
            var anomalyCount = _anomalyQuery.CalculateEntityCount();

            using var builder = new DeterministicRegistryBuilder<Space4XAnomalyRegistryEntry>(anomalyCount, Allocator.Temp);

            int activeAnomalies = 0;
            var highestSeverity = Space4XAnomalySeverity.None;
            int resolvedCount = 0;
            int fallbackCount = 0;
            int unmappedCount = 0;
            var syncHasData = hasSyncState && syncState.HasSpatialData;

            foreach (var (anomaly, transform, entity) in SystemAPI.Query<RefRO<Space4XAnomaly>, RefRO<LocalTransform>>().WithAll<SpatialIndexedTag>().WithEntityAccess())
            {
                var anomalyData = anomaly.ValueRO;
                var position = transform.ValueRO.Position;
                var flags = Space4XAnomalyRegistryFlags.FromAnomaly(anomalyData.State, anomalyData.Severity);

                var cellId = -1;
                var usedResidency = false;

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)gridConfig.CellCount)
                    {
                        cellId = residency.CellId;
                        resolvedCount++;
                        usedResidency = true;
                    }
                }

                if (!usedResidency && hasSpatial)
                {
                    SpatialHash.Quantize(position, gridConfig, out var coords);
                    var flattened = SpatialHash.Flatten(in coords, in gridConfig);
                    if ((uint)flattened < (uint)gridConfig.CellCount)
                    {
                        cellId = flattened;
                        fallbackCount++;
                    }
                    else
                    {
                        unmappedCount++;
                    }
                }
                else if (!usedResidency && !hasSpatial)
                {
                    unmappedCount++;
                }

                builder.Add(new Space4XAnomalyRegistryEntry
                {
                    AnomalyEntity = entity,
                    AnomalyId = anomalyData.AnomalyId,
                    Classification = anomalyData.Classification,
                    Severity = anomalyData.Severity,
                    State = anomalyData.State,
                    Instability = math.max(0f, anomalyData.Instability),
                    SectorId = anomalyData.SectorId,
                    WorldPosition = position,
                    Flags = flags,
                    CellId = cellId,
                    SpatialVersion = hasSpatial ? gridState.Version : (syncHasData ? syncState.SpatialVersion : 0u)
                });

                if ((flags & Space4XAnomalyRegistryFlags.AnomalyActive) != 0)
                {
                    activeAnomalies++;
                }

                if (anomalyData.Severity > highestSeverity)
                {
                    highestSeverity = anomalyData.Severity;
                }
            }

            var buffer = state.EntityManager.GetBuffer<Space4XAnomalyRegistryEntry>(_anomalyRegistryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(_anomalyRegistryEntity).ValueRW;
            var requireSpatialSync = metadata.SupportsSpatialQueries && syncHasData;
            var spatialVersion = hasSpatial ? gridState.Version : (syncHasData ? syncState.SpatialVersion : 0u);
            var provideSpatialData = hasSpatial || syncHasData;
            var continuity = provideSpatialData
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);
            builder.ApplyTo(ref buffer, ref metadata, tick, continuity);

            ref var summary = ref SystemAPI.GetComponentRW<Space4XAnomalyRegistry>(_anomalyRegistryEntity).ValueRW;
            summary.AnomalyCount = buffer.Length;
            summary.ActiveAnomalyCount = activeAnomalies;
            summary.HighestSeverity = highestSeverity;
            summary.LastUpdateTick = tick;
            summary.LastSpatialVersion = spatialVersion;
            summary.SpatialResolvedCount = resolvedCount;
            summary.SpatialFallbackCount = fallbackCount;
            summary.SpatialUnmappedCount = unmappedCount;

            ref var snapshot = ref SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(_snapshotEntity).ValueRW;
            snapshot.AnomalyCount = buffer.Length;
            snapshot.ActiveAnomalyCount = activeAnomalies;
            snapshot.HighestAnomalySeverity = highestSeverity;
            snapshot.LastRegistryTick = math.max(snapshot.LastRegistryTick, tick);
        }

        private void UpdateMiracleSnapshot(ref SystemState state, uint tick, float fixedDeltaTime)
        {
            if (!SystemAPI.TryGetSingleton(out MiracleRegistry miracleRegistry))
            {
                return;
            }

            ref var snapshot = ref SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(_snapshotEntity).ValueRW;
            snapshot.MiracleCount = miracleRegistry.TotalMiracles;
            snapshot.ActiveMiracleCount = miracleRegistry.ActiveMiracles;
            snapshot.MiracleTotalEnergyCost = miracleRegistry.TotalEnergyCost;
            snapshot.MiracleTotalCooldownSeconds = miracleRegistry.TotalCooldownSeconds;
            snapshot.MiracleAverageChargePercent = 0f;
            snapshot.MiracleAverageCastLatencySeconds = 0f;
            snapshot.MiracleCancellationCount = 0;

            if (SystemAPI.TryGetSingletonEntity<MiracleRegistry>(out var miracleRegistryEntity) &&
                state.EntityManager.HasBuffer<MiracleRegistryEntry>(miracleRegistryEntity))
            {
                var entries = state.EntityManager.GetBuffer<MiracleRegistryEntry>(miracleRegistryEntity);
                float totalCharge = 0f;
                int chargeSamples = 0;
                float totalLatencySeconds = 0f;
                int latencySamples = 0;
                int cancellationCount = 0;
                var latencyStep = math.max(fixedDeltaTime, 1e-4f);

                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    totalCharge += entry.ChargePercent;
                    chargeSamples++;

                    if (entry.LastCastTick > 0)
                    {
                        var deltaTicks = tick >= entry.LastCastTick ? tick - entry.LastCastTick : 0u;
                        totalLatencySeconds += deltaTicks * latencyStep;
                        latencySamples++;
                    }

                    if (entry.Lifecycle == MiracleLifecycleState.CoolingDown &&
                        (entry.Flags & MiracleRegistryFlags.Active) == 0)
                    {
                        cancellationCount++;
                    }
                }

                snapshot.MiracleAverageChargePercent = chargeSamples > 0 ? totalCharge / chargeSamples : 0f;
                snapshot.MiracleAverageCastLatencySeconds = latencySamples > 0 ? totalLatencySeconds / latencySamples : 0f;
                snapshot.MiracleCancellationCount = cancellationCount;
            }

            snapshot.LastRegistryTick = math.max(snapshot.LastRegistryTick, tick);
        }

        private static Entity EnsureRegistryEntity<TRegistry, TEntry>(ref SystemState state, FixedString64Bytes label, ushort archetypeId, RegistryHandleFlags flags = RegistryHandleFlags.None)
            where TRegistry : unmanaged, IComponentData
            where TEntry : unmanaged, IBufferElementData, IComparable<TEntry>
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TRegistry>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var entity = state.EntityManager.CreateEntity(typeof(TRegistry), typeof(RegistryMetadata));
            state.EntityManager.AddBuffer<TEntry>(entity);

            state.EntityManager.SetComponentData(entity, new TRegistry());

            var metadata = new RegistryMetadata();
            metadata.Initialise(RegistryKind.Custom, archetypeId, flags, label);
            state.EntityManager.SetComponentData(entity, metadata);

            return entity;
        }
    }

    /// <summary>
    /// Appends Space4X specific metrics to the shared telemetry buffer after the debug HUD snapshot is populated.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(DebugDisplaySystem))]
    public partial struct Space4XRegistryTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;
        private EntityQuery _snapshotQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XRegistrySnapshot>();
            state.RequireForUpdate<TelemetryStream>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _snapshotQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XRegistrySnapshot>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var snapshot = _snapshotQuery.GetSingleton<Space4XRegistrySnapshot>();

            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            buffer.AddMetric("space4x.registry.colonies", snapshot.ColonyCount);
            buffer.AddMetric("space4x.registry.colonies.supply.demand", snapshot.ColonySupplyDemandTotal);
            buffer.AddMetric("space4x.registry.colonies.supply.shortage", snapshot.ColonySupplyShortageTotal);
            buffer.AddMetric("space4x.registry.colonies.supply.avgRatio", snapshot.ColonyAverageSupplyRatio, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.registry.colonies.supply.bottleneck", snapshot.ColonyBottleneckCount);
            buffer.AddMetric("space4x.registry.colonies.supply.critical", snapshot.ColonyCriticalCount);
            buffer.AddMetric("space4x.registry.fleets", snapshot.FleetCount);
            buffer.AddMetric("space4x.registry.fleets.engaging", snapshot.FleetEngagementCount);
            buffer.AddMetric("space4x.registry.logisticsRoutes", snapshot.LogisticsRouteCount);
            buffer.AddMetric("space4x.registry.logisticsRoutes.active", snapshot.ActiveLogisticsRouteCount);
            buffer.AddMetric("space4x.registry.logisticsRoutes.highRisk", snapshot.HighRiskRouteCount);
            buffer.AddMetric("space4x.registry.logisticsRoutes.throughput", snapshot.LogisticsTotalThroughput);
            buffer.AddMetric("space4x.registry.logisticsRoutes.avgRisk", snapshot.LogisticsAverageRisk, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.registry.anomalies", snapshot.AnomalyCount);
            buffer.AddMetric("space4x.registry.anomalies.active", snapshot.ActiveAnomalyCount);
            buffer.AddMetric("space4x.registry.anomalies.highestSeverity", (float)snapshot.HighestAnomalySeverity);
            buffer.AddMetric("space4x.miracles.total", snapshot.MiracleCount);
            buffer.AddMetric("space4x.miracles.active", snapshot.ActiveMiracleCount);
            buffer.AddMetric("space4x.miracles.energy", snapshot.MiracleTotalEnergyCost, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.miracles.cooldownSeconds", snapshot.MiracleTotalCooldownSeconds, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.miracles.averageCharge", snapshot.MiracleAverageChargePercent, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.miracles.castLatencyMs", snapshot.MiracleAverageCastLatencySeconds * 1000f, TelemetryMetricUnit.DurationMilliseconds);
            buffer.AddMetric("space4x.miracles.cancellations", snapshot.MiracleCancellationCount);
            buffer.AddMetric("space4x.compliance.breaches", snapshot.ComplianceBreachCount);
            buffer.AddMetric("space4x.compliance.mutiny", snapshot.ComplianceMutinyCount);
            buffer.AddMetric("space4x.compliance.desertion", snapshot.ComplianceDesertionCount);
            buffer.AddMetric("space4x.compliance.independence", snapshot.ComplianceIndependenceCount);
            buffer.AddMetric("space4x.compliance.severity.avg", snapshot.ComplianceAverageSeverity, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.compliance.suspicion.mean", snapshot.ComplianceAverageSuspicion, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.compliance.suspicion.spyMean", snapshot.ComplianceAverageSpySuspicion, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.compliance.suspicion.max", snapshot.ComplianceMaxSuspicion, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.compliance.suspicion.alerts", snapshot.ComplianceSuspicionAlertCount);
            buffer.AddMetric("space4x.registry.techdiffusion.active", snapshot.TechDiffusionActiveCount);
            buffer.AddMetric("space4x.registry.techdiffusion.completed", snapshot.TechDiffusionCompletedCount);
            buffer.AddMetric("space4x.registry.techdiffusion.lastUpgradeTick", snapshot.TechDiffusionLastUpgradeTick, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.modules.ratings.offense", snapshot.ModuleOffenseRatingTotal);
            buffer.AddMetric("space4x.modules.ratings.defense", snapshot.ModuleDefenseRatingTotal);
            buffer.AddMetric("space4x.modules.ratings.utility", snapshot.ModuleUtilityRatingTotal);
            buffer.AddMetric("space4x.modules.power.balanceMW", snapshot.ModulePowerBalanceMW, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.modules.degraded", snapshot.ModuleDegradedCount);
            buffer.AddMetric("space4x.modules.repairing", snapshot.ModuleRepairingCount);
            buffer.AddMetric("space4x.modules.refitting", snapshot.ModuleRefittingCount);
            buffer.AddMetric("space4x.modules.refit.count", snapshot.ModuleRefitCount);
            buffer.AddMetric("space4x.modules.refit.field", snapshot.ModuleRefitFieldCount);
            buffer.AddMetric("space4x.modules.refit.facility", snapshot.ModuleRefitFacilityCount);
            buffer.AddMetric("space4x.modules.repair.count", snapshot.ModuleRepairCount);
            buffer.AddMetric("space4x.modules.repair.field", snapshot.ModuleRepairFieldCount);
            buffer.AddMetric("space4x.modules.repair.duration.avg_s", snapshot.ModuleRepairDurationAvgSeconds, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.modules.refit.duration.avg_s", snapshot.ModuleRefitDurationAvgSeconds, TelemetryMetricUnit.Custom);

            if (SystemAPI.TryGetSingletonEntity<VillagerRegistry>(out var villagerRegistryEntity) &&
                state.EntityManager.HasBuffer<VillagerLessonRegistryEntry>(villagerRegistryEntity))
            {
                var lessons = state.EntityManager.GetBuffer<VillagerLessonRegistryEntry>(villagerRegistryEntity);
                EmitLessonMetrics(ref buffer, lessons, new FixedString64Bytes("space4x"));
            }
        }

        private static void EmitLessonMetrics(
            ref DynamicBuffer<TelemetryMetric> buffer,
            DynamicBuffer<VillagerLessonRegistryEntry> lessons,
            in FixedString64Bytes prefix)
        {
            if (!lessons.IsCreated || lessons.Length == 0)
            {
                return;
            }

            var completed = 0;
            var axisMap = new NativeHashMap<FixedString64Bytes, AxisAggregate>(math.max(lessons.Length, 8), Allocator.Temp);
            try
            {
                for (var i = 0; i < lessons.Length; i++)
                {
                    var lesson = lessons[i];
                    if (lesson.Progress >= 0.99f)
                    {
                        completed++;
                    }

                    if (lesson.AxisId.Length == 0)
                    {
                        continue;
                    }

                    axisMap.TryGetValue(lesson.AxisId, out var aggregate);
                    aggregate.Count++;
                    aggregate.Progress += lesson.Progress;
                    axisMap[lesson.AxisId] = aggregate;
                }

                var totalKey = new FixedString64Bytes(prefix);
                totalKey.Append(".villagers.lessons.total");
                buffer.AddMetric(totalKey, lessons.Length);

                var completedKey = new FixedString64Bytes(prefix);
                completedKey.Append(".villagers.lessons.completed");
                buffer.AddMetric(completedKey, completed);

                var kv = axisMap.GetKeyValueArrays(Allocator.Temp);
                try
                {
                    for (var i = 0; i < kv.Length; i++)
                    {
                        var aggregate = kv.Values[i];
                        if (aggregate.Count <= 0)
                        {
                            continue;
                        }

                        var key = new FixedString64Bytes(prefix);
                        key.Append(".villagers.lessons.axis.");
                        key.Append(kv.Keys[i]);
                        var average = aggregate.Progress / aggregate.Count;
                        buffer.AddMetric(key, average, TelemetryMetricUnit.Ratio);
                    }
                }
                finally
                {
                    kv.Dispose();
                }
            }
            finally
            {
                axisMap.Dispose();
            }
        }

        private struct AxisAggregate
        {
            public float Progress;
            public int Count;
        }
    }
}
