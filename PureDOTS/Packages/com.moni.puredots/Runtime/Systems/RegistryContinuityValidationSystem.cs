using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Validates registry spatial continuity against the published spatial sync state and raises alerts when drift is detected.
    /// Also validates custom registry participants registered via <see cref="RegistryContinuityApi"/>.
    /// </summary>
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(RegistryHealthSystem))]
    public partial struct RegistryContinuityValidationSystem : ISystem
    {
        private ComponentLookup<RegistryMetadata> _metadataLookup;
        private ComponentLookup<RegistryHealth> _healthLookup;
        private BufferLookup<RegistryDirectoryEntry> _directoryLookup;
        private BufferLookup<RegistryContinuityParticipant> _participantsLookup;
        private BufferLookup<ContinuityValidationReport> _reportLookup;
        private EntityQuery _participantsQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RegistrySpatialSyncState>();
            state.RequireForUpdate<RegistryDirectory>();

            _metadataLookup = state.GetComponentLookup<RegistryMetadata>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<RegistryHealth>(isReadOnly: true);
            _directoryLookup = state.GetBufferLookup<RegistryDirectoryEntry>(isReadOnly: true);
            _participantsLookup = state.GetBufferLookup<RegistryContinuityParticipant>(isReadOnly: true);
            _reportLookup = state.GetBufferLookup<ContinuityValidationReport>(isReadOnly: false);
            _participantsQuery = state.GetEntityQuery(ComponentType.ReadOnly<RegistryContinuityParticipants>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var syncEntity = SystemAPI.GetSingletonEntity<RegistrySpatialSyncState>();
            var directoryEntity = SystemAPI.GetSingletonEntity<RegistryDirectory>();

            _metadataLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _directoryLookup.Update(ref state);
            _participantsLookup.Update(ref state);
            _reportLookup.Update(ref state);

            var alertsBuffer = state.EntityManager.GetBuffer<RegistryContinuityAlert>(syncEntity);
            var previousAlertCount = alertsBuffer.Length;
            alertsBuffer.Clear();

            ref var continuityState = ref SystemAPI.GetComponentRW<RegistryContinuityState>(syncEntity).ValueRW;
            var previousWarnings = continuityState.WarningCount;
            var previousFailures = continuityState.FailureCount;

            var thresholds = SystemAPI.HasSingleton<RegistryHealthThresholds>()
                ? SystemAPI.GetSingleton<RegistryHealthThresholds>()
                : RegistryHealthThresholds.CreateDefaults();
            var currentTick = SystemAPI.HasSingleton<TimeState>()
                ? SystemAPI.GetSingleton<TimeState>().Tick
                : 0u;

            if (!_directoryLookup.HasBuffer(directoryEntity))
            {
                continuityState.WarningCount = 0;
                continuityState.FailureCount = 0;
                continuityState.LastCheckTick = currentTick;
                if (previousWarnings != 0 || previousFailures != 0 || previousAlertCount != 0)
                {
                    continuityState.Version++;
                }
                return;
            }

            var entries = _directoryLookup[directoryEntity];
            if (entries.Length == 0)
            {
                continuityState.WarningCount = 0;
                continuityState.FailureCount = 0;
                continuityState.LastCheckTick = currentTick;
                if (previousWarnings != 0 || previousFailures != 0 || previousAlertCount != 0)
                {
                    continuityState.Version++;
                }
                return;
            }

            var syncState = SystemAPI.GetComponentRW<RegistrySpatialSyncState>(syncEntity).ValueRO;
            var publishedSpatialVersion = syncState.SpatialVersion;
            var hasSpatialData = syncState.HasSpatialData;

            var warningCount = 0;
            var failureCount = 0;
            var hasDefinitionCatalog = SystemAPI.TryGetSingleton(out RegistryDefinitionCatalog definitionCatalog) &&
                                       definitionCatalog.IsCreated &&
                                       definitionCatalog.Catalog.Value.Definitions.Length > 0;
            NativeParallelHashMap<Unity.Entities.Hash128, RegistryDefinition> definitionMap = default;
            if (hasDefinitionCatalog)
            {
                ref var definitions = ref definitionCatalog.Catalog.Value.Definitions;
                definitionMap = new NativeParallelHashMap<Unity.Entities.Hash128, RegistryDefinition>(definitions.Length, state.WorldUpdateAllocator);
                for (var defIndex = 0; defIndex < definitions.Length; defIndex++)
                {
                    var def = definitions[defIndex];
                    definitionMap.TryAdd(def.Id.Value, def);
                }
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var registryEntity = entry.Handle.RegistryEntity;
                if (!_metadataLookup.HasComponent(registryEntity))
                {
                    continue;
                }

                var metadata = _metadataLookup[registryEntity];
                var status = RegistryContinuityStatus.Nominal;
                var flags = RegistryHealthFlags.None;
                var delta = 0u;

                if (hasDefinitionCatalog && definitionMap.IsCreated && definitionMap.TryGetValue(metadata.Id.Value, out var definition))
                {
                    var expectedContinuity = definition.Continuity.WithDefaultsIfUnset();
                    var actualContinuity = metadata.ContinuityMeta.WithDefaultsIfUnset();
                    if (expectedContinuity.SchemaVersion != actualContinuity.SchemaVersion ||
                        expectedContinuity.Residency != actualContinuity.Residency ||
                        expectedContinuity.Category != actualContinuity.Category)
                    {
                        status = RegistryContinuityStatus.Failure;
                        flags |= RegistryHealthFlags.DefinitionMismatch;
                    }
                }

                var continuity = metadata.Continuity;
                var requireSpatialSync = metadata.SupportsSpatialQueries && continuity.RequiresSpatialSync;
                var hasContinuity = continuity.HasSpatialData;

                if (!requireSpatialSync && status == RegistryContinuityStatus.Nominal)
                {
                    continue;
                }

                var registrySpatialVersion = continuity.SpatialVersion;
                if (requireSpatialSync)
                {
                    if (!hasContinuity)
                    {
                        status = RegistryContinuityStatus.Failure;
                        flags |= RegistryHealthFlags.SpatialContinuityMissing;
                    }
                    else if (!hasSpatialData)
                    {
                        status = (RegistryContinuityStatus)math.max((int)status, (int)RegistryContinuityStatus.Warning);
                        flags |= RegistryHealthFlags.SpatialMismatchWarning;
                    }
                    else
                    {
                        delta = publishedSpatialVersion >= registrySpatialVersion
                            ? publishedSpatialVersion - registrySpatialVersion
                            : registrySpatialVersion - publishedSpatialVersion;

                        if (thresholds.SpatialVersionMismatchCritical > 0u &&
                            delta >= thresholds.SpatialVersionMismatchCritical)
                        {
                            status = RegistryContinuityStatus.Failure;
                            flags |= RegistryHealthFlags.SpatialMismatchCritical;
                        }
                        else if (thresholds.SpatialVersionMismatchWarning > 0u &&
                                 delta >= thresholds.SpatialVersionMismatchWarning)
                        {
                            status = (RegistryContinuityStatus)math.max((int)status, (int)RegistryContinuityStatus.Warning);
                            flags |= RegistryHealthFlags.SpatialMismatchWarning;
                        }
                    }
                }

                if (status == RegistryContinuityStatus.Nominal)
                {
                    continue;
                }

                if (_healthLookup.HasComponent(registryEntity))
                {
                    flags |= _healthLookup[registryEntity].FailureFlags;
                }

                if (status == RegistryContinuityStatus.Failure)
                {
                    failureCount++;
                }
                else
                {
                    warningCount++;
                }

                var handle = entry.Handle.WithVersion(metadata.Version);
                alertsBuffer.Add(new RegistryContinuityAlert
                {
                    Handle = handle,
                    Status = status,
                    SpatialVersion = publishedSpatialVersion,
                    RegistrySpatialVersion = registrySpatialVersion,
                    Delta = delta,
                    Flags = flags,
                    Label = metadata.Label
                });
            }

            // Validate custom registry participants
            var customWarnings = 0;
            var customFailures = 0;
            ValidateCustomParticipants(
                ref state,
                thresholds,
                publishedSpatialVersion,
                hasSpatialData,
                currentTick,
                ref customWarnings,
                ref customFailures);

            continuityState.WarningCount = warningCount + customWarnings;
            continuityState.FailureCount = failureCount + customFailures;
            continuityState.LastCheckTick = currentTick;

            if (previousAlertCount != alertsBuffer.Length ||
                previousWarnings != continuityState.WarningCount ||
                previousFailures != continuityState.FailureCount)
            {
                continuityState.Version++;
            }
        }

        private void ValidateCustomParticipants(
            ref SystemState state,
            in RegistryHealthThresholds thresholds,
            uint publishedSpatialVersion,
            bool hasSpatialData,
            uint currentTick,
            ref int warningCount,
            ref int failureCount)
        {
            // Find the participants singleton
            if (_participantsQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var participantsEntity = _participantsQuery.GetSingletonEntity();
            if (!_participantsLookup.HasBuffer(participantsEntity))
            {
                return;
            }

            var participants = _participantsLookup[participantsEntity];
            for (var i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (!participant.IsActiveFlag)
                {
                    continue;
                }

                var status = RegistryContinuityStatus.Nominal;
                var flags = RegistryHealthFlags.None;
                var delta = 0u;
                var errors = 0;
                var warnings = 0;

                // Validate spatial continuity if required
                if (participant.RequiresSpatialSyncFlag)
                {
                    if (!participant.Snapshot.HasSpatialData)
                    {
                        status = RegistryContinuityStatus.Failure;
                        flags |= RegistryHealthFlags.SpatialContinuityMissing;
                        errors++;
                    }
                    else if (!hasSpatialData)
                    {
                        status = (RegistryContinuityStatus)math.max((int)status, (int)RegistryContinuityStatus.Warning);
                        flags |= RegistryHealthFlags.SpatialMismatchWarning;
                        warnings++;
                    }
                    else
                    {
                        delta = publishedSpatialVersion >= participant.Snapshot.SpatialVersion
                            ? publishedSpatialVersion - participant.Snapshot.SpatialVersion
                            : participant.Snapshot.SpatialVersion - publishedSpatialVersion;

                        if (thresholds.SpatialVersionMismatchCritical > 0u &&
                            delta >= thresholds.SpatialVersionMismatchCritical)
                        {
                            status = RegistryContinuityStatus.Failure;
                            flags |= RegistryHealthFlags.SpatialMismatchCritical;
                            errors++;
                        }
                        else if (thresholds.SpatialVersionMismatchWarning > 0u &&
                                 delta >= thresholds.SpatialVersionMismatchWarning)
                        {
                            status = (RegistryContinuityStatus)math.max((int)status, (int)RegistryContinuityStatus.Warning);
                            flags |= RegistryHealthFlags.SpatialMismatchWarning;
                            warnings++;
                        }
                    }
                }

                // Update report buffer for this participant
                if (participant.ReportEntity != Entity.Null && _reportLookup.HasBuffer(participant.ReportEntity))
                {
                    var reportBuffer = _reportLookup[participant.ReportEntity];
                    reportBuffer.Clear();

                    if (status != RegistryContinuityStatus.Nominal)
                    {
                        reportBuffer.Add(new ContinuityValidationReport
                        {
                            RegistryEntity = participant.RegistryEntity,
                            Label = participant.Label,
                            Status = status,
                            Flags = flags,
                            SpatialVersion = publishedSpatialVersion,
                            RegistrySpatialVersion = participant.Snapshot.SpatialVersion,
                            Delta = delta,
                            Errors = errors,
                            Warnings = warnings,
                            ValidationTick = currentTick
                        });
                    }
                }

                if (status == RegistryContinuityStatus.Failure)
                {
                    failureCount++;
                }
                else if (status == RegistryContinuityStatus.Warning)
                {
                    warningCount++;
                }
            }
        }
    }
}
