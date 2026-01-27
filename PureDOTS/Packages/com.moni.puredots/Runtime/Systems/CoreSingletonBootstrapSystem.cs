using PureDOTS.Input;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Runtime.Knowledge;
// using PureDOTS.Runtime.Narrative; // Removed - namespace not accessible, using fully qualified names instead
using PureDOTS.Runtime.Orders;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Signals;
using PureDOTS.Runtime.Skills;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Transport;
using PureDOTS.Runtime.Villager;
using PureDOTS.Runtime.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Ensures the core deterministic singletons exist even without authoring data.
    /// Runs once at startup so downstream systems can safely require these components.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    public partial struct CoreSingletonBootstrapSystem : ISystem
    {
        private static BlobAssetReference<ResourceTypeIndexBlob> s_ResourceTypeIndexBlob;
        private static BlobAssetReference<KnowledgeLessonEffectBlob> s_KnowledgeLessonCatalogBlob;
        private static BlobAssetReference<ResourceRecipeSetBlob> s_ResourceRecipeSetBlob;
        private const string HeadlessTimeScaleEnv = "PUREDOTS_HEADLESS_TIME_SCALE";
        private const string HeadlessTimeScaleArg = "--headless-time-scale";
        private const string HeadlessTargetTpsEnv = "PUREDOTS_HEADLESS_TARGET_TPS";
        private const string HeadlessTargetTpsArg = "--headless-target-tps";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool s_loggedTimeBootstrap;
        private static bool s_loggedTimeConfigs;
#endif

        private EntityQuery _timeStateQuery;
        private EntityQuery _tickTimeStateQuery;
        private EntityQuery _rewindStateQuery;
        private EntityQuery _rewindLegacyStateQuery;

        public void OnCreate(ref SystemState state)
        {
            _timeStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<TimeState>());
            _tickTimeStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<TickTimeState>());
            _rewindStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<RewindState>());
            _rewindLegacyStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<RewindLegacyState>());
            EnsureIfMissing(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            EnsureIfMissing(ref state);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (s_ResourceTypeIndexBlob.IsCreated)
            {
                s_ResourceTypeIndexBlob.Dispose();
                s_ResourceTypeIndexBlob = default;
            }

            if (s_KnowledgeLessonCatalogBlob.IsCreated)
            {
                s_KnowledgeLessonCatalogBlob.Dispose();
                s_KnowledgeLessonCatalogBlob = default;
            }

            if (s_ResourceRecipeSetBlob.IsCreated)
            {
                s_ResourceRecipeSetBlob.Dispose();
                s_ResourceRecipeSetBlob = default;
            }
        }

        private void EnsureIfMissing(ref SystemState state)
        {
            if (!ApplyHeadlessTargetTpsIfPresent(state.EntityManager))
            {
                ApplyHeadlessTimeScaleOverrideIfPresent(state.EntityManager);
            }
            // This must be resilient to late SubScene streaming or world resets that can clear entities after OnCreate.
            // Only run the heavy singleton seeding path if the critical time singletons are missing.
            if (!_timeStateQuery.IsEmptyIgnoreFilter &&
                !_tickTimeStateQuery.IsEmptyIgnoreFilter &&
                !_rewindStateQuery.IsEmptyIgnoreFilter &&
                !_rewindLegacyStateQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            EnsureSingletons(state.EntityManager);
            EnsureSingleAudioListener();

            if (_timeStateQuery.IsEmptyIgnoreFilter)
            {
                UnityEngine.Debug.LogError("[CoreSingletonBootstrapSystem] TimeState singleton is missing! This will cause system failures.");
            }
            if (_tickTimeStateQuery.IsEmptyIgnoreFilter)
            {
                UnityEngine.Debug.LogError("[CoreSingletonBootstrapSystem] TickTimeState singleton is missing! This will cause system failures.");
            }
            if (_rewindStateQuery.IsEmptyIgnoreFilter)
            {
                UnityEngine.Debug.LogError("[CoreSingletonBootstrapSystem] RewindState singleton is missing! This will cause system failures.");
            }
            if (_rewindLegacyStateQuery.IsEmptyIgnoreFilter)
            {
                UnityEngine.Debug.LogWarning("[CoreSingletonBootstrapSystem] RewindLegacyState singleton is missing; adding defaults.");
            }
        }

        public static void EnsureSingletons(EntityManager entityManager)
        {
            Entity timeEntity;
            if (!HasSingleton<TimeState>(entityManager))
            {
                timeEntity = entityManager.CreateEntity(typeof(TimeState));
                entityManager.SetComponentData(timeEntity, new TimeState
                {
                    FixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime,
                    DeltaTime = TimeSettingsDefaults.FixedDeltaTime,
                    CurrentSpeedMultiplier = TimeSettingsDefaults.DefaultSpeed,
                    Tick = 0,
                    IsPaused = false
                });

                entityManager.AddComponentData(timeEntity, new TickTimeState
                {
                    FixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime,
                    CurrentSpeedMultiplier = TimeSettingsDefaults.DefaultSpeed,
                    Tick = 0,
                    TargetTick = 0,
                    IsPaused = false,
                    IsPlaying = true
                });
            }
            else
            {
                timeEntity = GetSingletonEntity<TimeState>(entityManager);
                if (!entityManager.HasComponent<TickTimeState>(timeEntity))
                {
                    var timeState = entityManager.GetComponentData<TimeState>(timeEntity);
                    entityManager.AddComponentData(timeEntity, new TickTimeState
                    {
                        FixedDeltaTime = timeState.FixedDeltaTime,
                        CurrentSpeedMultiplier = timeState.CurrentSpeedMultiplier,
                        Tick = timeState.Tick,
                        TargetTick = timeState.Tick,
                        IsPaused = timeState.IsPaused,
                        IsPlaying = !timeState.IsPaused
                    });
                }
            }

            if (!entityManager.HasComponent<GameplayFixedStep>(timeEntity))
            {
                entityManager.AddComponentData(timeEntity, new GameplayFixedStep
                {
                    FixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime
                });
            }
            else
            {
                var fixedStep = entityManager.GetComponentData<GameplayFixedStep>(timeEntity);
                if (fixedStep.FixedDeltaTime <= 0f)
                {
                    fixedStep.FixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime;
                    entityManager.SetComponentData(timeEntity, fixedStep);
                }
            }

            if (!entityManager.HasComponent<FixedStepInterpolationState>(timeEntity))
            {
                entityManager.AddComponentData(timeEntity, new FixedStepInterpolationState
                {
                    Alpha = 0f
                });
            }

            if (!entityManager.HasComponent<TimeContext>(timeEntity))
            {
                var tickState = entityManager.GetComponentData<TickTimeState>(timeEntity);
                entityManager.AddComponentData(timeEntity, new TimeContext
                {
                    PresentTick = tickState.Tick,
                    ViewTick = tickState.Tick,
                    TargetTick = tickState.TargetTick,
                    FixedDeltaTime = tickState.FixedDeltaTime,
                    IsPaused = tickState.IsPaused,
                    SpeedMultiplier = tickState.CurrentSpeedMultiplier,
                    Mode = RewindMode.Record
                });
            }

            if (!entityManager.HasComponent<TimeLogSettings>(timeEntity))
            {
                entityManager.AddComponentData(timeEntity, TimeLogDefaults.CreateDefault());
            }

            if (!entityManager.HasComponent<PerformanceBudgetSettings>(timeEntity))
            {
                entityManager.AddComponentData(timeEntity, PerformanceBudgetDefaults.CreateDefault());
            }

            if (!entityManager.HasComponent<InputCommandLogState>(timeEntity))
            {
                entityManager.AddComponentData(timeEntity, new InputCommandLogState
                {
                    Capacity = TimeLogUtility.ExpandSecondsToTicks(TimeLogDefaults.CommandLogSeconds),
                    Count = 0,
                    StartIndex = 0,
                    LastTick = 0
                });
            }

            if (!entityManager.HasComponent<TickSnapshotLogState>(timeEntity))
            {
                entityManager.AddComponentData(timeEntity, new TickSnapshotLogState
                {
                    Capacity = TimeLogUtility.ExpandSecondsToTicks(TimeLogDefaults.SnapshotLogSeconds),
                    Count = 0,
                    StartIndex = 0,
                    LastTick = 0
                });
            }

            if (!entityManager.HasBuffer<InputCommandLogEntry>(timeEntity))
            {
                var buffer = entityManager.AddBuffer<InputCommandLogEntry>(timeEntity);
                buffer.ResizeUninitialized(TimeLogUtility.ExpandSecondsToTicks(TimeLogDefaults.CommandLogSeconds));
            }
            else
            {
                var cmdLogState = entityManager.GetComponentData<InputCommandLogState>(timeEntity);
                var buffer = entityManager.GetBuffer<InputCommandLogEntry>(timeEntity);
                if (buffer.Length < cmdLogState.Capacity)
                {
                    buffer.ResizeUninitialized(cmdLogState.Capacity);
                }
            }

            if (!entityManager.HasBuffer<TickSnapshotLogEntry>(timeEntity))
            {
                var buffer = entityManager.AddBuffer<TickSnapshotLogEntry>(timeEntity);
                buffer.ResizeUninitialized(TimeLogUtility.ExpandSecondsToTicks(TimeLogDefaults.SnapshotLogSeconds));
            }
            else
            {
                var snapshotState = entityManager.GetComponentData<TickSnapshotLogState>(timeEntity);
                var buffer = entityManager.GetBuffer<TickSnapshotLogEntry>(timeEntity);
                if (buffer.Length < snapshotState.Capacity)
                {
                    buffer.ResizeUninitialized(snapshotState.Capacity);
                }
            }

            if (!HasSingleton<HistorySettings>(entityManager))
            {
                var entity = entityManager.CreateEntity(typeof(HistorySettings));
                entityManager.SetComponentData(entity, HistorySettingsDefaults.CreateDefault());
            }

            // Ensure HistorySettingsConfig singleton exists (required for rewind).
            // IMPORTANT: HistorySettingsConfigSystem destroys the HistorySettingsConfig entity after applying it.
            // Do NOT attach this component to other singleton entities (e.g. HistorySettings), or they will be destroyed.
            if (!HasSingleton<HistorySettingsConfig>(entityManager))
            {
                var configEntity = entityManager.CreateEntity(typeof(HistorySettingsConfig));
                entityManager.SetComponentData(configEntity, new HistorySettingsConfig
                {
                    Value = HistorySettingsDefaults.CreateDefault()
                });
            }

            // Ensure TimeSettingsConfig singleton exists (required for time system).
            // IMPORTANT: TimeSettingsConfigSystem destroys the TimeSettingsConfig entity after applying it.
            // Do NOT attach this component to the TimeState entity, or it will destroy the time singleton entity.
            if (!HasSingleton<TimeSettingsConfig>(entityManager))
            {
                var configEntity = entityManager.CreateEntity(typeof(TimeSettingsConfig));
                entityManager.SetComponentData(configEntity, new TimeSettingsConfig
                {
                    FixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime,
                    MaxDeltaTime = TimeSettingsDefaults.FixedDeltaTime * 4f,
                    DefaultSpeedMultiplier = TimeSettingsDefaults.DefaultSpeedMultiplier,
                    PauseOnStart = false
                });
            }

            // RewindState must exist or the time system will never tick (TimeTickSystem requires it).
            // Prefer storing it on the TimeState singleton entity to keep core singletons co-located and stable.
            Entity rewindEntity;
            if (HasSingleton<RewindState>(entityManager))
            {
                rewindEntity = GetSingletonEntity<RewindState>(entityManager);
                if (timeEntity != Entity.Null && rewindEntity != timeEntity)
                {
                    var rewindState = entityManager.GetComponentData<RewindState>(rewindEntity);
                    var hasLegacy = entityManager.HasComponent<RewindLegacyState>(rewindEntity);
                    var legacyState = hasLegacy ? entityManager.GetComponentData<RewindLegacyState>(rewindEntity) : default;

                    if (!entityManager.HasComponent<RewindState>(timeEntity))
                    {
                        entityManager.AddComponentData(timeEntity, rewindState);
                    }
                    else
                    {
                        entityManager.SetComponentData(timeEntity, rewindState);
                    }

                    if (hasLegacy)
                    {
                        if (!entityManager.HasComponent<RewindLegacyState>(timeEntity))
                        {
                            entityManager.AddComponentData(timeEntity, legacyState);
                        }
                        else
                        {
                            entityManager.SetComponentData(timeEntity, legacyState);
                        }
                    }

                    if (entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
                    {
                        var sourceBuffer = entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
                        if (!entityManager.HasBuffer<TimeControlCommand>(timeEntity))
                        {
                            entityManager.AddBuffer<TimeControlCommand>(timeEntity);
                        }
                        var targetBuffer = entityManager.GetBuffer<TimeControlCommand>(timeEntity);
                        for (int i = 0; i < sourceBuffer.Length; i++)
                        {
                            targetBuffer.Add(sourceBuffer[i]);
                        }
                    }

                    entityManager.RemoveComponent<RewindState>(rewindEntity);
                    if (entityManager.HasComponent<RewindLegacyState>(rewindEntity))
                    {
                        entityManager.RemoveComponent<RewindLegacyState>(rewindEntity);
                    }
                    if (entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
                    {
                        entityManager.RemoveComponent<TimeControlCommand>(rewindEntity);
                    }

                    rewindEntity = timeEntity;
                }
            }
            else
            {
                rewindEntity = timeEntity != Entity.Null ? timeEntity : entityManager.CreateEntity();
                if (!entityManager.HasComponent<RewindState>(rewindEntity))
                {
                    entityManager.AddComponentData(rewindEntity, new RewindState
                    {
                        Mode = RewindMode.Record,
                        TargetTick = 0,
                        TickDuration = TimeSettingsDefaults.FixedDeltaTime,
                        MaxHistoryTicks = (int)HistorySettingsDefaults.DefaultGlobalHorizonTicks,
                        PendingStepTicks = 0
                    });
                }
            }

            if (!entityManager.HasComponent<RewindLegacyState>(rewindEntity))
            {
                entityManager.AddComponentData(rewindEntity, new RewindLegacyState
                {
                    PlaybackSpeed = 1f,
                    CurrentTick = 0,
                    StartTick = 0,
                    PlaybackTick = 0,
                    PlaybackTicksPerSecond = HistorySettingsDefaults.DefaultTicksPerSecond,
                    ScrubDirection = 0,
                    ScrubSpeedMultiplier = 1f,
                    RewindWindowTicks = 0,
                    ActiveTrack = default
                });
            }

            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            // Ensure RewindControlState singleton exists
            EnsureRewindControlState(entityManager);

            // Ensure TimeScaleSchedule singleton exists
            EnsureTimeScaleSchedule(entityManager);

            // Ensure WorldSnapshot singleton exists
            EnsureWorldSnapshotState(entityManager);

            // Ensure TimeSystemFeatureFlags singleton exists
            EnsureTimeSystemFeatureFlags(entityManager);

            // Ensure SimulationValve singleton exists
            EnsureSimulationValveSingleton(entityManager);
            
            // Verify configs exist and log
            VerifyTimeConfigs(entityManager);

            EnsureRegistry<ResourceRegistry, ResourceRegistryEntry>(entityManager, RegistryKind.Resource, "ResourceRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries | RegistryHandleFlags.SupportsPathfinding);
            EnsureRegistry<StorehouseRegistry, StorehouseRegistryEntry>(entityManager, RegistryKind.Storehouse, "StorehouseRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries | RegistryHandleFlags.SupportsPathfinding);
            EnsureRegistry<ProcessingStationRegistry, ProcessingStationRegistryEntry>(entityManager, RegistryKind.ProcessingStation, "ProcessingStationRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries);
            EnsureRegistry<VillagerRegistry, VillagerRegistryEntry>(entityManager, RegistryKind.Villager, "VillagerRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries | RegistryHandleFlags.SupportsPathfinding);
            EnsureVillagerLessonRegistryBuffer(entityManager);
            // MiracleRegistry is now created by Godgame.Systems.MiracleRegistrySystem (game-specific)
            // Game-specific transport registries (MinerVessel, Hauler, Freighter, Wagon) are now created by Space4X.Systems.TransportBootstrapSystem
            EnsureRegistry<CreatureRegistry, CreatureRegistryEntry>(entityManager, RegistryKind.Creature, "CreatureRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries | RegistryHandleFlags.SupportsPathfinding);
            EnsureRegistry<ConstructionRegistry, ConstructionRegistryEntry>(entityManager, RegistryKind.Construction, "ConstructionRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries);
            EnsureRegistry<LogisticsRequestRegistry, LogisticsRequestRegistryEntry>(entityManager, RegistryKind.LogisticsRequest, "LogisticsRequestRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries | RegistryHandleFlags.SupportsPathfinding);
            EnsureRegistry<BandRegistry, BandRegistryEntry>(entityManager, RegistryKind.Band, "BandRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries | RegistryHandleFlags.SupportsPathfinding);
            EnsureRegistry<AbilityRegistry, AbilityRegistryEntry>(entityManager, RegistryKind.Ability, "AbilityRegistry", RegistryHandleFlags.SupportsAIQueries);
            EnsureRegistry<SpawnerRegistry, SpawnerRegistryEntry>(entityManager, RegistryKind.Spawner, "SpawnerRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries | RegistryHandleFlags.SupportsPathfinding);
            EnsureRegistry<FactionRegistry, FactionRegistryEntry>(entityManager, RegistryKind.Faction, "FactionRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries);
            EnsureRegistry<ClimateHazardRegistry, ClimateHazardRegistryEntry>(entityManager, RegistryKind.ClimateHazard, "ClimateHazardRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries);
            EnsureRegistry<AreaEffectRegistry, AreaEffectRegistryEntry>(entityManager, RegistryKind.AreaEffect, "AreaEffectRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries);
            EnsureRegistry<CultureAlignmentRegistry, CultureAlignmentRegistryEntry>(entityManager, RegistryKind.CultureAlignment, "CultureAlignmentRegistry", RegistryHandleFlags.SupportsSpatialQueries | RegistryHandleFlags.SupportsAIQueries);

            // EnsureResourceTypeIndex(entityManager);
            // EnsureResourceRecipeSet(entityManager);

            EnsureAICommandQueue(entityManager);
            EnsureMoralityEventQueue(entityManager);

            EnsureSpatialGridSingleton(entityManager);
            EnsureSpatialProviderRegistry(entityManager);

            EnsureRegistryDirectory(entityManager);
            EnsureRegistrySpatialSyncState(entityManager);

            EnsureKnowledgeLessonCatalog(entityManager);
            EnsureSkillXpCurveConfig(entityManager);
            EnsureTelemetryStream(entityManager);
            EnsureTelemetryExportConfig(entityManager);
            EnsureSignalBus(entityManager);
            EnsureOrderEventStream(entityManager);
            EnsureGodHandCommandStream(entityManager);
            EnsureFrameTimingStream(entityManager);
            EnsureReplayCaptureStream(entityManager);
            EnsureRegistryHealthConfig(entityManager);
            EnsureSpawnerTelemetry(entityManager);

            EnsureFlowFieldConfig(entityManager);
            EnsureTerrainVersion(entityManager);
            // EnsureResourceTypeIndex(entityManager);
            // EnsureResourceRecipeSet(entityManager);
            EnsurePhysicsConfig(entityManager);
            // TODO: Re-enable when Narrative namespace is accessible
            // EnsureNarrativeRegistry(entityManager);

            // For compatibility with previous behaviour, ensure the system would be disabled after seeding.
        }

        private static bool HasSingleton<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static Entity GetSingletonEntity<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingletonEntity();
        }

        private static void EnsureRegistrySpatialSyncState(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistrySpatialSyncState>());
            Entity syncEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                syncEntity = entityManager.CreateEntity(typeof(RegistrySpatialSyncState));
            }
            else
            {
                syncEntity = query.GetSingletonEntity();
            }

            EnsureBuffer<RegistryContinuityAlert>(entityManager, syncEntity);

            if (!entityManager.HasComponent<RegistryContinuityState>(syncEntity))
            {
                entityManager.AddComponentData(syncEntity, new RegistryContinuityState
                {
                    Version = 0,
                    LastCheckTick = 0,
                    WarningCount = 0,
                    FailureCount = 0
                });
            }
        }

        private static void EnsureRegistry<TComponent, TBuffer>(EntityManager entityManager, RegistryKind kind, FixedString64Bytes label, RegistryHandleFlags flags)
            where TComponent : unmanaged, IComponentData
            where TBuffer : unmanaged, IBufferElementData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TComponent>());
            Entity registryEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                registryEntity = entityManager.CreateEntity(typeof(TComponent));
            }
            else
            {
                registryEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<TBuffer>(registryEntity))
            {
                entityManager.AddBuffer<TBuffer>(registryEntity);
            }

            if (!entityManager.HasComponent<RegistryMetadata>(registryEntity))
            {
                var metadata = new RegistryMetadata();
                metadata.Initialise(kind, 0, flags, label);
                entityManager.AddComponentData(registryEntity, metadata);
            }
            else
            {
                var metadata = entityManager.GetComponentData<RegistryMetadata>(registryEntity);
                if (metadata.Kind == RegistryKind.Unknown && metadata.Version == 0 && metadata.EntryCount == 0)
                {
                    metadata.Initialise(kind, metadata.ArchetypeId, flags, label);
                    entityManager.SetComponentData(registryEntity, metadata);
                }
            }

            if (!entityManager.HasComponent<RegistryHealth>(registryEntity))
            {
                entityManager.AddComponentData(registryEntity, default(RegistryHealth));
            }
        }

        private static void EnsureAICommandQueue(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AICommandQueueTag>());
            Entity queueEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                queueEntity = entityManager.CreateEntity(typeof(AICommandQueueTag));
            }
            else
            {
                queueEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<AICommand>(queueEntity))
            {
                entityManager.AddBuffer<AICommand>(queueEntity);
            }
        }

        private static void EnsureMoralityEventQueue(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MoralityEventQueueTag>());
            Entity queueEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                queueEntity = entityManager.CreateEntity(typeof(MoralityEventQueueTag));
            }
            else
            {
                queueEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<MoralityEvent>(queueEntity))
            {
                entityManager.AddBuffer<MoralityEvent>(queueEntity);
            }

            if (!entityManager.HasComponent<MoralityEventProcessingState>(queueEntity))
            {
                entityManager.AddComponentData(queueEntity, default(MoralityEventProcessingState));
            }
        }

        private static void EnsureSpatialGridSingleton(EntityManager entityManager)
        {
            // Only augment the singleton if it exists (created by Authoring).
            // We do NOT create a default one here to avoid duplicates when SubScenes load later.
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpatialGridConfig>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var gridEntity = query.GetSingletonEntity();
            if (!entityManager.HasComponent<SpatialGridState>(gridEntity))
            {
                entityManager.AddComponentData(gridEntity, new SpatialGridState
                {
                    ActiveBufferIndex = 0,
                    TotalEntries = 0,
                    Version = 0,
                    LastUpdateTick = 0,
                    LastDirtyTick = 0,
                    DirtyVersion = 0,
                    DirtyAddCount = 0,
                    DirtyUpdateCount = 0,
                    DirtyRemoveCount = 0,
                    LastRebuildMilliseconds = 0f,
                    LastStrategy = SpatialGridRebuildStrategy.None
                });
            }

            if (!entityManager.HasComponent<SpatialRebuildThresholds>(gridEntity))
            {
                entityManager.AddComponentData(gridEntity, SpatialRebuildThresholds.CreateDefaults());
            }

            EnsureBuffer<SpatialGridCellRange>(entityManager, gridEntity);
            EnsureBuffer<SpatialGridEntry>(entityManager, gridEntity);
            EnsureBuffer<SpatialGridStagingEntry>(entityManager, gridEntity);
            EnsureBuffer<SpatialGridStagingCellRange>(entityManager, gridEntity);
            EnsureBuffer<SpatialGridEntryLookup>(entityManager, gridEntity);
            EnsureBuffer<SpatialGridDirtyOp>(entityManager, gridEntity);

            if (!entityManager.HasComponent<SpatialRegistryMetadata>(gridEntity))
            {
                entityManager.AddComponentData(gridEntity, default(SpatialRegistryMetadata));
            }

            EnsureSignalField(entityManager, gridEntity);
        }

        private static void EnsureRegistryDirectory(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryDirectory>());
            Entity directoryEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                directoryEntity = entityManager.CreateEntity(typeof(RegistryDirectory));
                entityManager.SetComponentData(directoryEntity, new RegistryDirectory
                {
                    Version = 0,
                    LastUpdateTick = 0,
                    AggregateHash = 0
                });
            }
            else
            {
                directoryEntity = query.GetSingletonEntity();
            }

            EnsureBuffer<RegistryDirectoryEntry>(entityManager, directoryEntity);
            EnsureBuffer<RegistryInstrumentationSample>(entityManager, directoryEntity);

            if (!entityManager.HasComponent<RegistryInstrumentationState>(directoryEntity))
            {
                entityManager.AddComponentData(directoryEntity, new RegistryInstrumentationState
                {
                    Version = 0,
                    LastUpdateTick = 0,
                    SampleCount = 0
                });
            }
        }

        private static void EnsureSignalField(EntityManager entityManager, Entity gridEntity)
        {
            if (!entityManager.HasComponent<SignalFieldState>(gridEntity))
            {
                entityManager.AddComponentData(gridEntity, default(SignalFieldState));
            }

            if (!entityManager.HasComponent<SignalFieldConfig>(gridEntity))
            {
                entityManager.AddComponentData(gridEntity, SignalFieldConfig.Default);
            }

            if (!entityManager.HasComponent<SignalPerceptionThresholds>(gridEntity))
            {
                entityManager.AddComponentData(gridEntity, SignalPerceptionThresholds.Default);
            }

            EnsureBuffer<SignalFieldCell>(entityManager, gridEntity);

            var config = entityManager.GetComponentData<SpatialGridConfig>(gridEntity);
            if (config.CellCount <= 0)
            {
                return;
            }

            var cells = entityManager.GetBuffer<SignalFieldCell>(gridEntity);
            if (cells.Length != config.CellCount)
            {
                cells.ResizeUninitialized(config.CellCount);
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = default;
                }
            }
        }

        private static void EnsureTelemetryStream(EntityManager entityManager)
        {
            using var streamQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            Entity telemetryEntity;

            if (streamQuery.IsEmptyIgnoreFilter)
            {
                telemetryEntity = entityManager.CreateEntity(typeof(TelemetryStream));
                entityManager.SetComponentData(telemetryEntity, new TelemetryStream
                {
                    Version = 0,
                    LastTick = 0
                });
            }
            else
            {
                telemetryEntity = streamQuery.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);
            }

            TelemetryStreamUtility.EnsureEventStream(entityManager);
        }

        private static void EnsureTelemetryExportConfig(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryExportConfig>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(typeof(TelemetryExportConfig));
            entityManager.SetComponentData(entity, TelemetryExportConfig.CreateDisabled());
        }

        private static void EnsureSignalBus(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SignalBus>());
            Entity busEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                busEntity = entityManager.CreateEntity(typeof(SignalBus));
            }
            else
            {
                busEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<SignalEvent>(busEntity))
            {
                entityManager.AddBuffer<SignalEvent>(busEntity);
            }

            if (!entityManager.HasComponent<SignalBusConfig>(busEntity))
            {
                entityManager.AddComponentData(busEntity, SignalBusConfig.CreateDefault());
            }
        }

        private static void EnsureOrderEventStream(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<OrderEventStream>());
            Entity streamEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                streamEntity = entityManager.CreateEntity(typeof(OrderEventStream));
            }
            else
            {
                streamEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<OrderEvent>(streamEntity))
            {
                entityManager.AddBuffer<OrderEvent>(streamEntity);
            }

            if (!entityManager.HasComponent<OrderEventStreamConfig>(streamEntity))
            {
                entityManager.AddComponentData(streamEntity, OrderEventStreamConfig.CreateDefault());
            }
        }

        private static void EnsureGodHandCommandStream(EntityManager entityManager)
        {
            GodHandCommandStreamUtility.EnsureStream(entityManager);
        }

        private static void EnsureFrameTimingStream(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FrameTimingStream>());
            Entity frameEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                frameEntity = entityManager.CreateEntity(typeof(FrameTimingStream));
                entityManager.SetComponentData(frameEntity, new FrameTimingStream
                {
                    Version = 0,
                    LastTick = 0
                });
            }
            else
            {
                frameEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<FrameTimingSample>(frameEntity))
            {
                entityManager.AddBuffer<FrameTimingSample>(frameEntity);
            }

            if (!entityManager.HasComponent<AllocationDiagnostics>(frameEntity))
            {
                entityManager.AddComponentData(frameEntity, new AllocationDiagnostics());
            }
        }

        private static void EnsureReplayCaptureStream(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ReplayCaptureStream>());
            Entity replayEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                replayEntity = entityManager.CreateEntity(typeof(ReplayCaptureStream));
                entityManager.SetComponentData(replayEntity, new ReplayCaptureStream
                {
                    Version = 0,
                    LastTick = 0,
                    EventCount = 0,
                    LastEventType = ReplayableEvent.EventType.Custom,
                    LastEventLabel = default
                });
            }
            else
            {
                replayEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<ReplayCaptureEvent>(replayEntity))
            {
                entityManager.AddBuffer<ReplayCaptureEvent>(replayEntity);
            }
        }

        private static void EnsureSpawnerTelemetry(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpawnerTelemetry>());
            if (query.IsEmptyIgnoreFilter)
            {
                var telemetryEntity = entityManager.CreateEntity(typeof(SpawnerTelemetry));
                entityManager.SetComponentData(telemetryEntity, default(SpawnerTelemetry));
            }
        }

        private static void EnsureSpatialProviderRegistry(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpatialProviderRegistry>());
            Entity registryEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                registryEntity = entityManager.CreateEntity(typeof(SpatialProviderRegistry));
                entityManager.AddBuffer<SpatialProviderRegistryEntry>(registryEntity);
                entityManager.SetComponentData(registryEntity, new SpatialProviderRegistry
                {
                    NextProviderId = 2,
                    Version = 0
                });
            }
        }

        private static void EnsureVillagerBehaviorConfig(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerBehaviorConfig>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(VillagerBehaviorConfig));
                entityManager.SetComponentData(entity, VillagerBehaviorConfig.CreateDefaults());
            }
        }

        private static void EnsureResourceInteractionConfig(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ResourceInteractionConfig>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(ResourceInteractionConfig));
                entityManager.SetComponentData(entity, ResourceInteractionConfig.CreateDefaults());
            }
        }

        private static void EnsureTerrainVersion(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PureDOTS.Environment.TerrainVersion>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(PureDOTS.Environment.TerrainVersion));
                entityManager.SetComponentData(entity, new PureDOTS.Environment.TerrainVersion { Value = 0 });
            }
        }

        private static void EnsurePrayerPower(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PrayerPower>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(PrayerPower));
                entityManager.SetComponentData(entity, new PrayerPower
                {
                    CurrentMana = 100f,
                    MaxMana = 100f,
                    RegenRate = 1f,
                    LastRegenTick = 0
                });
            }
        }

        private static void EnsureFlowFieldConfig(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldConfig>());
            Entity flowFieldEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                flowFieldEntity = entityManager.CreateEntity(typeof(FlowFieldConfig));
                entityManager.SetComponentData(flowFieldEntity, new FlowFieldConfig
                {
                    CellSize = 5f,
                    WorldBoundsMin = new float2(-100f, -100f),
                    WorldBoundsMax = new float2(100f, 100f),
                    RebuildCadenceTicks = 30,
                    SteeringWeight = 1f,
                    AvoidanceWeight = 1.5f,
                    CohesionWeight = 0.5f,
                    SeparationWeight = 2f,
                    LastRebuildTick = 0,
                    Version = 0,
                    TerrainVersion = 0
                });
            }
            else
            {
                flowFieldEntity = query.GetSingletonEntity();
            }

            EnsureBuffer<FlowFieldLayer>(entityManager, flowFieldEntity);
            EnsureBuffer<FlowFieldCellData>(entityManager, flowFieldEntity);
            EnsureBuffer<FlowFieldRequest>(entityManager, flowFieldEntity);
            EnsureBuffer<FlowFieldHazardUpdate>(entityManager, flowFieldEntity);
        }

        private static void EnsureRegistryHealthConfig(EntityManager entityManager)
        {
            using var monitoringQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryHealthMonitoring>());

            if (monitoringQuery.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(RegistryHealthMonitoring), typeof(RegistryHealthThresholds));
                entityManager.SetComponentData(entity, RegistryHealthMonitoring.CreateDefaults());
                var thresholds = RegistryHealthThresholds.CreateDefaults();
                if (RuntimeMode.IsHeadless)
                {
                    thresholds.DirectoryVersionMismatchWarning = 0;
                }
                entityManager.SetComponentData(entity, thresholds);
                
                // Ensure villager behavior config singleton
                EnsureVillagerBehaviorConfig(entityManager);
                
                // Ensure resource interaction config singleton
                EnsureResourceInteractionConfig(entityManager);
                return;
            }

            var monitoringEntity = monitoringQuery.GetSingletonEntity();

            if (!entityManager.HasComponent<RegistryHealthThresholds>(monitoringEntity))
            {
                entityManager.AddComponentData(monitoringEntity, RegistryHealthThresholds.CreateDefaults());
            }

            if (RuntimeMode.IsHeadless)
            {
                var thresholds = entityManager.GetComponentData<RegistryHealthThresholds>(monitoringEntity);
                thresholds.DirectoryVersionMismatchWarning = 0;
                entityManager.SetComponentData(monitoringEntity, thresholds);
            }
        }

        private static SpatialGridConfig CreateDefaultSpatialConfig()
        {
            return new SpatialGridConfig
            {
                CellSize = 4f,
                WorldMin = new float3(-512f, -64f, -512f),
                WorldMax = new float3(512f, 64f, 512f),
                CellCounts = new int3(256, 32, 256),
                HashSeed = 0u,
                ProviderId = 0
            };
        }

        private static void EnsureBuffer<TBuffer>(EntityManager entityManager, Entity entity)
            where TBuffer : unmanaged, IBufferElementData
        {
            if (!entityManager.HasBuffer<TBuffer>(entity))
            {
                entityManager.AddBuffer<TBuffer>(entity);
            }
        }

        private static void EnsureVillagerLessonRegistryBuffer(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerRegistry>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = query.GetSingletonEntity();
            EnsureBuffer<VillagerLessonRegistryEntry>(entityManager, entity);
        }

        private static void EnsureKnowledgeLessonCatalog(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<KnowledgeLessonEffectCatalog>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!s_KnowledgeLessonCatalogBlob.IsCreated)
            {
                s_KnowledgeLessonCatalogBlob = KnowledgeLessonEffectDefaults.CreateDefaultCatalog();
            }
            var entity = entityManager.CreateEntity(typeof(KnowledgeLessonEffectCatalog));
            entityManager.SetComponentData(entity, new KnowledgeLessonEffectCatalog { Blob = s_KnowledgeLessonCatalogBlob });
        }

        private static void EnsureSkillXpCurveConfig(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SkillXpCurveConfig>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(typeof(SkillXpCurveConfig));
            entityManager.SetComponentData(entity, SkillXpCurveConfig.CreateDefaults());
        }

        private static void EnsureResourceTypeIndex(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ResourceTypeIndex>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!s_ResourceTypeIndexBlob.IsCreated)
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<ResourceTypeIndexBlob>();
                builder.Allocate(ref root.Ids, 0);
                builder.Allocate(ref root.DisplayNames, 0);
                builder.Allocate(ref root.Colors, 0);
                s_ResourceTypeIndexBlob = builder.CreateBlobAssetReference<ResourceTypeIndexBlob>(Allocator.Persistent);
            }

            var entity = entityManager.CreateEntity(typeof(ResourceTypeIndex));
            entityManager.SetComponentData(entity, new ResourceTypeIndex { Catalog = s_ResourceTypeIndexBlob });
        }

        private static void EnsureResourceRecipeSet(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ResourceRecipeSet>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!s_ResourceRecipeSetBlob.IsCreated)
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<ResourceRecipeSetBlob>();
                builder.Allocate(ref root.Families, 0);
                builder.Allocate(ref root.Recipes, 0);
                s_ResourceRecipeSetBlob = builder.CreateBlobAssetReference<ResourceRecipeSetBlob>(Allocator.Persistent);
            }

            var entity = entityManager.CreateEntity(typeof(ResourceRecipeSet));
            entityManager.SetComponentData(entity, new ResourceRecipeSet { Value = s_ResourceRecipeSetBlob });
        }

        private static void EnsurePhysicsConfig(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsConfig>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = entityManager.CreateEntity(typeof(PhysicsConfig), typeof(PhysicsConfigTag));
            entityManager.SetComponentData(entity, PhysicsConfig.CreateDefault());
            
            UnityEngine.Debug.Log("[CoreSingletonBootstrapSystem] PhysicsConfig singleton created with default settings");
        }

        // TODO: Re-enable when Narrative namespace is accessible to compiler/source generator
        // The Narrative types exist but aren't accessible during compilation, causing source generator errors
        /*
        private static void EnsureNarrativeRegistry(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PureDOTS.Runtime.Narrative.NarrativeRegistrySingleton>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var eventRegistry = PureDOTS.Runtime.Narrative.NarrativeRegistryBuilder.CreateTestEventRegistry(Allocator.Persistent);
            var situationRegistry = PureDOTS.Runtime.Narrative.NarrativeRegistryBuilder.CreateTestSituationRegistry(Allocator.Persistent);

            var entity = entityManager.CreateEntity(typeof(PureDOTS.Runtime.Narrative.NarrativeRegistrySingleton));
            entityManager.SetComponentData(entity, new PureDOTS.Runtime.Narrative.NarrativeRegistrySingleton
            {
                EventRegistry = eventRegistry,
                SituationRegistry = situationRegistry
            });

            // Create singleton entities for narrative buffers
            EnsureNarrativeBuffers(entityManager);
        }

        private static void EnsureNarrativeBuffers(EntityManager entityManager)
        {
            // Narrative signal singleton
            Entity signalEntity;
            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PureDOTS.Runtime.Narrative.NarrativeSignalBufferElement>()))
            {
                if (query.IsEmptyIgnoreFilter)
                {
                    signalEntity = entityManager.CreateEntity();
                    entityManager.AddBuffer<PureDOTS.Runtime.Narrative.NarrativeSignalBufferElement>(signalEntity);
                }
            }

            // Effect request singleton
            Entity effectEntity;
            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PureDOTS.Runtime.Narrative.NarrativeEffectRequest>()))
            {
                if (query.IsEmptyIgnoreFilter)
                {
                    effectEntity = entityManager.CreateEntity();
                    entityManager.AddBuffer<PureDOTS.Runtime.Narrative.NarrativeEffectRequest>(effectEntity);
                }
            }

            // Reward signal singleton
            Entity rewardEntity;
            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PureDOTS.Runtime.Narrative.NarrativeRewardSignal>()))
            {
                if (query.IsEmptyIgnoreFilter)
                {
                    rewardEntity = entityManager.CreateEntity();
                    entityManager.AddBuffer<PureDOTS.Runtime.Narrative.NarrativeRewardSignal>(rewardEntity);
                }
            }

            // Inbox singleton (spawn requests, choices, world facts)
            Entity inboxEntity;
            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PureDOTS.Runtime.Narrative.SituationSpawnRequest>()))
            {
                if (query.IsEmptyIgnoreFilter)
                {
                    inboxEntity = entityManager.CreateEntity();
                    entityManager.AddBuffer<PureDOTS.Runtime.Narrative.SituationSpawnRequest>(inboxEntity);
                    entityManager.AddBuffer<PureDOTS.Runtime.Narrative.SituationChoice>(inboxEntity);
                    entityManager.AddBuffer<PureDOTS.Runtime.Narrative.WorldFactEvent>(inboxEntity);
                }
            }
        }
        */

        private static void EnsureTimeScaleSchedule(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeScaleScheduleState>());
            Entity scheduleEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                scheduleEntity = entityManager.CreateEntity(typeof(TimeScaleScheduleState), typeof(TimeScaleScheduleTag));
                entityManager.SetComponentData(scheduleEntity, new TimeScaleScheduleState
                {
                    NextEntryId = 1,
                    ResolvedScale = 1.0f,
                    IsPaused = false,
                    ActiveEntryId = 0,
                    ActiveSource = TimeScaleSource.Default
                });
            }
            else
            {
                scheduleEntity = query.GetSingletonEntity();
            }

            // Ensure the buffer exists
            if (!entityManager.HasBuffer<TimeScaleEntry>(scheduleEntity))
            {
                entityManager.AddBuffer<TimeScaleEntry>(scheduleEntity);
            }

            // Ensure config singleton exists
            using var configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeScaleConfig>());
            if (configQuery.IsEmptyIgnoreFilter)
            {
                entityManager.AddComponentData(scheduleEntity, TimeScaleConfig.CreateDefault());
            }
        }

        private static void EnsureWorldSnapshotState(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldSnapshotState>());
            Entity snapshotEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                snapshotEntity = entityManager.CreateEntity(typeof(WorldSnapshotState), typeof(WorldSnapshotTag));
                entityManager.SetComponentData(snapshotEntity, WorldSnapshotState.CreateDefault());
            }
            else
            {
                snapshotEntity = query.GetSingletonEntity();
            }

            // Ensure buffers exist
            if (!entityManager.HasBuffer<WorldSnapshotMeta>(snapshotEntity))
            {
                entityManager.AddBuffer<WorldSnapshotMeta>(snapshotEntity);
            }
            if (!entityManager.HasBuffer<WorldSnapshotData>(snapshotEntity))
            {
                entityManager.AddBuffer<WorldSnapshotData>(snapshotEntity);
            }

            // Ensure TimeHistoryState singleton exists
            using var historyQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeHistoryState>());
            if (historyQuery.IsEmptyIgnoreFilter)
            {
                var historyEntity = entityManager.CreateEntity(typeof(TimeHistoryState));
                entityManager.SetComponentData(historyEntity, new TimeHistoryState
                {
                    ActiveEntityCount = 0,
                    EstimatedMemoryBytes = 0,
                    LastCleanupTick = 0,
                    LastCleanupPrunedCount = 0,
                    IsUnderMemoryPressure = false
                });
            }
        }

        private static void EnsureTimeSystemFeatureFlags(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeSystemFeatureFlags>());
            Entity flagsEntity;
            TimeSystemFeatureFlags flags;
            
            if (query.IsEmptyIgnoreFilter)
            {
                flagsEntity = entityManager.CreateEntity(typeof(TimeSystemFeatureFlags), typeof(TimeSystemFeaturesConfiguredTag));
                flags = TimeSystemFeatureFlags.CreateDefault();
                // Explicitly set mode semantics for single-player
                flags.SimulationMode = TimeSimulationMode.SinglePlayer;
                flags.IsMultiplayerSession = false;
                flags.MultiplayerMode = TimeMultiplayerMode.SinglePlayerOnly;
                entityManager.SetComponentData(flagsEntity, flags);
            }
            else
            {
                flagsEntity = query.GetSingletonEntity();
                flags = entityManager.GetComponentData<TimeSystemFeatureFlags>(flagsEntity);
                
                // Always explicitly set mode semantics (ensures consistency even if flags were created before)
                flags.SimulationMode = TimeSimulationMode.SinglePlayer;
                flags.IsMultiplayerSession = false;
                flags.MultiplayerMode = TimeMultiplayerMode.SinglePlayerOnly;
                entityManager.SetComponentData(flagsEntity, flags);
            }
            
            // Add debug log after flags are set (reuse flags variable from outer scope)
            var tickState = HasSingleton<TickTimeState>(entityManager) 
                ? entityManager.GetComponentData<TickTimeState>(GetSingletonEntity<TickTimeState>(entityManager))
                : default;
            var timeScaleConfig = HasSingleton<TimeScaleConfig>(entityManager)
                ? entityManager.GetComponentData<TimeScaleConfig>(GetSingletonEntity<TimeScaleConfig>(entityManager))
                : TimeScaleConfig.CreateDefault();
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!s_loggedTimeBootstrap)
            {
                s_loggedTimeBootstrap = true;
                if (!Application.isBatchMode)
                {
                    UnityEngine.Debug.Log($"[Time] tick={tickState.Tick} baseScale={timeScaleConfig.DefaultScale} mode={flags.SimulationMode} mp={flags.IsMultiplayerSession}");
                }
            }
