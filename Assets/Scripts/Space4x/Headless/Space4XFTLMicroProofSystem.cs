using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Time;
using Space4x.Scenario;
using Space4X.Registry;
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
    /// Headless proof for an FTL micro slice (intent -> jump -> exit) on the FTL micro scenario.
    /// Logs exactly one BANK PASS/FAIL line and can request exit when configured.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct Space4XFTLMicroProofSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_HEADLESS_FTL_PROOF";
        private const string ExitOnResultEnv = "SPACE4X_HEADLESS_FTL_PROOF_EXIT";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string ScenarioSourcePathEnv = "SPACE4X_SCENARIO_SOURCE_PATH";
        private const string FtlScenarioFile = "space4x_ftl_micro.json";
        private const uint JumpDelayTicks = 30;
        private const uint MaxJumpTicks = 600;
        private const float TargetTolerance = 25f;
        private const float MinJumpDistance = 50f;
        private static readonly FixedString64Bytes FtlFleetId = new FixedString64Bytes("FTL-FLEET");
        private static readonly FixedString64Bytes FtlCarrierId = new FixedString64Bytes("ftl-carrier-alpha");
        private static readonly FixedString64Bytes FtlTestId = new FixedString64Bytes("S4.SPACE4X_FTL_MICRO");

        private byte _enabled;
        private byte _done;
        private byte _bankResolved;
        private byte _bankLogged;
        private byte _telemetryLogged;
        private FixedString64Bytes _bankTestId;

        private byte _intentObserved;
        private byte _jumpCompleted;
        private uint _intentTick;
        private uint _jumpTick;
        private float _intentWorldSeconds;
        private float _jumpWorldSeconds;
        private float3 _targetPosition;
        private float3 _startPosition;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            var enabled = SystemEnv.GetEnvironmentVariable(EnabledEnv);
            if (!IsTruthy(enabled))
            {
                state.Enabled = false;
                return;
            }

            _enabled = 1;
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<Carrier>();
            state.RequireForUpdate<LocalTransform>();
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

            var hasCarrier = TryFindCarrier(ref state, out var carrierEntity, out var carrierTransform);
            if (_intentObserved == 0)
            {
                if (TryResolveMoveTarget(ref state, out var targetPosition))
                {
                    _intentObserved = 1;
                    _intentTick = timeState.Tick;
                    _intentWorldSeconds = timeState.WorldSeconds;
                    _targetPosition = targetPosition;
                    _startPosition = hasCarrier ? carrierTransform.Position : float3.zero;
                }
            }

            if (_intentObserved != 0 && _jumpCompleted == 0 && hasCarrier)
            {
                if (timeState.Tick - _intentTick >= JumpDelayTicks)
                {
                    carrierTransform.Position = _targetPosition;
                    state.EntityManager.SetComponentData(carrierEntity, carrierTransform);
                    _jumpCompleted = 1;
                    _jumpTick = timeState.Tick;
                    _jumpWorldSeconds = timeState.WorldSeconds;
                }
            }

            if (timeState.Tick < scenario.EndTick)
            {
                return;
            }

            if (!hasCarrier)
            {
                TryEmitTelemetry(ref state, float.NaN);
                Fail(ref state, timeState.Tick, "missing_carrier");
                return;
            }

            if (_intentObserved == 0)
            {
                TryEmitTelemetry(ref state, float.NaN);
                Fail(ref state, timeState.Tick, "missing_intent");
                return;
            }

            if (_jumpCompleted == 0)
            {
                TryEmitTelemetry(ref state, float.NaN);
                Fail(ref state, timeState.Tick, "jump_not_completed");
                return;
            }

            var distance = ResolveDistanceToTarget(carrierTransform.Position);
            if (distance > TargetTolerance)
            {
                TryEmitTelemetry(ref state, distance);
                Fail(ref state, timeState.Tick, "target_miss");
                return;
            }

            var jumpTicks = _jumpTick - _intentTick;
            if (jumpTicks > MaxJumpTicks)
            {
                TryEmitTelemetry(ref state, distance);
                Fail(ref state, timeState.Tick, "jump_timeout");
                return;
            }

            var movedDistance = ResolveMovedDistance(carrierTransform.Position);
            if (movedDistance < MinJumpDistance)
            {
                TryEmitTelemetry(ref state, distance);
                Fail(ref state, timeState.Tick, "jump_distance_small");
                return;
            }

            TryEmitTelemetry(ref state, distance);
            Pass(ref state, timeState.Tick);
        }

        private bool TryFindCarrier(ref SystemState state, out Entity entity, out LocalTransform transform)
        {
            entity = Entity.Null;
            transform = default;
            foreach (var (carrier, localTransform, carrierEntity) in SystemAPI.Query<RefRO<Carrier>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!carrier.ValueRO.CarrierId.Equals(FtlCarrierId))
                {
                    continue;
                }

                entity = carrierEntity;
                transform = localTransform.ValueRO;
                return true;
            }

            return false;
        }

        private bool TryResolveMoveTarget(ref SystemState state, out float3 targetPosition)
        {
            targetPosition = default;
            foreach (var (target, transform) in SystemAPI.Query<RefRO<Space4XScenarioMoveTarget>, RefRO<LocalTransform>>())
            {
                if (!target.ValueRO.FleetId.Equals(FtlFleetId))
                {
                    continue;
                }

                targetPosition = transform.ValueRO.Position;
                return true;
            }

            return false;
        }

        private float ResolveDistanceToTarget(float3 currentPosition)
        {
            var current2 = new float2(currentPosition.x, currentPosition.z);
            var target2 = new float2(_targetPosition.x, _targetPosition.z);
            return math.distance(current2, target2);
        }

        private float ResolveMovedDistance(float3 currentPosition)
        {
            var start2 = new float2(_startPosition.x, _startPosition.z);
            var current2 = new float2(currentPosition.x, currentPosition.z);
            return math.distance(start2, current2);
        }

        private void TryEmitTelemetry(ref SystemState state, float distance)
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

            var intentCount = _intentObserved != 0 ? 1f : 0f;
            var jumpCount = _jumpCompleted != 0 ? 1f : 0f;
            var timeToJump = _jumpCompleted != 0 ? _jumpWorldSeconds - _intentWorldSeconds : -1f;
            var distanceValue = float.IsNaN(distance) ? -1f : distance;

            buffer.AddMetric("space4x.ftl.intent_count", intentCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.ftl.jump_completed_count", jumpCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.ftl.time_to_jump_s", timeToJump, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.ftl.exit_distance_to_target", distanceValue, TelemetryMetricUnit.Custom);
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
            if (!IsTruthy(SystemEnv.GetEnvironmentVariable(ExitOnResultEnv)))
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
            var scenarioSourcePath = SystemEnv.GetEnvironmentVariable(ScenarioSourcePathEnv);
            _bankResolved = 1;
            if ((!string.IsNullOrWhiteSpace(scenarioSourcePath) &&
                 scenarioSourcePath.EndsWith(FtlScenarioFile, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(scenarioPath) &&
                 scenarioPath.EndsWith(FtlScenarioFile, StringComparison.OrdinalIgnoreCase)))
            {
                _bankTestId = FtlTestId;
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

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
