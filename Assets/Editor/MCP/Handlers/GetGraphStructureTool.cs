using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections;
using System.Collections.Generic;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("get_graph_structure")]
    public static class GetGraphStructureTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();

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
                controller.GetType().GetMethod("SyncControllerFromModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                var controllerModel = VfxGraphReflectionHelpers.GetProperty(controller, "model");
                if (controllerModel != null)
                {
                    VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "ModelChanged", controllerModel as UnityEngine.Object ?? controllerModel);
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "GraphChanged");

                var graphAsset = string.IsNullOrEmpty(graphPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath);
                var graphType = DetermineGraphType(graphPath, graphAsset);

                var graph = VfxGraphReflectionHelpers.GetGraph(resource);
                // Reduced verbosity - only log if there's an issue
                // Debug.Log($"[MCP Tools] Graph object resolved: {(graph == null ? "null" : graph.GetType().FullName)}");

                var nodes = BuildNodeSummaries(controller);
                if (nodes.Count == 0 && graph != null)
                {
                    nodes = BuildNodesFromGraph(graph);
                }

                var dataEdges = BuildDataEdges(controller);
                if (dataEdges.Count == 0 && graph != null)
                {
                    dataEdges = BuildDataEdgesFromGraph(graph);
                }

                var flowEdges = BuildFlowEdges(controller);

                var structure = new
                {
                    nodeCount = nodes.Count,
                    dataConnectionCount = dataEdges.Count,
                    flowConnectionCount = flowEdges.Count,
                    nodes,
                    dataConnections = dataEdges,
                    flowConnections = flowEdges,
                    debug = new
                    {
                        controllerNodes = nodes.Count,
                        graphTypeName = graph?.GetType().FullName
                    }
                };

                return Response.Success($"Graph structure retrieved for {graphPath}", new
                {
                    graphPath,
                    graphType,
                    structure
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to get graph structure: {ex.Message}");
            }
        }

        private static List<Dictionary<string, object>> BuildNodeSummaries(object controller)
        {
            var result = new List<Dictionary<string, object>>();

            foreach (var nodeController in CollectNodeControllers(controller))
            {
                try
                {
                    var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model");
                    var unityObj = model as UnityEngine.Object;
                    var nodeId = unityObj != null ? unityObj.GetInstanceID() : nodeController.GetHashCode();
                    var nodeType = model?.GetType();

                    var position = Vector2.zero;
                    var positionProperty = nodeType?.GetProperty("position");
                    if (positionProperty != null)
                    {
                        var rawPosition = positionProperty.GetValue(model);
                        if (rawPosition is Vector2 typedPosition)
                        {
                            position = typedPosition;
                        }
                    }

                    var nodeInfo = new Dictionary<string, object>
                    {
                        ["id"] = nodeId,
                        ["name"] = unityObj?.name ?? nodeType?.Name ?? "Unknown",
                        ["type"] = nodeType?.FullName ?? nodeController.GetType().FullName,
                        ["position"] = new { x = position.x, y = position.y },
                        ["inputs"] = BuildPortSummaries(nodeController, "inputPorts", "input"),
                        ["outputs"] = BuildPortSummaries(nodeController, "outputPorts", "output")
                    };

                    var title = VfxGraphReflectionHelpers.GetProperty(nodeController, "title");
                    if (title is string titleString && !string.IsNullOrEmpty(titleString))
                    {
                        nodeInfo["title"] = titleString;
                    }

                    result.Add(nodeInfo);
                }
                catch (Exception nodeEx)
                {
                    Debug.LogWarning($"[MCP Tools] Failed to reflect VFX node: {nodeEx.Message}");
                }
            }

            return result;
        }

        private static List<Dictionary<string, object>> BuildPortSummaries(object nodeController, string propertyName, string direction)
        {
            var ports = new List<Dictionary<string, object>>();
            var portsEnumerable = VfxGraphReflectionHelpers.GetProperty(nodeController, propertyName);

            foreach (var portController in VfxGraphReflectionHelpers.Enumerate(portsEnumerable))
            {
                try
                {
                    var slot = VfxGraphReflectionHelpers.GetProperty(portController, "model");
                    var slotType = slot?.GetType();
                    var slotName = slotType?.GetProperty("name")?.GetValue(slot) as string;
                    var slotPath = slotType?.GetProperty("path")?.GetValue(slot) as string;
                    var portType = VfxGraphReflectionHelpers.GetProperty(portController, "portType") as Type;
                    var connected = VfxGraphReflectionHelpers.GetProperty(portController, "connected") as bool? ?? false;

                    ports.Add(new Dictionary<string, object>
                    {
                        ["name"] = slotName ?? "Unnamed",
                        ["path"] = slotPath,
                        ["direction"] = direction,
                        ["connected"] = connected,
                        ["portType"] = portType?.FullName
                    });
                }
                catch (Exception portEx)
                {
                    Debug.LogWarning($"[MCP Tools] Failed to reflect VFX port: {portEx.Message}");
                }
            }

            return ports;
        }

        private static List<Dictionary<string, object>> BuildDataEdges(object controller)
        {
            var edges = new List<Dictionary<string, object>>();

            var dataEdgesField = controller.GetType().GetField("m_DataEdges", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dataEdges = dataEdgesField?.GetValue(controller) as IEnumerable ?? VfxGraphReflectionHelpers.Enumerate(VfxGraphReflectionHelpers.GetProperty(controller, "dataEdges"));

            foreach (var edge in VfxGraphReflectionHelpers.Enumerate(dataEdges))
            {
                try
                {
                    var inputAnchor = VfxGraphReflectionHelpers.GetProperty(edge, "input");
                    var outputAnchor = VfxGraphReflectionHelpers.GetProperty(edge, "output");

                    var inputNode = VfxGraphReflectionHelpers.GetProperty(inputAnchor, "sourceNode");
                    var outputNode = VfxGraphReflectionHelpers.GetProperty(outputAnchor, "sourceNode");

                    var inputModel = VfxGraphReflectionHelpers.GetProperty(inputNode, "model") as UnityEngine.Object;
                    var outputModel = VfxGraphReflectionHelpers.GetProperty(outputNode, "model") as UnityEngine.Object;

                    var inputSlot = VfxGraphReflectionHelpers.GetProperty(inputAnchor, "model");
                    var outputSlot = VfxGraphReflectionHelpers.GetProperty(outputAnchor, "model");

                    var inputSlotType = inputSlot?.GetType();
                    var outputSlotType = outputSlot?.GetType();

                    edges.Add(new Dictionary<string, object>
                    {
                        ["sourceNodeId"] = outputModel?.GetInstanceID(),
                        ["sourcePort"] = outputSlotType?.GetProperty("name")?.GetValue(outputSlot) as string,
                        ["sourcePath"] = outputSlotType?.GetProperty("path")?.GetValue(outputSlot) as string,
                        ["targetNodeId"] = inputModel?.GetInstanceID(),
                        ["targetPort"] = inputSlotType?.GetProperty("name")?.GetValue(inputSlot) as string,
                        ["targetPath"] = inputSlotType?.GetProperty("path")?.GetValue(inputSlot) as string
                    });
                }
                catch (Exception edgeEx)
                {
                    Debug.LogWarning($"[MCP Tools] Failed to reflect VFX data edge: {edgeEx.Message}");
                }
            }

            return edges;
        }

        private static List<Dictionary<string, object>> BuildFlowEdges(object controller)
        {
            var edges = new List<Dictionary<string, object>>();

            var flowEdgesField = controller.GetType().GetField("m_FlowEdges", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var flowEdges = flowEdgesField?.GetValue(controller) as IEnumerable ?? VfxGraphReflectionHelpers.Enumerate(VfxGraphReflectionHelpers.GetProperty(controller, "flowEdges"));

            foreach (var edge in VfxGraphReflectionHelpers.Enumerate(flowEdges))
            {
                try
                {
                    var inputAnchor = VfxGraphReflectionHelpers.GetProperty(edge, "input");
                    var outputAnchor = VfxGraphReflectionHelpers.GetProperty(edge, "output");

                    var inputContextController = VfxGraphReflectionHelpers.GetProperty(inputAnchor, "context");
                    var outputContextController = VfxGraphReflectionHelpers.GetProperty(outputAnchor, "context");

                    var inputModel = VfxGraphReflectionHelpers.GetProperty(inputContextController, "model") as UnityEngine.Object;
                    var outputModel = VfxGraphReflectionHelpers.GetProperty(outputContextController, "model") as UnityEngine.Object;

                    var inputSlotIndex = VfxGraphReflectionHelpers.GetProperty(inputAnchor, "slotIndex") as int? ?? -1;
                    var outputSlotIndex = VfxGraphReflectionHelpers.GetProperty(outputAnchor, "slotIndex") as int? ?? -1;

                    edges.Add(new Dictionary<string, object>
                    {
                        ["sourceContextId"] = outputModel?.GetInstanceID(),
                        ["sourceSlotIndex"] = outputSlotIndex,
                        ["targetContextId"] = inputModel?.GetInstanceID(),
                        ["targetSlotIndex"] = inputSlotIndex
                    });
                }
                catch (Exception edgeEx)
                {
                    Debug.LogWarning($"[MCP Tools] Failed to reflect VFX flow edge: {edgeEx.Message}");
                }
            }

            return edges;
        }

        private static string DetermineGraphType(string path, UnityEngine.Object asset)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "Unknown";
            }

            if (path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
            {
                return "ShaderGraph";
            }
            if (path.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase))
            {
                return "VFXGraph";
            }
            if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && asset != null && asset.GetType().Name.Contains("ScriptGraph"))
            {
                return "VisualScripting";
            }

            return "Unknown";
        }

        private static List<Dictionary<string, object>> BuildNodesFromGraph(object graph)
        {
            var nodes = new List<Dictionary<string, object>>();
            if (graph == null)
            {
                return nodes;
            }

            var recursiveChildrenMethod = graph.GetType().GetMethod("GetRecursiveChildren", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (recursiveChildrenMethod?.Invoke(graph, null) is not IEnumerable children)
            {
                return nodes;
            }

            foreach (var model in children)
            {
                if (model == null || ReferenceEquals(model, graph))
                {
                    continue;
                }

                var unityObj = model as UnityEngine.Object;
                var modelType = model.GetType();

                var node = new Dictionary<string, object>
                {
                    ["id"] = unityObj?.GetInstanceID() ?? model.GetHashCode(),
                    ["name"] = unityObj?.name ?? modelType.Name,
                    ["type"] = modelType.FullName
                };

                var positionProperty = modelType.GetProperty("position", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (positionProperty != null)
                {
                    var rawPosition = positionProperty.GetValue(model);
                    if (rawPosition is Vector2 vectorPosition)
                    {
                        node["position"] = new { x = vectorPosition.x, y = vectorPosition.y };
                    }
                }

                var inputs = BuildSlotSummariesFromModel(model, "inputSlots");
                if (inputs.Count > 0)
                {
                    node["inputs"] = inputs;
                }

                var outputs = BuildSlotSummariesFromModel(model, "outputSlots");
                if (outputs.Count > 0)
                {
                    node["outputs"] = outputs;
                }

                if (!node.ContainsKey("inputs") && !node.ContainsKey("outputs"))
                {
                    // No slot data available; skip purely structural nodes (e.g. slot descriptors)
                    if (unityObj == null)
                    {
                        continue;
                    }
                }

                nodes.Add(node);
            }

            return nodes;
        }

        private static List<Dictionary<string, object>> BuildSlotSummariesFromModel(object model, string propertyName)
        {
            var slots = new List<Dictionary<string, object>>();

            var slotsProperty = model.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (slotsProperty?.GetValue(model) is not IEnumerable slotEnumerable)
            {
                return slots;
            }

            foreach (var slot in slotEnumerable)
            {
                if (slot == null)
                {
                    continue;
                }

                var slotType = slot.GetType();
                var slotInfo = new Dictionary<string, object>
                {
                    ["name"] = slotType.GetProperty("name")?.GetValue(slot) as string,
                    ["path"] = slotType.GetProperty("path")?.GetValue(slot) as string,
                    ["direction"] = slotType.GetProperty("direction")?.GetValue(slot)?.ToString()
                };

                var property = slotType.GetProperty("property")?.GetValue(slot);
                var portType = property?.GetType().GetProperty("type")?.GetValue(property) as Type;
                if (portType != null)
                {
                    slotInfo["portType"] = portType.FullName;
                }

                slots.Add(slotInfo);
            }

            return slots;
        }

        private static List<Dictionary<string, object>> BuildDataEdgesFromGraph(object graph)
        {
            var edges = new List<Dictionary<string, object>>();
            if (graph == null)
            {
                return edges;
            }

            var recursiveChildrenMethod = graph.GetType().GetMethod("GetRecursiveChildren", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (recursiveChildrenMethod?.Invoke(graph, null) is not IEnumerable children)
            {
                return edges;
            }

            foreach (var model in children)
            {
                if (model == null)
                {
                    continue;
                }

                var unityObj = model as UnityEngine.Object;
                var targetId = unityObj?.GetInstanceID() ?? model.GetHashCode();

                var inputSlotsProperty = model.GetType().GetProperty("inputSlots", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (inputSlotsProperty?.GetValue(model) is not IEnumerable inputSlots)
                {
                    continue;
                }

                foreach (var slot in inputSlots)
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    var slotType = slot.GetType();
                    var getNbLinks = slotType.GetMethod("GetNbLinks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var getLink = slotType.GetMethod("GetLink", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (getNbLinks == null || getLink == null)
                    {
                        continue;
                    }

                    var linkCountObj = getNbLinks.Invoke(slot, null);
                    var linkCount = linkCountObj is int count ? count : 0;

                    for (int i = 0; i < linkCount; i++)
                    {
                        var linkedSlot = getLink.Invoke(slot, new object[] { i });
                        if (linkedSlot == null)
                        {
                            continue;
                        }

                        var linkedSlotType = linkedSlot.GetType();
                        var owner = linkedSlotType.GetProperty("owner")?.GetValue(linkedSlot);
                        var ownerUnity = owner as UnityEngine.Object;
                        var sourceId = ownerUnity?.GetInstanceID() ?? owner?.GetHashCode();
                        if (sourceId == null)
                        {
                            continue;
                        }

                        edges.Add(new Dictionary<string, object>
                        {
                            ["sourceNodeId"] = sourceId,
                            ["sourcePort"] = linkedSlotType.GetProperty("name")?.GetValue(linkedSlot) as string,
                            ["sourcePath"] = linkedSlotType.GetProperty("path")?.GetValue(linkedSlot) as string,
                            ["targetNodeId"] = targetId,
                            ["targetPort"] = slotType.GetProperty("name")?.GetValue(slot) as string,
                            ["targetPath"] = slotType.GetProperty("path")?.GetValue(slot) as string
                        });
                    }
                }
            }

            return edges;
        }
        private static IEnumerable<object> CollectNodeControllers(object controller)
        {
            var syncedModelsField = controller.GetType().GetField("m_SyncedModels", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var syncedModels = syncedModelsField?.GetValue(controller) as IEnumerable;
            if (syncedModels != null)
            {
                foreach (var entry in syncedModels)
                {
                    var entryType = entry?.GetType();
                    var value = entryType?.GetProperty("Value")?.GetValue(entry) as IEnumerable;
                    if (value == null)
                    {
                        continue;
                    }

                    foreach (var nodeController in value)
                    {
                        if (nodeController != null)
                        {
                            yield return nodeController;
                        }
                    }
                }

                yield break;
            }

            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                yield return nodeController;
            }
        }
    }
}


