using System;
using System.IO;
using System.Text;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using UnityEngine;

namespace PureDOTS.Runtime.Devtools
{
    /// <summary>
    /// Entry points for running scenarios from CLI (-executeMethod) or debug menus.
    /// Future slices will drive actual world boot/run; for now this validates inputs and prints a summary.
    /// </summary>
    public static class ScenarioRunnerEntryPoints
    {
        /// <summary>
        /// Invoked via -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioFromArgs
        /// Expected args: --scenario <path to json> [--report <path>]
        /// </summary>
        public static void RunScenarioFromArgs()
        {
            var args = System.Environment.GetCommandLineArgs();
            var scenarioPath = ReadArg(args, "--scenario");
            var reportPath = ReadArg(args, "--report");

            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                Debug.LogWarning("ScenarioRunner: missing --scenario <path>");
                return;
            }

            if (!File.Exists(scenarioPath))
            {
                Debug.LogError($"ScenarioRunner: scenario not found at {scenarioPath}");
                return;
            }

            var json = File.ReadAllText(scenarioPath);
            if (!ScenarioRunner.TryParse(json, out var data, out var parseError))
            {
                Debug.LogError($"ScenarioRunner: failed to parse JSON: {parseError}");
                return;
            }

            if (!ScenarioRunner.TryBuild(data, Allocator.Temp, out var scenario, out var buildError))
            {
                Debug.LogError($"ScenarioRunner: failed to build scenario: {buildError}");
                return;
            }

            using (scenario)
            {
                var summary = $"ScenarioRunner: loaded {scenario.ScenarioId} seed={scenario.Seed} ticks={scenario.RunTicks} entities={scenario.EntityCounts.Length} commands={scenario.InputCommands.Length}";
                Debug.Log(summary);

                if (!string.IsNullOrWhiteSpace(reportPath))
                {
                    File.WriteAllText(reportPath, summary);
                }
            }
        }

        /// <summary>
        /// Invoked via -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioExecutorFromArgs
        /// Expected args: --scenario <name or path> [--report <path>]
        /// </summary>
        public static void RunScenarioExecutorFromArgs()
        {
            var args = System.Environment.GetCommandLineArgs();
            var scenarioArg = ReadArg(args, "--scenario");
            var reportPath = ReadArg(args, "--report");

            if (string.IsNullOrWhiteSpace(scenarioArg))
            {
                Debug.LogWarning("ScenarioExecutor: missing --scenario <name or path>");
                return;
            }

            var scenarioPath = ResolveScenarioPath(scenarioArg);
            if (string.IsNullOrWhiteSpace(scenarioPath) || !File.Exists(scenarioPath))
            {
                Debug.LogError($"ScenarioExecutor: scenario not found: {scenarioArg}");
                return;
            }

            try
            {
                var result = ScenarioRunnerExecutor.RunFromFile(scenarioPath, reportPath);
                Debug.Log($"ScenarioExecutor: completed {result.ScenarioId} ticks={result.RunTicks} snapshots={result.SnapshotLogCount}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ScenarioExecutor: run failed: {ex}");
            }
        }

