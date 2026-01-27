using System;
using System.Globalization;
using System.IO;
using System.Text;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using SystemEnvironment = global::System.Environment;
using UnityDebug = UnityEngine.Debug;

namespace PureDOTS.Systems.Telemetry
{
    /// <summary>
    /// Shared NDJSON exporter that flushes telemetry metrics, frame timing samples,
    /// behavior telemetry records, and replay metadata to a single stream.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(AiTrainingTelemetrySystem))]
    public partial class TelemetryExportSystem : SystemBase
    {
        private bool _headerWritten;
        private uint _knownConfigVersion;
        private FixedString128Bytes _runIdCache;
        private string _runIdString;
        private string _activePath;
        private string _scenarioIdString;
        private uint _scenarioSeed;
        private bool _outputCapReached;
        private bool _truncateOutput;
        private bool _capRecordWritten;
        private StringBuilder _lineBuilder;
        private StringWriter _lineWriter;
        private bool _oracleProbeEnabled;
        private bool _oracleProbeInitialized;
        private string _oracleProbeLoggedRunId;
        private const string OracleProbeVersion = "oracle_probe_v1";

        protected override void OnCreate()
        {
            RequireForUpdate<TelemetryExportConfig>();
            _activePath = string.Empty;
            _scenarioIdString = string.Empty;
            _lineBuilder = new StringBuilder(512);
            _lineWriter = new StringWriter(_lineBuilder, CultureInfo.InvariantCulture) { NewLine = "\n" };
            EnsureExportStateExists();
        }

        protected override void OnDestroy()
        {
            _lineWriter?.Dispose();
            _lineWriter = null;
        }

        protected override void OnUpdate()
        {
            var configEntity = SystemAPI.GetSingletonEntity<TelemetryExportConfig>();
            var config = SystemAPI.GetComponent<TelemetryExportConfig>(configEntity);
            var exportState = SystemAPI.GetSingletonRW<TelemetryExportState>();

            if (config.Enabled == 0 || config.OutputPath.Length == 0)
            {
                _headerWritten = false;
                _outputCapReached = false;
                _truncateOutput = true;
                _capRecordWritten = false;
                ClearExportState(ref exportState.ValueRW);
                return;
            }

            if (config.RunId.Length == 0)
            {
                config.RunId = GenerateRunId();
                config.Version++;
                SystemAPI.SetComponent(configEntity, config);
            }

            if (_knownConfigVersion != config.Version)
            {
                _knownConfigVersion = config.Version;
                _headerWritten = false;
                _runIdCache = config.RunId;
                _runIdString = _runIdCache.ToString();
                _activePath = config.OutputPath.ToString();
                _outputCapReached = false;
                _truncateOutput = true;
                _capRecordWritten = false;
                _oracleProbeLoggedRunId = string.Empty;
                ResetExportState(ref exportState.ValueRW, config);
            }
            else if (!exportState.ValueRO.RunId.Equals(config.RunId) || exportState.ValueRO.MaxOutputBytes != config.MaxOutputBytes)
            {
                ResetExportState(ref exportState.ValueRW, config);
            }

            if (string.IsNullOrEmpty(_activePath))
            {
                return;
            }

            if (exportState.ValueRO.CapReached != 0)
            {
                _outputCapReached = true;
                return;
            }

            ResolveScenarioMetadata();
            EnsureOracleProbeState();

            try
            {
                EnsureDirectory(_activePath);

                bool truncate = _truncateOutput || !_headerWritten;
                using var writer = OpenWriter(_activePath, truncate, out var bytesWritten);

                if (truncate)
                {
                    _truncateOutput = false;
                }

                if (bytesWritten < exportState.ValueRO.BytesWritten)
                {
                    bytesWritten = exportState.ValueRO.BytesWritten;
                    writer.BaseStream.Position = (long)bytesWritten;
                }

                var tick = GetCurrentTick();
                ulong maxBytes = config.MaxOutputBytes;
                string truncatedRecord = null;
                ulong reserveBytes = 0;

                if (maxBytes > 0)
                {
                    truncatedRecord = BuildTruncatedRecord(tick, maxBytes);
                    reserveBytes = (ulong)Encoding.UTF8.GetByteCount(truncatedRecord);
                    if (reserveBytes >= maxBytes || bytesWritten >= maxBytes)
                    {
                        HandleCapReached(writer, ref bytesWritten, maxBytes, truncatedRecord, reserveBytes, ref exportState.ValueRW);
                        return;
                    }
                }

                if (!_headerWritten)
                {
                    if (!TryWriteRecord(writer, ref bytesWritten, maxBytes, reserveBytes, recordWriter => WriteRunHeader(recordWriter, config)))
                    {
                        HandleCapReached(writer, ref bytesWritten, maxBytes, truncatedRecord, reserveBytes, ref exportState.ValueRW);
                        return;
                    }

                    _headerWritten = true;
                }

                if ((config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) != 0)
                {
                    var cadence = config.CadenceTicks > 0 ? config.CadenceTicks : 30u;
                    var shouldExport = cadence <= 1u || tick % cadence == 0u;
                    if (shouldExport)
                    {
                        if (!WriteOracleProbe(writer, tick, ref bytesWritten, maxBytes, reserveBytes))
                        {
                            HandleCapReached(writer, ref bytesWritten, maxBytes, truncatedRecord, reserveBytes, ref exportState.ValueRW);
                            return;
                        }

                        if (!ExportTelemetryMetrics(writer, tick, ref bytesWritten, maxBytes, reserveBytes))
                        {
                            HandleCapReached(writer, ref bytesWritten, maxBytes, truncatedRecord, reserveBytes, ref exportState.ValueRW);
                            return;
                        }
                    }
                    else
                    {
                        ClearTelemetryMetricsBuffer();
                    }
                }

                if ((config.Flags & TelemetryExportFlags.IncludeFrameTiming) != 0)
                {
                    if (!ExportFrameTiming(writer, tick, ref bytesWritten, maxBytes, reserveBytes))
                    {
                        HandleCapReached(writer, ref bytesWritten, maxBytes, truncatedRecord, reserveBytes, ref exportState.ValueRW);
                        return;
                    }
                }

                if ((config.Flags & TelemetryExportFlags.IncludeBehaviorTelemetry) != 0)
                {
                    if (!ExportBehaviorTelemetry(writer, ref bytesWritten, maxBytes, reserveBytes))
                    {
                        HandleCapReached(writer, ref bytesWritten, maxBytes, truncatedRecord, reserveBytes, ref exportState.ValueRW);
                        return;
                    }
                }

                if ((config.Flags & TelemetryExportFlags.IncludeReplayEvents) != 0)
                {
                    if (!ExportReplayTelemetry(writer, tick, ref bytesWritten, maxBytes, reserveBytes))
                    {
                        HandleCapReached(writer, ref bytesWritten, maxBytes, truncatedRecord, reserveBytes, ref exportState.ValueRW);
                        return;
                    }
                }

                if ((config.Flags & TelemetryExportFlags.IncludeTelemetryEvents) != 0)
                {
                    if (!ExportTelemetryEvents(writer, ref bytesWritten, maxBytes, reserveBytes))
                    {
                        HandleCapReached(writer, ref bytesWritten, maxBytes, truncatedRecord, reserveBytes, ref exportState.ValueRW);
                        return;
                    }
                }

                writer.Flush();
                exportState.ValueRW.BytesWritten = bytesWritten;
                exportState.ValueRW.MaxOutputBytes = maxBytes;
                exportState.ValueRW.RunId = config.RunId;
                exportState.ValueRW.CapReached = _outputCapReached ? (byte)1 : (byte)0;
            }
            catch (Exception ex)
            {
                UnityDebug.LogError($"[TelemetryExportSystem] Failed to export telemetry to '{_activePath}': {ex}");
            }
        }

