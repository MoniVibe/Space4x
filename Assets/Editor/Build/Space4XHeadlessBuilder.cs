#if UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Scenarios;
using System.Reflection;
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
            "Assets/Scenes/HeadlessBootstrap.unity"
        };

        private const string DefaultOutputFolder = "Builds/Space4X_headless/Linux";
        private const string BuildReportFileName = "Space4X_HeadlessBuildReport.log";
        private const string BuildFailureFileName = "Space4X_HeadlessBuildFailure.log";
        private const string EditorLogSnapshotFileName = "Space4X_HeadlessEditor.log";
        private const string MissingScriptsFileName = "Space4X_HeadlessMissingScripts.log";
        private const int EditorLogSnapshotBytes = 2 * 1024 * 1024;
        private static string s_PreflightLogDirectory = string.Empty;

        [MenuItem("Space4X/Build/Headless/Linux Server")]
        public static void BuildFromMenu() => BuildLinuxHeadless();

        /// <summary>
        /// Creates a StandaloneLinux64 server build with bundled scenarios.
        /// Can be invoked via -executeMethod Space4X.Headless.Editor.Space4XHeadlessBuilder.BuildLinuxHeadless
        /// </summary>
        public static BuildReport BuildLinuxHeadless() => BuildLinuxHeadless(DefaultOutputFolder);

        public static BuildReport BuildLinuxHeadless(string outputDirectory)
        {
            var absoluteOutput = ResolveOutputDirectory(outputDirectory);
            UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] START BUILD {DateTime.UtcNow:O}");
            UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] Output path: {absoluteOutput}");
            UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] Working directory: {Directory.GetCurrentDirectory()}");

            BuildReport? report = null;
            string editorLogSnapshotPath = string.Empty;
            string reportPath = string.Empty;
            try
            {
                Directory.CreateDirectory(absoluteOutput);
                using var renderPipelineScope = new RenderPipelineScope();
                using var defineScope = new ScriptingDefineScope(BuildTargetGroup.Standalone, "HYBRID_RENDERER_DISABLED");
                RunHeadlessPreflight(absoluteOutput);
                EnsureLinuxServerSupport();

                using var targetScope = new BuildTargetScope(BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone);
                using var buildSettingsSceneScope = new BuildSettingsSceneScope(HeadlessScenes);

                var buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = HeadlessScenes,
                    locationPathName = Path.Combine(absoluteOutput, "Space4X_Headless.x86_64"),
                    target = BuildTarget.StandaloneLinux64,
                    targetGroup = BuildTargetGroup.Standalone,
                    subtarget = (int)StandaloneBuildSubtarget.Server,
                    options = BuildOptions.EnableHeadlessMode | BuildOptions.DetailedBuildReport
                };

                report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                LogBuildSummary(report);
                reportPath = PersistBuildReport(report, absoluteOutput);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException($"Space4X headless build failed: {report.summary.result}. See {reportPath} for details.");
                }

                var playerDataFolder = GetPlayerDataFolderPath(buildPlayerOptions.locationPathName);
                CopyScenarioContent(playerDataFolder);
                EnsureOptionalManagedAssemblies(playerDataFolder);
                UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] Headless Linux build created at {absoluteOutput} (report: {reportPath})");
            }
            catch (Exception ex)
            {
                var failureLog = TryWriteFailureLog(absoluteOutput, ex, report);
                if (!string.IsNullOrEmpty(failureLog))
                {
                    UnityEngine.Debug.LogError($"[Space4XHeadlessBuilder] Build failed. Details written to {failureLog}");
                }
                throw;
            }
            finally
            {
                editorLogSnapshotPath = CaptureEditorLogSnapshot(absoluteOutput);
                UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] END BUILD {DateTime.UtcNow:O}");
            }

            if (!string.IsNullOrEmpty(editorLogSnapshotPath))
            {
                UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] Editor log snapshot: {editorLogSnapshotPath}");
            }

            if (report == null)
            {
                throw new BuildFailedException("Space4X headless build did not produce a BuildReport.");
            }

            return report;
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

        private static string ResolveOutputDirectory(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = DefaultOutputFolder;
            }

            if (Path.IsPathRooted(outputDirectory))
            {
                return Path.GetFullPath(outputDirectory);
            }

            return Path.GetFullPath(Path.Combine(ProjectRoot, outputDirectory));
        }

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

        private static void RunHeadlessPreflight(string outputDirectory)
        {
            s_PreflightLogDirectory = outputDirectory;
            try
            {
                DisableEntitiesGraphicsForHeadless();
                EnsureResourceTypeCatalogAsset();
                ValidateResourceAssets();
                ScanForMissingScripts();
                ScanForPPtrCastIssues();
            }
            finally
            {
                s_PreflightLogDirectory = string.Empty;
            }
        }

        private static void DisableEntitiesGraphicsForHeadless()
        {
            if (!InternalEditorUtility.inBatchMode)
            {
                return;
            }

            var rootsType = Type.GetType("Unity.Entities.UnityObjectRefUtility, Unity.Entities");
            if (rootsType != null)
            {
                var rootsField = rootsType.GetField("s_AdditionalRootsHandlerDelegates", BindingFlags.NonPublic | BindingFlags.Static);
                if (rootsField != null)
                {
                    var handlers = rootsField.GetValue(null) as IList;
                    if (handlers != null)
                    {
                        for (var i = handlers.Count - 1; i >= 0; i--)
                        {
                            if (handlers[i] is Delegate handler &&
                                handler.Method?.DeclaringType?.FullName == "Unity.Rendering.EntitiesGraphicsSystemUtility")
                            {
                                handlers.RemoveAt(i);
                            }
                        }
                    }
                }
            }

            if (World.DefaultGameObjectInjectionWorld == null)
            {
                return;
            }

            var field = typeof(Unity.Rendering.EntitiesGraphicsSystem)
                .GetField("m_RegisteredAssets", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var world in World.All)
            {
                var system = world.GetExistingSystemManaged<Unity.Rendering.EntitiesGraphicsSystem>();
                if (system != null)
                {
                    system.Enabled = false;
                    if (field != null)
                    {
                        var value = (NativeHashSet<int>)field.GetValue(system);
                        if (!value.IsCreated)
                        {
                            value = new NativeHashSet<int>(0, Allocator.Persistent);
                            field.SetValue(system, value);
                        }
                    }
                }
            }

            UnityEngine.Debug.Log("[Space4XHeadlessBuilder] Disabled Entities Graphics systems for batchmode build.");
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

        private static void ScanForMissingScripts()
        {
            var missing = new List<string>();
            var critical = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path) || IsSkippablePath(path))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) > 0)
                {
                    var entry = $"{path} (prefab_missing)";
                    missing.Add(entry);
                    if (IsCriticalPath(path))
                    {
                        critical.Add(entry);
                    }
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path) || IsSkippablePath(path))
                {
                    continue;
                }

                var scriptable = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (scriptable == null)
                {
                    var entry = $"{path} (scriptable_missing)";
                    missing.Add(entry);
                    if (IsCriticalPath(path))
                    {
                        critical.Add(entry);
                    }
                }
            }

            foreach (var scenePath in HeadlessScenes)
            {
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                try
                {
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    var roots = scene.GetRootGameObjects();
                    var sceneMissing = 0;
                    foreach (var root in roots)
                    {
                        sceneMissing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
                    }

                    if (sceneMissing > 0)
                    {
                        var entry = $"{scenePath} (scene_missing={sceneMissing})";
                        missing.Add(entry);
                        critical.Add(entry);
                    }

                    EditorSceneManager.CloseScene(scene, true);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Failed to scan scene {scenePath}: {ex.Message}");
                }
            }

            if (missing.Count == 0)
            {
                return;
            }

            UnityEngine.Debug.LogError($"[Space4XHeadlessBuilder] Missing scripts detected ({missing.Count}).");
            for (var i = 0; i < missing.Count; i++)
            {
                UnityEngine.Debug.LogError($"[Space4XHeadlessBuilder] Missing script asset: {missing[i]}");
            }

            string logPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(s_PreflightLogDirectory))
            {
                try
                {
                    logPath = Path.Combine(s_PreflightLogDirectory, MissingScriptsFileName);
                    File.WriteAllLines(logPath, missing);
                    UnityEngine.Debug.LogError($"[Space4XHeadlessBuilder] Missing script list written to {logPath}");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Failed to write missing script list: {ex.Message}");
                }
            }

            var previewCount = Math.Min(20, missing.Count);
            var preview = string.Join("; ", missing.GetRange(0, previewCount));
            var strict = ShouldFailOnMissingScripts(critical.Count > 0);
            if (!strict)
            {
                UnityEngine.Debug.LogWarning("[Space4XHeadlessBuilder] Missing scripts detected, but strict mode is off. Build will continue.");
                return;
            }

            if (!string.IsNullOrEmpty(logPath))
            {
                throw new BuildFailedException($"Missing scripts detected ({missing.Count}). See {logPath} for asset paths. Preview: {preview}");
            }

            throw new BuildFailedException($"Missing scripts detected ({missing.Count}). Preview: {preview}");
        }

        private sealed class RenderPipelineScope : IDisposable
        {
            private readonly RenderPipelineAsset _defaultPipeline;
            private readonly RenderPipelineAsset _qualityPipeline;
            private readonly bool _changed;

            public RenderPipelineScope()
            {
                if (!InternalEditorUtility.inBatchMode)
                {
                    _changed = false;
                    return;
                }

                _defaultPipeline = GraphicsSettings.defaultRenderPipeline;
                _qualityPipeline = QualitySettings.renderPipeline;
                if (_defaultPipeline == null && _qualityPipeline == null)
                {
                    _changed = false;
                    return;
                }

                GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                _changed = true;
                UnityEngine.Debug.Log("[Space4XHeadlessBuilder] Cleared render pipeline assets for batchmode build.");
            }

            public void Dispose()
            {
                if (!_changed)
                {
                    return;
                }

                GraphicsSettings.defaultRenderPipeline = _defaultPipeline;
                QualitySettings.renderPipeline = _qualityPipeline;
            }
        }

        private sealed class ScriptingDefineScope : IDisposable
        {
            private readonly BuildTargetGroup _group;
            private readonly string _previous;
            private readonly bool _changed;

            public ScriptingDefineScope(BuildTargetGroup group, string define)
            {
                _group = group;
                _previous = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                if (HasSymbol(_previous, define))
                {
                    _changed = false;
                    return;
                }

                var updated = string.IsNullOrWhiteSpace(_previous) ? define : $"{_previous};{define}";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, updated);
                _changed = true;
                WaitForCompilation("apply headless define");
            }

            public void Dispose()
            {
                if (!_changed)
                {
                    return;
                }

                PlayerSettings.SetScriptingDefineSymbolsForGroup(_group, _previous);
                WaitForCompilation("restore headless defines");
            }

            private static bool HasSymbol(string defines, string symbol)
            {
                if (string.IsNullOrWhiteSpace(defines))
                {
                    return false;
                }

                var parts = defines.Split(';');
                for (var i = 0; i < parts.Length; i++)
                {
                    if (string.Equals(parts[i].Trim(), symbol, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static void WaitForCompilation(string reason)
            {
                CompilationPipeline.RequestScriptCompilation();
                var start = DateTime.UtcNow;
                while (EditorApplication.isCompiling)
                {
                    if ((DateTime.UtcNow - start) > TimeSpan.FromMinutes(5))
                    {
                        UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Timed out waiting for script compilation ({reason}).");
                        break;
                    }

                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        private static void ScanForPPtrCastIssues()
        {
            var flag = System.Environment.GetEnvironmentVariable("SPACE4X_HEADLESS_PPTR_SCAN");
            if (!string.IsNullOrWhiteSpace(flag) &&
                (flag.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                 flag.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                 flag.Equals("no", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var outputDir = s_PreflightLogDirectory;
            string? logPath = null;
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Path.Combine(outputDir, "build");
                logPath = Path.Combine(outputDir, "Space4X_HeadlessPPtrCastScan.log");
            }

            var issues = Space4X.Editor.Diagnostics.Space4XPPtrCastScanner.RunHeadlessScan(outputDir);
            if (issues > 0)
            {
                if (!string.IsNullOrWhiteSpace(logPath))
                {
                    UnityEngine.Debug.LogError($"[Space4XHeadlessBuilder] PPtr cast scan log: {logPath}");
                }
                throw new BuildFailedException($"PPtr cast issues detected ({issues}). See {logPath ?? "Space4X_HeadlessPPtrCastScan.log"} for asset paths.");
            }
        }

        private static bool IsSkippablePath(string path)
        {
            return path.IndexOf("/Samples/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCriticalPath(string path)
        {
            return path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldFailOnMissingScripts(bool hasCritical)
        {
            var flag = System.Environment.GetEnvironmentVariable("SPACE4X_HEADLESS_STRICT_MISSING_SCRIPTS");
            if (!string.IsNullOrWhiteSpace(flag))
            {
                return flag.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                       flag.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       flag.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }

            return hasCritical;
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

        private static void EnsureOptionalManagedAssemblies(string playerDataFolder)
        {
            if (string.IsNullOrWhiteSpace(playerDataFolder))
            {
                return;
            }

            var managedDir = Path.Combine(playerDataFolder, "Managed");
            if (!Directory.Exists(managedDir))
            {
                return;
            }

            EnsureOptionalManagedAssembly(managedDir, "glTFast.Documentation.Examples.dll");
        }

        private static void EnsureOptionalManagedAssembly(string managedDir, string assemblyName)
        {
            var destination = Path.Combine(managedDir, assemblyName);
            if (File.Exists(destination))
            {
                return;
            }

            var libraryRoot = Path.Combine(ProjectRoot, "Library");
            var candidates = new[]
            {
                Path.Combine(libraryRoot, "ScriptAssemblies", assemblyName),
                Path.Combine(libraryRoot, "Bee", "PlayerScriptAssemblies", assemblyName)
            };

            string sourcePath = string.Empty;
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    sourcePath = candidate;
                    break;
                }
            }

            if (string.IsNullOrEmpty(sourcePath) && Directory.Exists(Path.Combine(libraryRoot, "Bee")))
            {
                var beeMatches = Directory.GetFiles(Path.Combine(libraryRoot, "Bee"), assemblyName, SearchOption.AllDirectories);
                if (beeMatches.Length > 0)
                {
                    sourcePath = beeMatches[0];
                }
            }

            if (string.IsNullOrEmpty(sourcePath))
            {
                UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Optional managed assembly missing: {assemblyName} (no source found).");
                return;
            }

            try
            {
                File.Copy(sourcePath, destination, true);
                CopyPdbIfPresent(sourcePath, destination);
                UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Injected optional managed assembly for headless build: {assemblyName}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Space4XHeadlessBuilder] Failed to inject optional managed assembly {assemblyName}: {ex.Message}");
            }
        }

        private static void CopyPdbIfPresent(string sourceDll, string destinationDll)
        {
            var sourcePdb = Path.ChangeExtension(sourceDll, ".pdb");
            if (!File.Exists(sourcePdb))
            {
                return;
            }

            var destinationPdb = Path.ChangeExtension(destinationDll, ".pdb");
            File.Copy(sourcePdb, destinationPdb, true);
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

        private static string TryWriteFailureLog(string outputDirectory, Exception exception, BuildReport? report)
        {
            try
            {
                return WriteFailureLog(outputDirectory, exception, report);
            }
            catch (Exception logEx)
            {
                UnityEngine.Debug.LogError($"[Space4XHeadlessBuilder] Failed to write failure log: {logEx.Message}");
                return string.Empty;
            }
        }

        private static void LogBuildSummary(BuildReport? report)
        {
            if (report == null)
            {
                return;
            }

            var summary = report.summary;
            UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] Build summary: result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} output={summary.outputPath}");
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