        /// <summary>
        /// Run scale test scenario with metrics collection.
        /// Invoked via -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScaleTest
        /// Expected args: --scenario <name or path> [--metrics <report path>] [--target-ms <tick time budget>]
        ///                [--enable-lod-debug] [--enable-aggregate-debug]
        /// </summary>
        public static void RunScaleTest()
        {
            var args = System.Environment.GetCommandLineArgs();
            var scenarioArg = ReadArg(args, "--scenario");
            var metricsPath = ReadArg(args, "--metrics");
            var targetMsArg = ReadArg(args, "--target-ms");
            var enableLodDebug = HasFlag(args, "--enable-lod-debug");
            var enableAggregateDebug = HasFlag(args, "--enable-aggregate-debug");

            if (string.IsNullOrWhiteSpace(scenarioArg))
            {
                Debug.LogWarning("ScaleTest: missing --scenario <name or path>");
                Debug.Log("Available scale scenarios:");
                Debug.Log("  Scale: scale_baseline_10k, scale_stress_100k, scale_extreme_1m");
                Debug.Log("  Sanity: scale_mini_lod, scale_mini_aggregate");
                Debug.Log("  Game scenarios: scenario_space_01, scenario_god_01");
                return;
            }

            // Resolve scenario path
            var scenarioPath = ResolveScenarioPath(scenarioArg);
            if (string.IsNullOrWhiteSpace(scenarioPath) || !File.Exists(scenarioPath))
            {
                Debug.LogError($"ScaleTest: scenario not found: {scenarioArg}");
                return;
            }

            // Parse target tick time
            var targetTickTimeMs = 16.67f; // Default 60 FPS
            if (!string.IsNullOrWhiteSpace(targetMsArg) && float.TryParse(targetMsArg, out var parsed))
            {
                targetTickTimeMs = parsed;
            }

            // Load and validate scenario
            var json = File.ReadAllText(scenarioPath);
            if (!ScenarioRunner.TryParse(json, out var data, out var parseError))
            {
                Debug.LogError($"ScaleTest: failed to parse JSON: {parseError}");
                return;
            }

            if (!ScenarioRunner.TryBuild(data, Allocator.Temp, out var scenario, out var buildError))
            {
                Debug.LogError($"ScaleTest: failed to build scenario: {buildError}");
                return;
            }

            using (scenario)
            {
                Debug.Log($"[ScaleTest] Starting: {scenario.ScenarioId}");
                Debug.Log($"[ScaleTest] Target tick time: {targetTickTimeMs}ms");
                Debug.Log($"[ScaleTest] Ticks to run: {scenario.RunTicks}");
                Debug.Log($"[ScaleTest] Entity counts: {scenario.EntityCounts.Length} types");
                Debug.Log($"[ScaleTest] Debug flags: LOD={enableLodDebug}, Aggregate={enableAggregateDebug}");

                // Log entity breakdown
                for (int i = 0; i < scenario.EntityCounts.Length; i++)
                {
                    var ec = scenario.EntityCounts[i];
                    Debug.Log($"[ScaleTest]   {ec.RegistryId}: {ec.Count}");
                }

                // Create metrics config for this run
                var metricsConfig = new ScaleTestMetricsConfigData
                {
                    SampleInterval = 10,
                    LogInterval = 50,
                    CollectSystemTimings = true,
                    CollectMemoryStats = true,
                    EnableLODDebug = enableLodDebug,
                    EnableAggregateDebug = enableAggregateDebug,
                    TargetTickTimeMs = targetTickTimeMs,
                    TargetMemoryMB = 2048f
                };

                // Generate metrics report
                var report = GenerateScaleTestReport(scenario, targetTickTimeMs, metricsConfig);
                Debug.Log(report);

                if (!string.IsNullOrWhiteSpace(metricsPath))
                {
                    // Ensure directory exists
                    var dir = Path.GetDirectoryName(metricsPath);
                    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(metricsPath, report);
                    Debug.Log($"[ScaleTest] Report written to: {metricsPath}");
                }
            }
        }

        /// <summary>
        /// Serializable config data for scale test metrics.
        /// </summary>
        [Serializable]
        public struct ScaleTestMetricsConfigData
        {
            public uint SampleInterval;
            public uint LogInterval;
            public bool CollectSystemTimings;
            public bool CollectMemoryStats;
            public bool EnableLODDebug;
            public bool EnableAggregateDebug;
            public float TargetTickTimeMs;
            public float TargetMemoryMB;
        }

