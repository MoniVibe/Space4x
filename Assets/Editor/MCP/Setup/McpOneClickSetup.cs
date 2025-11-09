using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PureDOTS.Editor.MCP;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Provides a single entry point for configuring MCP tools within the current Unity project.
    /// </summary>
    public static class McpOneClickSetup
    {
        private const string PythonFolder = "Assets/Editor/MCP/Python";
        private const string PythonToolsAssetPath = "Assets/Editor/MCP/Assets/McpPythonTools.asset";

        [MenuItem("Tools/MCP/Run One-Click MCP Setup")]
        public static void Run()
        {
            Debug.Log("=== MCP One-Click Setup ===");

            if (!EnsureMcpPackageLoaded())
            {
                Debug.LogError("MCP For Unity package types could not be resolved. Please ensure the package is installed.");
                return;
            }

            if (!EnsurePythonDirectory())
            {
                Debug.LogError($"Python directory not found: {PythonFolder}. Ensure MCP Python files are present.");
                return;
            }

            if (!EnsurePythonToolsAsset(out var asset))
            {
                Debug.LogError("Failed to create or load PythonToolsAsset. See console for details.");
                return;
            }

            int mappedFiles = MapPythonFiles(asset);
            bool synced = SyncPythonFiles();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("=== MCP One-Click Setup Complete ===");
            Debug.Log($"Python files mapped: {mappedFiles}");
            Debug.Log(synced ? "Python tools synced." : "Python tools sync skipped or failed.");
            Debug.LogWarning("IMPORTANT: You must rebuild the MCP server for tools to appear!");
            Debug.LogWarning("  Go to: Window > MCP For Unity > Rebuild Server");
            Debug.LogWarning("After rebuild, you should see " + (22 + mappedFiles) + " total tools (22 default + " + mappedFiles + " custom).");
            Debug.LogWarning("If additional tools are added in the future, rerun this setup and rebuild the server.");
        }

        private static bool EnsureMcpPackageLoaded()
        {
            var commandRegistryType = McpReflectionHelpers.GetMcpType("MCPForUnity.Editor.Tools.CommandRegistry");
            return commandRegistryType != null;
        }

        private static bool EnsurePythonDirectory()
        {
            if (Directory.Exists(PythonFolder))
            {
                return true;
            }

            Directory.CreateDirectory(PythonFolder);
            return Directory.Exists(PythonFolder);
        }

        private static bool EnsurePythonToolsAsset(out ScriptableObject asset)
        {
            asset = null;

            var pythonToolsType = McpReflectionHelpers.GetMcpType("MCPForUnity.Editor.Data.PythonToolsAsset");
            if (pythonToolsType == null)
            {
                Debug.LogError("Could not resolve MCPForUnity.Editor.Data.PythonToolsAsset.");
                return false;
            }

            asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PythonToolsAssetPath);
            if (asset != null)
            {
                return true;
            }

            string assetFolder = Path.GetDirectoryName(PythonToolsAssetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(assetFolder) && !Directory.Exists(assetFolder))
            {
                Directory.CreateDirectory(assetFolder);
            }

            asset = ScriptableObject.CreateInstance(pythonToolsType) as ScriptableObject;
            if (asset == null)
            {
                Debug.LogError("Failed to instantiate PythonToolsAsset.");
                return false;
            }

            AssetDatabase.CreateAsset(asset, PythonToolsAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created PythonToolsAsset at {PythonToolsAssetPath}");
            return true;
        }

        private static int MapPythonFiles(ScriptableObject asset)
        {
            var pythonFiles = Directory.GetFiles(PythonFolder, "*.py", SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace("\\", "/"))
                .Where(path => !path.EndsWith("__init__.py", StringComparison.OrdinalIgnoreCase))
                .Select(path => AssetDatabase.LoadAssetAtPath<TextAsset>(path))
                .Where(assetRef => assetRef != null)
                .Distinct()
                .ToList();

            if (pythonFiles.Count == 0)
            {
                Debug.LogWarning($"No Python files found in {PythonFolder}.");
            }

            var so = new SerializedObject(asset);
            var pythonFilesProp = so.FindProperty("pythonFiles");

            if (pythonFilesProp == null)
            {
                Debug.LogError("pythonFiles field not found on PythonToolsAsset. Package version may be incompatible.");
                return 0;
            }

            so.Update();
            pythonFilesProp.arraySize = pythonFiles.Count;

            for (int i = 0; i < pythonFiles.Count; i++)
            {
                pythonFilesProp.GetArrayElementAtIndex(i).objectReferenceValue = pythonFiles[i];
            }

            so.ApplyModifiedProperties();

            Debug.Log($"Mapped {pythonFiles.Count} Python files to PythonToolsAsset.");
            return pythonFiles.Count;
        }

        private static bool SyncPythonFiles()
        {
            var syncProcessorType = McpReflectionHelpers.GetMcpType("MCPForUnity.Editor.Helpers.PythonToolSyncProcessor");
            if (syncProcessorType == null)
            {
                Debug.LogWarning("PythonToolSyncProcessor not available. Python files will sync on next editor restart.");
                return false;
            }

            var syncMethod = syncProcessorType.GetMethod("SyncAllTools", BindingFlags.Public | BindingFlags.Static);
            if (syncMethod == null)
            {
                Debug.LogWarning("SyncAllTools method not found on PythonToolSyncProcessor.");
                return false;
            }

            try
            {
                syncMethod.Invoke(null, null);
                Debug.Log("Triggered Python tool sync.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to sync Python tools: {ex.Message}");
                return false;
            }
        }
    }
}

