using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Emits a deterministic digest metric for capital battle demo scenarios.
    /// Uses ordered telemetry totals to avoid nondeterministic iteration artifacts.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCombatTelemetrySystem))]
    public partial struct Space4XBattleDeterminismDigestSystem : ISystem
    {
        private static readonly FixedString64Bytes Capital20Scenario = new FixedString64Bytes("space4x_capital_20_vs_20_supergreen");
        private static readonly FixedString64Bytes Capital100Scenario = new FixedString64Bytes("space4x_capital_100_vs_100_proper");

        private static readonly FixedString64Bytes MetricDigest = new FixedString64Bytes("space4x.battle.determinism.digest");
        private static readonly FixedString64Bytes MetricShotsFired = new FixedString64Bytes("space4x.combat.shots.fired_total");
        private static readonly FixedString64Bytes MetricShotsHit = new FixedString64Bytes("space4x.combat.shots.hit_total");
        private static readonly FixedString64Bytes MetricCombatantsTotal = new FixedString64Bytes("space4x.combat.combatants.total");
        private static readonly FixedString64Bytes MetricCombatantsDestroyed = new FixedString64Bytes("space4x.combat.combatants.destroyed");
        private static readonly FixedString64Bytes MetricWinnerSide = new FixedString64Bytes("space4x.combat.outcome.winner_side");
        private static readonly FixedString64Bytes MetricTotalAlive = new FixedString64Bytes("space4x.combat.outcome.total_alive");

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TelemetryExportConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo scenarioInfo) || !IsCapitalScenario(scenarioInfo.ScenarioId))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!TryGetTelemetryMetricBuffer(ref state, out var metricBuffer))
            {
                return;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var time = SystemAPI.GetSingleton<TimeState>();

            // Emit once the run is at (or past) the configured end tick when available.
            if (runtime.EndTick > 0u && time.Tick + 1u < runtime.EndTick)
            {
                return;
            }

            var digest = ComputeDigest(
                scenarioInfo.Seed,
                runtime.EndTick,
                time.Tick,
                ReadMetricOrDefault(metricBuffer, MetricShotsFired),
                ReadMetricOrDefault(metricBuffer, MetricShotsHit),
                ReadMetricOrDefault(metricBuffer, MetricCombatantsTotal),
                ReadMetricOrDefault(metricBuffer, MetricCombatantsDestroyed),
                ReadMetricOrDefault(metricBuffer, MetricWinnerSide),
                ReadMetricOrDefault(metricBuffer, MetricTotalAlive));

            var digest24 = digest & 0x00FFFFFFu;
            AddOrUpdateMetric(metricBuffer, MetricDigest, digest24, TelemetryMetricUnit.Count);
        }

        private static bool IsCapitalScenario(in FixedString64Bytes scenarioId)
        {
            return scenarioId.Equals(Capital20Scenario) || scenarioId.Equals(Capital100Scenario);
        }

        private static uint ComputeDigest(
            uint seed,
            uint endTick,
            uint currentTick,
            float shotsFired,
            float shotsHit,
            float combatantsTotal,
            float combatantsDestroyed,
            float winnerSide,
            float totalAlive)
        {
            var digest = 0x811C9DC5u;
            digest = Mix(digest, seed);
            digest = Mix(digest, endTick);
            digest = Mix(digest, currentTick);
            digest = Mix(digest, Quantize(shotsFired));
            digest = Mix(digest, Quantize(shotsHit));
            digest = Mix(digest, Quantize(combatantsTotal));
            digest = Mix(digest, Quantize(combatantsDestroyed));
            digest = Mix(digest, Quantize(winnerSide));
            digest = Mix(digest, Quantize(totalAlive));
            return digest;
        }

        private static uint Mix(uint digest, uint value)
        {
            return math.hash(new uint4(
                digest ^ 0x9E3779B9u,
                value + 0x85EBCA6Bu,
                digest * 1664525u + 1013904223u,
                value ^ 0xC2B2AE35u));
        }

        private static uint Quantize(float value)
        {
            if (!math.isfinite(value))
            {
                return 0u;
            }

            var rounded = (int)math.round(value);
            return unchecked((uint)rounded);
        }

        private static float ReadMetricOrDefault(DynamicBuffer<TelemetryMetric> metricBuffer, in FixedString64Bytes key)
        {
            for (var i = 0; i < metricBuffer.Length; i++)
            {
                var metric = metricBuffer[i];
                if (metric.Key.Equals(key))
                {
                    return metric.Value;
                }
            }

            return 0f;
        }

        private static void AddOrUpdateMetric(
            DynamicBuffer<TelemetryMetric> metricBuffer,
            in FixedString64Bytes key,
            float value,
            TelemetryMetricUnit unit)
        {
            for (var i = 0; i < metricBuffer.Length; i++)
            {
                var metric = metricBuffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                metricBuffer[i] = metric;
                return;
            }

            metricBuffer.AddMetric(key, value, unit);
        }

        private bool TryGetTelemetryMetricBuffer(ref SystemState state, out DynamicBuffer<TelemetryMetric> buffer)
        {
            buffer = default;
            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                return false;
            }

            if (telemetryEntity == Entity.Null || !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            return true;
        }
    }
}