        private void EnsureExportStateExists()
        {
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryExportState>());
            if (query.IsEmpty)
            {
                EntityManager.CreateEntity(typeof(TelemetryExportState));
            }
        }

        private static void ClearExportState(ref TelemetryExportState state)
        {
            state.RunId = default;
            state.BytesWritten = 0;
            state.MaxOutputBytes = 0;
            state.CapReached = 0;
        }

        private static void ResetExportState(ref TelemetryExportState state, in TelemetryExportConfig config)
        {
            state.RunId = config.RunId;
            state.BytesWritten = 0;
            state.MaxOutputBytes = config.MaxOutputBytes;
            state.CapReached = 0;
        }

        private StreamWriter OpenWriter(string path, bool truncate, out ulong bytesWritten)
        {
            FileStream fileStream;
            if (truncate)
            {
                fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                bytesWritten = 0;
            }
            else
            {
                fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                fileStream.Position = fileStream.Length;
                bytesWritten = (ulong)fileStream.Position;
            }

            return new StreamWriter(fileStream, Encoding.UTF8) { NewLine = "\n" };
        }

        private bool TryWriteRecord(StreamWriter writer, ref ulong bytesWritten, ulong maxBytes, ulong reserveBytes, Action<TextWriter> writeRecord)
        {
            _lineBuilder.Clear();
            writeRecord(_lineWriter);
            var line = _lineBuilder.ToString();
            var recordBytes = (ulong)Encoding.UTF8.GetByteCount(line);
            if (maxBytes > 0 && bytesWritten + recordBytes + reserveBytes > maxBytes)
            {
                return false;
            }

            writer.Write(line);
            bytesWritten += recordBytes;
            return true;
        }

        private string BuildTruncatedRecord(uint tick, ulong maxBytes)
        {
            _lineBuilder.Clear();
            _lineWriter.Write("{\"type\":\"telemetryTruncated\",\"runId\":\"");
            WriteEscapedString(_lineWriter, _runIdString);
            _lineWriter.Write("\",\"scenario\":\"");
            WriteEscapedString(_lineWriter, _scenarioIdString);
            _lineWriter.Write("\",\"seed\":");
            _lineWriter.Write(_scenarioSeed);
            _lineWriter.Write(",\"tick\":");
            _lineWriter.Write(tick);
            _lineWriter.Write(",\"maxBytes\":");
            _lineWriter.Write(maxBytes);
            _lineWriter.WriteLine("}");
            return _lineBuilder.ToString();
        }

