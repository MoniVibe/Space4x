using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Aggregates compliance and suspicion signals into telemetry and the registry snapshot.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAffiliationComplianceSystem))]
    public partial struct Space4XComplianceTelemetrySystem : ISystem
    {
        private EntityQuery _breachQuery;
        private EntityQuery _suspicionQuery;

        private FixedString64Bytes _breachKey;
        private FixedString64Bytes _mutinyKey;
        private FixedString64Bytes _desertionKey;
        private FixedString64Bytes _independenceKey;
        private FixedString64Bytes _severityKey;
        private FixedString64Bytes _suspicionKey;
        private FixedString64Bytes _spySuspicionKey;
        private FixedString64Bytes _suspicionAlertKey;
        private FixedString64Bytes _suspicionMaxKey;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XRegistrySnapshot>();
            state.RequireForUpdate<TimeState>();

            _breachQuery = SystemAPI.QueryBuilder()
                .WithAll<ComplianceBreach>()
                .Build();

            _suspicionQuery = SystemAPI.QueryBuilder()
                .WithAll<SuspicionScore>()
                .Build();

            _breachKey = "space4x.compliance.breaches";
            _mutinyKey = "space4x.compliance.mutiny";
            _desertionKey = "space4x.compliance.desertion";
            _independenceKey = "space4x.compliance.independence";
            _severityKey = "space4x.compliance.severity.avg";
            _suspicionKey = "space4x.compliance.suspicion.mean";
            _spySuspicionKey = "space4x.compliance.suspicion.spyMean";
            _suspicionAlertKey = "space4x.compliance.suspicion.alerts";
            _suspicionMaxKey = "space4x.compliance.suspicion.max";
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();

            int breachCount = 0;
            int mutinyCount = 0;
            int desertionCount = 0;
            int independenceCount = 0;
            float severitySum = 0f;
            int severitySamples = 0;

            if (!_breachQuery.IsEmptyIgnoreFilter)
            {
                foreach (var breaches in SystemAPI.Query<DynamicBuffer<ComplianceBreach>>())
                {
                    if (!breaches.IsCreated || breaches.Length == 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < breaches.Length; i++)
                    {
                        var breach = breaches[i];
                        breachCount++;
                        severitySum += (float)breach.Severity;
                        severitySamples++;
                        switch (breach.Type)
                        {
                            case ComplianceBreachType.Mutiny:
                                mutinyCount++;
                                break;
                            case ComplianceBreachType.Desertion:
                                desertionCount++;
                                break;
                            case ComplianceBreachType.Independence:
                                independenceCount++;
                                break;
                        }
                    }
                }
            }

            float suspicionSum = 0f;
            float spySuspicionSum = 0f;
            float maxSuspicion = 0f;
            int suspicionAlerts = 0;
            int suspicionSamples = 0;
            int spySamples = 0;

            if (!_suspicionQuery.IsEmptyIgnoreFilter)
            {
                foreach (var (suspicion, entity) in SystemAPI.Query<RefRO<SuspicionScore>>().WithEntityAccess())
                {
                    var value = math.max(0f, (float)suspicion.ValueRO.Value);
                    suspicionSum += value;
                    suspicionSamples++;
                    maxSuspicion = math.max(maxSuspicion, value);
                    if (value >= 0.25f)
                    {
                        suspicionAlerts++;
                    }

                    if (SystemAPI.HasComponent<SpyRole>(entity))
                    {
                        spySuspicionSum += value;
                        spySamples++;
                    }
                }
            }

            var snapshotEntity = SystemAPI.GetSingletonEntity<Space4XRegistrySnapshot>();
            var snapshot = SystemAPI.GetComponentRW<Space4XRegistrySnapshot>(snapshotEntity);

            snapshot.ValueRW.ComplianceBreachCount = breachCount;
            snapshot.ValueRW.ComplianceMutinyCount = mutinyCount;
            snapshot.ValueRW.ComplianceDesertionCount = desertionCount;
            snapshot.ValueRW.ComplianceIndependenceCount = independenceCount;
            snapshot.ValueRW.ComplianceAverageSeverity = severitySamples > 0 ? severitySum / severitySamples : 0f;
            snapshot.ValueRW.ComplianceAverageSuspicion = suspicionSamples > 0 ? suspicionSum / suspicionSamples : 0f;
            snapshot.ValueRW.ComplianceAverageSpySuspicion = spySamples > 0 ? spySuspicionSum / spySamples : 0f;
            snapshot.ValueRW.ComplianceMaxSuspicion = maxSuspicion;
            snapshot.ValueRW.ComplianceSuspicionAlertCount = suspicionAlerts;
            snapshot.ValueRW.ComplianceLastUpdateTick = timeState.Tick;

            if (SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity) &&
                state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
                buffer.AddMetric(_breachKey, breachCount);
                buffer.AddMetric(_mutinyKey, mutinyCount);
                buffer.AddMetric(_desertionKey, desertionCount);
                buffer.AddMetric(_independenceKey, independenceCount);
                buffer.AddMetric(_severityKey, snapshot.ValueRO.ComplianceAverageSeverity, TelemetryMetricUnit.Ratio);
                buffer.AddMetric(_suspicionKey, snapshot.ValueRO.ComplianceAverageSuspicion, TelemetryMetricUnit.Ratio);
                buffer.AddMetric(_spySuspicionKey, snapshot.ValueRO.ComplianceAverageSpySuspicion, TelemetryMetricUnit.Ratio);
                buffer.AddMetric(_suspicionAlertKey, suspicionAlerts);
                buffer.AddMetric(_suspicionMaxKey, maxSuspicion, TelemetryMetricUnit.Ratio);
            }
        }
    }
}
