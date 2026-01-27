using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Publishes processing station metadata (sawmills, refineries, etc.) for AI and telemetry consumers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup), OrderLast = true)]
    public partial struct ProcessingStationRegistrySystem : ISystem
    {
        private EntityQuery _stationQuery;
        private BufferLookup<ResourceProcessorQueue> _queueLookup;
        private ComponentLookup<ResourceProcessorState> _processorStateLookup;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _stationQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceProcessorConfig, LocalTransform>()
                .Build();

            state.RequireForUpdate<ProcessingStationRegistry>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _queueLookup = state.GetBufferLookup<ResourceProcessorQueue>(true);
            _processorStateLookup = state.GetComponentLookup<ResourceProcessorState>(true);
            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var registryEntity = SystemAPI.GetSingletonEntity<ProcessingStationRegistry>();
            var registry = SystemAPI.GetComponentRW<ProcessingStationRegistry>(registryEntity);
            var entries = state.EntityManager.GetBuffer<ProcessingStationRegistryEntry>(registryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;

            _queueLookup.Update(ref state);
            _processorStateLookup.Update(ref state);
            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);

            var expectedCount = math.max(8, _stationQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<ProcessingStationRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasSpatialConfig && hasSpatialState && gridConfig.CellCount > 0 && gridConfig.CellSize > 0f;
            var hasSyncState = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState syncState);
            var requireSpatialSync = metadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial ? gridState.Version : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            var resolvedCount = 0;
            var fallbackCount = 0;
            var unmappedCount = 0;
            var totalStations = 0;
            var activeStations = 0;

            foreach (var (config, transform, entity) in SystemAPI.Query<RefRO<ResourceProcessorConfig>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var queueDepth = 0;
                if (_queueLookup.HasBuffer(entity))
                {
                    var queue = _queueLookup[entity];
                    queueDepth = queue.Length;
                }

                var activeJobs = 0;
                var averageSeconds = 0f;
                if (_processorStateLookup.HasComponent(entity))
                {
                    var processorState = _processorStateLookup[entity];
                    activeJobs = processorState.RemainingSeconds > 0f ? 1 : 0;
                    averageSeconds = processorState.RemainingSeconds;
                }

                totalStations++;
                if (activeJobs > 0 || queueDepth > 0)
                {
                    activeStations++;
                }

                var position = transform.ValueRO.Position;
                var cellId = -1;
                var resolved = false;

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)gridConfig.CellCount)
                    {
                        cellId = residency.CellId;
                        resolved = true;
                        resolvedCount++;
                    }
                }

                if (!resolved && hasSpatial)
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

                builder.Add(new ProcessingStationRegistryEntry
                {
                    StationEntity = entity,
                    StationTypeId = config.ValueRO.FacilityTag,
                    AcceptedResourceTypes = default,
                    QueueDepth = (byte)math.clamp(queueDepth, 0, byte.MaxValue),
                    ActiveJobs = (byte)math.clamp(activeJobs, 0, byte.MaxValue),
                    AverageProcessSeconds = averageSeconds,
                    SkillBias = 0,
                    TierUpgradeHint = ResourceQualityTier.Unknown,
                    LastMutationTick = timeState.Tick,
                    CellId = cellId,
                    SpatialVersion = spatialVersion
                });
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref metadata, timeState.Tick, continuity);

            registry.ValueRW = new ProcessingStationRegistry
            {
                TotalStations = totalStations,
                ActiveStations = activeStations,
                LastUpdateTick = timeState.Tick,
                LastSpatialVersion = spatialVersion,
                SpatialResolvedCount = resolvedCount,
                SpatialFallbackCount = fallbackCount,
                SpatialUnmappedCount = unmappedCount
            };
        }
    }
}
