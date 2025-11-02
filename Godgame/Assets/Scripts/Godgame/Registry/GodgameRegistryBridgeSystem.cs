using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Godgame.Registry
{
    /// <summary>
    /// Bridges Godgame authored entities into the shared PureDOTS villager and storehouse registries.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RegistrySpatialSyncSystem))]
    public partial struct GodgameRegistryBridgeSystem : ISystem
    {
        private EntityQuery _villagerQuery;
        private EntityQuery _storehouseQuery;
        private Entity _snapshotEntity;
        private ComponentLookup<SpatialGridResidency> _spatialResidencyLookup;
        private ComponentLookup<MiracleRuntimeState> _miracleRuntimeLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RegistryDirectory>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<VillagerRegistry>();
            state.RequireForUpdate<StorehouseRegistry>();

            _villagerQuery = SystemAPI.QueryBuilder()
                .WithAll<GodgameVillager, LocalTransform>()
                .Build();

            _storehouseQuery = SystemAPI.QueryBuilder()
                .WithAll<GodgameStorehouse, LocalTransform>()
                .Build();

            _spatialResidencyLookup = state.GetComponentLookup<SpatialGridResidency>(isReadOnly: true);
            _miracleRuntimeLookup = state.GetComponentLookup<MiracleRuntimeState>(isReadOnly: true);

            using var snapshotQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GodgameRegistrySnapshot>());
            if (snapshotQuery.IsEmptyIgnoreFilter)
            {
                _snapshotEntity = state.EntityManager.CreateEntity(typeof(GodgameRegistrySnapshot));
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
            _spatialResidencyLookup.Update(ref state);
            _miracleRuntimeLookup.Update(ref state);
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var summary = new BridgeSummary(tick);

            UpdateVillagerRegistry(ref state, ref summary);
            UpdateStorehouseRegistry(ref state, ref summary);

            ref var snapshot = ref SystemAPI.GetComponentRW<GodgameRegistrySnapshot>(_snapshotEntity).ValueRW;
            snapshot.VillagerCount = summary.VillagerCount;
            snapshot.AvailableVillagers = summary.AvailableVillagers;
            snapshot.IdleVillagers = summary.IdleVillagers;
            snapshot.ReservedVillagers = summary.ReservedVillagers;
            snapshot.CombatReadyVillagers = summary.CombatReadyVillagers;
            snapshot.AverageVillagerHealth = summary.AverageHealth;
            snapshot.AverageVillagerMorale = summary.AverageMorale;
            snapshot.AverageVillagerEnergy = summary.AverageEnergy;
            snapshot.StorehouseCount = summary.StorehouseCount;
            snapshot.TotalStorehouseCapacity = summary.TotalStorehouseCapacity;
            snapshot.TotalStorehouseStored = summary.TotalStorehouseStored;
            snapshot.TotalStorehouseReserved = summary.TotalStorehouseReserved;
            snapshot.LastRegistryTick = summary.Tick;
        }

        private void UpdateVillagerRegistry(ref SystemState state, ref BridgeSummary summary)
        {
            var registryEntity = SystemAPI.GetSingletonEntity<VillagerRegistry>();
            var buffer = state.EntityManager.GetBuffer<VillagerRegistryEntry>(registryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;
            ref var registry = ref SystemAPI.GetComponentRW<VillagerRegistry>(registryEntity).ValueRW;

            if (metadata.ArchetypeId == 0)
            {
                metadata.ArchetypeId = GodgameRegistryIds.VillagerArchetype;
            }

            var expectedCount = math.max(4, _villagerQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<VillagerRegistryEntry>(expectedCount, Allocator.Temp);

            float healthSum = 0f;
            float moraleSum = 0f;
            float energySum = 0f;

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig spatialConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState spatialState);
            var hasSpatialGrid = hasSpatialConfig && hasSpatialState && spatialConfig.CellCount > 0 && spatialConfig.CellSize > 0f;
            var hasSpatialSyncState = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState spatialSyncState);
            var spatialVersionSource = hasSpatialGrid ? spatialState.Version : (hasSpatialSyncState && spatialSyncState.HasSpatialData ? spatialSyncState.SpatialVersion : 0u);
            var requireSpatialSync = metadata.SupportsSpatialQueries && hasSpatialSyncState && spatialSyncState.HasSpatialData;

            var resolvedCount = 0;
            var fallbackCount = 0;
            var unmappedCount = 0;

            foreach (var (villager, transform, entity) in SystemAPI.Query<RefRO<GodgameVillager>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var data = villager.ValueRO;
                var position = transform.ValueRO.Position;

                var availabilityFlags = BuildAvailabilityFlags(data.IsAvailable, data.IsReserved);

                var cellId = -1;
                var entrySpatialVersion = 0u;

                if (hasSpatialGrid)
                {
                    var resolved = false;
                    var fallback = false;

                    if (_spatialResidencyLookup.HasComponent(entity))
                    {
                        var residency = _spatialResidencyLookup[entity];
                        if ((uint)residency.CellId < (uint)spatialConfig.CellCount && residency.Version == spatialState.Version)
                        {
                            cellId = residency.CellId;
                            entrySpatialVersion = residency.Version;
                            resolved = true;
                        }
                    }

                    if (!resolved)
                    {
                        SpatialHash.Quantize(position, spatialConfig, out var coords);
                        var computedCell = SpatialHash.Flatten(in coords, in spatialConfig);
                        if ((uint)computedCell < (uint)spatialConfig.CellCount)
                        {
                            cellId = computedCell;
                            entrySpatialVersion = spatialState.Version;
                            fallback = true;
                        }
                        else
                        {
                            cellId = -1;
                            entrySpatialVersion = 0;
                            unmappedCount++;
                        }
                    }

                    if (resolved)
                    {
                        resolvedCount++;
                    }
                    else if (fallback)
                    {
                        fallbackCount++;
                    }
                }

                builder.Add(new VillagerRegistryEntry
                {
                    VillagerEntity = entity,
                    VillagerId = data.VillagerId,
                    FactionId = data.FactionId,
                    Position = position,
                    CellId = cellId,
                    SpatialVersion = entrySpatialVersion,
                    JobType = data.JobType,
                    JobPhase = data.JobPhase,
                    ActiveTicketId = data.ActiveTicketId,
                    CurrentResourceTypeIndex = data.CurrentResourceTypeIndex,
                    AvailabilityFlags = availabilityFlags,
                    Discipline = (byte)data.Discipline,
                    HealthPercent = (byte)math.clamp(math.round(data.HealthPercent), 0f, 100f),
                    MoralePercent = (byte)math.clamp(math.round(data.MoralePercent), 0f, 100f),
                    EnergyPercent = (byte)math.clamp(math.round(data.EnergyPercent), 0f, 100f),
                    AIState = (byte)data.AIState,
                    AIGoal = (byte)data.AIGoal,
                    CurrentTarget = data.CurrentTarget,
                    Productivity = data.Productivity
                });

                summary.VillagerCount++;

                if (data.IsAvailable != 0)
                {
                    summary.AvailableVillagers++;
                }

                if (data.IsAvailable != 0 && data.IsReserved == 0 && data.JobPhase == VillagerJob.JobPhase.Idle)
                {
                    summary.IdleVillagers++;
                }

                if (data.IsReserved != 0)
                {
                    summary.ReservedVillagers++;
                }

                if (data.IsCombatReady != 0)
                {
                    summary.CombatReadyVillagers++;
                }

                healthSum += math.clamp(data.HealthPercent, 0f, 100f);
                moraleSum += math.clamp(data.MoralePercent, 0f, 100f);
                energySum += math.clamp(data.EnergyPercent, 0f, 100f);
            }

            var continuity = hasSpatialGrid
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersionSource, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref buffer, ref metadata, summary.Tick, continuity);

            var averageHealth = summary.VillagerCount > 0 ? healthSum / summary.VillagerCount : 0f;
            var averageMorale = summary.VillagerCount > 0 ? moraleSum / summary.VillagerCount : 0f;
            var averageEnergy = summary.VillagerCount > 0 ? energySum / summary.VillagerCount : 0f;

            registry.TotalVillagers = summary.VillagerCount;
            registry.AvailableVillagers = summary.AvailableVillagers;
            registry.IdleVillagers = summary.IdleVillagers;
            registry.ReservedVillagers = summary.ReservedVillagers;
            registry.CombatReadyVillagers = summary.CombatReadyVillagers;
            registry.AverageHealthPercent = averageHealth;
            registry.AverageMoralePercent = averageMorale;
            registry.AverageEnergyPercent = averageEnergy;
            registry.LastUpdateTick = summary.Tick;
            registry.LastSpatialVersion = spatialVersionSource;
            registry.SpatialResolvedCount = resolvedCount;
            registry.SpatialFallbackCount = fallbackCount;
            registry.SpatialUnmappedCount = unmappedCount;

            summary.AverageHealth = averageHealth;
            summary.AverageMorale = averageMorale;
            summary.AverageEnergy = averageEnergy;
        }

        private void UpdateStorehouseRegistry(ref SystemState state, ref BridgeSummary summary)
        {
            var registryEntity = SystemAPI.GetSingletonEntity<StorehouseRegistry>();
            var buffer = state.EntityManager.GetBuffer<StorehouseRegistryEntry>(registryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;
            ref var registry = ref SystemAPI.GetComponentRW<StorehouseRegistry>(registryEntity).ValueRW;

            if (metadata.ArchetypeId == 0)
            {
                metadata.ArchetypeId = GodgameRegistryIds.StorehouseArchetype;
            }

            var expectedCount = math.max(2, _storehouseQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<StorehouseRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig spatialConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState spatialState);
            var hasSpatialGrid = hasSpatialConfig && hasSpatialState && spatialConfig.CellCount > 0 && spatialConfig.CellSize > 0f;
            var hasSpatialSyncState = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState spatialSyncState);
            var spatialVersionSource = hasSpatialGrid ? spatialState.Version : (hasSpatialSyncState && spatialSyncState.HasSpatialData ? spatialSyncState.SpatialVersion : 0u);
            var requireSpatialSync = metadata.SupportsSpatialQueries && hasSpatialSyncState && spatialSyncState.HasSpatialData;

            var resolvedCount = 0;
            var fallbackCount = 0;
            var unmappedCount = 0;

            foreach (var (storehouse, transform, entity) in SystemAPI.Query<RefRO<GodgameStorehouse>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var data = storehouse.ValueRO;
                var position = transform.ValueRO.Position;

                var cellId = -1;
                var entrySpatialVersion = 0u;

                if (hasSpatialGrid)
                {
                    var resolved = false;
                    var fallback = false;

                    if (_spatialResidencyLookup.HasComponent(entity))
                    {
                        var residency = _spatialResidencyLookup[entity];
                        if ((uint)residency.CellId < (uint)spatialConfig.CellCount && residency.Version == spatialState.Version)
                        {
                            cellId = residency.CellId;
                            entrySpatialVersion = residency.Version;
                            resolved = true;
                        }
                    }

                    if (!resolved)
                    {
                        SpatialHash.Quantize(position, spatialConfig, out var coords);
                        var computedCell = SpatialHash.Flatten(in coords, in spatialConfig);
                        if ((uint)computedCell < (uint)spatialConfig.CellCount)
                        {
                            cellId = computedCell;
                            entrySpatialVersion = spatialState.Version;
                            fallback = true;
                        }
                        else
                        {
                            cellId = -1;
                            entrySpatialVersion = 0;
                            unmappedCount++;
                        }
                    }

                    if (resolved)
                    {
                        resolvedCount++;
                    }
                    else if (fallback)
                    {
                        fallbackCount++;
                    }
                }

                var summaries = new FixedList32Bytes<StorehouseRegistryCapacitySummary>();
                if (data.ResourceSummaries.Length > 0)
                {
                    for (var i = 0; i < data.ResourceSummaries.Length; i++)
                    {
                        var resourceSummary = data.ResourceSummaries[i];
                        summaries.Add(new StorehouseRegistryCapacitySummary
                        {
                            ResourceTypeIndex = resourceSummary.ResourceTypeIndex,
                            Capacity = resourceSummary.Capacity,
                            Stored = resourceSummary.Stored,
                            Reserved = resourceSummary.Reserved
                        });
                    }
                }
                else if (data.TotalCapacity > 0f || data.TotalStored > 0f || data.TotalReserved > 0f)
                {
                    summaries.Add(new StorehouseRegistryCapacitySummary
                    {
                        ResourceTypeIndex = data.PrimaryResourceTypeIndex,
                        Capacity = data.TotalCapacity,
                        Stored = data.TotalStored,
                        Reserved = data.TotalReserved
                    });
                }

                builder.Add(new StorehouseRegistryEntry
                {
                    StorehouseEntity = entity,
                    Position = position,
                    TotalCapacity = data.TotalCapacity,
                    TotalStored = data.TotalStored,
                    TypeSummaries = summaries,
                    LastMutationTick = data.LastMutationTick != 0 ? data.LastMutationTick : summary.Tick,
                    CellId = cellId,
                    SpatialVersion = entrySpatialVersion
                });

                summary.StorehouseCount++;
                summary.TotalStorehouseCapacity += math.max(0f, data.TotalCapacity);
                summary.TotalStorehouseStored += math.max(0f, data.TotalStored);
                summary.TotalStorehouseReserved += math.max(0f, data.TotalReserved);
            }

            var continuity = hasSpatialGrid
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersionSource, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref buffer, ref metadata, summary.Tick, continuity);

            registry.TotalStorehouses = summary.StorehouseCount;
            registry.TotalCapacity = summary.TotalStorehouseCapacity;
            registry.TotalStored = summary.TotalStorehouseStored;
            registry.LastUpdateTick = summary.Tick;
            registry.LastSpatialVersion = spatialVersionSource;
            registry.SpatialResolvedCount = resolvedCount;
            registry.SpatialFallbackCount = fallbackCount;
            registry.SpatialUnmappedCount = unmappedCount;
        }

        private static byte BuildAvailabilityFlags(byte available, byte reserved)
        {
            byte flags = 0;
            if (available != 0)
            {
                flags |= VillagerAvailabilityFlags.Available;
            }

            if (reserved != 0)
            {
                flags |= VillagerAvailabilityFlags.Reserved;
            }

            return flags;
        }

        private struct BridgeSummary
        {
            public uint Tick;
            public int VillagerCount;
            public int AvailableVillagers;
            public int IdleVillagers;
            public int ReservedVillagers;
            public int CombatReadyVillagers;
            public int StorehouseCount;
            public float TotalStorehouseCapacity;
            public float TotalStorehouseStored;
            public float TotalStorehouseReserved;
            public float AverageHealth;
            public float AverageMorale;
            public float AverageEnergy;

            public BridgeSummary(uint tick)
            {
                Tick = tick;
                VillagerCount = 0;
                AvailableVillagers = 0;
                IdleVillagers = 0;
                ReservedVillagers = 0;
                CombatReadyVillagers = 0;
                StorehouseCount = 0;
                TotalStorehouseCapacity = 0f;
                TotalStorehouseStored = 0f;
                TotalStorehouseReserved = 0f;
                AverageHealth = 0f;
                AverageMorale = 0f;
                AverageEnergy = 0f;
            }
        }
    }

    /// <summary>
    /// Publishes Godgame registry metrics into the shared telemetry stream after debug data is assembled.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(DebugDisplaySystem))]
    public partial struct GodgameRegistryTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricVillagers = new FixedString64Bytes("godgame.registry.villagers");
        private static readonly FixedString64Bytes MetricVillagersAvailable = new FixedString64Bytes("godgame.registry.villagers.available");
        private static readonly FixedString64Bytes MetricVillagersIdle = new FixedString64Bytes("godgame.registry.villagers.idle");
        private static readonly FixedString64Bytes MetricVillagersReserved = new FixedString64Bytes("godgame.registry.villagers.reserved");
        private static readonly FixedString64Bytes MetricVillagersCombatReady = new FixedString64Bytes("godgame.registry.villagers.combatready");
        private static readonly FixedString64Bytes MetricVillagersHealth = new FixedString64Bytes("godgame.registry.villagers.health.avg");
        private static readonly FixedString64Bytes MetricVillagersMorale = new FixedString64Bytes("godgame.registry.villagers.morale.avg");
        private static readonly FixedString64Bytes MetricVillagersEnergy = new FixedString64Bytes("godgame.registry.villagers.energy.avg");
        private static readonly FixedString64Bytes MetricStorehouses = new FixedString64Bytes("godgame.registry.storehouses");
        private static readonly FixedString64Bytes MetricStorehousesCapacity = new FixedString64Bytes("godgame.registry.storehouses.capacity");
        private static readonly FixedString64Bytes MetricStorehousesStored = new FixedString64Bytes("godgame.registry.storehouses.stored");
        private static readonly FixedString64Bytes MetricStorehousesReserved = new FixedString64Bytes("godgame.registry.storehouses.reserved");
        private static readonly FixedString64Bytes MetricTick = new FixedString64Bytes("godgame.registry.tick");

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GodgameRegistrySnapshot>();
            state.RequireForUpdate<TelemetryStream>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var snapshot = SystemAPI.GetSingleton<GodgameRegistrySnapshot>();
            var buffer = SystemAPI.GetSingletonBuffer<TelemetryMetric>();

            buffer.Add(new TelemetryMetric { Key = MetricVillagers, Value = snapshot.VillagerCount, Unit = TelemetryMetricUnit.Count });
            buffer.Add(new TelemetryMetric { Key = MetricVillagersAvailable, Value = snapshot.AvailableVillagers, Unit = TelemetryMetricUnit.Count });
            buffer.Add(new TelemetryMetric { Key = MetricVillagersIdle, Value = snapshot.IdleVillagers, Unit = TelemetryMetricUnit.Count });
            buffer.Add(new TelemetryMetric { Key = MetricVillagersReserved, Value = snapshot.ReservedVillagers, Unit = TelemetryMetricUnit.Count });
            buffer.Add(new TelemetryMetric { Key = MetricVillagersCombatReady, Value = snapshot.CombatReadyVillagers, Unit = TelemetryMetricUnit.Count });
            buffer.Add(new TelemetryMetric { Key = MetricVillagersHealth, Value = snapshot.AverageVillagerHealth, Unit = TelemetryMetricUnit.Ratio });
            buffer.Add(new TelemetryMetric { Key = MetricVillagersMorale, Value = snapshot.AverageVillagerMorale, Unit = TelemetryMetricUnit.Ratio });
            buffer.Add(new TelemetryMetric { Key = MetricVillagersEnergy, Value = snapshot.AverageVillagerEnergy, Unit = TelemetryMetricUnit.Ratio });
            buffer.Add(new TelemetryMetric { Key = MetricStorehouses, Value = snapshot.StorehouseCount, Unit = TelemetryMetricUnit.Count });
            buffer.Add(new TelemetryMetric { Key = MetricStorehousesCapacity, Value = snapshot.TotalStorehouseCapacity, Unit = TelemetryMetricUnit.Count });
            buffer.Add(new TelemetryMetric { Key = MetricStorehousesStored, Value = snapshot.TotalStorehouseStored, Unit = TelemetryMetricUnit.Count });
            buffer.Add(new TelemetryMetric { Key = MetricStorehousesReserved, Value = snapshot.TotalStorehouseReserved, Unit = TelemetryMetricUnit.Count });
            buffer.Add(new TelemetryMetric { Key = MetricTick, Value = snapshot.LastRegistryTick, Unit = TelemetryMetricUnit.Count });
        }
    }
}
