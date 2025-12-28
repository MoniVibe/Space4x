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
        private const string FailOnBudgetEnv = "SPACE4X_SCENARIO_FAIL_ON_BUDGET";
        private const string HeadlessPresentationEnv = "PUREDOTS_HEADLESS_PRESENTATION";
        private const string PresentationSceneName = "TRI_Space4X_Smoke";
        private static bool s_executed;

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

            if (!TryGetArgument(ScenarioArg, out var scenarioArg))
                return;

            s_executed = true;
            var scenarioPath = ResolvePath(scenarioArg);
            if (!File.Exists(scenarioPath))
            {
                UnityDebug.LogError($"[ScenarioEntryPoint] Scenario file not found: {scenarioPath}");
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
                    DisableHeadlessProofsForScenario();
                    if (!string.IsNullOrEmpty(reportPath))
                    {
                        EnsureTelemetryPathDerivedFromReport(reportPath);
                    }

                    UnityDebug.Log($"[ScenarioEntryPoint] Space4X mining scenario requested: '{scenarioPath}'. Telemetry path='{SystemEnv.GetEnvironmentVariable("PUREDOTS_TELEMETRY_PATH") ?? "(unset)"}'.");
                    return;
                }

                var result = ScenarioRunnerExecutor.RunFromFile(scenarioPath, reportPath);
                UnityDebug.Log($"[ScenarioEntryPoint] Scenario '{scenarioPath}' completed. ticks={result.RunTicks} snapshots={result.SnapshotLogCount}");
                if (result.PerformanceBudgetFailed)
                {
                    UnityDebug.LogError($"[ScenarioEntryPoint] Performance budget failure ({result.PerformanceBudgetMetric}) at tick {result.PerformanceBudgetTick}: value={result.PerformanceBudgetValue:F2}, budget={result.PerformanceBudgetLimit:F2}");
                    Quit(string.Equals(SystemEnv.GetEnvironmentVariable(FailOnBudgetEnv), "1", StringComparison.OrdinalIgnoreCase) ? 2 : 0);
                }
                else
                {
                    Quit(0);
                }
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

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
                return Path.GetFullPath(path ?? string.Empty);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static void Quit(int exitCode) => Application.Quit(exitCode);
    }
}
