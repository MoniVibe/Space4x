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
    [McpForUnityTool("list_snippets")]
    public static class ListSnippetsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var parentGraphId = @params["parent_graph_id"]?.ToString();
                var includeTags = @params["include_tags"]?.ToObject<bool>() ?? true;

                // Find all VFX graphs that could be snippets
                // For now, we'll identify snippets as:
                // 1. Subgraphs (VFXSubgraphOperator assets)
                // 2. Graphs in a "Snippets" folder
                // 3. Graphs with "snippet" in their name/tags
                
                var snippets = new List<Dictionary<string, object>>();
                
                // Find subgraph assets
                string[] subgraphGuids = AssetDatabase.FindAssets("t:VFXSubgraphOperator", new[] { "Assets" });
                foreach (string guid in subgraphGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var snippetId = System.IO.Path.GetFileNameWithoutExtension(path);
                    var tags = ExtractTagsFromPath(path);
                    
                    if (!string.IsNullOrEmpty(parentGraphId))
                    {
                        // Filter by parent graph if specified
                        if (!path.Contains(parentGraphId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }
                    
                    snippets.Add(new Dictionary<string, object>
                    {
                        ["id"] = snippetId,
                        ["name"] = snippetId,
                        ["kind"] = "subgraph",
                        ["path"] = path,
                        ["tags"] = tags,
                        ["parent_graph_id"] = ExtractParentGraphId(path)
                    });
                }
                
                // Find graphs in Snippets folders or with "snippet" in name
                string[] graphGuids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { "Assets" });
                foreach (string guid in graphGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var snippetId = System.IO.Path.GetFileNameWithoutExtension(path);
                    var pathLower = path.ToLowerInvariant();
                    
                    // Check if it's a snippet candidate
                    bool isSnippet = pathLower.Contains("/snippets/") || 
                                    pathLower.Contains("snippet") ||
                                    snippetId.ToLowerInvariant().Contains("snippet");
                    
                    if (!isSnippet) continue;
                    
                    var tags = ExtractTagsFromPath(path);
                    
                    if (!string.IsNullOrEmpty(parentGraphId))
                    {
                        if (!path.Contains(parentGraphId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }
                    
                    snippets.Add(new Dictionary<string, object>
                    {
                        ["id"] = snippetId,
                        ["name"] = snippetId,
                        ["kind"] = "graph",
                        ["path"] = path,
                        ["tags"] = tags,
                        ["parent_graph_id"] = ExtractParentGraphId(path)
                    });
                }

                return Response.Success($"Found {snippets.Count} snippet(s)", new Dictionary<string, object>
                {
                    ["snippets"] = snippets
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list snippets: {ex.Message}");
            }
        }

        private static List<string> ExtractTagsFromPath(string path)
        {
            var tags = new List<string>();
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

                if (part.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase) || 
                    part.EndsWith(".vfxsubgraph", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(part) && part.Length > 1)
                {
                    tags.Add(part.ToLowerInvariant());
                }
            }

            return tags;
        }

        private static string ExtractParentGraphId(string path)
        {
            // Try to infer parent graph from path structure
            // e.g., "Assets/VFX/Bonfire/Snippets/Embers.vfx" -> "Bonfire"
            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].Equals("snippets", StringComparison.OrdinalIgnoreCase) && i > 0)
                {
                    return parts[i - 1];
                }
            }
            return null;
        }
    }
}

