using PureDOTS.Runtime.Alignment;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Surfaces compliance alerts for intel/telemetry and clamps counts for HUD.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CrewAggregationSystem))]
    public partial struct AffiliationComplianceSystem : ISystem
    {
        private FixedString64Bytes _alertKey;
        private FixedString64Bytes _nominalKey;
        private FixedString64Bytes _warningKey;
        private FixedString64Bytes _breachKey;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CrewCompliance>();
            _alertKey = CreateAlertKey();
            _nominalKey = CreateNominalKey();
            _warningKey = CreateWarningKey();
            _breachKey = CreateBreachKey();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var telemetryExists = SystemAPI.HasSingleton<TelemetryStream>();
            DynamicBuffer<TelemetryMetric> buffer = default;

            if (telemetryExists && SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                if (state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
                {
                    buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
                }
            }

            int nominal = 0;
            int warning = 0;
            int breach = 0;

            foreach (var (compliance, alerts) in SystemAPI.Query<RefRO<CrewCompliance>, DynamicBuffer<ComplianceAlert>>())
            {
                switch (compliance.ValueRO.Status)
                {
                    case ComplianceStatus.Nominal:
                        nominal++;
                        break;
                    case ComplianceStatus.Warning:
                        warning++;
                        break;
                    case ComplianceStatus.Breach:
                        breach++;
                        break;
                }

                if (telemetryExists && buffer.IsCreated && alerts.IsCreated && alerts.Length > 0)
                {
                    var alert = alerts[alerts.Length - 1];
                    buffer.Add(new TelemetryMetric
                    {
                        Key = _alertKey,
                        Value = (int)alert.Status,
                        Unit = TelemetryMetricUnit.Count
                    });
                }
            }

            if (telemetryExists && buffer.IsCreated)
            {
                buffer.Add(new TelemetryMetric { Key = _nominalKey, Value = nominal, Unit = TelemetryMetricUnit.Count });
                buffer.Add(new TelemetryMetric { Key = _warningKey, Value = warning, Unit = TelemetryMetricUnit.Count });
                buffer.Add(new TelemetryMetric { Key = _breachKey, Value = breach, Unit = TelemetryMetricUnit.Count });
            }
        }

        private static FixedString64Bytes CreateAlertKey()
        {
            FixedString64Bytes fs = default;
            fs.Append('c'); fs.Append('o'); fs.Append('m'); fs.Append('p'); fs.Append('l'); fs.Append('i'); fs.Append('a'); fs.Append('n'); fs.Append('c'); fs.Append('e'); fs.Append('.'); fs.Append('a'); fs.Append('l'); fs.Append('e'); fs.Append('r'); fs.Append('t');
            return fs;
        }

        private static FixedString64Bytes CreateNominalKey()
        {
            FixedString64Bytes fs = default;
            fs.Append('c'); fs.Append('o'); fs.Append('m'); fs.Append('p'); fs.Append('l'); fs.Append('i'); fs.Append('a'); fs.Append('n'); fs.Append('c'); fs.Append('e'); fs.Append('.'); fs.Append('n'); fs.Append('o'); fs.Append('m'); fs.Append('i'); fs.Append('n'); fs.Append('a'); fs.Append('l');
            return fs;
        }

        private static FixedString64Bytes CreateWarningKey()
        {
            FixedString64Bytes fs = default;
            fs.Append('c'); fs.Append('o'); fs.Append('m'); fs.Append('p'); fs.Append('l'); fs.Append('i'); fs.Append('a'); fs.Append('n'); fs.Append('c'); fs.Append('e'); fs.Append('.'); fs.Append('w'); fs.Append('a'); fs.Append('r'); fs.Append('n'); fs.Append('i'); fs.Append('n'); fs.Append('g');
            return fs;
        }

        private static FixedString64Bytes CreateBreachKey()
        {
            FixedString64Bytes fs = default;
            fs.Append('c'); fs.Append('o'); fs.Append('m'); fs.Append('p'); fs.Append('l'); fs.Append('i'); fs.Append('a'); fs.Append('n'); fs.Append('c'); fs.Append('e'); fs.Append('.'); fs.Append('b'); fs.Append('r'); fs.Append('e'); fs.Append('a'); fs.Append('c'); fs.Append('h');
            return fs;
        }
    }
}
