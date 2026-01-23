using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4X.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless proof that the ARC-FLEET reaches the arc exit waypoint for arc_micro.
    /// Logs exactly one PASS/FAIL line and can request exit when configured.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct Space4XArcProofSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_HEADLESS_ARC_PROOF";
        private const string ExitOnResultEnv = "SPACE4X_HEADLESS_ARC_PROOF_EXIT";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string ArcScenarioFile = "space4x_arc_micro.json";
        private const float TargetX = 120f;
        private const float TargetZ = 520f;
        private const float TargetTolerance = 140f;
        private const float MinZ = 300f;
        private static readonly FixedString64Bytes ArcCarrierId = new FixedString64Bytes("arc-carrier-alpha");
        private static readonly FixedString64Bytes ArcTestId = new FixedString64Bytes("S1.SPACE4X_ARC_MICRO");

        private byte _enabled;
        private byte _done;
        private byte _bankResolved;
        private byte _bankLogged;
        private byte _telemetryLogged;
        private FixedString64Bytes _bankTestId;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            var enabled = SystemEnv.GetEnvironmentVariable(EnabledEnv);
            if (string.Equals(enabled, "0", StringComparison.OrdinalIgnoreCase))
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<Carrier>();
            state.RequireForUpdate<LocalTransform>();
            state.RequireForUpdate<TelemetryStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_enabled == 0 || _done != 0)
            {
                return;
            }

            if (!ResolveBankTestId())
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

            var found = false;
            float3 position = float3.zero;
            foreach (var (carrier, transform) in SystemAPI.Query<RefRO<Carrier>, RefRO<LocalTransform>>().WithNone<Prefab>())
            {
                if (!carrier.ValueRO.CarrierId.Equals(ArcCarrierId))
                {
                    continue;
                }

                position = transform.ValueRO.Position;
                found = true;
                break;
            }

            if (!found)
            {
                TryEmitTelemetry(ref state, timeState.Tick, float.NaN, false);
                Fail(ref state, timeState.Tick, "missing_carrier");
                return;
            }

            var pos2 = new float2(position.x, position.z);
            var target2 = new float2(TargetX, TargetZ);
            var dist = math.distance(pos2, target2);
            if (dist <= TargetTolerance && position.z >= MinZ)
            {
                TryEmitTelemetry(ref state, timeState.Tick, dist, true);
                Pass(ref state, timeState.Tick);
                return;
            }

            TryEmitTelemetry(ref state, timeState.Tick, dist, false);
            Fail(ref state, timeState.Tick, "target_not_reached");
        }

        private void TryEmitTelemetry(ref SystemState state, uint tick, float distance, bool reached)
        {
            if (_telemetryLogged != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!TryGetTelemetryMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.AddMetric("space4x.arc.distance_to_target", float.IsNaN(distance) ? -1f : distance, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.arc.target_reached", reached ? 1f : 0f, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.arc.target_reached_tick", reached ? tick : 0f, TelemetryMetricUnit.Custom);
            _telemetryLogged = 1;
        }

        private void Pass(ref SystemState state, uint tick)
        {
            _done = 1;
            LogBankResult(ref state, true, "pass", tick);
            ExitIfRequested(ref state, tick, 0);
        }

        private void Fail(ref SystemState state, uint tick, string reason)
        {
            _done = 1;
            LogBankResult(ref state, false, reason, tick);
            ExitIfRequested(ref state, tick, 4);
        }

        private static void ExitIfRequested(ref SystemState state, uint tick, int exitCode)
        {
            if (!string.Equals(SystemEnv.GetEnvironmentVariable(ExitOnResultEnv), "1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HeadlessExitUtility.Request(state.EntityManager, tick, exitCode);
        }

        private bool ResolveBankTestId()
        {
            if (_bankResolved != 0)
            {
                return !_bankTestId.IsEmpty;
            }

            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return false;
            }

            _bankResolved = 1;
            if (scenarioPath.EndsWith(ArcScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _bankTestId = ArcTestId;
                return true;
            }

            _enabled = 0;
            return false;
        }

        private void LogBankResult(ref SystemState state, bool pass, string reason, uint tick)
        {
            if (_bankLogged != 0 || _bankTestId.IsEmpty)
            {
                return;
            }

            ResolveTickInfo(ref state, tick, out var tickTime, out var scenarioTick);
            var delta = (int)tickTime - (int)scenarioTick;
            _bankLogged = 1;

            if (pass)
            {
                UnityDebug.Log($"BANK:{_bankTestId}:PASS tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
                return;
            }

            UnityDebug.Log($"BANK:{_bankTestId}:FAIL reason={reason} tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
        }

        private void ResolveTickInfo(ref SystemState state, uint tick, out uint tickTime, out uint scenarioTick)
        {
            tickTime = tick;
            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState))
            {
                tickTime = tickTimeState.Tick;
            }

            scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenario)
                ? scenario.Tick
                : 0u;
        }

        private bool TryGetTelemetryMetricBuffer(ref SystemState state, out DynamicBuffer<TelemetryMetric> buffer)
        {
            buffer = default;
            if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var telemetryRef))
            {
                return false;
            }

            if (telemetryRef.Stream == Entity.Null || !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryRef.Stream))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryRef.Stream);
            return true;
        }
    }
}
