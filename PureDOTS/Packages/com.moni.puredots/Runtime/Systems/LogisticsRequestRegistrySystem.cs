using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Builds the logistics request registry each frame for deterministic discovery.
    /// Runs after spatial systems to ensure spatial data is available.
    /// Note: Game-specific transport registry systems (e.g., TransportRegistrySystem in Space4X) should run after this system.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TransportPhaseGroup))]
    public partial struct LogisticsRequestRegistrySystem : ISystem
    {
        private EntityQuery _requestQuery;
        private ComponentLookup<LogisticsRequestProgress> _progressLookup;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _requestQuery = SystemAPI.QueryBuilder()
                .WithAll<LogisticsRequest>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build();

            _progressLookup = state.GetComponentLookup<LogisticsRequestProgress>(true);
            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(true);

            state.RequireForUpdate<LogisticsRequestRegistry>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            _progressLookup.Update(ref state);
            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);

            var registryEntity = SystemAPI.GetSingletonEntity<LogisticsRequestRegistry>();
            var registry = SystemAPI.GetComponentRW<LogisticsRequestRegistry>(registryEntity);
            var entries = state.EntityManager.GetBuffer<LogisticsRequestRegistryEntry>(registryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig spatialConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState spatialState);
            var hasSpatialGrid = hasSpatialConfig
                                 && hasSpatialState
                                 && spatialConfig.CellCount > 0
                                 && spatialConfig.CellSize > 0f;

            var hasSpatialSync = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState spatialSyncState);
            var spatialVersionSource = hasSpatialGrid
                ? spatialState.Version
                : (hasSpatialSync && spatialSyncState.HasSpatialData ? spatialSyncState.SpatialVersion : 0u);
            var requireSpatialSync = metadata.SupportsSpatialQueries && hasSpatialSync && spatialSyncState.HasSpatialData;

            var expectedCount = math.max(8, _requestQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<LogisticsRequestRegistryEntry>(expectedCount, Allocator.Temp);

            var totalRequests = 0;
            var pendingRequests = 0;
            var inProgressRequests = 0;
            var criticalRequests = 0;
            var totalRequestedUnits = 0f;
            var totalAssignedUnits = 0f;
            var totalRemainingUnits = 0f;

            var resolvedCount = 0;
            var fallbackCount = 0;
            var unmappedCount = 0;

            foreach (var (request, entity) in SystemAPI.Query<RefRO<LogisticsRequest>>().WithEntityAccess())
            {
                var progress = _progressLookup.HasComponent(entity)
                    ? _progressLookup[entity]
                    : default;

                var unitsRemaining = math.max(0f, request.ValueRO.RequestedUnits - request.ValueRO.FulfilledUnits);
                var assignedUnits = math.max(0f, progress.AssignedUnits);

                var sourcePosition = request.ValueRO.SourcePosition;
                var destinationPosition = request.ValueRO.DestinationPosition;
                var sourceCellId = -1;
                var destinationCellId = -1;
                var entrySpatialVersion = spatialVersionSource;

                if (hasSpatialGrid)
                {
                    entrySpatialVersion = spatialState.Version;
                    sourceCellId = ResolveCellId(request.ValueRO.SourceEntity, sourcePosition, in spatialConfig, spatialState.Version, ref resolvedCount, ref fallbackCount, ref unmappedCount);
                    destinationCellId = ResolveCellId(request.ValueRO.DestinationEntity, destinationPosition, in spatialConfig, spatialState.Version, ref resolvedCount, ref fallbackCount, ref unmappedCount);
                }

                var entryFlags = request.ValueRO.Flags;
                var priority = request.ValueRO.Priority;
                var isComplete = unitsRemaining <= 0.01f;
                var isPending = !isComplete && assignedUnits <= 0.01f;
                var isInProgress = !isComplete && assignedUnits > 0.01f;

                totalRequests++;
                totalRequestedUnits += request.ValueRO.RequestedUnits;
                totalAssignedUnits += assignedUnits;
                totalRemainingUnits += unitsRemaining;

                if (isPending)
                {
                    pendingRequests++;
                }

                if (isInProgress)
                {
                    inProgressRequests++;
                }

                if (priority >= LogisticsRequestPriority.Critical || (entryFlags & LogisticsRequestFlags.Urgent) != 0)
                {
                    criticalRequests++;
                }

                builder.Add(new LogisticsRequestRegistryEntry
                {
                    RequestEntity = entity,
                    SourceEntity = request.ValueRO.SourceEntity,
                    DestinationEntity = request.ValueRO.DestinationEntity,
                    SourcePosition = sourcePosition,
                    DestinationPosition = destinationPosition,
                    SourceCellId = sourceCellId,
                    DestinationCellId = destinationCellId,
                    SpatialVersion = entrySpatialVersion,
                    ResourceTypeIndex = request.ValueRO.ResourceTypeIndex,
                    RequestedUnits = request.ValueRO.RequestedUnits,
                    AssignedUnits = assignedUnits,
                    RemainingUnits = unitsRemaining,
                    Priority = priority,
                    Flags = entryFlags,
                    CreatedTick = request.ValueRO.CreatedTick,
                    LastUpdateTick = request.ValueRO.LastUpdateTick
                });
            }

            var continuity = hasSpatialGrid
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersionSource, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref metadata, timeState.Tick, continuity);

            registry.ValueRW = new LogisticsRequestRegistry
            {
                TotalRequests = totalRequests,
                PendingRequests = pendingRequests,
                InProgressRequests = inProgressRequests,
                CriticalRequests = criticalRequests,
                TotalRequestedUnits = totalRequestedUnits,
                TotalAssignedUnits = totalAssignedUnits,
                TotalRemainingUnits = totalRemainingUnits,
                LastUpdateTick = timeState.Tick,
                LastSpatialVersion = spatialVersionSource,
                SpatialResolvedCount = resolvedCount,
                SpatialFallbackCount = fallbackCount,
                SpatialUnmappedCount = unmappedCount
            };
        }

        private int ResolveCellId(Entity entity, float3 fallbackPosition, in SpatialGridConfig config, uint gridVersion, ref int resolved, ref int fallback, ref int unmapped)
        {
            if (entity != Entity.Null && _residencyLookup.HasComponent(entity))
            {
                var residency = _residencyLookup[entity];
                if (residency.Version == gridVersion && (uint)residency.CellId < (uint)config.CellCount)
                {
                    resolved++;
                    return residency.CellId;
                }
            }

            return ClassifyPosition(fallbackPosition, in config, ref fallback, ref unmapped);
        }

        private static int ClassifyPosition(float3 position, in SpatialGridConfig config, ref int fallback, ref int unmapped)
        {
            SpatialHash.Quantize(position, config, out var coords);
            var cellId = SpatialHash.Flatten(in coords, in config);
            if ((uint)cellId < (uint)config.CellCount)
            {
                fallback++;
                return cellId;
            }

            unmapped++;
            return -1;
        }
    }
}
