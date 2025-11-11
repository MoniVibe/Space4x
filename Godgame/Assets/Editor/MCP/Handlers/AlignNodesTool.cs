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
    [McpForUnityTool("align_nodes")]
    public static class AlignNodesTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var nodeIds = @params["node_ids"]?.ToObject<int[]>();
                var alignment = @params["alignment"]?.ToString()?.ToLowerInvariant() ?? "horizontal";
                var spacing = @params["spacing"]?.ToObject<float?>() ?? 200f;

                if (nodeIds == null || nodeIds.Length == 0)
                {
                    return Response.Error("node_ids array is required with at least one node ID");
                }

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

                var nodeMap = BuildNodeMap(controller);
                var nodesToAlign = new List<(object nodeController, object model, Vector2 position)>();

                foreach (var nodeId in nodeIds)
                {
                    if (nodeMap.TryGetValue(nodeId, out var nodeController))
                    {
                        var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                        if (model != null)
                        {
                            var currentPosition = VfxGraphReflectionHelpers.GetProperty(model, "position");
                            if (currentPosition is Vector2 pos)
                            {
                                nodesToAlign.Add((nodeController, model, pos));
                            }
                        }
                    }
                }

                if (nodesToAlign.Count == 0)
                {
                    return Response.Error("No valid nodes found to align");
                }

                // Sort nodes by current position
                if (alignment == "horizontal" || alignment == "h")
                {
                    nodesToAlign = nodesToAlign.OrderBy(n => n.position.x).ToList();
                    var baseY = nodesToAlign[0].position.y;
                    var currentX = nodesToAlign[0].position.x;
                    foreach (var (nodeController, model, _) in nodesToAlign)
                    {
                        VfxGraphReflectionHelpers.SetModelPosition(model, new Vector2(currentX, baseY));
                        currentX += spacing;
                    }
                }
                else if (alignment == "vertical" || alignment == "v")
                {
                    nodesToAlign = nodesToAlign.OrderBy(n => n.position.y).ToList();
                    var baseX = nodesToAlign[0].position.x;
                    var currentY = nodesToAlign[0].position.y;
                    foreach (var (nodeController, model, _) in nodesToAlign)
                    {
                        VfxGraphReflectionHelpers.SetModelPosition(model, new Vector2(baseX, currentY));
                        currentY += spacing;
                    }
                }
                else
                {
                    return Response.Error("alignment must be 'horizontal' or 'vertical'");
                }

                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                return Response.Success($"Aligned {nodesToAlign.Count} nodes in graph {graphPath}", new
                {
                    graphPath,
                    nodeCount = nodesToAlign.Count,
                    alignment,
                    spacing
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to align nodes: {ex.Message}");
            }
        }

        private static Dictionary<int, object> BuildNodeMap(object controller)
        {
            var map = new Dictionary<int, object>();
            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model != null)
                {
                    map[model.GetInstanceID()] = nodeController;
                }
            }
            return map;
        }
    }
}

