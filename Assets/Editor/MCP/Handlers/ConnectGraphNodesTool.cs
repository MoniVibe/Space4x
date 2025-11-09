using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("connect_graph_nodes")]
    public static class ConnectGraphNodesTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var sourceNodeToken = @params["source_node_id"];
                var sourcePort = @params["source_port"]?.ToString();
                var targetNodeToken = @params["target_node_id"];
                var targetPort = @params["target_port"]?.ToString();

                if (!TryParseNodeId(sourceNodeToken, out var sourceNodeId, out var error) ||
                    !TryParseNodeId(targetNodeToken, out var targetNodeId, out error))
                {
                    return Response.Error(error);
                }

                if (string.IsNullOrWhiteSpace(sourcePort) || string.IsNullOrWhiteSpace(targetPort))
                {
                    return Response.Error("source_port and target_port are required");
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
                controller.GetType().GetMethod("SyncControllerFromModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                var nodeMap = BuildNodeMap(controller);

                if (!nodeMap.TryGetValue(sourceNodeId, out var sourceNodeController))
                {
                    return Response.Error($"Source node with id {sourceNodeId} not found");
                }

                if (!nodeMap.TryGetValue(targetNodeId, out var targetNodeController))
                {
                    return Response.Error($"Target node with id {targetNodeId} not found");
                }

                var outputAnchor = FindPortController(sourceNodeController, sourcePort, true);
                if (outputAnchor == null)
                {
                    var availableOutputs = ListAvailablePorts(sourceNodeController, true);
                    return Response.Error($"Output port '{sourcePort}' not found on node {sourceNodeId}. Available output ports: {string.Join(", ", availableOutputs)}");
                }

                var inputAnchor = FindPortController(targetNodeController, targetPort, false);
                if (inputAnchor == null)
                {
                    var availableInputs = ListAvailablePorts(targetNodeController, false);
                    return Response.Error($"Input port '{targetPort}' not found on node {targetNodeId}. Available input ports: {string.Join(", ", availableInputs)}");
                }

                var createLinkMethod = controller.GetType()
                    .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "CreateLink" && m.GetParameters().Length == 3);

                if (createLinkMethod == null)
                {
                    return Response.Error("Unable to locate VFXViewController.CreateLink method");
                }

                var success = createLinkMethod.Invoke(controller, new[] { inputAnchor, outputAnchor, false }) as bool? ?? false;

                if (!success)
                {
                    return Response.Error("Unity reported that the connection could not be created (type mismatch or invalid link)");
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                controller.GetType().GetMethod("SyncControllerFromModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                EditorUtility.SetDirty(resource as UnityEngine.Object);
                VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();

                var sourceSlotInfo = DescribePort(outputAnchor);
                var targetSlotInfo = DescribePort(inputAnchor);

                return Response.Success($"Connected nodes in graph {graphPath}", new
                {
                    graphPath,
                    connection = new
                    {
                        sourceNodeId,
                        sourcePort = sourceSlotInfo,
                        targetNodeId,
                        targetPort = targetSlotInfo
                    }
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to connect graph nodes: {ex.Message}");
            }
        }

        private static bool TryParseNodeId(JToken token, out int id, out string error)
        {
            error = null;
            id = 0;

            if (token == null)
            {
                error = "source_node_id and target_node_id are required";
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

        private static object FindPortController(object nodeController, string portName, bool isOutput)
        {
            var propertyName = isOutput ? "outputPorts" : "inputPorts";
            var portsEnumerable = VfxGraphReflectionHelpers.GetProperty(nodeController, propertyName);

            // Try parsing as index first
            if (int.TryParse(portName, out int portIndex))
            {
                var portsList = VfxGraphReflectionHelpers.Enumerate(portsEnumerable).Cast<object>().ToList();
                if (portIndex >= 0 && portIndex < portsList.Count)
                {
                    return portsList[portIndex];
                }
            }

            // Otherwise search by name or path
            int currentIndex = 0;
            foreach (var portController in VfxGraphReflectionHelpers.Enumerate(portsEnumerable))
            {
                var slot = VfxGraphReflectionHelpers.GetProperty(portController, "model");
                var slotType = slot?.GetType();
                if (slotType == null)
                {
                    currentIndex++;
                    continue;
                }

                var name = slotType.GetProperty("name")?.GetValue(slot) as string;
                var path = slotType.GetProperty("path")?.GetValue(slot) as string;

                if (!string.IsNullOrEmpty(path) && string.Equals(path, portName, StringComparison.OrdinalIgnoreCase))
                {
                    return portController;
                }

                if (!string.IsNullOrEmpty(name) && string.Equals(name, portName, StringComparison.OrdinalIgnoreCase))
                {
                    return portController;
                }

                currentIndex++;
            }

            return null;
        }

        private static object DescribePort(object portController)
        {
            var slot = VfxGraphReflectionHelpers.GetProperty(portController, "model");
            var slotType = slot?.GetType();
            if (slotType == null)
            {
                return new { name = "Unknown", path = (string)null };
            }

            var name = slotType.GetProperty("name")?.GetValue(slot) as string;
            var path = slotType.GetProperty("path")?.GetValue(slot) as string;

            return new
            {
                name,
                path
            };
        }

        private static List<string> ListAvailablePorts(object nodeController, bool isOutput)
        {
            var ports = new List<string>();
            var propertyName = isOutput ? "outputPorts" : "inputPorts";
            var portsEnumerable = VfxGraphReflectionHelpers.GetProperty(nodeController, propertyName);

            int index = 0;
            foreach (var portController in VfxGraphReflectionHelpers.Enumerate(portsEnumerable))
            {
                var slot = VfxGraphReflectionHelpers.GetProperty(portController, "model");
                var slotType = slot?.GetType();
                if (slotType == null)
                {
                    ports.Add($"[{index}]");
                    index++;
                    continue;
                }

                var name = slotType.GetProperty("name")?.GetValue(slot) as string;
                var path = slotType.GetProperty("path")?.GetValue(slot) as string;

                if (!string.IsNullOrEmpty(path))
                {
                    ports.Add(path);
                }
                else if (!string.IsNullOrEmpty(name))
                {
                    ports.Add(name);
                }
                else
                {
                    ports.Add($"[{index}]");
                }
                index++;
            }

            return ports;
        }
    }
}


