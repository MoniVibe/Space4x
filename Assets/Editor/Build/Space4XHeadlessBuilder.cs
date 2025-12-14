#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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

        [MenuItem("Space4X/Build/Headless/Linux Server")]
        public static void BuildFromMenu() => BuildLinuxHeadless();

        /// <summary>
        /// Creates a StandaloneLinux64 server build with bundled scenarios.
        /// Can be invoked via -executeMethod Space4X.Headless.Editor.Space4XHeadlessBuilder.BuildLinuxHeadless
        /// </summary>
        public static void BuildLinuxHeadless(string outputDirectory = DefaultOutputFolder)
        {
            var absoluteOutput = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(absoluteOutput);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = HeadlessScenes,
                locationPathName = Path.Combine(absoluteOutput, "Space4X_Headless.x86_64"),
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.StrictMode
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Space4X headless build failed: {report.summary.result}");
            }

            CopyScenarioContent(absoluteOutput);
            UnityEngine.Debug.Log($"[Space4XHeadlessBuilder] Headless Linux build created at {absoluteOutput}");
        }

        private static void CopyScenarioContent(string buildRoot)
        {
            var destinationRoot = Path.Combine(buildRoot, "Scenarios");
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
                CopyDirectory(sourcePath, destination);
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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
            yield return ("puredots_samples", Path.Combine(ProjectRoot, "PureDOTS", "Packages", "com.moni.puredots", "Runtime", "Runtime", "Scenarios", "Samples"));
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }
}
#endif
