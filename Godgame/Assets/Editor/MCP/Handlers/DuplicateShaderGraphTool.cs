#if false
// Shader Graph tools disabled - Shader Graph package not installed
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.IO;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("duplicate_shader_graph")]
    public static class DuplicateShaderGraphTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string sourcePath = @params["source_path"]?.ToString();
                string destinationPath = @params["destination_path"]?.ToString();
                bool allowOverwrite = @params["allow_overwrite"]?.ToObject<bool>() ?? false;
                
                if (string.IsNullOrEmpty(sourcePath))
                {
                    return Response.Error("source_path is required");
                }
                
                if (string.IsNullOrEmpty(destinationPath))
                {
                    return Response.Error("destination_path is required");
                }
                
                // Check if source exists
                if (!File.Exists(sourcePath))
                {
                    return Response.Error($"Source graph not found at {sourcePath}");
                }
                
                // Check if destination exists
                if (File.Exists(destinationPath) && !allowOverwrite)
                {
                    return Response.Error($"Destination graph already exists at {destinationPath}. Set allow_overwrite=true to overwrite.");
                }
                
                // Ensure destination directory exists
                string directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Copy the file
                File.Copy(sourcePath, destinationPath, overwrite: allowOverwrite);
                
                // Import the new asset
                AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                
                // Wait for import
                System.Threading.Thread.Sleep(200);
                AssetDatabase.Refresh();
                
                return Response.Success($"Shader Graph duplicated successfully", new
                {
                    sourcePath = sourcePath,
                    destinationPath = destinationPath
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to duplicate shader graph: {ex.Message}");
            }
        }
    }
}
#endif