        private void HandleCapReached(StreamWriter writer, ref ulong bytesWritten, ulong maxBytes, string truncatedRecord, ulong truncatedBytes, ref TelemetryExportState exportState)
        {
            if (!_outputCapReached)
            {
                if (!_capRecordWritten && !string.IsNullOrEmpty(truncatedRecord))
                {
                    var recordBytes = truncatedBytes > 0 ? truncatedBytes : (ulong)Encoding.UTF8.GetByteCount(truncatedRecord);
                    if (maxBytes == 0 || bytesWritten + recordBytes <= maxBytes)
                    {
                        writer.Write(truncatedRecord);
                        bytesWritten += recordBytes;
                        _capRecordWritten = true;
                    }
                }

                writer.Flush();
                _outputCapReached = true;
                UnityDebug.LogWarning($"[TelemetryExportSystem] Output cap reached ({bytesWritten} of {maxBytes} bytes). Telemetry export paused.");
            }

            exportState.BytesWritten = bytesWritten;
            exportState.MaxOutputBytes = maxBytes;
            exportState.CapReached = 1;
        }

        private static FixedString128Bytes GenerateRunId()
        {
            FixedString128Bytes id = default;
            var guid = Guid.NewGuid().ToString("N");
            for (int i = 0; i < guid.Length && i < id.Capacity; i++)
            {
                id.Append(guid[i]);
            }
            return id;
        }

