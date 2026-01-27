using PureDOTS.Runtime.Bands;
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
    /// Publishes deterministic snapshots of all active bands so downstream systems and HUDs can consume them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(RegistrySpatialSyncSystem))]
    public partial struct BandRegistrySystem : ISystem
    {
        private EntityQuery _bandQuery;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;
        private ComponentLookup<BandStats> _statsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _bandQuery = SystemAPI.QueryBuilder()
                .WithAll<BandId, BandStats, LocalTransform>()
                .Build();

            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(isReadOnly: true);
            _statsLookup = state.GetComponentLookup<BandStats>(isReadOnly: true);

            state.RequireForUpdate<BandRegistry>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate(_bandQuery);
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

            var registryEntity = SystemAPI.GetSingletonEntity<BandRegistry>();
            var registry = SystemAPI.GetComponentRW<BandRegistry>(registryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;
            var entries = state.EntityManager.GetBuffer<BandRegistryEntry>(registryEntity);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig spatialConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState spatialState);
            var hasSpatial = hasSpatialConfig && hasSpatialState && spatialConfig.CellCount > 0 && spatialConfig.CellSize > 0f;

            var hasSyncState = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState syncState);
            var requireSpatialSync = metadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial
                ? spatialState.Version
                : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);
            _statsLookup.Update(ref state);

            var expectedCount = math.max(16, _bandQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<BandRegistryEntry>(expectedCount, Allocator.Temp);

            var resolved = 0;
            var fallback = 0;
            var unmapped = 0;

            var memberAccumulator = 0;
            var moraleAccumulator = 0f;
            var cohesionAccumulator = 0f;
            var disciplineAccumulator = 0f;

            foreach (var (bandId, transform, entity) in SystemAPI
                         .Query<RefRO<BandId>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var stats = _statsLookup.HasComponent(entity)
                    ? _statsLookup[entity]
                    : default;

                var entry = new BandRegistryEntry
                {
                    BandEntity = entity,
                    BandId = bandId.ValueRO.Value,
                    Position = transform.ValueRO.Position,
                    MemberCount = stats.MemberCount,
                    Morale = stats.Morale,
                    Cohesion = stats.Cohesion,
                    AverageDiscipline = stats.AverageDiscipline,
                    Flags = stats.Flags,
                    CellId = -1,
                    SpatialVersion = 0
                };

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)spatialConfig.CellCount)
                    {
                        entry.CellId = residency.CellId;
                        entry.SpatialVersion = residency.Version;
                        resolved++;
                    }
                }

                if (entry.CellId < 0 && hasSpatial)
                {
                    SpatialHash.Quantize(transform.ValueRO.Position, spatialConfig, out var coords);
                    var flattened = SpatialHash.Flatten(in coords, in spatialConfig);
                    if ((uint)flattened < (uint)spatialConfig.CellCount)
                    {
                        entry.CellId = flattened;
                        entry.SpatialVersion = spatialVersion;
                        fallback++;
                    }
                    else
                    {
                        entry.CellId = -1;
                        entry.SpatialVersion = 0;
                        unmapped++;
                    }
                }

                builder.Add(entry);

                memberAccumulator += stats.MemberCount;
                moraleAccumulator += stats.Morale;
                cohesionAccumulator += stats.Cohesion;
                disciplineAccumulator += stats.AverageDiscipline;
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolved, fallback, unmapped, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref metadata, timeState.Tick, continuity);

            var totalBands = entries.Length;
            var normaliser = math.max(1, totalBands);

            registry.ValueRW = new BandRegistry
            {
                TotalBands = totalBands,
                TotalMembers = memberAccumulator,
                AverageMorale = totalBands > 0 ? moraleAccumulator / normaliser : 0f,
                AverageCohesion = totalBands > 0 ? cohesionAccumulator / normaliser : 0f,
                AverageDiscipline = totalBands > 0 ? disciplineAccumulator / normaliser : 0f,
                LastUpdateTick = timeState.Tick,
                LastSpatialVersion = spatialVersion,
                SpatialResolvedCount = resolved,
                SpatialFallbackCount = fallback,
                SpatialUnmappedCount = unmapped
            };
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
