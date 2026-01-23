using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Emits lightweight perf summary metrics into standard telemetry for headless gating.
    /// </summary>
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(Space4XMovementTelemetrySystem))]
    public partial struct Space4XPerfSummaryTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;
        private EntityQuery _frameQuery;
        private uint _lastStructuralVersion;
        private byte _hasStructuralBaseline;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryExportConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<FrameTimingStream>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _frameQuery = SystemAPI.QueryBuilder()
                .WithAll<FrameTimingStream, AllocationDiagnostics, FrameTimingSample>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var tick = timeState.Tick;
            var cadence = config.CadenceTicks > 0 ? config.CadenceTicks : 30u;
            if (tick % cadence != 0u)
            {
                return;
            }

            if (_frameQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var frameEntity = _frameQuery.GetSingletonEntity();
            var samples = state.EntityManager.GetBuffer<FrameTimingSample>(frameEntity);
            var allocation = state.EntityManager.GetComponentData<AllocationDiagnostics>(frameEntity);

            double totalMs = 0d;
            for (int i = 0; i < samples.Length; i++)
            {
                totalMs += samples[i].DurationMs;
            }

            if (double.IsNaN(totalMs) || double.IsInfinity(totalMs) || totalMs < 0d)
            {
                totalMs = 0d;
            }

            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            var entityCount = state.EntityManager.UniversalQuery.CalculateEntityCount();
            var chunkCount = state.EntityManager.UniversalQuery.CalculateChunkCountWithoutFiltering();
            var structuralVersion = state.EntityManager.GlobalSystemVersion;
            uint structuralDelta = 0;
            if (_hasStructuralBaseline != 0)
            {
                structuralDelta = structuralVersion - _lastStructuralVersion;
            }
            else
            {
                _hasStructuralBaseline = 1;
            }
            _lastStructuralVersion = structuralVersion;

            buffer.AddMetric("perf.timing.total_ms", (float)totalMs, TelemetryMetricUnit.DurationMilliseconds);
            buffer.AddMetric("perf.memory.reserved.bytes", allocation.TotalReservedBytes, TelemetryMetricUnit.Bytes);
            buffer.AddMetric("perf.entities.total", entityCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("perf.chunks.total", chunkCount, TelemetryMetricUnit.Count);
            buffer.AddMetric("perf.structural.change_delta", structuralDelta, TelemetryMetricUnit.Count);
        }
    }
}
