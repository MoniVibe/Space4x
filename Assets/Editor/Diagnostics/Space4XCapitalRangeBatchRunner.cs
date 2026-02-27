#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Space4X.Headless;
using Space4x.Scenario;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SysEnv = global::System.Environment;

namespace Space4X.Editor.Diagnostics
{
    /// <summary>
    /// Batch CLI runner that executes one or many capital-range scenarios and exports a scorecard.
    /// Usage:
    ///   Unity.exe -batchmode -nographics -projectPath <repo> -executeMethod Space4X.Editor.Diagnostics.Space4XCapitalRangeBatchRunner.Run --suite Assets/Scenarios/space4x_capital_shooting_range_suite.v1.json --outDir Temp/capital_range_batch
    ///   Unity.exe -batchmode -nographics -projectPath <repo> -executeMethod Space4X.Editor.Diagnostics.Space4XCapitalRangeBatchRunner.Run --scenario Assets/Scenarios/space4x_capital_shooting_range_micro.json --outDir Temp/capital_range_batch
    /// </summary>
    public static class Space4XCapitalRangeBatchRunner
    {
        private const string HeadlessScenePath = "Assets/Scenes/HeadlessBootstrap.unity";
        private const string DefaultScenarioPath = "Assets/Scenarios/space4x_capital_shooting_range_micro.json";
        private const string DefaultSuiteId = "space4x.capital_range.single";
        private const double DefaultTimeoutSeconds = 150d;

        private static readonly FixedString64Bytes MetricShotsFired = new FixedString64Bytes("space4x.gunnery.capital_range.shots.fired");
        private static readonly FixedString64Bytes MetricShotsHit = new FixedString64Bytes("space4x.gunnery.capital_range.shots.hit");
        private static readonly FixedString64Bytes MetricHitRate = new FixedString64Bytes("space4x.gunnery.capital_range.hit_rate");
        private static readonly FixedString64Bytes MetricReactionTime = new FixedString64Bytes("space4x.gunnery.capital_range.reaction_time_s");
        private static readonly FixedString64Bytes MetricScore = new FixedString64Bytes("space4x.gunnery.capital_range.score");

        private static bool s_active;
        private static bool s_stopRequested;
        private static int s_exitCode;
        private static string s_exitReason = string.Empty;
        private static string s_outDir = string.Empty;
        private static string s_suiteId = string.Empty;
        private static double s_timeoutSeconds;
        private static double s_runStartTime;
        private static double s_suiteStartTime;
        private static Snapshot s_latest;
        private static bool s_hasSnapshot;
        private static List<RunPlanEntry> s_plan = new List<RunPlanEntry>();
        private static List<RunResultRecord> s_results = new List<RunResultRecord>();
        private static int s_runIndex;
        private static int s_currentExitCode;
        private static string s_currentExitReason = string.Empty;

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                global::UnityEngine.Debug.LogError("[CapitalRangeBatchRunner] This runner is batchmode-only.");
                EditorApplication.Exit(2);
                return;
            }

            if (s_active)
            {
                global::UnityEngine.Debug.LogWarning("[CapitalRangeBatchRunner] already active.");
                return;
            }

            ParseArgs(out var scenarioPath, out var scenarioList, out var suitePath, out s_outDir, out s_timeoutSeconds);
            if (string.IsNullOrWhiteSpace(s_outDir))
            {
                s_outDir = Path.Combine("Temp", "capital_range_batch");
            }

            s_outDir = Path.GetFullPath(s_outDir);
            Directory.CreateDirectory(s_outDir);

            s_plan = BuildRunPlan(scenarioPath, scenarioList, suitePath, out s_suiteId);
            if (s_plan.Count == 0)
            {
                global::UnityEngine.Debug.LogError("[CapitalRangeBatchRunner] no scenarios resolved.");
                EditorApplication.Exit(2);
                return;
            }

            s_results.Clear();
            s_runIndex = 0;
            s_exitCode = 0;
            s_exitReason = "completed";
            s_suiteStartTime = EditorApplication.timeSinceStartup;

