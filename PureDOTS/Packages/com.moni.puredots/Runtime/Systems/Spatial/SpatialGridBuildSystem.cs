using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems.Spatial
{
    /// <summary>
    /// Rebuilds the spatial grid each frame for entities tagged with <see cref="SpatialIndexedTag" />.
    /// Maintains deterministic ordering and double-buffer semantics for consumer safety.
    /// </summary>
    [UpdateInGroup(typeof(global::PureDOTS.Systems.SpatialSystemGroup), OrderFirst = true)]
    public partial struct SpatialGridBuildSystem : ISystem
    {
        private EntityQuery _indexedQuery;
        private ComponentTypeHandle<LocalTransform> _transformHandle;
        private EntityTypeHandle _entityTypeHandle;
        private BufferLookup<RegistryDirectoryEntry> _directoryEntriesLookup;
        private ComponentLookup<SpatialRebuildThresholds> _thresholdsLookup;
        private EntityQuery _directoryQuery;
        private EntityQuery _providerRegistryQuery;
        private SpatialGridConfig _cachedConfig;
        private bool _hasCachedConfig;
        private uint _lastDirtyVersionProcessed;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _indexedQuery = SystemAPI.QueryBuilder()
                .WithAll<SpatialIndexedTag, LocalTransform>()
                .Build();

            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<SpatialGridState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            _entityTypeHandle = state.GetEntityTypeHandle();
            _directoryEntriesLookup = state.GetBufferLookup<RegistryDirectoryEntry>(isReadOnly: true);
            _thresholdsLookup = state.GetComponentLookup<SpatialRebuildThresholds>(isReadOnly: true);
            _directoryQuery = state.GetEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            _providerRegistryQuery = state.GetEntityQuery(ComponentType.ReadOnly<SpatialProviderRegistry>());
            _cachedConfig = default;
            _hasCachedConfig = false;
            _lastDirtyVersionProcessed = 0u;

            state.RequireForUpdate(_directoryQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformHandle.Update(ref state);
            _entityTypeHandle.Update(ref state);
            _directoryEntriesLookup.Update(ref state);
            _thresholdsLookup.Update(ref state);

            var config = SystemAPI.GetSingleton<SpatialGridConfig>();
            if (config.CellCount <= 0 || config.CellSize <= 0f)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
            var stateRW = SystemAPI.GetComponentRW<SpatialGridState>(gridEntity);
            var currentState = stateRW.ValueRO;
            var dirtyOps = state.EntityManager.GetBuffer<SpatialGridDirtyOp>(gridEntity);
            var dirtyCount = dirtyOps.Length;
            var dirtyVersion = currentState.DirtyVersion;
            var configChanged = !_hasCachedConfig || HasConfigChanged(config, _cachedConfig);

            if (!configChanged && dirtyVersion == _lastDirtyVersionProcessed)
            {
                return;
            }

            if (!configChanged && dirtyCount == 0)
            {
                _lastDirtyVersionProcessed = dirtyVersion;
                return;
            }

            var activeEntries = state.EntityManager.GetBuffer<SpatialGridEntry>(gridEntity);
            var activeRanges = state.EntityManager.GetBuffer<SpatialGridCellRange>(gridEntity);
            var stagingEntries = state.EntityManager.GetBuffer<SpatialGridStagingEntry>(gridEntity);
            var stagingRanges = state.EntityManager.GetBuffer<SpatialGridStagingCellRange>(gridEntity);
            var lookupBuffer = state.EntityManager.GetBuffer<SpatialGridEntryLookup>(gridEntity);

            var providerContext = new SpatialGridProviderContext
            {
                IndexedQuery = _indexedQuery,
                TransformHandle = _transformHandle,
                EntityTypeHandle = _entityTypeHandle
            };

            var requiresFullRebuild = configChanged || !_hasCachedConfig || currentState.Version == 0;
            var strategy = SpatialGridRebuildStrategy.Full;
            var totalEntries = activeEntries.Length;
            float rebuildMilliseconds = SystemAPI.Time.DeltaTime * 1000f;

            var providerId = config.ProviderId;
            bool processed = false;

            // Look up provider from registry
            if (!_providerRegistryQuery.IsEmptyIgnoreFilter)
            {
                var registryEntity = _providerRegistryQuery.GetSingletonEntity();
                var registryEntries = state.EntityManager.GetBuffer<SpatialProviderRegistryEntry>(registryEntity);

                if (SpatialProviderRegistryHelpers.TryGetProviderFactoryType(providerId, registryEntries, out var factoryTypeId))
                {
                    // Dispatch to appropriate provider type based on factory type ID
                    if (SpatialProviderRegistryHelpers.TryCreateHashedProvider(factoryTypeId, providerId, out var hashedProvider))
                    {
                        processed = ProcessProvider(ref hashedProvider, ref state, in config, in providerContext, in currentState, dirtyCount, ref requiresFullRebuild, ref strategy, ref totalEntries, ref activeEntries, ref activeRanges, ref stagingEntries, ref stagingRanges, ref lookupBuffer, ref dirtyOps, gridEntity, _thresholdsLookup);
                    }
                    else if (SpatialProviderRegistryHelpers.TryCreateUniformProvider(factoryTypeId, providerId, out var uniformProvider))
                    {
                        processed = ProcessProvider(ref uniformProvider, ref state, in config, in providerContext, in currentState, dirtyCount, ref requiresFullRebuild, ref strategy, ref totalEntries, ref activeEntries, ref activeRanges, ref stagingEntries, ref stagingRanges, ref lookupBuffer, ref dirtyOps, gridEntity, _thresholdsLookup);
                    }
                    else
                    {
                        // Custom provider types would be handled here
                        // For now, fall back to hashed provider
                        UnityEngine.Debug.LogWarning($"[SpatialGridBuildSystem] Unsupported factory type {factoryTypeId} for provider {providerId}; falling back to hashed provider.");
                        var fallbackProvider = new HashedSpatialGridProvider();
                        processed = ProcessProvider(ref fallbackProvider, ref state, in config, in providerContext, in currentState, dirtyCount, ref requiresFullRebuild, ref strategy, ref totalEntries, ref activeEntries, ref activeRanges, ref stagingEntries, ref stagingRanges, ref lookupBuffer, ref dirtyOps, gridEntity, _thresholdsLookup);
                    }
                }
                else
                {
                    // Provider not found in registry; fallback to hashed provider
                    UnityEngine.Debug.LogWarning($"[SpatialGridBuildSystem] Provider id {providerId} not found in registry; falling back to hashed provider.");
                    var fallbackProvider = new HashedSpatialGridProvider();
                    processed = ProcessProvider(ref fallbackProvider, ref state, in config, in providerContext, in currentState, dirtyCount, ref requiresFullRebuild, ref strategy, ref totalEntries, ref activeEntries, ref activeRanges, ref stagingEntries, ref stagingRanges, ref lookupBuffer, ref dirtyOps, gridEntity, _thresholdsLookup);
                }
            }
            else
            {
                // Registry not initialized; fallback to direct instantiation (backward compatibility)
                if (providerId == SpatialGridProviderIds.Uniform)
                {
                    var uniformProvider = new UniformSpatialGridProvider();
                    processed = ProcessProvider(ref uniformProvider, ref state, in config, in providerContext, in currentState, dirtyCount, ref requiresFullRebuild, ref strategy, ref totalEntries, ref activeEntries, ref activeRanges, ref stagingEntries, ref stagingRanges, ref lookupBuffer, ref dirtyOps, gridEntity, _thresholdsLookup);
                }
                else
                {
                    var hashedProvider = new HashedSpatialGridProvider();
                    processed = ProcessProvider(ref hashedProvider, ref state, in config, in providerContext, in currentState, dirtyCount, ref requiresFullRebuild, ref strategy, ref totalEntries, ref activeEntries, ref activeRanges, ref stagingEntries, ref stagingRanges, ref lookupBuffer, ref dirtyOps, gridEntity, _thresholdsLookup);
                }
            }

            if (!processed)
            {
                return;
            }

            _cachedConfig = config;
            _hasCachedConfig = true;
            _lastDirtyVersionProcessed = dirtyVersion;

            var lastTick = timeState.Tick;
            if (SystemAPI.TryGetSingleton<TimeContext>(out var timeContext))
            {
                lastTick = timeContext.ViewTick;
            }

            var nextState = new SpatialGridState
            {
                ActiveBufferIndex = (currentState.ActiveBufferIndex + 1) & 1,
                TotalEntries = totalEntries,
                Version = currentState.Version + 1,
                LastUpdateTick = lastTick,
                LastDirtyTick = currentState.LastDirtyTick,
                DirtyVersion = currentState.DirtyVersion,
                DirtyAddCount = currentState.DirtyAddCount,
                DirtyUpdateCount = currentState.DirtyUpdateCount,
                DirtyRemoveCount = currentState.DirtyRemoveCount,
                LastRebuildMilliseconds = rebuildMilliseconds,
                LastStrategy = strategy
            };

            stateRW.ValueRW = nextState;

            if (SystemAPI.HasComponent<SpatialRegistryMetadata>(gridEntity))
            {
                var metadata = SystemAPI.GetComponentRW<SpatialRegistryMetadata>(gridEntity);
                var value = metadata.ValueRO;
                value.ResetHandles();

                if (!_directoryQuery.IsEmptyIgnoreFilter)
                {
                    var directoryEntity = _directoryQuery.GetSingletonEntity();
                    if (_directoryEntriesLookup.TryGetBuffer(directoryEntity, out var directoryEntries))
                    {
                        for (var i = 0; i < directoryEntries.Length; i++)
                        {
                            value.SetHandle(directoryEntries[i].Handle);
                        }
                    }
                }

                metadata.ValueRW = value;
            }
        }

        private static bool HasConfigChanged(in SpatialGridConfig current, in SpatialGridConfig cached)
        {
            var worldMinChanged = math.any(math.abs(current.WorldMin - cached.WorldMin) > 1e-3f);
            var worldMaxChanged = math.any(math.abs(current.WorldMax - cached.WorldMax) > 1e-3f);
            var cellSizeChanged = math.abs(current.CellSize - cached.CellSize) > 1e-4f;
            var cellCountsChanged = math.any(current.CellCounts != cached.CellCounts);
            var hashChanged = current.HashSeed != cached.HashSeed;
            var providerChanged = current.ProviderId != cached.ProviderId;
            return worldMinChanged || worldMaxChanged || cellSizeChanged || cellCountsChanged || hashChanged || providerChanged;
        }

        internal static void CopyStagingToActive(
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridEntry> activeEntries,
            in DynamicBuffer<SpatialGridStagingCellRange> stagingRanges,
            in DynamicBuffer<SpatialGridStagingEntry> stagingEntries)
        {
            activeEntries.Clear();
            activeEntries.ResizeUninitialized(stagingEntries.Length);

            for (var i = 0; i < stagingEntries.Length; i++)
            {
                var source = stagingEntries[i];
                activeEntries[i] = new SpatialGridEntry
                {
                    Entity = source.Entity,
                    Position = source.Position,
                    CellId = source.CellId
                };
            }

            activeRanges.Clear();
            activeRanges.ResizeUninitialized(stagingRanges.Length);
            for (var i = 0; i < stagingRanges.Length; i++)
            {
                var range = stagingRanges[i];
                activeRanges[i] = new SpatialGridCellRange
                {
                    StartIndex = range.StartIndex,
                    Count = range.Count
                };
            }
        }

        private static bool ProcessProvider<TProvider>(
            ref TProvider selectedProvider,
            ref SystemState state,
            in SpatialGridConfig config,
            in SpatialGridProviderContext providerContext,
            in SpatialGridState currentState,
            int dirtyCount,
            ref bool requiresFullRebuild,
            ref SpatialGridRebuildStrategy strategy,
            ref int totalEntries,
            ref DynamicBuffer<SpatialGridEntry> activeEntries,
            ref DynamicBuffer<SpatialGridCellRange> activeRanges,
            ref DynamicBuffer<SpatialGridStagingEntry> stagingEntries,
            ref DynamicBuffer<SpatialGridStagingCellRange> stagingRanges,
            ref DynamicBuffer<SpatialGridEntryLookup> lookupBuffer,
            ref DynamicBuffer<SpatialGridDirtyOp> dirtyOps,
            Entity gridEntity,
            in ComponentLookup<SpatialRebuildThresholds> thresholdsLookup)
            where TProvider : struct, ISpatialGridProvider
        {
            if (!selectedProvider.ValidateConfig(in config, out var validationError))
            {
                if (validationError.Length > 0)
                {
                    LogSpatialProviderValidationError(validationError);
                }

                return false;
            }

            if (!requiresFullRebuild)
            {
                // Get rebuild thresholds from config entity or use defaults
                var thresholds = SpatialRebuildThresholds.CreateDefaults();
                if (thresholdsLookup.HasComponent(gridEntity))
                {
                    thresholds = thresholdsLookup[gridEntity];
                }

                var activeCount = math.max(currentState.TotalEntries, 0);
                var dirtyRatio = activeCount > 0 ? (float)dirtyCount / math.max(activeCount, 1) : 1f;

                // Use configurable thresholds instead of hard-coded values
                var exceedsMaxOps = dirtyCount >= thresholds.MaxDirtyOpsForPartialRebuild;
                var exceedsMaxRatio = dirtyRatio >= thresholds.MaxDirtyRatioForPartialRebuild;
                var belowMinEntries = activeCount < thresholds.MinEntryCountForPartialRebuild;

                requiresFullRebuild = exceedsMaxOps || exceedsMaxRatio || belowMinEntries;

                if (!requiresFullRebuild)
                {
                    if (!selectedProvider.TryApplyPartialRebuild(ref activeEntries, ref activeRanges, ref lookupBuffer, in dirtyOps, in config))
                    {
                        requiresFullRebuild = true;
                    }
                    else
                    {
                        strategy = SpatialGridRebuildStrategy.Partial;
                        totalEntries = activeEntries.Length;
                        dirtyOps.Clear();
                    }
                }
            }

            if (requiresFullRebuild)
            {
                totalEntries = selectedProvider.PerformFullRebuild(ref state, in config, in providerContext, ref activeEntries, ref activeRanges, ref stagingEntries, ref stagingRanges);
                selectedProvider.RebuildLookup(ref lookupBuffer, in activeEntries);
                strategy = SpatialGridRebuildStrategy.Full;
                dirtyOps.Clear();
            }

            return true;
        }

        private static void LogSpatialProviderValidationError(Unity.Collections.FixedString128Bytes validationError)
        {
            UnityEngine.Debug.LogError("[SpatialGridBuildSystem] Invalid spatial grid config.");
        }

        public struct SpatialGridEntryCellComparer : IComparer<SpatialGridStagingEntry>
        {
            public int Compare(SpatialGridStagingEntry x, SpatialGridStagingEntry y)
            {
                var cellCompare = x.CellId.CompareTo(y.CellId);
                if (cellCompare != 0)
                {
                    return cellCompare;
                }

                var indexCompare = x.Entity.Index.CompareTo(y.Entity.Index);
                if (indexCompare != 0)
                {
                    return indexCompare;
                }

                return x.Entity.Version.CompareTo(y.Entity.Version);
            }
        }





        
    }
}
