using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Time;
using UnityEngine;
using UnityEngine.SceneManagement;
using SystemEnv = System.Environment;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Headless
{
    static class Space4XScenarioEntryPoint
    {
        private const string ScenarioArg = "--scenario";
        private const string ReportArg = "--report";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string ReportPathEnv = "SPACE4X_SCENARIO_REPORT_PATH";
        private const string FailOnBudgetEnv = "SPACE4X_SCENARIO_FAIL_ON_BUDGET";
        private const string PerfGateSpawnBatchEnv = "SPACE4X_PERF_GATE_SPAWN_BATCH";
        private const string PerfGateLightweightEnv = "SPACE4X_PERF_GATE_LIGHTWEIGHT";
        private const string PerfGateRunTicksEnv = "SPACE4X_PERF_GATE_RUN_TICKS";
        private const string SoftExitOnQuestionsEnv = "SPACE4X_SOFT_EXIT_ON_QUESTIONS";
        private const string HeadlessPresentationEnv = "PUREDOTS_HEADLESS_PRESENTATION";
        private const string TelemetryPathEnv = "PUREDOTS_TELEMETRY_PATH";
        private const string TelemetryEnableEnv = "PUREDOTS_TELEMETRY_ENABLE";
        private const string PerfTelemetryPathEnv = "PUREDOTS_PERF_TELEMETRY_PATH";
        private const string ExitPolicyEnv = "PUREDOTS_EXIT_POLICY";
        private const string PresentationSceneName = "TRI_Space4X_Smoke";
        private static bool s_executed;
        private static bool s_loggedTelemetry;
        private static bool s_loggedPerfTelemetry;
        private static bool s_loggedBuildStamp;
        private static int s_exitFallbackScheduled;
        private static int s_killFallbackScheduled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void LoadPresentationSceneIfRequested()
        {
            if (!Application.isBatchMode || !PureDOTS.Runtime.Core.RuntimeMode.IsHeadless)
            {
                return;
            }

            if (!IsTruthy(global::System.Environment.GetEnvironmentVariable(HeadlessPresentationEnv)))
            {
                return;
            }

            if (!PureDOTS.Runtime.Core.RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            UnityDebug.Log($"[ScenarioEntryPoint] {HeadlessPresentationEnv}=1 detected; loading presentation scene '{PresentationSceneName}'.");
            SceneManager.LoadScene(PresentationSceneName, LoadSceneMode.Single);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void RunScenariosIfRequested()
        {
            if (s_executed || !Application.isBatchMode)
                return;

            Space4XHeadlessDiagnostics.InitializeFromArgs();
            if (Space4XHeadlessDiagnostics.Enabled)
            {
                Space4XHeadlessDiagnostics.UpdateProgress("bootstrap", "entrypoint", 0);
            }

            if (!TryGetArgument(ScenarioArg, out var scenarioArg))
                return;

            s_executed = true;
            LogBuildStampOnce();
            UnityDebug.Log($"SCENARIO_ARG:{scenarioArg}");
            var scenarioPath = ResolveScenarioArgToFilePath(scenarioArg);
            var scenarioFound = !string.IsNullOrWhiteSpace(scenarioPath) && File.Exists(scenarioPath);
            UnityDebug.Log($"SCENARIO_RESOLVED:{(scenarioFound ? scenarioPath : "(not found)")}");
            if (!scenarioFound)
            {
                UnityDebug.LogError($"[ScenarioEntryPoint] Scenario not found for '{scenarioArg}'.");
                Quit(1);
                return;
            }

            string reportPath = null;
            if (TryGetArgument(ReportArg, out var reportArg))
            {
                reportPath = ResolvePath(reportArg);
                var dir = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
            }

            try
            {
                if (LooksLikeSpace4XMiningScenarioJson(scenarioPath))
                {
                    SystemEnv.SetEnvironmentVariable(ScenarioPathEnv, scenarioPath);
                    if (!string.IsNullOrEmpty(reportPath))
                    {
                        SystemEnv.SetEnvironmentVariable(ReportPathEnv, reportPath);
                    }
                    DisableHeadlessProofsForScenario();
                    if (!Space4XHeadlessDiagnostics.Enabled && !string.IsNullOrEmpty(reportPath))
                    {
                        EnsureTelemetryPathDerivedFromReport(reportPath);
                    }

                    var telemetryPath = SystemEnv.GetEnvironmentVariable("PUREDOTS_TELEMETRY_PATH") ?? "(unset)";
                    LogTelemetryOutOnce(telemetryPath);
                    UnityDebug.Log($"[ScenarioEntryPoint] Space4X mining scenario requested: '{scenarioPath}'. Telemetry path='{telemetryPath}'.");
                    return;
                }

                var isPerfGate = IsPerfGateScenario(scenarioPath);
                if (isPerfGate)
                {
                    SetEnvIfUnset("PUREDOTS_HEADLESS_TIME_PROOF", "0");
                    SetEnvIfUnset("PUREDOTS_HEADLESS_REWIND_PROOF", "0");
                    SetEnvIfUnset("SPACE4X_HEADLESS_MINING_PROOF", "0");
                    SetEnvIfUnset("SPACE4X_HEADLESS_MOVEMENT_DIAG", "0");
                    SetEnvIfUnset("SPACE4X_RESOURCE_REGISTRY_POPULATION", "0");
                    SetEnvIfUnset(PerfGateSpawnBatchEnv, "10000");
                    if (IsLargePerfGateScenario(scenarioPath))
                    {
                        SetEnvIfUnset(PerfGateLightweightEnv, "1");
                    }
                    SetEnvIfUnset(ExitPolicyEnv, "never");
                    UnityDebug.Log("PERF_GATE_MODE:1");
                }

                LogTelemetryOutOnce(SystemEnv.GetEnvironmentVariable("PUREDOTS_TELEMETRY_PATH") ?? "(unset)");
                LogPerfTelemetryOutOnce(SystemEnv.GetEnvironmentVariable(PerfTelemetryPathEnv) ?? "(unset)");
                var result = isPerfGate
                    ? RunPerfGateScenario(scenarioPath, reportPath)
                    : ScenarioRunnerExecutor.RunFromFile(scenarioPath, reportPath);
                UnityDebug.Log($"[ScenarioEntryPoint] Scenario '{scenarioPath}' completed. ticks={result.RunTicks} snapshots={result.SnapshotLogCount}");
                var exitCode = 0;
                var invariantFail = ScenarioExitUtility.ShouldExitNonZero(result, out _);
                if (invariantFail)
                {
                    if (ShouldSoftExitOnQuestions())
                    {
                        UnityDebug.LogWarning("[ScenarioEntryPoint] Required questions missing or unknown; treating as warning and exiting 0.");
                    }
                    else
                    {
                        exitCode = Space4XHeadlessDiagnostics.TestFailExitCode;
                    }
                }
                else if (result.PerformanceBudgetFailed)
                {
                    UnityDebug.LogError($"[ScenarioEntryPoint] Performance budget failure ({result.PerformanceBudgetMetric}) at tick {result.PerformanceBudgetTick}: value={result.PerformanceBudgetValue:F2}, budget={result.PerformanceBudgetLimit:F2}");
                    exitCode = string.Equals(SystemEnv.GetEnvironmentVariable(FailOnBudgetEnv), "1", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
                }

                if (Space4XHeadlessDiagnostics.Enabled)
                {
                    Space4XHeadlessDiagnostics.UpdateProgress("complete", "scenario_runner", (uint)result.RunTicks);
                    Space4XHeadlessDiagnostics.WriteScenarioRunnerInvariants(result, exitCode);
                    Space4XHeadlessDiagnostics.ShutdownWriter();
                }

                ClearTelemetryEnvForExit();
                Quit(exitCode);
            }
            catch (Exception ex)
            {
                UnityDebug.LogError($"[ScenarioEntryPoint] Scenario execution failed: {ex}");
                Quit(1);
            }
        }

        private static void EnsureTelemetryPathDerivedFromReport(string reportPath)
        {
            if (!string.IsNullOrWhiteSpace(SystemEnv.GetEnvironmentVariable("PUREDOTS_TELEMETRY_PATH")))
            {
                return;
            }

            var reportDirectory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            var reportBase = Path.Combine(reportDirectory ?? string.Empty, Path.GetFileNameWithoutExtension(reportPath));
            var telemetryPath = $"{reportBase}_telemetry.ndjson";
            SystemEnv.SetEnvironmentVariable("PUREDOTS_TELEMETRY_PATH", telemetryPath);
            SystemEnv.SetEnvironmentVariable("PUREDOTS_TELEMETRY_ENABLE", "1");
        }

        private static void LogTelemetryOutOnce(string telemetryPath)
        {
            if (s_loggedTelemetry)
            {
                return;
            }

            s_loggedTelemetry = true;
            UnityDebug.Log($"TELEMETRY_OUT:{telemetryPath}");
        }

        private static void LogBuildStampOnce()
        {
            if (s_loggedBuildStamp)
            {
                return;
            }

            s_loggedBuildStamp = true;
            var commit = SystemEnv.GetEnvironmentVariable("GIT_COMMIT")
                ?? SystemEnv.GetEnvironmentVariable("GITHUB_SHA")
                ?? "(unset)";
            var branch = SystemEnv.GetEnvironmentVariable("GIT_BRANCH")
                ?? SystemEnv.GetEnvironmentVariable("GITHUB_REF_NAME")
                ?? "(unset)";
            UnityDebug.Log($"[BuildStamp] version={Application.version} unity={Application.unityVersion} build_guid={Application.buildGUID} commit={commit} branch={branch}");
        }

        private static void LogPerfTelemetryOutOnce(string telemetryPath)
        {
            if (s_loggedPerfTelemetry)
            {
                return;
            }

            s_loggedPerfTelemetry = true;
            UnityDebug.Log($"PERF_TELEMETRY_OUT:{telemetryPath}");
        }

        private static void ClearTelemetryEnvForExit()
        {
            SystemEnv.SetEnvironmentVariable(TelemetryPathEnv, string.Empty);
            SystemEnv.SetEnvironmentVariable(PerfTelemetryPathEnv, string.Empty);
            SystemEnv.SetEnvironmentVariable(TelemetryEnableEnv, "0");
        }

        private static bool IsPerfGateScenario(string scenarioPath)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return false;
            }

            return scenarioPath.Contains("perf_gate", StringComparison.OrdinalIgnoreCase)
                || scenarioPath.Contains("perfgate", StringComparison.OrdinalIgnoreCase);
        }

        private static ScenarioRunResult RunPerfGateScenario(string scenarioPath, string reportPath)
        {
            var overrideTicks = ResolvePerfGateRunTicks();
            if (overrideTicks <= 0)
            {
                return ScenarioRunnerExecutor.RunFromFile(scenarioPath, reportPath);
            }

            var json = File.ReadAllText(scenarioPath);
            if (!ScenarioRunner.TryParse(json, out var data, out var parseError))
            {
                throw new InvalidOperationException($"Scenario parse failed: {parseError}");
            }

            var originalTicks = data.runTicks;
            data.runTicks = Math.Max(1, overrideTicks);
            UnityDebug.Log($"[ScenarioEntryPoint] Perf gate runTicks override: {originalTicks} -> {data.runTicks}");
            return ScenarioRunnerExecutor.Run(data, scenarioPath, reportPath);
        }

        private static int ResolvePerfGateRunTicks()
        {
            var value = SystemEnv.GetEnvironmentVariable(PerfGateRunTicksEnv);
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : 0;
        }

        private static bool IsLargePerfGateScenario(string scenarioPath)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return false;
            }

            return scenarioPath.Contains("perf_gate_250k", StringComparison.OrdinalIgnoreCase)
                || scenarioPath.Contains("perf_gate_500k", StringComparison.OrdinalIgnoreCase)
                || scenarioPath.Contains("perf_gate_1m", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeSpace4XMiningScenarioJson(string scenarioPath)
        {
            // We intentionally avoid deserializing here (types live in gameplay asmdefs); this is a cheap schema sniff.
            // Space4X mining scenarios have: seed, duration_s, spawn:[...]
            const int charsToRead = 4096;
            using var stream = File.OpenRead(scenarioPath);
            using var reader = new StreamReader(stream);
            var buffer = new char[charsToRead];
            var read = reader.ReadBlock(buffer, 0, buffer.Length);
            var head = read > 0 ? new string(buffer, 0, read) : string.Empty;
            return head.Contains("\"duration_s\"", StringComparison.OrdinalIgnoreCase) &&
                   head.Contains("\"spawn\"", StringComparison.OrdinalIgnoreCase);
        }

        private static void DisableHeadlessProofsForScenario()
        {
            SetEnvIfUnset("PUREDOTS_HEADLESS_TIME_PROOF", "0");
            SetEnvIfUnset("PUREDOTS_HEADLESS_REWIND_PROOF", "0");
        }

        private static void SetEnvIfUnset(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(SystemEnv.GetEnvironmentVariable(key)))
            {
                SystemEnv.SetEnvironmentVariable(key, value);
            }
        }

        private static bool ShouldSoftExitOnQuestions()
        {
            var overrideValue = SystemEnv.GetEnvironmentVariable(SoftExitOnQuestionsEnv);
            if (!string.IsNullOrWhiteSpace(overrideValue))
            {
                return IsTruthy(overrideValue);
            }

            return Application.isBatchMode && PureDOTS.Runtime.Core.RuntimeMode.IsHeadless;
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetArgument(string key, out string value)
        {
            var args = SystemEnv.GetCommandLineArgs();
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

        private static string ResolveScenarioArgToFilePath(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                return arg;
            }

            if (File.Exists(arg))
            {
                return arg;
            }

            if (arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = ResolvePath(arg);
                if (File.Exists(resolved))
                {
                    return resolved;
                }

                var triRoot = SystemEnv.GetEnvironmentVariable("TRI_REPO_ROOT") ??
                              SystemEnv.GetEnvironmentVariable("TRI_ROOT");
                if (!string.IsNullOrWhiteSpace(triRoot))
                {
                    var triCandidate = Path.Combine(triRoot, arg);
                    if (File.Exists(triCandidate))
                    {
                        return triCandidate;
                    }
                }

                var dataCandidate = Path.Combine(Application.dataPath, arg);
                if (File.Exists(dataCandidate))
                {
                    return dataCandidate;
                }

                var scenarioCandidate = Path.Combine(Application.dataPath, "Scenarios", arg);
                if (File.Exists(scenarioCandidate))
                {
                    return scenarioCandidate;
                }

                return resolved;
            }

            var scenariosRoot = Path.Combine(Application.dataPath, "Scenarios");
            if (!Directory.Exists(scenariosRoot))
            {
                var triRoot = SystemEnv.GetEnvironmentVariable("TRI_REPO_ROOT") ??
                              SystemEnv.GetEnvironmentVariable("TRI_ROOT");
                if (!string.IsNullOrWhiteSpace(triRoot))
                {
                    var triCandidate = Path.Combine(triRoot, "Assets", "Scenarios", $"{arg}.json");
                    if (File.Exists(triCandidate))
                    {
                        return triCandidate;
                    }
                }
                return arg;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(scenariosRoot, "*.json", SearchOption.AllDirectories))
                {
                    if (TryReadScenarioId(file, out var scenarioId) &&
                        string.Equals(scenarioId, arg, StringComparison.Ordinal))
                    {
                        return file;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityDebug.LogWarning($"[ScenarioEntryPoint] Scenario scan failed: {ex.Message}");
            }

            return arg;
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
                return Path.GetFullPath(path ?? string.Empty);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static bool TryReadScenarioId(string path, out string scenarioId)
        {
            scenarioId = null;
            if (TryReadScenarioIdPrefix(path, out scenarioId))
            {
                return true;
            }

            return TryReadScenarioIdFull(path, out scenarioId);
        }

        private static bool TryReadScenarioIdPrefix(string path, out string scenarioId)
        {
            scenarioId = null;
            try
            {
                const int charsToRead = 8192;
                using var stream = File.OpenRead(path);
                using var reader = new StreamReader(stream);
                var buffer = new char[charsToRead];
                var read = reader.ReadBlock(buffer, 0, buffer.Length);
                var head = read > 0 ? new string(buffer, 0, read) : string.Empty;
                return TryParseScenarioId(head, out scenarioId);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadScenarioIdFull(string path, out string scenarioId)
        {
            scenarioId = null;
            try
            {
                var content = File.ReadAllText(path);
                return TryParseScenarioId(content, out scenarioId);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseScenarioId(string json, out string scenarioId)
        {
            scenarioId = null;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            const string key = "\"scenarioId\"";
            var keyIndex = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return false;
            }

            var colonIndex = json.IndexOf(':', keyIndex + key.Length);
            if (colonIndex < 0)
            {
                return false;
            }

            var quoteStart = json.IndexOf('"', colonIndex + 1);
            if (quoteStart < 0)
            {
                return false;
            }

            var quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0)
            {
                return false;
            }

            scenarioId = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            return !string.IsNullOrWhiteSpace(scenarioId);
        }

        private static void Quit(int exitCode)
        {
            if (exitCode != 0)
            {
                UnityDebug.LogError($"[ScenarioEntryPoint] Quit exit_code={exitCode}\n{SystemEnv.StackTrace}");
            }
            else
            {
                UnityDebug.Log($"[ScenarioEntryPoint] Quit exit_code={exitCode}");
            }
            if (Application.isBatchMode && PureDOTS.Runtime.Core.RuntimeMode.IsHeadless)
            {
                // Defer to headless exit handling to avoid Unity shutdown crash.
                var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    HeadlessExitUtility.Request(world.EntityManager, 0, exitCode);
                    return;
                }
                UnityDebug.LogWarning("[ScenarioEntryPoint] Default world unavailable during headless exit; falling back to Environment.Exit.");
                ScheduleExitFallback(exitCode, 2000);
                ScheduleKillFallback(7000);
                return;
            }
            Application.Quit(exitCode);
        }
        private static void ScheduleExitFallback(int exitCode, int delayMs)
        {
            if (Interlocked.Exchange(ref s_exitFallbackScheduled, 1) != 0)
            {
                return;
            }

            var thread = new Thread(() =>
            {
                Thread.Sleep(delayMs);
                try
                {
                    SystemEnv.Exit(exitCode);
                }
                catch
                {
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
        }

        private static void ScheduleKillFallback(int delayMs)
        {
            if (Interlocked.Exchange(ref s_killFallbackScheduled, 1) != 0)
            {
                return;
            }

            var thread = new Thread(() =>
            {
                Thread.Sleep(delayMs);
                try
                {
                    Process.GetCurrentProcess().Kill();
                }
                catch
                {
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
        }

    }
}
