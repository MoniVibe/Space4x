using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("find_assets_by_type")]
    public static class FindAssetsByTypeTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string assetType = @params["asset_type"]?.ToString();
                bool searchInSubfolders = @params["search_in_subfolders"]?.ToObject<bool>() ?? true;
                
                if (string.IsNullOrEmpty(assetType))
                {
                    return Response.Error("asset_type is required");
                }
                
                // Resolve type from string
                Type type = ResolveAssetType(assetType);
                if (type == null)
                {
                    return Response.Error($"Asset type '{assetType}' not recognized. Supported types: Prefab, Material, Texture2D, ScriptableObject, GameObject, Script, etc.");
                }
                
                // Find all assets of this type
                string[] guids = AssetDatabase.FindAssets($"t:{type.Name}", searchInSubfolders ? new[] { "Assets" } : null);
                
                var assets = new List<object>();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset != null)
                    {
                        assets.Add(new
                        {
                            path = path,
                            name = asset.name,
                            guid = guid,
                            type = asset.GetType().Name
                        });
                    }
                }
                
                return Response.Success($"Found {assets.Count} assets of type {assetType}", new
                {
                    assetType = assetType,
                    count = assets.Count,
                    assets = assets,
                    searchInSubfolders = searchInSubfolders
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to find assets by type: {ex.Message}");
            }
        }
        
        private static Type ResolveAssetType(string assetType)
        {
            // Try direct type lookup first
            Type type = Type.GetType(assetType);
            if (type != null) return type;
            
            // Common type aliases
            var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Prefab"] = "UnityEngine.GameObject",
                ["Material"] = "UnityEngine.Material",
                ["Texture2D"] = "UnityEngine.Texture2D",
                ["Script"] = "UnityEditor.MonoScript",
                ["ScriptableObject"] = "UnityEngine.ScriptableObject",
                ["GameObject"] = "UnityEngine.GameObject",
                ["Texture"] = "UnityEngine.Texture",
                ["Mesh"] = "UnityEngine.Mesh",
                ["AudioClip"] = "UnityEngine.AudioClip",
                ["AnimationClip"] = "UnityEngine.AnimationClip",
                ["Shader"] = "UnityEngine.Shader",
                ["Font"] = "UnityEngine.Font",
            };
            
            if (typeMap.TryGetValue(assetType, out string fullTypeName))
            {
                type = Type.GetType(fullTypeName);
                if (type != null) return type;
            }
            
            // Try finding in all assemblies
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(assetType, false);
                    if (type != null) return type;
                    
                    // Also try without namespace
                    string shortName = assetType.Contains(".") ? assetType.Split('.').Last() : assetType;
                    type = assembly.GetTypes().FirstOrDefault(t => t.Name == shortName);
                    if (type != null) return type;
                }
                catch { }
            }
            
            return null;
        }
    }
}

