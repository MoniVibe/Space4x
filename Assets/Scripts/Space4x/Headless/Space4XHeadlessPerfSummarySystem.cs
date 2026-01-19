using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Telemetry;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityTime = UnityEngine.Time;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XHeadlessPerfSummarySystem : ISystem
    {
        private const uint MaxSamples = 2048;

        private NativeList<float> _tickSamples;
        private NativeList<float> _structuralSamples;
        private double _lastTickTime;
        private byte _initialized;
        private byte _done;
        private uint _sampleInterval;
        private uint _startTick;
        private uint _endTick;
        private uint _lastStructuralVersion;
        private byte _hasStructuralBaseline;
        private long _reservedBytesPeak;
        private long _allocatedBytesPeak;
        private float _tickMin;
        private float _tickMax;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();

            _tickSamples = new NativeList<float>(Allocator.Persistent);
            _structuralSamples = new NativeList<float>(Allocator.Persistent);
            _tickMin = float.MaxValue;
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
            if (_done != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                _lastTickTime = UnityTime.realtimeSinceStartup;
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                _lastTickTime = UnityTime.realtimeSinceStartup;
                return;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (_sampleInterval == 0)
            {
                _startTick = runtime.StartTick;
                _endTick = runtime.EndTick;
                var totalTicks = runtime.EndTick > runtime.StartTick ? runtime.EndTick - runtime.StartTick : 0u;
                _sampleInterval = math.max(1u, totalTicks > 0 ? totalTicks / MaxSamples : 1u);
            }

            var now = UnityTime.realtimeSinceStartup;
            if (_initialized == 0)
            {
                _lastTickTime = now;
                _initialized = 1;
            }
            else
            {
                var tickMs = (float)((now - _lastTickTime) * 1000.0);
                _lastTickTime = now;
                _tickMax = math.max(_tickMax, tickMs);
                _tickMin = math.min(_tickMin, tickMs);

                var sampleOffset = timeState.Tick >= _startTick ? timeState.Tick - _startTick : timeState.Tick;
                if (_sampleInterval > 0 && sampleOffset % _sampleInterval == 0)
                {
                    _tickSamples.Add(tickMs);
                    _structuralSamples.Add(CaptureStructuralDelta(ref state));
                }
            }

            CaptureMemoryPeaks(ref state);

            if (runtime.EndTick > 0 && timeState.Tick >= runtime.EndTick)
            {
                EmitSummary(ref state);
                _done = 1;
            }
        }

        private float CaptureStructuralDelta(ref SystemState state)
        {
            var version = state.EntityManager.GlobalSystemVersion;
            float delta = 0f;
            if (_hasStructuralBaseline != 0)
            {
                delta = version - _lastStructuralVersion;
            }
            else
            {
                _hasStructuralBaseline = 1;
            }

            _lastStructuralVersion = version;
            return delta;
        }

        private void CaptureMemoryPeaks(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<FrameTimingStream>(out var frameEntity))
            {
                return;
            }

            if (!state.EntityManager.HasComponent<AllocationDiagnostics>(frameEntity))
            {
                return;
            }

            var allocation = state.EntityManager.GetComponentData<AllocationDiagnostics>(frameEntity);
            if (allocation.TotalReservedBytes > _reservedBytesPeak)
            {
                _reservedBytesPeak = allocation.TotalReservedBytes;
            }

            if (allocation.TotalAllocatedBytes > _allocatedBytesPeak)
            {
                _allocatedBytesPeak = allocation.TotalAllocatedBytes;
            }
        }

        private void EmitSummary(ref SystemState state)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            var tickP50 = ComputePercentile(_tickSamples, 0.50f);
            var tickP95 = ComputePercentile(_tickSamples, 0.95f);
            var tickP99 = ComputePercentile(_tickSamples, 0.99f);
            var structuralP95 = ComputePercentile(_structuralSamples, 0.95f);

            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.fixed_step.ms.p50"), tickP50);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.fixed_step.ms.p95"), tickP95);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.fixed_step.ms.p99"), tickP99);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.fixed_step.ms.min"), _tickMin);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.fixed_step.ms.max"), _tickMax);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.structural.delta.p95"), structuralP95);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.memory.reserved.bytes.peak"), _reservedBytesPeak);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.memory.allocated.bytes.peak"), _allocatedBytesPeak);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.samples.tick_count"), _tickSamples.Length);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("perf.samples.structural_count"), _structuralSamples.Length);
        }

        private static float ComputePercentile(NativeList<float> samples, float percentile)
        {
            if (!samples.IsCreated || samples.Length == 0)
            {
                return float.NaN;
            }

            using var sorted = new NativeArray<float>(samples.Length, Allocator.Temp);
            NativeArray<float>.Copy(samples.AsArray(), sorted);
            sorted.Sort();

            var position = (samples.Length - 1) * percentile;
            var index = (int)math.ceil(position);
            index = math.clamp(index, 0, samples.Length - 1);
            return sorted[index];
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
