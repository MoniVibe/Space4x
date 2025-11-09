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
    [McpForUnityTool("disconnect_flow_contexts")]
    public static class DisconnectFlowContextsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var sourceContextToken = @params["source_context_id"];
                var sourceSlotIndex = @params["source_slot_index"]?.ToObject<int?>() ?? 0;
                var targetContextToken = @params["target_context_id"];
                var targetSlotIndex = @params["target_slot_index"]?.ToObject<int?>() ?? 0;

                if (!TryParseNodeId(sourceContextToken, out var sourceContextId, out var error) ||
                    !TryParseNodeId(targetContextToken, out var targetContextId, out error))
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

                var contextMap = BuildContextMap(controller);

                if (!contextMap.TryGetValue(sourceContextId, out var sourceContextController))
                {
                    return Response.Error($"Source context with id {sourceContextId} not found");
                }

                if (!contextMap.TryGetValue(targetContextId, out var targetContextController))
                {
                    return Response.Error($"Target context with id {targetContextId} not found");
                }

                var sourceFlowAnchor = GetFlowAnchor(sourceContextController, sourceSlotIndex, true);
                var targetFlowAnchor = GetFlowAnchor(targetContextController, targetSlotIndex, false);

                // Try to remove flow edge from the graph model first
                var graph = VfxGraphReflectionHelpers.GetGraph(resource);
                var sourceContextModel = VfxGraphReflectionHelpers.GetProperty(sourceContextController, "model");
                var targetContextModel = VfxGraphReflectionHelpers.GetProperty(targetContextController, "model");
                
                bool modelEdgeRemoved = false;
                if (graph != null && sourceContextModel != null && targetContextModel != null)
                {
                    // Try to unlink flow slots directly on the context models
                    var targetInputFlowSlot = GetModelFlowSlot(targetContextModel, false, targetSlotIndex);
                    var sourceOutputFlowSlot = GetModelFlowSlot(sourceContextModel, true, sourceSlotIndex);
                    
                    if (targetInputFlowSlot != null)
                    {
                        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                        var unlinkMethod = targetInputFlowSlot.GetType().GetMethod("Unlink", flags);
                        if (unlinkMethod != null)
                        {
                            try
                            {
                                var unlinkParams = unlinkMethod.GetParameters();
                                if (unlinkParams.Length == 0)
                                {
                                    unlinkMethod.Invoke(targetInputFlowSlot, null);
                                    modelEdgeRemoved = true;
                                    Debug.Log("[MCP Tools] disconnect_flow_contexts: Successfully unlinked flow slots on models");
                                }
                                else if (unlinkParams.Length == 1 && unlinkParams[0].ParameterType.IsInstanceOfType(sourceOutputFlowSlot))
                                {
                                    unlinkMethod.Invoke(targetInputFlowSlot, new[] { sourceOutputFlowSlot });
                                    modelEdgeRemoved = true;
                                    Debug.Log("[MCP Tools] disconnect_flow_contexts: Successfully unlinked flow slots on models (with param)");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[MCP Tools] disconnect_flow_contexts: Failed to unlink model slots: {ex.Message}");
                            }
                        }
                    }
                }

                // Find and remove the flow edge from controller if model removal failed
                object flowEdges = null;
                var controllerType = controller.GetType();
                var flowEdgesField = controllerType.GetField("m_FlowEdges", BindingFlags.Instance | BindingFlags.NonPublic);
                if (flowEdgesField != null)
                {
                    flowEdges = flowEdgesField.GetValue(controller);
                }

                if (flowEdges == null)
                {
                    var getFlowEdgesMethod = controllerType.GetMethod("get_flowEdges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    flowEdges = getFlowEdgesMethod?.Invoke(controller, null);
                }

                bool removed = modelEdgeRemoved;
                if (!removed && flowEdges != null)
                {
                    Debug.Log($"[MCP Tools] disconnect_flow_contexts: flowEdges collection type = {flowEdges.GetType().FullName}");
                    var edgesList = VfxGraphReflectionHelpers.Enumerate(flowEdges).Cast<object>().ToList();
                    Debug.Log($"[MCP Tools] disconnect_flow_contexts: Existing flow edges count = {edgesList.Count}");

                    var removeAtMethod = flowEdges.GetType().GetMethod("RemoveAt", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var removeMethod = flowEdges.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    for (int i = 0; i < edgesList.Count; i++)
                    {
                        var edge = edgesList[i];
                        var inputAnchor = VfxGraphReflectionHelpers.GetProperty(edge, "input");
                        var outputAnchor = VfxGraphReflectionHelpers.GetProperty(edge, "output");

                        if (ReferenceEquals(inputAnchor, targetFlowAnchor) && ReferenceEquals(outputAnchor, sourceFlowAnchor))
                        {
                            if (removeAtMethod != null)
                            {
                                removeAtMethod.Invoke(flowEdges, new object[] { i });
                                removed = true;
                            }
                            else if (removeMethod != null)
                            {
                                removeMethod.Invoke(flowEdges, new[] { edge });
                                removed = true;
                            }
                            else if (flowEdges is IList list)
                            {
                                list.Remove(edge);
                                removed = true;
                            }

                            if (removed)
                            {
                                Debug.Log("[MCP Tools] disconnect_flow_contexts: Flow edge removed via collection remove");
                                var updated = VfxGraphReflectionHelpers.Enumerate(flowEdges).Cast<object>().ToList();
                                Debug.Log($"[MCP Tools] disconnect_flow_contexts: Flow edges count after remove = {updated.Count}");
                                break;
                            }
                        }
                    }
                }

                if (!removed && !modelEdgeRemoved)
                {
                    // Try RemoveFlowLink method
                    var removeFlowLinkMethod = controller.GetType()
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "RemoveFlowLink" || (m.Name == "RemoveLink" && m.GetParameters().Length >= 1));

                    if (removeFlowLinkMethod != null)
                    {
                        try
                        {
                            removeFlowLinkMethod.Invoke(controller, new object[] { targetFlowAnchor });
                            removed = true;
                        }
                        catch
                        {
                            try
                            {
                                removeFlowLinkMethod.Invoke(controller, new object[] { sourceFlowAnchor });
                                removed = true;
                            }
                            catch { }
                        }
                    }
                }

                if (!removed)
                {
                    return Response.Error("Flow connection not found or could not be removed");
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                controllerType.GetMethod("RecreateFlowEdges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, null);
                controllerType.GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                EditorUtility.SetDirty(resource as UnityEngine.Object);
                VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();

                return Response.Success($"Disconnected flow contexts in graph {graphPath}", new
                {
                    graphPath,
                    sourceContextId,
                    sourceSlotIndex,
                    targetContextId,
                    targetSlotIndex
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to disconnect flow contexts: {ex.Message}");
            }
        }

        private static bool TryParseNodeId(JToken token, out int id, out string error)
        {
            error = null;
            id = 0;

            if (token == null)
            {
                error = "context_id is required";
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

            error = $"Unable to parse context id '{asString}'";
            return false;
        }

        private static Dictionary<int, object> BuildContextMap(object controller)
        {
            var map = new Dictionary<int, object>();
            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model != null)
                {
                    var modelType = model.GetType();
                    var vfxContextType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContext");
                    if (vfxContextType != null && vfxContextType.IsAssignableFrom(modelType))
                    {
                        map[model.GetInstanceID()] = nodeController;
                    }
                }
            }
            return map;
        }

        private static object GetModelFlowSlot(object contextModel, bool isOutput, int slotIndex)
        {
            var contextType = contextModel.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            
            // Try to get flow slot from model
            var slotPropertyName = isOutput ? "outputFlowSlot" : "inputFlowSlot";
            var slotMethodName = isOutput ? "get_outputFlowSlot" : "get_inputFlowSlot";
            
            // Try property first
            var slotProperty = contextType.GetProperty(slotPropertyName, flags);
            if (slotProperty != null)
            {
                var slot = slotProperty.GetValue(contextModel);
                if (slot != null)
                {
                    // Check if it's an array/collection
                    if (slot is IEnumerable slotEnum)
                    {
                        var slots = VfxGraphReflectionHelpers.Enumerate(slotEnum).Cast<object>().ToList();
                        if (slotIndex >= 0 && slotIndex < slots.Count)
                        {
                            return slots[slotIndex];
                        }
                    }
                    else
                    {
                        // Single slot
                        return slotIndex == 0 ? slot : null;
                    }
                }
            }
            
            // Try method
            var slotMethod = contextType.GetMethod(slotMethodName, flags);
            if (slotMethod != null)
            {
                try
                {
                    var slot = slotMethod.Invoke(contextModel, null);
                    if (slot != null)
                    {
                        if (slot is IEnumerable slotEnum)
                        {
                            var slots = VfxGraphReflectionHelpers.Enumerate(slotEnum).Cast<object>().ToList();
                            if (slotIndex >= 0 && slotIndex < slots.Count)
                            {
                                return slots[slotIndex];
                            }
                        }
                        else
                        {
                            return slotIndex == 0 ? slot : null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] disconnect_flow_contexts: Failed to invoke {slotMethodName}: {ex.Message}");
                }
            }
            
            return null;
        }

        private static object GetFlowAnchor(object contextController, int slotIndex, bool isOutput)
        {
            var propertyName = isOutput ? "outputFlowAnchors" : "inputFlowAnchors";
            var anchorsEnumerable = VfxGraphReflectionHelpers.GetProperty(contextController, propertyName);

            if (anchorsEnumerable == null)
            {
                var alternateNames = isOutput
                    ? new[] { "flowOutputAnchors", "outputFlowSlots", "flowOutputSlotControllers" }
                    : new[] { "flowInputAnchors", "inputFlowSlots", "flowInputSlotControllers" };

                foreach (var name in alternateNames)
                {
                    anchorsEnumerable = VfxGraphReflectionHelpers.GetProperty(contextController, name);
                    if (anchorsEnumerable != null)
                    {
                        Debug.Log($"[MCP Tools] disconnect_flow_contexts: Using alternate flow anchor property '{name}' (isOutput={isOutput})");
                        break;
                    }
                }
            }

            var anchorsList = VfxGraphReflectionHelpers.Enumerate(anchorsEnumerable).Cast<object>().ToList();
            if (slotIndex >= 0 && slotIndex < anchorsList.Count)
            {
                return anchorsList[slotIndex];
            }

            return null;
        }
    }
}

