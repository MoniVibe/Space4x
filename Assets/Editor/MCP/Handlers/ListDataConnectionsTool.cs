using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("list_data_connections")]
    public static class ListDataConnectionsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var nodeIdFilter = @params["node_id"]?.ToObject<int?>();

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
                var connections = new List<Dictionary<string, object>>();

                // Get data edges from controller
                var dataEdgesProperty = controller.GetType().GetProperty("dataEdges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (dataEdgesProperty != null)
                {
                    var dataEdges = dataEdgesProperty.GetValue(controller);
                    if (dataEdges is IEnumerable edgesEnumerable)
                    {
                        foreach (var edge in VfxGraphReflectionHelpers.Enumerate(edgesEnumerable))
                        {
                            if (edge == null) continue;

                            var edgeType = edge.GetType();
                            var inputProperty = edgeType.GetProperty("input", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            var outputProperty = edgeType.GetProperty("output", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                            if (inputProperty == null || outputProperty == null) continue;

                            var inputAnchor = inputProperty.GetValue(edge);
                            var outputAnchor = outputProperty.GetValue(edge);

                            var sourceNodeController = GetNodeControllerFromAnchor(outputAnchor, controller);
                            var targetNodeController = GetNodeControllerFromAnchor(inputAnchor, controller);

                            if (sourceNodeController == null || targetNodeController == null) continue;

                            var sourceModel = VfxGraphReflectionHelpers.GetProperty(sourceNodeController, "model") as UnityEngine.Object;
                            var targetModel = VfxGraphReflectionHelpers.GetProperty(targetNodeController, "model") as UnityEngine.Object;

                            if (sourceModel == null || targetModel == null) continue;

                            var sourceNodeId = sourceModel.GetInstanceID();
                            var targetNodeId = targetModel.GetInstanceID();

                            // Apply filter if specified
                            if (nodeIdFilter.HasValue && sourceNodeId != nodeIdFilter.Value && targetNodeId != nodeIdFilter.Value)
                            {
                                continue;
                            }

                            var sourceSlot = VfxGraphReflectionHelpers.GetProperty(outputAnchor, "model");
                            var targetSlot = VfxGraphReflectionHelpers.GetProperty(inputAnchor, "model");

                            var sourceSlotName = sourceSlot?.GetType().GetProperty("name")?.GetValue(sourceSlot) as string;
                            var sourceSlotPath = sourceSlot?.GetType().GetProperty("path")?.GetValue(sourceSlot) as string;
                            var targetSlotName = targetSlot?.GetType().GetProperty("name")?.GetValue(targetSlot) as string;
                            var targetSlotPath = targetSlot?.GetType().GetProperty("path")?.GetValue(targetSlot) as string;

                            connections.Add(new Dictionary<string, object>
                            {
                                ["sourceNodeId"] = sourceNodeId,
                                ["sourcePortName"] = sourceSlotName,
                                ["sourcePortPath"] = sourceSlotPath,
                                ["targetNodeId"] = targetNodeId,
                                ["targetPortName"] = targetSlotName,
                                ["targetPortPath"] = targetSlotPath,
                                ["connectionType"] = "data"
                            });
                        }
                    }
                }

                return Response.Success($"Found {connections.Count} data connections in graph {graphPath}", new
                {
                    graphPath,
                    connectionCount = connections.Count,
                    connections
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list data connections: {ex.Message}");
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

        private static object GetNodeControllerFromAnchor(object anchor, object controller)
        {
            try
            {
                var ownerProperty = anchor?.GetType().GetProperty("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (ownerProperty != null)
                {
                    return ownerProperty.GetValue(anchor);
                }

                // Fallback: search all nodes
                var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
                foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
                {
                    var inputPorts = VfxGraphReflectionHelpers.GetProperty(nodeController, "inputPorts");
                    var outputPorts = VfxGraphReflectionHelpers.GetProperty(nodeController, "outputPorts");

                    foreach (var port in VfxGraphReflectionHelpers.Enumerate(inputPorts))
                    {
                        if (port == anchor) return nodeController;
                    }
                    foreach (var port in VfxGraphReflectionHelpers.Enumerate(outputPorts))
                    {
                        if (port == anchor) return nodeController;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}

