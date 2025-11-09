using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("set_visual_effect_graph")]
    public static class SetVisualEffectGraphTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var gameObjectName = @params["gameobject_name"]?.ToString();
                var graphPath = @params["graph_path"]?.ToString();

                if (string.IsNullOrWhiteSpace(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }

                if (string.IsNullOrWhiteSpace(graphPath))
                {
                    return Response.Error("graph_path is required");
                }

                if (!VfxGraphReflectionHelpers.TrySetVisualEffectGraph(gameObjectName, graphPath, out var error))
                {
                    return Response.Error(error);
                }

                return Response.Success($"VisualEffect graph set on {gameObjectName}", new
                {
                    gameObjectName,
                    graphPath
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to set visual effect graph: {ex.Message}");
            }
        }
    }
}

