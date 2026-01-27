using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = UnityEngine.Hash128;

namespace PureDOTS.Runtime.Scenarios
{
    internal static class ScenarioRunRecorder
    {
        private const string LogPathEnvVar = "PUREDOTS_SCENARIO_RUN_LOG";
        private const string BaselineEnvVar = "PUREDOTS_SCENARIO_BASELINE";
        private const string DigestIntervalEnvVar = "PUREDOTS_SCENARIO_DIGEST_INTERVAL";

        private static bool s_enabled;
        private static bool s_headerWritten;
        private static bool s_summaryWritten;
        private static string s_logPath;
        private static string s_baselinePath;
        private static int s_digestInterval = 1;
        private static uint s_lastDigestTick;
        private static StreamWriter s_writer;
        private static RunHeaderRecord s_header;
        private static GitMetadataUtility.GitMetadata s_gitMetadata;

        public static void Initialize(in ResolvedScenario scenario, string sourceLabel, float fixedDeltaTime)
        {
            s_enabled = TryResolveLogPath(out s_logPath);
            if (!s_enabled)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(s_logPath) ?? ".");
            s_writer = new StreamWriter(File.Open(s_logPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = false,
                NewLine = "\n"
            };

            s_baselinePath = global::System.Environment.GetEnvironmentVariable(BaselineEnvVar);
            if (int.TryParse(global::System.Environment.GetEnvironmentVariable(DigestIntervalEnvVar), out var interval) && interval > 0)
            {
                s_digestInterval = interval;
            }

            GitMetadataUtility.TryReadMetadata(out s_gitMetadata);

            s_header = new RunHeaderRecord
            {
                type = "run",
                scenarioId = scenario.ScenarioId.ToString(),
                seed = scenario.Seed,
                runTicks = scenario.RunTicks,
                fixedDeltaTime = fixedDeltaTime,
                source = sourceLabel ?? "inline",
                gitCommit = string.IsNullOrEmpty(s_gitMetadata.Commit) ? "unknown" : s_gitMetadata.Commit,
                gitBranch = string.IsNullOrEmpty(s_gitMetadata.Branch) ? "unknown" : s_gitMetadata.Branch,
                gitDirty = s_gitMetadata.IsDirty,
                buildConfig = GetBuildConfig(),
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        public static void TryWriteRunHeader(EntityManager entityManager)
        {
            if (!s_enabled || s_writer == null || s_headerWritten)
            {
                return;
            }

            s_header.registryAggregateHash = TryGetRegistryAggregate(entityManager, out var catalogs);
            s_header.catalogs = catalogs;

            WriteJson(s_header);
            s_writer.Flush();
            s_headerWritten = true;
        }

        public static void RecordDigest(EntityManager entityManager)
        {
            if (!s_enabled || s_writer == null || !s_headerWritten)
            {
                return;
            }

            if (!TryGetSingleton(entityManager, out TimeState timeState))
            {
                return;
            }

            if (timeState.Tick == s_lastDigestTick)
            {
                return;
            }

            if (timeState.Tick % (uint)s_digestInterval != 0)
            {
                return;
            }

            var digest = new DigestRecord
            {
                type = "digest",
                tick = timeState.Tick,
                entityCount = (uint)entityManager.UniversalQuery.CalculateEntityCount(),
                timeHash = HashTimeState(timeState)
            };

            if (TryGetSingleton(entityManager, out ScenarioState scenarioState))
            {
                digest.scenarioHash = HashScenarioState(scenarioState);
            }

            if (TryGetSingleton(entityManager, out RegistryDirectory directory))
            {
                digest.registryHash = directory.AggregateHash;
            }

            if (TryGetSingleton(entityManager, out RewindState rewindState))
            {
                digest.rewindHash = HashRewindState(rewindState);
            }

            if (TryGetSingleton(entityManager, out TickTimeState tickTimeState))
            {
                digest.tickTimeHash = HashTickTimeState(tickTimeState);
            }

            if (TryGetSingleton(entityManager, out InputCommandLogState commandLog))
            {
                digest.commandLogCount = (uint)commandLog.Count;
            }

            if (TryGetSingleton(entityManager, out TickSnapshotLogState snapshotLog))
            {
                digest.snapshotLogCount = (uint)snapshotLog.Count;
            }

            if (TryGetSingleton(entityManager, out ScenarioConfig scenarioConfig))
            {
                digest.randomHash = HashScenarioConfig(scenarioConfig);
            }

            digest.randomHash = HashStep(digest.randomHash, (uint)UnityEngine.Random.state.GetHashCode());

            digest.hash = ComputeDigestHash(digest, timeState);

            WriteJson(digest);
            s_lastDigestTick = timeState.Tick;
        }

        public static void CompleteRun(in ScenarioRunResult result)
        {
            if (!s_enabled || s_writer == null)
            {
                return;
            }

            if (!s_summaryWritten)
            {
                var summary = new RunSummaryRecord
                {
                    type = "summary",
                    scenarioId = result.ScenarioId,
                    seed = result.Seed,
                    finalTick = result.FinalTick,
                    runTicks = result.RunTicks,
                    commandLogCount = result.CommandLogCount,
                    snapshotLogCount = result.SnapshotLogCount,
                    frameBudgetExceeded = result.FrameTimingBudgetExceeded,
                    worstFrameMs = result.FrameTimingWorstMs,
                    worstFrameGroup = result.FrameTimingWorstGroup,
                    registryContinuityFailures = result.RegistryContinuityFailures,
                    registryContinuityWarnings = result.RegistryContinuityWarnings,
                    logBytes = result.TotalLogBytes,
                    perfBudgetFailed = result.PerformanceBudgetFailed,
                    perfBudgetMetric = result.PerformanceBudgetMetric,
                    perfBudgetValue = result.PerformanceBudgetValue,
                    perfBudgetLimit = result.PerformanceBudgetLimit,
                    perfBudgetTick = result.PerformanceBudgetTick,
                    metrics = ConvertMetrics(result.Metrics)
                };

                WriteJson(summary);
                s_writer.Flush();
                s_summaryWritten = true;
            }

            s_writer.Dispose();
            s_writer = null;

            if (!string.IsNullOrEmpty(s_baselinePath))
            {
                CompareAgainstBaseline();
            }

            s_enabled = false;
            s_headerWritten = false;
            s_summaryWritten = false;
            s_lastDigestTick = 0;
            s_digestInterval = 1;
        }

        private static bool TryResolveLogPath(out string resolvedPath)
        {
            resolvedPath = global::System.Environment.GetEnvironmentVariable(LogPathEnvVar);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return false;
            }

            resolvedPath = resolvedPath.Trim();
            if (!Path.IsPathRooted(resolvedPath))
            {
                resolvedPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), resolvedPath));
            }

