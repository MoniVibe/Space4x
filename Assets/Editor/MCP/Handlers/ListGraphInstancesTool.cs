using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("list_graph_instances")]
    public static class ListGraphInstancesTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var instances = VfxGraphReflectionHelpers.ListGraphInstances();

                return Response.Success($"Found {instances.Count} VFX graph instances in scene", new
                {
                    instanceCount = instances.Count,
                    instances
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list graph instances: {ex.Message}");
            }
        }
    }
}

