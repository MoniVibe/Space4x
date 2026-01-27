using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Computes health metrics for each active registry and updates aggregated monitoring state.
    /// </summary>
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(RegistryDirectorySystem))]
    public partial struct RegistryHealthSystem : ISystem
    {
        private ComponentLookup<RegistryMetadata> _metadataLookup;
        private ComponentLookup<RegistryHealth> _healthLookup;
        private ComponentLookup<ResourceRegistry> _resourceRegistryLookup;
        private ComponentLookup<StorehouseRegistry> _storehouseRegistryLookup;
        private BufferLookup<ResourceRegistryEntry> _resourceEntriesLookup;
        private BufferLookup<StorehouseRegistryEntry> _storehouseEntriesLookup;
        private BufferLookup<RegistryDirectoryEntry> _directoryEntriesLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RegistryHealthMonitoring>();
            state.RequireForUpdate<RegistryDirectory>();
            state.RequireForUpdate<TimeState>();

            _metadataLookup = state.GetComponentLookup<RegistryMetadata>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<RegistryHealth>(isReadOnly: false);
            _resourceRegistryLookup = state.GetComponentLookup<ResourceRegistry>(isReadOnly: true);
            _storehouseRegistryLookup = state.GetComponentLookup<StorehouseRegistry>(isReadOnly: true);
            _resourceEntriesLookup = state.GetBufferLookup<ResourceRegistryEntry>(isReadOnly: true);
            _storehouseEntriesLookup = state.GetBufferLookup<StorehouseRegistryEntry>(isReadOnly: true);
            _directoryEntriesLookup = state.GetBufferLookup<RegistryDirectoryEntry>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<RegistryHealthMonitoring>())
            {
                return;
            }

            var monitoring = SystemAPI.GetSingletonRW<RegistryHealthMonitoring>();
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            if (monitoring.ValueRO.MinCheckIntervalTicks > 0u)
            {
                var ticksSinceLast = currentTick >= monitoring.ValueRO.LastCheckTick
                    ? currentTick - monitoring.ValueRO.LastCheckTick
                    : 0u;

                if (ticksSinceLast < monitoring.ValueRO.MinCheckIntervalTicks)
                {
                    return;
                }
            }

            monitoring.ValueRW.LastCheckTick = currentTick;

            var thresholds = SystemAPI.HasSingleton<RegistryHealthThresholds>()
                ? SystemAPI.GetSingleton<RegistryHealthThresholds>()
                : RegistryHealthThresholds.CreateDefaults();

            var directoryEntity = SystemAPI.GetSingletonEntity<RegistryDirectory>();

            _metadataLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _resourceRegistryLookup.Update(ref state);
            _storehouseRegistryLookup.Update(ref state);
            _resourceEntriesLookup.Update(ref state);
            _storehouseEntriesLookup.Update(ref state);
            _directoryEntriesLookup.Update(ref state);

            var directory = SystemAPI.GetComponentRO<RegistryDirectory>(directoryEntity).ValueRO;

            if (!_directoryEntriesLookup.TryGetBuffer(directoryEntity, out var entries))
            {
                monitoring.ValueRW.WorstHealthLevel = RegistryHealthLevel.Healthy;
                monitoring.ValueRW.UnhealthyRegistryCount = 0;
                return;
            }

            var worstLevel = RegistryHealthLevel.Healthy;
            var unhealthyCount = 0;

            var spatialVersion = 0u;
            var hasSpatialGrid = SystemAPI.TryGetSingleton<SpatialGridState>(out var spatialState);
            if (hasSpatialGrid)
            {
                spatialVersion = spatialState.Version;
            }

            var hasSpatialSync = SystemAPI.TryGetSingleton<RegistrySpatialSyncState>(out var spatialSyncState);

            for (var i = 0; i < entries.Length; i++)
            {
                var directoryEntry = entries[i];
                var registryEntity = directoryEntry.Handle.RegistryEntity;

                if (!_metadataLookup.HasComponent(registryEntity) || !_healthLookup.HasComponent(registryEntity))
                {
                    continue;
                }

                var metadata = _metadataLookup[registryEntity];
                var health = _healthLookup[registryEntity];
                var previousLevel = health.HealthLevel;

                var totalEntries = math.max(metadata.EntryCount, 0);
                var ticksSinceUpdate = currentTick >= metadata.LastUpdateTick
                    ? currentTick - metadata.LastUpdateTick
                    : 0u;
                var hasEverUpdated = metadata.LastUpdateTick != 0;
                if (!hasEverUpdated)
                {
                    ticksSinceUpdate = 0;
                }
                var directoryVersionDelta = directory.Version >= metadata.Version
                    ? directory.Version - metadata.Version
                    : metadata.Version - directory.Version;

                var staleEntryCount = 0;

                switch (directoryEntry.Kind)
                {
                    case RegistryKind.Resource:
                        if (thresholds.MaxStaleTickAge > 0u && _resourceEntriesLookup.HasBuffer(registryEntity))
                        {
                            staleEntryCount = CountStaleEntries(_resourceEntriesLookup[registryEntity], currentTick, thresholds.MaxStaleTickAge);
                        }
                        break;

                    case RegistryKind.Storehouse:
                        if (thresholds.MaxStaleTickAge > 0u && _storehouseEntriesLookup.HasBuffer(registryEntity))
                        {
                            staleEntryCount = CountStaleEntries(_storehouseEntriesLookup[registryEntity], currentTick, thresholds.MaxStaleTickAge);
                        }
                        break;

                    default:
                        staleEntryCount = 0;
                        break;
                }

                var continuity = metadata.Continuity;
                var hasContinuity = continuity.HasSpatialData;
                var requireSpatialSync = metadata.SupportsSpatialQueries && continuity.RequiresSpatialSync;

                var referenceSpatialVersion = 0u;
                var hasReferenceSpatialVersion = false;

                if (hasSpatialSync && spatialSyncState.HasSpatialData)
                {
                    referenceSpatialVersion = spatialSyncState.SpatialVersion;
                    hasReferenceSpatialVersion = true;
                }
                else if (hasSpatialGrid)
                {
                    referenceSpatialVersion = spatialVersion;
                    hasReferenceSpatialVersion = true;
                }

                var flags = RegistryHealthFlags.None;
                var healthLevel = RegistryHealthLevel.Healthy;
                var spatialVersionDelta = 0u;

                if (requireSpatialSync && !hasContinuity)
                {
                    flags |= RegistryHealthFlags.SpatialContinuityMissing;
                    healthLevel = Max(healthLevel, RegistryHealthLevel.Failure);
                    if (hasReferenceSpatialVersion)
                    {
                        spatialVersionDelta = referenceSpatialVersion;
                    }
                }
                else if (hasContinuity && hasReferenceSpatialVersion)
                {
                    spatialVersionDelta = referenceSpatialVersion >= continuity.SpatialVersion
                        ? referenceSpatialVersion - continuity.SpatialVersion
                        : continuity.SpatialVersion - referenceSpatialVersion;
                }

                var staleRatio = totalEntries > 0 ? (float)staleEntryCount / totalEntries : 0f;

                if (thresholds.StaleEntryCriticalRatio > 0f && staleRatio >= thresholds.StaleEntryCriticalRatio)
                {
                    flags |= RegistryHealthFlags.StaleEntriesCritical;
                    healthLevel = Max(healthLevel, RegistryHealthLevel.Critical);
                }
                else if (thresholds.StaleEntryWarningRatio > 0f && staleRatio >= thresholds.StaleEntryWarningRatio)
                {
                    flags |= RegistryHealthFlags.StaleEntriesWarning;
                    healthLevel = Max(healthLevel, RegistryHealthLevel.Warning);
                }

                if (thresholds.SpatialVersionMismatchCritical > 0u && spatialVersionDelta >= thresholds.SpatialVersionMismatchCritical)
                {
                    flags |= RegistryHealthFlags.SpatialMismatchCritical;
                    healthLevel = Max(healthLevel, RegistryHealthLevel.Critical);
                }
                else if (thresholds.SpatialVersionMismatchWarning > 0u && spatialVersionDelta >= thresholds.SpatialVersionMismatchWarning)
                {
                    flags |= RegistryHealthFlags.SpatialMismatchWarning;
                    healthLevel = Max(healthLevel, RegistryHealthLevel.Warning);
                }

                if (thresholds.MinUpdateFrequencyTicks > 0u && hasEverUpdated && ticksSinceUpdate > thresholds.MinUpdateFrequencyTicks)
                {
                    flags |= RegistryHealthFlags.UpdateFrequencyWarning;
                    healthLevel = Max(healthLevel, RegistryHealthLevel.Warning);
                }

                if (thresholds.DirectoryVersionMismatchWarning > 0u && directoryVersionDelta > thresholds.DirectoryVersionMismatchWarning)
                {
                    flags |= RegistryHealthFlags.DirectoryMismatchWarning;
                    healthLevel = Max(healthLevel, RegistryHealthLevel.Warning);
                }

                health.HealthLevel = healthLevel;
                health.StaleEntryCount = staleEntryCount;
                health.StaleEntryRatio = staleRatio;
                health.SpatialVersionDelta = spatialVersionDelta;
                health.TicksSinceLastUpdate = ticksSinceUpdate;
                health.DirectoryVersionDelta = directoryVersionDelta;
                health.TotalEntryCount = totalEntries;
                health.LastHealthCheckTick = currentTick;
                health.FailureFlags = flags;

                _healthLookup[registryEntity] = health;

                if (healthLevel >= RegistryHealthLevel.Warning)
                {
                    unhealthyCount++;
                }

                worstLevel = Max(worstLevel, healthLevel);

                if (monitoring.ValueRO.LogWarnings && healthLevel > previousLevel)
                {
                    var label = metadata.Label;
                    UnityEngine.Debug.LogWarning($"[RegistryHealth] {label.ToString()} degraded to {healthLevel} (flags: {flags}).");
                }
            }

            monitoring.ValueRW.WorstHealthLevel = worstLevel;
            monitoring.ValueRW.UnhealthyRegistryCount = unhealthyCount;
        }

        private static int CountStaleEntries(DynamicBuffer<ResourceRegistryEntry> entries, uint currentTick, uint maxAge)
        {
            if (entries.Length == 0)
            {
                return 0;
            }

            var staleCount = 0;
            var array = entries.AsNativeArray();
            for (var i = 0; i < array.Length; i++)
            {
                var entry = array[i];
                var age = currentTick >= entry.LastMutationTick ? currentTick - entry.LastMutationTick : 0u;
                if (age > maxAge)
                {
                    staleCount++;
                }
            }

            return staleCount;
        }

        private static int CountStaleEntries(DynamicBuffer<StorehouseRegistryEntry> entries, uint currentTick, uint maxAge)
        {
            if (entries.Length == 0)
            {
                return 0;
            }

            var staleCount = 0;
            var array = entries.AsNativeArray();
            for (var i = 0; i < array.Length; i++)
            {
                var entry = array[i];
                var age = currentTick >= entry.LastMutationTick ? currentTick - entry.LastMutationTick : 0u;
                if (age > maxAge)
                {
                    staleCount++;
                }
            }

            return staleCount;
        }

        private static RegistryHealthLevel Max(RegistryHealthLevel lhs, RegistryHealthLevel rhs)
        {
            return (RegistryHealthLevel)math.max((int)lhs, (int)rhs);
        }
    }
}


