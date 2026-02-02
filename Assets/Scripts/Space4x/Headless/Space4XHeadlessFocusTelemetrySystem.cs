using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless telemetry for focus usage.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFocusModifierSystem))]
    public partial struct Space4XHeadlessFocusTelemetrySystem : ISystem
    {
        private const uint ReportIntervalTicks = 30;
        private uint _nextReportTick;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
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

            if (timeState.Tick < _nextReportTick)
            {
                return;
            }

            _nextReportTick = timeState.Tick + ReportIntervalTicks;

            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            var entityCount = 0;
            var activeAbilities = 0;
            var exhaustedCount = 0;
            var comaCount = 0;
            var avgFocusRatio = 0f;
            var avgExhaustion = 0f;

            foreach (var (focus, abilities) in SystemAPI.Query<RefRO<Space4XEntityFocus>, DynamicBuffer<Space4XActiveFocusAbility>>())
            {
                entityCount++;
                activeAbilities += abilities.Length;
                avgFocusRatio += focus.ValueRO.Ratio;
                avgExhaustion += focus.ValueRO.ExhaustionLevel;

                if (focus.ValueRO.IsExhausted)
                {
                    exhaustedCount++;
                }
                if (focus.ValueRO.IsInComa != 0)
                {
                    comaCount++;
                }
            }

            if (entityCount > 0)
            {
                avgFocusRatio /= entityCount;
                avgExhaustion /= entityCount;
            }

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.focus.entity_count"), entityCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.focus.active_abilities"), activeAbilities);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.focus.avg_ratio"), avgFocusRatio);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.focus.avg_exhaustion"), avgExhaustion);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.focus.exhausted_count"), exhaustedCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.focus.coma_count"), comaCount);
        }

        private static void AddOrUpdateMetric(DynamicBuffer<Space4XOperatorMetric> buffer, FixedString64Bytes key, float value)
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
