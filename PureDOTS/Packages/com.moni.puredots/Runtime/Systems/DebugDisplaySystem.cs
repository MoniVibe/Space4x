using PureDOTS.Environment;
using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy;
using PureDOTS.Runtime.Alignment;
using PureDOTS.Runtime.Mobility;
using PureDOTS.Runtime.Pooling;
using PureDOTS.Runtime.Orders;
#if CAMERA_RIG_ENABLED
using PureDOTS.Runtime.Camera;
#endif
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Transport;
using PureDOTS.Runtime.Presentation;
using PureDOTS.Runtime.Signals;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Streaming;
using PureDOTS.Runtime.Telemetry;
#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Devtools;
#endif
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Updates debug display singleton with current simulation state.
    /// Runs in presentation group to provide data for UI layers.
    /// Deterministic and Burst-safe.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct DebugDisplaySystem : ISystem
    {
        private EntityQuery _villagerQuery;
        private EntityQuery _storehouseQuery;
        private EntityQuery _sunlightQuery;
#if CAMERA_RIG_ENABLED
        private EntityQuery _cameraRigTelemetryQuery;
#endif
        private ComponentLookup<RegistryMetadata> _registryMetadataLookup;
        private BufferLookup<RegistryDirectoryEntry> _registryDirectoryLookup;
        private ComponentLookup<RegistryHealth> _registryHealthLookup;
        private ComponentLookup<RegistryInstrumentationState> _registryInstrumentationLookup;
        private ComponentLookup<RegistryContinuityState> _registryContinuityLookup;
        private BufferLookup<RegistryContinuityAlert> _registryContinuityAlertLookup;
        private BufferLookup<SunlightGridRuntimeSample> _sunlightRuntimeLookup;

        public void OnCreate(ref SystemState state)
        {
            // Create singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<DebugDisplayData>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<DebugDisplayData>(entity);
            }

            // Cache queries for performance
            _villagerQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerId>()
                .Build();

            _storehouseQuery = SystemAPI.QueryBuilder()
                .WithAll<StorehouseInventory>()
                .Build();

            _sunlightQuery = SystemAPI.QueryBuilder()
                .WithAll<SunlightGrid>()
                .Build();

#if CAMERA_RIG_ENABLED
            _cameraRigTelemetryQuery = state.GetEntityQuery(ComponentType.ReadOnly<CameraRigTelemetry>());
#endif

            _registryMetadataLookup = state.GetComponentLookup<RegistryMetadata>(isReadOnly: true);
            _registryDirectoryLookup = state.GetBufferLookup<RegistryDirectoryEntry>(isReadOnly: true);
            _registryHealthLookup = state.GetComponentLookup<RegistryHealth>(isReadOnly: true);
            _registryInstrumentationLookup = state.GetComponentLookup<RegistryInstrumentationState>(isReadOnly: true);
            _registryContinuityLookup = state.GetComponentLookup<RegistryContinuityState>(isReadOnly: true);
            _registryContinuityAlertLookup = state.GetBufferLookup<RegistryContinuityAlert>(isReadOnly: true);
            _sunlightRuntimeLookup = state.GetBufferLookup<SunlightGridRuntimeSample>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<DebugDisplayData>())
            {
                return;
            }

            var debugData = SystemAPI.GetSingletonRW<DebugDisplayData>();
            _registryMetadataLookup.Update(ref state);
            _registryDirectoryLookup.Update(ref state);
            _registryHealthLookup.Update(ref state);
            _registryInstrumentationLookup.Update(ref state);
            _registryContinuityLookup.Update(ref state);
            _registryContinuityAlertLookup.Update(ref state);

            debugData.ValueRW.TargetTick = 0;
            debugData.ValueRW.IsPlaying = false;
            debugData.ValueRW.CommandLogCount = 0;
            debugData.ValueRW.SnapshotLogCount = 0;
            debugData.ValueRW.TickLogText = default;

            // Update time state
            if (SystemAPI.HasSingleton<TickTimeState>())
            {
                var timeState = SystemAPI.GetSingleton<TickTimeState>();
                debugData.ValueRW.CurrentTick = timeState.Tick;
                debugData.ValueRW.TargetTick = timeState.TargetTick;
                debugData.ValueRW.IsPaused = timeState.IsPaused;
                debugData.ValueRW.IsPlaying = timeState.IsPlaying;

                var text = new FixedString128Bytes();
                text.Append("Tick: ");
                text.Append(timeState.Tick);
                text.Append(" / Target: ");
                text.Append(timeState.TargetTick);
                text.Append(" | Speed: ");
                var speedRounded = math.round(timeState.CurrentSpeedMultiplier * 100f) / 100f;
                text.Append(speedRounded);
                text.Append(" | ");
                text.Append(timeState.IsPlaying ? "Playing" : "Stopped");
                if (timeState.IsPaused)
                {
                    text.Append(" (Paused)");
                }
                debugData.ValueRW.TimeStateText = text;
            }
            else if (SystemAPI.HasSingleton<TimeState>())
            {
                var timeState = SystemAPI.GetSingleton<TimeState>();
                debugData.ValueRW.CurrentTick = timeState.Tick;
                debugData.ValueRW.TargetTick = timeState.Tick;
                debugData.ValueRW.IsPaused = timeState.IsPaused;
                debugData.ValueRW.IsPlaying = !timeState.IsPaused;

                var text = new FixedString128Bytes();
                text.Append("Tick: ");
                text.Append(timeState.Tick);
                text.Append(" | Speed: ");
                var speedRounded = math.round(timeState.CurrentSpeedMultiplier * 100f) / 100f;
                text.Append(speedRounded);
                text.Append(" | ");
                text.Append(timeState.IsPaused ? "Paused" : "Running");
                debugData.ValueRW.TimeStateText = text;
            }

            // Update rewind state
            if (SystemAPI.HasSingleton<RewindState>())
            {
                var rewindState = SystemAPI.GetSingleton<RewindState>();
                uint viewTick = 0;
                if (SystemAPI.HasSingleton<TimeContext>())
                {
                    viewTick = SystemAPI.GetSingleton<TimeContext>().ViewTick;
                }
                else if (SystemAPI.HasSingleton<TickTimeState>())
                {
                    viewTick = SystemAPI.GetSingleton<TickTimeState>().Tick;
                }
                else if (SystemAPI.HasSingleton<TimeState>())
                {
                    viewTick = SystemAPI.GetSingleton<TimeState>().Tick;
                }

                var text = new FixedString128Bytes();
                text.Append("Mode: ");
                switch (rewindState.Mode)
                {
                    case RewindMode.Record:
                        text.Append("Record");
                        break;
                    case RewindMode.Playback:
                        text.Append("Playback");
                        break;
                    case RewindMode.CatchUp:
                        text.Append("CatchUp");
                        break;
                    default:
                        text.Append("Unknown");
                        break;
                }
                text.Append(" | Playback Tick: ");
                text.Append(viewTick);
                text.Append(" → Target: ");
                text.Append(rewindState.TargetTick);
                debugData.ValueRW.RewindStateText = text;
            }

            debugData.ValueRW.CameraRigActive = 0;
            debugData.ValueRW.CameraRigPosition = float3.zero;
            debugData.ValueRW.CameraRigDistance = 0f;
            debugData.ValueRW.CameraRigPitch = 0f;
            debugData.ValueRW.CameraRigYaw = 0f;
            debugData.ValueRW.CameraRigShake = 0f;
            debugData.ValueRW.CameraRigStateText = default;

#if CAMERA_RIG_ENABLED
            if (!_cameraRigTelemetryQuery.IsEmptyIgnoreFilter)
            {
                var cameraRig = _cameraRigTelemetryQuery.GetSingleton<CameraRigTelemetry>();
                debugData.ValueRW.CameraRigActive = 1;
                debugData.ValueRW.CameraRigPosition = cameraRig.Position;
                debugData.ValueRW.CameraRigDistance = cameraRig.Distance;
                debugData.ValueRW.CameraRigPitch = cameraRig.Pitch;
                debugData.ValueRW.CameraRigYaw = cameraRig.Yaw;
                debugData.ValueRW.CameraRigShake = cameraRig.Shake;

                var cameraText = new FixedString128Bytes();
                cameraText.Append("Cam dist=");
                cameraText.Append(math.round(cameraRig.Distance * 100f) / 100f);
                cameraText.Append(" yaw=");
                cameraText.Append(math.round(cameraRig.Yaw * 100f) / 100f);
                cameraText.Append(" pitch=");
                cameraText.Append(math.round(cameraRig.Pitch * 100f) / 100f);
                debugData.ValueRW.CameraRigStateText = cameraText;
            }
#endif

            if (SystemAPI.HasSingleton<InputCommandLogState>() && SystemAPI.HasSingleton<TickSnapshotLogState>())
            {
                var commandState = SystemAPI.GetSingleton<InputCommandLogState>();
                var snapshotState = SystemAPI.GetSingleton<TickSnapshotLogState>();
                debugData.ValueRW.CommandLogCount = commandState.Count;
                debugData.ValueRW.SnapshotLogCount = snapshotState.Count;

                var text = new FixedString128Bytes();
                text.Append("Cmd ");
                text.Append(commandState.Count);
                text.Append("/");
                text.Append(commandState.Capacity);
                text.Append(" | Snap ");
                text.Append(snapshotState.Count);
                text.Append("/");
                text.Append(snapshotState.Capacity);
                text.Append(" | Last Tick ");
                text.Append(math.max(commandState.LastTick, snapshotState.LastTick));

                var commandBytes = commandState.Capacity * UnsafeUtility.SizeOf<InputCommandLogEntry>();
                var snapshotBytes = snapshotState.Capacity * UnsafeUtility.SizeOf<TickSnapshotLogEntry>();
                var totalBytes = commandBytes + snapshotBytes;
                if (SystemAPI.TryGetSingleton(out TimeLogSettings logSettings) && logSettings.MemoryBudgetBytes > 0)
                {
                    text.Append(" | Mem ");
                    text.Append(BytesToKilobytes(totalBytes));
                    text.Append("KB/");
                    text.Append(BytesToKilobytes(logSettings.MemoryBudgetBytes));
                    text.Append("KB");
                    if (totalBytes > logSettings.MemoryBudgetBytes)
                    {
                        text.Append("!");
                    }
                }
                debugData.ValueRW.TickLogText = text;
            }

            debugData.ValueRW.VillagerCount = _villagerQuery.CalculateEntityCount();

            float totalStored = 0f;
            foreach (var inventory in SystemAPI.Query<RefRO<StorehouseInventory>>())
            {
                totalStored += inventory.ValueRO.TotalStored;
            }
            debugData.ValueRW.TotalResourcesStored = totalStored;

            UpdateRegistryDiagnostics(ref state, ref debugData.ValueRW);
            UpdateMobilityDiagnostics(ref state, ref debugData.ValueRW);
            UpdateBatchInventoryDiagnostics(ref state, ref debugData.ValueRW);
            UpdateTradeOpportunityDiagnostics(ref state, ref debugData.ValueRW);
            UpdateOrderAndSignalDiagnostics(ref state, ref debugData.ValueRW);
            UpdateSpawnerDiagnostics(ref state, ref debugData.ValueRW);
            UpdateComplianceDiagnostics(ref state, ref debugData.ValueRW);
            UpdateLogisticsDiagnostics(ref state, ref debugData.ValueRW);
            UpdatePoolingDiagnostics(ref debugData.ValueRW);
            UpdateSpatialDiagnostics(ref state, ref debugData.ValueRW);
            UpdateSunlightDiagnostics(ref state, ref debugData.ValueRW);
            UpdateEnvironmentGridDiagnostics(ref state, ref debugData.ValueRW);
            UpdateStreamingDiagnostics(ref state, ref debugData.ValueRW);
            UpdateFrameTiming(ref state, ref debugData.ValueRW);
            UpdateReplayDiagnostics(ref state, ref debugData.ValueRW);
