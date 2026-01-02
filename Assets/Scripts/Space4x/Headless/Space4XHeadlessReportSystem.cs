using System;
using System.IO;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Telemetry.TelemetryExportSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XHeadlessReportSystem : ISystem
    {
        private const string ReportPathEnv = "SPACE4X_SCENARIO_REPORT_PATH";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string TelemetryPathEnv = "PUREDOTS_TELEMETRY_PATH";

        private bool _written;
        private FixedString512Bytes _reportPath;
        private FixedString512Bytes _scenarioPath;
        private FixedString512Bytes _telemetryPath;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            var reportPathValue = SystemEnv.GetEnvironmentVariable(ReportPathEnv);
            if (string.IsNullOrWhiteSpace(reportPathValue))
            {
                state.Enabled = false;
                return;
            }

            var reportPathFull = Path.GetFullPath(reportPathValue);
            if (!TryAssignFixedString(reportPathFull, ref _reportPath))
            {
                state.Enabled = false;
                return;
            }

            var scenarioPathValue = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(scenarioPathValue))
            {
                var scenarioPathFull = Path.GetFullPath(scenarioPathValue);
                TryAssignFixedString(scenarioPathFull, ref _scenarioPath);
            }

            var telemetryPathValue = SystemEnv.GetEnvironmentVariable(TelemetryPathEnv);
            if (!string.IsNullOrWhiteSpace(telemetryPathValue))
            {
                var telemetryPathFull = Path.GetFullPath(telemetryPathValue);
                TryAssignFixedString(telemetryPathFull, ref _telemetryPath);
            }

            state.RequireForUpdate<HeadlessExitRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_written)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out HeadlessExitRequest request))
            {
                return;
            }

            WriteReport(request);
            _written = true;
        }

        private void WriteReport(HeadlessExitRequest request)
        {
            if (_reportPath.IsEmpty)
            {
                return;
            }

            var reportPath = _reportPath.ToString();
            var reportDir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrWhiteSpace(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            var scenarioPath = _scenarioPath.IsEmpty ? string.Empty : _scenarioPath.ToString();
            var telemetryPath = _telemetryPath.IsEmpty ? string.Empty : _telemetryPath.ToString();
            var status = request.ExitCode == 0 ? "PASS" : "FAIL";
            var utc = DateTime.UtcNow.ToString("O");

            var sb = new StringBuilder(256);
            sb.Append("{");
            AppendString(sb, "scenarioPath", scenarioPath); sb.Append(",");
            AppendString(sb, "telemetryPath", telemetryPath); sb.Append(",");
            AppendString(sb, "status", status); sb.Append(",");
            AppendInt(sb, "exitCode", request.ExitCode); sb.Append(",");
            AppendUInt(sb, "tick", request.RequestedTick); sb.Append(",");
            AppendString(sb, "utc", utc);
            sb.Append("}");

            try
            {
                File.WriteAllText(reportPath, sb.ToString());
                UnityDebug.Log($"[Space4XHeadlessReport] Wrote report to '{reportPath}'.");
            }
            catch (Exception ex)
            {
                UnityDebug.LogError($"[Space4XHeadlessReport] Failed to write report '{reportPath}': {ex.Message}");
            }
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append("\"").Append(key).Append("\":\"").Append(Escape(value)).Append("\"");
        }

        private static void AppendInt(StringBuilder sb, string key, int value)
        {
            sb.Append("\"").Append(key).Append("\":").Append(value);
        }

        private static void AppendUInt(StringBuilder sb, string key, uint value)
        {
            sb.Append("\"").Append(key).Append("\":").Append(value);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool TryAssignFixedString(string value, ref FixedString512Bytes target)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                target = default;
                return false;
            }

            if (value.Length > 512)
            {
                target = default;
                return false;
            }

            target = value;
            return true;
        }
    }
}
