using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Pooling;
using PureDOTS.Runtime.Presentation;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace PureDOTS.Systems.Telemetry
{
    /// <summary>
    /// Streams performance and health telemetry to NDJSON for headless regression detection and enforces budgets.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public sealed partial class PerformanceTelemetryExportSystem : SystemBase
    {
        private const string ExportEnvVar = "PUREDOTS_PERF_TELEMETRY_PATH";
        private const uint BudgetWarmupTicks = 5;
        private const uint FlushCadenceTicks = 60;
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        private EntityQuery _frameTimingQuery;
        private EntityQuery _companionQuery;
        private EntityQuery _universalQuery;

        private StreamWriter _writer;
        private string _exportPath;
        private bool _exportEnabled;
        private bool _exportPathLogged;
        private bool _missingExportWarningLogged;
        private readonly HashSet<string> _failuresWritten = new(StringComparer.Ordinal);
        private readonly StringBuilder _jsonBuilder = new(512);
        private bool _hasStructuralBaseline;
        private uint _lastStructuralVersion;
        private float _rollingFrameMs;
        private bool _rollingInitialized;
        private int _lastArchetypeCount;
        private bool _hasArchetypeBaseline;
        private uint _lastArchetypeWarningTick;
        private bool _headerWritten;
        private bool _flushInitialized;
        private uint _nextFlushTick;

        protected override void OnCreate()
        {
            RequireForUpdate<FrameTimingStream>();
            RequireForUpdate<TimeState>();
            RequireForUpdate<PerformanceBudgetSettings>();

            _frameTimingQuery = GetEntityQuery(
                ComponentType.ReadOnly<FrameTimingStream>(),
                ComponentType.ReadOnly<AllocationDiagnostics>(),
                ComponentType.ReadOnly<FrameTimingSample>());

            _companionQuery = GetEntityQuery(ComponentType.ReadOnly<CompanionPresentation>());
            _universalQuery = EntityManager.UniversalQuery;

            if (SystemAPI.TryGetSingletonEntity<TimeState>(out var timeEntity)
                && !EntityManager.HasComponent<PerformanceBudgetStatus>(timeEntity))
            {
                EntityManager.AddComponentData(timeEntity, default(PerformanceBudgetStatus));
            }

            _exportPath = global::System.Environment.GetEnvironmentVariable(ExportEnvVar);
            _exportEnabled = !string.IsNullOrWhiteSpace(_exportPath);
            EnsureWriterStarted();
        }

        protected override void OnDestroy()
        {
            _writer?.Dispose();
            _writer = null;
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<FrameTimingStream>(out var frameEntity))
            {
                return;
            }

            var timeEntity = SystemAPI.GetSingletonEntity<TimeState>();
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var budgets = SystemAPI.GetSingleton<PerformanceBudgetSettings>();
            var universalBudgets = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_exportEnabled && _writer == null)
            {
                EnsureWriterStarted();
            }

            var samples = EntityManager.GetBuffer<FrameTimingSample>(frameEntity);
            var allocation = EntityManager.GetComponentData<AllocationDiagnostics>(frameEntity);

            double totalFrameMs = 0d;
            for (int i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                totalFrameMs += sample.DurationMs;
                WriteTimingMetric(sample, timeState.Tick, timestampMs);
            }

            if (!_rollingInitialized)
            {
                _rollingFrameMs = (float)totalFrameMs;
                _rollingInitialized = true;
            }
            else
            {
                _rollingFrameMs = math.lerp(_rollingFrameMs, (float)totalFrameMs, 0.1f);
            }

            WriteMetric("timing.total", totalFrameMs, "ms", timeState.Tick, timestampMs);
            WriteMetric("timing.frameEma", _rollingFrameMs, "ms", timeState.Tick, timestampMs);

            var entityCount = _universalQuery.CalculateEntityCount();
            var chunkCount = _universalQuery.CalculateChunkCountWithoutFiltering();
            var archetypes = new NativeList<EntityArchetype>(Allocator.Temp);
            EntityManager.GetAllArchetypes(archetypes);
            var archetypeCount = archetypes.Length;
            var chunksPerArchetype = archetypeCount > 0 ? (double)chunkCount / archetypeCount : 0d;
            archetypes.Dispose();

            WriteMetric("entities.total", entityCount, "count", timeState.Tick, timestampMs);
            WriteMetric("chunks.total", chunkCount, "count", timeState.Tick, timestampMs);
            WriteMetric("archetypes.total", archetypeCount, "count", timeState.Tick, timestampMs);
            WriteMetric("chunks.perArchetype", chunksPerArchetype, "ratio", timeState.Tick, timestampMs);

            // Archetype spike detection (dev builds only)
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            CheckArchetypeSpike(archetypeCount, timeState.Tick);
            #endif

            var structuralVersion = EntityManager.GlobalSystemVersion;
            uint structuralDelta = 0;
            if (_hasStructuralBaseline)
            {
                structuralDelta = structuralVersion - _lastStructuralVersion;
            }
            else
            {
                _hasStructuralBaseline = true;
            }
            _lastStructuralVersion = structuralVersion;

            WriteMetric("structural.changeDelta", structuralDelta, "count", timeState.Tick, timestampMs);

            var managedBytes = GC.GetTotalMemory(false);
            WriteMetric("memory.managed.bytes", managedBytes, "bytes", timeState.Tick, timestampMs);
            WriteMetric("memory.allocated.bytes", allocation.TotalAllocatedBytes, "bytes", timeState.Tick, timestampMs);
            WriteMetric("memory.reserved.bytes", allocation.TotalReservedBytes, "bytes", timeState.Tick, timestampMs);
            WriteMetric("memory.unusedReserved.bytes", allocation.TotalUnusedReservedBytes, "bytes", timeState.Tick, timestampMs);
            WriteMetric("gc.collections.gen0", allocation.GcCollectionsGeneration0, "count", timeState.Tick, timestampMs);
            WriteMetric("gc.collections.gen1", allocation.GcCollectionsGeneration1, "count", timeState.Tick, timestampMs);
            WriteMetric("gc.collections.gen2", allocation.GcCollectionsGeneration2, "count", timeState.Tick, timestampMs);

            long commandBytes = 0;
            long snapshotBytes = 0;
            if (EntityManager.HasComponent<InputCommandLogState>(timeEntity))
            {
                var commandState = EntityManager.GetComponentData<InputCommandLogState>(timeEntity);
                commandBytes = (long)commandState.Capacity * UnsafeUtility.SizeOf<InputCommandLogEntry>();
                WriteMetric("telemetry.command.entries", commandState.Count, "count", timeState.Tick, timestampMs);
                WriteMetric("telemetry.command.capacityBytes", commandBytes, "bytes", timeState.Tick, timestampMs);
            }

            if (EntityManager.HasComponent<TickSnapshotLogState>(timeEntity))
            {
                var snapshotState = EntityManager.GetComponentData<TickSnapshotLogState>(timeEntity);
                snapshotBytes = (long)snapshotState.Capacity * UnsafeUtility.SizeOf<TickSnapshotLogEntry>();
                WriteMetric("telemetry.snapshot.entries", snapshotState.Count, "count", timeState.Tick, timestampMs);
                WriteMetric("telemetry.snapshot.capacityBytes", snapshotBytes, "bytes", timeState.Tick, timestampMs);
            }

            var pooling = NxPoolingRuntime.GatherDiagnostics();
            WriteMetric("pool.commandBuffers.borrowed", pooling.CommandBuffersBorrowed, "count", timeState.Tick, timestampMs);
            WriteMetric("pool.commandBuffers.available", pooling.CommandBuffersAvailable, "count", timeState.Tick, timestampMs);
            WriteMetric("pool.nativeLists.borrowed", pooling.NativeListsBorrowed, "count", timeState.Tick, timestampMs);
            WriteMetric("pool.nativeLists.available", pooling.NativeListsAvailable, "count", timeState.Tick, timestampMs);
            WriteMetric("pool.nativeQueues.borrowed", pooling.NativeQueuesBorrowed, "count", timeState.Tick, timestampMs);
            WriteMetric("pool.nativeQueues.available", pooling.NativeQueuesAvailable, "count", timeState.Tick, timestampMs);

            var companionCount = _companionQuery.CalculateEntityCount();
            WriteMetric("presentation.companions.active", companionCount, "count", timeState.Tick, timestampMs);

            // Export perception counters
            if (SystemAPI.TryGetSingleton<UniversalPerformanceCounters>(out var counters))
            {
                WriteMetric("perception.losChecks.physics", counters.LosChecksPhysicsThisTick, "count", timeState.Tick, timestampMs);
                WriteMetric("perception.losChecks.obstacleGrid", counters.LosChecksObstacleGridThisTick, "count", timeState.Tick, timestampMs);
                WriteMetric("perception.losChecks.unknown", counters.LosChecksUnknownThisTick, "count", timeState.Tick, timestampMs);
                WriteMetric("perception.signalCellsSampled", counters.SignalCellsSampledThisTick, "count", timeState.Tick, timestampMs);
                WriteMetric("perception.miracleEntitiesDetected", counters.MiracleEntitiesDetectedThisTick, "count", timeState.Tick, timestampMs);

                // Emit budget violation events (read from UniversalPerformanceBudget config)
                var losTotal = counters.LosChecksPhysicsThisTick + counters.LosChecksObstacleGridThisTick + counters.LosChecksUnknownThisTick;
                var maxLosChecks = universalBudgets.MaxLosRaysPerTick;
                if (losTotal > maxLosChecks)
                {
                    WriteFailRecord("perception.losChecks.total", losTotal, maxLosChecks, timeState.Tick, timestampMs);
                }

                var maxSignalCells = universalBudgets.MaxSignalCellsSampledPerTick;
                if (counters.SignalCellsSampledThisTick > maxSignalCells)
                {
                    WriteFailRecord("perception.signalCellsSampled", counters.SignalCellsSampledThisTick, maxSignalCells, timeState.Tick, timestampMs);
                }

                var unknownWarningThreshold = (int)(maxLosChecks * universalBudgets.LosChecksUnknownWarningRatio);
                if (counters.LosChecksUnknownThisTick > unknownWarningThreshold)
                {
                    WriteFailRecord("perception.losChecks.unknown", counters.LosChecksUnknownThisTick, unknownWarningThreshold, timeState.Tick, timestampMs);
                }
            }

            if (!EntityManager.Exists(timeEntity))
            {
                return;
            }

            if (!EntityManager.HasComponent<PerformanceBudgetStatus>(timeEntity))
            {
                EntityManager.AddComponentData(timeEntity, default(PerformanceBudgetStatus));
                return;
            }

            var status = EntityManager.GetComponentData<PerformanceBudgetStatus>(timeEntity);
            var statusChanged = false;

            var allowBudgetChecks = timeState.Tick >= BudgetWarmupTicks;
            if (allowBudgetChecks)
            {
                statusChanged |= CheckBudget("timing.fixedTick", totalFrameMs, budgets.FixedTickBudgetMs, timeState.Tick, timestampMs, ref status);
                statusChanged |= CheckBudget("telemetry.snapshot.capacityBytes", snapshotBytes, budgets.SnapshotRingBudgetBytes, timeState.Tick, timestampMs, ref status);
                statusChanged |= CheckBudget("telemetry.command.capacityBytes", commandBytes, budgets.CommandRingBudgetBytes, timeState.Tick, timestampMs, ref status);
                statusChanged |= CheckBudget("presentation.companions.active", companionCount, budgets.CompanionBudget, timeState.Tick, timestampMs, ref status);
            }

            if (statusChanged)
            {
                EntityManager.SetComponentData(timeEntity, status);
            }

            FlushIfNeeded(timeState.Tick);
        }

        /// <summary>
        /// Checks for archetype count spikes and logs warnings in dev builds.
        /// Rate-limited to avoid spam (max once per 60 ticks).
        /// </summary>
        private void CheckArchetypeSpike(int currentArchetypeCount, uint currentTick)
        {
            const int SpikeAbsoluteThreshold = 50; // Absolute increase threshold
            const float SpikePercentThreshold = 0.10f; // 10% increase threshold
            const uint WarningCooldownTicks = 60; // Rate limit: max one warning per 60 ticks

            if (!_hasArchetypeBaseline)
            {
                _lastArchetypeCount = currentArchetypeCount;
                _hasArchetypeBaseline = true;
                return;
            }

            // Rate limit warnings
            if (currentTick - _lastArchetypeWarningTick < WarningCooldownTicks)
            {
                return;
            }

            int delta = currentArchetypeCount - _lastArchetypeCount;
            bool spikeDetected = false;
            string reason = null;

            // Check absolute threshold
            if (delta >= SpikeAbsoluteThreshold)
            {
                spikeDetected = true;
                reason = $"absolute increase of {delta} archetypes";
            }
            // Check percentage threshold (only if baseline is significant)
            else if (_lastArchetypeCount > 0)
            {
                float percentIncrease = delta / (float)_lastArchetypeCount;
                if (percentIncrease >= SpikePercentThreshold)
                {
                    spikeDetected = true;
                    reason = $"{percentIncrease * 100f:F1}% increase ({delta} archetypes)";
                }
            }

            if (spikeDetected)
            {
                _lastArchetypeWarningTick = currentTick;
                UnityDebug.LogWarning(
                    $"[PerformanceTelemetry] Archetype spike detected at tick {currentTick}: {reason}. " +
                    $"Count: {_lastArchetypeCount} → {currentArchetypeCount}. " +
                    $"This may indicate structural change churn (add/remove components in hot loops). " +
                    $"See PERF_SYNCPOINT_AUDIT.md for refactoring guidance.");
            }

            _lastArchetypeCount = currentArchetypeCount;
        }

        private void TryOpenWriter()
        {
            try
            {
                var directory = Path.GetDirectoryName(_exportPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _writer = new StreamWriter(File.Open(_exportPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    AutoFlush = false,
                    NewLine = "\n"
                };

                if (!_exportPathLogged)
                {
                    UnityDebug.Log($"[PerformanceTelemetry] Exporting metrics to '{_exportPath}'.");
                    _exportPathLogged = true;
                }

                _flushInitialized = false;
            }
            catch (Exception ex)
            {
                UnityDebug.LogError($"[PerformanceTelemetry] Failed to initialize export '{_exportPath}': {ex}");
                _writer = null;
                _exportEnabled = false;
            }
        }

        private void EnsureWriterStarted()
        {
            if (!_exportEnabled)
            {
                return;
            }

            if (_writer == null)
            {
                TryOpenWriter();
            }

            WriteHeaderRecord();
        }

        private void WriteHeaderRecord()
        {
            if (_writer == null || _headerWritten)
            {
                return;
            }

            _jsonBuilder.Clear();
            _jsonBuilder.Append("{\"type\":\"header\"");
            _jsonBuilder.Append(",\"timestamp\":").Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (GitMetadataUtility.TryReadMetadata(out var metadata))
            {
                if (!string.IsNullOrWhiteSpace(metadata.Commit))
                {
                    _jsonBuilder.Append(",\"commit\":\"").Append(metadata.Commit).Append('\"');
                }

                if (!string.IsNullOrWhiteSpace(metadata.Branch))
                {
                    _jsonBuilder.Append(",\"branch\":\"").Append(metadata.Branch).Append('\"');
                }
            }
            _jsonBuilder.Append('}');
            _writer.WriteLine(_jsonBuilder.ToString());
            _writer.Flush();
            _headerWritten = true;
        }

        private void WriteTimingMetric(in FrameTimingSample sample, uint tick, long timestampMs)
        {
            var group = sample.Group;
            var metricKey = FrameTimingUtility.GetMetricKey(group).ToString();
            var groupLabel = FrameTimingUtility.GetGroupLabel(group).ToString();
            var systemCount = sample.SystemCount;
            var budgetMs = sample.BudgetMs;
            var flags = sample.Flags;

            WriteMetric(metricKey, sample.DurationMs, "ms", tick, timestampMs, builder =>
            {
                builder.Add("group", groupLabel);
                builder.Add("systemCount", systemCount);
                if (budgetMs > 0f)
                {
                    builder.Add("budgetMs", budgetMs);
                    builder.Add("budgetExceeded", (flags & FrameTimingFlags.BudgetExceeded) != 0);
                }

                builder.Add("catchUp", (flags & FrameTimingFlags.CatchUp) != 0);
            });
        }

        private bool CheckBudget(string metric, double value, double budget, uint tick, long timestampMs, ref PerformanceBudgetStatus status)
        {
            if (budget <= 0d || value <= budget)
            {
                return false;
            }

            if (_exportEnabled && _writer != null && _failuresWritten.Add(metric))
            {
                WriteFailRecord(metric, value, budget, tick, timestampMs);
                _writer.Flush();
            }
            else if (!_exportEnabled && !_missingExportWarningLogged)
            {
                if (!Application.isBatchMode)
                {
                    UnityDebug.LogWarning("[PerformanceTelemetry] Budget failure detected but PUREDOTS_PERF_TELEMETRY_PATH is not set; telemetry will not be written.");
                }

                _missingExportWarningLogged = true;
            }

            if (status.HasFailure == 0)
            {
                status.HasFailure = 1;
                FixedString64Bytes metricLabel = metric;
                status.Metric = metricLabel;
                status.ObservedValue = (float)value;
                status.BudgetValue = (float)budget;
                status.Tick = tick;
                return true;
            }

            return false;
        }

        private void WriteMetric(string metric, double value, string unit, uint tick, long timestampMs, Action<TagBuilder> tagWriter = null)
        {
            if (!_exportEnabled || _writer == null)
            {
                return;
            }

            AppendMetricHeader(metric, value, unit, tick, timestampMs);

            if (tagWriter != null)
            {
                var tags = new TagBuilder(_jsonBuilder);
                tagWriter(tags);
                tags.Dispose();
            }

            _jsonBuilder.Append('}');
            _writer.WriteLine(_jsonBuilder.ToString());
        }

        private void AppendMetricHeader(string metric, double value, string unit, uint tick, long timestampMs)
        {
            _jsonBuilder.Clear();
            _jsonBuilder.Append("{\"type\":\"metric\"");
            _jsonBuilder.Append(",\"timestamp\":").Append(timestampMs);
            _jsonBuilder.Append(",\"tick\":").Append(tick);
            _jsonBuilder.Append(",\"metric\":\"").Append(metric).Append('\"');
            _jsonBuilder.Append(",\"value\":").Append(value.ToString("G17", InvariantCulture));
            if (!string.IsNullOrEmpty(unit))
            {
                _jsonBuilder.Append(",\"unit\":\"").Append(unit).Append('\"');
            }
        }

        private void WriteFailRecord(string metric, double value, double budget, uint tick, long timestampMs)
        {
            _jsonBuilder.Clear();
            _jsonBuilder.Append("{\"type\":\"fail\"");
            _jsonBuilder.Append(",\"timestamp\":").Append(timestampMs);
            _jsonBuilder.Append(",\"tick\":").Append(tick);
            _jsonBuilder.Append(",\"metric\":\"").Append(metric).Append('\"');
            _jsonBuilder.Append(",\"value\":").Append(value.ToString("G17", InvariantCulture));
            _jsonBuilder.Append(",\"budget\":").Append(budget.ToString("G17", InvariantCulture));
            _jsonBuilder.Append('}');
            _writer?.WriteLine(_jsonBuilder.ToString());
        }

        private void FlushIfNeeded(uint tick)
        {
            if (!_exportEnabled || _writer == null)
            {
                return;
            }

            if (!_flushInitialized)
            {
                _nextFlushTick = tick + FlushCadenceTicks;
                _flushInitialized = true;
                return;
            }

            if (tick >= _nextFlushTick)
            {
                _writer.Flush();
                _nextFlushTick = tick + FlushCadenceTicks;
            }
        }

        private struct TagBuilder : IDisposable
        {
            private readonly StringBuilder _builder;
            private bool _hasEntries;

            public TagBuilder(StringBuilder builder)
            {
                _builder = builder;
                _hasEntries = false;
                _builder.Append(",\"tags\":{");
            }

            public void Add(string key, string value)
            {
                AppendSeparator();
                _builder.Append('\"').Append(key).Append("\":\"").Append(value).Append('\"');
            }

            public void Add(string key, double value)
            {
                AppendSeparator();
                _builder.Append('\"').Append(key).Append("\":").Append(value.ToString("G17", InvariantCulture));
            }

            public void Add(string key, int value)
            {
                AppendSeparator();
                _builder.Append('\"').Append(key).Append("\":").Append(value);
            }

            public void Add(string key, bool value)
            {
                AppendSeparator();
                _builder.Append('\"').Append(key).Append("\":").Append(value ? "true" : "false");
            }

            private void AppendSeparator()
            {
                if (_hasEntries)
                {
                    _builder.Append(',');
                }

                _hasEntries = true;
            }

            public void Dispose()
            {
                _builder.Append('}');
            }
        }
    }
}
