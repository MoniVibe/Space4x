using System;
using System.IO;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Demo
{
    /// <summary>
    /// Collects metrics from TelemetryStream and writes JSON/CSV reports to Reports/Space4X/.
    /// Captures screenshots at scenario start/end.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct Space4XDemoReporterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Ensure DemoReporterState singleton exists
            if (!SystemAPI.HasSingleton<DemoReporterState>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<DemoReporterState>(entity);
                state.EntityManager.SetComponentData(entity, new DemoReporterState
                {
                    ReportStarted = 0,
                    ReportCompleted = 0
                });
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonRW<DemoReporterState>(out var reporterState))
                return;

            // Start report when scenario begins
            if (reporterState.ValueRO.ReportStarted == 0 && SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                if (timeState.Tick > 0)
                {
                    reporterState.ValueRW.ReportStarted = 1;
                    CaptureStartScreenshot();
                }
            }

            // End report when scenario completes (or manually triggered)
            if (reporterState.ValueRO.ReportCompleted == 0 && reporterState.ValueRO.ReportStarted == 1)
            {
                // TODO: Detect scenario end (check ScenarioInfo or time limit)
                // For now, this is a placeholder
            }
        }

        [BurstCompile]
        private void CaptureStartScreenshot()
        {
            // Screenshot capture must be done from main thread
            // This is a placeholder - actual implementation would use Unity API
        }

        public static void WriteReport(DemoReportData reportData, NativeArray<TelemetryMetric> metrics)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportsDir = Path.Combine(Application.dataPath, "..", "Reports", "Space4X", reportData.ScenarioName.ToString());
            Directory.CreateDirectory(reportsDir);

            string jsonPath = Path.Combine(reportsDir, $"{timestamp}_metrics.json");
            string csvPath = Path.Combine(reportsDir, $"{timestamp}_metrics.csv");

            WriteJsonReport(jsonPath, reportData, metrics);
            WriteCsvReport(csvPath, metrics);
        }

        private static void WriteJsonReport(string path, DemoReportData reportData, NativeArray<TelemetryMetric> metrics)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"game\": \"{reportData.GameName}\",");
            sb.AppendLine($"  \"scenario\": \"{reportData.ScenarioName}\",");
            sb.AppendLine($"  \"timestamp\": \"{DateTime.Now:O}\",");
            sb.AppendLine($"  \"start_tick\": {reportData.StartTick},");
            sb.AppendLine($"  \"end_tick\": {reportData.EndTick},");
            sb.AppendLine("  \"metrics\": {");

            // Aggregate metrics by key
            var metricDict = new System.Collections.Generic.Dictionary<string, float>();
            for (int i = 0; i < metrics.Length; i++)
            {
                var metric = metrics[i];
                string key = metric.Key.ToString();
                if (!metricDict.ContainsKey(key))
                {
                    metricDict[key] = metric.Value;
                }
                else
                {
                    metricDict[key] = metric.Value; // Use latest value
                }
            }

            int count = 0;
            foreach (var kvp in metricDict)
            {
                sb.Append($"    \"{kvp.Key}\": {kvp.Value:F2}");
                if (count < metricDict.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
                count++;
            }

            sb.AppendLine("  }");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteCsvReport(string path, NativeArray<TelemetryMetric> metrics)
        {
            var sb = new StringBuilder();
            sb.AppendLine("metric,value,unit");

            for (int i = 0; i < metrics.Length; i++)
            {
                var metric = metrics[i];
                sb.AppendLine($"{metric.Key},{metric.Value:F2},{metric.Unit}");
            }

            File.WriteAllText(path, sb.ToString());
        }
    }
}

