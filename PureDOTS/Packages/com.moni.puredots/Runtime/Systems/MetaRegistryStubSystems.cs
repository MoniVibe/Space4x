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
    /// Maintains a registry of all factions/empires with their territories and diplomatic state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    public partial struct FactionRegistrySystem : ISystem
    {
        private EntityQuery _factionQuery;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _factionQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionId, FactionState>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<FactionRegistry>();

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

            var registryEntity = SystemAPI.GetSingletonEntity<FactionRegistry>();
            var registry = SystemAPI.GetComponentRW<FactionRegistry>(registryEntity);
            var entries = state.EntityManager.GetBuffer<FactionRegistryEntry>(registryEntity);
            ref var registryMetadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;

            var totalTerritoryCells = 0;
            var totalResources = 0f;

            var expectedCount = math.max(16, _factionQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<FactionRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasSpatialConfig && hasSpatialState && gridConfig.CellCount > 0 && gridConfig.CellSize > 0f;

            RegistrySpatialSyncState syncState = default;
            var hasSyncState = SystemAPI.TryGetSingleton(out syncState);
            var requireSpatialSync = registryMetadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial
                ? gridState.Version
                : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);

            foreach (var (factionId, factionState, transform, entity) in SystemAPI.Query<RefRO<FactionId>, RefRO<FactionState>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                var cellId = -1;

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)gridConfig.CellCount)
                    {
                        cellId = residency.CellId;
                    }
                }
                else if (hasSpatial)
                {
                    SpatialHash.Quantize(position, gridConfig, out var coords);
                    var flattened = SpatialHash.Flatten(in coords, in gridConfig);
                    if ((uint)flattened < (uint)gridConfig.CellCount)
                    {
                        cellId = flattened;
                    }
                }

                builder.Add(new FactionRegistryEntry
                {
                    FactionEntity = entity,
                    TerritoryCenter = factionState.ValueRO.TerritoryCenter,
                    CellId = cellId,
                    SpatialVersion = spatialVersion,
                    LastMutationTick = timeState.Tick,
                    FactionId = factionId.ValueRO.Value,
                    FactionName = factionId.ValueRO.Name,
                    FactionType = factionId.ValueRO.Type,
                    ResourceStockpile = factionState.ValueRO.ResourceStockpile,
                    PopulationCount = factionState.ValueRO.PopulationCount,
                    TerritoryCellCount = factionState.ValueRO.TerritoryCellCount,
                    DiplomaticStatus = factionState.ValueRO.DiplomaticStatus,
                    Description = default // Could be populated from a catalog if needed
                });

                totalTerritoryCells += factionState.ValueRO.TerritoryCellCount;
                totalResources += factionState.ValueRO.ResourceStockpile;
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, 0, 0, 0, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref registryMetadata, timeState.Tick, continuity);

            registry.ValueRW = new FactionRegistry
            {
                FactionCount = entries.Length,
                TotalTerritoryCells = totalTerritoryCells,
                TotalResources = totalResources,
                LastUpdateTick = timeState.Tick
            };
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }

    /// <summary>
    /// Maintains a registry of all climate hazards affecting regions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    public partial struct ClimateHazardRegistrySystem : ISystem
    {
        private EntityQuery _hazardQuery;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _hazardQuery = SystemAPI.QueryBuilder()
                .WithAll<ClimateHazardState>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ClimateHazardRegistry>();

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

            var registryEntity = SystemAPI.GetSingletonEntity<ClimateHazardRegistry>();
            var registry = SystemAPI.GetComponentRW<ClimateHazardRegistry>(registryEntity);
            var entries = state.EntityManager.GetBuffer<ClimateHazardRegistryEntry>(registryEntity);
            ref var registryMetadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;

            var globalIntensity = 0f;
            var activeCount = 0;

            var expectedCount = math.max(16, _hazardQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<ClimateHazardRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasSpatialConfig && hasSpatialState && gridConfig.CellCount > 0 && gridConfig.CellSize > 0f;

            RegistrySpatialSyncState syncState = default;
            var hasSyncState = SystemAPI.TryGetSingleton(out syncState);
            var requireSpatialSync = registryMetadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial
                ? gridState.Version
                : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);

            foreach (var (hazardState, transform, entity) in SystemAPI.Query<RefRO<ClimateHazardState>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Check if hazard has expired
                if (hazardState.ValueRO.StartTick + hazardState.ValueRO.DurationTicks < timeState.Tick)
                {
                    continue; // Skip expired hazards
                }

                var position = transform.ValueRO.Position;
                var cellId = -1;

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)gridConfig.CellCount)
                    {
                        cellId = residency.CellId;
                    }
                }
                else if (hasSpatial)
                {
                    SpatialHash.Quantize(position, gridConfig, out var coords);
                    var flattened = SpatialHash.Flatten(in coords, in gridConfig);
                    if ((uint)flattened < (uint)gridConfig.CellCount)
                    {
                        cellId = flattened;
                    }
                }

                var expirationTick = hazardState.ValueRO.StartTick + hazardState.ValueRO.DurationTicks;

                builder.Add(new ClimateHazardRegistryEntry
                {
                    HazardEntity = entity,
                    Position = position,
                    CellId = cellId,
                    SpatialVersion = spatialVersion,
                    LastMutationTick = timeState.Tick,
                    CurrentIntensity = hazardState.ValueRO.CurrentIntensity,
                    ExpirationTick = expirationTick,
                    HazardType = hazardState.ValueRO.HazardType,
                    Radius = hazardState.ValueRO.Radius,
                    MaxIntensity = hazardState.ValueRO.MaxIntensity,
                    StartTick = hazardState.ValueRO.StartTick,
                    DurationTicks = hazardState.ValueRO.DurationTicks,
                    HazardName = hazardState.ValueRO.HazardName,
                    AffectedEnvironmentChannels = hazardState.ValueRO.AffectedEnvironmentChannels
                });

                globalIntensity += hazardState.ValueRO.CurrentIntensity;
                activeCount++;
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, 0, 0, 0, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref registryMetadata, timeState.Tick, continuity);

            registry.ValueRW = new ClimateHazardRegistry
            {
                ActiveHazardCount = activeCount,
                GlobalHazardIntensity = math.saturate(globalIntensity), // Clamp to 0-1
                LastUpdateTick = timeState.Tick
            };
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }

    /// <summary>
    /// Maintains a registry of all area-based effects (buffs, debuffs, slow fields, etc.).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    public partial struct AreaEffectRegistrySystem : ISystem
    {
        private EntityQuery _effectQuery;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _effectQuery = SystemAPI.QueryBuilder()
                .WithAll<AreaEffectState>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<AreaEffectRegistry>();

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

            var registryEntity = SystemAPI.GetSingletonEntity<AreaEffectRegistry>();
            var registry = SystemAPI.GetComponentRW<AreaEffectRegistry>(registryEntity);
            var entries = state.EntityManager.GetBuffer<AreaEffectRegistryEntry>(registryEntity);
            ref var registryMetadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;

            var expectedCount = math.max(16, _effectQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<AreaEffectRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasSpatialConfig && hasSpatialState && gridConfig.CellCount > 0 && gridConfig.CellSize > 0f;

            RegistrySpatialSyncState syncState = default;
            var hasSyncState = SystemAPI.TryGetSingleton(out syncState);
            var requireSpatialSync = registryMetadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial
                ? gridState.Version
                : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);

            foreach (var (effectState, transform, entity) in SystemAPI.Query<RefRO<AreaEffectState>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Check if effect has expired
                if (effectState.ValueRO.ExpirationTick < timeState.Tick)
                {
                    continue; // Skip expired effects
                }

                var position = transform.ValueRO.Position;
                var cellId = -1;

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)gridConfig.CellCount)
                    {
                        cellId = residency.CellId;
                    }
                }
                else if (hasSpatial)
                {
                    SpatialHash.Quantize(position, gridConfig, out var coords);
                    var flattened = SpatialHash.Flatten(in coords, in gridConfig);
                    if ((uint)flattened < (uint)gridConfig.CellCount)
                    {
                        cellId = flattened;
                    }
                }

                builder.Add(new AreaEffectRegistryEntry
                {
                    EffectEntity = entity,
                    Position = position,
                    CellId = cellId,
                    SpatialVersion = spatialVersion,
                    LastMutationTick = timeState.Tick,
                    CurrentStrength = effectState.ValueRO.CurrentStrength,
                    ExpirationTick = effectState.ValueRO.ExpirationTick,
                    EffectType = effectState.ValueRO.EffectType,
                    Radius = effectState.ValueRO.Radius,
                    MaxStrength = effectState.ValueRO.MaxStrength,
                    OwnerEntity = effectState.ValueRO.OwnerEntity,
                    EffectId = effectState.ValueRO.EffectId,
                    AffectedArchetypes = effectState.ValueRO.AffectedArchetypes,
                    EffectName = effectState.ValueRO.EffectName
                });
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, 0, 0, 0, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref registryMetadata, timeState.Tick, continuity);

            registry.ValueRW = new AreaEffectRegistry
            {
                ActiveEffectCount = entries.Length,
                LastUpdateTick = timeState.Tick
            };
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }

    /// <summary>
    /// Maintains a registry of all cultures/alignments with their loyalty and affinity state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    public partial struct CultureAlignmentRegistrySystem : ISystem
    {
        private EntityQuery _cultureQuery;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cultureQuery = SystemAPI.QueryBuilder()
                .WithAll<CultureState>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CultureAlignmentRegistry>();

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

            var registryEntity = SystemAPI.GetSingletonEntity<CultureAlignmentRegistry>();
            var registry = SystemAPI.GetComponentRW<CultureAlignmentRegistry>(registryEntity);
            var entries = state.EntityManager.GetBuffer<CultureAlignmentRegistryEntry>(registryEntity);
            ref var registryMetadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;

            var globalAlignmentScore = 0f;

            var expectedCount = math.max(16, _cultureQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<CultureAlignmentRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasSpatialConfig && hasSpatialState && gridConfig.CellCount > 0 && gridConfig.CellSize > 0f;

            RegistrySpatialSyncState syncState = default;
            var hasSyncState = SystemAPI.TryGetSingleton(out syncState);
            var requireSpatialSync = registryMetadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial
                ? gridState.Version
                : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);

            foreach (var (cultureState, transform, entity) in SystemAPI.Query<RefRO<CultureState>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                var cellId = -1;

                if (hasSpatial && _residencyLookup.HasComponent(entity))
                {
                    var residency = _residencyLookup[entity];
                    if ((uint)residency.CellId < (uint)gridConfig.CellCount)
                    {
                        cellId = residency.CellId;
                    }
                }
                else if (hasSpatial)
                {
                    SpatialHash.Quantize(position, gridConfig, out var coords);
                    var flattened = SpatialHash.Flatten(in coords, in gridConfig);
                    if ((uint)flattened < (uint)gridConfig.CellCount)
                    {
                        cellId = flattened;
                    }
                }

                builder.Add(new CultureAlignmentRegistryEntry
                {
                    CultureEntity = entity,
                    RegionCenter = position,
                    CellId = cellId,
                    SpatialVersion = spatialVersion,
                    LastMutationTick = timeState.Tick,
                    CurrentAlignment = cultureState.ValueRO.CurrentAlignment,
                    AlignmentVelocity = cultureState.ValueRO.AlignmentVelocity,
                    CultureId = cultureState.ValueRO.CultureId,
                    CultureName = cultureState.ValueRO.CultureName,
                    CultureType = cultureState.ValueRO.CultureType,
                    MemberCount = cultureState.ValueRO.MemberCount,
                    BaseAlignment = cultureState.ValueRO.BaseAlignment,
                    AlignmentFlags = cultureState.ValueRO.AlignmentFlags,
                    Description = cultureState.ValueRO.Description
                });

                globalAlignmentScore += cultureState.ValueRO.CurrentAlignment;
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, 0, 0, 0, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref registryMetadata, timeState.Tick, continuity);

            // Average alignment score
            var avgAlignment = entries.Length > 0 ? globalAlignmentScore / entries.Length : 0f;

            registry.ValueRW = new CultureAlignmentRegistry
            {
                CultureCount = entries.Length,
                GlobalAlignmentScore = math.clamp(avgAlignment, -1f, 1f),
                LastUpdateTick = timeState.Tick
            };
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
