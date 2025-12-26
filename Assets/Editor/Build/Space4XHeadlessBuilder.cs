#if UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Scenarios;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Space4X.Headless.Editor
{
    /// <summary>
    /// Builds a Linux headless/server player with a deterministic scene list and bundled scenarios.
    /// </summary>
    public static class Space4XHeadlessBuilder
    {
        private static readonly string[] HeadlessScenes =
        {
            "Assets/Scenes/HeadlessBootstrap.unity",
            "Assets/Scenes/TRI_Space4X_Smoke.unity"
        };

        private const string DefaultOutputFolder = "Builds/Space4X_headless/Linux";
        private const string BuildReportFileName = "Space4X_HeadlessBuildReport.log";
        private const string BuildFailureFileName = "Space4X_HeadlessBuildFailure.log";
        private const string EditorLogSnapshotFileName = "Space4X_HeadlessEditor.log";
        private const int EditorLogSnapshotBytes = 2 * 1024 * 1024;

        [MenuItem("Space4X/Build/Headless/Linux Server")]
        public static void BuildFromMenu() => BuildLinuxHeadless();

        /// <summary>
        /// Creates a StandaloneLinux64 server build with bundled scenarios.
        /// Can be invoked via -executeMethod Space4X.Headless.Editor.Space4XHeadlessBuilder.BuildLinuxHeadless
        /// </summary>
        public static void BuildLinuxHeadless() => BuildLinuxHeadless(DefaultOutputFolder);

        public static void BuildLinuxHeadless(string outputDirectory)
        {
            RunHeadlessPreflight();
            EnsureLinuxServerSupport();

            var absoluteOutput = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(absoluteOutput);

            using var targetScope = new BuildTargetScope(BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone);
            using var buildSettingsSceneScope = new BuildSettingsSceneScope(HeadlessScenes);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = HeadlessScenes,
                locationPathName = Path.Combine(absoluteOutput, "Space4X_Headless.x86_64"),
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.StrictMode
            };

            BuildReport? report = null;
            string editorLogSnapshotPath = string.Empty;
            try
            {
                report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                var reportPath = PersistBuildReport(report, absoluteOutput);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException($"Space4X headless build failed: {report.summary.result}. See {reportPath} for details.");
                }

                CopyScenarioContent(GetPlayerDataFolderPath(buildPlayerOptions.locationPathName));
                UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] Headless Linux build created at {absoluteOutput} (report: {reportPath})");
            }
            catch (Exception ex)
            {
                var failureLog = WriteFailureLog(absoluteOutput, ex, report);
                UnityEngine.Debug.LogError($"[Space4XHeadlessBuilder] Build failed. Details written to {failureLog}");
                throw;
            }
            finally
            {
                editorLogSnapshotPath = CaptureEditorLogSnapshot(absoluteOutput);
            }

            if (!string.IsNullOrEmpty(editorLogSnapshotPath))
            {
                UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] Editor log snapshot: {editorLogSnapshotPath}");
            }
        }

        private sealed class BuildSettingsSceneScope : IDisposable
        {
            private readonly EditorBuildSettingsScene[] _previousScenes;
            private bool _restored;

            public BuildSettingsSceneScope(string[] scenePaths)
            {
                _previousScenes = EditorBuildSettings.scenes;
                EditorBuildSettings.scenes = BuildSceneList(scenePaths);
            }

            public void Dispose()
            {
                if (_restored)
                {
                    return;
                }

                EditorBuildSettings.scenes = _previousScenes;
                _restored = true;
            }

            private static EditorBuildSettingsScene[] BuildSceneList(string[] scenePaths)
            {
                if (scenePaths == null || scenePaths.Length == 0)
                {
                    return Array.Empty<EditorBuildSettingsScene>();
                }

                var scenes = new List<EditorBuildSettingsScene>(scenePaths.Length);
                foreach (var scenePath in scenePaths)
                {
                    if (string.IsNullOrWhiteSpace(scenePath))
                    {
                        continue;
                    }

                    var absoluteScene = Path.GetFullPath(Path.Combine(ProjectRoot, scenePath));
                    if (!File.Exists(absoluteScene))
                    {
                        throw new BuildFailedException($"Headless build scene not found: {scenePath}");
                    }

                    scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                }

                return scenes.ToArray();
            }
        }

        private static void CopyScenarioContent(string playerDataFolder)
        {
            Directory.CreateDirectory(playerDataFolder);
            var destinationRoot = Path.Combine(playerDataFolder, "Scenarios");
            if (Directory.Exists(destinationRoot))
            {
                Directory.Delete(destinationRoot, true);
            }
            Directory.CreateDirectory(destinationRoot);

            foreach (var (label, sourcePath) in EnumerateScenarioSources())
            {
                if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
                {
                    UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Scenario source missing: {sourcePath}");
                    continue;
                }

                var destination = Path.Combine(destinationRoot, label);
                CopyScenarioDirectory(sourcePath, destination);
            }
        }

        private static void CopyScenarioDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*.json", SearchOption.AllDirectories))
            {
                var relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destinationPath = Path.Combine(destinationDir, relative);
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Copy(file, destinationPath, true);
            }
        }

        private static IEnumerable<(string label, string path)> EnumerateScenarioSources()
        {
            yield return ("space4x", Path.Combine(Application.dataPath, "Scenarios"));

            var samplesRoot = TryGetPureDotsSamplesDirectory();
            if (!string.IsNullOrEmpty(samplesRoot))
            {
                yield return ("puredots_samples", samplesRoot);
            }
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static void EnsureLinuxServerSupport()
        {
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64) &&
                HasLinuxServerModule())
            {
                return;
            }

            throw new BuildFailedException(
                "Linux Dedicated Server module is not installed. Install \"Linux Build Support\" (Server) via Unity Hub to build headless players.");
        }

        private static bool HasLinuxServerModule()
        {
            var linuxSupportPath = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "LinuxStandaloneSupport");
            if (!Directory.Exists(linuxSupportPath))
            {
                return false;
            }

            var variationsRoot = Path.Combine(linuxSupportPath, "Variations");
            if (!Directory.Exists(variationsRoot))
            {
                return false;
            }

            foreach (var directory in Directory.GetDirectories(variationsRoot))
            {
                if (directory.IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RunHeadlessPreflight()
        {
            EnsureResourceTypeCatalogAsset();
            ValidateResourceAssets();
        }

        private static void EnsureResourceTypeCatalogAsset()
        {
            const string catalogAssetPath = "Assets/Space4x/Config/PureDotsResourceTypes.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ResourceTypeCatalog>(catalogAssetPath);
            if (catalog != null)
            {
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(catalogAssetPath) != null || File.Exists(Path.Combine(ProjectRoot, catalogAssetPath)))
            {
                AssetDatabase.DeleteAsset(catalogAssetPath);
            }

            var fullDirectory = Path.Combine(ProjectRoot, Path.GetDirectoryName(catalogAssetPath) ?? string.Empty);
            Directory.CreateDirectory(fullDirectory);

            catalog = ScriptableObject.CreateInstance<ResourceTypeCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Recreated missing/invalid ResourceTypeCatalog at {catalogAssetPath}");
        }

        private static void ValidateResourceAssets()
        {
            foreach (var resourcePath in EnumerateResourceAssetPaths())
            {
                if (!resourcePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryHandleLegacyResourceAsset(resourcePath))
                {
                    continue;
                }

                if (IsClientOnlyBinding(resourcePath))
                {
                    UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Skipping client-only Resources asset for headless build: {resourcePath}");
                    continue;
                }

                if (Path.GetExtension(resourcePath).Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    EnsurePrefabHasNoMissingScripts(resourcePath);
                    continue;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(resourcePath);
                if (asset == null)
                {
                    throw new BuildFailedException($"Asset in Resources references type compiled out by define constraints: {resourcePath}");
                }
            }
        }

        private static void EnsurePrefabHasNoMissingScripts(string prefabPath)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefabRoot) > 0)
                {
                    throw new BuildFailedException($"Asset in Resources references type compiled out by define constraints: {prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static IEnumerable<string> EnumerateResourceAssetPaths()
        {
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                yield return path;
            }
        }

        private static bool TryHandleLegacyResourceAsset(string assetPath)
        {
            if (!IsLegacyResourcePath(assetPath))
            {
                return false;
            }

            UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Removing legacy Resources asset before headless build: {assetPath}");
            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                throw new BuildFailedException($"Unable to remove legacy Resources asset: {assetPath}");
            }

            AssetDatabase.Refresh();
            return true;
        }

        private static bool IsLegacyResourcePath(string assetPath)
        {
            return assetPath.IndexOf("/_Archive/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   assetPath.IndexOf("/Legacy/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsClientOnlyBinding(string assetPath)
        {
            return assetPath.IndexOf("Space4X/Bindings/Space4XPresentationBinding", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string TryGetPureDotsSamplesDirectory()
        {
            var packageInfo = PackageManagerPackageInfo.FindForAssembly(typeof(ScenarioRunnerExecutor).Assembly);
            if (packageInfo == null)
            {
                UnityEngine.Debug.LogWarning("[Space4XHeadlessBuilder] Unable to locate PureDOTS package; skipping scenario samples.");
                return string.Empty;
            }

            return Path.Combine(packageInfo.resolvedPath, "Runtime", "Runtime", "Scenarios", "Samples");
        }

        private static string GetPlayerDataFolderPath(string executablePath)
        {
            var playerDirectory = Path.GetDirectoryName(executablePath) ?? throw new InvalidOperationException("Invalid headless build path.");
            var playerName = Path.GetFileNameWithoutExtension(executablePath);
            return Path.Combine(playerDirectory, $"{playerName}_Data");
        }

        private static string PersistBuildReport(BuildReport? report, string outputDirectory)
        {
            if (report == null)
            {
                return string.Empty;
            }

            var path = Path.Combine(outputDirectory, BuildReportFileName);
            var sb = new StringBuilder();
            var summary = report.summary;

            sb.AppendLine($"Space4X Headless Build Report ({DateTime.Now:O})");
            sb.AppendLine(new string('-', 72));
            sb.AppendLine($"Result:        {summary.result}");
            sb.AppendLine($"Output:        {summary.outputPath}");
            sb.AppendLine($"Errors:        {summary.totalErrors}");
            sb.AppendLine($"Warnings:      {summary.totalWarnings}");
            sb.AppendLine($"Total Size:    {summary.totalSize} bytes");
            sb.AppendLine($"Duration:      {summary.totalTime}");
            sb.AppendLine();

            foreach (var step in report.steps)
            {
                sb.AppendLine($"Step: {step.name} ({step.duration.TotalSeconds:F1}s)");
                foreach (var message in step.messages)
                {
                    sb.AppendLine($"    [{message.type}] {message.content}");
                }

                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private static string WriteFailureLog(string outputDirectory, Exception exception, BuildReport? report)
        {
            var path = Path.Combine(outputDirectory, BuildFailureFileName);
            var sb = new StringBuilder();
            sb.AppendLine($"Space4X Headless Build Failure ({DateTime.Now:O})");
            sb.AppendLine(exception.ToString());

            if (report != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Build result: {report.summary.result}");
                sb.AppendLine($"Total errors: {report.summary.totalErrors}");
                sb.AppendLine($"Total warnings: {report.summary.totalWarnings}");
            }

            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private static string CaptureEditorLogSnapshot(string outputDirectory)
        {
            var logPath = GetEditorLogPath();
            if (string.IsNullOrEmpty(logPath))
            {
                return string.Empty;
            }

            try
            {
                var snapshotPath = Path.Combine(outputDirectory, EditorLogSnapshotFileName);
                using var source = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (source.Length > EditorLogSnapshotBytes)
                {
                    source.Seek(-EditorLogSnapshotBytes, SeekOrigin.End);
                }

                using var destination = new FileStream(snapshotPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                source.CopyTo(destination);
                return snapshotPath;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Failed to capture editor log snapshot: {ex.Message}");
                return string.Empty;
            }
        }

        private static string GetEditorLogPath()
        {
            var path = Application.consoleLogPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return path;
            }

            try
            {
                var fallback = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Unity", "Editor", "Editor.log");
                return File.Exists(fallback) ? fallback : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private readonly struct BuildTargetScope : IDisposable
        {
            private readonly BuildTarget _originalTarget;
            private readonly BuildTargetGroup _originalGroup;
            private readonly bool _shouldRevert;

            public BuildTargetScope(BuildTarget target, BuildTargetGroup group)
            {
                _originalTarget = EditorUserBuildSettings.activeBuildTarget;
                _originalGroup = BuildPipeline.GetBuildTargetGroup(_originalTarget);

                if (_originalTarget == target)
                {
                    _shouldRevert = false;
                    return;
                }

                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                {
                    throw new BuildFailedException($"Unable to switch active build target to {target}. Check Build Settings and try again.");
                }

                _shouldRevert = true;
            }

            public void Dispose()
            {
                if (_shouldRevert)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(_originalGroup, _originalTarget);
                }
            }
        }
    }
}
#nullable disable
#endif
