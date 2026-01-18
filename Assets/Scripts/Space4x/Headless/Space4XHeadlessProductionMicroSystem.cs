using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Space4X.Registry;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(Unity.Entities.LateSimulationSystemGroup))]
    public partial struct Space4XHeadlessProductionMicroSystem : ISystem
    {
        private const int StallNone = 0;
        private const int StallNoInput = 1;
        private const int StallNoStorage = 2;
        private const int StallUnknown = 3;
        private const float ProgressThreshold = 0.1f;

        private byte _enabled;
        private byte _done;
        private uint _startTick;
        private float _startHeld;
        private float _startSourceUnits;
        private int _expectedStallReason;

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
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            var scenarioInfo = SystemAPI.GetSingleton<ScenarioInfo>();
            if (_enabled == 0)
            {
                var scenarioId = scenarioInfo.ScenarioId.ToString();
                if (!scenarioId.StartsWith("space4x_production_", StringComparison.OrdinalIgnoreCase))
                {
                    state.Enabled = false;
                    return;
                }

                _expectedStallReason = ResolveExpectedStallReason(scenarioId);
                _enabled = 1;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (_startTick == 0)
            {
                _startTick = timeState.Tick;
                _startHeld = GetTotalHeld();
                _startSourceUnits = GetTotalSourceUnits();
            }

            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            var endHeld = GetTotalHeld();
            var endSourceUnits = GetTotalSourceUnits();
            var progressDelta = math.max(0f, endHeld - _startHeld);
            var progressCount = progressDelta > ProgressThreshold ? 1f : 0f;
            var remainingCapacity = GetRemainingCapacity();
            var stallReason = ResolveStallReason(progressCount, endSourceUnits, remainingCapacity);

            EmitMetrics(ref state, progressCount, progressDelta, stallReason, _expectedStallReason, endSourceUnits, remainingCapacity);
            _done = 1;
        }

        private static float GetTotalHeld()
        {
            var total = 0f;
            foreach (var storage in SystemAPI.Query<DynamicBuffer<ResourceStorage>>().WithAll<Carrier>())
            {
                for (var i = 0; i < storage.Length; i++)
                {
                    total += math.max(0f, storage[i].Amount);
                }
            }

            return total;
        }

        private static float GetTotalSourceUnits()
        {
            var total = 0f;
            foreach (var source in SystemAPI.Query<RefRO<ResourceSourceState>>())
            {
                total += math.max(0f, source.ValueRO.UnitsRemaining);
            }

            return total;
        }

        private static float GetRemainingCapacity()
        {
            var total = 0f;
            foreach (var storage in SystemAPI.Query<DynamicBuffer<ResourceStorage>>().WithAll<Carrier>())
            {
                for (var i = 0; i < storage.Length; i++)
                {
                    total += storage[i].GetRemainingCapacity();
                }
            }

            return total;
        }

        private static int ResolveExpectedStallReason(string scenarioId)
        {
            if (scenarioId.EndsWith("_noinput_micro", StringComparison.OrdinalIgnoreCase))
            {
                return StallNoInput;
            }

            if (scenarioId.EndsWith("_nostorage_micro", StringComparison.OrdinalIgnoreCase))
            {
                return StallNoStorage;
            }

            return StallNone;
        }

        private static int ResolveStallReason(float progressCount, float sourceUnits, float remainingCapacity)
        {
            if (progressCount > 0f)
            {
                return StallNone;
            }

            if (sourceUnits <= 0f)
            {
                return StallNoInput;
            }

            if (remainingCapacity <= 0f)
            {
                return StallNoStorage;
            }

            return StallUnknown;
        }

        private static void EmitMetrics(
            ref SystemState state,
            float progressCount,
            float progressDelta,
            int stallReason,
            int expectedStallReason,
            float sourceUnits,
            float remainingCapacity)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.production.chain_progress_count"), progressCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.production.chain_progress_delta"), progressDelta);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.production.stall_reason"), stallReason);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.production.expected_stall_reason"), expectedStallReason);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.production.source_units"), sourceUnits);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.production.remaining_capacity"), remainingCapacity);
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
