#if UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Space4X.Headless.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

namespace Tri.BuildTools
{
    public static class HeadlessLinuxBuild
    {
        private const string BuildTargetLabel = "StandaloneLinux64-Server";
        private const string LogsFolderName = "logs";
        private const string OutcomeFileName = "build_outcome.json";
        private const string ManifestFileName = "build_manifest.json";
        private const string BuildReportJsonName = "build_report.json";
        private const string BuildReportTextName = "build_report.txt";

        public static void Build()
        {
            var args = BuildArgs.Parse();
            var startUtc = DateTime.UtcNow;
            var outcome = BuildOutcome.Create(args, "Failed", "Build did not start", string.Empty, startUtc);

            EnsureDirectory(args.ArtifactRoot);
            var logsDir = Path.Combine(args.ArtifactRoot, LogsFolderName);
            EnsureDirectory(logsDir);

            var reportJsonPath = Path.Combine(logsDir, BuildReportJsonName);
            var reportTextPath = Path.Combine(logsDir, BuildReportTextName);
            var outcomePath = Path.Combine(logsDir, OutcomeFileName);
            var manifestPath = Path.Combine(args.ArtifactRoot, ManifestFileName);

            try
            {
                ValidateArgs(args);
                ConfigurePlayerSettings();
                EnsureLinuxServerSupport();

                var report = Space4XHeadlessBuilder.BuildLinuxHeadless(args.BuildOut);
                var entrypointPath = ResolveEntrypointPath(report, args.BuildOut);
                var dataPath = GetPlayerDataFolderPath(entrypointPath);

                WriteBuildReport(report, reportJsonPath, reportTextPath);

                var dataPaths = new List<string>();
                if (Directory.Exists(dataPath))
                {
                    dataPaths.Add(MakeRelativePath(args.ArtifactRoot, dataPath));
                }

                var scenarios = DetectScenarioLabels(dataPath);
                var contentHashes = BuildContentHashes(args.ArtifactRoot, entrypointPath, dataPath);

                var manifest = new BuildManifest(
                    args.BuildId,
                    args.Commit,
                    Application.unityVersion,
                    BuildTargetLabel,
                    DateTime.UtcNow,
                    MakeRelativePath(args.ArtifactRoot, entrypointPath),
                    dataPaths,
                    args.DefaultArgs,
                    scenarios,
                    contentHashes,
                    args.Notes);

                var manifestWithoutSelf = manifest.ToJson(includeSelfHash: false);
                var manifestHash = Sha256Hex(manifestWithoutSelf);
                if (!string.IsNullOrEmpty(manifestHash))
                {
                    manifest.ContentHashes[ManifestFileName] = manifestHash;
                }

                File.WriteAllText(manifestPath, manifest.ToJson(includeSelfHash: true), Encoding.ASCII);

                outcome = BuildOutcome.Create(
                    args,
                    "Succeeded",
                    "Build succeeded",
                    MakeRelativePath(args.ArtifactRoot, reportJsonPath),
                    DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[HeadlessLinuxBuild] Build failed: {ex.Message}");
                outcome = BuildOutcome.Create(
                    args,
                    "Failed",
                    ex.Message,
                    MakeRelativePath(args.ArtifactRoot, reportJsonPath),
                    DateTime.UtcNow);

                TryWriteFallbackManifest(args, manifestPath);
                if (!InternalEditorUtility.inBatchMode)
                {
                    throw;
                }
            }
            finally
            {
                TryWriteOutcome(outcome, outcomePath);
                if (InternalEditorUtility.inBatchMode)
                {
                    EditorApplication.Exit(outcome.Result == "Succeeded" ? 0 : 1);
                }
            }
        }

        private static void ValidateArgs(BuildArgs args)
        {
            var failures = new List<string>();
            if (args.MissingArtifactRoot) failures.Add("artifactRoot");
            if (args.MissingBuildId) failures.Add("buildId");
            if (args.MissingCommit) failures.Add("commit");
            if (failures.Count > 0)
            {
                throw new BuildFailedException("Missing required build args: " + string.Join(", ", failures));
            }
        }

        private static void ConfigurePlayerSettings()
        {
            var group = BuildTargetGroup.Standalone;
            PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
        }

        private static void EnsureLinuxServerSupport()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                throw new BuildFailedException("Linux Build Support (Server) is not installed.");
            }

            var linuxSupportPath = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "LinuxStandaloneSupport");
            var variationsRoot = Path.Combine(linuxSupportPath, "Variations");
            if (!Directory.Exists(variationsRoot))
            {
                throw new BuildFailedException("Linux Standalone Support module not found.");
            }