        private static void EnsureDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private uint GetCurrentTick()
        {
            if (SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioTick) && scenarioTick.Tick > 0)
            {
                return scenarioTick.Tick;
            }

            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickState))
            {
                var tick = tickState.Tick;
                if (SystemAPI.TryGetSingleton<TimeState>(out var timeState) && timeState.Tick > tick)
                {
                    tick = timeState.Tick;
                }

                if (tick == 0 && Application.isBatchMode)
                {
                    var dt = (float)SystemAPI.Time.DeltaTime;
                    var elapsed = (float)SystemAPI.Time.ElapsedTime;
                    if (dt > 0f && elapsed > 0f)
                    {
                        var elapsedTick = (uint)(elapsed / dt);
                        if (elapsedTick > tick)
                        {
                            tick = elapsedTick;
                        }
                    }
                }

                return tick;
            }

            if (SystemAPI.TryGetSingleton<TimeState>(out var legacyTime))
            {
                var tick = legacyTime.Tick;
                if (tick == 0 && Application.isBatchMode)
                {
                    var dt = (float)SystemAPI.Time.DeltaTime;
                    var elapsed = (float)SystemAPI.Time.ElapsedTime;
                    if (dt > 0f && elapsed > 0f)
                    {
                        var elapsedTick = (uint)(elapsed / dt);
                        if (elapsedTick > tick)
                        {
                            tick = elapsedTick;
                        }
                    }
                }

                return tick;
            }

            return 0;
        }

        private void EnsureOracleProbeState()
        {
            if (_oracleProbeInitialized)
            {
                return;
            }

            _oracleProbeEnabled = IsTruthyEnv("PUREDOTS_TELEMETRY_ORACLE_PROBE");
            _oracleProbeInitialized = true;
        }

        private static bool IsTruthyEnv(string key)
        {
            var value = global::System.Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            value = value.Trim();
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private bool WriteOracleProbe(StreamWriter writer, uint tick, ref ulong bytesWritten, ulong maxBytes, ulong reserveBytes)
        {
            if (!_oracleProbeEnabled)
            {
                return true;
            }

            var metricCount = 0;
            var oracleCandidateCount = 0;
            var oracleTelemetryCount = 0;
            var oracleAiCount = 0;
            var oracleMoveCount = 0;
            var oraclePowerCount = 0;
            var oracleModuleCount = 0;
            var loopSkipCount = 0;

            if (SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity) &&
                EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                var buffer = EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
                metricCount = buffer.Length;
                for (int i = 0; i < buffer.Length; i++)
                {
                    var key = buffer[i].Key.ToString();
                    if (key.StartsWith("telemetry.oracle.", StringComparison.OrdinalIgnoreCase))
                    {
                        oracleTelemetryCount += 1;
                        oracleCandidateCount += 1;
                    }
                    else if (key.StartsWith("ai.", StringComparison.OrdinalIgnoreCase))
                    {
                        oracleAiCount += 1;
                        oracleCandidateCount += 1;
                    }
                    else if (key.StartsWith("move.", StringComparison.OrdinalIgnoreCase))
                    {
                        oracleMoveCount += 1;
                        oracleCandidateCount += 1;
                    }
                    else if (key.StartsWith("power.", StringComparison.OrdinalIgnoreCase))
                    {
                        oraclePowerCount += 1;
                        oracleCandidateCount += 1;
                    }
                    else if (key.StartsWith("module.", StringComparison.OrdinalIgnoreCase))
                    {
                        oracleModuleCount += 1;
                        oracleCandidateCount += 1;
                    }

                    var loopLabel = GetLoopLabel(key);
                    if (!ShouldWriteLoop(loopLabel))
                    {
                        loopSkipCount += 1;
                    }
                }
            }

            var timeTickPresent = 0;
            var scenarioTickPresent = 0;
            var tickTimePresent = 0;
            uint timeTick = 0;
            uint scenarioTick = 0;
            uint tickTime = 0;

            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                timeTick = timeState.Tick;
                timeTickPresent = 1;
            }

            if (SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioState))
            {
                scenarioTick = scenarioState.Tick;
                scenarioTickPresent = 1;
            }

            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickState))
            {
                tickTime = tickState.Tick;
                tickTimePresent = 1;
            }

            if (!TryWriteRecord(writer, ref bytesWritten, maxBytes, reserveBytes, recordWriter =>
                {
                    recordWriter.Write("{\"type\":\"debug\",\"runId\":\"");
                    WriteEscapedString(recordWriter, _runIdString);
                    recordWriter.Write("\",\"scenario\":\"");
                    WriteEscapedString(recordWriter, _scenarioIdString);
                    recordWriter.Write("\",\"debugKind\":\"oracleProbe\",\"tick\":");
                    recordWriter.Write(tick);
                    recordWriter.Write(",\"metrics_total\":");
                    recordWriter.Write(metricCount);
                    recordWriter.Write(",\"oracle_candidates\":");
                    recordWriter.Write(oracleCandidateCount);
                    recordWriter.Write(",\"oracle_telemetry\":");
                    recordWriter.Write(oracleTelemetryCount);
                    recordWriter.Write(",\"oracle_ai\":");
                    recordWriter.Write(oracleAiCount);
                    recordWriter.Write(",\"oracle_move\":");
                    recordWriter.Write(oracleMoveCount);
                    recordWriter.Write(",\"oracle_power\":");
                    recordWriter.Write(oraclePowerCount);
                    recordWriter.Write(",\"oracle_module\":");
                    recordWriter.Write(oracleModuleCount);
                    recordWriter.Write(",\"loop_skipped\":");
                    recordWriter.Write(loopSkipCount);
                    recordWriter.Write(",\"tick_time\":");
                    recordWriter.Write(timeTick);
                    recordWriter.Write(",\"tick_time_present\":");
                    recordWriter.Write(timeTickPresent);
                    recordWriter.Write(",\"tick_scenario\":");
                    recordWriter.Write(scenarioTick);
                    recordWriter.Write(",\"tick_scenario_present\":");
                    recordWriter.Write(scenarioTickPresent);
                    recordWriter.Write(",\"tick_ticktime\":");
                    recordWriter.Write(tickTime);
                    recordWriter.Write(",\"tick_ticktime_present\":");
                    recordWriter.Write(tickTimePresent);
                    recordWriter.WriteLine("}");
                }))
            {
                return false;
            }

            if (_oracleProbeLoggedRunId != _runIdString)
            {
                UnityDebug.Log($"[TelemetryExportSystem] OracleProbe runId={_runIdString} metrics_total={metricCount} oracle_candidates={oracleCandidateCount} oracle_telemetry={oracleTelemetryCount} oracle_ai={oracleAiCount} oracle_move={oracleMoveCount} oracle_power={oraclePowerCount} oracle_module={oracleModuleCount} loop_skipped={loopSkipCount} tick_export={tick} tick_time={timeTick} tick_scenario={scenarioTick} tick_ticktime={tickTime}");
                _oracleProbeLoggedRunId = _runIdString;
            }

            return true;
        }

        private bool ExportTelemetryMetrics(StreamWriter writer, uint tick, ref ulong bytesWritten, ulong maxBytes, ulong reserveBytes)
        {
            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                return true;
            }

            if (!EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return true;
            }

            var buffer = EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            if (buffer.Length == 0)
            {
                return true;
            }

            var culture = CultureInfo.InvariantCulture;
            var completed = true;
            for (int i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                var loopLabel = GetLoopLabel(metric.Key.ToString());
                if (!ShouldWriteLoop(loopLabel))
                {
                    continue;
                }

                if (!TryWriteRecord(writer, ref bytesWritten, maxBytes, reserveBytes, recordWriter =>
                    {
                        recordWriter.Write("{\"type\":\"metric\",\"runId\":\"");
                        WriteEscapedString(recordWriter, _runIdString);
                        recordWriter.Write("\",\"scenario\":\"");
                        WriteEscapedString(recordWriter, _scenarioIdString);
                        recordWriter.Write("\",\"seed\":");
                        recordWriter.Write(_scenarioSeed);
                        recordWriter.Write(",\"tick\":");
                        recordWriter.Write(tick);
                        recordWriter.Write(",\"loop\":\"");
                        WriteEscapedString(recordWriter, loopLabel);
                        recordWriter.Write("\",\"key\":\"");
                        WriteEscapedString(recordWriter, metric.Key.ToString());
                        recordWriter.Write("\",\"value\":");
                        recordWriter.Write(metric.Value.ToString("R", culture));
                        recordWriter.Write(",\"unit\":\"");
                        recordWriter.Write(GetUnitLabel(metric.Unit));
                        recordWriter.WriteLine("\"}");
                    }))
                {
                    completed = false;
                    break;
                }
            }

            if (completed)
            {
                buffer.Clear();
            }

            return completed;
        }

        private void ClearTelemetryMetricsBuffer()
        {
            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                return;
            }

            if (!EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return;
            }

            var buffer = EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            if (buffer.Length > 0)
            {
                buffer.Clear();
            }
        }

        private bool ExportFrameTiming(StreamWriter writer, uint tick, ref ulong bytesWritten, ulong maxBytes, ulong reserveBytes)
        {
            if (!SystemAPI.TryGetSingletonEntity<FrameTimingStream>(out var frameEntity))
            {
                return true;
            }

            if (!EntityManager.HasBuffer<FrameTimingSample>(frameEntity))
            {
                return true;
            }

            var samples = EntityManager.GetBuffer<FrameTimingSample>(frameEntity);
            if (samples.Length > 0)
            {
                var culture = CultureInfo.InvariantCulture;
                var completed = true;
                for (int i = 0; i < samples.Length; i++)
                {
                    var sample = samples[i];
                    var label = FrameTimingRecorderSystem.GetGroupLabel(sample.Group).ToString();
                    if (!TryWriteRecord(writer, ref bytesWritten, maxBytes, reserveBytes, recordWriter =>
                        {
                            recordWriter.Write("{\"type\":\"frameTiming\",\"runId\":\"");
                            WriteEscapedString(recordWriter, _runIdString);
                            recordWriter.Write("\",\"scenario\":\"");
                            WriteEscapedString(recordWriter, _scenarioIdString);
                            recordWriter.Write("\",\"seed\":");
                            recordWriter.Write(_scenarioSeed);
                            recordWriter.Write(",\"tick\":");
                            recordWriter.Write(tick);
                            recordWriter.Write(",\"loop\":\"\"");
                            recordWriter.Write(",\"group\":\"");
                            WriteEscapedString(recordWriter, label);
                            recordWriter.Write("\",\"durationMs\":");
                            recordWriter.Write(sample.DurationMs.ToString("R", culture));
                            recordWriter.Write(",\"budgetMs\":");
                            recordWriter.Write(sample.BudgetMs.ToString("R", culture));
                            recordWriter.Write(",\"systemCount\":");
                            recordWriter.Write(sample.SystemCount);
                            recordWriter.Write(",\"budgetExceeded\":");
                            recordWriter.Write((sample.Flags & FrameTimingFlags.BudgetExceeded) != 0 ? "true" : "false");
                            recordWriter.Write(",\"catchUp\":");
                            recordWriter.Write((sample.Flags & FrameTimingFlags.CatchUp) != 0 ? "true" : "false");
                            recordWriter.WriteLine("}");
                        }))
                    {
                        completed = false;
                        break;
                    }
                }

                if (!completed)
                {
                    return false;
                }
            }

            if (EntityManager.HasComponent<AllocationDiagnostics>(frameEntity))
            {
                var allocations = EntityManager.GetComponentData<AllocationDiagnostics>(frameEntity);
                if (!TryWriteRecord(writer, ref bytesWritten, maxBytes, reserveBytes, recordWriter =>
                    {
                        recordWriter.Write("{\"type\":\"allocation\",\"runId\":\"");
                        WriteEscapedString(recordWriter, _runIdString);
                        recordWriter.Write("\",\"scenario\":\"");
                        WriteEscapedString(recordWriter, _scenarioIdString);
                        recordWriter.Write("\",\"seed\":");
                        recordWriter.Write(_scenarioSeed);
                        recordWriter.Write(",\"tick\":");
                        recordWriter.Write(tick);
                        recordWriter.Write(",\"loop\":\"\"");
                        recordWriter.Write(",\"totalAllocated\":");
                        recordWriter.Write(allocations.TotalAllocatedBytes);
                        recordWriter.Write(",\"totalReserved\":");
                        recordWriter.Write(allocations.TotalReservedBytes);
                        recordWriter.Write(",\"unusedReserved\":");
                        recordWriter.Write(allocations.TotalUnusedReservedBytes);
                        recordWriter.Write(",\"gc0\":");
                        recordWriter.Write(allocations.GcCollectionsGeneration0);
                        recordWriter.Write(",\"gc1\":");
                        recordWriter.Write(allocations.GcCollectionsGeneration1);
                        recordWriter.Write(",\"gc2\":");
                        recordWriter.Write(allocations.GcCollectionsGeneration2);
                        recordWriter.WriteLine("}");
                    }))
                {
                    return false;
                }
            }

            samples.Clear();

            return true;
        }

        private bool ExportTelemetryEvents(StreamWriter writer, ref ulong bytesWritten, ulong maxBytes, ulong reserveBytes)
        {
            var buffer = GetTelemetryEventBuffer();
            if (!buffer.IsCreated || buffer.Length == 0)
            {
                return true;
            }

            var completed = true;
            for (int i = 0; i < buffer.Length; i++)
            {
                var record = buffer[i];
                var eventType = record.EventType.ToString();
                var source = record.Source.ToString();
                var payload = record.Payload.ToString();
                var loopLabel = GetLoopLabel(eventType, payload);
                if (!ShouldWriteLoop(loopLabel))
                {
                    continue;
                }

                var recordTick = record.Tick;
                if (!TryWriteRecord(writer, ref bytesWritten, maxBytes, reserveBytes, recordWriter =>
                    {
                        recordWriter.Write("{\"type\":\"event\",\"runId\":\"");
                        WriteEscapedString(recordWriter, _runIdString);
                        recordWriter.Write("\",\"scenario\":\"");
                        WriteEscapedString(recordWriter, _scenarioIdString);
                        recordWriter.Write("\",\"seed\":");
                        recordWriter.Write(_scenarioSeed);
                        recordWriter.Write(",\"tick\":");
                        recordWriter.Write(recordTick);
                        recordWriter.Write(",\"loop\":\"");
                        WriteEscapedString(recordWriter, loopLabel);
                        recordWriter.Write("\",\"event\":\"");
                        WriteEscapedString(recordWriter, eventType);
                        recordWriter.Write("\",\"source\":\"");
                        WriteEscapedString(recordWriter, source);
                        recordWriter.Write("\",\"payload\":");
                        if (string.IsNullOrEmpty(payload))
                        {
                            recordWriter.Write("null");
                        }
                        else
                        {
                            recordWriter.Write(payload);
                        }
                        recordWriter.WriteLine("}");
                    }))
                {
                    completed = false;
                    break;
                }
            }

            if (completed)
            {
                buffer.Clear();
            }

            return completed;
        }

        private Entity GetTelemetryEventStreamEntity()
        {
            if (SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var reference) && reference.Stream != Entity.Null)
            {
                return reference.Stream;
            }

            return Entity.Null;
        }

        private DynamicBuffer<TelemetryEvent> GetTelemetryEventBuffer()
        {
            var entity = GetTelemetryEventStreamEntity();
            if (entity == Entity.Null || !EntityManager.HasBuffer<TelemetryEvent>(entity))
            {
                return default;
            }

            return EntityManager.GetBuffer<TelemetryEvent>(entity);
        }

        private bool ExportBehaviorTelemetry(StreamWriter writer, ref ulong bytesWritten, ulong maxBytes, ulong reserveBytes)
        {
            if (!SystemAPI.HasSingleton<BehaviorTelemetryState>())
            {
                return true;
            }

            var buffer = SystemAPI.GetSingletonBuffer<BehaviorTelemetryRecord>();

            if (buffer.Length == 0)
            {
                return true;
            }

            var completed = true;
            for (int i = 0; i < buffer.Length; i++)
            {
                var record = buffer[i];
                if (!TryWriteRecord(writer, ref bytesWritten, maxBytes, reserveBytes, recordWriter =>
                    {
                        recordWriter.Write("{\"type\":\"behavior\",\"runId\":\"");
                        WriteEscapedString(recordWriter, _runIdString);
                        recordWriter.Write("\",\"scenario\":\"");
                        WriteEscapedString(recordWriter, _scenarioIdString);
                        recordWriter.Write("\",\"seed\":");
                        recordWriter.Write(_scenarioSeed);
                        recordWriter.Write(",\"tick\":");
                        recordWriter.Write(record.Tick);
                        recordWriter.Write(",\"loop\":\"\"");
                        recordWriter.Write(",\"behaviorId\":");
                        recordWriter.Write((ushort)record.Behavior);
                        recordWriter.Write(",\"behaviorKind\":");
                        recordWriter.Write((byte)record.Kind);
                        recordWriter.Write(",\"metricId\":");
                        recordWriter.Write(record.MetricOrInvariantId);
                        recordWriter.Write(",\"valueA\":");
                        recordWriter.Write(record.ValueA);
                        recordWriter.Write(",\"valueB\":");
                        recordWriter.Write(record.ValueB);
                        recordWriter.Write(",\"passed\":");
                        recordWriter.Write(record.Passed != 0 ? "true" : "false");
                        recordWriter.WriteLine("}");
                    }))
                {
                    completed = false;
                    break;
                }
            }

            if (completed)
            {
                buffer.Clear();
            }

            return completed;
        }

        private bool ExportReplayTelemetry(StreamWriter writer, uint tick, ref ulong bytesWritten, ulong maxBytes, ulong reserveBytes)
        {
            if (!SystemAPI.TryGetSingletonEntity<ReplayCaptureStream>(out var replayEntity))
            {
                return true;
            }

            var stream = SystemAPI.GetComponent<ReplayCaptureStream>(replayEntity);
            if (!TryWriteRecord(writer, ref bytesWritten, maxBytes, reserveBytes, recordWriter =>
                {
                    recordWriter.Write("{\"type\":\"replay\",\"runId\":\"");
                    WriteEscapedString(recordWriter, _runIdString);
                    recordWriter.Write("\",\"scenario\":\"");
                    WriteEscapedString(recordWriter, _scenarioIdString);
                    recordWriter.Write("\",\"seed\":");
                    recordWriter.Write(_scenarioSeed);
                    recordWriter.Write(",\"tick\":");
                    recordWriter.Write(tick);
                    recordWriter.Write(",\"loop\":\"\"");
                    recordWriter.Write(",\"eventCount\":");
                    recordWriter.Write(stream.EventCount);
                    recordWriter.Write(",\"lastEventType\":\"");
                    WriteEscapedString(recordWriter, ReplayCaptureSystem.GetEventTypeLabel(stream.LastEventType).ToString());
                    recordWriter.Write("\",\"lastEventLabel\":\"");
                    WriteEscapedString(recordWriter, stream.LastEventLabel.ToString());
                    recordWriter.WriteLine("\"}");
                }))
            {
                return false;
            }

            if (EntityManager.HasBuffer<ReplayCaptureEvent>(replayEntity))
            {
                var events = EntityManager.GetBuffer<ReplayCaptureEvent>(replayEntity);
                var completed = true;
                for (int i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    if (!TryWriteRecord(writer, ref bytesWritten, maxBytes, reserveBytes, recordWriter =>
                        {
                            recordWriter.Write("{\"type\":\"replayEvent\",\"runId\":\"");
                            WriteEscapedString(recordWriter, _runIdString);
                            recordWriter.Write("\",\"scenario\":\"");
                            WriteEscapedString(recordWriter, _scenarioIdString);
                            recordWriter.Write("\",\"seed\":");
                            recordWriter.Write(_scenarioSeed);
                            recordWriter.Write(",\"tick\":");
                            recordWriter.Write(evt.Tick);
                            recordWriter.Write(",\"loop\":\"\"");
                            recordWriter.Write(",\"eventType\":\"");
                            WriteEscapedString(recordWriter, ReplayCaptureSystem.GetEventTypeLabel(evt.Type).ToString());
                            recordWriter.Write("\",\"label\":\"");
                            WriteEscapedString(recordWriter, evt.Label.ToString());
                            recordWriter.Write("\",\"value\":");
                            recordWriter.Write(evt.Value.ToString("R", CultureInfo.InvariantCulture));
                            recordWriter.WriteLine("}");
                        }))
                    {
                        completed = false;
                        break;
                    }
                }

                if (completed)
                {
                    events.Clear();
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private void WriteRunHeader(TextWriter writer, in TelemetryExportConfig config)
        {
            writer.Write("{\"type\":\"run\",\"runId\":\"");
            WriteEscapedString(writer, _runIdString);
            writer.Write("\",\"timestamp\":\"");
            writer.Write(DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            writer.Write("\",\"world\":\"");
            WriteEscapedString(writer, World.Name);
            writer.Write("\",\"flags\":");
            writer.Write((int)config.Flags);
            writer.Write(",\"application\":\"");
            WriteEscapedString(writer, Application.productName);
            writer.Write("\",\"unityVersion\":\"");
            WriteEscapedString(writer, Application.unityVersion);
            writer.Write("\"");

            var envOracleProbe = SystemEnvironment.GetEnvironmentVariable("PUREDOTS_TELEMETRY_ORACLE_PROBE") ?? string.Empty;
            var envTelemetryLevel = SystemEnvironment.GetEnvironmentVariable("PUREDOTS_TELEMETRY_LEVEL") ?? string.Empty;
            var envTelemetryFlags = SystemEnvironment.GetEnvironmentVariable("PUREDOTS_TELEMETRY_FLAGS") ?? string.Empty;
            var hasScenarioRunnerTick = SystemAPI.HasSingleton<ScenarioRunnerTick>();
            var hasTickTimeState = SystemAPI.HasSingleton<TickTimeState>();
            var hasTimeState = SystemAPI.HasSingleton<TimeState>();

            writer.Write(",\"probeVersion\":\"");
            WriteEscapedString(writer, OracleProbeVersion);
            writer.Write("\",\"envOracleProbe\":\"");
            WriteEscapedString(writer, envOracleProbe);
            writer.Write("\",\"envTelemetryLevel\":\"");
            WriteEscapedString(writer, envTelemetryLevel);
            writer.Write("\",\"envTelemetryFlags\":\"");
            WriteEscapedString(writer, envTelemetryFlags);
            writer.Write("\",\"cadenceTicks\":");
            writer.Write(config.CadenceTicks);
            writer.Write(",\"hasScenarioRunnerTick\":");
            writer.Write(hasScenarioRunnerTick ? "true" : "false");
            writer.Write(",\"hasTickTimeState\":");
            writer.Write(hasTickTimeState ? "true" : "false");
            writer.Write(",\"hasTimeState\":");
            writer.Write(hasTimeState ? "true" : "false");

            writer.Write(",\"scenarioId\":\"");
            WriteEscapedString(writer, _scenarioIdString);
            writer.Write("\",\"seed\":");
            writer.Write(_scenarioSeed);

            if (SystemAPI.TryGetSingleton<ScenarioState>(out var scenario))
            {
                writer.Write(",\"scenarioKind\":");
                writer.Write((byte)scenario.Current);
                writer.Write(",\"scenarioKindLabel\":\"");
                WriteEscapedString(writer, scenario.Current.ToString());
                writer.Write("\",\"bootPhase\":");
                writer.Write((byte)scenario.BootPhase);
                writer.Write(",\"isInitialized\":");
                writer.Write(scenario.IsInitialized ? "true" : "false");
                writer.Write(",\"enableGodgame\":");
                writer.Write(scenario.EnableGodgame ? "true" : "false");
                writer.Write(",\"enableSpace4x\":");
                writer.Write(scenario.EnableSpace4x ? "true" : "false");
                writer.Write(",\"enableEconomy\":");
                writer.Write(scenario.EnableEconomy ? "true" : "false");
            }

            writer.WriteLine("}");
        }

        private void ResolveScenarioMetadata()
        {
            _scenarioSeed = 0;
            _scenarioIdString = string.Empty;
            if (SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                _scenarioSeed = scenarioInfo.Seed;
                _scenarioIdString = scenarioInfo.ScenarioId.ToString();
            }
        }

        private string GetLoopLabel(string metricKey)
        {
            if (string.IsNullOrEmpty(metricKey))
            {
                return string.Empty;
            }

            if (metricKey.StartsWith("loop.", StringComparison.OrdinalIgnoreCase))
            {
                var slice = metricKey.Substring(5);
                var dotIndex = slice.IndexOf('.');
                return dotIndex > 0 ? slice.Substring(0, dotIndex) : slice;
            }

            return string.Empty;
        }

        private string GetLoopLabel(string eventType, string payload)
        {
            if (string.IsNullOrEmpty(eventType))
            {
                return string.Empty;
            }

            if (eventType.StartsWith("loop_", StringComparison.OrdinalIgnoreCase))
            {
                var extracted = ExtractLoopFromPayload(payload);
                if (!string.IsNullOrEmpty(extracted))
                {
                    return extracted;
                }
            }

            return string.Empty;
        }

        private static string ExtractLoopFromPayload(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return string.Empty;
            }

            const string marker = "\"l\":\"";
            var index = payload.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                return string.Empty;
            }

            index += marker.Length;
            var end = payload.IndexOf('\"', index);
            if (end <= index)
            {
                return string.Empty;
            }

            return payload.Substring(index, end - index);
        }

        private bool ShouldWriteLoop(string loopLabel)
        {
            if (string.IsNullOrEmpty(loopLabel))
            {
                return true;
            }

            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config))
            {
                return true;
            }

            var loopFlag = MapLoopFlag(loopLabel);
            if (loopFlag == TelemetryLoopFlags.None)
            {
                return true;
            }

            return (config.Loops & loopFlag) != 0;
        }

        private static TelemetryLoopFlags MapLoopFlag(string loopLabel)
        {
            switch (loopLabel.Trim().ToLowerInvariant())
            {
                case "extract":
                    return TelemetryLoopFlags.Extract;
                case "logistics":
                    return TelemetryLoopFlags.Logistics;
                case "construction":
                    return TelemetryLoopFlags.Construction;
                case "exploration":
                    return TelemetryLoopFlags.Exploration;
                case "combat":
                    return TelemetryLoopFlags.Combat;
                default:
                    return TelemetryLoopFlags.None;
            }
        }

        private static void WriteEscapedString(TextWriter writer, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            for (int i = 0; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\\':
                        writer.Write("\\\\");
                        break;
                    case '\"':
                        writer.Write("\\\"");
                        break;
                    case '\n':
                        writer.Write("\\n");
                        break;
                    case '\r':
                        writer.Write("\\r");
                        break;
                    case '\t':
                        writer.Write("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            writer.Write($"\\u{(int)c:X4}");
                        }
                        else
                        {
                            writer.Write(c);
                        }
                        break;
                }
            }
        }

        private static string GetUnitLabel(TelemetryMetricUnit unit)
        {
            return unit switch
            {
                TelemetryMetricUnit.Count => "count",
                TelemetryMetricUnit.Ratio => "ratio",
                TelemetryMetricUnit.DurationMilliseconds => "ms",
                TelemetryMetricUnit.Bytes => "bytes",
                TelemetryMetricUnit.None => "none",
                TelemetryMetricUnit.Custom => "custom",
                _ => "unknown"
            };
        }
    }
}
