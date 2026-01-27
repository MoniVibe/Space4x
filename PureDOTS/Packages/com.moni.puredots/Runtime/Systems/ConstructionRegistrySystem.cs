using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Maintains a registry of active construction sites for cross-domain consumers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ConstructionSystemGroup), OrderFirst = true)]
    public partial struct ConstructionRegistrySystem : ISystem
    {
        private EntityQuery _siteQuery;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;
        private ComponentLookup<ConstructionSiteFlags> _flagsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _siteQuery = SystemAPI.QueryBuilder()
                .WithAll<ConstructionSiteId, ConstructionSiteProgress, LocalTransform>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ConstructionRegistry>();

            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(isReadOnly: true);
            _flagsLookup = state.GetComponentLookup<ConstructionSiteFlags>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var registryEntity = SystemAPI.GetSingletonEntity<ConstructionRegistry>();
            var registry = SystemAPI.GetComponentRW<ConstructionRegistry>(registryEntity);
            ref var registryMetadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;
            var entries = state.EntityManager.GetBuffer<ConstructionRegistryEntry>(registryEntity);

            var expectedCount = math.max(16, _siteQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<ConstructionRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasSpatialConfig && hasSpatialState && gridConfig.CellCount > 0 && gridConfig.CellSize > 0f;

            var hasSyncState = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState syncState);
            var requireSpatialSync = registryMetadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial ? gridState.Version : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);
            _flagsLookup.Update(ref state);

            var resolvedCount = 0;
            var fallbackCount = 0;
            var unmappedCount = 0;
            var activeCount = 0;
            var completedCount = 0;

            foreach (var (siteId, progress, transform, entity) in SystemAPI.Query<RefRO<ConstructionSiteId>, RefRO<ConstructionSiteProgress>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var flags = _flagsLookup.HasComponent(entity) ? _flagsLookup[entity].Value : (byte)0;
                var isCompleted = (flags & ConstructionSiteFlags.Completed) != 0;

                if (isCompleted)
                {
                    completedCount++;
                }
                else
                {
                    activeCount++;
                }

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
                    SpatialHash.Quantize(transform.ValueRO.Position, gridConfig, out var coords);
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

                var requiredProgress = progress.ValueRO.RequiredProgress;
                var currentProgress = progress.ValueRO.CurrentProgress;
                var normalizedProgress = isCompleted
                    ? 1f
                    : (requiredProgress > 0f ? math.saturate(currentProgress / requiredProgress) : 0f);

                builder.Add(new ConstructionRegistryEntry
                {
                    SiteEntity = entity,
                    SiteId = siteId.ValueRO.Value,
                    Position = transform.ValueRO.Position,
                    RequiredProgress = requiredProgress,
                    CurrentProgress = currentProgress,
                    NormalizedProgress = normalizedProgress,
                    Flags = flags,
                    CellId = cellId,
                    SpatialVersion = spatialVersion
                });
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref registryMetadata, timeState.Tick, continuity);

            registry.ValueRW = new ConstructionRegistry
            {
                ActiveSiteCount = activeCount,
                CompletedSiteCount = completedCount,
                LastUpdateTick = timeState.Tick,
                LastSpatialVersion = spatialVersion,
                SpatialResolvedCount = resolvedCount,
                SpatialFallbackCount = fallbackCount,
                SpatialUnmappedCount = unmappedCount
            };
        }
    }
}
