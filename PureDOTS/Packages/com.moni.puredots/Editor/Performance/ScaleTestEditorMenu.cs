#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using PureDOTS.Runtime.Devtools;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PureDOTS.Editor.Performance
{
    /// <summary>
    /// Editor menu items for running scale tests.
    /// </summary>
    public static class ScaleTestEditorMenu
    {
        private const string SamplesPath = "Packages/com.moni.puredots/Runtime/Runtime/Scenarios/Samples/";
        private const string ReportsPath = "CI/Reports/";

        [MenuItem("PureDOTS/Scale Tests/List Available Scenarios", priority = 100)]
        public static void ListScenarios()
        {
            // TODO: reintroduce ScenarioRunnerEntryPoints or align with new ScenarioRunner APIs
            Debug.LogWarning("[ScaleTest] List Available Scenarios is temporarily disabled. Use Run Scenario menu items instead.");
            // ScenarioRunnerEntryPoints.ListScaleScenarios();
        }

        [MenuItem("PureDOTS/Scale Tests/Run Mini LOD Scenario", priority = 200)]
        public static void RunMiniLODScenario()
        {
            RunScenario("scale_mini_lod.json", "mini_lod_report.json");
        }

        [MenuItem("PureDOTS/Scale Tests/Run Mini Aggregate Scenario", priority = 201)]
        public static void RunMiniAggregateScenario()
        {
            RunScenario("scale_mini_aggregate.json", "mini_aggregate_report.json");
        }

        [MenuItem("PureDOTS/Scale Tests/Run Baseline 10k", priority = 300)]
        public static void RunBaseline10k()
        {
            RunScenario("scale_baseline_10k.json", "baseline_10k_report.json");
        }

        [MenuItem("PureDOTS/Scale Tests/Run Stress 100k", priority = 301)]
        public static void RunStress100k()
        {
            RunScenario("scale_stress_100k.json", "stress_100k_report.json");
        }

        [MenuItem("PureDOTS/Scale Tests/Run Extreme 1M", priority = 302)]
        public static void RunExtreme1M()
        {
            RunScenario("scale_extreme_1m.json", "extreme_1m_report.json");
        }

        [MenuItem("PureDOTS/Scale Tests/Game Scenarios/Space4X Scenario", priority = 500)]
        public static void RunSpace4XScenario()
        {
            RunScenario("scenario_space_01.json", "space_01_report.json");
        }

        [MenuItem("PureDOTS/Scale Tests/Game Scenarios/Godgame Scenario", priority = 501)]
        public static void RunGodgameScenario()
        {
            RunScenario("scenario_god_01.json", "god_01_report.json");
        }

        [MenuItem("PureDOTS/Scale Tests/Open Reports Folder", priority = 400)]
        public static void OpenReportsFolder()
        {
            EnsureReportsFolder();
            EditorUtility.RevealInFinder(ReportsPath);
        }

        private static void RunScenario(string scenarioFileName, string reportFileName)
        {
            var scenarioPath = SamplesPath + scenarioFileName;
            
            if (!File.Exists(scenarioPath))
            {
                Debug.LogError($"[ScaleTest] Scenario not found: {scenarioPath}");
                return;
            }

            EnsureReportsFolder();
            var reportPath = ReportsPath + reportFileName;

            Debug.Log($"[ScaleTest] Running scenario: {scenarioFileName}");
            Debug.Log($"[ScaleTest] Report will be written to: {reportPath}");

            try
            {
                var sw = Stopwatch.StartNew();
                var result = PureDOTS.Runtime.Scenarios.ScenarioRunnerExecutor.RunFromFile(scenarioPath, reportPath);
                sw.Stop();

                var msPerTick = result.RunTicks > 0 ? sw.Elapsed.TotalMilliseconds / result.RunTicks : 0d;

                Debug.Log($"[ScaleTest] Completed {result.RunTicks} ticks in {sw.Elapsed.TotalSeconds:0.###}s ({msPerTick:0.###} ms/tick). ScenarioId={result.ScenarioId}, seed={result.Seed}, logs={result.CommandLogCount + result.SnapshotLogCount}");
                Debug.Log($"[ScaleTest] Report written to: {reportPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ScaleTest] Failed to run scenario {scenarioFileName}: {ex.Message}");
            }
        }

        private static string GenerateReport(
            PureDOTS.Runtime.Scenarios.ResolvedScenario scenario,
            string scenarioFileName)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"scenarioFile\": \"{scenarioFileName}\",");
            sb.AppendLine($"  \"scenarioId\": \"{scenario.ScenarioId}\",");
            sb.AppendLine($"  \"seed\": {scenario.Seed},");
            sb.AppendLine($"  \"runTicks\": {scenario.RunTicks},");
            sb.AppendLine($"  \"timestamp\": \"{System.DateTime.UtcNow:O}\",");
            sb.AppendLine("  \"entityCounts\": [");

            int totalEntities = 0;
            for (int i = 0; i < scenario.EntityCounts.Length; i++)
            {
                var ec = scenario.EntityCounts[i];
                totalEntities += ec.Count;
                var comma = i < scenario.EntityCounts.Length - 1 ? "," : "";
                sb.AppendLine($"    {{ \"registryId\": \"{ec.RegistryId}\", \"count\": {ec.Count} }}{comma}");
            }

            sb.AppendLine("  ],");
            sb.AppendLine($"  \"totalEntities\": {totalEntities},");
            sb.AppendLine("  \"status\": \"scenario_validated\",");
            sb.AppendLine("  \"note\": \"Run via Unity Editor menu. For full metrics, use CLI batch mode.\"");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void EnsureReportsFolder()
        {
            if (!Directory.Exists(ReportsPath))
            {
                Directory.CreateDirectory(ReportsPath);
            }
        }
    }
}
#endif
