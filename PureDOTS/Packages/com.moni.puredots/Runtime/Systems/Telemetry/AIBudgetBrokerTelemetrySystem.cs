using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Telemetry
{
    /// <summary>
    /// Emits NDJSON metrics for budget broker state/counters (headless + perf regression friendly).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(AiTrainingTelemetrySystem))]
    [UpdateBefore(typeof(TelemetryExportSystem))]
    public partial struct AIBudgetBrokerTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricLosRemaining = "budget.los.remaining";
        private static readonly FixedString64Bytes MetricLosDeferred = "budget.los.deferred";
        private static readonly FixedString64Bytes MetricLosAttempted = "budget.los.attempted";
        private static readonly FixedString64Bytes MetricLosGranted = "budget.los.granted";
        private static readonly FixedString64Bytes MetricLosDeferredCounter = "budget.los.deferredCounter";

        private static readonly FixedString64Bytes MetricDecisionRemaining = "budget.decision.remaining";
        private static readonly FixedString64Bytes MetricDecisionDeferred = "budget.decision.deferred";

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

            if (!SystemAPI.TryGetSingleton<AIBudgetBrokerState>(out var broker))
            {
                return;
            }

            var metrics = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            metrics.AddMetric(MetricLosRemaining, broker.RemainingLosRays, TelemetryMetricUnit.Count);
            metrics.AddMetric(MetricLosDeferred, broker.DeferredLosRays, TelemetryMetricUnit.Count);
            metrics.AddMetric(MetricDecisionRemaining, broker.RemainingDecisionUpdates, TelemetryMetricUnit.Count);
            metrics.AddMetric(MetricDecisionDeferred, broker.DeferredDecisions, TelemetryMetricUnit.Count);

            if (SystemAPI.TryGetSingleton<UniversalPerformanceCounters>(out var counters))
            {
                metrics.AddMetric(MetricLosAttempted, counters.LosRaysAttemptedThisTick, TelemetryMetricUnit.Count);
                metrics.AddMetric(MetricLosGranted, counters.LosRaysGrantedThisTick, TelemetryMetricUnit.Count);
                metrics.AddMetric(MetricLosDeferredCounter, counters.LosRaysDeferredThisTick, TelemetryMetricUnit.Count);
            }
        }
    }
}