            return true;
        }

        private static CatalogRecord[] ConvertCatalogs(NativeArray<RegistryMetadata> metadata)
        {
            if (metadata.Length == 0)
            {
                return Array.Empty<CatalogRecord>();
            }

            var result = new CatalogRecord[metadata.Length];
            for (var i = 0; i < metadata.Length; i++)
            {
                result[i] = new CatalogRecord
                {
                    label = metadata[i].Label.ToString(),
                    kind = metadata[i].Kind.ToString(),
                    version = metadata[i].Version,
                    entryCount = metadata[i].EntryCount,
                    spatialVersion = metadata[i].Continuity.SpatialVersion
                };
            }

            return result;
        }

        private static uint TryGetRegistryAggregate(EntityManager entityManager, out CatalogRecord[] catalogs)
        {
            catalogs = Array.Empty<CatalogRecord>();
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryMetadata>());
            if (query.IsEmptyIgnoreFilter)
            {
                return 0u;
            }

            var metadata = query.ToComponentDataArray<RegistryMetadata>(Allocator.Temp);
            try
            {
                catalogs = ConvertCatalogs(metadata);
            }
            finally
            {
                metadata.Dispose();
            }

            if (TryGetSingleton(entityManager, out RegistryDirectory directory))
            {
                return directory.AggregateHash;
            }

            return 0u;
        }

        private static List<MetricRecord> ConvertMetrics(List<ScenarioMetric> metrics)
        {
            if (metrics == null || metrics.Count == 0)
            {
                return null;
            }

            var list = new List<MetricRecord>(metrics.Count);
            foreach (var metric in metrics)
            {
                list.Add(new MetricRecord
                {
                    key = metric.Key,
                    value = metric.Value
                });
            }
            return list;
        }

        private static string GetBuildConfig()
        {
#if UNITY_EDITOR
            var prefix = "Editor-";
#else
            var prefix = string.Empty;
#endif
            var config = Debug.isDebugBuild ? "Debug" : "Release";
            return prefix + config;
        }

        private static string ComputeDigestHash(in DigestRecord digest, in TimeState timeState)
        {
            var builder = new StringBuilder(128);
            builder.Append(digest.tick);
            builder.Append('|');
            builder.Append(digest.scenarioHash);
            builder.Append('|');
            builder.Append(digest.registryHash);
            builder.Append('|');
            builder.Append(digest.entityCount);
            builder.Append('|');
            builder.Append(digest.commandLogCount);
            builder.Append('|');
            builder.Append(digest.snapshotLogCount);
            builder.Append('|');
            builder.Append(digest.randomHash);
            builder.Append('|');
            builder.Append(digest.rewindHash);
            builder.Append('|');
            builder.Append(digest.tickTimeHash);
            builder.Append('|');
            builder.Append(timeState.IsPaused ? 1 : 0);
            builder.Append('|');
            builder.Append(timeState.WorldSeconds.ToString("R", CultureInfo.InvariantCulture));
            var hash = ComputeStableHash(builder.ToString());
            return hash.ToString();
        }

        private static Hash128 ComputeStableHash(string text)
        {
            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            var hashBytes = md5.ComputeHash(bytes);
            var guid = new Guid(hashBytes);
            return Hash128.Parse(guid.ToString("N"));
        }

        private static uint HashScenarioState(in ScenarioState state)
        {
            uint hash = FnvSeed;
            hash = HashStep(hash, (uint)state.Current);
            hash = HashStep(hash, state.IsInitialized ? 1u : 0u);
            hash = HashStep(hash, (uint)state.BootPhase);
            hash = HashStep(hash, state.EnableGodgame ? 1u : 0u);
            hash = HashStep(hash, state.EnableSpace4x ? 1u : 0u);
            hash = HashStep(hash, state.EnableEconomy ? 1u : 0u);
            return hash;
        }

        private static uint HashScenarioConfig(in ScenarioConfig config)
        {
            uint hash = FnvSeed;
            hash = HashStep(hash, config.GodgameSeed);
            hash = HashStep(hash, config.Space4xSeed);
            hash = HashStep(hash, (uint)math.asint(config.Difficulty));
            hash = HashStep(hash, (uint)math.asint(config.Density));
            hash = HashStep(hash, (uint)config.VillageCount);
            hash = HashStep(hash, (uint)config.VillagersPerVillage);
            hash = HashStep(hash, (uint)config.CarrierCount);
            hash = HashStep(hash, (uint)config.AsteroidCount);
            return hash;
        }

        private static uint HashTimeState(in TimeState state)
        {
            uint hash = FnvSeed;
            hash = HashStep(hash, state.Tick);
            hash = HashStep(hash, (uint)math.asint(state.DeltaTime));
            hash = HashStep(hash, (uint)math.asint(state.WorldSeconds));
            hash = HashStep(hash, (uint)math.asint(state.FixedDeltaTime));
            hash = HashStep(hash, state.IsPaused ? 1u : 0u);
            hash = HashStep(hash, (uint)math.asint(state.CurrentSpeedMultiplier));
            return hash;
        }

        private static uint HashRewindState(in RewindState state)
        {
            uint hash = FnvSeed;
            hash = HashStep(hash, (uint)state.Mode);
            hash = HashStep(hash, (uint)state.TargetTick);
            hash = HashStep(hash, (uint)math.asint(state.TickDuration));
            hash = HashStep(hash, (uint)state.MaxHistoryTicks);
            hash = HashStep(hash, state.PendingStepTicks);
            return hash;
        }

        private static uint HashTickTimeState(in TickTimeState state)
        {
            uint hash = FnvSeed;
            hash = HashStep(hash, state.Tick);
            hash = HashStep(hash, (uint)math.asint(state.FixedDeltaTime));
            hash = HashStep(hash, (uint)math.asint(state.WorldSeconds));
            hash = HashStep(hash, state.IsPaused ? 1u : 0u);
            hash = HashStep(hash, state.IsPlaying ? 1u : 0u);
            hash = HashStep(hash, state.TargetTick);
            return hash;
        }

        private static void CompareAgainstBaseline()
        {
            if (!File.Exists(s_baselinePath) || !File.Exists(s_logPath))
            {
                Debug.LogWarning($"[ScenarioRunRecorder] Baseline '{s_baselinePath}' or current log '{s_logPath}' missing; skipping diff.");
                return;
            }

            try
            {
                var baseline = LoadDigestMap(s_baselinePath);
                var current = LoadDigestMap(s_logPath);
                var mismatch = false;

                if (baseline.Count != current.Count)
                {
                    ScenarioExitUtility.ReportDeterminism("ScenarioBaselineCountMismatch", $"Baseline digest count {baseline.Count} != current {current.Count}.");
                    mismatch = true;
                }

                foreach (var pair in baseline)
                {
                    if (!current.TryGetValue(pair.Key, out var hash) || !string.Equals(hash, pair.Value, StringComparison.Ordinal))
                    {
                        ScenarioExitUtility.ReportDeterminism("ScenarioBaselineDrift", $"Determinism drift on tick {pair.Key}: baseline {pair.Value} vs current {hash ?? "<missing>"}");
                        mismatch = true;
                    }
                }

                if (!mismatch)
                {
                    Debug.Log($"[ScenarioRunRecorder] Determinism baseline matched ({baseline.Count} digests).");
                }
            }
            catch (Exception ex)
            {
                ScenarioExitUtility.ReportDeterminism("ScenarioBaselineError", ex.Message);
            }
        }

        private static Dictionary<uint, string> LoadDigestMap(string path)
        {
            var map = new Dictionary<uint, string>();
            foreach (var line in File.ReadLines(path))
            {
                if (!line.Contains("\"type\":\"digest\"", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    var record = JsonUtility.FromJson<DigestRecord>(line);
                    map[record.tick] = record.hash ?? string.Empty;
                }
                catch
                {
                    // ignore malformed line
                }
            }

            return map;
        }

        private static bool TryGetSingleton<T>(EntityManager entityManager, out T data) where T : unmanaged, IComponentData
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                data = default;
                return false;
            }

            data = query.GetSingleton<T>();
            return true;
        }

        private static void WriteJson<T>(T record)
        {
            if (s_writer == null)
            {
                return;
            }

            var json = JsonUtility.ToJson(record);
            s_writer.WriteLine(json);
        }

        private const uint FnvSeed = 2166136261u;

        private static uint HashStep(uint current, uint value)
        {
            unchecked
            {
                const uint prime = 16777619u;
                return (current ^ value) * prime;
            }
        }

        [Serializable]
        private struct RunHeaderRecord
        {
            public string type;
            public string scenarioId;
            public uint seed;
            public int runTicks;
            public float fixedDeltaTime;
            public string source;
            public string gitCommit;
            public string gitBranch;
            public bool gitDirty;
            public string buildConfig;
            public string platform;
            public string unityVersion;
            public string timestampUtc;
            public uint registryAggregateHash;
            public CatalogRecord[] catalogs;
        }

        [Serializable]
        private struct CatalogRecord
        {
            public string label;
            public string kind;
            public uint version;
            public int entryCount;
            public uint spatialVersion;
        }

        [Serializable]
        private struct DigestRecord
        {
            public string type;
            public uint tick;
            public string hash;
            public uint scenarioHash;
            public uint registryHash;
            public uint randomHash;
            public uint timeHash;
            public uint rewindHash;
            public uint tickTimeHash;
            public uint entityCount;
            public uint commandLogCount;
            public uint snapshotLogCount;
        }

        [Serializable]
        private struct RunSummaryRecord
        {
            public string type;
            public string scenarioId;
            public uint seed;
            public uint finalTick;
            public int runTicks;
            public int commandLogCount;
            public int snapshotLogCount;
            public bool frameBudgetExceeded;
            public float worstFrameMs;
            public string worstFrameGroup;
            public int registryContinuityWarnings;
            public int registryContinuityFailures;
            public int logBytes;
            public bool perfBudgetFailed;
            public string perfBudgetMetric;
            public float perfBudgetValue;
            public float perfBudgetLimit;
            public uint perfBudgetTick;
            public List<MetricRecord> metrics;
        }

        [Serializable]
        private struct MetricRecord
        {
            public string key;
            public double value;
        }
    }
}
