using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("duplicate_graph")]
    public static class DuplicateGraphTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var sourcePath = @params["source_path"]?.ToString();
                var destinationPath = @params["destination_path"]?.ToString();

                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    return Response.Error("source_path is required");
                }

                if (string.IsNullOrWhiteSpace(destinationPath))
                {
                    return Response.Error("destination_path is required");
                }

                if (!VfxGraphReflectionHelpers.TryDuplicateGraph(sourcePath, destinationPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                return Response.Success($"Graph duplicated from {sourcePath} to {destinationPath}", new
                {
                    sourcePath,
                    destinationPath,
                    resourceId = (resource as UnityEngine.Object)?.GetInstanceID()
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to duplicate graph: {ex.Message}");
            }
        }
    }
}