#endif
        }
        
        private static void EnsureRewindControlState(EntityManager entityManager)
        {
            if (!HasSingleton<RewindControlState>(entityManager))
            {
                var controlEntity = entityManager.CreateEntity(typeof(RewindControlState));
                entityManager.SetComponentData(controlEntity, new RewindControlState
                {
                    Phase = RewindPhase.Inactive,
                    PresentTickAtStart = 0,
                    PreviewTick = 0,
                    ScrubSpeed = 1.0f
                });
                
                // Add command buffer to RewindControlState entity as well (for convenience)
                if (!entityManager.HasBuffer<TimeControlCommand>(controlEntity))
                {
                    entityManager.AddBuffer<TimeControlCommand>(controlEntity);
                }
            }
        }

        private static void VerifyTimeConfigs(EntityManager entityManager)
        {
            bool hasTimeScaleConfig = HasSingleton<TimeScaleConfig>(entityManager);
            bool hasHistoryConfig = HasSingleton<HistorySettingsConfig>(entityManager);
            bool hasTimeSettingsConfig = HasSingleton<TimeSettingsConfig>(entityManager);
            
            if (hasTimeScaleConfig && hasHistoryConfig && hasTimeSettingsConfig)
            {
                var timeScaleConfig = entityManager.GetComponentData<TimeScaleConfig>(GetSingletonEntity<TimeScaleConfig>(entityManager));
                var historyConfig = entityManager.GetComponentData<HistorySettingsConfig>(GetSingletonEntity<HistorySettingsConfig>(entityManager));
                var timeSettingsConfig = entityManager.GetComponentData<TimeSettingsConfig>(GetSingletonEntity<TimeSettingsConfig>(entityManager));
                
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!s_loggedTimeConfigs)
                {
                    s_loggedTimeConfigs = true;
                    if (!Application.isBatchMode)
                    {
                        UnityEngine.Debug.Log($"[Time] Configs loaded: TimeScaleConfig (min={timeScaleConfig.MinScale}, max={timeScaleConfig.MaxScale}, default={timeScaleConfig.DefaultScale}), " +
                            $"HistoryConfig (horizon={historyConfig.Value.DefaultHorizonSeconds}s), " +
                            $"TimeSettingsConfig (fixedDt={timeSettingsConfig.FixedDeltaTime}, defaultSpeed={timeSettingsConfig.DefaultSpeedMultiplier})");
                    }
                }
#endif
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!s_loggedTimeConfigs)
                {
                    s_loggedTimeConfigs = true;
                    if (!Application.isBatchMode)
                    {
                        UnityEngine.Debug.LogWarning($"[Time] Missing configs: TimeScaleConfig={hasTimeScaleConfig}, HistoryConfig={hasHistoryConfig}, TimeSettingsConfig={hasTimeSettingsConfig}");
                    }
                }
