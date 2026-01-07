using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Space4x.Scenario;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    internal static class Space4XHeadlessDiagnostics
    {
        public const int TestFailExitCode = 10;
        private const int ProgressQueueLimit = 32;
        private const int DefaultFlushTimeoutMs = 750;
        private const string OutDirArg = "--outDir";
        private const string ScenarioArg = "--scenario";
        private const string SeedArg = "--seed";
        private const string InvariantsPathArg = "--invariantsPath";
        private const string ProgressPathArg = "--progressPath";
        private const string TelemetryPathArg = "--telemetryPath";
        private const string TelemetryEnabledArg = "--telemetryEnabled";
        private const string TelemetryPathEnv = "PUREDOTS_TELEMETRY_PATH";
        private const string TelemetryEnableEnv = "PUREDOTS_TELEMETRY_ENABLE";
        private const string TelemetryMaxBytesEnv = "PUREDOTS_TELEMETRY_MAX_BYTES";
        private const ulong DefaultTelemetryMaxBytes = 25 * 1024 * 1024;

        private static readonly string[] BuildIdEnvKeys = { "TRI_BUILD_ID", "BUILD_ID", "HEADLESS_BUILD_ID" };
        private static readonly string[] CommitEnvKeys = { "TRI_BUILD_COMMIT", "BUILD_COMMIT", "GIT_COMMIT", "GIT_SHA" };
        private static readonly string[] MetricsExcludedFromHash = { "entity_count_peak" };

        private static bool s_initialized;
        private static readonly List<InvariantRecord> s_invariants = new();
        private static string s_lastPhase = string.Empty;
        private static string s_lastCheckpoint = string.Empty;
        private static uint s_lastProgressTick;
        private static uint s_simTicks;
        private static uint s_expectedSimTicks;
        private static int s_entityCountPeak;
        private static long s_allocBytesPeak;
        private static float s_avgDtMs;
        private static readonly ConcurrentQueue<WriteRequest> s_writeQueue = new();
        private static readonly AutoResetEvent s_writeSignal = new(false);
        private static Thread s_writerThread;
        private static int s_writerStarted;
        private static volatile bool s_stopWriter;
        private static int s_queueDepth;
        private static int s_droppedWrites;

        public static bool Enabled { get; private set; }
        public static string OutDir { get; private set; } = string.Empty;
        public static string InvariantsPath { get; private set; } = string.Empty;
        public static string ProgressPath { get; private set; } = string.Empty;
        public static string TelemetryPath { get; private set; } = string.Empty;
        public static bool TelemetryEnabled { get; private set; } = true;
        public static string ScenarioIdOverride { get; private set; } = string.Empty;
        public static uint SeedOverride { get; private set; }
        public static string BuildId { get; private set; } = string.Empty;
        public static string Commit { get; private set; } = string.Empty;
        public static bool HasInvariantFailures => s_invariants.Count > 0;

        public static void InitializeFromArgs()
        {
            if (s_initialized)
            {
                return;
            }

            s_initialized = true;
            RuntimeMode.RefreshFromEnvironment();
            if (!Application.isBatchMode || !RuntimeMode.IsHeadless)
            {
                Enabled = false;
                return;
            }

            var args = SystemEnv.GetCommandLineArgs();
            if (!TryGetArg(args, OutDirArg, out var outDirValue) || string.IsNullOrWhiteSpace(outDirValue))
            {
                UnityDebug.LogWarning("[Space4XHeadlessDiagnostics] --outDir missing; diagnostics disabled.");
                Enabled = false;
                return;
            }

            outDirValue = Path.GetFullPath(outDirValue.Trim('"'));
            OutDir = outDirValue;
            Directory.CreateDirectory(outDirValue);

            if (TryGetArg(args, ScenarioArg, out var scenarioArg) && !string.IsNullOrWhiteSpace(scenarioArg))
            {
                ScenarioIdOverride = ExtractScenarioId(scenarioArg);
            }

            if (TryGetArg(args, SeedArg, out var seedArg) &&
                uint.TryParse(seedArg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
            {
                SeedOverride = seed;
            }

            TelemetryEnabled = true;
            if (TryGetArg(args, TelemetryEnabledArg, out var telemetryEnabledArg) &&
                !string.IsNullOrWhiteSpace(telemetryEnabledArg))
            {
                TelemetryEnabled = !string.Equals(telemetryEnabledArg.Trim(), "0", StringComparison.OrdinalIgnoreCase);
            }

            InvariantsPath = ResolvePathWithinOutDir(outDirValue,
                TryGetArg(args, InvariantsPathArg, out var invariantsArg) ? invariantsArg : null,
                "invariants.json");
            ProgressPath = ResolvePathWithinOutDir(outDirValue,
                TryGetArg(args, ProgressPathArg, out var progressArg) ? progressArg : null,
                "progress.json");
            TelemetryPath = ResolvePathWithinOutDir(outDirValue,
                TryGetArg(args, TelemetryPathArg, out var telemetryArg) ? telemetryArg : null,
                "telemetry.ndjson");

            BuildId = ResolveEnv(BuildIdEnvKeys);
            Commit = ResolveEnv(CommitEnvKeys);

            SystemEnv.SetEnvironmentVariable(TelemetryPathEnv, TelemetryPath);
            SystemEnv.SetEnvironmentVariable(TelemetryEnableEnv, TelemetryEnabled ? "1" : "0");
            if (string.IsNullOrWhiteSpace(SystemEnv.GetEnvironmentVariable(TelemetryMaxBytesEnv)))
            {
                SystemEnv.SetEnvironmentVariable(TelemetryMaxBytesEnv, DefaultTelemetryMaxBytes.ToString(CultureInfo.InvariantCulture));
            }

            Enabled = true;
        }

        public static void RecordMetrics(uint tick, float fixedDeltaTime, EntityManager entityManager, ref uint lastSampleTick, uint sampleInterval)
        {
            if (!Enabled)
            {
                return;
            }

            s_simTicks = tick;
            TryCaptureExpectedSimTicks(entityManager);
            if (fixedDeltaTime > 0f)
            {
                s_avgDtMs = fixedDeltaTime * 1000f;
            }

            if (sampleInterval == 0 || tick - lastSampleTick >= sampleInterval)
            {
                lastSampleTick = tick;
                try
                {
                    var count = entityManager.UniversalQuery.CalculateEntityCount();
                    if (count > s_entityCountPeak)
                    {
                        s_entityCountPeak = count;
                    }
                }
                catch
                {
                }
            }
        }

        public static void UpdateProgress(string phase, string checkpoint, uint tick)
        {
            if (!Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(phase))
            {
                phase = "unknown";
            }

            if (string.IsNullOrWhiteSpace(checkpoint))
            {
                checkpoint = "unknown";
            }

            if (phase == s_lastPhase && checkpoint == s_lastCheckpoint && tick == s_lastProgressTick)
            {
                return;
            }

            s_lastPhase = phase;
            s_lastCheckpoint = checkpoint;
            s_lastProgressTick = tick;
            EnqueueWrite(ProgressPath, BuildProgressJson(phase, checkpoint, tick));
        }

        public static void ReportInvariant(string id, string message, string observed = "", string expected = "", string contextJson = "")
        {
            if (!Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                id = "INV-UNKNOWN";
            }

            s_invariants.Add(new InvariantRecord
            {
                Id = id,
                Status = "FAIL",
                Message = message ?? string.Empty,
                Observed = observed ?? string.Empty,
                Expected = expected ?? string.Empty,
                ContextJson = string.IsNullOrWhiteSpace(contextJson) ? "{}" : contextJson
            });
        }

        public static void WriteInvariantsForExit(EntityManager entityManager, int exitCode, uint fallbackTick)
        {
            if (!Enabled)
            {
                return;
            }

            var scenarioId = ScenarioIdOverride;
            var seed = SeedOverride;
            if (TryGetScenarioInfo(entityManager, out var infoScenario, out var infoSeed))
            {
                scenarioId = infoScenario;
                seed = infoSeed;
            }

            var simTicks = s_simTicks != 0 ? s_simTicks : fallbackTick;
            var expectedSimTicks = ResolveExpectedSimTicks(entityManager, simTicks);
            var invariants = new List<InvariantRecord>(s_invariants);
            if (exitCode != 0 && invariants.Count == 0)
            {
                invariants.Add(new InvariantRecord
                {
                    Id = "INV-HEADLESS-EXIT",
                    Status = "FAIL",
                    Message = "Headless exit requested with non-zero code.",
                    Observed = $"exit_code={exitCode}",
                    Expected = "exit_code=0",
                    ContextJson = "{}"
                });
            }

            var json = BuildInvariantsJson(invariants,
                scenarioId,
                seed,
                simTicks,
                expectedSimTicks,
                s_entityCountPeak,
                s_allocBytesPeak,
                s_avgDtMs,
                s_lastPhase,
                s_lastCheckpoint,
                s_lastProgressTick);
            TryWriteInvariantFile(json);
        }

        public static void WriteScenarioRunnerInvariants(in ScenarioRunResult result, int exitCode)
        {
            if (!Enabled)
            {
                return;
            }

            var scenarioId = string.IsNullOrWhiteSpace(result.ScenarioId) ? ScenarioIdOverride : result.ScenarioId;
            var seed = result.Seed != 0 ? result.Seed : SeedOverride;
            var simTicks = result.RunTicks > 0 ? (uint)result.RunTicks : s_simTicks;
            var expectedSimTicks = s_expectedSimTicks != 0 ? s_expectedSimTicks : simTicks;
            var avgDtMs = s_avgDtMs > 0f ? s_avgDtMs : 1000f / 60f;
            var invariants = new List<InvariantRecord>();

            if (result.Issues != null)
            {
                for (int i = 0; i < result.Issues.Count; i++)
                {
                    var issue = result.Issues[i];
                    if (issue.Severity < ScenarioSeverity.Error)
                    {
                        continue;
                    }

                    invariants.Add(new InvariantRecord
                    {
                        Id = issue.Code.ToString(),
                        Status = "FAIL",
                        Message = issue.Message.ToString(),
                        Observed = string.Empty,
                        Expected = string.Empty,
                        ContextJson = $"{{\"kind\":\"{issue.Kind}\",\"severity\":\"{issue.Severity}\"}}"
                    });
                }
            }

            if (result.AssertionResults != null)
            {
                for (int i = 0; i < result.AssertionResults.Count; i++)
                {
                    var assertion = result.AssertionResults[i];
                    if (assertion.Passed)
                    {
                        continue;
                    }

                    invariants.Add(new InvariantRecord
                    {
                        Id = $"ASSERT-{assertion.MetricId}",
                        Status = "FAIL",
                        Message = assertion.FailureMessage ?? "Scenario assertion failed.",
                        Observed = assertion.ActualValue.ToString("0.###", CultureInfo.InvariantCulture),
                        Expected = assertion.ExpectedValue.ToString("0.###", CultureInfo.InvariantCulture),
                        ContextJson = $"{{\"operator\":\"{assertion.Operator}\"}}"
                    });
                }
            }

            if (exitCode != 0 && invariants.Count == 0)
            {
                invariants.Add(new InvariantRecord
                {
                    Id = "INV-HEADLESS-EXIT",
                    Status = "FAIL",
                    Message = "ScenarioRunner exit requested with non-zero code.",
                    Observed = $"exit_code={exitCode}",
                    Expected = "exit_code=0",
                    ContextJson = "{}"
                });
            }

            var json = BuildInvariantsJson(invariants,
                scenarioId,
                seed,
                simTicks,
                expectedSimTicks,
                0,
                0,
                avgDtMs,
                s_lastPhase,
                s_lastCheckpoint,
                s_lastProgressTick);
            TryWriteInvariantFile(json);
        }

        public static void ShutdownWriter(int timeoutMs = DefaultFlushTimeoutMs)
        {
            if (Interlocked.CompareExchange(ref s_writerStarted, 0, 0) == 0)
            {
                return;
            }

            s_stopWriter = true;
            s_writeSignal.Set();
            if (s_writerThread == null)
            {
                return;
            }

            if (!s_writerThread.Join(timeoutMs))
            {
                UnityDebug.LogWarning("[Space4XHeadlessDiagnostics] Progress writer flush exceeded timeout; abandoning remaining writes.");
            }
        }

        private static void EnsureWriter()
        {
            if (Interlocked.Exchange(ref s_writerStarted, 1) != 0)
            {
                return;
            }

            s_stopWriter = false;
            s_writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "Space4XHeadlessDiagWriter"
            };
            s_writerThread.Start();
        }

        private static void EnqueueWrite(string path, string payload)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            EnsureWriter();
            var depth = Interlocked.Increment(ref s_queueDepth);
            if (depth > ProgressQueueLimit)
            {
                Interlocked.Decrement(ref s_queueDepth);
                if (Interlocked.Increment(ref s_droppedWrites) == 1)
                {
                    UnityDebug.LogWarning("[Space4XHeadlessDiagnostics] Progress writer queue full; dropping updates.");
                }
                return;
            }

            s_writeQueue.Enqueue(new WriteRequest
            {
                Path = path,
                Payload = payload
            });
            s_writeSignal.Set();
        }

        private static void WriterLoop()
        {
            while (!s_stopWriter)
            {
                DrainQueue();
                s_writeSignal.WaitOne(50);
            }

            DrainQueue();
        }

        private static void DrainQueue()
        {
            while (s_writeQueue.TryDequeue(out var request))
            {
                Interlocked.Decrement(ref s_queueDepth);
                TryWriteAtomic(request.Path, request.Payload);
            }
        }

        private static void TryWriteInvariantFile(string json)
        {
            if (string.IsNullOrWhiteSpace(InvariantsPath))
            {
                return;
            }

            TryWriteAtomic(InvariantsPath, json);
        }

        private static void TryWriteAtomic(string path, string payload)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, payload);
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tempPath, path, null);
                        return;
                    }
                    catch
                    {
                    }

                    File.Delete(path);
                }

                File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                UnityDebug.LogWarning($"[Space4XHeadlessDiagnostics] Failed to write diagnostics file '{path}': {ex.Message}");
            }
        }

        private static bool TryGetScenarioInfo(EntityManager entityManager, out string scenarioId, out uint seed)
        {
            scenarioId = string.Empty;
            seed = 0;
            try
            {
                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioInfo>());
                if (query.IsEmptyIgnoreFilter)
                {
                    return false;
                }

                var info = query.GetSingleton<ScenarioInfo>();
                scenarioId = info.ScenarioId.ToString();
                seed = info.Seed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildProgressJson(string phase, string checkpoint, uint tick)
        {
            var sb = new StringBuilder(96);
            sb.Append('{');
            AppendString(sb, "phase", phase);
            sb.Append(',');
            AppendString(sb, "checkpoint", checkpoint);
            sb.Append(',');
            AppendUInt(sb, "tick", tick);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildInvariantsJson(
            List<InvariantRecord> invariants,
            string scenarioId,
            uint seed,
            uint simTicks,
            uint expectedSimTicks,
            int entityCountPeak,
            long allocBytesPeak,
            float avgDtMs,
            string lastPhase,
            string lastCheckpoint,
            uint lastTick)
        {
            invariants ??= new List<InvariantRecord>();
            invariants.Sort(InvariantRecordComparer.Instance);

            var buildId = string.IsNullOrWhiteSpace(BuildId) ? "unknown" : BuildId;
            var commit = string.IsNullOrWhiteSpace(Commit) ? "unknown" : Commit;
            var scenario = string.IsNullOrWhiteSpace(scenarioId) ? "unknown" : scenarioId;
            var determinismHash = ComputeDeterminismHash(scenario, seed, expectedSimTicks,
                allocBytesPeak, avgDtMs, invariants);

            var sb = new StringBuilder(512);
            sb.Append('{');
            AppendInt(sb, "diagnostics_version", 1, prependComma: false); sb.Append(',');
            AppendString(sb, "build_id", buildId); sb.Append(',');
            AppendString(sb, "commit", commit); sb.Append(',');
            AppendString(sb, "scenario_id", scenario); sb.Append(',');
            AppendUInt(sb, "seed", seed); sb.Append(',');
            AppendUInt(sb, "sim_ticks", simTicks); sb.Append(',');
            AppendUInt(sb, "expected_sim_ticks", expectedSimTicks); sb.Append(',');

            sb.Append("\"metrics\":{");
            AppendLong(sb, "alloc_bytes_peak", allocBytesPeak, prependComma: false); sb.Append(',');
            AppendFloat(sb, "avg_dt_ms", avgDtMs, prependComma: false); sb.Append(',');
            AppendInt(sb, "entity_count_peak", entityCountPeak, prependComma: false);
            sb.Append("}");
            if (MetricsExcludedFromHash.Length > 0)
            {
                sb.Append(',');
                AppendStringArray(sb, "metrics_excluded_from_hash", MetricsExcludedFromHash);
            }
            sb.Append(',');

            sb.Append("\"invariants\":[");
            for (int i = 0; i < invariants.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var inv = invariants[i];
                sb.Append('{');
                AppendString(sb, "id", inv.Id); sb.Append(',');
                AppendString(sb, "status", inv.Status); sb.Append(',');
                AppendString(sb, "message", inv.Message); sb.Append(',');
                AppendString(sb, "observed", inv.Observed); sb.Append(',');
                AppendString(sb, "expected", inv.Expected); sb.Append(',');
                sb.Append("\"context\":");
                sb.Append(string.IsNullOrWhiteSpace(inv.ContextJson) ? "{}" : inv.ContextJson);
                sb.Append('}');
            }
            sb.Append("],");

            sb.Append("\"progress\":{");
            AppendString(sb, "last_phase", lastPhase ?? string.Empty); sb.Append(',');
            AppendString(sb, "last_checkpoint", lastCheckpoint ?? string.Empty); sb.Append(',');
            AppendUInt(sb, "last_tick", lastTick);
            sb.Append("},");

            AppendString(sb, "determinism_hash", determinismHash);
            sb.Append('}');
            return sb.ToString();
        }

        private static string ComputeDeterminismHash(
            string scenarioId,
            uint seed,
            uint expectedSimTicks,
            long allocBytesPeak,
            float avgDtMs,
            List<InvariantRecord> invariants)
        {
            var sb = new StringBuilder(256);
            sb.Append("scenario_id=").Append(scenarioId).Append('\n');
            sb.Append("seed=").Append(seed).Append('\n');
            sb.Append("sim_ticks=").Append(expectedSimTicks).Append('\n');
            sb.Append("alloc_bytes_peak=").Append(allocBytesPeak).Append('\n');
            sb.Append("avg_dt_ms=").Append(FormatFloat(avgDtMs)).Append('\n');
            for (int i = 0; i < invariants.Count; i++)
            {
                var inv = invariants[i];
                var context = string.IsNullOrWhiteSpace(inv.ContextJson) ? "{}" : inv.ContextJson;
                sb.Append("inv=").Append(inv.Id)
                    .Append('|').Append(inv.Status)
                    .Append('|').Append(inv.Observed)
                    .Append('|').Append(inv.Expected)
                    .Append('|').Append(context)
                    .Append('\n');
            }

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hash = sha.ComputeHash(bytes);
            var hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            return hex.ToString();
        }

        private static string ResolveEnv(string[] keys)
        {
            foreach (var key in keys)
            {
                var value = SystemEnv.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string ExtractScenarioId(string scenarioArg)
        {
            var trimmed = scenarioArg.Trim('"');
            var fileName = Path.GetFileNameWithoutExtension(trimmed);
            return string.IsNullOrWhiteSpace(fileName) ? trimmed : fileName;
        }

        private static void TryCaptureExpectedSimTicks(EntityManager entityManager)
        {
            if (s_expectedSimTicks != 0)
            {
                return;
            }

            if (TryGetExpectedSimTicks(entityManager, out var expectedSimTicks))
            {
                s_expectedSimTicks = expectedSimTicks;
            }
        }

        private static uint ResolveExpectedSimTicks(EntityManager entityManager, uint observedSimTicks)
        {
            TryCaptureExpectedSimTicks(entityManager);
            return s_expectedSimTicks != 0 ? s_expectedSimTicks : observedSimTicks;
        }

        private static bool TryGetExpectedSimTicks(EntityManager entityManager, out uint expectedSimTicks)
        {
            expectedSimTicks = 0;
            try
            {
                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XScenarioRuntime>());
                if (query.IsEmptyIgnoreFilter)
                {
                    return false;
                }

                var runtime = query.GetSingleton<Space4XScenarioRuntime>();
                if (runtime.EndTick <= runtime.StartTick)
                {
                    return false;
                }

                expectedSimTicks = runtime.EndTick - runtime.StartTick;
                return expectedSimTicks > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolvePathWithinOutDir(string outDir, string candidate, string defaultFileName)
        {
            if (string.IsNullOrWhiteSpace(outDir))
            {
                return string.Empty;
            }

            var root = Path.GetFullPath(outDir);
            var value = string.IsNullOrWhiteSpace(candidate) ? Path.Combine(root, defaultFileName) : candidate.Trim('"');
            if (!Path.IsPathRooted(value))
            {
                value = Path.Combine(root, value);
            }

            value = Path.GetFullPath(value);
            if (!IsUnderRoot(root, value))
            {
                value = Path.Combine(root, defaultFileName);
            }

            return value;
        }

        private static bool IsUnderRoot(string root, string path)
        {
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                 + Path.DirectorySeparatorChar;
            var comparison = Application.platform == RuntimePlatform.WindowsPlayer ||
                             Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return path.StartsWith(normalizedRoot, comparison);
        }

        private static bool TryGetArg(string[] args, string key, out string value)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.Equals(arg, key, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        value = args[i + 1];
                        return true;
                    }
                    break;
                }

                var prefix = key + "=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(prefix.Length).Trim('"');
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":\"");
            sb.Append(Escape(value));
            sb.Append('"');
        }

        private static void AppendStringArray(StringBuilder sb, string key, string[] values, bool prependComma = false)
        {
            if (prependComma)
            {
                sb.Append(',');
            }
            sb.Append('"').Append(key).Append("\":[");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append('"').Append(Escape(values[i] ?? string.Empty)).Append('"');
            }
            sb.Append(']');
        }

        private static void AppendUInt(StringBuilder sb, string key, uint value)
        {
            sb.Append('"').Append(key).Append("\":").Append(value);
        }

        private static void AppendInt(StringBuilder sb, string key, int value, bool prependComma = false)
        {
            if (prependComma)
            {
                sb.Append(',');
            }
            sb.Append('"').Append(key).Append("\":").Append(value);
        }

        private static void AppendLong(StringBuilder sb, string key, long value, bool prependComma = false)
        {
            if (prependComma)
            {
                sb.Append(',');
            }
            sb.Append('"').Append(key).Append("\":").Append(value);
        }

        private static void AppendFloat(StringBuilder sb, string key, float value, bool prependComma = false)
        {
            if (prependComma)
            {
                sb.Append(',');
            }
            sb.Append('"').Append(key).Append("\":").Append(FormatFloat(value));
        }

        private static string FormatFloat(float value)
        {
            if (float.IsNaN(value))
            {
                return "\"NaN\"";
            }

            if (float.IsInfinity(value))
            {
                return value > 0f ? "\"Inf\"" : "\"-Inf\"";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private struct WriteRequest
        {
            public string Path;
            public string Payload;
        }

        private struct InvariantRecord
        {
            public string Id;
            public string Status;
            public string Message;
            public string Observed;
            public string Expected;
            public string ContextJson;
        }

        private sealed class InvariantRecordComparer : IComparer<InvariantRecord>
        {
            public static readonly InvariantRecordComparer Instance = new();

            public int Compare(InvariantRecord x, InvariantRecord y)
            {
                return string.CompareOrdinal(x.Id, y.Id);
            }
        }
    }
}
