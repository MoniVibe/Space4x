using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4x.Scenario;
using Unity.Entities;
using UnityEngine;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    /// <summary>
    /// Emits one-shot movement observe summary metrics at scenario end.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct Space4XMovementObserveSummarySystem : ISystem
    {
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string ObserveScenarioFile = "space4x_movement_observe_micro.json";

        private byte _enabled;
        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryExportConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_enabled == 0 || _done != 0)
            {
                return;
            }

            if (!ResolveScenarioGate())
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var scenario = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < scenario.EndTick)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                return;
            }

            if (!state.EntityManager.HasComponent<Space4XMovementObserveAccumulator>(telemetryEntity))
            {
                return;
            }

            var acc = state.EntityManager.GetComponentData<Space4XMovementObserveAccumulator>(telemetryEntity);
            if (!state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                state.EntityManager.AddBuffer<TelemetryMetric>(telemetryEntity);
            }

            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            EmitBucket(buffer, "carrier", acc.CarrierCount, acc.CarrierTimeToTargetSum, acc.CarrierOvershootSum,
                acc.CarrierSettleTimeSum, acc.CarrierPeakLateralSum, acc.CarrierTurnTimeSum);
            EmitBucket(buffer, "miner", acc.MinerCount, acc.MinerTimeToTargetSum, acc.MinerOvershootSum,
                acc.MinerSettleTimeSum, acc.MinerPeakLateralSum, acc.MinerTurnTimeSum);
            EmitBucket(buffer, "strike", acc.StrikeCount, acc.StrikeTimeToTargetSum, acc.StrikeOvershootSum,
                acc.StrikeSettleTimeSum, acc.StrikePeakLateralSum, acc.StrikeTurnTimeSum);

            _done = 1;
        }

        private static void EmitBucket(DynamicBuffer<TelemetryMetric> buffer, string label, uint count,
            float timeToTargetSum, float overshootSum, float settleTimeSum, float lateralSum, float turnTimeSum)
        {
            var denom = count > 0 ? count : 1u;
            buffer.AddMetric($"space4x.movement.observe.final.{label}.count", count, TelemetryMetricUnit.Count);
            buffer.AddMetric($"space4x.movement.observe.final.{label}.time_to_target_s", timeToTargetSum / denom, TelemetryMetricUnit.Custom);
            buffer.AddMetric($"space4x.movement.observe.final.{label}.overshoot_distance", overshootSum / denom, TelemetryMetricUnit.Custom);
            buffer.AddMetric($"space4x.movement.observe.final.{label}.settle_time_s", settleTimeSum / denom, TelemetryMetricUnit.Custom);
            buffer.AddMetric($"space4x.movement.observe.final.{label}.peak_lateral_speed", lateralSum / denom, TelemetryMetricUnit.Custom);
            buffer.AddMetric($"space4x.movement.observe.final.{label}.turn_time_s", turnTimeSum / denom, TelemetryMetricUnit.Custom);
        }

        private bool ResolveScenarioGate()
        {
            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                _enabled = 0;
                return false;
            }

            if (scenarioPath.EndsWith(ObserveScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            _enabled = 0;
            return false;
        }
    }
}
