using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Publishes module health/queue counts to the telemetry stream for HUD/observability.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ModuleRepairSystem))]
    public partial struct ModuleTelemetrySystem : ISystem
    {
        private FixedString64Bytes _degradedKey;
        private FixedString64Bytes _failedKey;
        private FixedString64Bytes _queueKey;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            _degradedKey = CreateDegradedKey();
            _failedKey = CreateFailedKey();
            _queueKey = CreateQueueKey();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            if (!state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return;
            }

            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            int degraded = 0;
            int failed = 0;
            int queued = 0;

            foreach (var (aggregate, tickets) in SystemAPI.Query<RefRO<CarrierModuleAggregate>, DynamicBuffer<ModuleRepairTicket>>())
            {
                degraded += aggregate.ValueRO.DegradedCount;
                failed += aggregate.ValueRO.FailedCount;
                queued += tickets.Length;
            }

            buffer.AddMetric(_degradedKey, degraded, TelemetryMetricUnit.Count);
            buffer.AddMetric(_failedKey, failed, TelemetryMetricUnit.Count);
            buffer.AddMetric(_queueKey, queued, TelemetryMetricUnit.Count);
        }

        private static FixedString64Bytes CreateDegradedKey()
        {
            FixedString64Bytes key = default;
            key.Append('s'); key.Append('p'); key.Append('a'); key.Append('c'); key.Append('e'); key.Append('4'); key.Append('x'); key.Append('.'); key.Append('m'); key.Append('o'); key.Append('d'); key.Append('u'); key.Append('l'); key.Append('e'); key.Append('s'); key.Append('.'); key.Append('d'); key.Append('e'); key.Append('g'); key.Append('r'); key.Append('a'); key.Append('d'); key.Append('e'); key.Append('d');
            return key;
        }

        private static FixedString64Bytes CreateFailedKey()
        {
            FixedString64Bytes key = default;
            key.Append('s'); key.Append('p'); key.Append('a'); key.Append('c'); key.Append('e'); key.Append('4'); key.Append('x'); key.Append('.'); key.Append('m'); key.Append('o'); key.Append('d'); key.Append('u'); key.Append('l'); key.Append('e'); key.Append('s'); key.Append('.'); key.Append('f'); key.Append('a'); key.Append('i'); key.Append('l'); key.Append('e'); key.Append('d');
            return key;
        }

        private static FixedString64Bytes CreateQueueKey()
        {
            FixedString64Bytes key = default;
            key.Append('s'); key.Append('p'); key.Append('a'); key.Append('c'); key.Append('e'); key.Append('4'); key.Append('x'); key.Append('.'); key.Append('m'); key.Append('o'); key.Append('d'); key.Append('u'); key.Append('l'); key.Append('e'); key.Append('s'); key.Append('.'); key.Append('r'); key.Append('e'); key.Append('p'); key.Append('a'); key.Append('i'); key.Append('r'); key.Append('.'); key.Append('q'); key.Append('u'); key.Append('e'); key.Append('u'); key.Append('e');
            return key;
        }
    }
}
