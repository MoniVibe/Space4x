using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.IO;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("save_scene_as")]
    public static class SaveSceneAsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string scenePath = @params["scene_path"]?.ToString();
                bool createBackup = @params["create_backup"]?.ToObject<bool>() ?? true;
                
                if (string.IsNullOrEmpty(scenePath))
                {
                    return Response.Error("scene_path is required");
                }
                
                // Ensure path has .unity extension
                if (!scenePath.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                {
                    scenePath += ".unity";
                }
                
                // Get current scene
                var activeScene = EditorSceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    return Response.Error("No active scene to save");
                }
                
                // Create backup if requested and file exists
                if (createBackup && File.Exists(scenePath))
                {
                    string backupPath = scenePath + ".backup";
                    File.Copy(scenePath, backupPath, true);
                }
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(scenePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Save scene
                var savedScene = EditorSceneManager.SaveScene(activeScene, scenePath);
                
                if (!savedScene)
                {
                    return Response.Error($"Failed to save scene at {scenePath}");
                }
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return Response.Success($"Scene saved successfully", new
                {
                    scenePath = scenePath,
                    backupCreated = createBackup && File.Exists(scenePath + ".backup")
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to save scene: {ex.Message}");
            }
        }
    }
}

