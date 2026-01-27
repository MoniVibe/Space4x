using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Telemetry
{
    /// <summary>
    /// Lightweight comms metrics for headless regression + budget tuning.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(AiTrainingTelemetrySystem))]
    [UpdateBefore(typeof(TelemetryExportSystem))]
    public partial struct CommsTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricCommsEmitted = "comms.messages.emitted";
        private static readonly FixedString64Bytes MetricCommsDropped = "comms.messages.dropped";
        private static readonly FixedString64Bytes MetricCommsReceipts = "comms.receipts";

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var exportConfig) || exportConfig.Enabled == 0)
            {
                return;
            }

            if ((exportConfig.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<UniversalPerformanceCounters>(out var counters))
            {
                return;
            }

            var metrics = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            metrics.AddMetric(MetricCommsEmitted, counters.CommsMessagesEmittedThisTick, TelemetryMetricUnit.Count);
            metrics.AddMetric(MetricCommsDropped, counters.CommsMessagesDroppedThisTick, TelemetryMetricUnit.Count);
            metrics.AddMetric(MetricCommsReceipts, counters.CommsReceiptsThisTick, TelemetryMetricUnit.Count);
        }
    }
}


