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
    [McpForUnityTool("describe_node_ports")]
    public static class DescribeNodePortsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var nodeIdToken = @params["node_id"];

                if (!TryParseNodeId(nodeIdToken, out var nodeId, out var error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out error))
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

                if (!nodeMap.TryGetValue(nodeId, out var nodeController))
                {
                    return Response.Error($"Node with id {nodeId} not found");
                }

                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model == null)
                {
                    return Response.Error($"Node controller found but model is null");
                }

                var inputPorts = DescribePorts(nodeController, false);
                var outputPorts = DescribePorts(nodeController, true);

                return Response.Success($"Described ports for node {nodeId}", new
                {
                    graphPath,
                    nodeId,
                    nodeType = model.GetType().FullName,
                    inputs = inputPorts,
                    outputs = outputPorts,
                    inputCount = inputPorts.Count,
                    outputCount = outputPorts.Count
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to describe node ports: {ex.Message}");
            }
        }

        private static bool TryParseNodeId(JToken token, out int id, out string error)
        {
            error = null;
            id = 0;

            if (token == null)
            {
                error = "node_id is required";
                return false;
            }

            if (token.Type == JTokenType.Integer && token.ToObject<int?>() is int directId)
            {
                id = directId;
                return true;
            }

            var asString = token.ToString();
            if (int.TryParse(asString, out id))
            {
                return true;
            }

            error = $"Unable to parse node id '{asString}'";
            return false;
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

        private static List<Dictionary<string, object>> DescribePorts(object nodeController, bool isOutput)
        {
            var ports = new List<Dictionary<string, object>>();
            var propertyName = isOutput ? "outputPorts" : "inputPorts";
            var portsEnumerable = VfxGraphReflectionHelpers.GetProperty(nodeController, propertyName);

            int index = 0;
            foreach (var portController in VfxGraphReflectionHelpers.Enumerate(portsEnumerable))
            {
                try
                {
                    var slot = VfxGraphReflectionHelpers.GetProperty(portController, "model");
                    var slotType = slot?.GetType();
                    if (slotType == null)
                    {
                        index++;
                        continue;
                    }

                    var name = slotType.GetProperty("name")?.GetValue(slot) as string;
                    var path = slotType.GetProperty("path")?.GetValue(slot) as string;
                    var portType = VfxGraphReflectionHelpers.GetProperty(portController, "portType") as Type;
                    var connected = VfxGraphReflectionHelpers.GetProperty(portController, "connected") as bool? ?? false;

                    // Try to get connection information
                    var connections = GetPortConnections(portController, nodeController, isOutput);

                    // Try to get default value
                    object defaultValue = null;
                    var valueProperty = slotType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (valueProperty != null)
                    {
                        try
                        {
                            defaultValue = valueProperty.GetValue(slot);
                        }
                        catch { }
                    }

                    var portInfo = new Dictionary<string, object>
                    {
                        ["index"] = index,
                        ["name"] = name ?? "Unnamed",
                        ["path"] = path,
                        ["direction"] = isOutput ? "output" : "input",
                        ["connected"] = connected,
                        ["portType"] = portType?.FullName,
                        ["connections"] = connections
                    };

                    if (defaultValue != null)
                    {
                        portInfo["defaultValue"] = ConvertDefaultValue(defaultValue);
                    }

                    ports.Add(portInfo);
                }
                catch (Exception portEx)
                {
                    Debug.LogWarning($"[MCP Tools] Failed to describe port at index {index}: {portEx.Message}");
                }

                index++;
            }

            return ports;
        }

        private static List<Dictionary<string, object>> GetPortConnections(object portController, object nodeController, bool isOutput)
        {
            var connections = new List<Dictionary<string, object>>();

            try
            {
                // Try to get links from the controller
                var controllerType = nodeController.GetType();
                var controller = VfxGraphReflectionHelpers.GetProperty(nodeController, "controller");
                if (controller == null)
                {
                    return connections;
                }

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

                            // Check if this port is involved in this connection
                            if (isOutput && outputAnchor == portController)
                            {
                                // This is an output port, find the target
                                var targetNodeController = GetNodeControllerFromAnchor(inputAnchor, controller);
                                if (targetNodeController != null)
                                {
                                    var targetModel = VfxGraphReflectionHelpers.GetProperty(targetNodeController, "model") as UnityEngine.Object;
                                    var targetSlot = VfxGraphReflectionHelpers.GetProperty(inputAnchor, "model");
                                    var targetSlotName = targetSlot?.GetType().GetProperty("name")?.GetValue(targetSlot) as string;
                                    var targetSlotPath = targetSlot?.GetType().GetProperty("path")?.GetValue(targetSlot) as string;

                                    connections.Add(new Dictionary<string, object>
                                    {
                                        ["targetNodeId"] = targetModel?.GetInstanceID(),
                                        ["targetPortName"] = targetSlotName,
                                        ["targetPortPath"] = targetSlotPath,
                                        ["connectionType"] = "data"
                                    });
                                }
                            }
                            else if (!isOutput && inputAnchor == portController)
                            {
                                // This is an input port, find the source
                                var sourceNodeController = GetNodeControllerFromAnchor(outputAnchor, controller);
                                if (sourceNodeController != null)
                                {
                                    var sourceModel = VfxGraphReflectionHelpers.GetProperty(sourceNodeController, "model") as UnityEngine.Object;
                                    var sourceSlot = VfxGraphReflectionHelpers.GetProperty(outputAnchor, "model");
                                    var sourceSlotName = sourceSlot?.GetType().GetProperty("name")?.GetValue(sourceSlot) as string;
                                    var sourceSlotPath = sourceSlot?.GetType().GetProperty("path")?.GetValue(sourceSlot) as string;

                                    connections.Add(new Dictionary<string, object>
                                    {
                                        ["sourceNodeId"] = sourceModel?.GetInstanceID(),
                                        ["sourcePortName"] = sourceSlotName,
                                        ["sourcePortPath"] = sourceSlotPath,
                                        ["connectionType"] = "data"
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] Failed to get port connections: {ex.Message}");
            }

            return connections;
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

        private static object ConvertDefaultValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            var valueType = value.GetType();
            
            // Handle common Unity types
            if (valueType == typeof(Vector2))
            {
                var v = (Vector2)value;
                return new { x = v.x, y = v.y };
            }
            if (valueType == typeof(Vector3))
            {
                var v = (Vector3)value;
                return new { x = v.x, y = v.y, z = v.z };
            }
            if (valueType == typeof(Vector4))
            {
                var v = (Vector4)value;
                return new { x = v.x, y = v.y, z = v.z, w = v.w };
            }
            if (valueType == typeof(Color))
            {
                var c = (Color)value;
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            }

            // Return primitive types as-is
            if (valueType.IsPrimitive || valueType == typeof(string))
            {
                return value;
            }

            return value.ToString();
        }
    }
}

