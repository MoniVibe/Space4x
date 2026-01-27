using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Produces per-registry instrumentation samples for debug HUDs and telemetry bridges.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct RegistryInstrumentationSystem : ISystem
    {
        private ComponentLookup<RegistryMetadata> _metadataLookup;
        private ComponentLookup<RegistryHealth> _healthLookup;
        private BufferLookup<RegistryDirectoryEntry> _directoryLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RegistryDirectory>();

            _metadataLookup = state.GetComponentLookup<RegistryMetadata>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<RegistryHealth>(isReadOnly: true);
            _directoryLookup = state.GetBufferLookup<RegistryDirectoryEntry>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<RegistryDirectory>(out var directoryEntity))
            {
                return;
            }

            _metadataLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _directoryLookup.Update(ref state);

            if (!_directoryLookup.HasBuffer(directoryEntity))
            {
                return;
            }

            var entries = _directoryLookup[directoryEntity];
            var samples = state.EntityManager.GetBuffer<RegistryInstrumentationSample>(directoryEntity);
            samples.Clear();

            var instrumentationHandle = SystemAPI.GetComponentRW<RegistryInstrumentationState>(directoryEntity);
            var instrumentationState = instrumentationHandle.ValueRO;
            var previousSampleCount = instrumentationState.SampleCount;
            var previousHealthy = instrumentationState.HealthyCount;
            var previousWarning = instrumentationState.WarningCount;
            var previousCritical = instrumentationState.CriticalCount;
            var previousFailure = instrumentationState.FailureCount;

            var currentTick = 0u;
            if (SystemAPI.TryGetSingleton(out TimeState timeState))
            {
                currentTick = timeState.Tick;
            }

            var healthyCount = 0;
            var warningCount = 0;
            var criticalCount = 0;
            var failureCount = 0;

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var registryEntity = entry.Handle.RegistryEntity;
                if (!_metadataLookup.HasComponent(registryEntity))
                {
                    continue;
                }

                var metadata = _metadataLookup[registryEntity];
                var handle = entry.Handle.WithVersion(metadata.Version);

                var healthLevel = RegistryHealthLevel.Healthy;
                var healthFlags = RegistryHealthFlags.None;
                var spatialDelta = 0u;

                if (_healthLookup.HasComponent(registryEntity))
                {
                    var health = _healthLookup[registryEntity];
                    healthLevel = health.HealthLevel;
                    healthFlags = health.FailureFlags;
                    spatialDelta = health.SpatialVersionDelta;
                }

                switch (healthLevel)
                {
                    case RegistryHealthLevel.Healthy:
                        healthyCount++;
                        break;
                    case RegistryHealthLevel.Warning:
                        warningCount++;
                        break;
                    case RegistryHealthLevel.Critical:
                        criticalCount++;
                        break;
                    case RegistryHealthLevel.Failure:
                        failureCount++;
                        break;
                }

                samples.Add(new RegistryInstrumentationSample
                {
                    Handle = handle,
                    HealthLevel = healthLevel,
                    HealthFlags = healthFlags,
                    EntryCount = metadata.EntryCount,
                    Version = metadata.Version,
                    LastUpdateTick = metadata.LastUpdateTick,
                    SpatialVersion = metadata.Continuity.SpatialVersion,
                    SpatialVersionDelta = spatialDelta,
                    TelemetryKey = metadata.TelemetryKey,
                    Label = metadata.Label
                });
            }

            instrumentationState.LastUpdateTick = currentTick;
            instrumentationState.SampleCount = samples.Length;
            instrumentationState.HealthyCount = healthyCount;
            instrumentationState.WarningCount = warningCount;
            instrumentationState.CriticalCount = criticalCount;
            instrumentationState.FailureCount = failureCount;

            if (previousSampleCount != samples.Length ||
                previousHealthy != healthyCount ||
                previousWarning != warningCount ||
                previousCritical != criticalCount ||
                previousFailure != failureCount)
            {
                instrumentationState.Version++;
            }

            instrumentationHandle.ValueRW = instrumentationState;
        }
    }
}