            s_active = true;
            s_stopRequested = false;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            StartCurrentRun();
        }

        private static void StartCurrentRun()
        {
            if (s_runIndex >= s_plan.Count)
            {
                FinalizeAndExit();
                return;
            }

            s_latest = default;
            s_hasSnapshot = false;
            s_stopRequested = false;
            s_currentExitCode = 1;
            s_currentExitReason = "unknown";
            s_runStartTime = EditorApplication.timeSinceStartup;

            var run = s_plan[s_runIndex];
            ConfigureEnvironment(run);

            EditorSceneManager.OpenScene(HeadlessScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            global::UnityEngine.Debug.Log($"[CapitalRangeBatchRunner] run_start index={s_runIndex + 1}/{s_plan.Count} id='{run.Id}' scenario='{run.ScenarioPath}' timeout_s={s_timeoutSeconds:0.#}");
        }

        private static void OnEditorUpdate()
        {
            if (!s_active)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                if (s_stopRequested)
                {
                    FinalizeCurrentRun();
                }

                return;
            }

            var elapsed = EditorApplication.timeSinceStartup - s_runStartTime;
            if (elapsed > s_timeoutSeconds)
            {
                BeginStopCurrentRun(3, "timeout");
                return;
            }

            if (!TryReadSnapshot(out var snapshot))
            {
                return;
            }

            s_latest = snapshot;
            s_hasSnapshot = true;

            if (snapshot.EndTick == 0 || snapshot.CurrentTick < snapshot.EndTick)
            {
                return;
            }

            if (snapshot.ShotsFired > 0f)
            {
                BeginStopCurrentRun(0, "completed_with_fire");
            }
            else
            {
                BeginStopCurrentRun(1, "completed_no_fire");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!s_active)
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode && s_stopRequested)
            {
                FinalizeCurrentRun();
            }
        }

        private static void BeginStopCurrentRun(int exitCode, string reason)
        {
            if (s_stopRequested)
            {
                return;
            }

            s_stopRequested = true;
            s_currentExitCode = exitCode;
            s_currentExitReason = reason ?? "unknown";
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void FinalizeCurrentRun()
        {
            if (s_runIndex < 0 || s_runIndex >= s_plan.Count)
            {
                return;
            }

            var run = s_plan[s_runIndex];
            var elapsedWallSeconds = (float)math.max(0f, (float)(EditorApplication.timeSinceStartup - s_runStartTime));
            var result = new RunResultRecord
            {
                id = run.Id,
                label = run.Label,
                scenarioPath = run.ScenarioPath,
                tags = run.Tags,
                exitReason = s_currentExitReason,
                exitCode = s_currentExitCode,
                hasSnapshot = s_hasSnapshot,
                elapsedWallSeconds = elapsedWallSeconds,
                world = s_latest.WorldName ?? string.Empty,
                currentTick = s_latest.CurrentTick,
                endTick = s_latest.EndTick,
                shotsFired = s_latest.ShotsFired,
                shotsHit = s_latest.ShotsHit,
                hitRate = s_latest.HitRate,
                reactionTimeS = s_latest.ReactionTimeSeconds,
                score = s_latest.Score
            };
            s_results.Add(result);
            WriteRunResultJson(run, result);

            if (s_currentExitCode > s_exitCode)
            {
                s_exitCode = s_currentExitCode;
            }

            s_runIndex++;
            if (s_runIndex >= s_plan.Count)
            {
                FinalizeAndExit();
                return;
            }

            StartCurrentRun();
        }

        private static void FinalizeAndExit()
        {
            var suiteResultPath = WriteSuiteResultJson();
            global::UnityEngine.Debug.Log($"[CapitalRangeBatchRunner] done runs={s_results.Count} exit={s_exitCode} result='{suiteResultPath}'");

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            s_active = false;
            s_stopRequested = false;
            EditorApplication.Exit(s_exitCode);
        }

        private static string WriteRunResultJson(RunPlanEntry run, RunResultRecord result)
        {
            var fileName = $"capital_range_result_{SanitizeFileToken(run.Id)}.json";
            var path = Path.Combine(s_outDir, fileName);
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            return path;
        }

        private static string WriteSuiteResultJson()
        {
            var output = new SuiteResultRecord
            {
                suiteId = string.IsNullOrWhiteSpace(s_suiteId) ? DefaultSuiteId : s_suiteId,
                generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                scene = HeadlessScenePath,
                outDir = s_outDir,
                exitCode = s_exitCode,
                runCount = s_results.Count,
                passCount = s_results.Count(r => r.exitCode == 0),
                failCount = s_results.Count(r => r.exitCode == 1),
                timeoutCount = s_results.Count(r => r.exitCode == 3),
                elapsedWallSeconds = (float)math.max(0f, (float)(EditorApplication.timeSinceStartup - s_suiteStartTime)),
                runs = s_results.ToArray(),
                rankingByScore = s_results
                    .Where(r => r.hasSnapshot)
                    .OrderByDescending(r => r.score)
                    .ThenByDescending(r => r.hitRate)
                    .Select((r, index) => new RankedRunRecord
                    {
                        rank = index + 1,
                        id = r.id,
                        label = r.label,
                        score = r.score,
                        hitRate = r.hitRate,
                        shotsFired = r.shotsFired,
                        shotsHit = r.shotsHit,
                        reactionTimeS = r.reactionTimeS
                    })
                    .ToArray()
            };

            var path = Path.Combine(s_outDir, "capital_range_result.json");
            File.WriteAllText(path, JsonUtility.ToJson(output, true));
            return path;
        }

        private static List<RunPlanEntry> BuildRunPlan(string scenarioPathArg, string scenarioListArg, string suitePathArg, out string suiteId)
        {
            var plan = new List<RunPlanEntry>();
            suiteId = DefaultSuiteId;

            if (!string.IsNullOrWhiteSpace(suitePathArg))
            {
                var suitePath = ResolveProjectPath(suitePathArg);
                if (!File.Exists(suitePath))
                {
                    global::UnityEngine.Debug.LogWarning($"[CapitalRangeBatchRunner] suite not found: {suitePath}");
                }
                else
                {
                    try
                    {
                        var json = File.ReadAllText(suitePath);
                        var suite = JsonUtility.FromJson<ScenarioSuiteConfig>(json);
                        if (suite?.scenarios != null && suite.scenarios.Length > 0)
                        {
                            suiteId = string.IsNullOrWhiteSpace(suite.suiteId) ? Path.GetFileNameWithoutExtension(suitePath) : suite.suiteId;
                            foreach (var entry in suite.scenarios)
                            {
                                if (entry == null || string.IsNullOrWhiteSpace(entry.path))
                                {
                                    continue;
                                }

                                var resolvedPath = ResolveProjectPath(entry.path);
                                plan.Add(new RunPlanEntry
                                {
                                    Id = string.IsNullOrWhiteSpace(entry.id) ? Path.GetFileNameWithoutExtension(resolvedPath) : entry.id.Trim(),
                                    Label = string.IsNullOrWhiteSpace(entry.label) ? Path.GetFileNameWithoutExtension(resolvedPath) : entry.label.Trim(),
                                    ScenarioPath = resolvedPath,
                                    Tags = entry.tags ?? Array.Empty<string>()
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        global::UnityEngine.Debug.LogError($"[CapitalRangeBatchRunner] failed to parse suite '{suitePathArg}': {ex.Message}");
                    }
                }
            }

            if (plan.Count == 0 && !string.IsNullOrWhiteSpace(scenarioListArg))
            {
                var tokens = scenarioListArg.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < tokens.Length; i++)
                {
                    var token = tokens[i].Trim();
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }

                    var resolvedPath = ResolveProjectPath(token);
                    var id = Path.GetFileNameWithoutExtension(resolvedPath);
                    plan.Add(new RunPlanEntry
                    {
                        Id = id,
                        Label = id,
                        ScenarioPath = resolvedPath,
                        Tags = Array.Empty<string>()
                    });
                }

                if (plan.Count > 0)
                {
                    suiteId = "space4x.capital_range.scenario_list";
                }
            }

            if (plan.Count == 0)
            {
                var scenarioPath = string.IsNullOrWhiteSpace(scenarioPathArg) ? DefaultScenarioPath : scenarioPathArg;
                var resolvedPath = ResolveProjectPath(scenarioPath);
                var id = Path.GetFileNameWithoutExtension(resolvedPath);
                plan.Add(new RunPlanEntry
                {
                    Id = id,
                    Label = id,
                    ScenarioPath = resolvedPath,
                    Tags = Array.Empty<string>()
                });
                suiteId = "space4x.capital_range.single";
            }

            return plan;
        }

        private static string ResolveProjectPath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(projectRoot, rawPath));
        }

        private static void ParseArgs(out string scenarioPath, out string scenarioList, out string suitePath, out string outDir, out double timeoutSeconds)
        {
            scenarioPath = DefaultScenarioPath;
            scenarioList = string.Empty;
            suitePath = string.Empty;
            outDir = string.Empty;
            timeoutSeconds = DefaultTimeoutSeconds;

            var args = SysEnv.GetCommandLineArgs();
            if (args == null || args.Length == 0)
            {
                return;
            }

            for (var i = 0; i < args.Length; i++)
            {
                var token = args[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (token.Equals("--scenario", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    scenarioPath = args[++i];
                    continue;
                }

                if (token.Equals("--scenarioList", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    scenarioList = args[++i];
                    continue;
                }

                if (token.Equals("--suite", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    suitePath = args[++i];
                    continue;
                }

                if (token.Equals("--outDir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    outDir = args[++i];
                    continue;
                }

                if (token.Equals("--timeoutSec", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
                    double.TryParse(args[++i], out var parsed) && parsed > 0d)
                {
                    timeoutSeconds = parsed;
                }
            }
        }

        private static void ConfigureEnvironment(RunPlanEntry run)
        {
            var telemetryName = $"capital_range_telemetry_{SanitizeFileToken(run.Id)}.ndjson";
            var telemetryPath = Path.Combine(s_outDir, telemetryName);
            var enableMiningProof = ResolveHeadlessFlag("SPACE4X_HEADLESS_MINING_PROOF", run.ScenarioPath, "space4x.q.mining.");
            var enableMovementDiag = ResolveHeadlessFlag("SPACE4X_HEADLESS_MOVEMENT_DIAG", run.ScenarioPath, "space4x.q.movement.turnrate_bounds");

            SysEnv.SetEnvironmentVariable("SPACE4X_SCENARIO_PATH", run.ScenarioPath);
            SysEnv.SetEnvironmentVariable("PUREDOTS_HEADLESS", "1");
            SysEnv.SetEnvironmentVariable("PUREDOTS_FORCE_RENDER", "0");
            SysEnv.SetEnvironmentVariable("PUREDOTS_RENDERING", "0");
            SysEnv.SetEnvironmentVariable("PUREDOTS_EXIT_POLICY", "nevernonzero");
            SysEnv.SetEnvironmentVariable("PUREDOTS_HEADLESS_TIME_PROOF", "0");
            SysEnv.SetEnvironmentVariable("PUREDOTS_HEADLESS_REWIND_PROOF", "0");
            SysEnv.SetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF", enableMiningProof ? "1" : "0");
            SysEnv.SetEnvironmentVariable("SPACE4X_HEADLESS_MOVEMENT_DIAG", enableMovementDiag ? "1" : "0");
            SysEnv.SetEnvironmentVariable("SPACE4X_HEADLESS_SCENARIO_QUIT_DISABLE", string.Empty);
            SysEnv.SetEnvironmentVariable("PUREDOTS_TELEMETRY_ENABLE", "1");
            SysEnv.SetEnvironmentVariable("PUREDOTS_TELEMETRY_PATH", telemetryPath);
            SysEnv.SetEnvironmentVariable("PUREDOTS_TELEMETRY_MAX_BYTES", "8388608");
            SysEnv.SetEnvironmentVariable("PUREDOTS_PERF_TELEMETRY_PATH", "NUL");
        }

        private static bool ResolveHeadlessFlag(string envKey, string scenarioPath, string questionPrefix)
        {
            return ReadBoolEnv(envKey) || ScenarioRequestsQuestionPrefix(scenarioPath, questionPrefix);
        }

        private static bool ReadBoolEnv(string key)
        {
            var value = SysEnv.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ScenarioRequestsQuestionPrefix(string scenarioPath, string questionPrefix)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath) ||
                string.IsNullOrWhiteSpace(questionPrefix) ||
                !File.Exists(scenarioPath))
            {
                return false;
            }

            try
            {
                const int maxChars = 131072;
                using var stream = File.OpenRead(scenarioPath);
                using var reader = new StreamReader(stream);
                var buffer = new char[maxChars];
                var read = reader.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    return false;
                }

                var head = new string(buffer, 0, read);
                var escapedPrefix = Regex.Escape(questionPrefix);
                return Regex.IsMatch(
                    head,
                    $"\"id\"\\s*:\\s*\"{escapedPrefix}",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadSnapshot(out Snapshot snapshot)
        {
            snapshot = default;
            for (var i = 0; i < World.All.Count; i++)
            {
                var world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                var entityManager = world.EntityManager;
                using var runtimeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XScenarioRuntime>());
                if (runtimeQuery.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                var runtime = runtimeQuery.GetSingleton<Space4XScenarioRuntime>();
                snapshot.WorldName = world.Name;
                snapshot.EndTick = runtime.EndTick;

                using var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
                if (!timeQuery.IsEmptyIgnoreFilter)
                {
                    snapshot.CurrentTick = timeQuery.GetSingleton<TimeState>().Tick;
                }

                using var metricQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Space4XOperatorReportTag>(),
                    ComponentType.ReadOnly<Space4XOperatorMetric>());
                if (metricQuery.IsEmptyIgnoreFilter)
                {
                    return true;
                }

                var reportEntity = metricQuery.GetSingletonEntity();
                var metrics = entityManager.GetBuffer<Space4XOperatorMetric>(reportEntity);
                snapshot.ShotsFired = TryGetMetric(metrics, MetricShotsFired, out var shotsFired) ? shotsFired : -1f;
                snapshot.ShotsHit = TryGetMetric(metrics, MetricShotsHit, out var shotsHit) ? shotsHit : -1f;
                snapshot.HitRate = TryGetMetric(metrics, MetricHitRate, out var hitRate) ? hitRate : -1f;
                snapshot.ReactionTimeSeconds = TryGetMetric(metrics, MetricReactionTime, out var reactionTime) ? reactionTime : -1f;
                snapshot.Score = TryGetMetric(metrics, MetricScore, out var score) ? score : -1f;
                return true;
            }

            return false;
        }

        private static bool TryGetMetric(DynamicBuffer<Space4XOperatorMetric> metrics, FixedString64Bytes key, out float value)
        {
            for (var i = 0; i < metrics.Length; i++)
            {
                if (!metrics[i].Key.Equals(key))
                {
                    continue;
                }

                value = metrics[i].Value;
                return true;
            }

            value = 0f;
            return false;
        }

        private static string SanitizeFileToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "run";
            }

            var chars = value.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if ((c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '-' || c == '_')
                {
                    continue;
                }

                chars[i] = '_';
            }

            return new string(chars);
        }

        [Serializable]
        private sealed class ScenarioSuiteConfig
        {
            public string suiteId;
            public ScenarioSuiteEntry[] scenarios;
        }

        [Serializable]
        private sealed class ScenarioSuiteEntry
        {
            public string id;
            public string label;
            public string path;
            public string[] tags;
        }

        [Serializable]
        private sealed class SuiteResultRecord
        {
            public string suiteId;
            public string generatedUtc;
            public string scene;
            public string outDir;
            public int exitCode;
            public int runCount;
            public int passCount;
            public int failCount;
            public int timeoutCount;
            public float elapsedWallSeconds;
            public RunResultRecord[] runs;
            public RankedRunRecord[] rankingByScore;
        }

        [Serializable]
        private sealed class RunResultRecord
        {
            public string id;
            public string label;
            public string scenarioPath;
            public string[] tags;
            public string exitReason;
            public int exitCode;
            public bool hasSnapshot;
            public float elapsedWallSeconds;
            public string world;
            public uint currentTick;
            public uint endTick;
            public float shotsFired;
            public float shotsHit;
            public float hitRate;
            public float reactionTimeS;
            public float score;
        }

        [Serializable]
        private sealed class RankedRunRecord
        {
            public int rank;
            public string id;
            public string label;
            public float score;
            public float hitRate;
            public float shotsFired;
            public float shotsHit;
            public float reactionTimeS;
        }

        private struct RunPlanEntry
        {
            public string Id;
            public string Label;
            public string ScenarioPath;
            public string[] Tags;
        }

        private struct Snapshot
        {
            public string WorldName;
            public uint CurrentTick;
            public uint EndTick;
            public float ShotsFired;
            public float ShotsHit;
            public float HitRate;
            public float ReactionTimeSeconds;
            public float Score;
        }
    }
}
#endif
