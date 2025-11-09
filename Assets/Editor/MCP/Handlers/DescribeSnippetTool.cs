using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("describe_snippet")]
    public static class DescribeSnippetTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var snippetId = @params["snippet_id"]?.ToString();
                var snippetPath = @params["snippet_path"]?.ToString();
                int lod = @params["lod"]?.ToObject<int>() ?? 2;

                // Resolve snippet path
                if (!string.IsNullOrEmpty(snippetId) && string.IsNullOrEmpty(snippetPath))
                {
                    snippetPath = ResolveSnippetIdToPath(snippetId);
                    if (string.IsNullOrEmpty(snippetPath))
                    {
                        return Response.Error($"Snippet with id '{snippetId}' not found");
                    }
                }

                if (string.IsNullOrEmpty(snippetPath))
                {
                    return Response.Error("snippet_path or snippet_id is required");
                }

                // Check if it's a subgraph or regular graph
                bool isSubgraph = snippetPath.EndsWith(".vfxsubgraph", StringComparison.OrdinalIgnoreCase);
                
                if (isSubgraph)
                {
                    return DescribeSubgraphSnippet(snippetPath, snippetId ?? System.IO.Path.GetFileNameWithoutExtension(snippetPath), lod);
                }
                else
                {
                    // Treat as regular VFX graph snippet - reuse describe_vfx_graph logic
                    return DescribeGraphSnippet(snippetPath, snippetId ?? System.IO.Path.GetFileNameWithoutExtension(snippetPath), lod);
                }
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to describe snippet: {ex.Message}");
            }
        }

        private static object DescribeGraphSnippet(string graphPath, string snippetId, int lod)
        {
            // Reuse the describe_vfx_graph logic but mark as snippet
            var graphParams = new JObject
            {
                ["graph_path"] = graphPath,
                ["lod"] = lod
            };
            
            var graphDesc = DescribeVfxGraphTool.HandleCommand(graphParams);
            
            if (graphDesc is JObject response && response["success"]?.ToObject<bool>() == true)
            {
                var data = response["data"] as JObject;
                if (data != null)
                {
                    data["id"] = snippetId;
                    data["kind"] = "graph";
                    data["parent_graph_id"] = ExtractParentGraphId(graphPath);
                }
            }
            
            return graphDesc;
        }

        private static object DescribeSubgraphSnippet(string subgraphPath, string snippetId, int lod)
        {
            try
            {
                if (!VfxGraphReflectionHelpers.TryGetResource(subgraphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                var tags = ExtractTagsFromPath(subgraphPath);
                
                // Get exposed parameters (similar to describe_vfx_graph)
                var exposedParams = GetExposedParameters(subgraphPath, resource, lod);
                
                // Get stages
                var stages = GetStages(subgraphPath, resource, lod);
                
                var descriptor = new Dictionary<string, object>
                {
                    ["id"] = snippetId,
                    ["kind"] = "subgraph",
                    ["exposed_params"] = exposedParams,
                    ["stages"] = stages,
                    ["tags"] = tags,
                    ["parent_graph_id"] = ExtractParentGraphId(subgraphPath)
                };

                if (lod >= 2)
                {
                    descriptor["path"] = subgraphPath;
                    
                    // Add structure if available
                    var structure = GetGraphStructure(resource, lod);
                    if (structure != null)
                    {
                        descriptor["structure"] = structure;
                    }
                }

                var serializer = new Newtonsoft.Json.JsonSerializer
                {
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                };
                var descriptorToken = JToken.FromObject(descriptor, serializer);

                return Response.Success($"Snippet descriptor generated for {snippetId}", descriptorToken);
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to describe subgraph snippet: {ex.Message}");
            }
        }

        private static string ResolveSnippetIdToPath(string snippetId)
        {
            // Try subgraphs first
            string[] subgraphGuids = AssetDatabase.FindAssets("t:VFXSubgraphOperator", new[] { "Assets" });
            foreach (string guid in subgraphGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(snippetId, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            
            // Try regular graphs
            string[] graphGuids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { "Assets" });
            foreach (string guid in graphGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(snippetId, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            
            return null;
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

                if (skipAssets) continue;

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

        // Reuse methods from DescribeVfxGraphTool via reflection or direct calls
        private static List<Dictionary<string, object>> GetExposedParameters(string graphPath, object resource, int lod)
        {
            // Use reflection to call DescribeVfxGraphTool's private method
            // For now, create a simplified version
            var paramsList = new List<Dictionary<string, object>>();
            
            try
            {
                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out var error))
                {
                    Debug.LogWarning($"[MCP Tools] describe_snippet: Unable to get controller: {error}");
                    return paramsList;
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                var syncArgs = new object[] { false };
                controller.GetType()
                    .GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                // Get parameters (simplified - reuse DescribeVfxGraphTool logic via public API)
                var graphParams = new JObject { ["graph_path"] = graphPath, ["lod"] = lod };
                var graphDesc = DescribeVfxGraphTool.HandleCommand(graphParams);
                
                if (graphDesc is JObject response && response["success"]?.ToObject<bool>() == true)
                {
                    var data = response["data"] as JObject;
                    if (data != null && data["exposed_params"] != null)
                    {
                        var exposedParamsToken = data["exposed_params"];
                        if (exposedParamsToken is JArray paramsArray)
                        {
                            foreach (var paramToken in paramsArray)
                            {
                                if (paramToken is JObject paramObj)
                                {
                                    paramsList.Add(paramObj.ToObject<Dictionary<string, object>>());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] describe_snippet: Error getting parameters: {ex.Message}");
            }

            return paramsList;
        }

        private static Dictionary<string, List<string>> GetStages(string graphPath, object resource, int lod)
        {
            // Reuse DescribeVfxGraphTool logic
            var graphParams = new JObject { ["graph_path"] = graphPath, ["lod"] = lod };
            var graphDesc = DescribeVfxGraphTool.HandleCommand(graphParams);
            
            if (graphDesc is JObject response && response["success"]?.ToObject<bool>() == true)
            {
                var data = response["data"] as JObject;
                if (data != null && data["stages"] != null)
                {
                    var stagesToken = data["stages"];
                    if (stagesToken is JObject stagesObj)
                    {
                        return stagesObj.ToObject<Dictionary<string, List<string>>>();
                    }
                }
            }
            
            return new Dictionary<string, List<string>>();
        }

        private static Dictionary<string, object> GetGraphStructure(object resource, int lod)
        {
            // Reuse DescribeVfxGraphTool logic via reflection
            // For now, return null - structure extraction is complex
            return null;
        }
    }
}

