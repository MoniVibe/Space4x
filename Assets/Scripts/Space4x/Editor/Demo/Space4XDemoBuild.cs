using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor.Demo
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Build pipeline for demo executables.
    /// Sets scripting symbols, runs preflight, builds executable, and creates package.
    /// </summary>
    public static class Space4XDemoBuild
    {
        public static class Demos
        {
            public static class Build
            {
                [MenuItem("Space4X/Demo/Build Space4X Demo")]
                public static void Run()
                {
                    Run("Space4X", null, "Minimal");
                }

                public static void Run(string game, string scenario, string bindings)
                {
                    UnityDebug.Log($"[Demo Build] Starting build for {game}...");

                    // Parse command line args if provided
                    string[] args = System.Environment.GetCommandLineArgs();
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i] == "--game" && i + 1 < args.Length)
                        {
                            game = args[i + 1];
                        }
                        else if (args[i] == "--scenario" && i + 1 < args.Length)
                        {
                            scenario = args[i + 1];
                        }
                        else if (args[i] == "--bindings" && i + 1 < args.Length)
                        {
                            bindings = args[i + 1];
                        }
                    }

                    // Step 1: Set scripting symbols
                    SetScriptingSymbols(game);

                    // Step 2: Run preflight validation
                    UnityDebug.Log("[Demo Build] Running preflight validation...");
                    Space4XDemoPreflight.Demos.Preflight.Run(game);

                    // Step 3: Build executable
                    UnityDebug.Log("[Demo Build] Building executable...");
                    string buildPath = BuildExecutable(game);

                    // Step 4: Copy scenarios and bindings
                    UnityDebug.Log("[Demo Build] Copying scenarios and bindings...");
                    CopyScenariosAndBindings(buildPath, game);

                    // Step 5: Create package zip
                    UnityDebug.Log("[Demo Build] Creating package...");
                    CreatePackage(buildPath, game);

                    UnityDebug.Log($"[Demo Build] Build complete! Output: {buildPath}");
                }

                private static void SetScriptingSymbols(string game)
                {
                    string define = game == "Space4X" ? "SPACE4X_SCENARIO" : "GODGAME_SCENARIO";
                    var buildTarget = EditorUserBuildSettings.activeBuildTarget;
                    var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
                    var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
                    string defines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);

                    if (!defines.Contains(define))
                    {
                        defines = string.IsNullOrEmpty(defines) ? define : defines + ";" + define;
                        PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
                        UnityDebug.Log($"[Demo Build] Set scripting symbol: {define}");
                    }
                }

                private static string BuildExecutable(string game)
                {
                    string date = DateTime.Now.ToString("yyyyMMdd");
                    string exeName = $"{game}_Demo_{date}.exe";
                    string buildPath = Path.Combine(Application.dataPath, "..", "Build", exeName);

                    BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
                    {
                        scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray(),
                        locationPathName = buildPath,
                        target = EditorUserBuildSettings.activeBuildTarget,
                        options = BuildOptions.None
                    };

                    BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                    BuildSummary summary = report.summary;

                    if (summary.result == BuildResult.Succeeded)
                    {
                        UnityDebug.Log($"[Demo Build] Build succeeded: {summary.totalSize} bytes");
                        return buildPath;
                    }
                    else
                    {
                        UnityDebug.LogError($"[Demo Build] Build failed: {summary.result}");
                        throw new Exception($"Build failed: {summary.result}");
                    }
                }

                private static void CopyScenariosAndBindings(string buildPath, string game)
                {
                    string buildDir = Path.GetDirectoryName(buildPath);
                    string dataDir = Path.Combine(buildDir, $"{game}_Demo_{DateTime.Now:yyyyMMdd}_Data");

                    // Create directories
                    string scenariosDir = Path.Combine(dataDir, "Scenarios");
                    string bindingsDir = Path.Combine(dataDir, "Bindings");
                    Directory.CreateDirectory(scenariosDir);
                    Directory.CreateDirectory(bindingsDir);

                    // Copy scenarios
                    string sourceScenariosDir = Path.Combine(Application.dataPath, "Scenarios");
                    if (Directory.Exists(sourceScenariosDir))
                    {
                        var jsonFiles = Directory.GetFiles(sourceScenariosDir, "*.json", SearchOption.TopDirectoryOnly);
                        foreach (var file in jsonFiles)
                        {
                            string dest = Path.Combine(scenariosDir, Path.GetFileName(file));
                            File.Copy(file, dest, true);
                        }
                        UnityDebug.Log($"[Demo Build] Copied {jsonFiles.Length} scenario files");
                    }

                    // Copy bindings
                    string sourceBindingsDir = Path.Combine(Application.dataPath, "Space4X", "Bindings");
                    if (Directory.Exists(sourceBindingsDir))
                    {
                        var assetFiles = Directory.GetFiles(sourceBindingsDir, "*.asset", SearchOption.TopDirectoryOnly);
                        foreach (var file in assetFiles)
                        {
                            string dest = Path.Combine(bindingsDir, Path.GetFileName(file));
                            File.Copy(file, dest, true);
                        }
                        UnityDebug.Log($"[Demo Build] Copied {assetFiles.Length} binding files");
                    }
                }

                private static void CreatePackage(string buildPath, string game)
                {
                    string buildDir = Path.GetDirectoryName(buildPath);
                    string date = DateTime.Now.ToString("yyyyMMdd");
                    string zipName = $"{game}_Demo_{date}.zip";
                    string zipPath = Path.Combine(buildDir, zipName);

                    // TODO: Use System.IO.Compression.ZipFile or external tool
                    // For now, just log the intended package structure
                    UnityDebug.Log($"[Demo Build] Package would be created at: {zipPath}");
                    UnityDebug.Log("[Demo Build] Package structure:");
                    UnityDebug.Log($"  - {Path.GetFileName(buildPath)}");
                    UnityDebug.Log($"  - {game}_Demo_{date}_Data/");
                    UnityDebug.Log($"    - Scenarios/");
                    UnityDebug.Log($"    - Bindings/");
                    UnityDebug.Log($"    - Reports/");
                }
            }
        }
    }
}

