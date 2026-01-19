using System;
using System.IO;
using PureDOTS.Runtime.Scenarios;
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
        private const string HeadlessPresentationEnv = "PUREDOTS_HEADLESS_PRESENTATION";
        private const string TelemetryPathEnv = "PUREDOTS_TELEMETRY_PATH";
        private const string TelemetryEnableEnv = "PUREDOTS_TELEMETRY_ENABLE";
        private const string PerfTelemetryPathEnv = "PUREDOTS_PERF_TELEMETRY_PATH";
        private const string ExitPolicyEnv = "PUREDOTS_EXIT_POLICY";
        private const string PresentationSceneName = "TRI_Space4X_Smoke";
        private static bool s_executed;
        private static bool s_loggedTelemetry;
        private static bool s_loggedPerfTelemetry;

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

                if (IsPerfGateScenario(scenarioPath))
                {
                    SetEnvIfUnset("PUREDOTS_HEADLESS_TIME_PROOF", "0");
                    SetEnvIfUnset("PUREDOTS_HEADLESS_REWIND_PROOF", "0");
                    SetEnvIfUnset("SPACE4X_HEADLESS_MINING_PROOF", "0");
                    SetEnvIfUnset("SPACE4X_HEADLESS_MOVEMENT_DIAG", "0");
                    SetEnvIfUnset(ExitPolicyEnv, "never");
                    UnityDebug.Log("PERF_GATE_MODE:1");
                }

                LogTelemetryOutOnce(SystemEnv.GetEnvironmentVariable("PUREDOTS_TELEMETRY_PATH") ?? "(unset)");
                LogPerfTelemetryOutOnce(SystemEnv.GetEnvironmentVariable(PerfTelemetryPathEnv) ?? "(unset)");
                var result = ScenarioRunnerExecutor.RunFromFile(scenarioPath, reportPath);
                UnityDebug.Log($"[ScenarioEntryPoint] Scenario '{scenarioPath}' completed. ticks={result.RunTicks} snapshots={result.SnapshotLogCount}");
                var exitCode = 0;
                var invariantFail = ScenarioExitUtility.ShouldExitNonZero(result, out _);
                if (invariantFail)
                {
                    exitCode = Space4XHeadlessDiagnostics.TestFailExitCode;
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

        private static void Quit(int exitCode) => Application.Quit(exitCode);
    }
}
