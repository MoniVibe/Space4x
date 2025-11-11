using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Deploys MCP tools from PureDOTS to all Unity projects in the workspace.
    /// Run from: Tools > MCP > Deploy Tools to All Projects
    /// </summary>
    public static class DeployToProjects
    {
        private static readonly string[] DirectoriesToCopy =
        {
            "Handlers",
            "Helpers",
            "Python",
            "Setup",
            "Tests",
            "Assets"
        };

        private static readonly HashSet<string> RootFileExtensions = new HashSet<string>(
            new[] { ".cs", ".meta", ".asmdef", ".asmref" },
            System.StringComparer.OrdinalIgnoreCase);

        [MenuItem("Tools/MCP/Deploy Tools to All Projects")]
        public static void Deploy()
        {
            var sourcePath = "Assets/Editor/MCP";
            var sourceFullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), sourcePath);
            
            if (!Directory.Exists(sourceFullPath))
            {
                Debug.LogError($"Source MCP directory not found: {sourceFullPath}");
                return;
            }

            Debug.Log("=== Deploying MCP Tools to All Projects ===\n");

            // Projects to deploy to
            var projects = new[]
            {
                "Godgame",
                "Space4x",
                "VFXPlayground"
            };

            var workspaceRoot = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            var workspaceParent = Directory.GetParent(workspaceRoot)?.FullName ?? workspaceRoot;

            foreach (var projectName in projects)
            {
                if (string.Equals(projectName, "PureDOTS", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("Skipping PureDOTS (environment project holds MCP baseline)");
                    continue;
                }

                var projectPath = Path.Combine(workspaceParent, projectName, "Assets", "Editor", "MCP");
                
                if (!Directory.Exists(Path.GetDirectoryName(projectPath)))
                {
                    Debug.LogWarning($"Project {projectName} not found at expected location, skipping...");
                    continue;
                }

                var normalizedSource = Path.GetFullPath(sourceFullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var normalizedTarget = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(normalizedSource, normalizedTarget, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"Skipping {projectName} (source project)");
                    continue;
                }

                Debug.Log($"Deploying to {projectName}...");

                // Ensure target directory exists
                if (!Directory.Exists(projectPath))
                {
                    Directory.CreateDirectory(projectPath);
                    Debug.Log($"  Created directory: {projectPath}");
                }

                // Copy directories
                foreach (var dirName in DirectoriesToCopy)
                {
                    CopyDirectory(
                        Path.Combine(sourceFullPath, dirName),
                        Path.Combine(projectPath, dirName),
                        dirName);
                }

                // Copy root-level MCP scripts/assets (Deploy script, helper menus, etc.)
                CopyRootFiles(sourceFullPath, projectPath);

                // Copy README
                var readmeSource = Path.Combine(sourceFullPath, "README.md");
                var readmeDest = Path.Combine(projectPath, "README.md");
                if (File.Exists(readmeSource))
                {
                    if (CopyFileWithRetry(readmeSource, readmeDest))
                    {
                        Debug.Log($"  Copied README.md");
                    }
                    else
                    {
                        Debug.LogWarning($"  Skipped README.md (file locked)");
                    }
                }

                Debug.Log($"  ✓ {projectName} deployment complete\n");
            }

            Debug.Log("=== Deployment Complete ===");
            Debug.Log("Next steps:");
            Debug.Log("1. Open each project in Unity");
            Debug.Log("2. Run: Tools > MCP > Run One-Click MCP Setup");
            Debug.Log("3. Rebuild MCP server: Window > MCP For Unity > Rebuild Server");
        }

        private static void CopyDirectory(string sourceDir, string destDir, string dirName)
        {
            if (!Directory.Exists(sourceDir))
            {
                Debug.LogWarning($"  Source directory not found: {sourceDir}");
                return;
            }

            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            if (allFiles.Length == 0)
            {
                Debug.LogWarning($"  No files found in {dirName}");
                return;
            }

            int copied = 0;
            int skipped = 0;

            foreach (var file in allFiles)
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var destFile = Path.Combine(destDir, relativePath);
                var destFileDir = Path.GetDirectoryName(destFile);

                if (!string.IsNullOrEmpty(destFileDir) && !Directory.Exists(destFileDir))
                {
                    Directory.CreateDirectory(destFileDir);
                }

                if (CopyFileWithRetry(file, destFile))
                {
                    copied++;
                }
                else
                {
                    skipped++;
                    Debug.LogWarning($"  Failed to copy: {relativePath} (file may be locked by Unity)");
                }
            }

            var summary = $"  Copied {dirName} ({copied}/{allFiles.Length} files)";
            if (skipped > 0)
            {
                summary += $" - {skipped} files skipped (locked)";
            }
            Debug.Log(summary);
        }

        private static void CopyRootFiles(string sourceDir, string destDir)
        {
            var rootFiles = Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly);
            var filesToCopy = new List<string>();

            foreach (var file in rootFiles)
            {
                var fileName = Path.GetFileName(file);
                if (string.Equals(fileName, "README.md", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue; // handled separately
                }

                var ext = Path.GetExtension(file);
                if (!string.IsNullOrEmpty(ext) && RootFileExtensions.Contains(ext))
                {
                    filesToCopy.Add(file);
                }
            }

            if (filesToCopy.Count == 0)
            {
                return;
            }

            int copied = 0;
            int skipped = 0;

            foreach (var file in filesToCopy)
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                if (CopyFileWithRetry(file, destFile))
                {
                    copied++;
                }
                else
                {
                    skipped++;
                    Debug.LogWarning($"  Failed to copy root file: {Path.GetFileName(file)} (file may be locked by Unity)");
                }
            }

            var summary = $"  Copied root MCP files ({copied}/{filesToCopy.Count})";
            if (skipped > 0)
            {
                summary += $" - {skipped} skipped (locked)";
            }
            Debug.Log(summary);
        }

        private static bool CopyFileWithRetry(string sourceFile, string destFile, int maxRetries = 3)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // If file exists and is read-only, make it writable first
                    if (File.Exists(destFile))
                    {
                        var fileInfo = new FileInfo(destFile);
                        if (fileInfo.IsReadOnly)
                        {
                            fileInfo.IsReadOnly = false;
                        }
                    }

                    File.Copy(sourceFile, destFile, overwrite: true);
                    return true;
                }
                catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                {
                    if (attempt < maxRetries - 1)
                    {
                        // Wait with exponential backoff: 50ms, 100ms, 200ms
                        Thread.Sleep(50 * (int)System.Math.Pow(2, attempt));
                    }
                    else
                    {
                        // Last attempt failed
                        return false;
                    }
                }
                catch (System.UnauthorizedAccessException)
                {
                    // File is read-only or access denied
                    try
                    {
                        if (File.Exists(destFile))
                        {
                            var fileInfo = new FileInfo(destFile);
                            fileInfo.IsReadOnly = false;
                            File.Copy(sourceFile, destFile, overwrite: true);
                            return true;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return false;
        }
    }
}

