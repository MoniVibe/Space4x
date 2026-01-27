using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Maintains a registry of all resource sources indexed by type for efficient queries.
    /// Updates singleton component and buffer with current resource state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup), OrderFirst = true)]
    public partial struct ResourceRegistrySystem : ISystem
    {
        private EntityQuery _resourceQuery;
        private EntityQuery _resourceDepositQuery;
        private ComponentLookup<ResourceJobReservation> _reservationLookup;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;
        private ComponentLookup<ResourceDeposit> _resourceDepositLookup;
        private NativeArray<ResourceTypeMetadata> _typeMetadata;
        private EntityQuery _recipeSetQuery;
        private bool _loggedInitialState;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // NOTE: If ObjectDisposedException occurs for BufferTypeHandle<ResourceRegistryEntry>,
            // temporarily disable this system by uncommenting the line below:
            // state.Enabled = false;
            // return;

            _resourceQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceSourceConfig, ResourceTypeId>()
                .Build();

            _resourceDepositQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceNodeTag, ResourceDeposit>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ResourceTypeIndex>();
            state.RequireForUpdate<ResourceRegistry>();
            state.RequireForUpdate<ResourceRecipeSet>();

            _reservationLookup = state.GetComponentLookup<ResourceJobReservation>(true);
            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(true);
            _resourceDepositLookup = state.GetComponentLookup<ResourceDeposit>(true);
            _typeMetadata = default;
            _recipeSetQuery = state.GetEntityQuery(ComponentType.ReadOnly<ResourceRecipeSet>());
            _loggedInitialState = false;
        }

#if !UNITY_EDITOR
        [BurstCompile]
#endif
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var registryEntity = SystemAPI.GetSingletonEntity<ResourceRegistry>();
#if UNITY_EDITOR
            if (timeState.Tick == 1)
            {
                UnityEngine.Debug.Log("[ResourceRegistrySystem] OnUpdate start");
            }
#endif
            var registry = SystemAPI.GetComponentRW<ResourceRegistry>(registryEntity);
            var entries = state.EntityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);
            ref var registryMetadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;

            var totalResources = 0;
            var totalActiveResources = 0;

            // Get catalog for type lookups
            var catalog = SystemAPI.GetSingleton<ResourceTypeIndex>().Catalog;
            if (!catalog.IsCreated)
            {
                return;
            }

            EnsureTypeMetadata(ref state, catalog);

            _reservationLookup.Update(ref state);
            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);
            _resourceDepositLookup.Update(ref state);
            var resourceSourceEntityCount = _resourceQuery.CalculateEntityCount();
            var resourceDepositEntityCount = _resourceDepositQuery.CalculateEntityCount();
#if UNITY_EDITOR
            if (!_loggedInitialState)
            {
                UnityEngine.Debug.Log($"[ResourceRegistrySystem] Resource source query count = {resourceSourceEntityCount}");
                int logged = 0;
                foreach (var (typeId, config, entity) in SystemAPI.Query<RefRO<ResourceTypeId>, RefRO<ResourceSourceConfig>>().WithEntityAccess())
                {
                    var typeLabel = typeId.ValueRO.Value.ToString();
                    UnityEngine.Debug.Log($"[ResourceRegistrySystem] Source entity {entity.Index}:{entity.Version} type='{typeLabel}' gatherRate={config.ValueRO.GatherRatePerWorker}");
                    if (++logged >= 5)
                    {
                        break;
                    }
                }
                _loggedInitialState = true;
            }
