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
    [McpForUnityTool("create_shader_graph")]
    public static class CreateShaderGraphTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string graphPath = @params["graph_path"]?.ToString();
                bool isSubGraph = @params["is_subgraph"]?.ToObject<bool>() ?? false;
                bool replaceExisting = @params["replace_existing"]?.ToObject<bool>() ?? false;
                
                if (string.IsNullOrEmpty(graphPath))
                {
                    return Response.Error("graph_path is required");
                }
                
                // Ensure path has correct extension
                if (isSubGraph && !graphPath.EndsWith(".shadersubgraph", System.StringComparison.OrdinalIgnoreCase))
                {
                    graphPath += ".shadersubgraph";
                }
                else if (!isSubGraph && !graphPath.EndsWith(".shadergraph", System.StringComparison.OrdinalIgnoreCase))
                {
                    graphPath += ".shadergraph";
                }
                
                // Check if graph already exists
                if (File.Exists(graphPath) && !replaceExisting)
                {
                    return Response.Error($"Shader Graph already exists at {graphPath}. Set replace_existing=true to overwrite.");
                }
                
                // Use helper to create graph
                if (!ShaderGraphReflectionHelpers.TryCreateGraph(graphPath, out object graphAsset, out string error))
                {
                    return Response.Error(error);
                }
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return Response.Success($"Shader Graph created successfully", new
                {
                    graphPath = graphPath,
                    isSubGraph = isSubGraph,
                    graphAssetName = graphAsset != null ? graphAsset.GetType().Name : "Unknown"
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create shader graph: {ex.Message}");
            }
        }
    }
}
#endif

