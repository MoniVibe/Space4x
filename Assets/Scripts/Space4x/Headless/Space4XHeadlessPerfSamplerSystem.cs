using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Telemetry;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XHeadlessOperatorReportSystem))]
    public partial struct Space4XHeadlessPerfSamplerSystem : ISystem
    {
        private const string SampleStrideEnv = "SPACE4X_HEADLESS_PERF_SAMPLE_STRIDE";

        private EntityQuery _frameTimingQuery;
        private NativeList<float> _tickSamples;
        private NativeList<float> _structuralSamples;
        private float _reservedPeakBytes;
        private uint _lastStructuralVersion;
        private byte _hasStructuralBaseline;
        private byte _written;
        private uint _sampleStride;
        private uint _nextSampleTick;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<FrameTimingStream>();

            _frameTimingQuery = state.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<FrameTimingStream>(),
                ComponentType.ReadOnly<AllocationDiagnostics>(),
                ComponentType.ReadOnly<FrameTimingSample>());

            _tickSamples = new NativeList<float>(128, Allocator.Persistent);
            _structuralSamples = new NativeList<float>(128, Allocator.Persistent);
            _sampleStride = ResolveSampleStride();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_tickSamples.IsCreated)
            {
                _tickSamples.Dispose();
            }

            if (_structuralSamples.IsCreated)
            {
                _structuralSamples.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_written != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();

            SampleTick(ref state, timeState.Tick);

            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            if (Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var metrics))
            {
                WriteMetrics(ref state, metrics, timeState.Tick);
            }

            _written = 1;
        }

        private void SampleTick(ref SystemState state, uint tick)
        {
            if (_frameTimingQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (_sampleStride > 1 && tick < _nextSampleTick)
            {
                return;
            }

            _nextSampleTick = tick + math.max(1u, _sampleStride);

            var frameEntity = _frameTimingQuery.GetSingletonEntity();
            var samples = state.EntityManager.GetBuffer<FrameTimingSample>(frameEntity);
            var allocation = state.EntityManager.GetComponentData<AllocationDiagnostics>(frameEntity);

            double totalMs = 0d;
            for (int i = 0; i < samples.Length; i++)
            {
                totalMs += samples[i].DurationMs;
            }

            _tickSamples.Add((float)totalMs);

            var structuralVersion = state.EntityManager.GlobalSystemVersion;
            if (_hasStructuralBaseline != 0)
            {
                _structuralSamples.Add(structuralVersion - _lastStructuralVersion);
            }
            else
            {
                _hasStructuralBaseline = 1;
            }

            _lastStructuralVersion = structuralVersion;

            if (allocation.TotalReservedBytes > _reservedPeakBytes)
            {
                _reservedPeakBytes = allocation.TotalReservedBytes;
            }
        }

        private void WriteMetrics(ref SystemState state, DynamicBuffer<Space4XOperatorMetric> metrics, uint tick)
        {
            var tickP95 = ComputePercentile(_tickSamples, 0.95f);
            var structuralP95 = ComputePercentile(_structuralSamples, 0.95f);

            AddMetric(metrics, "space4x.perf.sample_count", _tickSamples.Length);
            AddMetric(metrics, "space4x.perf.structural_sample_count", _structuralSamples.Length);
            AddMetric(metrics, "space4x.perf.tick_p95_ms", tickP95);
            AddMetric(metrics, "space4x.perf.structural_p95", structuralP95);
            AddMetric(metrics, "space4x.perf.reserved_peak_bytes", _reservedPeakBytes);
            AddMetric(metrics, "space4x.perf.sample_tick_end", tick);

            if (TryGetPerformanceBudgetStatus(state.EntityManager, out var status))
            {
                AddMetric(metrics, "space4x.perf.budget_failed", status.HasFailure);
                AddMetric(metrics, "space4x.perf.budget_value", status.ObservedValue);
                AddMetric(metrics, "space4x.perf.budget_limit", status.BudgetValue);
                AddMetric(metrics, "space4x.perf.budget_tick", status.Tick);
            }
        }

        private static float ComputePercentile(NativeList<float> samples, float percentile)
        {
            if (!samples.IsCreated || samples.Length == 0)
            {
                return 0f;
            }

            var array = samples.AsArray();
            NativeSortExtension.Sort(array);
            var index = math.clamp((int)math.ceil(array.Length * percentile) - 1, 0, array.Length - 1);
            return array[index];
        }

        private static void AddMetric(DynamicBuffer<Space4XOperatorMetric> buffer, string key, float value)
        {
            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }

        private static bool TryGetPerformanceBudgetStatus(EntityManager entityManager, out PerformanceBudgetStatus status)
        {
            status = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PerformanceBudgetStatus>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            status = query.GetSingleton<PerformanceBudgetStatus>();
            return true;
        }

        private static uint ResolveSampleStride()
        {
            var value = SystemEnv.GetEnvironmentVariable(SampleStrideEnv);
            return uint.TryParse(value, out var stride) && stride > 0 ? stride : 1u;
        }
    }
}
