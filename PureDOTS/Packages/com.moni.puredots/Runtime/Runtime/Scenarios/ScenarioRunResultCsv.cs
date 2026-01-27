using System.IO;
using System.Text;
using UnityEngine;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Lightweight CSV serializer for ScenarioRunResult. Appends a header if the file is new.
    /// </summary>
    public static class ScenarioRunResultCsv
    {
        private static readonly string Header = string.Join(",",
            "scenarioId",
            "seed",
            "runTicks",
            "finalTick",
            "telemetryVersion",
            "commandLogCount",
            "commandCapacity",
            "commandBytes",
            "snapshotLogCount",
            "snapshotCapacity",
            "snapshotBytes",
            "totalLogBytes",
            "frameTimingBudgetExceeded",
            "frameTimingWorstMs",
            "frameTimingWorstGroup",
            "registryContinuityWarnings",
            "registryContinuityFailures",
            "entityCountEntries",
            "performanceBudgetFailed",
            "performanceBudgetMetric",
            "performanceBudgetValue",
            "performanceBudgetLimit",
            "performanceBudgetTick",
            "exitPolicy",
            "highestSeverity",
            "issueCount",
            "issueCodes",
            "assertionCount",
            "assertionFailures");

        public static void Write(string path, in ScenarioRunResult result)
        {
            var line = BuildLine(result);
            var fileExists = File.Exists(path);
            var sb = new StringBuilder();
            if (!fileExists)
            {
                sb.AppendLine(Header);
            }
            sb.AppendLine(line);
            File.AppendAllText(path, sb.ToString());
            Debug.Log($"ScenarioRunner: wrote CSV report to {path}");
        }

        private static string BuildLine(in ScenarioRunResult result)
        {
            // Basic CSV escaping for commas/quotes if they appear; expected fields are simple.
            string Escape(string v) => $"\"{v.Replace("\"", "\"\"")}\"";
            var issueCount = result.Issues?.Count ?? 0;
            var issueCodes = string.Empty;
            if (issueCount > 0)
            {
                var codeBuilder = new StringBuilder();
                for (int i = 0; i < result.Issues.Count; i++)
                {
                    if (i > 0)
                    {
                        codeBuilder.Append('|');
                    }
                    codeBuilder.Append(result.Issues[i].Code.ToString());
                }
                issueCodes = codeBuilder.ToString();
            }

            var assertionCount = result.AssertionResults?.Count ?? 0;
            var failedAssertions = string.Empty;
            if (assertionCount > 0)
            {
                var builder = new StringBuilder();
                var failures = 0;
                for (int i = 0; i < result.AssertionResults.Count; i++)
                {
                    var assertion = result.AssertionResults[i];
                    if (assertion.Passed)
                    {
                        continue;
                    }

                    if (failures > 0)
                    {
                        builder.Append('|');
                    }

                    builder.Append(assertion.MetricId ?? string.Empty);
                    failures++;
                }

                failedAssertions = builder.ToString();
            }

            return string.Join(",",
                Escape(result.ScenarioId ?? string.Empty),
                result.Seed,
                result.RunTicks,
                result.FinalTick,
                result.TelemetryVersion,
                result.CommandLogCount,
                result.CommandCapacity,
                result.CommandBytes,
                result.SnapshotLogCount,
                result.SnapshotCapacity,
                result.SnapshotBytes,
                result.TotalLogBytes,
                result.FrameTimingBudgetExceeded ? "true" : "false",
                result.FrameTimingWorstMs.ToString("0.###"),
                Escape(result.FrameTimingWorstGroup ?? string.Empty),
                result.RegistryContinuityWarnings,
                result.RegistryContinuityFailures,
                result.EntityCountEntries,
                result.PerformanceBudgetFailed ? "true" : "false",
                Escape(result.PerformanceBudgetMetric ?? string.Empty),
                result.PerformanceBudgetValue.ToString("0.###"),
                result.PerformanceBudgetLimit.ToString("0.###"),
                result.PerformanceBudgetTick,
                Escape(result.ExitPolicy.ToString()),
                Escape(result.HighestSeverity.ToString()),
                issueCount,
                Escape(issueCodes),
                assertionCount,
                Escape(failedAssertions));
        }
    }
}
