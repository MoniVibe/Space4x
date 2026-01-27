using System;
using System.Globalization;
using System.IO;
using System.Text;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Telemetry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using SystemEnvironment = System.Environment;

namespace PureDOTS.Runtime.Scenarios
{
    public static class HeadlessInvariantBundleWriter
    {
        private const string TelemetryPathEnv = "PUREDOTS_TELEMETRY_PATH";
        private const string Space4xScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string GodgameScenarioPathEnv = "GODGAME_SCENARIO_PATH";
        private const string Space4xReportPathEnv = "SPACE4X_SCENARIO_REPORT_PATH";

        public static bool TryWriteBundle(
            EntityManager entityManager,
            string code,
            string message,
            uint tick,
            float worldSeconds,
            Entity entity = default,
            bool hasEntity = false,
            float3 position = default,
            bool hasPosition = false,
            float3 velocity = default,
            bool hasVelocity = false,
            quaternion rotation = default,
            bool hasRotation = false)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                return false;
            }

            var telemetryPath = ResolveTelemetryPath(entityManager);
            var scenarioPath = ResolveScenarioPath();
            var reportPath = ResolveReportPath();
            var outputDir = ResolveOutputDirectory(telemetryPath, reportPath, scenarioPath);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                return false;
            }

            Directory.CreateDirectory(outputDir);
            var baseName = ResolveBaseName(telemetryPath);
            var fileName = $"{baseName}_invariant_{SanitizeFileName(code)}_tick{tick}.json";
            var outputPath = Path.Combine(outputDir, fileName);

            var scenarioId = string.Empty;
            var seed = 0u;
            if (TryGetScenarioInfo(entityManager, out var info))
            {
                scenarioId = info.ScenarioId.ToString();
                seed = info.Seed;
            }

            var sb = new StringBuilder(512);
            var first = true;
            sb.Append('{');
            AppendString(ref first, sb, "code", code ?? string.Empty);
            AppendString(ref first, sb, "message", message ?? string.Empty);
            AppendUInt(ref first, sb, "tick", tick);
            AppendFloat(ref first, sb, "worldSeconds", worldSeconds);
            if (!string.IsNullOrEmpty(scenarioId))
            {
                AppendString(ref first, sb, "scenarioId", scenarioId);
                AppendUInt(ref first, sb, "seed", seed);
            }
            if (!string.IsNullOrEmpty(scenarioPath))
            {
                AppendString(ref first, sb, "scenarioPath", scenarioPath);
            }
            if (!string.IsNullOrEmpty(reportPath))
            {
                AppendString(ref first, sb, "reportPath", reportPath);
            }
            if (!string.IsNullOrEmpty(telemetryPath))
            {
                AppendString(ref first, sb, "telemetryPath", telemetryPath);
            }
            if (hasEntity)
            {
                AppendInt(ref first, sb, "entityIndex", entity.Index);
                AppendInt(ref first, sb, "entityVersion", entity.Version);
            }
            if (hasPosition)
            {
                AppendFloat3(ref first, sb, "position", position);
            }
            if (hasVelocity)
            {
                AppendFloat3(ref first, sb, "velocity", velocity);
            }
            if (hasRotation)
            {
                AppendQuaternion(ref first, sb, "rotation", rotation);
            }

            AppendString(ref first, sb, "utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            sb.Append('}');

            try
            {
                File.WriteAllText(outputPath, sb.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetScenarioInfo(EntityManager entityManager, out ScenarioInfo info)
        {
            info = default;
            if (!IsEntityManagerReady(entityManager))
            {
                return false;
            }

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioInfo>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            info = query.GetSingleton<ScenarioInfo>();
            return true;
        }

        private static string ResolveTelemetryPath(EntityManager entityManager)
        {
            if (IsEntityManagerReady(entityManager))
            {
                using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryExportConfig>());
                if (!query.IsEmptyIgnoreFilter)
                {
                    var config = query.GetSingleton<TelemetryExportConfig>();
                    if (config.Enabled != 0 && config.OutputPath.Length > 0)
                    {
                        return config.OutputPath.ToString();
                    }
                }
            }

            return SystemEnvironment.GetEnvironmentVariable(TelemetryPathEnv) ?? string.Empty;
        }

        private static string ResolveScenarioPath()
        {
            var space4x = SystemEnvironment.GetEnvironmentVariable(Space4xScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(space4x))
            {
                return space4x;
            }

            return SystemEnvironment.GetEnvironmentVariable(GodgameScenarioPathEnv) ?? string.Empty;
        }

        private static string ResolveReportPath()
        {
            return SystemEnvironment.GetEnvironmentVariable(Space4xReportPathEnv) ?? string.Empty;
        }

        private static string ResolveOutputDirectory(string telemetryPath, string reportPath, string scenarioPath)
        {
            var directory = GetDirectory(telemetryPath);
            if (!string.IsNullOrEmpty(directory))
            {
                return directory;
            }

            directory = GetDirectory(reportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                return directory;
            }

            directory = GetDirectory(scenarioPath);
            if (!string.IsNullOrEmpty(directory))
            {
                return directory;
            }

            var fallback = Application.persistentDataPath;
            return string.IsNullOrWhiteSpace(fallback) ? "." : Path.Combine(fallback, "headless_invariants");
        }

        private static string ResolveBaseName(string telemetryPath)
        {
            if (!string.IsNullOrWhiteSpace(telemetryPath))
            {
                return Path.GetFileNameWithoutExtension(telemetryPath);
            }

            return "headless";
        }

        private static string GetDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetDirectoryName(path) ?? string.Empty;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "invariant";
            }

            var sanitized = value.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
            foreach (var ch in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(ch, '_');
            }

            return sanitized;
        }

        private static void AppendString(ref bool first, StringBuilder sb, string key, string value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private static void AppendUInt(ref bool first, StringBuilder sb, string key, uint value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":").Append(value);
        }

        private static void AppendInt(ref bool first, StringBuilder sb, string key, int value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":").Append(value);
        }

        private static void AppendFloat(ref bool first, StringBuilder sb, string key, float value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":");
            AppendFloatValue(sb, value);
        }

        private static void AppendFloat3(ref bool first, StringBuilder sb, string key, float3 value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":[");
            AppendFloatValue(sb, value.x);
            sb.Append(',');
            AppendFloatValue(sb, value.y);
            sb.Append(',');
            AppendFloatValue(sb, value.z);
            sb.Append(']');
        }

        private static void AppendQuaternion(ref bool first, StringBuilder sb, string key, quaternion value)
        {
            AppendSeparator(ref first, sb);
            sb.Append('"').Append(key).Append("\":[");
            var v = value.value;
            AppendFloatValue(sb, v.x);
            sb.Append(',');
            AppendFloatValue(sb, v.y);
            sb.Append(',');
            AppendFloatValue(sb, v.z);
            sb.Append(',');
            AppendFloatValue(sb, v.w);
            sb.Append(']');
        }

        private static void AppendSeparator(ref bool first, StringBuilder sb)
        {
            if (!first)
            {
                sb.Append(',');
                return;
            }

            first = false;
        }

        private static void AppendFloatValue(StringBuilder sb, float value)
        {
            if (float.IsNaN(value))
            {
                sb.Append("\"NaN\"");
                return;
            }

            if (float.IsInfinity(value))
            {
                sb.Append(value > 0f ? "\"Inf\"" : "\"-Inf\"");
                return;
            }

            sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool IsEntityManagerReady(EntityManager entityManager)
        {
            var world = entityManager.World;
            return world != null && world.IsCreated && entityManager.WorldUnmanaged.IsCreated;
        }
    }
}
