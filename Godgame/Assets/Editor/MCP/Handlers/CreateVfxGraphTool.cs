using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("create_vfx_graph")]
    public static class CreateVfxGraphTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();

                if (string.IsNullOrWhiteSpace(graphPath))
                {
                    return Response.Error("graph_path is required");
                }

                if (!VfxGraphReflectionHelpers.TryCreateGraph(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                return Response.Success($"VFX graph created at {graphPath}", new
                {
                    graphPath,
                    resourceId = (resource as UnityEngine.Object)?.GetInstanceID()
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create VFX graph: {ex.Message}");
            }
        }
    }
}

