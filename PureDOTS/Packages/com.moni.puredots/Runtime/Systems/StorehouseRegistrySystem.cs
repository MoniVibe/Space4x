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
    /// Maintains a registry of all storehouses with capacity information for efficient queries.
    /// Updates singleton component and buffer with current storehouse state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(ResourceRegistrySystem))]
    [UpdateBefore(typeof(ResourceDepositSystem))]
    public partial struct StorehouseRegistrySystem : ISystem
    {
        private EntityQuery _storehouseQuery;
        private ComponentLookup<StorehouseJobReservation> _reservationLookup;
        private BufferLookup<StorehouseReservationItem> _reservationItemsLookup;
        private BufferLookup<StorehouseCapacityElement> _capacityLookup;
        private BufferLookup<StorehouseInventoryItem> _inventoryItemsLookup;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _storehouseQuery = SystemAPI.QueryBuilder()
                .WithAll<StorehouseConfig, StorehouseInventory>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ResourceTypeIndex>();
            state.RequireForUpdate<StorehouseRegistry>();

            _reservationLookup = state.GetComponentLookup<StorehouseJobReservation>(true);
            _reservationItemsLookup = state.GetBufferLookup<StorehouseReservationItem>(true);
            _capacityLookup = state.GetBufferLookup<StorehouseCapacityElement>(true);
            _inventoryItemsLookup = state.GetBufferLookup<StorehouseInventoryItem>(true);
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

            var registryEntity = SystemAPI.GetSingletonEntity<StorehouseRegistry>();
            var registry = SystemAPI.GetComponentRW<StorehouseRegistry>(registryEntity);
            var entries = state.EntityManager.GetBuffer<StorehouseRegistryEntry>(registryEntity);
            ref var registryMetadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;

            var totalStorehouses = 0;
            var totalCapacity = 0f;
            var totalStored = 0f;

            var catalogRef = SystemAPI.GetSingleton<ResourceTypeIndex>().Catalog;
            if (!catalogRef.IsCreated)
            {
                return;
            }

            ref var catalog = ref catalogRef.Value;

            _reservationLookup.Update(ref state);
            _reservationItemsLookup.Update(ref state);
            _capacityLookup.Update(ref state);
            _inventoryItemsLookup.Update(ref state);
            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            _residencyLookup.Update(ref state);

            var expectedCount = math.max(16, _storehouseQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<StorehouseRegistryEntry>(expectedCount, Allocator.Temp);

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

            // Query all storehouses
            foreach (var (inventory, transform, entity) in SystemAPI.Query<RefRO<StorehouseInventory>, RefRO<LocalTransform>>()
                .WithAll<StorehouseConfig>()
                .WithEntityAccess())
            {
                var typeSummaries = new FixedList64Bytes<StorehouseRegistryCapacitySummary>();
                var reservation = _reservationLookup.HasComponent(entity)
                    ? _reservationLookup[entity]
                    : default;

                DynamicBuffer<StorehouseReservationItem> reservationItems = default;
                var hasReservationItems = _reservationItemsLookup.HasBuffer(entity);
                if (hasReservationItems)
                {
                    reservationItems = _reservationItemsLookup[entity];
                }

                DynamicBuffer<StorehouseCapacityElement> capacities = default;
                var hasCapacities = _capacityLookup.HasBuffer(entity);
                if (hasCapacities)
                {
                    capacities = _capacityLookup[entity];
                    for (int i = 0; i < capacities.Length; i++)
                    {
                        var capacity = capacities[i];
                        var typeIndex = catalog.LookupIndex(capacity.ResourceTypeId);
                        if (typeIndex < 0)
                        {
                            continue;
                        }

                        TryAddSummary(ref typeSummaries, new StorehouseRegistryCapacitySummary
                        {
                            ResourceTypeIndex = (ushort)typeIndex,
                            Capacity = capacity.MaxCapacity,
                            Stored = 0f,
                            Reserved = 0f,
                            TierId = (byte)ResourceQualityTier.Unknown,
                            AverageQuality = 0
                        });
                    }
                }

                if (_inventoryItemsLookup.HasBuffer(entity))
                {
                    var inventoryItems = _inventoryItemsLookup[entity];
                    for (int i = 0; i < inventoryItems.Length; i++)
                    {
                        var item = inventoryItems[i];
                        var typeIndex = catalog.LookupIndex(item.ResourceTypeId);
                        if (typeIndex < 0)
                        {
                            continue;
                        }

                        var tierId = item.TierId;
                        var summaryIndex = FindSummaryIndex(ref typeSummaries, (ushort)typeIndex, tierId);
                        if (summaryIndex >= 0)
                        {
                            var summary = typeSummaries[summaryIndex];
                            summary.Stored = item.Amount;
                            summary.Reserved = item.Reserved;
                            summary.TierId = tierId;
                            summary.AverageQuality = item.AverageQuality;
                            typeSummaries[summaryIndex] = summary;
                        }
                        else
                        {
                            var maxCapacity = hasCapacities ? ResolveCapacity(catalogRef, capacities, (ushort)typeIndex) : 0f;
                            TryAddSummary(ref typeSummaries, new StorehouseRegistryCapacitySummary
                            {
                                ResourceTypeIndex = (ushort)typeIndex,
                                Capacity = maxCapacity,
                                Stored = item.Amount,
                                Reserved = item.Reserved,
                                TierId = tierId,
                                AverageQuality = item.AverageQuality
                            });
                        }
                    }
                }

                if (hasReservationItems)
                {
                    for (int i = 0; i < reservationItems.Length; i++)
                    {
                        var item = reservationItems[i];
                        var idx = FindSummaryIndex(ref typeSummaries, item.ResourceTypeIndex, (byte)ResourceQualityTier.Unknown);
                        if (idx >= 0)
                        {
                            var summary = typeSummaries[idx];
                            summary.Reserved += item.Reserved;
                            typeSummaries[idx] = summary;
                        }
                        else
                        {
                            var maxCapacity = hasCapacities ? ResolveCapacity(catalogRef, capacities, item.ResourceTypeIndex) : 0f;
                            TryAddSummary(ref typeSummaries, new StorehouseRegistryCapacitySummary
                            {
                                ResourceTypeIndex = item.ResourceTypeIndex,
                                Capacity = maxCapacity,
                                Stored = 0f,
                                Reserved = item.Reserved,
                                TierId = (byte)ResourceQualityTier.Unknown,
                                AverageQuality = 0
                            });
                        }
                    }
                }

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

                // Refresh aggregate (tier unknown) entries so capacity math reflects per-tier storage.
                for (int summaryIndex = 0; summaryIndex < typeSummaries.Length; summaryIndex++)
                {
                    if (typeSummaries[summaryIndex].TierId != (byte)ResourceQualityTier.Unknown)
                    {
                        continue;
                    }

                    var resourceType = typeSummaries[summaryIndex].ResourceTypeIndex;
                    var aggregate = typeSummaries[summaryIndex];
                    for (int other = 0; other < typeSummaries.Length; other++)
                    {
                        if (other == summaryIndex)
                        {
                            continue;
                        }

                        var otherSummary = typeSummaries[other];
                        if (otherSummary.ResourceTypeIndex != resourceType)
                        {
                            continue;
                        }

                        aggregate.Stored += otherSummary.Stored;
                        aggregate.Reserved += otherSummary.Reserved;
                    }

                    typeSummaries[summaryIndex] = aggregate;
                }

                var dominantTier = ResourceQualityTier.Unknown;
                var weightedQuality = 0f;
                var totalQualityWeight = 0f;
                for (int s = 0; s < typeSummaries.Length; s++)
                {
                    var summary = typeSummaries[s];
                    if (summary.Stored <= 0f)
                    {
                        continue;
                    }

                    totalQualityWeight += summary.Stored;
                    weightedQuality += summary.AverageQuality * summary.Stored;
                    if (summary.TierId > (byte)dominantTier)
                    {
                        dominantTier = (ResourceQualityTier)summary.TierId;
                    }
                }

                var avgQuality = totalQualityWeight > 0f
                    ? (ushort)math.clamp(math.round(weightedQuality / totalQualityWeight), 0f, 600f)
                    : (ushort)0;

                builder.Add(new StorehouseRegistryEntry
                {
                    StorehouseEntity = entity,
                    Position = position,
                    TotalCapacity = inventory.ValueRO.TotalCapacity,
                    TotalStored = inventory.ValueRO.TotalStored,
                    TypeSummaries = typeSummaries,
                    LastMutationTick = math.max(inventory.ValueRO.LastUpdateTick, reservation.LastMutationTick),
                    CellId = cellId,
                    SpatialVersion = spatialVersion,
                    DominantTier = dominantTier,
                    AverageQuality = avgQuality
                });

                totalStorehouses++;
                totalCapacity += inventory.ValueRO.TotalCapacity;
                totalStored += inventory.ValueRO.TotalStored;
            }

            var continuity = hasSpatial
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersion, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref registryMetadata, timeState.Tick, continuity);

            registry.ValueRW = new StorehouseRegistry
            {
                TotalStorehouses = totalStorehouses,
                TotalCapacity = totalCapacity,
                TotalStored = totalStored,
                LastUpdateTick = timeState.Tick,
                LastSpatialVersion = spatialVersion,
                SpatialResolvedCount = resolvedCount,
                SpatialFallbackCount = fallbackCount,
                SpatialUnmappedCount = unmappedCount
            };
        }

        private static int FindSummaryIndex(ref FixedList64Bytes<StorehouseRegistryCapacitySummary> summaries, ushort resourceTypeIndex, byte tierId)
        {
            for (int i = 0; i < summaries.Length; i++)
            {
                if (summaries[i].ResourceTypeIndex == resourceTypeIndex && summaries[i].TierId == tierId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryAddSummary(ref FixedList64Bytes<StorehouseRegistryCapacitySummary> summaries, in StorehouseRegistryCapacitySummary summary)
        {
            if (summaries.Length >= summaries.Capacity)
            {
                return false;
            }

            summaries.Add(summary);
            return true;
        }

        private static float ResolveCapacity(in BlobAssetReference<ResourceTypeIndexBlob> catalog, DynamicBuffer<StorehouseCapacityElement> capacities, ushort resourceTypeIndex)
        {
            if (!catalog.IsCreated)
            {
                return 0f;
            }

            var resourceId = catalog.Value.Ids[resourceTypeIndex];
            for (int i = 0; i < capacities.Length; i++)
            {
                if (capacities[i].ResourceTypeId.Equals(resourceId))
                {
                    return capacities[i].MaxCapacity;
                }
            }

            return 0f;
        }
    }
}
