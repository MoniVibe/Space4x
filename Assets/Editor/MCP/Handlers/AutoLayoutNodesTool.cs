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
    [McpForUnityTool("auto_layout_nodes")]
    public static class AutoLayoutNodesTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var spacingX = @params["spacing_x"]?.ToObject<float?>() ?? 200f;
                var spacingY = @params["spacing_y"]?.ToObject<float?>() ?? 150f;
                var startX = @params["start_x"]?.ToObject<float?>() ?? 0f;
                var startY = @params["start_y"]?.ToObject<float?>() ?? 0f;

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out error))
                {
                    return Response.Error(error);
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                var syncArgs = new object[] { false };
                controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                var nodes = new List<(object nodeController, object model)>();
                var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
                foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
                {
                    var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                    if (model != null)
                    {
                        nodes.Add((nodeController, model));
                    }
                }

                // Simple grid layout
                var columns = (int)Mathf.Ceil(Mathf.Sqrt(nodes.Count));
                var currentX = startX;
                var currentY = startY;
                var columnIndex = 0;

                foreach (var (nodeController, model) in nodes)
                {
                    VfxGraphReflectionHelpers.SetModelPosition(model, new Vector2(currentX, currentY));

                    columnIndex++;
                    if (columnIndex >= columns)
                    {
                        columnIndex = 0;
                        currentX = startX;
                        currentY += spacingY;
                    }
                    else
                    {
                        currentX += spacingX;
                    }
                }

                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                return Response.Success($"Auto-layout applied to {nodes.Count} nodes in graph {graphPath}", new
                {
                    graphPath,
                    nodeCount = nodes.Count,
                    layout = new { spacingX, spacingY, startX, startY }
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to auto-layout nodes: {ex.Message}");
            }
        }
    }
}

