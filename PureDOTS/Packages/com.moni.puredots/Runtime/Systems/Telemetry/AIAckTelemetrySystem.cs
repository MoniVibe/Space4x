using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Telemetry
{
    /// <summary>
    /// Emits high-signal metrics about ack activity (counts + drops).
    /// Ack payload/event export remains the job of TelemetryExportSystem (via TelemetryEvent),
    /// so this stays lightweight and budget-safe.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(AiTrainingTelemetrySystem))]
    [UpdateBefore(typeof(TelemetryExportSystem))]
    public partial struct AIAckTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricAckStreamCount = "ai.ack.stream.count";
        private static readonly FixedString64Bytes MetricAckIssued = "ai.ack.issued";
        private static readonly FixedString64Bytes MetricAckReceived = "ai.ack.received";
        private static readonly FixedString64Bytes MetricAckDropped = "ai.ack.dropped";

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

            var metrics = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            if (!SystemAPI.TryGetSingletonEntity<AIAckStreamTag>(out var ackEntity) || !state.EntityManager.HasBuffer<AIAckEvent>(ackEntity))
            {
                metrics.AddMetric(MetricAckStreamCount, 0f, TelemetryMetricUnit.Count);
                return;
            }

            var ackBuffer = state.EntityManager.GetBuffer<AIAckEvent>(ackEntity);
            metrics.AddMetric(MetricAckStreamCount, ackBuffer.Length, TelemetryMetricUnit.Count);

            // Aggregate by stage (bounded buffer; linear scan is fine).
            var issued = 0;
            var received = 0;
            for (int i = 0; i < ackBuffer.Length; i++)
            {
                var ev = ackBuffer[i];
                if (ev.Stage == AIAckStage.Issued) issued++;
                else if (ev.Stage == AIAckStage.Received) received++;
            }

            metrics.AddMetric(MetricAckIssued, issued, TelemetryMetricUnit.Count);
            metrics.AddMetric(MetricAckReceived, received, TelemetryMetricUnit.Count);

            if (SystemAPI.TryGetSingleton<PureDOTS.Runtime.Performance.UniversalPerformanceCounters>(out var counters))
            {
                metrics.AddMetric(MetricAckDropped, counters.AckEventsDroppedThisTick, TelemetryMetricUnit.Count);
            }
        }
    }
}


