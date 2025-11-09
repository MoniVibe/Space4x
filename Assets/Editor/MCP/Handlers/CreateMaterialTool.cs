using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.IO;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("create_material")]
    public static class CreateMaterialTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string materialPath = @params["material_path"]?.ToString();
                string shaderName = @params["shader_name"]?.ToString() ?? "Standard";
                bool replaceExisting = @params["replace_existing"]?.ToObject<bool>() ?? false;
                
                if (string.IsNullOrEmpty(materialPath))
                {
                    return Response.Error("material_path is required");
                }
                
                // Ensure path has .mat extension
                if (!materialPath.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
                {
                    materialPath += ".mat";
                }
                
                // Check if material already exists
                if (File.Exists(materialPath) && !replaceExisting)
                {
                    return Response.Error($"Material already exists at {materialPath}. Set replace_existing=true to overwrite.");
                }
                
                // Find shader
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    return Response.Error($"Shader '{shaderName}' not found. Use a valid shader name like 'Standard', 'Unlit/Color', etc.");
                }
                
                // Create material
                Material material = new Material(shader);
                material.name = Path.GetFileNameWithoutExtension(materialPath);
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(materialPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Save material asset
                AssetDatabase.CreateAsset(material, materialPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return Response.Success($"Material created successfully", new
                {
                    materialPath = materialPath,
                    shaderName = shaderName,
                    materialName = material.name
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create material: {ex.Message}");
            }
        }
    }
}