#if DEVTOOLS_ENABLED
            UpdateSpawnTelemetry(ref debugData.ValueRW, ref state);
#endif
            WriteTelemetrySnapshot(ref state, in debugData.ValueRO);
        }

        private void UpdateRegistryDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.RegisteredRegistryCount = 0;
            debugData.RegisteredEntryCount = 0;
            debugData.RegistryDirectoryVersion = 0;
            debugData.RegistryDirectoryLastUpdateTick = 0;
            debugData.RegistryDirectoryAggregateHash = 0;
            debugData.RegistryStateText = default;
            debugData.RegistryHealthHeadline = default;
            debugData.RegistryHealthAlerts = default;
            debugData.RegistryWorstHealthLevel = (byte)RegistryHealthLevel.Healthy;
            debugData.RegistryHealthyCount = 0;
            debugData.RegistryWarningCount = 0;
            debugData.RegistryCriticalCount = 0;
            debugData.RegistryFailureCount = 0;
            debugData.RegistryInstrumentationVersion = 0;
            debugData.RegistryContinuityVersion = 0;
            debugData.RegistryContinuityWarningCount = 0;
            debugData.RegistryContinuityFailureCount = 0;
            debugData.RegistryContinuityAlerts = default;
            debugData.RegistryHasAlerts = false;
            debugData.RegistryDefinitionCount = 0;
            debugData.RegistryContinuityLastCheckTick = 0;
            debugData.ResourceSpatialResolved = 0;
            debugData.ResourceSpatialFallback = 0;
            debugData.ResourceSpatialUnmapped = 0;
            debugData.StorehouseSpatialResolved = 0;
            debugData.StorehouseSpatialFallback = 0;
            debugData.StorehouseSpatialUnmapped = 0;
            debugData.BandRegistryCount = 0;
            debugData.BandEntryCount = 0;
            debugData.BandTotalMembers = 0;
            debugData.BandSpatialResolved = 0;
            debugData.BandSpatialFallback = 0;
            debugData.BandSpatialUnmapped = 0;
            debugData.FactionRegistryCount = 0;
            debugData.FactionEntryCount = 0;
            debugData.TotalFactionTerritoryCells = 0;
            debugData.TotalFactionResources = 0f;
            debugData.ClimateHazardRegistryCount = 0;
            debugData.ClimateHazardEntryCount = 0;
            debugData.ClimateHazardActiveCount = 0;
            debugData.ClimateHazardGlobalIntensity = 0f;
            debugData.AreaEffectRegistryCount = 0;
            debugData.AreaEffectEntryCount = 0;
            debugData.AreaEffectActiveCount = 0;
            debugData.AreaEffectAverageStrength = 0f;
            debugData.CultureRegistryCount = 0;
            debugData.CultureEntryCount = 0;
            debugData.CultureGlobalAlignmentScore = 0f;

            if (!SystemAPI.TryGetSingletonEntity<RegistryDirectory>(out var registryEntity))
            {
                return;
            }

            var directory = SystemAPI.GetComponentRO<RegistryDirectory>(registryEntity).ValueRO;
            debugData.RegistryDirectoryVersion = directory.Version;
            debugData.RegistryDirectoryLastUpdateTick = directory.LastUpdateTick;
            debugData.RegistryDirectoryAggregateHash = directory.AggregateHash;

            foreach (var catalog in SystemAPI.Query<RefRO<RegistryDefinitionCatalog>>())
            {
                if (!catalog.ValueRO.Catalog.IsCreated)
                {
                    continue;
                }

                var catalogBlob = catalog.ValueRO.Catalog;
                debugData.RegistryDefinitionCount += catalogBlob.Value.Definitions.Length;
            }

            if (!_registryDirectoryLookup.HasBuffer(registryEntity))
            {
                var emptyText = new FixedString128Bytes();
                emptyText.Append("Registries: 0");
                debugData.RegistryStateText = emptyText;
                return;
            }

            var entries = _registryDirectoryLookup[registryEntity];
            var hasInstrumentation = _registryInstrumentationLookup.HasComponent(registryEntity);
            var instrumentationState = hasInstrumentation ? _registryInstrumentationLookup[registryEntity] : default;

            int resourceRegistries = 0;
            int resourceEntries = 0;
            int storehouseRegistries = 0;
            int storehouseEntries = 0;
            int villagerRegistries = 0;
            int villagerEntries = 0;
            int bandRegistries = 0;
            int bandEntries = 0;
            int factionRegistries = 0;
            int factionEntries = 0;
            int hazardRegistries = 0;
            int hazardEntries = 0;
            int areaEffectRegistries = 0;
            int areaEffectEntries = 0;
            int cultureRegistries = 0;
            int cultureEntries = 0;
            int healthyCount = 0;
            int warningCount = 0;
            int criticalCount = 0;
            var worstLevelValue = (int)RegistryHealthLevel.Healthy;
            var alerts = new FixedString512Bytes();

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                debugData.RegisteredRegistryCount++;

                if (_registryMetadataLookup.HasComponent(entry.Handle.RegistryEntity))
                {
                    var metadata = _registryMetadataLookup[entry.Handle.RegistryEntity];
                    debugData.RegisteredEntryCount += metadata.EntryCount;

                    if (_registryHealthLookup.HasComponent(entry.Handle.RegistryEntity))
                    {
                        var health = _registryHealthLookup[entry.Handle.RegistryEntity];
                        UpdateHealthCounters(ref healthyCount, ref warningCount, ref criticalCount, ref worstLevelValue, ref alerts, metadata.Label, health);
                    }
                    else
                    {
                        healthyCount++;
                    }

                    switch (entry.Kind)
                    {
                        case RegistryKind.Resource:
                            resourceRegistries++;
                            resourceEntries += metadata.EntryCount;
                            break;
                        case RegistryKind.Storehouse:
                            storehouseRegistries++;
                            storehouseEntries += metadata.EntryCount;
                            break;
                        case RegistryKind.Villager:
                            villagerRegistries++;
                            villagerEntries += metadata.EntryCount;
                            break;
                        case RegistryKind.Band:
                            bandRegistries++;
                            bandEntries += metadata.EntryCount;
                            break;
                        case RegistryKind.Faction:
                            factionRegistries++;
                            factionEntries += metadata.EntryCount;
                            break;
                        case RegistryKind.ClimateHazard:
                            hazardRegistries++;
                            hazardEntries += metadata.EntryCount;
                            break;
                        case RegistryKind.AreaEffect:
                            areaEffectRegistries++;
                            areaEffectEntries += metadata.EntryCount;
                            break;
                        case RegistryKind.CultureAlignment:
                            cultureRegistries++;
                            cultureEntries += metadata.EntryCount;
                            break;
                    }
                }
                else
                {
                    healthyCount++;
                }
            }

            debugData.BandRegistryCount = bandRegistries;
            debugData.BandEntryCount = bandEntries;
            debugData.FactionRegistryCount = factionRegistries;
            debugData.FactionEntryCount = factionEntries;
            debugData.ClimateHazardRegistryCount = hazardRegistries;
            debugData.ClimateHazardEntryCount = hazardEntries;
            debugData.AreaEffectRegistryCount = areaEffectRegistries;
            debugData.AreaEffectEntryCount = areaEffectEntries;
            debugData.CultureRegistryCount = cultureRegistries;
            debugData.CultureEntryCount = cultureEntries;

            var displayHealthy = healthyCount;
            var displayWarning = warningCount;
            var displayCritical = criticalCount;
            var displayFailure = 0;

            if (hasInstrumentation)
            {
                displayHealthy = instrumentationState.HealthyCount;
                displayWarning = instrumentationState.WarningCount;
                displayCritical = instrumentationState.CriticalCount;
                displayFailure = instrumentationState.FailureCount;
            }

            var text = new FixedString128Bytes();
            text.Append("Registries: ");
            text.Append(debugData.RegisteredRegistryCount);
            text.Append(" | Entries: ");
            text.Append(debugData.RegisteredEntryCount);

            if (resourceRegistries > 0)
            {
                text.Append(" | Res ");
                text.Append(resourceRegistries);
                text.Append("/");
                text.Append(resourceEntries);
            }

            if (storehouseRegistries > 0)
            {
                text.Append(" | Store ");
                text.Append(storehouseRegistries);
                text.Append("/");
                text.Append(storehouseEntries);
            }

            if (villagerRegistries > 0)
            {
                text.Append(" | Vill ");
                text.Append(villagerRegistries);
                text.Append("/");
                text.Append(villagerEntries);
            }

            if (bandRegistries > 0)
            {
                text.Append(" | Band ");
                text.Append(bandRegistries);
                text.Append("/");
                text.Append(bandEntries);
            }

            text.Append(" | Health ok=");
            text.Append(displayHealthy);
            text.Append(" warn=");
            text.Append(displayWarning);
            text.Append(" crit=");
            text.Append(displayCritical);

            if (displayFailure > 0)
            {
                text.Append(" fail=");
                text.Append(displayFailure);
            }

            if (SystemAPI.TryGetSingleton<ResourceRegistry>(out var resourceRegistry))
            {
                debugData.ResourceSpatialResolved = resourceRegistry.SpatialResolvedCount;
                debugData.ResourceSpatialFallback = resourceRegistry.SpatialFallbackCount;
                debugData.ResourceSpatialUnmapped = resourceRegistry.SpatialUnmappedCount;

                if (resourceRegistry.TotalResources > 0)
                {
                    text.Append(" | ResAlign ");
                    text.Append(resourceRegistry.SpatialResolvedCount);
                    text.Append("/");
                    text.Append(resourceRegistry.TotalResources);

                    if (resourceRegistry.SpatialFallbackCount > 0)
                    {
                        text.Append(" f=");
                        text.Append(resourceRegistry.SpatialFallbackCount);
                    }

                    if (resourceRegistry.SpatialUnmappedCount > 0)
                    {
                        text.Append(" u=");
                        text.Append(resourceRegistry.SpatialUnmappedCount);
                    }
                }
            }

            if (SystemAPI.TryGetSingleton<BandRegistry>(out var bandRegistry))
            {
                debugData.BandTotalMembers = bandRegistry.TotalMembers;
                debugData.BandSpatialResolved = bandRegistry.SpatialResolvedCount;
                debugData.BandSpatialFallback = bandRegistry.SpatialFallbackCount;
                debugData.BandSpatialUnmapped = bandRegistry.SpatialUnmappedCount;

                if (bandRegistry.TotalBands > 0)
                {
                    if (bandRegistries == 0)
                    {
                        debugData.BandRegistryCount = math.max(debugData.BandRegistryCount, 1);
                        debugData.BandEntryCount = math.max(debugData.BandEntryCount, bandRegistry.TotalBands);
                        text.Append(" | Band ");
                        text.Append(debugData.BandRegistryCount);
                        text.Append("/");
                        text.Append(debugData.BandEntryCount);
                    }

                    text.Append(" | BandMembers ");
                    text.Append(bandRegistry.TotalMembers);
                    text.Append("/");
                    text.Append(bandRegistry.TotalBands);

                    if (bandRegistry.SpatialFallbackCount > 0)
                    {
                        text.Append(" f=");
                        text.Append(bandRegistry.SpatialFallbackCount);
                    }

                    if (bandRegistry.SpatialUnmappedCount > 0)
                    {
                        text.Append(" u=");
                        text.Append(bandRegistry.SpatialUnmappedCount);
                    }
                }
            }

            if (SystemAPI.TryGetSingleton<StorehouseRegistry>(out var storehouseRegistry))
            {
                debugData.StorehouseSpatialResolved = storehouseRegistry.SpatialResolvedCount;
                debugData.StorehouseSpatialFallback = storehouseRegistry.SpatialFallbackCount;
                debugData.StorehouseSpatialUnmapped = storehouseRegistry.SpatialUnmappedCount;

                if (storehouseRegistry.TotalStorehouses > 0)
                {
                    text.Append(" | StoreAlign ");
                    text.Append(storehouseRegistry.SpatialResolvedCount);
                    text.Append("/");
                    text.Append(storehouseRegistry.TotalStorehouses);

                    if (storehouseRegistry.SpatialFallbackCount > 0)
                    {
                        text.Append(" f=");
                        text.Append(storehouseRegistry.SpatialFallbackCount);
                    }

                    if (storehouseRegistry.SpatialUnmappedCount > 0)
                    {
                        text.Append(" u=");
                        text.Append(storehouseRegistry.SpatialUnmappedCount);
                    }
                }
            }

            if (SystemAPI.TryGetSingletonEntity<FactionRegistry>(out var factionRegistryEntity))
            {
                var factionRegistry = SystemAPI.GetComponentRO<FactionRegistry>(factionRegistryEntity).ValueRO;
                debugData.FactionRegistryCount = math.max(debugData.FactionRegistryCount, factionRegistry.FactionCount > 0 ? 1 : 0);
                debugData.FactionEntryCount = math.max(debugData.FactionEntryCount, factionRegistry.FactionCount);
                debugData.TotalFactionResources = factionRegistry.TotalResources;
                debugData.TotalFactionTerritoryCells = factionRegistry.TotalTerritoryCells;

                if (factionRegistry.FactionCount > 0)
                {
                    text.Append(" | Fact ");
                    text.Append(factionRegistry.FactionCount);
                    text.Append(" terr=");
                    text.Append(factionRegistry.TotalTerritoryCells);
                    if (factionRegistry.TotalResources > 0f)
                    {
                        text.Append(" res=");
                        text.Append(math.round(factionRegistry.TotalResources * 10f) / 10f);
                    }
                }
            }
            else if (debugData.FactionRegistryCount > 0)
            {
                text.Append(" | Fact ");
                text.Append(debugData.FactionRegistryCount);
                text.Append("/");
                text.Append(debugData.FactionEntryCount);
            }

            if (SystemAPI.TryGetSingletonEntity<ClimateHazardRegistry>(out var hazardRegistryEntity))
            {
                var hazardRegistry = SystemAPI.GetComponentRO<ClimateHazardRegistry>(hazardRegistryEntity).ValueRO;
                debugData.ClimateHazardRegistryCount = math.max(debugData.ClimateHazardRegistryCount, hazardRegistry.ActiveHazardCount > 0 ? 1 : debugData.ClimateHazardRegistryCount);
                debugData.ClimateHazardEntryCount = math.max(debugData.ClimateHazardEntryCount, hazardRegistry.ActiveHazardCount);
                debugData.ClimateHazardActiveCount = hazardRegistry.ActiveHazardCount;
                debugData.ClimateHazardGlobalIntensity = hazardRegistry.GlobalHazardIntensity;

                if (hazardRegistry.ActiveHazardCount > 0)
                {
                    text.Append(" | Hazard ");
                    text.Append(hazardRegistry.ActiveHazardCount);
                    text.Append(" I=");
                    text.Append(math.round(hazardRegistry.GlobalHazardIntensity * 100f) / 100f);
                }
            }
            else if (debugData.ClimateHazardRegistryCount > 0)
            {
                text.Append(" | Hazard ");
                text.Append(debugData.ClimateHazardRegistryCount);
                text.Append("/");
                text.Append(debugData.ClimateHazardEntryCount);
            }

            if (SystemAPI.TryGetSingletonEntity<AreaEffectRegistry>(out var areaRegistryEntity))
            {
                var areaRegistry = SystemAPI.GetComponentRO<AreaEffectRegistry>(areaRegistryEntity).ValueRO;
                debugData.AreaEffectActiveCount = areaRegistry.ActiveEffectCount;

                if (state.EntityManager.HasBuffer<AreaEffectRegistryEntry>(areaRegistryEntity))
                {
                    var buffer = state.EntityManager.GetBuffer<AreaEffectRegistryEntry>(areaRegistryEntity, true);
                    if (buffer.Length > 0)
                    {
                        float totalStrength = 0f;
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            totalStrength += buffer[i].CurrentStrength;
                        }
                        debugData.AreaEffectAverageStrength = totalStrength / buffer.Length;
                        debugData.AreaEffectEntryCount = math.max(debugData.AreaEffectEntryCount, buffer.Length);
                    }
                }

                debugData.AreaEffectRegistryCount = math.max(debugData.AreaEffectRegistryCount, areaRegistry.ActiveEffectCount > 0 ? 1 : debugData.AreaEffectRegistryCount);

                if (areaRegistry.ActiveEffectCount > 0)
                {
                    text.Append(" | Effects ");
                    text.Append(areaRegistry.ActiveEffectCount);
                    if (debugData.AreaEffectAverageStrength > 0f)
                    {
                        text.Append(" avg=");
                        text.Append(math.round(debugData.AreaEffectAverageStrength * 100f) / 100f);
                    }
                }
            }
            else if (debugData.AreaEffectRegistryCount > 0)
            {
                text.Append(" | Effects ");
                text.Append(debugData.AreaEffectRegistryCount);
                text.Append("/");
                text.Append(debugData.AreaEffectEntryCount);
            }

            if (SystemAPI.TryGetSingletonEntity<CultureAlignmentRegistry>(out var cultureRegistryEntity))
            {
                var cultureRegistry = SystemAPI.GetComponentRO<CultureAlignmentRegistry>(cultureRegistryEntity).ValueRO;
                debugData.CultureRegistryCount = math.max(debugData.CultureRegistryCount, cultureRegistry.CultureCount > 0 ? 1 : debugData.CultureRegistryCount);
                debugData.CultureEntryCount = math.max(debugData.CultureEntryCount, cultureRegistry.CultureCount);
                debugData.CultureGlobalAlignmentScore = cultureRegistry.GlobalAlignmentScore;

                if (cultureRegistry.CultureCount > 0)
                {
                    text.Append(" | Culture ");
                    text.Append(cultureRegistry.CultureCount);
                    text.Append(" align=");
                    text.Append(math.round(cultureRegistry.GlobalAlignmentScore * 100f) / 100f);
                }
            }
            else if (debugData.CultureRegistryCount > 0)
            {
                text.Append(" | Culture ");
                text.Append(debugData.CultureRegistryCount);
                text.Append("/");
                text.Append(debugData.CultureEntryCount);
            }

            var worstLevel = (RegistryHealthLevel)worstLevelValue;
            if (hasInstrumentation)
            {
                if (instrumentationState.FailureCount > 0)
                {
                    worstLevel = RegistryHealthLevel.Failure;
                }
                else if (instrumentationState.CriticalCount > 0)
                {
                    worstLevel = RegistryHealthLevel.Critical;
                }
                else if (instrumentationState.WarningCount > 0)
                {
                    worstLevel = RegistryHealthLevel.Warning;
                }
                else
                {
                    worstLevel = RegistryHealthLevel.Healthy;
                }
            }

            FixedString512Bytes continuityAlerts = default;
            var continuityWarningCount = 0;
            var continuityFailureCount = 0;
            uint continuityVersion = 0;
            uint continuityLastTick = 0;

            if (SystemAPI.TryGetSingletonEntity<RegistrySpatialSyncState>(out var syncEntity))
            {
                if (_registryContinuityLookup.HasComponent(syncEntity))
                {
                    var continuity = _registryContinuityLookup[syncEntity];
                    continuityWarningCount = continuity.WarningCount;
                    continuityFailureCount = continuity.FailureCount;
                    continuityVersion = continuity.Version;
                    continuityLastTick = continuity.LastCheckTick;
                }

                if (_registryContinuityAlertLookup.HasBuffer(syncEntity))
                {
                    var buffer = _registryContinuityAlertLookup[syncEntity];
                    if (buffer.Length > 0)
                    {
                        var syncState = SystemAPI.GetComponentRO<RegistrySpatialSyncState>(syncEntity).ValueRO;
                        continuityAlerts = BuildContinuityAlerts(buffer, syncState.SpatialVersion);
                    }
                }
            }

            if (continuityAlerts.Length > 0)
            {
                if (alerts.Length > 0)
                {
                    alerts.Append(" || ");
                }
                alerts.Append(continuityAlerts);
            }

            debugData.RegistryHealthHeadline = BuildHealthHeadline(displayHealthy, displayWarning, displayCritical + displayFailure, worstLevel);
            debugData.RegistryHealthAlerts = alerts;
            debugData.RegistryHealthyCount = displayHealthy;
            debugData.RegistryWarningCount = displayWarning;
            debugData.RegistryCriticalCount = displayCritical;
            debugData.RegistryFailureCount = displayFailure;
            debugData.RegistryWorstHealthLevel = (byte)worstLevel;
            debugData.RegistryInstrumentationVersion = hasInstrumentation ? instrumentationState.Version : 0u;
            debugData.RegistryContinuityVersion = continuityVersion;
            debugData.RegistryContinuityWarningCount = continuityWarningCount;
            debugData.RegistryContinuityFailureCount = continuityFailureCount;
            debugData.RegistryContinuityAlerts = continuityAlerts;
            debugData.RegistryContinuityLastCheckTick = continuityLastTick;
            debugData.RegistryHasAlerts = displayWarning > 0 || displayCritical > 0 || displayFailure > 0 || continuityWarningCount > 0 || continuityFailureCount > 0;
            debugData.RegistryStateText = text;
        }

        private static void UpdateHealthCounters(ref int healthyCount, ref int warningCount, ref int criticalCount, ref int worstLevelValue, ref FixedString512Bytes alerts, in FixedString64Bytes label, in RegistryHealth health)
        {
            var levelValue = (int)health.HealthLevel;
            worstLevelValue = math.max(worstLevelValue, levelValue);

            switch (health.HealthLevel)
            {
                case RegistryHealthLevel.Healthy:
                    healthyCount++;
                    return;
                case RegistryHealthLevel.Warning:
                    warningCount++;
                    break;
                case RegistryHealthLevel.Critical:
                case RegistryHealthLevel.Failure:
                    criticalCount++;
                    break;
                default:
                    warningCount++;
                    break;
            }

            AppendAlert(ref alerts, label, health);
        }

        private static void AppendAlert(ref FixedString512Bytes alerts, in FixedString64Bytes label, in RegistryHealth health)
        {
            if (alerts.Length > 0)
            {
                alerts.Append(" | ");
            }
            else
            {
                alerts.Append("Alerts: ");
            }

            alerts.Append(label);
            alerts.Append(": ");
            alerts.Append(GetHealthShortLabel(health.HealthLevel));

            if (health.StaleEntryCount > 0)
            {
                alerts.Append(" stale ");
                alerts.Append(health.StaleEntryCount);
                if (health.TotalEntryCount > 0)
                {
                    alerts.Append("/");
                    alerts.Append(health.TotalEntryCount);
                }
            }

            if (health.SpatialVersionDelta > 0)
            {
                alerts.Append(" Δsp=");
                alerts.Append(health.SpatialVersionDelta);
            }

            if (health.TicksSinceLastUpdate > 0)
            {
                alerts.Append(" Δt=");
                alerts.Append(health.TicksSinceLastUpdate);
            }

            if (health.DirectoryVersionDelta > 0)
            {
                alerts.Append(" Δdir=");
                alerts.Append(health.DirectoryVersionDelta);
            }
        }

        private static FixedString512Bytes BuildContinuityAlerts(DynamicBuffer<RegistryContinuityAlert> alerts, uint spatialVersion)
        {
            var text = new FixedString512Bytes();
            if (alerts.Length == 0)
            {
                return text;
            }

            text.Append("Continuity v");
            text.Append(spatialVersion);

            for (var i = 0; i < alerts.Length; i++)
            {
                var alert = alerts[i];
                text.Append(" | ");
                text.Append(alert.Label);
                text.Append(": ");
                text.Append(alert.Status == RegistryContinuityStatus.Failure ? "fail" : "warn");

                if (alert.Delta > 0)
                {
                    text.Append(" Δ=");
                    text.Append(alert.Delta);
                }

                if (alert.RegistrySpatialVersion > 0)
                {
                    text.Append(" reg=");
                    text.Append(alert.RegistrySpatialVersion);
                }

                if ((alert.Flags & RegistryHealthFlags.SpatialContinuityMissing) != 0)
                {
                    text.Append(" missing");
                }
                if ((alert.Flags & RegistryHealthFlags.DefinitionMismatch) != 0)
                {
                    text.Append(" def");
                }
            }

            return text;
        }

        private static FixedString512Bytes BuildHealthHeadline(int healthyCount, int warningCount, int criticalCount, RegistryHealthLevel worstLevel)
        {
            var text = new FixedString512Bytes();
            text.Append("Worst=");
            text.Append(GetHealthShortLabel(worstLevel));
            text.Append(" | ok=");
            text.Append(healthyCount);
            text.Append(" warn=");
            text.Append(warningCount);
            text.Append(" crit=");
            text.Append(criticalCount);
            return text;
        }

        private static FixedString32Bytes GetHealthShortLabel(RegistryHealthLevel level)
        {
            var label = new FixedString32Bytes();
            switch (level)
            {
                case RegistryHealthLevel.Healthy:
                    label.Append("ok");
                    break;
                case RegistryHealthLevel.Warning:
                    label.Append("warn");
                    break;
                case RegistryHealthLevel.Critical:
                    label.Append("crit");
                    break;
                case RegistryHealthLevel.Failure:
                    label.Append("fail");
                    break;
                default:
                    label.Append((int)level);
                    break;
            }

            return label;
        }

        private void UpdateMobilityDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.MobilityWaypointCount = 0;
            debugData.MobilityHighwayCount = 0;
            debugData.MobilityGatewayCount = 0;
            debugData.MobilityDisabledWaypoints = 0;
            debugData.MobilityBlockedHighways = 0;
            debugData.MobilityOfflineGateways = 0;
            debugData.MobilityStateText = default;

            if (!SystemAPI.TryGetSingletonEntity<MobilityNetwork>(out var networkEntity))
            {
                return;
            }

            var network = SystemAPI.GetComponentRO<MobilityNetwork>(networkEntity).ValueRO;
            var waypoints = state.EntityManager.HasBuffer<MobilityWaypointEntry>(networkEntity)
                ? state.EntityManager.GetBuffer<MobilityWaypointEntry>(networkEntity)
                : default;
            var highways = state.EntityManager.HasBuffer<MobilityHighwayEntry>(networkEntity)
                ? state.EntityManager.GetBuffer<MobilityHighwayEntry>(networkEntity)
                : default;
            var gateways = state.EntityManager.HasBuffer<MobilityGatewayEntry>(networkEntity)
                ? state.EntityManager.GetBuffer<MobilityGatewayEntry>(networkEntity)
                : default;

            debugData.MobilityWaypointCount = network.WaypointCount;
            debugData.MobilityHighwayCount = network.HighwayCount;
            debugData.MobilityGatewayCount = network.GatewayCount;

            if (waypoints.IsCreated)
            {
                for (int i = 0; i < waypoints.Length; i++)
                {
                    if ((waypoints[i].Flags & (byte)WaypointFlags.Disabled) != 0)
                    {
                        debugData.MobilityDisabledWaypoints++;
                    }
                }
            }

            if (highways.IsCreated)
            {
                for (int i = 0; i < highways.Length; i++)
                {
                    if ((highways[i].Flags & (byte)HighwayFlags.Blocked) != 0)
                    {
                        debugData.MobilityBlockedHighways++;
                    }
                }
            }

            if (gateways.IsCreated)
            {
                for (int i = 0; i < gateways.Length; i++)
                {
                    if ((gateways[i].Flags & (byte)GatewayFlags.Offline) != 0)
                    {
                        debugData.MobilityOfflineGateways++;
                    }
                }
            }

            var text = new FixedString128Bytes();
            text.Append("Mobility wp=");
            text.Append(network.WaypointCount);
            text.Append(" hw=");
            text.Append(network.HighwayCount);
            if (network.GatewayCount > 0)
            {
                text.Append(" gw=");
                text.Append(network.GatewayCount);
            }
            if (debugData.MobilityDisabledWaypoints > 0 || debugData.MobilityBlockedHighways > 0 || debugData.MobilityOfflineGateways > 0)
            {
                text.Append(" blocked=");
                text.Append(debugData.MobilityBlockedHighways);
                text.Append(" off=");
                text.Append(debugData.MobilityOfflineGateways);
                text.Append(" dis=");
                text.Append(debugData.MobilityDisabledWaypoints);
            }
            debugData.MobilityStateText = text;
        }

        private void UpdateBatchInventoryDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.BatchInventoryBatchCount = 0;
            debugData.BatchInventoryEntityCount = 0;
            debugData.BatchInventoryTotalUnits = 0f;
            debugData.BatchInventorySpoiledUnits = 0f;
            debugData.BatchInventoryMaxPrice = 0f;
            debugData.BatchInventoryAvgPrice = 0f;
            debugData.BatchInventoryStateText = default;
            debugData.TradeOpportunityCount = 0;
            debugData.TradeOpportunityVersion = 0;
            debugData.TradeOpportunityStateText = default;

            foreach (var (inventory, batches, priceState) in SystemAPI.Query<RefRO<BatchInventory>, DynamicBuffer<InventoryBatch>, RefRO<BatchPricingState>>())
            {
                debugData.BatchInventoryEntityCount++;
                debugData.BatchInventoryBatchCount += batches.Length;
                debugData.BatchInventoryTotalUnits += inventory.ValueRO.TotalUnits;
                debugData.BatchInventorySpoiledUnits += inventory.ValueRO.SpoiledUnits;
                debugData.BatchInventoryMaxPrice = math.max(debugData.BatchInventoryMaxPrice, priceState.ValueRO.LastPriceMultiplier);
                debugData.BatchInventoryAvgPrice += priceState.ValueRO.LastPriceMultiplier;
            }

            if (debugData.BatchInventoryBatchCount > 0)
            {
                if (debugData.BatchInventoryEntityCount > 0)
                {
                    debugData.BatchInventoryAvgPrice /= debugData.BatchInventoryEntityCount;
                }
                var text = new FixedString128Bytes();
                text.Append("BatchInv ");
                text.Append(debugData.BatchInventoryBatchCount);
                text.Append(" tot=");
                text.Append(math.round(debugData.BatchInventoryTotalUnits * 100f) / 100f);
                if (debugData.BatchInventorySpoiledUnits > 0f)
                {
                    text.Append(" spoil=");
                    text.Append(math.round(debugData.BatchInventorySpoiledUnits * 100f) / 100f);
                }
                if (debugData.BatchInventoryMaxPrice > 0f)
                {
                    text.Append(" price=");
                    text.Append(math.round(debugData.BatchInventoryAvgPrice * 100f) / 100f);
                    text.Append("/");
                    text.Append(math.round(debugData.BatchInventoryMaxPrice * 100f) / 100f);
                }
                debugData.BatchInventoryStateText = text;
            }
        }

        private EntityQuery _tradeOpportunityQuery;
        private EntityQuery _logisticsRegistryQuery;

        private void EnsureQueries(ref SystemState state)
        {
            if (_tradeOpportunityQuery == default)
            {
                _tradeOpportunityQuery = state.GetEntityQuery(ComponentType.ReadOnly<TradeOpportunityState>());
            }

            if (_logisticsRegistryQuery == default)
            {
                _logisticsRegistryQuery = state.GetEntityQuery(ComponentType.ReadOnly<LogisticsRequestRegistry>());
            }
        }

        private void UpdateTradeOpportunityDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            EnsureQueries(ref state);

            debugData.TradeOpportunityCount = 0;
            debugData.TradeOpportunityVersion = 0;
            debugData.TradeOpportunityStateText = default;

            if (_tradeOpportunityQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var tradeState = _tradeOpportunityQuery.GetSingleton<TradeOpportunityState>();

            debugData.TradeOpportunityCount = tradeState.OpportunityCount;
            debugData.TradeOpportunityVersion = tradeState.Version;

            var text = new FixedString128Bytes();
            text.Append("Trade opps=");
            text.Append(tradeState.OpportunityCount);
            text.Append(" v");
            text.Append(tradeState.Version);
            debugData.TradeOpportunityStateText = text;
        }

        private void UpdateLogisticsDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            EnsureQueries(ref state);

            debugData.LogisticsRequestCount = 0;
            debugData.LogisticsPending = 0;
            debugData.LogisticsInProgress = 0;
            debugData.LogisticsCritical = 0;

            if (_logisticsRegistryQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var registry = _logisticsRegistryQuery.GetSingleton<LogisticsRequestRegistry>();

            debugData.LogisticsRequestCount = registry.TotalRequests;
            debugData.LogisticsPending = registry.PendingRequests;
            debugData.LogisticsInProgress = registry.InProgressRequests;
            debugData.LogisticsCritical = registry.CriticalRequests;
        }

        private void UpdateComplianceDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.ComplianceNominal = 0;
            debugData.ComplianceWarning = 0;
            debugData.ComplianceBreach = 0;
            debugData.ComplianceStateText = default;

            foreach (var compliance in SystemAPI.Query<RefRO<CrewCompliance>>())
            {
                switch (compliance.ValueRO.Status)
                {
                    case ComplianceStatus.Nominal:
                        debugData.ComplianceNominal++;
                        break;
                    case ComplianceStatus.Warning:
                        debugData.ComplianceWarning++;
                        break;
                    case ComplianceStatus.Breach:
                        debugData.ComplianceBreach++;
                        break;
                }
            }

            if (debugData.ComplianceNominal + debugData.ComplianceWarning + debugData.ComplianceBreach > 0)
            {
                var text = new FixedString128Bytes();
                text.Append("Compliance ok=");
                text.Append(debugData.ComplianceNominal);
                text.Append(" warn=");
                text.Append(debugData.ComplianceWarning);
                text.Append(" breach=");
                text.Append(debugData.ComplianceBreach);
                debugData.ComplianceStateText = text;
            }
        }

        private void UpdateOrderAndSignalDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.OrderCount = 0;
            debugData.OrderPendingCount = 0;
            debugData.OrderInProgressCount = 0;
            debugData.OrderCompletedCount = 0;
            debugData.OrderFailedCount = 0;
            debugData.OrderEventsBuffered = 0;
            debugData.OrderEventVersion = 0;
            debugData.OrderStateText = default;
            debugData.SignalCount = 0;
            debugData.SignalDroppedCount = 0;
            debugData.SignalVersion = 0;
            debugData.SignalStateText = default;

            foreach (var order in SystemAPI.Query<RefRO<Order>>())
            {
                debugData.OrderCount++;
                switch (order.ValueRO.Status)
                {
                    case OrderStatus.Pending:
                        debugData.OrderPendingCount++;
                        break;
                    case OrderStatus.InProgress:
                        debugData.OrderInProgressCount++;
                        break;
                    case OrderStatus.Completed:
                        debugData.OrderCompletedCount++;
                        break;
                    case OrderStatus.Failed:
                        debugData.OrderFailedCount++;
                        break;
                }
            }

            if (SystemAPI.TryGetSingletonEntity<OrderEventStream>(out var streamEntity))
            {
                var stream = SystemAPI.GetComponentRO<OrderEventStream>(streamEntity).ValueRO;
                debugData.OrderEventsBuffered = stream.EventCount;
                debugData.OrderEventVersion = stream.Version;
                if (state.EntityManager.HasBuffer<OrderEvent>(streamEntity))
                {
                    var events = state.EntityManager.GetBuffer<OrderEvent>(streamEntity);
                    if (events.Length > 0)
                    {
                        var last = events[events.Length - 1];
                        var text = new FixedString128Bytes();
                        text.Append("Orders ");
                        text.Append(debugData.OrderCount);
                        text.Append(" v");
                        text.Append(stream.Version);
                        text.Append(" last ");
                        text.Append(last.OrderType);
                        text.Append(" ");
                        AppendOrderEventLabel(ref text, last.EventType);
                        debugData.OrderStateText = text;
                    }
                }
            }

            if (SystemAPI.TryGetSingletonEntity<SignalBus>(out var busEntity))
            {
                var bus = SystemAPI.GetComponentRO<SignalBus>(busEntity).ValueRO;
                debugData.SignalCount = bus.PendingCount;
                debugData.SignalDroppedCount = bus.DroppedCount;
                debugData.SignalVersion = bus.Version;

                if (state.EntityManager.HasBuffer<SignalEvent>(busEntity))
                {
                    var signals = state.EntityManager.GetBuffer<SignalEvent>(busEntity);
                    var text = new FixedString128Bytes();
                    text.Append("Signals ");
                    text.Append(signals.Length);
                    text.Append(" v");
                    text.Append(bus.Version);
                    if (signals.Length > 0)
                    {
                        var last = signals[signals.Length - 1];
                        text.Append(" ");
                        text.Append(last.Channel);
                        if (last.Payload.Length > 0)
                        {
                            text.Append("=");
                            text.Append(last.Payload);
                        }
                    }
                    debugData.SignalStateText = text;
                }
            }
        }

        private static void AppendOrderEventLabel(ref FixedString128Bytes text, OrderEventType eventType)
        {
            switch (eventType)
            {
                case OrderEventType.Started:
                    text.Append("start");
                    break;
                case OrderEventType.Completed:
                    text.Append("done");
                    break;
                case OrderEventType.Failed:
                    text.Append("fail");
                    break;
                default:
                    text.Append("evt");
                    break;
            }
        }

        private void UpdateSpawnerDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.SpawnerTotal = 0;
            debugData.SpawnerReady = 0;
            debugData.SpawnerCooling = 0;
            debugData.SpawnerDisabled = 0;
            debugData.SpawnerAttempted = 0;
            debugData.SpawnerSpawned = 0;
            debugData.SpawnerFailed = 0;
            debugData.SpawnerTelemetryTick = 0;
            debugData.SpawnerCatalogVersion = 0;
            debugData.SpawnerStateText = default;

            if (!SystemAPI.TryGetSingleton(out SpawnerTelemetry telemetry))
            {
                return;
            }

            debugData.SpawnerTotal = telemetry.TotalSpawners;
            debugData.SpawnerReady = telemetry.ReadySpawners;
            debugData.SpawnerCooling = telemetry.CoolingSpawners;
            debugData.SpawnerDisabled = telemetry.DisabledSpawners;
            debugData.SpawnerAttempted = telemetry.SpawnAttempts;
            debugData.SpawnerSpawned = telemetry.Spawned;
            debugData.SpawnerFailed = telemetry.SpawnFailures;
            debugData.SpawnerTelemetryTick = telemetry.LastUpdateTick;
            debugData.SpawnerCatalogVersion = telemetry.CatalogVersion;

            var text = new FixedString128Bytes();
            text.Append("Spawners ");
            text.Append(telemetry.TotalSpawners);
            text.Append(" ready=");
            text.Append(telemetry.ReadySpawners);
            text.Append(" attempts=");
            text.Append(telemetry.SpawnAttempts);
            text.Append(" ok=");
            text.Append(telemetry.Spawned);
            text.Append("/");
            text.Append(telemetry.SpawnFailures);
            debugData.SpawnerStateText = text;
        }

        private void UpdatePoolingDiagnostics(ref DebugDisplayData debugData)
        {
            debugData.PoolingActive = NxPoolingRuntime.IsInitialised;
            debugData.PoolingSnapshot = NxPoolingRuntime.GatherDiagnostics();
            debugData.PresentationActiveEffects = 0;
            debugData.PresentationActiveCompanions = 0;
            debugData.PresentationPoolCount = 0;
            debugData.PresentationReusedCount = 0;
            debugData.PresentationFailedCount = 0;
            debugData.PresentationPoolStateText = default;

            var text = new FixedString128Bytes();

            if (!debugData.PoolingActive)
            {
                text.Append("Pooling: inactive");
                debugData.PoolingStateText = text;
                return;
            }

            ref var pooling = ref debugData.PoolingSnapshot;

            text.Append("ECB ");
            text.Append(pooling.CommandBuffersBorrowed);
            text.Append("/");
            text.Append(pooling.CommandBuffersBorrowed + pooling.CommandBuffersAvailable);

            text.Append(" | NLists ");
            text.Append(pooling.NativeListsBorrowed);
            text.Append("/");
            text.Append(pooling.NativeListsBorrowed + pooling.NativeListsAvailable);

            text.Append(" | NQueues ");
            text.Append(pooling.NativeQueuesBorrowed);
            text.Append("/");
            text.Append(pooling.NativeQueuesBorrowed + pooling.NativeQueuesAvailable);

            var bridge = PresentationBridgeLocator.TryResolve();
            if (bridge != null)
            {
                var stats = bridge.Stats;
                debugData.PresentationActiveEffects = stats.ActiveEffects;
                debugData.PresentationActiveCompanions = stats.ActiveCompanions;
                debugData.PresentationPoolCount = stats.PooledInstances;
                debugData.PresentationReusedCount = stats.ReusedFromPool;
                debugData.PresentationFailedCount = stats.FailedPlayback + stats.FailedReleases;

                var presentationText = new FixedString128Bytes();
                presentationText.Append("fx=");
                presentationText.Append(stats.ActiveEffects);
                presentationText.Append(" comp=");
                presentationText.Append(stats.ActiveCompanions);
                presentationText.Append(" pool=");
                presentationText.Append(stats.PooledInstances);

                if (stats.ReusedFromPool > 0)
                {
                    presentationText.Append(" reuse=");
                    presentationText.Append(stats.ReusedFromPool);
                }

                if (stats.FailedPlayback + stats.FailedReleases > 0)
                {
                    presentationText.Append(" fail=");
                    presentationText.Append(stats.FailedPlayback + stats.FailedReleases);
                }

                debugData.PresentationPoolStateText = presentationText;
                text.Append(" | Bridge ");
                text.Append(presentationText);
            }

            debugData.PoolingStateText = text;
        }

        private void UpdateSpatialDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.SpatialCellCount = 0;
            debugData.SpatialIndexedEntityCount = 0;
            debugData.SpatialVersion = 0;
            debugData.SpatialLastUpdateTick = 0;
            debugData.SpatialStateText = default;
            debugData.SpatialDirtyAddCount = 0;
            debugData.SpatialDirtyUpdateCount = 0;
            debugData.SpatialDirtyRemoveCount = 0;
            debugData.SpatialLastRebuildMilliseconds = 0f;
            debugData.SpatialLastStrategy = SpatialGridRebuildStrategy.None;

            if (!SystemAPI.TryGetSingletonEntity<SpatialGridConfig>(out var gridEntity))
            {
                return;
            }

            var config = SystemAPI.GetComponentRO<SpatialGridConfig>(gridEntity).ValueRO;
            var gridState = SystemAPI.GetComponentRO<SpatialGridState>(gridEntity).ValueRO;

            debugData.SpatialCellCount = config.CellCount;
            debugData.SpatialIndexedEntityCount = gridState.TotalEntries;
            debugData.SpatialVersion = gridState.Version;
            debugData.SpatialLastUpdateTick = gridState.LastUpdateTick;
            debugData.SpatialDirtyAddCount = gridState.DirtyAddCount;
            debugData.SpatialDirtyUpdateCount = gridState.DirtyUpdateCount;
            debugData.SpatialDirtyRemoveCount = gridState.DirtyRemoveCount;
            debugData.SpatialLastRebuildMilliseconds = gridState.LastRebuildMilliseconds;
            debugData.SpatialLastStrategy = gridState.LastStrategy;

            var text = new FixedString128Bytes();
            text.Append("Spatial Cells: ");
            text.Append(config.CellCount);
            text.Append(" | Entries: ");
            text.Append(gridState.TotalEntries);
            text.Append(" | Version: ");
            text.Append(gridState.Version);
            text.Append(" @ Tick ");
            text.Append(gridState.LastUpdateTick);

            if (config.CellCount > 0)
            {
                var average = (float)gridState.TotalEntries / config.CellCount;
                text.Append(" | Avg/Cell: ");
                var rounded = math.round(average * 100f) / 100f;
                text.Append(rounded);
            }

            text.Append(" | Dirty +/");
            text.Append(gridState.DirtyAddCount);
            text.Append(",~/");
            text.Append(gridState.DirtyUpdateCount);
            text.Append(",-/");
            text.Append(gridState.DirtyRemoveCount);

            text.Append(" | Strategy: ");
            text.Append(gridState.LastStrategy.ToString());

            if (gridState.LastRebuildMilliseconds > 0f)
            {
                var rebuildRounded = math.round(gridState.LastRebuildMilliseconds * 100f) / 100f;
                text.Append(" | RebuildMs: ");
                text.Append(rebuildRounded);
            }

            debugData.SpatialStateText = text;
        }

        private void UpdateSunlightDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.SunlightStateText = default;
            debugData.SunlightDirectAverage = 0f;
            debugData.SunlightAmbientAverage = 0f;
            debugData.SunlightIntensity = 0f;
            debugData.SunlightDirection = float3.zero;
            debugData.SunlightMaxOccluders = 0;
            debugData.SunlightLastUpdateTick = 0;

            if (_sunlightQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var sunlightEntity = _sunlightQuery.GetSingletonEntity();
            var sunlightGrid = SystemAPI.GetComponentRO<SunlightGrid>(sunlightEntity).ValueRO;

            debugData.SunlightIntensity = sunlightGrid.SunIntensity;
            debugData.SunlightDirection = sunlightGrid.SunDirection;
            debugData.SunlightLastUpdateTick = sunlightGrid.LastUpdateTick;

            float directSum = 0f;
            float ambientSum = 0f;
            ushort maxOccluders = 0;
            int sampleCount = 0;

            _sunlightRuntimeLookup.Update(ref state);

            if (_sunlightRuntimeLookup.TryGetBuffer(sunlightEntity, out var runtimeSamples) && runtimeSamples.Length > 0)
            {
                sampleCount = runtimeSamples.Length;
                for (var i = 0; i < runtimeSamples.Length; i++)
                {
                    var sample = runtimeSamples[i].Value;
                    directSum += sample.DirectLight;
                    ambientSum += sample.AmbientLight;
                    if (sample.OccluderCount > maxOccluders)
                    {
                        maxOccluders = sample.OccluderCount;
                    }
                }
            }
            else if (sunlightGrid.Blob.IsCreated)
            {
                ref var blobSamples = ref sunlightGrid.Blob.Value.Samples;
                sampleCount = blobSamples.Length;
                for (var i = 0; i < blobSamples.Length; i++)
                {
                    var sample = blobSamples[i];
                    directSum += sample.DirectLight;
                    ambientSum += sample.AmbientLight;
                    if (sample.OccluderCount > maxOccluders)
                    {
                        maxOccluders = sample.OccluderCount;
                    }
                }
            }

            if (sampleCount > 0)
            {
                debugData.SunlightDirectAverage = directSum / sampleCount;
                debugData.SunlightAmbientAverage = ambientSum / sampleCount;
                debugData.SunlightMaxOccluders = maxOccluders;
            }

            var directRounded = math.round(debugData.SunlightDirectAverage * 10f) * 0.1f;
            var ambientRounded = math.round(debugData.SunlightAmbientAverage * 10f) * 0.1f;
            var intensityRounded = math.round(debugData.SunlightIntensity * 100f) * 0.01f;
            var dirRounded = math.round(debugData.SunlightDirection * 100f) * 0.01f;

            var text = new FixedString128Bytes();
            text.Append("Sun ");
            text.Append(directRounded);
            text.Append("/");
            text.Append(ambientRounded);
            text.Append(" occ=");
            text.Append(debugData.SunlightMaxOccluders);
            text.Append(" I=");
            text.Append(intensityRounded);
            text.Append(" dir=");
            text.Append(dirRounded.x);
            text.Append(",");
            text.Append(dirRounded.y);
            text.Append(",");
            text.Append(dirRounded.z);
            debugData.SunlightStateText = text;
        }

        private void UpdateEnvironmentGridDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            // Initialize defaults
            debugData.MoistureStateText = default;
            debugData.MoistureAverage = 0f;
            debugData.MoistureMin = 0f;
            debugData.MoistureMax = 0f;
            debugData.MoistureLastUpdateTick = 0;
            debugData.MoistureLastTerrainVersion = 0;
            
            debugData.TemperatureStateText = default;
            debugData.TemperatureAverage = 0f;
            debugData.TemperatureMin = 0f;
            debugData.TemperatureMax = 0f;
            debugData.TemperatureLastUpdateTick = 0;
            debugData.TemperatureLastTerrainVersion = 0;
            
            debugData.WindStateText = default;
            debugData.WindAverageStrength = 0f;
            debugData.WindGlobalDirection = float2.zero;
            debugData.WindGlobalStrength = 0f;
            debugData.WindLastUpdateTick = 0;
            debugData.WindLastTerrainVersion = 0;
            
            debugData.BiomeStateText = default;
            debugData.BiomeUnknownCount = 0;
            debugData.BiomeTundraCount = 0;
            debugData.BiomeTaigaCount = 0;
            debugData.BiomeGrasslandCount = 0;
            debugData.BiomeForestCount = 0;
            debugData.BiomeDesertCount = 0;
            debugData.BiomeRainforestCount = 0;
            debugData.BiomeSavannaCount = 0;
            debugData.BiomeSwampCount = 0;
            debugData.BiomeLastUpdateTick = 0;
            debugData.BiomeLastTerrainVersion = 0;
            
            debugData.TerrainVersionText = default;
            debugData.TerrainVersion = 0;
            debugData.TerrainChangeEventCount = 0;

            // Update moisture grid diagnostics
            if (SystemAPI.TryGetSingletonEntity<PureDOTS.Environment.MoistureGrid>(out var moistureEntity))
            {
                var moistureGrid = SystemAPI.GetComponent<PureDOTS.Environment.MoistureGrid>(moistureEntity);
                debugData.MoistureLastUpdateTick = moistureGrid.LastUpdateTick;
                debugData.MoistureLastTerrainVersion = moistureGrid.LastTerrainVersion;
                
                if (state.EntityManager.HasBuffer<PureDOTS.Environment.MoistureGridRuntimeCell>(moistureEntity))
                {
                    var cells = state.EntityManager.GetBuffer<PureDOTS.Environment.MoistureGridRuntimeCell>(moistureEntity);
                    if (cells.Length > 0)
                    {
                        float sum = 0f;
                        float min = float.MaxValue;
                        float max = float.MinValue;
                        for (int i = 0; i < cells.Length; i++)
                        {
                            var moisture = cells[i].Moisture;
                            sum += moisture;
                            min = math.min(min, moisture);
                            max = math.max(max, moisture);
                        }
                        debugData.MoistureAverage = sum / cells.Length;
                        debugData.MoistureMin = min;
                        debugData.MoistureMax = max;
                        
                        var text = new FixedString128Bytes();
                        text.Append("Moisture avg=");
                        text.Append(math.round(debugData.MoistureAverage * 10f) * 0.1f);
                        text.Append(" min=");
                        text.Append(math.round(debugData.MoistureMin * 10f) * 0.1f);
                        text.Append(" max=");
                        text.Append(math.round(debugData.MoistureMax * 10f) * 0.1f);
                        text.Append(" TV=");
                        text.Append(debugData.MoistureLastTerrainVersion);
                        debugData.MoistureStateText = text;
                    }
                }
            }

            // Update temperature grid diagnostics
            if (SystemAPI.TryGetSingletonEntity<PureDOTS.Environment.TemperatureGrid>(out var tempEntity))
            {
                var tempGrid = SystemAPI.GetComponent<PureDOTS.Environment.TemperatureGrid>(tempEntity);
                debugData.TemperatureLastUpdateTick = tempGrid.LastUpdateTick;
                debugData.TemperatureLastTerrainVersion = tempGrid.LastTerrainVersion;
                
                if (tempGrid.IsCreated)
                {
                    ref var temps = ref tempGrid.Blob.Value.TemperatureCelsius;
                    if (temps.Length > 0)
                    {
                        float sum = 0f;
                        float min = float.MaxValue;
                        float max = float.MinValue;
                        for (int i = 0; i < temps.Length; i++)
                        {
                            var temp = temps[i];
                            sum += temp;
                            min = math.min(min, temp);
                            max = math.max(max, temp);
                        }
                        debugData.TemperatureAverage = sum / temps.Length;
                        debugData.TemperatureMin = min;
                        debugData.TemperatureMax = max;
                        
                        var text = new FixedString128Bytes();
                        text.Append("Temp avg=");
                        text.Append(math.round(debugData.TemperatureAverage * 10f) * 0.1f);
                        text.Append("°C min=");
                        text.Append(math.round(debugData.TemperatureMin * 10f) * 0.1f);
                        text.Append(" max=");
                        text.Append(math.round(debugData.TemperatureMax * 10f) * 0.1f);
                        text.Append(" TV=");
                        text.Append(debugData.TemperatureLastTerrainVersion);
                        debugData.TemperatureStateText = text;
                    }
                }
            }

            // Update wind field diagnostics
            if (SystemAPI.TryGetSingletonEntity<PureDOTS.Environment.WindField>(out var windEntity))
            {
                var windField = SystemAPI.GetComponent<PureDOTS.Environment.WindField>(windEntity);
                debugData.WindLastUpdateTick = windField.LastUpdateTick;
                debugData.WindLastTerrainVersion = windField.LastTerrainVersion;
                debugData.WindGlobalDirection = windField.GlobalWindDirection;
                debugData.WindGlobalStrength = windField.GlobalWindStrength;
                
                if (windField.IsCreated)
                {
                    ref var samples = ref windField.Blob.Value.Samples;
                    if (samples.Length > 0)
                    {
                        float strengthSum = 0f;
                        for (int i = 0; i < samples.Length; i++)
                        {
                            strengthSum += samples[i].Strength;
                        }
                        debugData.WindAverageStrength = strengthSum / samples.Length;
                        
                        var text = new FixedString128Bytes();
                        text.Append("Wind avg=");
                        text.Append(math.round(debugData.WindAverageStrength * 10f) * 0.1f);
                        text.Append("m/s global=");
                        text.Append(math.round(debugData.WindGlobalStrength * 10f) * 0.1f);
                        text.Append(" dir=(");
                        text.Append(math.round(debugData.WindGlobalDirection.x * 100f) * 0.01f);
                        text.Append(",");
                        text.Append(math.round(debugData.WindGlobalDirection.y * 100f) * 0.01f);
                        text.Append(") TV=");
                        text.Append(debugData.WindLastTerrainVersion);
                        debugData.WindStateText = text;
                    }
                }
            }

            // Update biome grid diagnostics
            if (SystemAPI.TryGetSingletonEntity<PureDOTS.Environment.BiomeGrid>(out var biomeEntity))
            {
                var biomeGrid = SystemAPI.GetComponent<PureDOTS.Environment.BiomeGrid>(biomeEntity);
                debugData.BiomeLastUpdateTick = biomeGrid.LastUpdateTick;
                debugData.BiomeLastTerrainVersion = biomeGrid.LastTerrainVersion;
                
                if (state.EntityManager.HasBuffer<PureDOTS.Environment.BiomeGridRuntimeCell>(biomeEntity))
                {
                    var cells = state.EntityManager.GetBuffer<PureDOTS.Environment.BiomeGridRuntimeCell>(biomeEntity);
                    for (int i = 0; i < cells.Length; i++)
                    {
                        var biome = cells[i].Value;
                        switch (biome)
                        {
                            case PureDOTS.Environment.BiomeType.Unknown:
                                debugData.BiomeUnknownCount++;
                                break;
                            case PureDOTS.Environment.BiomeType.Tundra:
                                debugData.BiomeTundraCount++;
                                break;
                            case PureDOTS.Environment.BiomeType.Taiga:
                                debugData.BiomeTaigaCount++;
                                break;
                            case PureDOTS.Environment.BiomeType.Grassland:
                                debugData.BiomeGrasslandCount++;
                                break;
                            case PureDOTS.Environment.BiomeType.Forest:
                                debugData.BiomeForestCount++;
                                break;
                            case PureDOTS.Environment.BiomeType.Desert:
                                debugData.BiomeDesertCount++;
                                break;
                            case PureDOTS.Environment.BiomeType.Rainforest:
                                debugData.BiomeRainforestCount++;
                                break;
                            case PureDOTS.Environment.BiomeType.Savanna:
                                debugData.BiomeSavannaCount++;
                                break;
                            case PureDOTS.Environment.BiomeType.Swamp:
                                debugData.BiomeSwampCount++;
                                break;
                        }
                    }
                    
                    var text = new FixedString128Bytes();
                    text.Append("Biomes F:");
                    text.Append(debugData.BiomeForestCount);
                    text.Append(" G:");
                    text.Append(debugData.BiomeGrasslandCount);
                    text.Append(" D:");
                    text.Append(debugData.BiomeDesertCount);
                    text.Append(" TV=");
                    text.Append(debugData.BiomeLastTerrainVersion);
                    debugData.BiomeStateText = text;
                }
            }

            // Update terrain version diagnostics
            if (SystemAPI.TryGetSingletonEntity<PureDOTS.Environment.TerrainVersion>(out var terrainVersionEntity))
            {
                var terrainVersion = SystemAPI.GetComponent<PureDOTS.Environment.TerrainVersion>(terrainVersionEntity);
                debugData.TerrainVersion = terrainVersion.Value;
                
                if (state.EntityManager.HasBuffer<PureDOTS.Environment.TerrainChangeEvent>(terrainVersionEntity))
                {
                    var events = state.EntityManager.GetBuffer<PureDOTS.Environment.TerrainChangeEvent>(terrainVersionEntity);
                    debugData.TerrainChangeEventCount = events.Length;
                }
                
                var text = new FixedString128Bytes();
                text.Append("Terrain v=");
                text.Append(debugData.TerrainVersion);
                text.Append(" events=");
                text.Append(debugData.TerrainChangeEventCount);
                debugData.TerrainVersionText = text;
            }
        }

        private void UpdateStreamingDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.StreamingDesiredCount = 0;
            debugData.StreamingLoadedCount = 0;
            debugData.StreamingLoadingCount = 0;
            debugData.StreamingQueuedLoads = 0;
            debugData.StreamingQueuedUnloads = 0;
            debugData.StreamingPendingCommands = 0;
            debugData.StreamingActiveCooldowns = 0;
            debugData.StreamingFirstLoadTick = StreamingStatistics.TickUnset;
            debugData.StreamingFirstUnloadTick = StreamingStatistics.TickUnset;
            debugData.StreamingStateText = default;

            if (!SystemAPI.TryGetSingletonEntity<StreamingCoordinator>(out var coordinatorEntity))
            {
                return;
            }

            if (!state.EntityManager.HasComponent<StreamingStatistics>(coordinatorEntity))
            {
                return;
            }

            var stats = SystemAPI.GetComponentRO<StreamingStatistics>(coordinatorEntity).ValueRO;

            debugData.StreamingDesiredCount = stats.DesiredCount;
            debugData.StreamingLoadedCount = stats.LoadedCount;
            debugData.StreamingLoadingCount = stats.LoadingCount;
            debugData.StreamingQueuedLoads = stats.QueuedLoads;
            debugData.StreamingQueuedUnloads = stats.QueuedUnloads;
            debugData.StreamingPendingCommands = stats.PendingCommands;
            debugData.StreamingActiveCooldowns = stats.ActiveCooldowns;
            debugData.StreamingFirstLoadTick = stats.FirstLoadTick;
            debugData.StreamingFirstUnloadTick = stats.FirstUnloadTick;

            var text = new FixedString128Bytes();
            text.Append("Streaming D:");
            text.Append(stats.DesiredCount);
            text.Append(" Ld:");
            text.Append(stats.LoadedCount);
            text.Append(" Lg:");
            text.Append(stats.LoadingCount);
            text.Append(" QL:");
            text.Append(stats.QueuedLoads);
            text.Append(" QU:");
            text.Append(stats.QueuedUnloads);
            text.Append(" P:");
            text.Append(stats.PendingCommands);
            text.Append(" CD:");
            text.Append(stats.ActiveCooldowns);

            if (stats.FirstLoadTick != StreamingStatistics.TickUnset)
            {
                text.Append(" | FirstLoad:");
                text.Append(stats.FirstLoadTick);
            }

            if (stats.FirstUnloadTick != StreamingStatistics.TickUnset)
            {
                text.Append(" | FirstUnload:");
                text.Append(stats.FirstUnloadTick);
            }

            debugData.StreamingStateText = text;
        }

        private void UpdateFrameTiming(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.FrameTimingText = default;
            debugData.FrameTimingSampleCount = 0;
            debugData.FrameTimingWorstDurationMs = 0f;
            debugData.FrameTimingWorstGroup = default;
            debugData.FrameTimingBudgetExceeded = false;
            debugData.AllocationStateText = default;
            debugData.GcCollectionsGeneration0 = 0;
            debugData.GcCollectionsGeneration1 = 0;
            debugData.GcCollectionsGeneration2 = 0;

            if (!SystemAPI.TryGetSingletonEntity<FrameTimingStream>(out var frameEntity))
            {
                return;
            }

            var samples = state.EntityManager.GetBuffer<FrameTimingSample>(frameEntity);
            var text = new FixedString128Bytes();

            for (int i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                if (sample.DurationMs <= 0f && sample.SystemCount == 0)
                {
                    continue;
                }

                if (debugData.FrameTimingSampleCount > 0)
                {
                    text.Append(" | ");
                }

                var label = FrameTimingRecorderSystem.GetGroupLabel(sample.Group);
                text.Append(label);
                text.Append(" ");
                var durationRounded = math.round(sample.DurationMs * 100f) / 100f;
                text.Append(durationRounded);
                text.Append("ms");

                if (sample.BudgetMs > 0f)
                {
                    text.Append("/");
                    text.Append(sample.BudgetMs);
                    text.Append("ms");
                }

                if ((sample.Flags & FrameTimingFlags.BudgetExceeded) != 0)
                {
                    text.Append("!");
                    debugData.FrameTimingBudgetExceeded = true;
                }

                if ((sample.Flags & FrameTimingFlags.CatchUp) != 0)
                {
                    text.Append("*");
                }

                debugData.FrameTimingSampleCount++;

                if (sample.DurationMs > debugData.FrameTimingWorstDurationMs)
                {
                    debugData.FrameTimingWorstDurationMs = sample.DurationMs;
                    debugData.FrameTimingWorstGroup = label;
                }
            }

            debugData.FrameTimingText = text;

            var allocation = SystemAPI.GetComponentRO<AllocationDiagnostics>(frameEntity).ValueRO;
            debugData.GcCollectionsGeneration0 = allocation.GcCollectionsGeneration0;
            debugData.GcCollectionsGeneration1 = allocation.GcCollectionsGeneration1;
            debugData.GcCollectionsGeneration2 = allocation.GcCollectionsGeneration2;

            var allocationText = new FixedString128Bytes();
            allocationText.Append("GC ");
            allocationText.Append(allocation.GcCollectionsGeneration0);
            allocationText.Append("/");
            allocationText.Append(allocation.GcCollectionsGeneration1);
            allocationText.Append("/");
            allocationText.Append(allocation.GcCollectionsGeneration2);
            allocationText.Append(" | Mem ");
            allocationText.Append(BytesToMegabytes(allocation.TotalAllocatedBytes));
            allocationText.Append("MB");
            allocationText.Append(" / Res ");
            allocationText.Append(BytesToMegabytes(allocation.TotalReservedBytes));
            allocationText.Append("MB");
            if (allocation.TotalUnusedReservedBytes > 0)
            {
                allocationText.Append(" (");
                allocationText.Append(BytesToMegabytes(allocation.TotalUnusedReservedBytes));
                allocationText.Append("MB unused)");
            }

            debugData.AllocationStateText = allocationText;
        }

        private void UpdateReplayDiagnostics(ref SystemState state, ref DebugDisplayData debugData)
        {
            debugData.ReplayEventCount = 0;
            debugData.ReplayStateText = default;

            if (!SystemAPI.TryGetSingletonEntity<ReplayCaptureStream>(out var replayEntity))
            {
                return;
            }

            var stream = SystemAPI.GetComponent<ReplayCaptureStream>(replayEntity);
            var events = state.EntityManager.GetBuffer<ReplayCaptureEvent>(replayEntity);
            debugData.ReplayEventCount = stream.EventCount;

            if (events.Length == 0)
            {
                if (stream.LastEventLabel.Length > 0)
                {
                    var idleText = new FixedString128Bytes();
                    idleText.Append("Replay last: ");
                    idleText.Append(stream.LastEventLabel);
                    debugData.ReplayStateText = idleText;
                }
                return;
            }

            var latest = events[events.Length - 1];
            var text = new FixedString128Bytes();
            text.Append("Replay ");
            text.Append(events.Length);
            text.Append(" @ ");
            text.Append(latest.Tick);
            text.Append(" ");
            text.Append(ReplayCaptureSystem.GetEventTypeLabel(latest.Type));
            if (latest.Label.Length > 0)
            {
                text.Append(" ");
                text.Append(latest.Label);
            }
            if (math.abs(latest.Value) > 0.0001f)
            {
                text.Append(" (");
                text.Append(latest.Value);
                text.Append(")");
            }

            debugData.ReplayStateText = text;
        }

        private static float BytesToMegabytes(long bytes)
        {
            return bytes <= 0 ? 0f : bytes / (1024f * 1024f);
        }

        private static int BytesToKilobytes(long bytes)
        {
            return bytes <= 0 ? 0 : (int)(bytes / 1024);
        }

        private void WriteTelemetrySnapshot(ref SystemState state, in DebugDisplayData debugData)
        {
            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                return;
            }

            var telemetry = SystemAPI.GetComponentRW<TelemetryStream>(telemetryEntity);
            telemetry.ValueRW.LastTick = debugData.CurrentTick;
            telemetry.ValueRW.Version++;

            if (!state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                state.EntityManager.AddBuffer<TelemetryMetric>(telemetryEntity);
            }

            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            buffer.Clear();

            FixedString64Bytes key;

            key = "tick.current";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CurrentTick, Unit = TelemetryMetricUnit.Count });

            key = "registry.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegisteredRegistryCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.entries";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegisteredEntryCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.definitions";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegistryDefinitionCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.health.worst";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegistryWorstHealthLevel, Unit = TelemetryMetricUnit.Count });

            key = "registry.health.warning";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegistryWarningCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.health.critical";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegistryCriticalCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.health.failure";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegistryFailureCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.continuity.warning";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegistryContinuityWarningCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.continuity.failure";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegistryContinuityFailureCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.continuity.version";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegistryContinuityVersion, Unit = TelemetryMetricUnit.Count });

            key = "registry.continuity.lasttick";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.RegistryContinuityLastCheckTick, Unit = TelemetryMetricUnit.Count });

            key = "registry.resource.spatial.resolved";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.ResourceSpatialResolved, Unit = TelemetryMetricUnit.Count });

            key = "registry.resource.spatial.fallback";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.ResourceSpatialFallback, Unit = TelemetryMetricUnit.Count });

            key = "registry.resource.spatial.unmapped";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.ResourceSpatialUnmapped, Unit = TelemetryMetricUnit.Count });

            key = "registry.storehouse.spatial.resolved";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StorehouseSpatialResolved, Unit = TelemetryMetricUnit.Count });

            key = "registry.storehouse.spatial.fallback";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StorehouseSpatialFallback, Unit = TelemetryMetricUnit.Count });

            key = "registry.storehouse.spatial.unmapped";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StorehouseSpatialUnmapped, Unit = TelemetryMetricUnit.Count });

            key = "mobility.waypoints";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.MobilityWaypointCount, Unit = TelemetryMetricUnit.Count });
            key = "mobility.highways";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.MobilityHighwayCount, Unit = TelemetryMetricUnit.Count });
            key = "mobility.gateways";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.MobilityGatewayCount, Unit = TelemetryMetricUnit.Count });
            key = "mobility.blocked";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.MobilityBlockedHighways, Unit = TelemetryMetricUnit.Count });
            key = "mobility.offline";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.MobilityOfflineGateways, Unit = TelemetryMetricUnit.Count });
            key = "mobility.disabled";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.MobilityDisabledWaypoints, Unit = TelemetryMetricUnit.Count });

            key = "batchinventory.batches";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BatchInventoryBatchCount, Unit = TelemetryMetricUnit.Count });
            key = "batchinventory.units";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BatchInventoryTotalUnits, Unit = TelemetryMetricUnit.Count });
            key = "batchinventory.spoiled";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BatchInventorySpoiledUnits, Unit = TelemetryMetricUnit.Count });
            key = "batchinventory.price.max";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BatchInventoryMaxPrice, Unit = TelemetryMetricUnit.None });
            key = "batchinventory.price.avg";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BatchInventoryAvgPrice, Unit = TelemetryMetricUnit.None });
            key = "trade.opportunities.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.TradeOpportunityCount, Unit = TelemetryMetricUnit.Count });
            key = "trade.opportunities.version";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.TradeOpportunityVersion, Unit = TelemetryMetricUnit.Count });
            key = "trade.logistics.total";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.LogisticsRequestCount, Unit = TelemetryMetricUnit.Count });
            key = "trade.logistics.pending";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.LogisticsPending, Unit = TelemetryMetricUnit.Count });
            key = "trade.logistics.inprogress";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.LogisticsInProgress, Unit = TelemetryMetricUnit.Count });
            key = "trade.logistics.critical";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.LogisticsCritical, Unit = TelemetryMetricUnit.Count });

            key = "orders.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.OrderCount, Unit = TelemetryMetricUnit.Count });
            key = "orders.pending";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.OrderPendingCount, Unit = TelemetryMetricUnit.Count });
            key = "orders.inprogress";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.OrderInProgressCount, Unit = TelemetryMetricUnit.Count });
            key = "orders.completed";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.OrderCompletedCount, Unit = TelemetryMetricUnit.Count });
            key = "orders.failed";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.OrderFailedCount, Unit = TelemetryMetricUnit.Count });
            key = "orders.events";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.OrderEventsBuffered, Unit = TelemetryMetricUnit.Count });

            key = "signals.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SignalCount, Unit = TelemetryMetricUnit.Count });
            key = "signals.dropped";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SignalDroppedCount, Unit = TelemetryMetricUnit.Count });

            key = "spawners.total";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpawnerTotal, Unit = TelemetryMetricUnit.Count });
            key = "spawners.ready";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpawnerReady, Unit = TelemetryMetricUnit.Count });
            key = "spawners.cooling";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpawnerCooling, Unit = TelemetryMetricUnit.Count });
            key = "spawners.attempts";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpawnerAttempted, Unit = TelemetryMetricUnit.Count });
            key = "spawners.spawned";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpawnerSpawned, Unit = TelemetryMetricUnit.Count });
            key = "spawners.failed";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpawnerFailed, Unit = TelemetryMetricUnit.Count });

            key = "compliance.nominal";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.ComplianceNominal, Unit = TelemetryMetricUnit.Count });
            key = "compliance.warning";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.ComplianceWarning, Unit = TelemetryMetricUnit.Count });
            key = "compliance.breach";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.ComplianceBreach, Unit = TelemetryMetricUnit.Count });

            key = "registry.band.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BandRegistryCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.band.entries";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BandEntryCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.band.members";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BandTotalMembers, Unit = TelemetryMetricUnit.Count });

            key = "registry.band.spatial.resolved";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BandSpatialResolved, Unit = TelemetryMetricUnit.Count });

            key = "registry.band.spatial.fallback";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BandSpatialFallback, Unit = TelemetryMetricUnit.Count });

            key = "registry.band.spatial.unmapped";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.BandSpatialUnmapped, Unit = TelemetryMetricUnit.Count });

            key = "registry.faction.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.FactionRegistryCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.faction.entries";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.FactionEntryCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.faction.territory";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.TotalFactionTerritoryCells, Unit = TelemetryMetricUnit.Count });

            key = "registry.faction.resources";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.TotalFactionResources, Unit = TelemetryMetricUnit.Count });

            key = "registry.hazard.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.ClimateHazardActiveCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.hazard.intensity";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.ClimateHazardGlobalIntensity, Unit = TelemetryMetricUnit.Ratio });

            key = "registry.area.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.AreaEffectActiveCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.area.strength";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.AreaEffectAverageStrength, Unit = TelemetryMetricUnit.Count });

            key = "registry.culture.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CultureRegistryCount, Unit = TelemetryMetricUnit.Count });

            key = "registry.culture.alignment";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CultureGlobalAlignmentScore, Unit = TelemetryMetricUnit.Ratio });

            key = "villagers.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.VillagerCount, Unit = TelemetryMetricUnit.Count });

            key = "resources.total";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.TotalResourcesStored, Unit = TelemetryMetricUnit.Count });

            key = "camera.distance";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CameraRigDistance, Unit = TelemetryMetricUnit.Count });
            key = "camera.yaw";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CameraRigYaw, Unit = TelemetryMetricUnit.Count });
            key = "camera.pitch";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CameraRigPitch, Unit = TelemetryMetricUnit.Count });
            key = "camera.shake";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CameraRigShake, Unit = TelemetryMetricUnit.Ratio });

            key = "presentation.active.effects";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.PresentationActiveEffects, Unit = TelemetryMetricUnit.Count });
            key = "presentation.active.companions";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.PresentationActiveCompanions, Unit = TelemetryMetricUnit.Count });
            key = "presentation.pool.count";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.PresentationPoolCount, Unit = TelemetryMetricUnit.Count });
            key = "presentation.pool.reused";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.PresentationReusedCount, Unit = TelemetryMetricUnit.Count });
            key = "presentation.pool.failed";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.PresentationFailedCount, Unit = TelemetryMetricUnit.Count });
            key = "presentation.camera.active";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CameraRigActive, Unit = TelemetryMetricUnit.Count });
            key = "presentation.camera.distance";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CameraRigDistance, Unit = TelemetryMetricUnit.Count });
            key = "presentation.camera.yaw";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CameraRigYaw, Unit = TelemetryMetricUnit.Count });
            key = "presentation.camera.pitch";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.CameraRigPitch, Unit = TelemetryMetricUnit.Count });

            key = "spatial.cells";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpatialCellCount, Unit = TelemetryMetricUnit.Count });

            key = "spatial.entries";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpatialIndexedEntityCount, Unit = TelemetryMetricUnit.Count });

            key = "spatial.version";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpatialVersion, Unit = TelemetryMetricUnit.Count });

            key = "spatial.last.tick";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SpatialLastUpdateTick, Unit = TelemetryMetricUnit.Count });

            key = "streaming.desired";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StreamingDesiredCount, Unit = TelemetryMetricUnit.Count });

            key = "streaming.loaded";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StreamingLoadedCount, Unit = TelemetryMetricUnit.Count });

            key = "streaming.loading";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StreamingLoadingCount, Unit = TelemetryMetricUnit.Count });

            key = "streaming.queued.loads";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StreamingQueuedLoads, Unit = TelemetryMetricUnit.Count });

            key = "streaming.queued.unloads";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StreamingQueuedUnloads, Unit = TelemetryMetricUnit.Count });

            key = "streaming.pending.commands";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StreamingPendingCommands, Unit = TelemetryMetricUnit.Count });

            key = "streaming.cooldowns.active";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StreamingActiveCooldowns, Unit = TelemetryMetricUnit.Count });

            if (debugData.StreamingFirstLoadTick != StreamingStatistics.TickUnset)
            {
                key = "streaming.first.load.tick";
                buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StreamingFirstLoadTick, Unit = TelemetryMetricUnit.Count });
            }

            if (debugData.StreamingFirstUnloadTick != StreamingStatistics.TickUnset)
            {
                key = "streaming.first.unload.tick";
                buffer.Add(new TelemetryMetric { Key = key, Value = debugData.StreamingFirstUnloadTick, Unit = TelemetryMetricUnit.Count });
            }

            key = "sunlight.direct.avg";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SunlightDirectAverage, Unit = TelemetryMetricUnit.Count });

            key = "sunlight.ambient.avg";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SunlightAmbientAverage, Unit = TelemetryMetricUnit.Count });

            key = "sunlight.intensity";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SunlightIntensity, Unit = TelemetryMetricUnit.Count });

            key = "sunlight.occluders.max";
            buffer.Add(new TelemetryMetric { Key = key, Value = debugData.SunlightMaxOccluders, Unit = TelemetryMetricUnit.Count });

            if (debugData.PoolingActive)
            {
                key = "pooling.ecb.borrowed";
                buffer.Add(new TelemetryMetric { Key = key, Value = debugData.PoolingSnapshot.CommandBuffersBorrowed, Unit = TelemetryMetricUnit.Count });

                key = "pooling.nativelist.borrowed";
                buffer.Add(new TelemetryMetric { Key = key, Value = debugData.PoolingSnapshot.NativeListsBorrowed, Unit = TelemetryMetricUnit.Count });

                key = "pooling.nativequeue.borrowed";
                buffer.Add(new TelemetryMetric { Key = key, Value = debugData.PoolingSnapshot.NativeQueuesBorrowed, Unit = TelemetryMetricUnit.Count });
            }

            if (SystemAPI.TryGetSingletonEntity<FrameTimingStream>(out var frameEntity))
            {
                var samples = state.EntityManager.GetBuffer<FrameTimingSample>(frameEntity);
                for (int i = 0; i < samples.Length; i++)
                {
                    var sample = samples[i];
                    key = FrameTimingUtility.GetMetricKey(sample.Group);
                    buffer.Add(new TelemetryMetric
                    {
                        Key = key,
                        Value = sample.DurationMs,
                        Unit = TelemetryMetricUnit.DurationMilliseconds
                    });
                }

                var allocation = SystemAPI.GetComponent<AllocationDiagnostics>(frameEntity);

                key = "memory.allocated.bytes";
                buffer.Add(new TelemetryMetric { Key = key, Value = (float)allocation.TotalAllocatedBytes, Unit = TelemetryMetricUnit.Bytes });

                key = "memory.reserved.bytes";
                buffer.Add(new TelemetryMetric { Key = key, Value = (float)allocation.TotalReservedBytes, Unit = TelemetryMetricUnit.Bytes });

                key = "memory.unused.bytes";
                buffer.Add(new TelemetryMetric { Key = key, Value = (float)allocation.TotalUnusedReservedBytes, Unit = TelemetryMetricUnit.Bytes });

                key = "gc.gen0.collections";
                buffer.Add(new TelemetryMetric { Key = key, Value = debugData.GcCollectionsGeneration0, Unit = TelemetryMetricUnit.Count });

                key = "gc.gen1.collections";
                buffer.Add(new TelemetryMetric { Key = key, Value = debugData.GcCollectionsGeneration1, Unit = TelemetryMetricUnit.Count });

                key = "gc.gen2.collections";
                buffer.Add(new TelemetryMetric { Key = key, Value = debugData.GcCollectionsGeneration2, Unit = TelemetryMetricUnit.Count });
            }

            if (SystemAPI.TryGetSingletonEntity<ReplayCaptureStream>(out var replayEntity))
            {
                var replayStream = SystemAPI.GetComponent<ReplayCaptureStream>(replayEntity);
                key = "replay.events";
                buffer.Add(new TelemetryMetric { Key = key, Value = replayStream.EventCount, Unit = TelemetryMetricUnit.Count });
            }
        }

