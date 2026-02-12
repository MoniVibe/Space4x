using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Produces a compact "after action" battle report from existing combat telemetry metrics.
    /// Intended to support player-facing UI and debugging while we scale up fleet battles.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCombatOutcomeCollectionSystem))]
    [UpdateAfter(typeof(Space4XCombatTelemetrySystem))]
    public partial struct Space4XBattleReportSystem : ISystem
    {
        // If we're not running a scenario, treat this much inactivity as "battle ended".
        // We keep this conservative to avoid spamming reports mid-fight.
        private const uint QuietFinalizeTicks = 300;

        private Entity _reportEntity;
        private byte _hasReportEntity;
        private byte _inBattle;
        private uint _battleStartTick;
        private uint _lastActivityTick;
        private byte _finalizedThisSession;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            // If we're in a scenario run, finalize exactly once at scenario end.
            if (SystemAPI.TryGetSingleton<Space4XScenarioRuntime>(out var runtime))
            {
                if (_finalizedThisSession == 0 && time.Tick >= runtime.EndTick)
                {
                    FinalizeReport(ref state, runtime.StartTick, time.Tick, isScenarioFinalize: true);
                    _finalizedThisSession = 1;
                }

                return;
            }

            // Non-scenario gameplay fallback: infer battle start/end from activity.
            if (!TryGetTelemetryMetricBuffer(ref state, out var metrics))
            {
                return;
            }

            var engaged = (uint)GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.combatants.engaged"), 0f);
            var shotsDelta = (uint)GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.shots.fired_delta"), 0f);
            var outcomesThisTick = TryGetOutcomeCount(ref state);

            var activity = engaged > 0 || shotsDelta > 0 || outcomesThisTick > 0;
            if (activity)
            {
                if (_inBattle == 0)
                {
                    _inBattle = 1;
                    _battleStartTick = time.Tick;
                }

                _lastActivityTick = time.Tick;
                return;
            }

            if (_inBattle == 0)
            {
                return;
            }

            if (time.Tick - _lastActivityTick < QuietFinalizeTicks)
            {
                return;
            }

            FinalizeReport(ref state, _battleStartTick, time.Tick, isScenarioFinalize: false);
            _inBattle = 0;
        }

        private int TryGetOutcomeCount(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XCombatOutcomeStream>(out var streamEntity))
            {
                return 0;
            }

            if (!state.EntityManager.HasBuffer<Space4XCombatOutcomeEvent>(streamEntity))
            {
                return 0;
            }

            return state.EntityManager.GetBuffer<Space4XCombatOutcomeEvent>(streamEntity).Length;
        }

        private void FinalizeReport(ref SystemState state, uint startTick, uint endTick, bool isScenarioFinalize)
        {
            if (!TryGetTelemetryMetricBuffer(ref state, out var metrics))
            {
                return;
            }

            EnsureReportEntity(ref state);

            var report = new Space4XBattleReport
            {
                BattleStartTick = startTick,
                BattleEndTick = endTick,
                WinnerSide = (int)GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.outcome.winner_side"), -1f),
                TotalCombatants = (int)GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.combatants.total"), 0f),
                TotalDestroyed = (int)GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.combatants.destroyed"), 0f),
                TotalAlive = (int)GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.outcome.total_alive"), 0f),
                ShotsFired = (int)GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.shots.fired_total"), 0f),
                ShotsHit = (int)GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.shots.hit_total"), 0f),
                DamageDealtTotal = GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.damage.dealt_total"), 0f),
                DamageReceivedTotal = GetMetricOrDefault(metrics, new FixedString64Bytes("space4x.combat.damage.received_total"), 0f),
            };

            // If we never saw battle activity (e.g. scenario ended before engagement), try to keep the start tick sane.
            if (report.BattleStartTick == 0 && report.BattleEndTick > 0)
            {
                report.BattleStartTick = report.BattleEndTick;
            }

            state.EntityManager.SetComponentData(_reportEntity, report);

            var sides = state.EntityManager.GetBuffer<Space4XBattleReportSide>(_reportEntity);
            sides.Clear();

            // We don't have a side-id list in telemetry, so probe a small bounded range.
            // Current scenarios use sides 0/1; this keeps the report stable without string parsing.
            for (var side = 0; side < 8; side++)
            {
                var shipsTotalKey = new FixedString64Bytes($"space4x.combat.side.{side}.ships.total");
                if (!TryGetMetric(metrics, shipsTotalKey, out var shipsTotalF))
                {
                    continue;
                }

                var shipsTotal = (int)shipsTotalF;
                if (shipsTotal <= 0)
                {
                    continue;
                }

                var shipsDestroyed = (int)GetMetricOrDefault(metrics, new FixedString64Bytes($"space4x.combat.side.{side}.ships.destroyed"), 0f);
                var shipsAlive = (int)GetMetricOrDefault(metrics, new FixedString64Bytes($"space4x.combat.side.{side}.ships.alive"), 0f);
                var shipsAliveRatio = GetMetricOrDefault(metrics, new FixedString64Bytes($"space4x.combat.side.{side}.ships.alive_ratio"), 0f);
                var hullRatio = GetMetricOrDefault(metrics, new FixedString64Bytes($"space4x.combat.side.{side}.hull.ratio"), 0f);
                var damageDealt = GetMetricOrDefault(metrics, new FixedString64Bytes($"space4x.combat.side.{side}.damage.dealt"), 0f);
                var damageReceived = GetMetricOrDefault(metrics, new FixedString64Bytes($"space4x.combat.side.{side}.damage.received"), 0f);

                sides.Add(new Space4XBattleReportSide
                {
                    Side = side,
                    ShipsTotal = shipsTotal,
                    ShipsDestroyed = shipsDestroyed,
                    ShipsAlive = shipsAlive,
                    ShipsAliveRatio = shipsAliveRatio,
                    HullRatio = hullRatio,
                    DamageDealt = damageDealt,
                    DamageReceived = damageReceived
                });
            }

            if (isScenarioFinalize)
            {
                // Seed start tick for scenario runs that don't go through the non-scenario activity detector.
                _battleStartTick = report.BattleStartTick;
                _inBattle = 0;
            }
        }

        private void EnsureReportEntity(ref SystemState state)
        {
            if (_hasReportEntity != 0 && state.EntityManager.Exists(_reportEntity))
            {
                return;
            }

            using var q = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XBattleReport>());
            if (!q.IsEmptyIgnoreFilter)
            {
                _reportEntity = q.GetSingletonEntity();
            }
            else
            {
                _reportEntity = state.EntityManager.CreateEntity(typeof(Space4XBattleReport));
                state.EntityManager.AddBuffer<Space4XBattleReportSide>(_reportEntity);
            }

            if (!state.EntityManager.HasBuffer<Space4XBattleReportSide>(_reportEntity))
            {
                state.EntityManager.AddBuffer<Space4XBattleReportSide>(_reportEntity);
            }

            _hasReportEntity = 1;
        }

        private static bool TryGetTelemetryMetricBuffer(ref SystemState state, out DynamicBuffer<TelemetryMetric> buffer)
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

        private static bool TryGetMetric(DynamicBuffer<TelemetryMetric> metrics, FixedString64Bytes key, out float value)
        {
            for (var i = 0; i < metrics.Length; i++)
            {
                var metric = metrics[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                value = metric.Value;
                return true;
            }

            value = 0f;
            return false;
        }

        private static float GetMetricOrDefault(DynamicBuffer<TelemetryMetric> metrics, FixedString64Bytes key, float fallback)
        {
            return TryGetMetric(metrics, key, out var value) ? value : fallback;
        }
    }
}
