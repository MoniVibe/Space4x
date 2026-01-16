using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(Unity.Entities.LateSimulationSystemGroup))]
    public partial struct Space4XHeadlessUndockRiskGateSystem : ISystem
    {
        private const float RiskGapMin = 0.00005f;
        private const float RiskEnforceMin = 0.01f;
        private const float RiskHardStop = 0.9f;
        private const byte DecisionUndock = 2;

        private ComponentLookup<ResolvedBehaviorProfile> _profileLookup;
        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<UndockDecisionState>();

            _profileLookup = state.GetComponentLookup<ResolvedBehaviorProfile>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            _done = 1;
            _profileLookup.Update(ref state);

            var lawfulRiskSum = 0f;
            var chaoticRiskSum = 0f;
            var lawfulWaitSum = 0f;
            var chaoticWaitSum = 0f;
            var lawfulUndocks = 0u;
            var chaoticUndocks = 0u;
            var lawfulWaitOnly = 0u;
            var chaoticWaitOnly = 0u;
            var maxRisk = 0f;

            foreach (var (decisionState, pilotLink) in SystemAPI.Query<RefRO<UndockDecisionState>, RefRO<VesselPilotLink>>())
            {
                var pilot = pilotLink.ValueRO.Pilot;
                if (pilot == Entity.Null || !_profileLookup.HasComponent(pilot))
                {
                    continue;
                }

                var profile = _profileLookup[pilot];
                var chaos = profile.Chaos01;
                var isLawful = chaos <= 0.4f;
                var isChaotic = chaos >= 0.6f;
                if (!isLawful && !isChaotic)
                {
                    continue;
                }

                var decision = decisionState.ValueRO;
                if (decision.Decisions == 0)
                {
                    continue;
                }

                var risk = decision.LastRiskScore;
                maxRisk = math.max(maxRisk, risk);

                if (isLawful)
                {
                    if (decision.UndockCount == 0)
                    {
                        lawfulWaitOnly++;
                    }
                    else if (decision.LastDecision == DecisionUndock)
                    {
                        lawfulRiskSum += risk;
                        lawfulWaitSum += decision.WaitCount;
                        lawfulUndocks++;
                    }
                }
                else if (isChaotic)
                {
                    if (decision.UndockCount == 0)
                    {
                        chaoticWaitOnly++;
                    }
                    else if (decision.LastDecision == DecisionUndock)
                    {
                        chaoticRiskSum += risk;
                        chaoticWaitSum += decision.WaitCount;
                        chaoticUndocks++;
                    }
                }
            }

            var lawfulAvgRisk = lawfulUndocks > 0 ? lawfulRiskSum / lawfulUndocks : 0f;
            var chaoticAvgRisk = chaoticUndocks > 0 ? chaoticRiskSum / chaoticUndocks : 0f;
            var lawfulAvgWait = lawfulUndocks > 0 ? lawfulWaitSum / lawfulUndocks : 0f;
            var chaoticAvgWait = chaoticUndocks > 0 ? chaoticWaitSum / chaoticUndocks : 0f;
            var hasLawfulSignal = lawfulUndocks > 0 || lawfulWaitOnly > 0;
            var hasBoth = hasLawfulSignal && chaoticUndocks > 0;
            var hasMeaningfulRisk = maxRisk >= RiskGapMin;
            var riskGapOk = chaoticAvgRisk >= lawfulAvgRisk + RiskGapMin;
            var waitOk = lawfulAvgWait >= chaoticAvgWait;
            var hardStopOk = maxRisk < RiskHardStop;

            if (!hasBoth || !hasMeaningfulRisk || maxRisk < RiskEnforceMin)
            {
                AppendUndockSkippedBlackCat(ref state, runtime, maxRisk, lawfulUndocks, chaoticUndocks);
                EmitOperatorSummary(ref state, lawfulAvgRisk, chaoticAvgRisk, lawfulAvgWait, chaoticAvgWait, maxRisk, lawfulUndocks, chaoticUndocks, lawfulWaitOnly, chaoticWaitOnly);
                EmitTelemetrySummary(ref state, lawfulAvgRisk, chaoticAvgRisk, lawfulAvgWait, chaoticAvgWait, maxRisk, lawfulUndocks, chaoticUndocks, lawfulWaitOnly, chaoticWaitOnly);
                return;
            }

            if ((lawfulUndocks > 0 && (!riskGapOk || !waitOk)) || !hardStopOk)
            {
                var observed = $"lawful_avg_risk={lawfulAvgRisk:F2} chaotic_avg_risk={chaoticAvgRisk:F2} lawful_wait_avg={lawfulAvgWait:F2} chaotic_wait_avg={chaoticAvgWait:F2} max_risk={maxRisk:F2}";
                var expected = $"lawful_undocks>0 chaotic_undocks>0 max_risk>={RiskGapMin:F2} chaotic_avg_risk>=lawful_avg_risk+{RiskGapMin:F2} lawful_wait_avg>=chaotic_wait_avg max_risk<{RiskHardStop:F2}";
                Space4XHeadlessDiagnostics.ReportInvariant(
                    "UNDOCK_RISK_GATE",
                    "Undock risk gating did not separate lawful vs chaotic profiles or exceeded hard stop.",
                    observed,
                    expected);
                HeadlessExitUtility.Request(state.EntityManager, runtime.EndTick, Space4XHeadlessDiagnostics.TestFailExitCode);
            }

            EmitOperatorSummary(ref state, lawfulAvgRisk, chaoticAvgRisk, lawfulAvgWait, chaoticAvgWait, maxRisk, lawfulUndocks, chaoticUndocks, lawfulWaitOnly, chaoticWaitOnly);
            EmitTelemetrySummary(ref state, lawfulAvgRisk, chaoticAvgRisk, lawfulAvgWait, chaoticAvgWait, maxRisk, lawfulUndocks, chaoticUndocks, lawfulWaitOnly, chaoticWaitOnly);
        }

        private void AppendUndockSkippedBlackCat(
            ref SystemState state,
            in Space4XScenarioRuntime runtime,
            float maxRisk,
            uint lawfulUndocks,
            uint chaoticUndocks)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = new FixedString64Bytes("UNDOCK_BEAT_SKIPPED"),
                Primary = Entity.Null,
                Secondary = Entity.Null,
                StartTick = runtime.StartTick,
                EndTick = runtime.EndTick,
                MetricA = maxRisk,
                MetricB = lawfulUndocks,
                MetricC = chaoticUndocks,
                MetricD = 0f,
                Classification = 1
            });
        }

        private void EmitOperatorSummary(
            ref SystemState state,
            float lawfulAvgRisk,
            float chaoticAvgRisk,
            float lawfulAvgWait,
            float chaoticAvgWait,
            float maxRisk,
            uint lawfulUndocks,
            uint chaoticUndocks,
            uint lawfulWaitOnly,
            uint chaoticWaitOnly)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.undock.lawful.avg_risk"), lawfulAvgRisk);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.undock.chaotic.avg_risk"), chaoticAvgRisk);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.undock.lawful.wait_avg"), lawfulAvgWait);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.undock.chaotic.wait_avg"), chaoticAvgWait);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.undock.max_risk"), maxRisk);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.undock.lawful.count"), lawfulUndocks);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.undock.chaotic.count"), chaoticUndocks);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.undock.lawful.wait_only"), lawfulWaitOnly);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.undock.chaotic.wait_only"), chaoticWaitOnly);
        }

        private void EmitTelemetrySummary(
            ref SystemState state,
            float lawfulAvgRisk,
            float chaoticAvgRisk,
            float lawfulAvgWait,
            float chaoticAvgWait,
            float maxRisk,
            uint lawfulUndocks,
            uint chaoticUndocks,
            uint lawfulWaitOnly,
            uint chaoticWaitOnly)
        {
            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity) ||
                !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return;
            }

            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            buffer.AddMetric("space4x.undock.lawful.avg_risk", lawfulAvgRisk, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.undock.chaotic.avg_risk", chaoticAvgRisk, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.undock.lawful.wait_avg", lawfulAvgWait, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.undock.chaotic.wait_avg", chaoticAvgWait, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.undock.max_risk", maxRisk, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.undock.lawful.count", lawfulUndocks, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.undock.chaotic.count", chaoticUndocks, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.undock.lawful.wait_only", lawfulWaitOnly, TelemetryMetricUnit.Count);
            buffer.AddMetric("space4x.undock.chaotic.wait_only", chaoticWaitOnly, TelemetryMetricUnit.Count);
        }

        private static void AddOrUpdateMetric(
            DynamicBuffer<Space4XOperatorMetric> buffer,
            FixedString64Bytes key,
            float value)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }
    }
}
