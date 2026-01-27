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
    /// Maintains a registry of spawn points (villagers, fauna, ships, etc.) for cross-domain consumers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup), OrderFirst = true)]
    public partial struct SpawnerRegistrySystem : ISystem
    {
        private EntityQuery _spawnerQuery;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;
        private ComponentLookup<SpawnerState> _stateLookup;
        private ComponentLookup<SpawnerConfig> _configLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spawnerQuery = SystemAPI.QueryBuilder()
                .WithAll<SpawnerId, SpawnerConfig, SpawnerState, LocalTransform>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SpawnerRegistry>();

            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(isReadOnly: true);
            _stateLookup = state.GetComponentLookup<SpawnerState>(isReadOnly: true);
            _configLookup = state.GetComponentLookup<SpawnerConfig>(isReadOnly: true);
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

            var registryEntity = SystemAPI.GetSingletonEntity<SpawnerRegistry>();
            var registry = SystemAPI.GetComponentRW<SpawnerRegistry>(registryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;
            var entries = state.EntityManager.GetBuffer<SpawnerRegistryEntry>(registryEntity);

            var expectedCount = math.max(16, _spawnerQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<SpawnerRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasSpatialConfig && hasSpatialState && gridConfig.CellCount > 0 && gridConfig.CellSize > 0f;

            var hasSyncState = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState syncState);
            var requireSpatialSync = metadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial ? gridState.Version : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);
            _stateLookup.Update(ref state);
            _configLookup.Update(ref state);

            var resolvedCount = 0;
            var fallbackCount = 0;
            var unmappedCount = 0;
            var totalSpawners = 0;
            var activeSpawners = 0;

            foreach (var (spawnerId, transform, entity) in SystemAPI.Query<RefRO<SpawnerId>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var config = _configLookup.HasComponent(entity)
                    ? _configLookup[entity]
                    : default;

                var stateData = _stateLookup.HasComponent(entity)
                    ? _stateLookup[entity]
                    : default;

                int cellId = -1;
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

                builder.Add(new SpawnerRegistryEntry
                {
                    SpawnerEntity = entity,
                    SpawnerTypeId = config.SpawnTypeId,
                    OwnerFaction = config.OwnerFaction,
                    ActiveSpawnCount = stateData.ActiveSpawnCount,
                    Capacity = config.Capacity,
                    CooldownSeconds = config.CooldownSeconds,
                    RemainingCooldown = stateData.RemainingCooldown,
                    Flags = stateData.Flags,
                    Position = transform.ValueRO.Position,
                    CellId = cellId,
                    SpatialVersion = spatialVersion
                });

                totalSpawners++;
                if (stateData.ActiveSpawnCount > 0)
                {
                    activeSpawners++;
                }
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref metadata, timeState.Tick, continuity);

            registry.ValueRW = new SpawnerRegistry
            {
                TotalSpawners = totalSpawners,
                ActiveSpawnerCount = activeSpawners,
                LastUpdateTick = timeState.Tick,
                LastSpatialVersion = spatialVersion,
                SpatialResolvedCount = resolvedCount,
                SpatialFallbackCount = fallbackCount,
                SpatialUnmappedCount = unmappedCount
            };
        }
    }
}
