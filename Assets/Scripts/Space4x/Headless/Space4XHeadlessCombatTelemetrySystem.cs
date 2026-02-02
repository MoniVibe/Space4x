using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4X.StrikeCraft;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
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
        private EntityQuery _wingDirectiveQuery;
        private byte _done;
        private byte _sawStrikeCraft;
        private byte _sawAttackRun;
        private byte _sawCap;
        private byte _sawWingDirective;
        private int _maxStrikeCraft;
        private int _maxAttackActive;
        private int _maxCapActive;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
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

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            EmitMetrics(ref state);
            _done = 1;
        }

        private void EmitMetrics(ref SystemState state)
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