#endif

            var expectedCount = math.max(16, resourceSourceEntityCount + resourceDepositEntityCount);
            using var builder = new DeterministicRegistryBuilder<ResourceRegistryEntry>(expectedCount, Allocator.Temp);

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig gridConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState gridState);
            var hasSpatial = hasSpatialConfig
                             && hasSpatialState
                             && gridConfig.CellCount > 0
                             && gridConfig.CellSize > 0f;

            RegistrySpatialSyncState syncState = default;
            var hasSyncState = SystemAPI.TryGetSingleton(out syncState);
            var requireSpatialSync = registryMetadata.SupportsSpatialQueries && hasSyncState && syncState.HasSpatialData;
            var spatialVersion = hasSpatial
                ? gridState.Version
                : (requireSpatialSync ? syncState.SpatialVersion : 0u);

            var resolvedCount = 0;
            var fallbackCount = 0;
            var unmappedCount = 0;

            // Query all resource sources
            foreach (var (sourceState, resourceTypeId, transform, entity) in SystemAPI.Query<RefRO<ResourceSourceState>, RefRO<ResourceTypeId>, RefRO<LocalTransform>>()
                .WithAll<ResourceSourceConfig>()
                .WithEntityAccess())
            {
                // Lookup type index
                var typeIndex = catalog.Value.LookupIndex(resourceTypeId.ValueRO.Value);
                if (typeIndex < 0)
                {
#if UNITY_EDITOR
                    if (!_loggedInitialState)
                    {
                        UnityEngine.Debug.LogWarning($"[ResourceRegistrySystem] Unknown resource type '{resourceTypeId.ValueRO.Value}' from entity {entity.Index}:{entity.Version}.");
                    }
#endif
                    continue; // Skip unknown types
                }

                var reservation = _reservationLookup.HasComponent(entity)
                    ? _reservationLookup[entity]
                    : default;

                var position = transform.ValueRO.Position;
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

                var metadata = ResourceTypeMetadata.CreateUnknown();
                if (_typeMetadata.IsCreated && typeIndex < _typeMetadata.Length)
                {
                    metadata = _typeMetadata[typeIndex];
                }

                builder.Add(new ResourceRegistryEntry
                {
                    ResourceTypeIndex = (ushort)typeIndex,
                    SourceEntity = entity,
                    Position = position,
                    UnitsRemaining = sourceState.ValueRO.UnitsRemaining,
                    ActiveTickets = reservation.ActiveTickets,
                    ClaimFlags = reservation.ClaimFlags,
                    LastMutationTick = reservation.LastMutationTick,
                    CellId = cellId,
                    SpatialVersion = spatialVersion,
                    FamilyIndex = metadata.FamilyIndex,
                    Tier = metadata.Tier,
                    QualityTier = sourceState.ValueRO.QualityTier,
                    AverageQuality = sourceState.ValueRO.BaseQuality,
                    KnowledgeMask = 0
                });

                totalResources++;
                if (sourceState.ValueRO.UnitsRemaining > 0f)
                {
                    totalActiveResources++;
                }
            }

            // Query ResourceDeposit entities (rocks with ResourceNodeTag)
            foreach (var (deposit, transform, entity) in SystemAPI.Query<RefRO<ResourceDeposit>, RefRO<LocalTransform>>()
                .WithAll<ResourceNodeTag>()
                .WithEntityAccess())
            {
                // Skip depleted deposits
                if (deposit.ValueRO.CurrentAmount <= 0f)
                {
                    continue;
                }

                // Map ResourceTypeId (int) to ResourceTypeId (FixedString64Bytes) via catalog
                // For now, we'll need to create a mapping or use a default type
                // This is a limitation - ResourceDeposit uses int while catalog uses FixedString64Bytes
                // TODO: Add proper mapping or change ResourceDeposit to use FixedString64Bytes
                
                // For now, skip deposits that can't be mapped (would need ResourceTypeId component)
                // Or add a helper to map int -> FixedString64Bytes via catalog
                // For initial implementation, we'll add them with a default type index
                
                // Note: This requires ResourceDeposit.ResourceTypeId to match catalog indices
                // or we need a mapping component. For now, we'll skip deposits without proper mapping.
                // Future: Add ResourceTypeId component to rocks or create mapping system.
                
                // Skip for now - would need proper type mapping
                // totalResources++;
                // if (deposit.ValueRO.CurrentAmount > 0f)
                // {
                //     totalActiveResources++;
                // }
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref registryMetadata, timeState.Tick, continuity);

            registry.ValueRW = new ResourceRegistry
            {
                TotalResources = totalResources,
                TotalActiveResources = totalActiveResources,
                LastUpdateTick = timeState.Tick,
                LastSpatialVersion = spatialVersion,
                SpatialResolvedCount = resolvedCount,
                SpatialFallbackCount = fallbackCount,
                SpatialUnmappedCount = unmappedCount
            };
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_typeMetadata.IsCreated)
            {
                _typeMetadata.Dispose();
            }
        }

        private void EnsureTypeMetadata(ref SystemState state, BlobAssetReference<ResourceTypeIndexBlob> catalogRef)
        {
            if (_typeMetadata.IsCreated)
            {
                return;
            }

            if (!catalogRef.IsCreated)
            {
                return;
            }

            using var recipeSetEntities = _recipeSetQuery.ToEntityArray(Allocator.Temp);
            if (recipeSetEntities.Length != 1)
            {
                UnityEngine.Debug.LogError($"[ResourceRegistrySystem] Expected exactly 1 ResourceRecipeSet singleton, found {recipeSetEntities.Length}. Resource metadata will not be initialized.");
                return;
            }

            var recipeSet = state.EntityManager.GetComponentData<ResourceRecipeSet>(recipeSetEntities[0]).Value;
            if (!recipeSet.IsCreated)
            {
                return;
            }

            ref var catalog = ref catalogRef.Value;
            _typeMetadata = new NativeArray<ResourceTypeMetadata>(catalog.Ids.Length, Allocator.Persistent);
            for (int i = 0; i < _typeMetadata.Length; i++)
            {
                _typeMetadata[i] = ResourceTypeMetadata.CreateUnknown();
            }

            ref var recipeBlob = ref recipeSet.Value;

            for (ushort familyIndex = 0; familyIndex < recipeBlob.Families.Length; familyIndex++)
            {
                ref var family = ref recipeBlob.Families[familyIndex];
                AssignFamilyMetadata(ref catalog, ref _typeMetadata, family.RawResourceId, familyIndex, ResourceTier.Raw);
                AssignFamilyMetadata(ref catalog, ref _typeMetadata, family.RefinedResourceId, familyIndex, ResourceTier.Refined);
                AssignFamilyMetadata(ref catalog, ref _typeMetadata, family.CompositeResourceId, familyIndex, ResourceTier.Composite);
            }

            for (int i = 0; i < recipeBlob.Recipes.Length; i++)
            {
                ref var recipe = ref recipeBlob.Recipes[i];
                var tier = recipe.Kind switch
                {
                    ResourceRecipeKind.Refinement => ResourceTier.Refined,
                    ResourceRecipeKind.Composite => ResourceTier.Composite,
                    ResourceRecipeKind.Byproduct => ResourceTier.Byproduct,
                    _ => ResourceTier.Unknown
                };

                AssignTierIfEmpty(ref catalog, ref _typeMetadata, recipe.OutputResourceId, tier);
            }
        }

        private struct ResourceTypeMetadata
        {
            public ushort FamilyIndex;
            public ResourceTier Tier;

            public static ResourceTypeMetadata CreateUnknown()
            {
                return new ResourceTypeMetadata
                {
                    FamilyIndex = ushort.MaxValue,
                    Tier = ResourceTier.Unknown
                };
            }
        }

        private static void AssignFamilyMetadata(ref ResourceTypeIndexBlob catalog, ref NativeArray<ResourceTypeMetadata> metadataArray, FixedString64Bytes resourceId, ushort familyIndex, ResourceTier tier)
        {
            if (resourceId.Length == 0)
            {
                return;
            }

            var index = catalog.LookupIndex(resourceId);
            if (index < 0 || index >= metadataArray.Length)
            {
                return;
            }

            var metadata = metadataArray[index];
            metadata.FamilyIndex = familyIndex;
            metadata.Tier = tier;
            metadataArray[index] = metadata;
        }

        private static void AssignTierIfEmpty(ref ResourceTypeIndexBlob catalog, ref NativeArray<ResourceTypeMetadata> metadataArray, FixedString64Bytes resourceId, ResourceTier tier)
        {
            if (resourceId.Length == 0)
            {
                return;
            }

            var index = catalog.LookupIndex(resourceId);
            if (index < 0 || index >= metadataArray.Length)
            {
                return;
            }

            var metadata = metadataArray[index];
            if (metadata.Tier == ResourceTier.Unknown)
            {
                metadata.Tier = tier;
                metadataArray[index] = metadata;
            }
        }
    }
}