#endif
            }
        }

        private static void EnsureSimulationValveSingleton(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SimulationFeatureFlags>());
            if (!query.IsEmptyIgnoreFilter)
            {
                var existingValveEntity = query.GetSingletonEntity();
                if (!entityManager.HasComponent<SimulationScalars>(existingValveEntity))
                {
                    entityManager.AddComponentData(existingValveEntity, SimulationScalars.Default);
                }

                var existingScalars = entityManager.GetComponentData<SimulationScalars>(existingValveEntity);
                TryApplyHeadlessTimeScaleOverride(ref existingScalars);
                entityManager.SetComponentData(existingValveEntity, existingScalars);
                return;
            }

            var valveEntity = entityManager.CreateEntity(
                typeof(SimulationFeatureFlags),
                typeof(SimulationScalars),
                typeof(SimulationOverrides),
                typeof(SimulationSandboxFlags));

            var scalars = SimulationScalars.Default;
            var overrides = SimulationOverrides.Default;
            var sandbox = SimulationSandboxFlags.Default;
            TryApplyHeadlessTimeScaleOverride(ref scalars);

            entityManager.SetComponentData(valveEntity, SimulationFeatureFlags.Default);
            entityManager.SetComponentData(valveEntity, scalars);
            entityManager.SetComponentData(valveEntity, overrides);
            entityManager.SetComponentData(valveEntity, sandbox);
        }

        private static void TryApplyHeadlessTimeScaleOverride(ref SimulationScalars scalars)
        {
            if (!TryGetHeadlessTimeScaleOverride(out var parsed))
            {
                return;
            }

            scalars.TimeScale = parsed;
        }

        private static bool ApplyHeadlessTargetTpsIfPresent(EntityManager entityManager)
        {
            if (!TryGetHeadlessTargetTps(out var targetTps))
            {
                return false;
            }

            UnityEngine.QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = Mathf.Max(1, (int)Mathf.Round(targetTps));
            UnityEngine.Time.maximumDeltaTime = 1f / targetTps;
            UnityEngine.Time.timeScale = 1f;

            if (!HasSingleton<HeadlessTpsCap>(entityManager))
            {
                var capEntity = entityManager.CreateEntity(typeof(HeadlessTpsCap));
                entityManager.SetComponentData(capEntity, new HeadlessTpsCap { TargetTps = targetTps });
            }
            else
            {
                var capEntity = GetSingletonEntity<HeadlessTpsCap>(entityManager);
                var cap = entityManager.GetComponentData<HeadlessTpsCap>(capEntity);
                cap.TargetTps = targetTps;
                entityManager.SetComponentData(capEntity, cap);
            }

            return true;
        }

        private static void ApplyHeadlessTimeScaleOverrideIfPresent(EntityManager entityManager)
        {
            if (!TryGetHeadlessTimeScaleOverride(out var parsed))
            {
                return;
            }

            UnityEngine.Time.timeScale = parsed;

            if (!HasSingleton<SimulationScalars>(entityManager))
            {
                return;
            }

            var valveEntity = GetSingletonEntity<SimulationScalars>(entityManager);
            var scalars = entityManager.GetComponentData<SimulationScalars>(valveEntity);
            scalars.TimeScale = parsed;
            entityManager.SetComponentData(valveEntity, scalars);

            if (entityManager.HasComponent<SimulationOverrides>(valveEntity))
            {
                var overrides = entityManager.GetComponentData<SimulationOverrides>(valveEntity);
                overrides.OverrideTimeScale = true;
                overrides.TimeScaleOverride = parsed;
                entityManager.SetComponentData(valveEntity, overrides);
            }

            if (HasSingleton<TimeScaleConfig>(entityManager))
            {
                var configEntity = GetSingletonEntity<TimeScaleConfig>(entityManager);
                var config = entityManager.GetComponentData<TimeScaleConfig>(configEntity);
                config.DefaultScale = parsed;
                config.MaxScale = math.max(config.MaxScale, parsed);
                entityManager.SetComponentData(configEntity, config);
            }

            if (HasSingleton<TimeScaleScheduleState>(entityManager))
            {
                var scheduleEntity = GetSingletonEntity<TimeScaleScheduleState>(entityManager);
                var scheduleState = entityManager.GetComponentData<TimeScaleScheduleState>(scheduleEntity);
                scheduleState.ResolvedScale = parsed;
                scheduleState.IsPaused = false;
                scheduleState.ActiveSource = TimeScaleSource.Default;
                entityManager.SetComponentData(scheduleEntity, scheduleState);
            }
        }

        private static bool TryGetHeadlessTimeScaleOverride(out float parsed)
        {
            parsed = 0f;
            if (!Application.isBatchMode)
            {
                return false;
            }

            var value = global::System.Environment.GetEnvironmentVariable(HeadlessTimeScaleEnv);
            if (string.IsNullOrWhiteSpace(value))
            {
                TryGetCommandLineArg(HeadlessTimeScaleArg, out value);
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!float.TryParse(value, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var raw))
            {
                return false;
            }

            if (raw <= 0f)
            {
                return false;
            }

            parsed = math.max(0.1f, raw);
            return true;
        }

        private static bool TryGetHeadlessTargetTps(out float parsed)
        {
            parsed = 0f;
            if (!Application.isBatchMode)
            {
                return false;
            }

            var value = global::System.Environment.GetEnvironmentVariable(HeadlessTargetTpsEnv);
            if (string.IsNullOrWhiteSpace(value))
            {
                TryGetCommandLineArg(HeadlessTargetTpsArg, out value);
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!float.TryParse(value, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var raw))
            {
                return false;
            }

            if (raw <= 0f)
            {
                return false;
            }

            parsed = math.max(1f, raw);
            return true;
        }

        private static bool TryGetCommandLineArg(string key, out string value)
        {
            var args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.Equals(arg, key, global::System.StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        value = args[i + 1];
                        return true;
                    }
                    break;
                }

                var prefix = key + "=";
                if (arg.StartsWith(prefix, global::System.StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(prefix.Length).Trim('"');
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Ensures exactly one AudioListener is enabled in the scene.
        /// Unity requires exactly one active AudioListener for audio to work correctly.
        /// </summary>
        private static void EnsureSingleAudioListener()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            var audioListeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            if (audioListeners.Length == 0)
            {
                return;
            }

            if (audioListeners.Length == 1)
            {
                if (!audioListeners[0].enabled)
                {
                    audioListeners[0].enabled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    UnityEngine.Debug.Log($"[Bootstrap] Enabled AudioListener on {audioListeners[0].gameObject.name}");
#endif
                }
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning($"[Bootstrap] Found {audioListeners.Length} AudioListeners in scene. Unity requires exactly one. Disabling extras.");
#endif

            AudioListener activeListener = null;
            foreach (var listener in audioListeners)
            {
                if (listener.gameObject.CompareTag("MainCamera"))
                {
                    activeListener = listener;
                    break;
                }
            }

            if (activeListener == null)
            {
                activeListener = audioListeners[0];
            }

            foreach (var listener in audioListeners)
            {
                if (listener == activeListener)
                {
                    listener.enabled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    UnityEngine.Debug.Log($"[Bootstrap] Keeping AudioListener enabled on {listener.gameObject.name}");
#endif
                }
                else
                {
                    listener.enabled = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    UnityEngine.Debug.Log($"[Bootstrap] Disabled AudioListener on {listener.gameObject.name}");
#endif
                }
            }
        }
    }
}