#if DEVTOOLS_ENABLED
        private void UpdateSpawnTelemetry(ref DebugDisplayData debugData, ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<SpawnTelemetry>())
            {
                return;
            }

            var spawnTelemetry = SystemAPI.GetSingleton<SpawnTelemetry>();
            debugData.SpawnRequestsThisTick = spawnTelemetry.RequestsThisTick;
            debugData.SpawnSpawnedThisTick = spawnTelemetry.SpawnedThisTick;
            debugData.SpawnFailuresThisTick = spawnTelemetry.FailuresThisTick;

            var reasons = new FixedString128Bytes();
            if (spawnTelemetry.FailuresByReason_TooSteep > 0)
            {
                reasons.Append("TooSteep:");
                reasons.Append(spawnTelemetry.FailuresByReason_TooSteep);
                reasons.Append(" ");
            }
            if (spawnTelemetry.FailuresByReason_Overlap > 0)
            {
                reasons.Append("Overlap:");
                reasons.Append(spawnTelemetry.FailuresByReason_Overlap);
                reasons.Append(" ");
            }
            if (spawnTelemetry.FailuresByReason_OutOfBounds > 0)
            {
                reasons.Append("OutOfBounds:");
                reasons.Append(spawnTelemetry.FailuresByReason_OutOfBounds);
                reasons.Append(" ");
            }
            debugData.SpawnFailureReasons = reasons;
        }
#endif
    }
}
