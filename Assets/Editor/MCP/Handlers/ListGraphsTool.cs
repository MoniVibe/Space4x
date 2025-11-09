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
    [McpForUnityTool("list_graphs")]
    public static class ListGraphsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                bool includeTags = @params["include_tags"]?.ToObject<bool>() ?? true;
                
                // Find all VisualEffectAsset assets
                string[] guids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { "Assets" });
                
                var graphs = new List<object>();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(path);
                    if (asset != null)
                    {
                        var graphInfo = new Dictionary<string, object>
                        {
                            ["graph_id"] = System.IO.Path.GetFileNameWithoutExtension(path),
                            ["name"] = asset.name,
                            ["path"] = path,
                            ["guid"] = guid
                        };
                        
                        if (includeTags)
                        {
                            graphInfo["tags"] = ExtractTagsFromPath(path);
                        }
                        
                        graphs.Add(graphInfo);
                    }
                }
                
                return Response.Success($"Found {graphs.Count} VFX graphs", new
                {
                    count = graphs.Count,
                    graphs = graphs
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list graphs: {ex.Message}");
            }
        }
        
        private static List<string> ExtractTagsFromPath(string path)
        {
            var tags = new List<string>();
            
            // Extract folder names as tags (skip "Assets" and "VFX" if present)
            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            bool skipAssets = true;
            
            foreach (var part in parts)
            {
                if (skipAssets && part.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                {
                    skipAssets = false;
                    continue;
                }
                
                if (skipAssets)
                {
                    continue;
                }
                
                // Skip the filename itself
                if (part.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                
                // Add meaningful folder names as tags
                if (!string.IsNullOrWhiteSpace(part) && part.Length > 1)
                {
                    tags.Add(part.ToLowerInvariant());
                }
            }
            
            return tags;
        }
    }
}

