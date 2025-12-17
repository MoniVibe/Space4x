using System;
using System.IO;
using PureDOTS.Runtime.Scenarios;
using UnityEngine;
using SystemEnv = System.Environment;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Headless
{
    static class Space4XScenarioEntryPoint
    {
        private const string ScenarioArg = "--scenario";
        private const string ReportArg = "--report";
        private static bool s_executed;

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
                var result = ScenarioRunnerExecutor.RunFromFile(scenarioPath, reportPath);
                UnityDebug.Log($"[ScenarioEntryPoint] Scenario '{scenarioPath}' completed. ticks={result.RunTicks} snapshots={result.SnapshotLogCount}");
                Quit(0);
            }
            catch (Exception ex)
            {
                UnityDebug.LogError($"[ScenarioEntryPoint] Scenario execution failed: {ex}");
                Quit(1);
            }
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