        /// <summary>
        /// Lists available scale test scenarios.
        /// </summary>
        public static void ListScaleScenarios()
        {
            Debug.Log("[ScaleTest] Available scale test scenarios:");
            Debug.Log("");
            Debug.Log("  Scale Tests:");
            Debug.Log("    - scale_baseline_10k     : 10k entities, target 60 FPS");
            Debug.Log("    - scale_stress_100k      : 100k entities, target 30 FPS");
            Debug.Log("    - scale_extreme_1m       : 1M+ entities, target 10 FPS");
            Debug.Log("");
            Debug.Log("  Sanity scenarios:");
            Debug.Log("    - scale_mini_lod       : 2k test entities with LOD components");
            Debug.Log("    - scale_mini_aggregate : 5 aggregates with 200 members");
            Debug.Log("");
            Debug.Log("  Game scenarios:");
            Debug.Log("    - scenario_space_01    : Space4X scenario (carriers/crafts/asteroids/fleets)");
            Debug.Log("    - scenario_god_01      : Godgame scenario (villagers/resources/villages)");
            Debug.Log("");
            Debug.Log("Usage:");
            Debug.Log("  -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScaleTest \\");
            Debug.Log("    --scenario <name> --metrics <output.json> [--enable-lod-debug] [--enable-aggregate-debug]");
        }

        private static string ResolveScenarioPath(string scenarioArg)
        {
            // If it's already a path, use it directly
            if (File.Exists(scenarioArg))
            {
                return scenarioArg;
            }

            // Try to find in Samples folder
            var basePath = "Packages/com.moni.puredots/Runtime/Runtime/Scenarios/Samples/";
            
            // Try with .json extension
            var withExtension = scenarioArg.EndsWith(".json") ? scenarioArg : scenarioArg + ".json";
            var fullPath = basePath + withExtension;
            
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            // Try common variations
            var variations = new[]
            {
                $"scale_{scenarioArg}.json",
                $"{scenarioArg}_scale.json",
                withExtension
            };

            foreach (var variant in variations)
            {
                var path = basePath + variant;
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static string GenerateScaleTestReport(ResolvedScenario scenario, float targetTickTimeMs, ScaleTestMetricsConfigData metricsConfig)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"scenarioId\": \"{scenario.ScenarioId}\",");
            sb.AppendLine($"  \"seed\": {scenario.Seed},");
            sb.AppendLine($"  \"runTicks\": {scenario.RunTicks},");
            sb.AppendLine($"  \"targetTickTimeMs\": {targetTickTimeMs},");
            sb.AppendLine($"  \"timestamp\": \"{DateTime.UtcNow:O}\",");
            sb.AppendLine("  \"entityCounts\": [");
            
            var totalEntities = 0;
            for (int i = 0; i < scenario.EntityCounts.Length; i++)
            {
                var ec = scenario.EntityCounts[i];
                totalEntities += ec.Count;
                var comma = i < scenario.EntityCounts.Length - 1 ? "," : "";
                sb.AppendLine($"    {{ \"registryId\": \"{ec.RegistryId}\", \"count\": {ec.Count} }}{comma}");
            }
            
            sb.AppendLine("  ],");
            sb.AppendLine($"  \"totalEntities\": {totalEntities},");
            sb.AppendLine("  \"metricsConfig\": {");
            sb.AppendLine($"    \"sampleInterval\": {metricsConfig.SampleInterval},");
            sb.AppendLine($"    \"logInterval\": {metricsConfig.LogInterval},");
            sb.AppendLine($"    \"collectSystemTimings\": {metricsConfig.CollectSystemTimings.ToString().ToLower()},");
            sb.AppendLine($"    \"collectMemoryStats\": {metricsConfig.CollectMemoryStats.ToString().ToLower()},");
            sb.AppendLine($"    \"enableLODDebug\": {metricsConfig.EnableLODDebug.ToString().ToLower()},");
            sb.AppendLine($"    \"enableAggregateDebug\": {metricsConfig.EnableAggregateDebug.ToString().ToLower()},");
            sb.AppendLine($"    \"targetTickTimeMs\": {metricsConfig.TargetTickTimeMs},");
            sb.AppendLine($"    \"targetMemoryMB\": {metricsConfig.TargetMemoryMB}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"status\": \"scenario_loaded\",");
            sb.AppendLine("  \"note\": \"Actual metrics collected during runtime execution\"");
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        private static string ReadArg(string[] args, string key)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.Equals(arg, key, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        return args[i + 1];
                    }
                    return string.Empty;
                }

                if (arg.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(key.Length + 1);
                }
            }

            return string.Empty;
        }

        private static bool HasFlag(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