            foreach (var directory in Directory.GetDirectories(variationsRoot))
            {
                if (directory.IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }
            }

            throw new BuildFailedException("Linux Build Support (Server) is not installed.");
        }

        private static void WriteBuildReport(BuildReport report, string jsonPath, string textPath)
        {
            if (report == null)
            {
                File.WriteAllText(jsonPath, "{\"summary\":{\"result\":\"Unknown\",\"message\":\"BuildReport missing\"}}", Encoding.ASCII);
                File.WriteAllText(textPath, "BuildReport missing.", Encoding.ASCII);
                return;
            }

            var summary = report.summary;
            var sbText = new StringBuilder();
            sbText.AppendLine($"Headless Build Report ({DateTime.UtcNow:O})");
            sbText.AppendLine($"Result: {summary.result}");
            sbText.AppendLine($"Output: {summary.outputPath}");
            sbText.AppendLine($"Errors: {summary.totalErrors}");
            sbText.AppendLine($"Warnings: {summary.totalWarnings}");
            sbText.AppendLine($"Total Size: {summary.totalSize} bytes");
            sbText.AppendLine($"Duration: {summary.totalTime}");
            File.WriteAllText(textPath, sbText.ToString(), Encoding.ASCII);

            var sbJson = new StringBuilder();
            sbJson.Append("{\"summary\":{");
            AppendJsonField(sbJson, "result", summary.result.ToString(), prependComma: false, quote: true);
            AppendJsonField(sbJson, "output_path", summary.outputPath ?? string.Empty, prependComma: true, quote: true);
            AppendJsonField(sbJson, "total_errors", summary.totalErrors.ToString(), prependComma: true, quote: false);
            AppendJsonField(sbJson, "total_warnings", summary.totalWarnings.ToString(), prependComma: true, quote: false);
            AppendJsonField(sbJson, "total_size_bytes", summary.totalSize.ToString(), prependComma: true, quote: false);
            AppendJsonField(sbJson, "total_time_seconds", summary.totalTime.TotalSeconds.ToString("F2"), prependComma: true, quote: false);
            sbJson.Append("},");
            sbJson.Append("\"steps\":[");

            var stepIndex = 0;
            foreach (var step in report.steps)
            {
                if (stepIndex > 0)
                {
                    sbJson.Append(",");
                }

                sbJson.Append("{");
                AppendJsonField(sbJson, "name", step.name, prependComma: false, quote: true);
                AppendJsonField(sbJson, "duration_seconds", step.duration.TotalSeconds.ToString("F2"), prependComma: true, quote: false);
                sbJson.Append(",\"messages\":[");

                var messageIndex = 0;
                foreach (var message in step.messages)
                {
                    if (messageIndex > 0)
                    {
                        sbJson.Append(",");
                    }

                    sbJson.Append("{");
                    AppendJsonField(sbJson, "type", message.type.ToString(), prependComma: false, quote: true);
                    AppendJsonField(sbJson, "content", message.content ?? string.Empty, prependComma: true, quote: true);
                    sbJson.Append("}");
                    messageIndex++;
                }

                sbJson.Append("]}");
                stepIndex++;
            }

            sbJson.Append("]}");
            File.WriteAllText(jsonPath, sbJson.ToString(), Encoding.ASCII);
        }

        private static string ResolveEntrypointPath(BuildReport report, string buildOut)
        {
            if (report != null && !string.IsNullOrWhiteSpace(report.summary.outputPath))
            {
                return Path.GetFullPath(report.summary.outputPath);
            }

            var fallbackName = $"{PlayerSettings.productName}_Headless.x86_64";
            return Path.Combine(buildOut, fallbackName);
        }

        private static string GetPlayerDataFolderPath(string executablePath)
        {
            var playerDirectory = Path.GetDirectoryName(executablePath) ?? throw new InvalidOperationException("Invalid headless build path.");
            var playerName = Path.GetFileNameWithoutExtension(executablePath);
            return Path.Combine(playerDirectory, $"{playerName}_Data");
        }

        private static List<string> DetectScenarioLabels(string dataPath)
        {
            var labels = new List<string>();
            var scenarioRoot = Path.Combine(dataPath, "Scenarios");
            if (!Directory.Exists(scenarioRoot))
            {
                return labels;
            }

            foreach (var directory in Directory.GetDirectories(scenarioRoot))
            {
                var name = Path.GetFileName(directory);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                labels.Add(name);
            }

            labels.Sort(StringComparer.OrdinalIgnoreCase);
            return labels;
        }

        private static Dictionary<string, string> BuildContentHashes(string artifactRoot, string entrypointPath, string dataPath)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(entrypointPath))
            {
                hashes[MakeRelativePath(artifactRoot, entrypointPath)] = HashFile(entrypointPath);
            }

            if (Directory.Exists(dataPath))
            {
                hashes[MakeRelativePath(artifactRoot, dataPath)] = HashDirectory(dataPath);
            }

            return hashes;
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(stream));
        }

        private static string HashDirectory(string root)
        {
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            foreach (var file in files)
            {
                var relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var normalized = relative.Replace('\\', '/');
                try
                {
                    sb.Append(normalized);
                    sb.Append(':');
                    sb.Append(HashFile(file));
                    sb.Append('\n');
                }
                catch (FileNotFoundException)
                {
                    if (IsOptionalManagedAssembly(file))
                    {
                        UnityEngine.Debug.LogWarning($"[HeadlessLinuxBuild] Optional managed assembly missing during hash: {normalized}");
                        continue;
                    }

                    throw;
                }
                catch (DirectoryNotFoundException)
                {
                    if (IsOptionalManagedAssembly(file))
                    {
                        UnityEngine.Debug.LogWarning($"[HeadlessLinuxBuild] Optional managed assembly path missing during hash: {normalized}");
                        continue;
                    }

                    throw;
                }
            }

            return Sha256Hex(sb.ToString());
        }

        private static string Sha256Hex(string content)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            return ToHex(sha.ComputeHash(bytes));
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static string MakeRelativePath(string root, string path)
        {
            try
            {
                var relative = Path.GetRelativePath(root, path);
                return relative.Replace('\\', '/');
            }
            catch
            {
                return path.Replace('\\', '/');
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
        }

        private static bool IsOptionalManagedAssembly(string path)
        {
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            return fileName.Equals("glTFast.Documentation.Examples.dll", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("glTFast.Documentation.Examples.pdb", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("System.ComponentModel.Composition.dll", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("System.ComponentModel.Composition.pdb", StringComparison.OrdinalIgnoreCase);
        }

        private static void TryWriteFallbackManifest(BuildArgs args, string manifestPath)
        {
            if (File.Exists(manifestPath))
            {
                return;
            }

            try
            {
                var manifest = new BuildManifest(
                    args.BuildId,
                    args.Commit,
                    Application.unityVersion,
                    BuildTargetLabel,
                    DateTime.UtcNow,
                    string.Empty,
                    new List<string>(),
                    args.DefaultArgs,
                    args.Scenarios,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    args.Notes);

                File.WriteAllText(manifestPath, manifest.ToJson(includeSelfHash: true), Encoding.ASCII);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[HeadlessLinuxBuild] Failed to write fallback manifest: {ex.Message}");
            }
        }

        private static void TryWriteOutcome(BuildOutcome outcome, string outcomePath)
        {
            try
            {
                File.WriteAllText(outcomePath, outcome.ToJson(), Encoding.ASCII);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[HeadlessLinuxBuild] Failed to write build_outcome.json: {ex.Message}");
            }
        }

        private static void AppendJsonField(StringBuilder sb, string name, string value, bool prependComma, bool quote)
        {
            if (prependComma)
            {
                sb.Append(",");
            }

            AppendJsonString(sb, name);
            sb.Append(":");
            if (quote)
            {
                AppendJsonString(sb, value);
            }
            else
            {
                sb.Append(value);
            }
        }

        private static void AppendJsonString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                foreach (var ch in value)
                {
                    switch (ch)
                    {
                        case '\\':
                            sb.Append("\\\\");
                            break;
                        case '"':
                            sb.Append("\\\"");
                            break;
                        case '\n':
                            sb.Append("\\n");
                            break;
                        case '\r':
                            sb.Append("\\r");
                            break;
                        case '\t':
                            sb.Append("\\t");
                            break;
                        default:
                            if (ch < 32)
                            {
                                sb.Append("\\u");
                                sb.Append(((int)ch).ToString("x4"));
                            }
                            else
                            {
                                sb.Append(ch);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        private sealed class BuildManifest
        {
            public BuildManifest(
                string buildId,
                string commit,
                string unityVersion,
                string buildTarget,
                DateTime createdUtc,
                string entrypoint,
                List<string> dataPaths,
                List<string> defaultArgs,
                List<string> scenariosSupported,
                Dictionary<string, string> contentHashes,
                string notes)
            {
                BuildId = buildId;
                Commit = commit;
                UnityVersion = unityVersion;
                BuildTarget = buildTarget;
                CreatedUtc = createdUtc;
                Entrypoint = entrypoint;
                DataPaths = dataPaths;
                DefaultArgs = defaultArgs;
                ScenariosSupported = scenariosSupported;
                ContentHashes = contentHashes;
                Notes = notes;
            }

            public string BuildId { get; }
            public string Commit { get; }
            public string UnityVersion { get; }
            public string BuildTarget { get; }
            public DateTime CreatedUtc { get; }
            public string Entrypoint { get; }
            public List<string> DataPaths { get; }
            public List<string> DefaultArgs { get; }
            public List<string> ScenariosSupported { get; }
            public Dictionary<string, string> ContentHashes { get; }
            public string Notes { get; }

            public string ToJson(bool includeSelfHash)
            {
                var sb = new StringBuilder();
                sb.Append("{");
                AppendJsonField(sb, "build_id", BuildId, prependComma: false, quote: true);
                AppendJsonField(sb, "commit", Commit, prependComma: true, quote: true);
                AppendJsonField(sb, "unity_version", UnityVersion, prependComma: true, quote: true);
                AppendJsonField(sb, "build_target", BuildTarget, prependComma: true, quote: true);
                AppendJsonField(sb, "created_utc", CreatedUtc.ToString("O"), prependComma: true, quote: true);
                AppendJsonField(sb, "entrypoint", Entrypoint, prependComma: true, quote: true);
                sb.Append(",\"data_paths\":");
                AppendJsonArray(sb, DataPaths);
                sb.Append(",\"default_args\":");
                AppendJsonArray(sb, DefaultArgs);
                sb.Append(",\"scenarios_supported\":");
                AppendJsonArray(sb, ScenariosSupported);

                if (ContentHashes.Count > 0)
                {
                    sb.Append(",\"content_hashes\":");
                    AppendJsonObject(sb, ContentHashes, includeSelfHash);
                }

                if (!string.IsNullOrWhiteSpace(Notes))
                {
                    AppendJsonField(sb, "notes", Notes, prependComma: true, quote: true);
                }

                sb.Append("}");
                return sb.ToString();
            }

            private static void AppendJsonArray(StringBuilder sb, List<string> values)
            {
                sb.Append("[");
                for (var i = 0; i < values.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(",");
                    }
                    AppendJsonString(sb, values[i]);
                }
                sb.Append("]");
            }

            private static void AppendJsonObject(StringBuilder sb, Dictionary<string, string> values, bool includeSelfHash)
            {
                sb.Append("{");
                var first = true;
                foreach (var key in GetSortedKeys(values))
                {
                    if (!includeSelfHash && key.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!first)
                    {
                        sb.Append(",");
                    }
                    first = false;
                    AppendJsonString(sb, key);
                    sb.Append(":");
                    AppendJsonString(sb, values[key]);
                }
                sb.Append("}");
            }

            private static List<string> GetSortedKeys(Dictionary<string, string> values)
            {
                var keys = new List<string>(values.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                return keys;
            }
        }

        private readonly struct BuildOutcome
        {
            private BuildOutcome(string buildId, string commit, string result, string message, string reportPath, DateTime utc)
            {
                BuildId = buildId;
                Commit = commit;
                Result = result;
                Message = message;
                ReportPath = reportPath;
                Utc = utc;
            }

            public string BuildId { get; }
            public string Commit { get; }
            public string Result { get; }
            public string Message { get; }
            public string ReportPath { get; }
            public DateTime Utc { get; }

            public static BuildOutcome Create(BuildArgs args, string result, string message, string reportPath, DateTime utc)
            {
                return new BuildOutcome(args.BuildId, args.Commit, result, message, reportPath, utc);
            }

            public string ToJson()
            {
                var sb = new StringBuilder();
                sb.Append("{");
                AppendJsonField(sb, "build_id", BuildId, prependComma: false, quote: true);
                AppendJsonField(sb, "commit", Commit, prependComma: true, quote: true);
                AppendJsonField(sb, "result", Result, prependComma: true, quote: true);
                AppendJsonField(sb, "message", Message, prependComma: true, quote: true);
                AppendJsonField(sb, "report_path", ReportPath, prependComma: true, quote: true);
                AppendJsonField(sb, "utc", Utc.ToString("O"), prependComma: true, quote: true);
                sb.Append("}");
                return sb.ToString();
            }
        }

        private sealed class BuildArgs
        {
            public string BuildId { get; private set; } = "missing";
            public string Commit { get; private set; } = "missing";
            public string ArtifactRoot { get; private set; } = string.Empty;
            public string BuildOut { get; private set; } = string.Empty;
            public List<string> DefaultArgs { get; } = new List<string>();
            public List<string> Scenarios { get; } = new List<string>();
            public string Notes { get; private set; } = string.Empty;
            public bool MissingArtifactRoot { get; private set; }
            public bool MissingBuildId { get; private set; }
            public bool MissingCommit { get; private set; }

            public static BuildArgs Parse()
            {
                var args = new BuildArgs();
                var cmd = Environment.GetCommandLineArgs();

                var missingBuildId = false;
                var missingCommit = false;
                var missingArtifactRoot = false;

                args.BuildId = ReadArg(cmd, "buildId", ref missingBuildId, fallback: "missing");
                args.Commit = ReadArg(cmd, "commit", ref missingCommit, fallback: "missing");
                args.ArtifactRoot = ReadArg(cmd, "artifactRoot", ref missingArtifactRoot, fallback: string.Empty);
                args.MissingBuildId = missingBuildId;
                args.MissingCommit = missingCommit;
                args.MissingArtifactRoot = missingArtifactRoot;
                args.BuildOut = ReadArg(cmd, "buildOut", fallback: string.Empty);
                args.Notes = ReadArg(cmd, "notes", fallback: string.Empty);

                args.DefaultArgs.AddRange(ReadListArg(cmd, "defaultArgs"));
                args.Scenarios.AddRange(ReadListArg(cmd, "scenarios"));

                if (string.IsNullOrWhiteSpace(args.ArtifactRoot))
                {
                    args.ArtifactRoot = Path.Combine(Path.GetTempPath(), "tri_headless_artifacts", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
                    args.MissingArtifactRoot = true;
                }

                args.ArtifactRoot = Path.GetFullPath(args.ArtifactRoot);
                if (string.IsNullOrWhiteSpace(args.BuildOut))
                {
                    args.BuildOut = Path.Combine(args.ArtifactRoot, "build");
                }

                args.BuildOut = Path.GetFullPath(args.BuildOut);
                EnsureDirectory(args.BuildOut);
                return args;
            }

            private static readonly HashSet<string> KnownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "buildId",
                "commit",
                "artifactRoot",
                "buildOut",
                "defaultArgs",
                "scenarios",
                "notes"
            };

            private static string ReadArg(string[] cmd, string key, ref bool missingFlag, string fallback)
            {
                for (var i = 0; i < cmd.Length; i++)
                {
                    if (TryReadInline(cmd[i], key, out var inlineValue))
                    {
                        return inlineValue;
                    }

                    if (!IsKey(cmd[i], key))
                    {
                        continue;
                    }

                    if (i + 1 < cmd.Length)
                    {
                        var next = cmd[i + 1];
                        if (!IsPotentialKey(next) || !KnownKeys.Contains(next.TrimStart('-')))
                        {
                            return next;
                        }
                    }

                    break;
                }

                missingFlag = true;
                return fallback;
            }

            private static string ReadArg(string[] cmd, string key, string fallback)
            {
                var missing = false;
                return ReadArg(cmd, key, ref missing, fallback);
            }

            private static List<string> ReadListArg(string[] cmd, string key)
            {
                var values = new List<string>();
                for (var i = 0; i < cmd.Length; i++)
                {
                    if (TryReadInline(cmd[i], key, out var inlineValue))
                    {
                        values.AddRange(SplitList(inlineValue));
                        continue;
                    }

                    if (!IsKey(cmd[i], key))
                    {
                        continue;
                    }

                    if (i + 1 >= cmd.Length)
                    {
                        continue;
                    }

                    var next = cmd[i + 1];
                    if (IsPotentialKey(next) && KnownKeys.Contains(next.TrimStart('-')))
                    {
                        continue;
                    }

                    values.AddRange(SplitList(next));
                }

                values.RemoveAll(string.IsNullOrWhiteSpace);
                return values;
            }

            private static IEnumerable<string> SplitList(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    yield break;
                }

                var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    yield return raw.Trim();
                    yield break;
                }

                foreach (var part in parts)
                {
                    yield return part.Trim();
                }
            }

            private static bool IsKey(string arg, string key)
            {
                return arg.Equals("-" + key, StringComparison.OrdinalIgnoreCase) ||
                       arg.Equals("--" + key, StringComparison.OrdinalIgnoreCase);
            }

            private static bool TryReadInline(string arg, string key, out string value)
            {
                var prefixLong = "--" + key + "=";
                var prefixShort = "-" + key + "=";
                if (arg.StartsWith(prefixLong, StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(prefixLong.Length);
                    return true;
                }

                if (arg.StartsWith(prefixShort, StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(prefixShort.Length);
                    return true;
                }

                value = string.Empty;
                return false;
            }

            private static bool IsPotentialKey(string arg)
            {
                return arg.StartsWith("-", StringComparison.Ordinal);
            }
        }
    }
}
#nullable disable
#endif
