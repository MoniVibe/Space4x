using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("disconnect_graph_nodes")]
    public static class DisconnectGraphNodesTool
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

                var resourceObject = resource as UnityEngine.Object;
                if (resourceObject != null)
                {
                    VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LoadIfNeeded");
                }

                var sourceNodeModelObject = VfxGraphReflectionHelpers.GetProperty(sourceNodeController, "model") as UnityEngine.Object;
                var targetNodeModelObject = VfxGraphReflectionHelpers.GetProperty(targetNodeController, "model") as UnityEngine.Object;

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

                var removed = false;

                // Strategy 1: Try RemoveLink with both anchors (mirroring CreateLink signature)
                // Try multiple method signatures
                var removeLinkMethods = controller.GetType()
                    .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .Where(m => m.Name == "RemoveLink")
                    .ToList();

                Debug.Log($"[MCP Tools] disconnect_graph_nodes: Found {removeLinkMethods.Count} RemoveLink overload(s).");
                if (removeLinkMethods.Count > 0)
                {
                    Debug.Log($"[MCP Tools] disconnect_graph_nodes: RemoveLink parameter counts => {string.Join(", ", removeLinkMethods.Select(m => m.GetParameters().Length))}");
                }

                foreach (var removeLinkMethod in removeLinkMethods)
                {
                    var paramCount = removeLinkMethod.GetParameters().Length;
                    Debug.Log($"[MCP Tools] disconnect_graph_nodes: Trying RemoveLink with {paramCount} parameters");
                    
                    try
                    {
                        object result = null;
                        if (paramCount == 1)
                        {
                            // Try with input anchor only
                            result = removeLinkMethod.Invoke(controller, new[] { inputAnchor });
                        }
                        else if (paramCount == 2)
                        {
                            // Try both orders: (input, output) and (output, input)
                            result = removeLinkMethod.Invoke(controller, new[] { inputAnchor, outputAnchor });
                            if (result is bool success && success)
                            {
                                removed = true;
                                Debug.Log("[MCP Tools] disconnect_graph_nodes: RemoveLink(inputAnchor, outputAnchor) succeeded.");
                                break;
                            }
                            else if (result == null)
                            {
                                removed = true;
                                Debug.Log("[MCP Tools] disconnect_graph_nodes: RemoveLink(inputAnchor, outputAnchor) completed (void return).");
                                break;
                            }
                            // Try reverse order
                            result = removeLinkMethod.Invoke(controller, new[] { outputAnchor, inputAnchor });
                        }

                        if (result is bool success2 && success2)
                        {
                            removed = true;
                            Debug.Log($"[MCP Tools] disconnect_graph_nodes: RemoveLink succeeded with {paramCount} parameters.");
                            break;
                        }
                        else if (result == null && paramCount == 1)
                        {
                            removed = true;
                            Debug.Log($"[MCP Tools] disconnect_graph_nodes: RemoveLink completed (void return, {paramCount} params).");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MCP Tools] RemoveLink({paramCount} params) threw: {ex.Message}");
                    }
                }

                // Strategy 2: Find edge by matching node IDs and port paths, then remove it
                if (!removed)
                {
                    // Sync controller from model first (like ListDataConnectionsTool does)
                    VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                    var syncMethod2 = controller.GetType().GetMethod("SyncControllerFromModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (syncMethod2 != null)
                    {
                        var syncArgs2 = new object[] { false };
                        syncMethod2.Invoke(controller, syncArgs2);
                    }

                    var sourceSlot = VfxGraphReflectionHelpers.GetProperty(outputAnchor, "model");
                    var targetSlot = VfxGraphReflectionHelpers.GetProperty(inputAnchor, "model");
                    var sourceSlotPath = GetSlotPath(sourceSlot);
                    var targetSlotPath = GetSlotPath(targetSlot);

                    Debug.Log($"[MCP Tools] disconnect_graph_nodes: Searching for edge: sourceNode={sourceNodeId}, sourcePath='{sourceSlotPath}' -> targetNode={targetNodeId}, targetPath='{targetSlotPath}'");

                    // Use property instead of field (like ListDataConnectionsTool)
                    var dataEdgesProperty = controller.GetType().GetProperty("dataEdges", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var dataEdges = dataEdgesProperty?.GetValue(controller) as IEnumerable;
                    if (dataEdges == null)
                    {
                        var dataEdgesField = controller.GetType().GetField("m_DataEdges", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        dataEdges = dataEdgesField?.GetValue(controller) as IEnumerable;
                    }

                    var edgeList = VfxGraphReflectionHelpers.Enumerate(dataEdges).Cast<object>().ToList();
                    Debug.Log($"[MCP Tools] disconnect_graph_nodes: Found {edgeList.Count} edges in dataEdges collection");

                    int edgeIndex = 0;
                    foreach (var edge in edgeList)
                    {
                        edgeIndex++;
                        var inputEdgeAnchor = VfxGraphReflectionHelpers.GetProperty(edge, "input");
                        var outputEdgeAnchor = VfxGraphReflectionHelpers.GetProperty(edge, "output");

                        if (inputEdgeAnchor == null || outputEdgeAnchor == null)
                        {
                            continue;
                        }

                        var edgeTypeName = edge.GetType().FullName ?? edge.GetType().Name;
                        Debug.Log($"[MCP Tools] disconnect_graph_nodes: Inspecting edge of type {edgeTypeName}");

                        // Get the node controllers for these anchors (use same method as ListDataConnectionsTool)
                        var inputNodeController = GetNodeControllerFromAnchor(inputEdgeAnchor, controller);
                        var outputNodeController = GetNodeControllerFromAnchor(outputEdgeAnchor, controller);

                        if (inputNodeController == null || outputNodeController == null)
                        {
                            // Only log every 10th skipped edge to avoid spam
                            if (edgeIndex % 10 == 0)
                            {
                                Debug.Log($"[MCP Tools] disconnect_graph_nodes: Edge {edgeIndex} missing node controllers");
                            }
                            continue;
                        }

                        // Get node IDs from the controllers
                        var inputNodeModel = VfxGraphReflectionHelpers.GetProperty(inputNodeController, "model") as UnityEngine.Object;
                        var outputNodeModel = VfxGraphReflectionHelpers.GetProperty(outputNodeController, "model") as UnityEngine.Object;

                        if (inputNodeModel == null || outputNodeModel == null)
                        {
                            // Only log every 10th skipped edge to avoid spam
                            if (edgeIndex % 10 == 0)
                            {
                                Debug.Log($"[MCP Tools] disconnect_graph_nodes: Edge {edgeIndex} missing node models");
                            }
                            continue;
                        }

                        var inputNodeId = inputNodeModel.GetInstanceID();
                        var outputNodeId = outputNodeModel.GetInstanceID();

                        // Get slot paths
                        var inputEdgeSlot = VfxGraphReflectionHelpers.GetProperty(inputEdgeAnchor, "model");
                        var outputEdgeSlot = VfxGraphReflectionHelpers.GetProperty(outputEdgeAnchor, "model");
                        var inputEdgePath = GetSlotPath(inputEdgeSlot);
                        var outputEdgePath = GetSlotPath(outputEdgeSlot);

                        Debug.Log($"[MCP Tools] disconnect_graph_nodes: Edge candidate: outputNode={outputNodeId} (want {sourceNodeId}), outputPath='{outputEdgePath}' (want '{sourceSlotPath}' or '{sourcePort}'), inputNode={inputNodeId} (want {targetNodeId}), inputPath='{inputEdgePath}' (want '{targetSlotPath}' or '{targetPort}')");

                        // Match: output from source node -> input to target node
                        // Primary match: compare controllers/models as well as IDs
                        bool controllerMatch = ReferenceEquals(outputNodeController, sourceNodeController) && ReferenceEquals(inputNodeController, targetNodeController);
                        bool modelMatch = ReferenceEquals(outputNodeModel, sourceNodeModelObject) && ReferenceEquals(inputNodeModel, targetNodeModelObject);
                        bool idMatch = outputNodeId == sourceNodeId && inputNodeId == targetNodeId;
                        bool nodeMatch = controllerMatch || modelMatch || idMatch;
                        
                        if (nodeMatch)
                        {
                            // If node IDs match, check port paths (be lenient with empty paths)
                            bool sourcePathMatch = string.IsNullOrEmpty(outputEdgePath) || 
                                                 string.IsNullOrEmpty(sourceSlotPath) || 
                                                 string.IsNullOrEmpty(sourcePort) ||
                                                 outputEdgePath == sourceSlotPath || 
                                                 outputEdgePath == sourcePort || 
                                                 sourceSlotPath == sourcePort;
                            bool targetPathMatch = string.IsNullOrEmpty(inputEdgePath) || 
                                                 string.IsNullOrEmpty(targetSlotPath) ||
                                                 string.IsNullOrEmpty(targetPort) ||
                                                 inputEdgePath == targetSlotPath || 
                                                 inputEdgePath == targetPort ||
                                                 targetSlotPath == targetPort;

                            // If paths are both empty, accept the match (node IDs are the primary identifier)
                            if (sourcePathMatch && targetPathMatch)
                            {
                                Debug.Log($"[MCP Tools] disconnect_graph_nodes: Found matching edge! Attempting removal...");
                            var inputAnchorType = inputEdgeAnchor?.GetType().FullName ?? "<null>";
                            var outputAnchorType = outputEdgeAnchor?.GetType().FullName ?? "<null>";
                            var inputSlotType = inputEdgeSlot?.GetType().FullName ?? "<null>";
                            var outputSlotType = outputEdgeSlot?.GetType().FullName ?? "<null>";
                            Debug.Log($"[MCP Tools] disconnect_graph_nodes: Anchor types => input:{inputAnchorType}, output:{outputAnchorType}");
                            Debug.Log($"[MCP Tools] disconnect_graph_nodes: Slot types => input:{inputSlotType}, output:{outputSlotType}");
                            if (inputEdgeSlot == null || outputEdgeSlot == null)
                            {
                                Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: One or both edge slots are null after model lookup.");
                            }

                                // Try RemoveLink with these specific anchors
                                var removeLinkMethod2 = controller.GetType()
                                    .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                                    .FirstOrDefault(m => m.Name == "RemoveLink" && m.GetParameters().Length == 2);

                                if (removeLinkMethod2 != null)
                                {
                                    try
                                    {
                                        var result = removeLinkMethod2.Invoke(controller, new[] { inputEdgeAnchor, outputEdgeAnchor });
                                        if (result is bool success && success)
                                        {
                                            removed = true;
                                            Debug.Log("[MCP Tools] disconnect_graph_nodes: RemoveLink with matched anchors succeeded.");
                                            break;
                                        }
                                        else if (result == null)
                                        {
                                            removed = true;
                                            Debug.Log("[MCP Tools] disconnect_graph_nodes: RemoveLink with matched anchors completed.");
                                            break;
                                        }
                                        else
                                        {
                                            Debug.Log($"[MCP Tools] disconnect_graph_nodes: RemoveLink with matched anchors returned {result}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[MCP Tools] RemoveLink with matched anchors threw: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: No 2-parameter RemoveLink method found for matched anchors.");
                                }

                                // Fallback: try edge.Remove() or slot.Unlink()
                            var edgeRemoveMethod = edge.GetType().GetMethod("Remove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                                if (edgeRemoveMethod != null)
                                {
                                    try
                                    {
                                    Debug.Log("[MCP Tools] disconnect_graph_nodes: Attempting edge.Remove()...");
                                        edgeRemoveMethod.Invoke(edge, null);
                                        removed = true;
                                        Debug.Log("[MCP Tools] disconnect_graph_nodes: Edge.Remove() succeeded.");
                                        break;
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[MCP Tools] Edge.Remove() threw: {ex.Message}");
                                    }
                                }
                            else
                            {
                                Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: Edge.Remove() method not found on edge type.");
                            }

                            // Try unlinking via slots
                            if (inputEdgeSlot != null && outputEdgeSlot != null)
                            {
                                var unlinkCandidates = inputEdgeSlot.GetType()
                                    .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                                    .Where(m => m.Name == "Unlink")
                                    .ToArray();

                                if (unlinkCandidates.Length == 0)
                                {
                                    Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: Slot.Unlink method not found on input slot type.");
                                }
                                else
                                {
                                    Debug.Log($"[MCP Tools] disconnect_graph_nodes: Slot.Unlink candidates => {string.Join(", ", unlinkCandidates.Select(m => $"{m.GetParameters().Length} params ({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"))}");

                                    foreach (var candidate in unlinkCandidates)
                                    {
                                        var parameters = candidate.GetParameters();
                                        Debug.Log($"[MCP Tools] disconnect_graph_nodes: Evaluating Slot.Unlink overload ({string.Join(", ", parameters.Select(p => p.ParameterType.FullName))})");
                                        object[] args;
                                        if (parameters.Length == 0)
                                        {
                                            args = Array.Empty<object>();
                                            Debug.Log("[MCP Tools] disconnect_graph_nodes: Attempting Slot.Unlink() with 0 params...");
                                        }
                                        else if (parameters.Length == 1)
                                        {
                                            var paramType = parameters[0].ParameterType;
                                            if (!paramType.IsInstanceOfType(outputEdgeSlot))
                                            {
                                                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Slot.Unlink parameter type {paramType.FullName} is not compatible with output slot type {outputEdgeSlot.GetType().FullName}.");
                                                continue;
                                            }

                                            args = new[] { outputEdgeSlot };
                                            Debug.Log("[MCP Tools] disconnect_graph_nodes: Attempting Slot.Unlink() with 1 param (output slot)...");
                                        }
                                        else if (parameters.Length == 2)
                                        {
                                            var firstType = parameters[0].ParameterType;
                                            var secondType = parameters[1].ParameterType;

                                            if (!firstType.IsInstanceOfType(outputEdgeSlot))
                                            {
                                                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Slot.Unlink first parameter type {firstType.FullName} is not compatible with output slot type {outputEdgeSlot.GetType().FullName}.");
                                                continue;
                                            }

                                            object secondArg = null;
                                            if (secondType == typeof(bool))
                                            {
                                                secondArg = false;
                                            }
                                            else if (secondType.IsInstanceOfType(inputEdgeSlot))
                                            {
                                                secondArg = inputEdgeSlot;
                                            }
                                            else
                                            {
                                                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Slot.Unlink second parameter type {secondType.FullName} not understood.");
                                                continue;
                                            }

                                            args = new[] { outputEdgeSlot, secondArg };
                                            Debug.Log("[MCP Tools] disconnect_graph_nodes: Attempting Slot.Unlink() with 2 params...");
                                        }
                                        else
                                        {
                                            Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Slot.Unlink candidate has unsupported parameter count {parameters.Length}.");
                                            continue;
                                        }

                                        try
                                        {
                                            candidate.Invoke(inputEdgeSlot, args);
                                            removed = true;
                                            Debug.Log("[MCP Tools] disconnect_graph_nodes: Slot.Unlink() invocation succeeded.");
                                            break;
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.LogWarning($"[MCP Tools] Slot.Unlink candidate threw: {ex.Message}");
                                        }
                                    }

                                    if (removed)
                                    {
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: Cannot attempt Slot.Unlink because a slot reference is null.");
                            }
                            }
                        }
                    }
                }

                if (!removed)
                {
                    Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: No matching edge found for removal.");
                }

                // Strategy 3: Fallback to model-based removal
                if (!removed)
                {
                    Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: Attempting removal via model slots.");
                    if (TryRemoveLinkFromModels(sourceNodeController, sourcePort, targetNodeController, targetPort))
                    {
                        Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: Removed link via model slots.");
                        removed = true;
                    }
                }

                if (!removed)
                {
                    return Response.Error("Connection not found or could not be removed");
                }

                // Use SyncAndSave helper for safe, guarded asset saving
                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                return Response.Success($"Disconnected nodes in graph {graphPath}", new
                {
                    graphPath,
                    sourceNodeId,
                    sourcePort,
                    targetNodeId,
                    targetPort
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to disconnect graph nodes: {ex.Message}");
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
            foreach (var portController in VfxGraphReflectionHelpers.Enumerate(portsEnumerable))
            {
                var slot = VfxGraphReflectionHelpers.GetProperty(portController, "model");
                var slotType = slot?.GetType();
                if (slotType == null)
                {
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
            }

            return null;
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

        private static bool TryRemoveLinkFromModels(object sourceNodeController, string sourcePort, object targetNodeController, string targetPort)
        {
            var sourceModel = VfxGraphReflectionHelpers.GetProperty(sourceNodeController, "model");
            var targetModel = VfxGraphReflectionHelpers.GetProperty(targetNodeController, "model");
            if (sourceModel == null || targetModel == null)
            {
                Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: Unable to access node models for removal.");
                return false;
            }

            var targetSlot = FindSlotOnModel(targetModel, targetPort, isOutput: false);
            if (targetSlot == null)
            {
                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Target slot '{targetPort}' not found on model {DescribeSlot(targetModel)}");
                return false;
            }

            var sourceSlot = FindSlotOnModel(sourceModel, sourcePort, isOutput: true);
            if (sourceSlot == null)
            {
                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Source slot '{sourcePort}' not found on model {DescribeSlot(sourceModel)}");
                return false;
            }

            var targetSlotType = targetSlot.GetType();
            var getNbLinks = targetSlotType.GetMethod("GetNbLinks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var getLink = targetSlotType.GetMethod("GetLink", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            if (getNbLinks != null && getLink != null)
            {
                var linkCountObj = getNbLinks.Invoke(targetSlot, null);
                var linkCount = linkCountObj is int count ? count : 0;
                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Target slot {DescribeSlot(targetSlot)} has {linkCount} link(s) via GetNbLinks.");
                if (linkCount > 0)
                {
                    for (int i = 0; i < linkCount; i++)
                    {
                        var linkedSlot = getLink.Invoke(targetSlot, new object[] { i });
                        if (linkedSlot == null)
                        {
                            continue;
                        }

                        Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Found linked slot {DescribeSlot(linkedSlot)}");

                        if (!SlotsEquivalent(linkedSlot, sourceSlot))
                        {
                            continue;
                        }

                        Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: Slot match confirmed via model graph (multi-link).");

                        if (InvokeSlotUnlink(targetSlot, linkedSlot))
                        {
                            return true;
                        }

                        Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Matching slot found but unlink/remove failed. targetSlot={DescribeSlot(targetSlot)}, linkedSlot={DescribeSlot(linkedSlot)}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Slot type '{targetSlotType.FullName}' does not expose GetNbLinks/GetLink. Attempting 'link' property fallback.");
                DumpSlotMembers(targetSlotType);

                var linkProperty = targetSlotType.GetProperty("link", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                                  ?? targetSlotType.GetProperty("LinkedSlot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (linkProperty != null)
                {
                    var linkedSlot = linkProperty.GetValue(targetSlot);
                    if (linkedSlot != null)
                    {
                        Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: 'link' property returned {DescribeSlot(linkedSlot)}");
                        if (SlotsEquivalent(linkedSlot, sourceSlot))
                        {
                            Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: Slot match confirmed via 'link' property.");
                            if (InvokeSlotUnlink(targetSlot, linkedSlot))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: 'link' property does not match source slot.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[MCP Tools] disconnect_graph_nodes: 'link' property returned null.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: No 'link' property found on slot type '{targetSlotType.FullName}'.");
                    DumpSlotMembers(targetSlotType);
                }
            }

            return false;
        }

        private static bool InvokeSlotUnlink(object targetSlot, object linkedSlot)
        {
            var targetSlotType = targetSlot.GetType();
            var linkedSlotType = linkedSlot?.GetType();
            if (linkedSlotType == null)
            {
                return false;
            }

            var unlinkMethod = targetSlotType.GetMethod("Unlink", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { linkedSlotType }, null)
                               ?? targetSlotType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                                   .FirstOrDefault(m => m.Name == "Unlink" && m.GetParameters().Length == 1);
            if (unlinkMethod != null)
            {
                unlinkMethod.Invoke(targetSlot, new[] { linkedSlot });
                return true;
            }

            var removeMethod = targetSlotType.GetMethod("RemoveLink", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { linkedSlotType }, null);
            if (removeMethod != null)
            {
                removeMethod.Invoke(targetSlot, new[] { linkedSlot });
                return true;
            }

            var linkedUnlink = linkedSlotType.GetMethod("Unlink", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { targetSlotType }, null)
                               ?? linkedSlotType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                                   .FirstOrDefault(m => m.Name == "Unlink" && m.GetParameters().Length == 1);
            if (linkedUnlink != null)
            {
                linkedUnlink.Invoke(linkedSlot, new[] { targetSlot });
                return true;
            }

            return false;
        }

        private static object GetNodeControllerFromAnchor(object anchor, object controller)
        {
            try
            {
                var ownerProperty = anchor?.GetType().GetProperty("owner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
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

        private static void DumpSlotMembers(Type slotType)
        {
            try
            {
                var props = slotType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var methods = slotType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                
                string FormatPropertySample(System.Reflection.PropertyInfo[] items)
                {
                    var names = items.Select(p => p.Name).ToArray();
                    if (names.Length <= 10)
                    {
                        return string.Join(", ", names);
                    }
                    return string.Join(", ", names.Take(10)) + $", ... (+{names.Length - 10} more)";
                }
                
                string FormatMethodSample(System.Reflection.MethodInfo[] items)
                {
                    var names = items.Select(m => m.Name).Distinct().ToArray();
                    if (names.Length <= 10)
                    {
                        return string.Join(", ", names);
                    }
                    return string.Join(", ", names.Take(10)) + $", ... (+{names.Length - 10} more)";
                }
                
                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Slot type '{slotType.FullName}' has {props.Length} properties: {FormatPropertySample(props)}");
                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Slot type '{slotType.FullName}' has {methods.Length} methods: {FormatMethodSample(methods)}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] disconnect_graph_nodes: Failed to dump slot members: {ex.Message}");
            }
        }

        private static object FindSlotOnModel(object model, string portName, bool isOutput)
        {
            if (model == null)
            {
                return null;
            }

            var propertyName = isOutput ? "outputSlots" : "inputSlots";
            var slotsEnumerable = model.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(model);
            if (slotsEnumerable == null)
            {
                return null;
            }

            var slots = VfxGraphReflectionHelpers.Enumerate(slotsEnumerable).Cast<object>().ToList();
            if (int.TryParse(portName, out var index))
            {
                if (index >= 0 && index < slots.Count)
                {
                    return slots[index];
                }
            }

            foreach (var slot in slots)
            {
                var slotType = slot?.GetType();
                if (slotType == null)
                {
                    continue;
                }

                var name = slotType.GetProperty("name")?.GetValue(slot) as string;
                var path = slotType.GetProperty("path")?.GetValue(slot) as string;

                if (!string.IsNullOrEmpty(path) && string.Equals(path, portName, StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }

                if (!string.IsNullOrEmpty(name) && string.Equals(name, portName, StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }

            return null;
        }

        private static bool SlotsEquivalent(object candidate, object target)
        {
            if (candidate == null || target == null)
            {
                return false;
            }

            if (ReferenceEquals(candidate, target))
            {
                return true;
            }

            var candidateType = candidate.GetType();
            var targetType = target.GetType();

            var candidateOwner = candidateType.GetProperty("owner")?.GetValue(candidate);
            var targetOwner = targetType.GetProperty("owner")?.GetValue(target);
            if (candidateOwner != null && targetOwner != null && ReferenceEquals(candidateOwner, targetOwner))
            {
                var candidatePath = candidateType.GetProperty("path")?.GetValue(candidate) as string;
                var targetPath = targetType.GetProperty("path")?.GetValue(target) as string;
                if (!string.IsNullOrEmpty(candidatePath) && !string.IsNullOrEmpty(targetPath) &&
                    string.Equals(candidatePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var candidateName = candidateType.GetProperty("name")?.GetValue(candidate) as string;
                var targetName = targetType.GetProperty("name")?.GetValue(target) as string;
                if (!string.IsNullOrEmpty(candidateName) && string.Equals(candidateName, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var candidateOwnerUnity = candidateOwner as UnityEngine.Object;
            var targetOwnerUnity = targetOwner as UnityEngine.Object;
            if (candidateOwnerUnity != null && targetOwnerUnity != null && candidateOwnerUnity.GetInstanceID() == targetOwnerUnity.GetInstanceID())
            {
                var candidatePath = candidateType.GetProperty("path")?.GetValue(candidate) as string;
                var targetPath = targetType.GetProperty("path")?.GetValue(target) as string;
                if (!string.IsNullOrEmpty(candidatePath) && !string.IsNullOrEmpty(targetPath) &&
                    string.Equals(candidatePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetSlotPath(object slot)
        {
            if (slot == null)
            {
                return string.Empty;
            }

            var slotType = slot.GetType();
            var path = slotType.GetProperty("path")?.GetValue(slot) as string;
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            var name = slotType.GetProperty("name")?.GetValue(slot) as string;
            return name ?? string.Empty;
        }

        private static string DescribeSlot(object slot)
        {
            if (slot == null)
            {
                return "<null>";
            }

            var slotType = slot.GetType();
            var name = slotType.GetProperty("name")?.GetValue(slot) as string ?? string.Empty;
            var path = slotType.GetProperty("path")?.GetValue(slot) as string ?? string.Empty;
            var owner = slotType.GetProperty("owner")?.GetValue(slot);
            var ownerName = (owner as UnityEngine.Object)?.name ?? owner?.GetType().Name ?? "<unknown>";
            return $"Slot(name={name}, path={path}, owner={ownerName})";
        }

        private static bool AnchorsMatch(object candidate, object target)
        {
            if (candidate == null || target == null)
            {
                return false;
            }

            if (ReferenceEquals(candidate, target))
            {
                return true;
            }

            var candidateSlot = VfxGraphReflectionHelpers.GetProperty(candidate, "slot");
            var targetSlot = VfxGraphReflectionHelpers.GetProperty(target, "slot");
            if (candidateSlot != null && targetSlot != null)
            {
                if (ReferenceEquals(candidateSlot, targetSlot))
                {
                    return true;
                }

                if (SlotsEquivalent(candidateSlot, targetSlot))
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeAnchor(object anchor)
        {
            if (anchor == null)
            {
                return "<null>";
            }

            var slot = VfxGraphReflectionHelpers.GetProperty(anchor, "slot");
            if (slot != null)
            {
                return DescribeSlot(slot);
            }

            var slotType = slot?.GetType();
            var name = slotType?.GetProperty("name")?.GetValue(slot) as string ?? "";
            var path = slotType?.GetProperty("path")?.GetValue(slot) as string ?? "";
            return $"Slot(name={name}, path={path})";
        }
    }
}

