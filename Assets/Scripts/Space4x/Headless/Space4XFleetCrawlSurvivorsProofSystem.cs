using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless proof for FleetCrawl Survivors v1.
    /// Passes if at least one friendly carrier survives to scenario end.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct Space4XFleetCrawlSurvivorsProofSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_HEADLESS_SURVIVORS_PROOF";
        private const string ExitOnResultEnv = "SPACE4X_HEADLESS_SURVIVORS_PROOF_EXIT";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string ScenarioFile = "space4x_fleetcrawl_survivors_v1.json";
        private const float AliveHullThreshold = 0.01f;
        private const float DamagedHullThreshold = 0.01f;

        private static readonly FixedString64Bytes ScenarioId = new FixedString64Bytes("space4x_fleetcrawl_survivors_v1");
        private static readonly FixedString64Bytes SurvivorsTestId = new FixedString64Bytes("S1.SPACE4X_FLEETCRAWL_SURVIVORS");

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

            if (string.Equals(SystemEnv.GetEnvironmentVariable(EnabledEnv), "0", StringComparison.OrdinalIgnoreCase))
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<Carrier>();
            state.RequireForUpdate<ScenarioSide>();
            state.RequireForUpdate<HullIntegrity>();
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

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            var friendlyTotal = 0;
            var friendlyAlive = 0;
            var hostileTotal = 0;
            var hostileAlive = 0;
            var damageSignalCount = 0;

            foreach (var (side, hull) in SystemAPI.Query<RefRO<ScenarioSide>, RefRO<HullIntegrity>>().WithNone<Prefab>())
            {
                var isAlive = hull.ValueRO.Current > AliveHullThreshold;
                var damaged = hull.ValueRO.Current < (hull.ValueRO.Max - DamagedHullThreshold);

                if (side.ValueRO.Side == 0)
                {
                    friendlyTotal++;
                    if (isAlive)
                    {
                        friendlyAlive++;
                    }
                }
                else
                {
                    hostileTotal++;
                    if (isAlive)
                    {
                        hostileAlive++;
                    }
                }

                if (damaged)
                {
                    damageSignalCount++;
                }
            }

            if (friendlyTotal <= 0)
            {
                Fail(ref state, timeState.Tick, "missing_friendly");
                return;
            }

            if (hostileTotal <= 0)
            {
                Fail(ref state, timeState.Tick, "missing_hostile");
                return;
            }

            var friendlySurvival = friendlyTotal > 0 ? (float)friendlyAlive / friendlyTotal : 0f;
            var hostileAttrition = hostileTotal > 0 ? 1f - ((float)hostileAlive / hostileTotal) : 0f;

            TryEmitTelemetry(ref state, friendlyTotal, friendlyAlive, hostileTotal, hostileAlive, friendlySurvival, hostileAttrition, damageSignalCount);

            if (friendlyAlive > 0)
            {
                UnityDebug.Log(
                    $"[Space4XFleetCrawlSurvivorsProof] PASS tick={timeState.Tick} friendlyAlive={friendlyAlive}/{friendlyTotal} survival={friendlySurvival:F3} hostileAlive={hostileAlive}/{hostileTotal} hostileAttrition={hostileAttrition:F3} damageSignals={damageSignalCount}");
                Pass(ref state, timeState.Tick);
                return;
            }

            UnityDebug.LogError(
                $"[Space4XFleetCrawlSurvivorsProof] FAIL tick={timeState.Tick} reason=no_friendly_survivors friendlyAlive={friendlyAlive}/{friendlyTotal} hostileAlive={hostileAlive}/{hostileTotal}");
            Fail(ref state, timeState.Tick, "no_friendly_survivors");
        }

        private void TryEmitTelemetry(
            ref SystemState state,
            int friendlyTotal,
            int friendlyAlive,
            int hostileTotal,
            int hostileAlive,
            float friendlySurvival,
            float hostileAttrition,
            int damageSignalCount)
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

            buffer.AddMetric("space4x.survivors.friendly_total", friendlyTotal, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.survivors.friendly_alive", friendlyAlive, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.survivors.friendly_survival_ratio", friendlySurvival, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.survivors.hostile_total", hostileTotal, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.survivors.hostile_alive", hostileAlive, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.survivors.hostile_attrition_ratio", hostileAttrition, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.survivors.damage_signal_count", damageSignalCount, TelemetryMetricUnit.Count);
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

            _bankResolved = 1;

            var scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(scenarioPath) &&
                scenarioPath.EndsWith(ScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                _bankTestId = SurvivorsTestId;
                return true;
            }

            if (SystemAPI.TryGetSingleton<ScenarioInfo>(out var info) && info.ScenarioId.Equals(ScenarioId))
            {
                _bankTestId = SurvivorsTestId;
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

            ResolveTickInfo(tick, out var tickTime, out var scenarioTick);
            var delta = (int)tickTime - (int)scenarioTick;
            _bankLogged = 1;

            if (pass)
            {
                UnityDebug.Log($"BANK:{_bankTestId}:PASS tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
                return;
            }

            UnityDebug.Log($"BANK:{_bankTestId}:FAIL reason={reason} tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
        }

        private void ResolveTickInfo(uint tick, out uint tickTime, out uint scenarioTick)
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
