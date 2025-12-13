using System.Linq;
using UnityEditor;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// CLI entry point for Prefab Maker that can be called from command line:
    /// Unity -batchmode -projectPath . -executeMethod Space4X.Editor.PrefabMakerCLI.Run --set=Minimal --dryRun
    /// Unity -batchmode -projectPath . -executeMethod Space4X.Editor.PrefabMakerCLI.Run --set=Full
    /// </summary>
    public static class PrefabMakerCLI
    {
        public enum Preset
        {
            Minimal,  // Placeholders only, no overwrite sockets
            Full,     // Full generation with overwrite sockets
            Validate  // Dry-run validation only
        }

        public static void Run()
        {
            var dryRun = false;
            var catalogPath = "Assets/Data/Catalogs";
            var preset = Preset.Minimal;

            // Parse command line arguments
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--dryRun" || args[i] == "-dryRun")
                {
                    dryRun = true;
                }
                else if (args[i] == "--catalogPath" && i + 1 < args.Length)
                {
                    catalogPath = args[i + 1];
                }
                else if (args[i].StartsWith("--set="))
                {
                    var presetStr = args[i].Substring(6);
                    if (System.Enum.TryParse<Preset>(presetStr, true, out var parsedPreset))
                    {
                        preset = parsedPreset;
                    }
                }
            }

            // Apply preset
            var placeholdersOnly = preset == Preset.Minimal;
            var overwriteMissingSockets = preset == Preset.Full;
            if (preset == Preset.Validate)
            {
                dryRun = true;
            }

            var options = new PrefabMakerOptions
            {
                CatalogPath = catalogPath,
                PlaceholdersOnly = placeholdersOnly,
                OverwriteMissingSockets = overwriteMissingSockets,
                DryRun = dryRun
            };

            var result = PrefabMaker.GenerateAll(options);
            
            // Generate coverage heatmap
            var coverageReport = CoverageHeatmap.GenerateReport(catalogPath);
            CoverageHeatmap.SaveReport(coverageReport, catalogPath);
            CoverageHeatmap.PrintReport(coverageReport);
            
            var logMsg = $"PrefabMaker CLI Run ({preset}): Created={result.CreatedCount}, Updated={result.UpdatedCount}, Skipped={result.SkippedCount}";
            logMsg += $"\nCoverage: {coverageReport.OverallCoverage:F1}% overall";
            
            if (result.Warnings.Count > 0)
            {
                logMsg += $"\nWarnings: {string.Join("; ", result.Warnings)}";
            }
            
            if (result.Errors.Count > 0)
            {
                logMsg += $"\nErrors: {string.Join("; ", result.Errors)}";
                UnityEngine.Debug.LogError(logMsg);
                EditorApplication.Exit(1);
            }
            else
            {
                UnityEngine.Debug.Log(logMsg);
            }
        }

        /// <summary>
        /// CLI entry point for Profile Combo Table generation:
        /// Unity -batchmode -projectPath . -executeMethod Space4X.Editor.PrefabMakerCLI.RunProfiles --dryRun
        /// </summary>
        public static void RunProfiles()
        {
            var dryRun = false;
            var catalogPath = "Assets/Data/Catalogs";

            // Parse command line arguments
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--dryRun" || args[i] == "-dryRun")
                {
                    dryRun = true;
                }
                else if (args[i] == "--catalogPath" && i + 1 < args.Length)
                {
                    catalogPath = args[i + 1];
                }
            }

            var result = AggregateComboBuilder.BuildComboTable(catalogPath, dryRun);
            var logMsg = $"Profile Combo Table Build Complete: Created={result.CreatedCount}, Invalid={result.InvalidCount}";

            if (result.Warnings.Count > 0)
            {
                logMsg += $"\nWarnings: {string.Join("; ", result.Warnings)}";
            }

            if (result.Errors.Count > 0)
            {
                logMsg += $"\nErrors: {string.Join("; ", result.Errors)}";
                UnityEngine.Debug.LogError(logMsg);
            }
            else
            {
                UnityEngine.Debug.Log(logMsg);
            }

            // Run validation
            var validationIssues = AggregateComboBuilder.ValidateProfiles(catalogPath);
            if (validationIssues.Count > 0)
            {
                var errorCount = validationIssues.Count(i => i.Severity == AggregateComboBuilder.ValidationSeverity.Error);
                var warningCount = validationIssues.Count - errorCount;
                UnityEngine.Debug.LogWarning($"Validation found {errorCount} errors and {warningCount} warnings");
            }
        }
    }
}

