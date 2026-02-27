using System;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.StrikeCraft;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Headless
{
    /// <summary>
    /// Emits headless operator metrics for strike craft combat loop coverage.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftSystem))]
    public partial struct Space4XHeadlessCombatTelemetrySystem : ISystem
    {
        private const string CapitalRangeScenarioId = "space4x_capital_shooting_range_micro";
        private const string CapitalRangeScenarioPrefix = "space4x_capital_shooting_range_";
        private const byte CapitalRangeMeasuredSide = 0;
        private static readonly FixedString64Bytes MetricCombatantsDestroyed = new FixedString64Bytes("space4x.combat.combatants.destroyed");
        private static readonly FixedString64Bytes MetricRangeShotsFired = new FixedString64Bytes("space4x.gunnery.capital_range.shots.fired");
        private static readonly FixedString64Bytes MetricRangeShotsHit = new FixedString64Bytes("space4x.gunnery.capital_range.shots.hit");
        private static readonly FixedString64Bytes MetricRangeHitRate = new FixedString64Bytes("space4x.gunnery.capital_range.hit_rate");
        private static readonly FixedString64Bytes MetricRangeReactionTime = new FixedString64Bytes("space4x.gunnery.capital_range.reaction_time_s");
        private static readonly FixedString64Bytes MetricRangeTtk = new FixedString64Bytes("space4x.gunnery.capital_range.ttk_s");
        private static readonly FixedString64Bytes MetricRangeLockChurn = new FixedString64Bytes("space4x.gunnery.capital_range.lock_churn");
        private static readonly FixedString64Bytes MetricRangeDroneAssist = new FixedString64Bytes("space4x.gunnery.capital_range.drone_assist");
        private static readonly FixedString64Bytes MetricRangeScore = new FixedString64Bytes("space4x.gunnery.capital_range.score");

        private EntityQuery _wingDirectiveQuery;
        private byte _done;
        private byte _sawStrikeCraft;
        private byte _sawAttackRun;
        private byte _sawCap;
        private byte _sawWingDirective;
        private int _maxStrikeCraft;
        private int _maxAttackActive;
        private int _maxCapActive;
        private FixedString64Bytes _activeScenarioId;
        private byte _capitalInitialized;
        private uint _capitalShotsFiredBaseline;
        private uint _capitalShotsHitBaseline;
        private uint _capitalDestroyedBaseline;
        private uint _capitalFirstShotTick;
        private uint _capitalFirstHitTick;
        private uint _capitalFirstKillTick;
        private uint _capitalLockChurnCount;
        private uint _capitalLastLockHash;
        private int _capitalLastLockCount;
        private byte _capitalHasLockSnapshot;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<ScenarioInfo>();
            _wingDirectiveQuery = SystemAPI.QueryBuilder().WithAll<StrikeCraftWingDirective>().Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) &&
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var scenarioInfo = SystemAPI.GetSingleton<ScenarioInfo>();
            var scenarioId = scenarioInfo.ScenarioId;
            if (!_activeScenarioId.Equals(scenarioId))
            {
                _activeScenarioId = scenarioId;
                ResetCapitalRangeState();
            }

            var isCapitalRangeScenario = IsCapitalRangeScenario(scenarioId);

            var strikeCount = 0;
            var attackActive = 0;
            var capActive = 0;
            foreach (var profile in SystemAPI.Query<RefRO<StrikeCraftProfile>>())
            {
                strikeCount++;
                if (profile.ValueRO.Phase == AttackRunPhase.Execute)
                {
                    attackActive++;
                }
                else if (profile.ValueRO.Phase == AttackRunPhase.CombatAirPatrol)
                {
                    capActive++;
                }
            }

            if (strikeCount > 0)
            {
                _sawStrikeCraft = 1;
            }

            if (attackActive > 0)
            {
                _sawAttackRun = 1;
            }

            if (capActive > 0)
            {
                _sawCap = 1;
            }

            if (!_wingDirectiveQuery.IsEmptyIgnoreFilter)
            {
                _sawWingDirective = 1;
            }

            if (strikeCount > _maxStrikeCraft)
            {
                _maxStrikeCraft = strikeCount;
            }

            if (attackActive > _maxAttackActive)
            {
                _maxAttackActive = attackActive;
            }

            if (capActive > _maxCapActive)
            {
                _maxCapActive = capActive;
            }

            if (isCapitalRangeScenario)
            {
                UpdateCapitalRangeState(ref state, timeState.Tick);
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            EmitMetrics(ref state, runtime, timeState, isCapitalRangeScenario);
            _done = 1;
        }

        private void EmitMetrics(ref SystemState state, in Space4XScenarioRuntime runtime, in TimeState timeState, bool includeCapitalRange)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.strikecraft_seen"), _sawStrikeCraft != 0 ? 1f : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.attack_run_seen"), _sawAttackRun != 0 ? 1f : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.cap_seen"), _sawCap != 0 ? 1f : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.wing_directive_seen"), _sawWingDirective != 0 ? 1f : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.strikecraft_max"), _maxStrikeCraft);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.attack_run_max_active"), _maxAttackActive);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.combat.cap_max_active"), _maxCapActive);

            if (!includeCapitalRange)
            {
                return;
            }

            if (!TryReadCapitalRangeTotals(ref state, CapitalRangeMeasuredSide, out var shotsFiredTotal, out var shotsHitTotal, out var combatantsDestroyedTotal))
            {
                return;
            }

            var fired = shotsFiredTotal >= _capitalShotsFiredBaseline ? shotsFiredTotal - _capitalShotsFiredBaseline : 0u;
            var hit = shotsHitTotal >= _capitalShotsHitBaseline ? shotsHitTotal - _capitalShotsHitBaseline : 0u;
            var hitRate = fired > 0u ? (float)hit / fired : 0f;

            var fixedDelta = timeState.FixedDeltaTime > 1e-5f ? timeState.FixedDeltaTime : (1f / 60f);
            var reactionSeconds = _capitalFirstShotTick > 0u
                ? math.max(0f, (_capitalFirstShotTick - runtime.StartTick) * fixedDelta)
                : -1f;

            var ttkSeconds = _capitalFirstKillTick > 0u && _capitalFirstShotTick > 0u && _capitalFirstKillTick >= _capitalFirstShotTick
                ? (_capitalFirstKillTick - _capitalFirstShotTick) * fixedDelta
                : -1f;

            var droneAssist = (_sawStrikeCraft != 0 || _sawAttackRun != 0) ? 1f : 0f;
            var accuracyScore = math.saturate(hitRate) * 100f;
            var reactionScore = reactionSeconds >= 0f ? math.saturate(1f - reactionSeconds / 25f) * 100f : 0f;
            var ttkScore = ttkSeconds >= 0f ? math.saturate(1f - ttkSeconds / 90f) * 100f : 40f;
            var churnPenalty = math.min(35f, _capitalLockChurnCount * 1.5f);
            var score = math.clamp(
                accuracyScore * 0.62f +
                reactionScore * 0.23f +
                ttkScore * 0.15f -
                churnPenalty +
                (droneAssist > 0f ? 5f : 0f),
                0f,
                100f);

            AddOrUpdateMetric(buffer, MetricRangeShotsFired, fired);
            AddOrUpdateMetric(buffer, MetricRangeShotsHit, hit);
            AddOrUpdateMetric(buffer, MetricRangeHitRate, hitRate);
            AddOrUpdateMetric(buffer, MetricRangeReactionTime, reactionSeconds);
            AddOrUpdateMetric(buffer, MetricRangeTtk, ttkSeconds);
            AddOrUpdateMetric(buffer, MetricRangeLockChurn, _capitalLockChurnCount);
            AddOrUpdateMetric(buffer, MetricRangeDroneAssist, droneAssist);
            AddOrUpdateMetric(buffer, MetricRangeScore, score);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.gunnery.capital_range.combatants.destroyed"), math.max(0u, combatantsDestroyedTotal - _capitalDestroyedBaseline));
        }

        private void ResetCapitalRangeState()
        {
            _capitalInitialized = 0;
            _capitalShotsFiredBaseline = 0u;
            _capitalShotsHitBaseline = 0u;
            _capitalDestroyedBaseline = 0u;
            _capitalFirstShotTick = 0u;
            _capitalFirstHitTick = 0u;
            _capitalFirstKillTick = 0u;
            _capitalLockChurnCount = 0u;
            _capitalLastLockHash = 0u;
            _capitalLastLockCount = 0;
            _capitalHasLockSnapshot = 0;
        }

        private void UpdateCapitalRangeState(ref SystemState state, uint tick)
        {
            if (!TryReadCapitalRangeTotals(ref state, CapitalRangeMeasuredSide, out var shotsFiredTotal, out var shotsHitTotal, out var combatantsDestroyedTotal))
            {
                return;
            }

            if (_capitalInitialized == 0)
            {
                _capitalShotsFiredBaseline = shotsFiredTotal;
                _capitalShotsHitBaseline = shotsHitTotal;
                _capitalDestroyedBaseline = combatantsDestroyedTotal;
                _capitalInitialized = 1;
            }

            var fired = shotsFiredTotal >= _capitalShotsFiredBaseline ? shotsFiredTotal - _capitalShotsFiredBaseline : 0u;
            var hit = shotsHitTotal >= _capitalShotsHitBaseline ? shotsHitTotal - _capitalShotsHitBaseline : 0u;
            var destroyed = combatantsDestroyedTotal >= _capitalDestroyedBaseline ? combatantsDestroyedTotal - _capitalDestroyedBaseline : 0u;

            if (_capitalFirstShotTick == 0u && fired > 0u)
            {
                _capitalFirstShotTick = tick;
            }

            if (_capitalFirstHitTick == 0u && hit > 0u)
            {
                _capitalFirstHitTick = tick;
            }

            if (_capitalFirstKillTick == 0u && destroyed > 0u)
            {
                _capitalFirstKillTick = tick;
            }

            var lockHash = 2166136261u;
            var lockCount = 0;
            foreach (var (mounts, side, entity) in SystemAPI.Query<DynamicBuffer<WeaponMount>, RefRO<ScenarioSide>>().WithEntityAccess())
            {
                if (side.ValueRO.Side != CapitalRangeMeasuredSide)
                {
                    continue;
                }

                for (var i = 0; i < mounts.Length; i++)
                {
                    var target = mounts[i].CurrentTarget;
                    if (target == Entity.Null)
                    {
                        continue;
                    }

                    lockCount++;
                    lockHash = (lockHash ^ (uint)(entity.Index + 1)) * 16777619u;
                    lockHash = (lockHash ^ (uint)(i + 1)) * 16777619u;
                    lockHash = (lockHash ^ (uint)(target.Index + 1)) * 16777619u;
                }
            }

            if (lockCount > 0)
            {
                if (_capitalHasLockSnapshot != 0 &&
                    _capitalLastLockCount > 0 &&
                    _capitalLastLockHash != lockHash)
                {
                    _capitalLockChurnCount++;
                }

                _capitalLastLockHash = lockHash;
                _capitalLastLockCount = lockCount;
                _capitalHasLockSnapshot = 1;
                return;
            }

            _capitalLastLockHash = 0u;
            _capitalLastLockCount = 0;
            _capitalHasLockSnapshot = 0;
        }

        private bool TryReadCapitalRangeTotals(ref SystemState state, byte measuredSide, out uint shotsFiredTotal, out uint shotsHitTotal, out uint combatantsDestroyedTotal)
        {
            shotsFiredTotal = 0u;
            shotsHitTotal = 0u;
            combatantsDestroyedTotal = 0u;

            var hasSideShots = false;
            foreach (var (mounts, side) in SystemAPI.Query<DynamicBuffer<WeaponMount>, RefRO<ScenarioSide>>())
            {
                if (side.ValueRO.Side != measuredSide)
                {
                    continue;
                }

                hasSideShots = true;
                for (var i = 0; i < mounts.Length; i++)
                {
                    shotsFiredTotal += mounts[i].ShotsFired;
                    shotsHitTotal += mounts[i].ShotsHit;
                }
            }

            var hasOpposingSide = false;
            foreach (var (hull, side) in SystemAPI.Query<RefRO<HullIntegrity>, RefRO<ScenarioSide>>())
            {
                if (side.ValueRO.Side == measuredSide)
                {
                    continue;
                }

                hasOpposingSide = true;
                if (hull.ValueRO.Current <= 0.01f)
                {
                    combatantsDestroyedTotal++;
                }
            }

            if (hasSideShots || hasOpposingSide)
            {
                return true;
            }

            // Fallback to global telemetry for legacy scenarios with no ScenarioSide wiring.
            var hasCombatTelemetry = SystemAPI.TryGetSingleton<Space4XCombatTelemetry>(out var telemetry);
            if (hasCombatTelemetry)
            {
                shotsFiredTotal = telemetry.TotalShotsFired;
                shotsHitTotal = telemetry.TotalShotsHit;
            }

            if (!TryReadTelemetryMetric(ref state, MetricCombatantsDestroyed, out var destroyed))
            {
                return hasCombatTelemetry;
            }

            combatantsDestroyedTotal = destroyed <= 0f ? 0u : (uint)destroyed;
            return true;
        }

        private static bool TryReadTelemetryMetric(ref SystemState state, in FixedString64Bytes key, out float value)
        {
            value = 0f;
            if (!TryGetTelemetryMetrics(ref state, out var metrics))
            {
                return false;
            }

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

            return false;
        }

        private static bool TryGetTelemetryMetrics(ref SystemState state, out DynamicBuffer<TelemetryMetric> metrics)
        {
            metrics = default;
            using var streamQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            if (!streamQuery.IsEmptyIgnoreFilter)
            {
                var streamEntity = streamQuery.GetSingletonEntity();
                if (state.EntityManager.HasBuffer<TelemetryMetric>(streamEntity))
                {
                    metrics = state.EntityManager.GetBuffer<TelemetryMetric>(streamEntity);
                    return true;
                }
            }

            using var singletonQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStreamSingleton>());
            if (singletonQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var singletonEntity = singletonQuery.GetSingletonEntity();
            var singleton = state.EntityManager.GetComponentData<TelemetryStreamSingleton>(singletonEntity);
            if (singleton.Stream == Entity.Null || !state.EntityManager.HasBuffer<TelemetryMetric>(singleton.Stream))
            {
                return false;
            }

            metrics = state.EntityManager.GetBuffer<TelemetryMetric>(singleton.Stream);
            return true;
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

        private static bool IsCapitalRangeScenario(in FixedString64Bytes scenarioId)
        {
            if (scenarioId.IsEmpty)
            {
                return false;
            }

            var id = scenarioId.ToString();
            if (id.Equals(CapitalRangeScenarioId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return id.StartsWith(CapitalRangeScenarioPrefix, StringComparison.OrdinalIgnoreCase) &&
                   id.EndsWith("_micro", StringComparison.OrdinalIgnoreCase);
        }
    }
}
