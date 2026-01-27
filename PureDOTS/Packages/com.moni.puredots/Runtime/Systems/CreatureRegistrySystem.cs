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
    /// Maintains registry entries for creatures / environmental threats.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup), OrderFirst = true)]
    public partial struct CreatureRegistrySystem : ISystem
    {
        private EntityQuery _creatureQuery;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;
        private ComponentLookup<CreatureAttributes> _attributesLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _creatureQuery = SystemAPI.QueryBuilder()
                .WithAll<CreatureId, CreatureAttributes, LocalTransform>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CreatureRegistry>();

            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(isReadOnly: true);
            _attributesLookup = state.GetComponentLookup<CreatureAttributes>(isReadOnly: true);
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

            var registryEntity = SystemAPI.GetSingletonEntity<CreatureRegistry>();
            var registry = SystemAPI.GetComponentRW<CreatureRegistry>(registryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;
            var entries = state.EntityManager.GetBuffer<CreatureRegistryEntry>(registryEntity);

            var expectedCount = math.max(16, _creatureQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<CreatureRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasSpatialConfig && hasSpatialState && gridConfig.CellCount > 0 && gridConfig.CellSize > 0f;

            var hasSyncState = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState syncState);
            var requireSpatialSync = metadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial ? gridState.Version : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);
            _attributesLookup.Update(ref state);

            var resolvedCount = 0;
            var fallbackCount = 0;
            var unmappedCount = 0;
            var totalThreat = 0f;

            foreach (var (creatureId, transform, entity) in SystemAPI.Query<RefRO<CreatureId>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var attributes = _attributesLookup.HasComponent(entity)
                    ? _attributesLookup[entity]
                    : new CreatureAttributes { ThreatLevel = 0f, Flags = 0, TypeId = default };

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

                builder.Add(new CreatureRegistryEntry
                {
                    CreatureEntity = entity,
                    CreatureId = creatureId.ValueRO.Value,
                    TypeId = attributes.TypeId,
                    ThreatLevel = attributes.ThreatLevel,
                    Flags = attributes.Flags,
                    Position = transform.ValueRO.Position,
                    CellId = cellId,
                    SpatialVersion = spatialVersion
                });

                totalThreat += attributes.ThreatLevel;
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref metadata, timeState.Tick, continuity);

            registry.ValueRW = new CreatureRegistry
            {
                TotalCreatures = entries.Length,
                TotalThreatScore = totalThreat,
                LastUpdateTick = timeState.Tick,
                LastSpatialVersion = spatialVersion,
                SpatialResolvedCount = resolvedCount,
                SpatialFallbackCount = fallbackCount,
                SpatialUnmappedCount = unmappedCount
            };
        }
    }
}
