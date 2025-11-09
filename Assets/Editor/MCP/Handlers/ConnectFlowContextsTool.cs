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
    [McpForUnityTool("connect_flow_contexts")]
    public static class ConnectFlowContextsTool
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

                if (@params["__debug"]?.ToObject<bool?>() == true)
                {
                    LogControllerFlowMethods(controller);
                    LogGraphFlowInfo(resource);
                    LogFlowAnchorDiagnostics("source", sourceContextId, sourceContextController);
                    LogFlowAnchorDiagnostics("target", targetContextId, targetContextController);
                }

                // Get flow anchor controllers
                var sourceFlowAnchor = GetFlowAnchor(sourceContextController, sourceSlotIndex, true);
                if (sourceFlowAnchor == null)
                {
                    return Response.Error($"Source flow anchor at slot index {sourceSlotIndex} not found on context {sourceContextId}");
                }

                var targetFlowAnchor = GetFlowAnchor(targetContextController, targetSlotIndex, false);
                if (targetFlowAnchor == null)
                {
                    return Response.Error($"Target flow anchor at slot index {targetSlotIndex} not found on context {targetContextId}");
                }

                if (@params["__debug"]?.ToObject<bool?>() == true)
                {
                    LogAnchorDetails("source", sourceFlowAnchor);
                    LogAnchorDetails("target", targetFlowAnchor);
                }

                // Try to add flow edge to the graph model first
                var graph = VfxGraphReflectionHelpers.GetGraph(resource);
                var sourceContextModel = VfxGraphReflectionHelpers.GetProperty(sourceContextController, "model");
                var targetContextModel = VfxGraphReflectionHelpers.GetProperty(targetContextController, "model");

                object sourceOutputFlowSlot = null;
                object targetInputFlowSlot = null;
                bool modelEdgeAdded = false;
                if (graph != null && sourceContextModel != null && targetContextModel != null)
                {
                    // Try to link flow slots directly on the context models
                    var sourceContextType = sourceContextModel.GetType();
                    var targetContextType = targetContextModel.GetType();
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    
                    // Get flow slots from models
                    sourceOutputFlowSlot = GetModelFlowSlot(sourceContextModel, true, sourceSlotIndex);
                    targetInputFlowSlot = GetModelFlowSlot(targetContextModel, false, targetSlotIndex);
                    
                    if (sourceOutputFlowSlot != null && targetInputFlowSlot != null)
                    {
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Found model flow slots - source: {sourceOutputFlowSlot.GetType().FullName}, target: {targetInputFlowSlot.GetType().FullName}");
                        
                        // Inspect slot properties to understand connection model
                        var slotType = targetInputFlowSlot.GetType();
                        var slotProps = slotType.GetProperties(flags)
                            .Select(p => $"{p.Name}: {p.PropertyType.Name}")
                            .ToArray();
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Target slot properties: {string.Join(", ", slotProps)}");
                        
                        var sourceSlotType = sourceOutputFlowSlot.GetType();
                        var sourceSlotProps = sourceSlotType.GetProperties(flags)
                            .Select(p => $"{p.Name}: {p.PropertyType.Name}")
                            .ToArray();
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Source slot properties: {string.Join(", ", sourceSlotProps)}");
                        
                        // List all methods on the target slot for debugging
                        var allMethods = slotType.GetMethods(flags)
                            .Where(m => m.Name.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                       m.Name.IndexOf("Connect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       m.Name.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       m.Name.IndexOf("Set", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                            .ToArray();
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Target slot methods (Link/Connect/Add/Set): {string.Join(", ", allMethods)}");
                        
                        // Try setting a linkedSlot or connectedSlot property
                        var linkedSlotProp = slotType.GetProperty("linkedSlot", flags) ?? 
                                           slotType.GetProperty("connectedSlot", flags) ??
                                           slotType.GetProperty("link", flags);
                        if (linkedSlotProp != null && linkedSlotProp.CanWrite)
                        {
                            try
                            {
                                linkedSlotProp.SetValue(targetInputFlowSlot, sourceOutputFlowSlot);
                                modelEdgeAdded = true;
                                Debug.Log($"[MCP Tools] connect_flow_contexts: Successfully set {linkedSlotProp.Name} property");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to set linkedSlot property: {ex.Message}");
                            }
                        }
                        
                        // Try to link the slots via method
                        var linkMethod = slotType.GetMethod("Link", flags);
                        if (linkMethod != null && !modelEdgeAdded)
                        {
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Found Link method with {linkMethod.GetParameters().Length} parameters");
                            try
                            {
                                var linkParams = linkMethod.GetParameters();
                                if (linkParams.Length == 1 && linkParams[0].ParameterType.IsInstanceOfType(sourceOutputFlowSlot))
                                {
                                    linkMethod.Invoke(targetInputFlowSlot, new[] { sourceOutputFlowSlot });
                                    modelEdgeAdded = true;
                                    Debug.Log("[MCP Tools] connect_flow_contexts: Successfully linked flow slots on models");
                                }
                                else if (linkParams.Length == 2)
                                {
                                    // Try with two parameters (slot, bool)
                                    linkMethod.Invoke(targetInputFlowSlot, new[] { sourceOutputFlowSlot, false });
                                    modelEdgeAdded = true;
                                    Debug.Log("[MCP Tools] connect_flow_contexts: Successfully linked flow slots on models (2 params)");
                                }
                                else
                                {
                                    Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Link method has {linkParams.Length} parameters, expected 1 or 2");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to link model slots: {ex.Message}");
                            }
                        }
                        else if (linkMethod == null && !modelEdgeAdded)
                        {
                            Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Link method not found on {slotType.FullName}");
                        }
                    }
                    
                    // Also try adding to graph's flow edges collection if it exists
                    var graphType = graph.GetType();
                    var graphFlowField = graphType.GetField("m_FlowEdges", flags);
                    var graphFlowProperty = graphType.GetProperty("flowEdges", flags);
                    object graphFlowEdges = graphFlowField?.GetValue(graph) ?? graphFlowProperty?.GetValue(graph);
                    
                    if (graphFlowEdges != null && !modelEdgeAdded)
                    {
                        // Try to create a flow edge model and add it
                        var vfxFlowEdgeModelType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXFlowEdge");
                        if (vfxFlowEdgeModelType != null)
                        {
                            var modelConstructors = vfxFlowEdgeModelType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            foreach (var ctor in modelConstructors)
                            {
                                var parameters = ctor.GetParameters();
                                if (parameters.Length == 2 &&
                                    parameters[0].ParameterType.IsInstanceOfType(sourceOutputFlowSlot) &&
                                    parameters[1].ParameterType.IsInstanceOfType(targetInputFlowSlot))
                                {
                                    try
                                    {
                                        var modelEdge = ctor.Invoke(new[] { sourceOutputFlowSlot, targetInputFlowSlot });
                                        var graphAddMethod = graphFlowEdges.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                        if (graphAddMethod != null)
                                        {
                                            graphAddMethod.Invoke(graphFlowEdges, new[] { modelEdge });
                                            modelEdgeAdded = true;
                                            Debug.Log("[MCP Tools] connect_flow_contexts: Added flow edge to graph model");
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to add model edge: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }

                if (!modelEdgeAdded && sourceOutputFlowSlot != null && targetInputFlowSlot != null)
                {
                    if (TryModelLink(targetContextModel, sourceOutputFlowSlot, targetInputFlowSlot))
                    {
                        modelEdgeAdded = true;
                    }
                    else if (TryAddFlowEdgeFromModel(graph, sourceOutputFlowSlot, targetInputFlowSlot))
                    {
                        modelEdgeAdded = true;
                        Undo.RegisterCompleteObjectUndo(resource as UnityEngine.Object, "Connect Flow Contexts");
                    }
                }

                // Try to create flow link via controller
                var flowLinkMethods = controller.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "CreateFlowLink")
                    .ToArray();

                var flowLinkInvoked = false;
                foreach (var method in flowLinkMethods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length < 2)
                    {
                        continue;
                    }

                    object[] BuildArgs(bool reverse)
                    {
                        var args = new object[parameters.Length];
                        var firstAnchor = reverse ? sourceFlowAnchor : targetFlowAnchor;
                        var secondAnchor = reverse ? targetFlowAnchor : sourceFlowAnchor;

                        args[0] = firstAnchor;
                        args[1] = secondAnchor;

                        if (parameters.Length > 2)
                        {
                            // Assume optional bool for auto-add to selection
                            args[2] = false;
                        }

                        return args;
                    }

                    bool TryInvoke(bool reverse)
                    {
                        try
                        {
                            var args = BuildArgs(reverse);
                            if (!parameters[0].ParameterType.IsInstanceOfType(args[0]) ||
                                !parameters[1].ParameterType.IsInstanceOfType(args[1]))
                            {
                                return false;
                            }

                            var result = method.Invoke(controller, args);
                            if (result is bool success && !success)
                            {
                                Debug.LogWarning("[MCP Tools] connect_flow_contexts: CreateFlowLink returned false");
                                return false;
                            }

                            Debug.Log($"[MCP Tools] connect_flow_contexts: CreateFlowLink succeeded via '{method.Name}' (reverse={reverse})");
                            return true;
                        }
                        catch (TargetInvocationException tie)
                        {
                            Debug.LogWarning($"[MCP Tools] connect_flow_contexts: CreateFlowLink threw {tie.InnerException?.Message ?? tie.Message}");
                            return false;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[MCP Tools] connect_flow_contexts: CreateFlowLink error {ex.Message}");
                            return false;
                        }
                    }

                    if (TryInvoke(reverse: false) || TryInvoke(reverse: true))
                    {
                        flowLinkInvoked = true;
                        // Ensure changes are applied after creating flow link
                        VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "ApplyChanges");
                        Debug.Log("[MCP Tools] connect_flow_contexts: ApplyChanges called after CreateFlowLink");
                        break;
                    }
                }

                if (!flowLinkInvoked && !modelEdgeAdded)
                {
                    // Fallback: try to add flow edge directly via controller-managed list
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

                    if (flowEdges == null)
                    {
                        return Response.Error("Unable to locate flow edges collection on controller");
                    }

                    Debug.Log($"[MCP Tools] connect_flow_contexts: flowEdges collection type = {flowEdges.GetType().FullName}");

                    var flowEdgeList = VfxGraphReflectionHelpers.Enumerate(flowEdges).Cast<object>().ToList();
                    Debug.Log($"[MCP Tools] connect_flow_contexts: Existing flow edges count = {flowEdgeList.Count}");
                    if (flowEdgeList.Count > 0)
                    {
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Flow edge type sample = {flowEdgeList[0].GetType().FullName}");
                    }

                    var controllerAddMethod = flowEdges.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (controllerAddMethod == null)
                    {
                        return Response.Error("Unable to locate Add method on flow edges collection");
                    }

                    var flowEdgeType = flowEdgeList.FirstOrDefault()?.GetType() ?? VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.UI.VFXFlowEdgeController") ?? VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.UI.VFXFlowEdge");
                    if (flowEdgeType == null)
                    {
                        return Response.Error("Unable to determine flow edge type");
                    }

                    var controllerConstructors = flowEdgeType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Debug.Log($"[MCP Tools] connect_flow_contexts: Flow edge constructors => {string.Join("; ", controllerConstructors.Select(c => string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name))))}");

                    object newFlowEdge = null;
                    foreach (var ctor in controllerConstructors)
                    {
                        var parameters = ctor.GetParameters();
                        if (parameters.Length == 3 &&
                            parameters[0].ParameterType.IsInstanceOfType(controller) &&
                            parameters[1].ParameterType.IsInstanceOfType(sourceFlowAnchor) &&
                            parameters[2].ParameterType.IsInstanceOfType(targetFlowAnchor))
                        {
                            newFlowEdge = ctor.Invoke(new[] { controller, sourceFlowAnchor, targetFlowAnchor });
                            break;
                        }

                        if (parameters.Length == 3 &&
                            parameters[0].ParameterType.IsInstanceOfType(controller) &&
                            parameters[1].ParameterType.IsInstanceOfType(targetFlowAnchor) &&
                            parameters[2].ParameterType.IsInstanceOfType(sourceFlowAnchor))
                        {
                            newFlowEdge = ctor.Invoke(new[] { controller, targetFlowAnchor, sourceFlowAnchor });
                            break;
                        }

                        if (parameters.Length == 2 &&
                            parameters[0].ParameterType.IsInstanceOfType(targetFlowAnchor) &&
                            parameters[1].ParameterType.IsInstanceOfType(sourceFlowAnchor))
                        {
                            newFlowEdge = ctor.Invoke(new[] { targetFlowAnchor, sourceFlowAnchor });
                            break;
                        }

                        if (parameters.Length == 2 &&
                            parameters[0].ParameterType.IsInstanceOfType(sourceFlowAnchor) &&
                            parameters[1].ParameterType.IsInstanceOfType(targetFlowAnchor))
                        {
                            newFlowEdge = ctor.Invoke(new[] { sourceFlowAnchor, targetFlowAnchor });
                            break;
                        }
                    }

                    if (newFlowEdge == null)
                    {
                        return Response.Error("Unable to instantiate flow edge with available constructors");
                    }

                    controllerAddMethod.Invoke(flowEdges, new[] { newFlowEdge });
                    Debug.Log("[MCP Tools] connect_flow_contexts: Flow edge added via fallback collection Add");

                    var updatedEdges = VfxGraphReflectionHelpers.Enumerate(flowEdges).Cast<object>().ToList();
                    Debug.Log($"[MCP Tools] connect_flow_contexts: Flow edges count after add = {updatedEdges.Count}");

                    // Register with Undo system to ensure proper tracking
                    Undo.RegisterCompleteObjectUndo(resource as UnityEngine.Object, "Connect Flow Contexts");
                    
                    // DO NOT call ApplyChanges here - it will rebuild the controller from the model and wipe out our changes
                    // Instead, mark dirty and save - the flow edge should persist if it's properly linked to the model
                    EditorUtility.SetDirty(resource as UnityEngine.Object);
                    
                    // Try to get the flow edge's model reference and ensure it's added to the graph model
                    var flowEdgeControllerType = newFlowEdge.GetType();
                    Debug.Log($"[MCP Tools] connect_flow_contexts: Flow edge controller type = {flowEdgeControllerType.FullName}");
                    
                    // List all properties on the flow edge controller
                    var flowEdgeProps = flowEdgeControllerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(p => $"{p.Name}: {p.PropertyType.Name}")
                        .ToArray();
                    Debug.Log($"[MCP Tools] connect_flow_contexts: Flow edge controller properties: {string.Join(", ", flowEdgeProps)}");
                    
                    var flowEdgeModelProp = flowEdgeControllerType.GetProperty("model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (flowEdgeModelProp != null)
                    {
                        var flowEdgeModel = flowEdgeModelProp.GetValue(newFlowEdge);
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Flow edge model type = {flowEdgeModel?.GetType().FullName ?? "null"}");
                        
                        // Try to add the model to the graph's flow edges collection
                        var graphFlowField = graph.GetType().GetField("m_FlowEdges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var graphFlowProperty = graph.GetType().GetProperty("flowEdges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        object graphFlowEdges = graphFlowField?.GetValue(graph) ?? graphFlowProperty?.GetValue(graph);
                        
                        if (graphFlowEdges != null && flowEdgeModel != null)
                        {
                            var graphAddMethod = graphFlowEdges.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (graphAddMethod != null)
                            {
                                try
                                {
                                    graphAddMethod.Invoke(graphFlowEdges, new[] { flowEdgeModel });
                                    Debug.Log("[MCP Tools] connect_flow_contexts: Added flow edge model to graph");
                                    modelEdgeAdded = true;
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to add flow edge model to graph: {ex.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[MCP Tools] connect_flow_contexts: Flow edge controller does not have a 'model' property");
                        
                        // Try to find if flow edges are stored in the context models' flow slots
                        // Flow edges might be stored as references in the input/output flow slots
                        var sourceOutputSlot = GetModelFlowSlot(sourceContextModel, true, sourceSlotIndex);
                        var targetInputSlot = GetModelFlowSlot(targetContextModel, false, targetSlotIndex);
                        
                        if (sourceOutputSlot != null && targetInputSlot != null)
                        {
                            Debug.Log("[MCP Tools] connect_flow_contexts: Attempting to link flow slots directly on models");
                            
                            // Log all properties and fields on the flow slot models
                            var sourceSlotType = sourceOutputSlot.GetType();
                            var targetSlotType = targetInputSlot.GetType();
                            
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Source slot type: {sourceSlotType.FullName}");
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Target slot type: {targetSlotType.FullName}");
                            
                            var sourceSlotProps = sourceSlotType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Select(p => $"{p.Name}: {p.PropertyType.Name} ({(p.CanRead ? "R" : "")}{(p.CanWrite ? "W" : "")})")
                                .ToArray();
                            var targetSlotProps = targetSlotType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Select(p => $"{p.Name}: {p.PropertyType.Name} ({(p.CanRead ? "R" : "")}{(p.CanWrite ? "W" : "")})")
                                .ToArray();
                            
                            var sourceSlotFields = sourceSlotType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Select(f => $"{f.Name}: {f.FieldType.Name}")
                                .ToArray();
                            var targetSlotFields = targetSlotType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Select(f => $"{f.Name}: {f.FieldType.Name}")
                                .ToArray();
                            
                            var sourceSlotMethods = sourceSlotType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Where(m => m.Name.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                           m.Name.IndexOf("Connect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           m.Name.IndexOf("Set", StringComparison.OrdinalIgnoreCase) >= 0)
                                .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                                .ToArray();
                            var targetSlotMethods = targetSlotType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Where(m => m.Name.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                           m.Name.IndexOf("Connect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           m.Name.IndexOf("Set", StringComparison.OrdinalIgnoreCase) >= 0)
                                .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                                .ToArray();
                            
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Source slot properties: {string.Join(", ", sourceSlotProps)}");
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Target slot properties: {string.Join(", ", targetSlotProps)}");
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Source slot fields: {string.Join(", ", sourceSlotFields)}");
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Target slot fields: {string.Join(", ", targetSlotFields)}");
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Source slot methods: {string.Join(", ", sourceSlotMethods)}");
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Target slot methods: {string.Join(", ", targetSlotMethods)}");
                            
                            // Try to set a reference from target input slot to source output slot
                            var linkedSlotProp = targetSlotType.GetProperty("linkedSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                               targetSlotType.GetProperty("connectedSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                               targetSlotType.GetProperty("link", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                               targetSlotType.GetProperty("source", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                               targetSlotType.GetProperty("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            
                            // Also try fields - prioritize 'link' since we know it exists
                            var linkedSlotField = targetSlotType.GetField("link", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                                targetSlotType.GetField("linkedSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                                targetSlotType.GetField("m_linkedSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                                targetSlotType.GetField("connectedSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                                targetSlotType.GetField("m_connectedSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                                targetSlotType.GetField("source", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            
                            if (linkedSlotProp != null && linkedSlotProp.CanWrite)
                            {
                                try
                                {
                                    linkedSlotProp.SetValue(targetInputSlot, sourceOutputSlot);
                                    Debug.Log("[MCP Tools] connect_flow_contexts: Successfully set linkedSlot property on model");
                                    modelEdgeAdded = true;
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to set linkedSlot property: {ex.Message}");
                                }
                            }
                            else if (linkedSlotField != null)
                            {
                                try
                                {
                                    var linkList = linkedSlotField.GetValue(targetInputSlot);
                                    if (linkList != null)
                                    {
                                        // The link field is a List<VFXContextLink> - we need to create a VFXContextLink object
                                        var vfxContextLinkType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContextLink");
                                        if (vfxContextLinkType != null)
                                        {
                                            Debug.Log($"[MCP Tools] connect_flow_contexts: VFXContextLink type found: {vfxContextLinkType.FullName}");
                                            Debug.Log($"[MCP Tools] connect_flow_contexts: VFXContextLink is struct: {vfxContextLinkType.IsValueType}");
                                            
                                            // Check if it's a struct - if so, we can use Activator.CreateInstance or set fields directly
                                            if (vfxContextLinkType.IsValueType)
                                            {
                                                Debug.Log("[MCP Tools] connect_flow_contexts: VFXContextLink is a struct, trying Activator.CreateInstance");
                                                object contextLink = Activator.CreateInstance(vfxContextLinkType);
                                                
                                                // Inspect available fields/properties so we know what to populate
                                                var linkFields = vfxContextLinkType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                var linkProps = vfxContextLinkType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                
                                                Debug.Log($"[MCP Tools] connect_flow_contexts: VFXContextLink fields: {string.Join(", ", linkFields.Select(f => f.Name))}");
                                                Debug.Log($"[MCP Tools] connect_flow_contexts: VFXContextLink properties: {string.Join(", ", linkProps.Select(p => p.Name))}");
                                                
                                                // Populate required data: context reference and slot index
                                                var contextField = linkFields.FirstOrDefault(f => f.Name.IndexOf("context", StringComparison.OrdinalIgnoreCase) >= 0);
                                                var slotIndexField = linkFields.FirstOrDefault(f => f.Name.IndexOf("slotIndex", StringComparison.OrdinalIgnoreCase) >= 0);
                                                var contextProp = linkProps.FirstOrDefault(p => p.Name.IndexOf("context", StringComparison.OrdinalIgnoreCase) >= 0 && p.CanWrite);
                                                var slotIndexProp = linkProps.FirstOrDefault(p => p.Name.IndexOf("slotIndex", StringComparison.OrdinalIgnoreCase) >= 0 && p.CanWrite);
                                                
                                                var sourceContextModelObj = sourceContextModel;
                                                var slotIndexValue = sourceSlotIndex;

                                                Debug.Log($"[MCP Tools] connect_flow_contexts: source context model type = {sourceContextModelObj?.GetType().FullName ?? "null"}, slotIndex = {slotIndexValue}");
                                                
                                                bool populated = false;
                                                
                                                if (contextField != null)
                                                {
                                                    Debug.Log($"[MCP Tools] connect_flow_contexts: context field type = {contextField.FieldType.FullName}");
                                                    if (sourceContextModelObj != null && contextField.FieldType.IsInstanceOfType(sourceContextModelObj))
                                                    {
                                                        contextField.SetValue(contextLink, sourceContextModelObj);
                                                        populated = true;
                                                        Debug.Log("[MCP Tools] connect_flow_contexts: Set context field on VFXContextLink");
                                                    }
                                                    else
                                                    {
                                                        Debug.LogWarning("[MCP Tools] connect_flow_contexts: Source context model not assignable to context field");
                                                    }
                                                }
                                                else if (contextProp != null && contextProp.PropertyType.IsInstanceOfType(sourceContextModelObj))
                                                {
                                                    contextProp.SetValue(contextLink, sourceContextModelObj);
                                                    populated = true;
                                                    Debug.Log("[MCP Tools] connect_flow_contexts: Set context property on VFXContextLink");
                                                }
                                                
                                                if (slotIndexField != null)
                                                {
                                                    Debug.Log($"[MCP Tools] connect_flow_contexts: slotIndex field type = {slotIndexField.FieldType.FullName}");
                                                    if (slotIndexField.FieldType == typeof(int))
                                                    {
                                                        slotIndexField.SetValue(contextLink, slotIndexValue);
                                                        populated = true;
                                                        Debug.Log("[MCP Tools] connect_flow_contexts: Set slotIndex field on VFXContextLink");
                                                    }
                                                    else
                                                    {
                                                        Debug.LogWarning("[MCP Tools] connect_flow_contexts: slotIndex field not an int");
                                                    }
                                                }
                                                else if (slotIndexProp != null && slotIndexProp.PropertyType == typeof(int))
                                                {
                                                    slotIndexProp.SetValue(contextLink, slotIndexValue);
                                                    populated = true;
                                                    Debug.Log("[MCP Tools] connect_flow_contexts: Set slotIndex property on VFXContextLink");
                                                }
                                                
                                                if (!populated)
                                                {
                                                    Debug.LogWarning("[MCP Tools] connect_flow_contexts: Unable to populate VFXContextLink with context/slotIndex");
                                                }
                                                
                                                // Add the VFXContextLink to the list
                                                var addMethod = linkList.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                if (addMethod != null)
                                                {
                                                    addMethod.Invoke(linkList, new[] { contextLink });
                                                    Debug.Log("[MCP Tools] connect_flow_contexts: Successfully added VFXContextLink to target slot's link list");
                                                    modelEdgeAdded = true;
                                                }
                                                else
                                                {
                                                    Debug.LogWarning("[MCP Tools] connect_flow_contexts: Could not find Add method on link list");
                                                }
                                            }
                                            else
                                            {
                                                // Try to create a VFXContextLink with the source output slot
                                                var linkConstructors = vfxContextLinkType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                Debug.Log($"[MCP Tools] connect_flow_contexts: Found {linkConstructors.Length} VFXContextLink constructors");
                                                
                                                foreach (var ctor in linkConstructors)
                                                {
                                                    var parameters = ctor.GetParameters();
                                                    Debug.Log($"[MCP Tools] connect_flow_contexts: Constructor with {parameters.Length} parameters: {string.Join(", ", parameters.Select(p => p.ParameterType.Name))}");
                                                }
                                                
                                                object contextLink = null;
                                                
                                                foreach (var ctor in linkConstructors)
                                                {
                                                    var parameters = ctor.GetParameters();
                                                    if (parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(sourceOutputSlot))
                                                    {
                                                        contextLink = ctor.Invoke(new[] { sourceOutputSlot });
                                                        Debug.Log("[MCP Tools] connect_flow_contexts: Successfully created VFXContextLink");
                                                        break;
                                                    }
                                                }
                                                
                                                if (contextLink != null)
                                                {
                                                    // Add the VFXContextLink to the list
                                                    var addMethod = linkList.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                                    if (addMethod != null)
                                                    {
                                                        addMethod.Invoke(linkList, new[] { contextLink });
                                                        Debug.Log("[MCP Tools] connect_flow_contexts: Successfully added VFXContextLink to target slot's link list");
                                                        modelEdgeAdded = true;
                                                    }
                                                    else
                                                    {
                                                        Debug.LogWarning("[MCP Tools] connect_flow_contexts: Could not find Add method on link list");
                                                    }
                                                }
                                                else
                                                {
                                                    Debug.LogWarning("[MCP Tools] connect_flow_contexts: Could not create VFXContextLink object - no matching constructor found");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Debug.LogWarning("[MCP Tools] connect_flow_contexts: VFXContextLink type not found");
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogWarning("[MCP Tools] connect_flow_contexts: link field is null");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to set linkedSlot field: {ex.Message}");
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[MCP Tools] connect_flow_contexts: No writable linkedSlot property/field found on flow slot model");
                                
                                // Try to find a Link method on the slot
                                var linkMethod = targetSlotType.GetMethod("Link", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                                targetSlotType.GetMethod("Connect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                                                targetSlotType.GetMethod("SetLink", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                
                                if (linkMethod != null)
                                {
                                    Debug.Log($"[MCP Tools] connect_flow_contexts: Found Link method: {linkMethod.Name} with {linkMethod.GetParameters().Length} parameters");
                                    try
                                    {
                                        var linkParams = linkMethod.GetParameters();
                                        if (linkParams.Length == 1 && linkParams[0].ParameterType.IsInstanceOfType(sourceOutputSlot))
                                        {
                                            linkMethod.Invoke(targetInputSlot, new[] { sourceOutputSlot });
                                            Debug.Log("[MCP Tools] connect_flow_contexts: Successfully invoked Link method");
                                            modelEdgeAdded = true;
                                        }
                                        else if (linkParams.Length == 0)
                                        {
                                            linkMethod.Invoke(targetInputSlot, null);
                                            Debug.Log("[MCP Tools] connect_flow_contexts: Successfully invoked Link method (no params)");
                                            modelEdgeAdded = true;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to invoke Link method: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning("[MCP Tools] connect_flow_contexts: No Link method found on flow slot model");
                                }
                            }
                        }
                    }
                    
                    // Verify the edge persisted by checking again
                    var verifyEdges = VfxGraphReflectionHelpers.Enumerate(flowEdges).Cast<object>().ToList();
                    Debug.Log($"[MCP Tools] connect_flow_contexts: Flow edges count before save = {verifyEdges.Count}");

                    var recreateMethod = controllerType.GetMethod("RecreateFlowEdges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    // Delay recreation until after controllers have been synced to avoid losing the newly added edge
                    //recreateMethod?.Invoke(controller, null);
                }

                if (modelEdgeAdded)
                {
                    // If we added to the model, sync the controller from the model to reflect the change
                    VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                    var syncMethod = controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (syncMethod != null)
                    {
                        var syncParams = syncMethod.GetParameters();
                        if (syncParams.Length == 0)
                        {
                            syncMethod.Invoke(controller, null);
                        }
                        else
                        {
                            syncMethod.Invoke(controller, syncArgs);
                        }
                    }
                }
                else if (flowLinkInvoked)
                {
                    // If we added via controller, ApplyChanges should have already synced to model
                    // Just ensure the asset is marked dirty and saved
                    Debug.Log("[MCP Tools] connect_flow_contexts: Flow edge added via controller, ApplyChanges should have persisted it");
                }
                else
                {
                    // Fallback: try to apply changes if nothing else worked
                    VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                }

                EditorUtility.SetDirty(resource as UnityEngine.Object);
                VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();

                Debug.Log($"[MCP Tools] connect_flow_contexts: Flow connection recorded between {sourceContextId} -> {targetContextId}");

                return Response.Success($"Connected flow contexts in graph {graphPath}", new
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
                return Response.Error($"Failed to connect flow contexts: {ex.Message}");
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
                    // Check if this is a context (VFXContext or derived)
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
            
            Debug.Log($"[MCP Tools] connect_flow_contexts: GetModelFlowSlot - context type: {contextType.FullName}, isOutput: {isOutput}, slotIndex: {slotIndex}");
            
            // Try to get flow slot from model
            var slotPropertyName = isOutput ? "outputFlowSlot" : "inputFlowSlot";
            var slotMethodName = isOutput ? "get_outputFlowSlot" : "get_inputFlowSlot";
            
            // List all flow-related properties and methods for debugging
            var allFlowProps = contextType.GetProperties(flags)
                .Where(p => p.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(p => p.Name)
                .ToArray();
            var allFlowMethods = contextType.GetMethods(flags)
                .Where(m => m.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(m => m.Name)
                .ToArray();
            Debug.Log($"[MCP Tools] connect_flow_contexts: Model flow properties: {string.Join(", ", allFlowProps)}");
            Debug.Log($"[MCP Tools] connect_flow_contexts: Model flow methods: {string.Join(", ", allFlowMethods)}");
            
            // Try property first
            var slotProperty = contextType.GetProperty(slotPropertyName, flags);
            if (slotProperty != null)
            {
                Debug.Log($"[MCP Tools] connect_flow_contexts: Found property {slotPropertyName}");
                var slot = slotProperty.GetValue(contextModel);
                if (slot != null)
                {
                    Debug.Log($"[MCP Tools] connect_flow_contexts: Property value type: {slot.GetType().FullName}");
                    // Check if it's an array/collection
                    if (slot is IEnumerable slotEnum)
                    {
                        var slots = VfxGraphReflectionHelpers.Enumerate(slotEnum).Cast<object>().ToList();
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Slot collection count: {slots.Count}");
                        if (slotIndex >= 0 && slotIndex < slots.Count)
                        {
                            return slots[slotIndex];
                        }
                    }
                    else
                    {
                        // Single slot
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Single slot found");
                        return slotIndex == 0 ? slot : null;
                    }
                }
                else
                {
                    Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Property {slotPropertyName} returned null");
                }
            }
            else
            {
                Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Property {slotPropertyName} not found");
            }
            
            // Try method
            var slotMethod = contextType.GetMethod(slotMethodName, flags);
            if (slotMethod != null)
            {
                Debug.Log($"[MCP Tools] connect_flow_contexts: Found method {slotMethodName}");
                try
                {
                    var slot = slotMethod.Invoke(contextModel, null);
                    if (slot != null)
                    {
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Method returned type: {slot.GetType().FullName}");
                        if (slot is IEnumerable slotEnum)
                        {
                            var slots = VfxGraphReflectionHelpers.Enumerate(slotEnum).Cast<object>().ToList();
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Method slot collection count: {slots.Count}");
                            if (slotIndex >= 0 && slotIndex < slots.Count)
                            {
                                return slots[slotIndex];
                            }
                        }
                        else
                        {
                            Debug.Log($"[MCP Tools] connect_flow_contexts: Method returned single slot");
                            return slotIndex == 0 ? slot : null;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Method {slotMethodName} returned null");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to invoke {slotMethodName}: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Method {slotMethodName} not found");
            }
            
            Debug.LogWarning($"[MCP Tools] connect_flow_contexts: GetModelFlowSlot failed - no flow slot found");
            return null;
        }

        private static object GetFlowAnchor(object contextController, int slotIndex, bool isOutput)
        {
            var propertyName = isOutput ? "outputFlowAnchors" : "inputFlowAnchors";
            var anchorsEnumerable = VfxGraphReflectionHelpers.GetProperty(contextController, propertyName);

            if (anchorsEnumerable == null)
            {
                // Try fallback property names observed in newer Unity versions
                var alternateNames = isOutput
                    ? new[] { "flowOutputAnchors", "outputFlowSlots", "flowOutputSlotControllers" }
                    : new[] { "flowInputAnchors", "inputFlowSlots", "flowInputSlotControllers" };

                foreach (var name in alternateNames)
                {
                    anchorsEnumerable = VfxGraphReflectionHelpers.GetProperty(contextController, name);
                    if (anchorsEnumerable != null)
                    {
                        Debug.Log($"[MCP Tools] connect_flow_contexts: Using alternate flow anchor property '{name}' (isOutput={isOutput})");
                        break;
                    }
                }

                if (anchorsEnumerable == null)
                {
                    var type = contextController.GetType();
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var availableProps = type.GetProperties(flags)
                        .Select(p => p.Name)
                        .Where(n => n.IndexOf("flow", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToArray();
                    var availableFields = type.GetFields(flags)
                        .Select(f => f.Name)
                        .Where(n => n.IndexOf("flow", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToArray();
                    Debug.LogWarning($"[MCP Tools] connect_flow_contexts: No flow anchor property found on {type.FullName}. Flow-related properties: {string.Join(", ", availableProps)}; fields: {string.Join(", ", availableFields)}");
                }
            }

            var anchorsList = VfxGraphReflectionHelpers.Enumerate(anchorsEnumerable).Cast<object>().ToList();
            Debug.Log($"[MCP Tools] connect_flow_contexts: {(isOutput ? "output" : "input")} flow anchors count = {anchorsList.Count}");
            if (slotIndex >= 0 && slotIndex < anchorsList.Count)
            {
                return anchorsList[slotIndex];
            }

            return null;
        }

        private static void LogFlowAnchorDiagnostics(string label, int contextId, object contextController)
        {
            if (contextController == null)
            {
                Debug.LogWarning($"[MCP Tools] connect_flow_contexts: {label} context {contextId} controller is null");
                return;
            }

            var type = contextController.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var properties = type.GetProperties(flags)
                .Select(p => p.Name)
                .Where(n => n.IndexOf("flow", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            var fields = type.GetFields(flags)
                .Select(f => f.Name)
                .Where(n => n.IndexOf("flow", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            Debug.Log($"[MCP Tools] connect_flow_contexts: {label} context {contextId} controller type {type.FullName}");
            Debug.Log($"[MCP Tools] connect_flow_contexts: {label} flow properties => {string.Join(", ", properties)}");
            Debug.Log($"[MCP Tools] connect_flow_contexts: {label} flow fields => {string.Join(", ", fields)}");

            var model = VfxGraphReflectionHelpers.GetProperty(contextController, "model");
            if (model != null)
            {
                var modelType = model.GetType();
                var modelFields = modelType.GetFields(flags)
                    .Where(f => f.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(f => f.Name)
                    .ToArray();
                var modelProperties = modelType.GetProperties(flags)
                    .Where(p => p.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(p => p.Name)
                    .ToArray();
                var modelMethods = modelType.GetMethods(flags)
                    .Where(m => m.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(m => m.Name)
                    .ToArray();
                Debug.Log($"[MCP Tools] connect_flow_contexts: {label} model flow fields => {string.Join(", ", modelFields)}");
                Debug.Log($"[MCP Tools] connect_flow_contexts: {label} model flow properties => {string.Join(", ", modelProperties)}");
                Debug.Log($"[MCP Tools] connect_flow_contexts: {label} model flow methods => {string.Join(", ", modelMethods)}");

                var outputSlot = modelMethods.Contains("get_outputFlowSlot")
                    ? modelType.GetMethod("get_outputFlowSlot", flags)?.Invoke(model, null)
                    : null;
                var inputSlot = modelMethods.Contains("get_inputFlowSlot")
                    ? modelType.GetMethod("get_inputFlowSlot", flags)?.Invoke(model, null)
                    : null;
                if (outputSlot != null)
                {
                    if (outputSlot is IEnumerable outputEnum)
                    {
                        var slots = outputEnum.Cast<object>().ToList();
                        Debug.Log($"[MCP Tools] connect_flow_contexts: {label} output slot array count = {slots.Count}");
                        if (slots.Count > 0 && slots[0] != null)
                        {
                            var itemType = slots[0].GetType();
                            var itemMethods = itemType.GetMethods(flags)
                                .Where(m => m.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0)
                                .Select(m => m.Name)
                                .ToArray();
                            Debug.Log($"[MCP Tools] connect_flow_contexts: {label} output slot item type {itemType.FullName} methods => {string.Join(", ", itemMethods)}");
                        }
                    }
                    else
                    {
                        var slotType = outputSlot.GetType();
                        var slotMethods = slotType.GetMethods(flags)
                            .Where(m => m.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Select(m => m.Name)
                            .ToArray();
                        Debug.Log($"[MCP Tools] connect_flow_contexts: {label} output slot type {slotType.FullName} methods => {string.Join(", ", slotMethods)}");
                    }
                }

                if (inputSlot != null)
                {
                    if (inputSlot is IEnumerable inputEnum)
                    {
                        var slots = inputEnum.Cast<object>().ToList();
                        Debug.Log($"[MCP Tools] connect_flow_contexts: {label} input slot array count = {slots.Count}");
                        if (slots.Count > 0 && slots[0] != null)
                        {
                            var itemType = slots[0].GetType();
                            var itemMethods = itemType.GetMethods(flags)
                                .Where(m => m.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0)
                                .Select(m => m.Name)
                                .ToArray();
                            Debug.Log($"[MCP Tools] connect_flow_contexts: {label} input slot item type {itemType.FullName} methods => {string.Join(", ", itemMethods)}");
                        }
                    }
                    else
                    {
                        var slotType = inputSlot.GetType();
                        var slotMethods = slotType.GetMethods(flags)
                            .Where(m => m.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Select(m => m.Name)
                            .ToArray();
                        Debug.Log($"[MCP Tools] connect_flow_contexts: {label} input slot type {slotType.FullName} methods => {string.Join(", ", slotMethods)}");
                    }
                }
            }
        }

        private static void LogControllerFlowMethods(object controller)
        {
            if (controller == null)
            {
                return;
            }

            var type = controller.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var methodSignatures = type.GetMethods(flags)
                .Where(m => m.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(m =>
                {
                    var parameters = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
                    return $"{m.Name}({parameters})";
                })
                .ToArray();

            var flowEdgeMethods = type.GetMethods(flags)
                .Where(m => m.Name.IndexOf("FlowEdge", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(m =>
                {
                    var parameters = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
                    return $"{m.Name}({parameters})";
                })
                .ToArray();

            var fieldNames = type.GetFields(flags)
                .Where(f => f.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => f.Name)
                .ToArray();

            Debug.Log($"[MCP Tools] connect_flow_contexts: controller flow methods => {string.Join("; ", methodSignatures)}");
            Debug.Log($"[MCP Tools] connect_flow_contexts: controller flow fields => {string.Join("; ", fieldNames)}");
            Debug.Log($"[MCP Tools] connect_flow_contexts: controller flow-edge methods => {string.Join("; ", flowEdgeMethods)}");
        }

        private static void LogGraphFlowInfo(object resource)
        {
            var graph = VfxGraphReflectionHelpers.GetGraph(resource);
            if (graph == null)
            {
                Debug.LogWarning("[MCP Tools] connect_flow_contexts: Unable to locate graph model for flow logging");
                return;
            }

            var graphType = graph.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var flowFields = graphType.GetFields(flags)
                .Where(f => f.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => f.Name)
                .ToArray();
            var flowProperties = graphType.GetProperties(flags)
                .Where(p => p.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(p => p.Name)
                .ToArray();
            Debug.Log($"[MCP Tools] connect_flow_contexts: graph flow fields => {string.Join("; ", flowFields)}");
            Debug.Log($"[MCP Tools] connect_flow_contexts: graph flow properties => {string.Join("; ", flowProperties)}");

            var flowEdgesField = graphType.GetField("m_FlowEdges", flags);
            var graphFlowEdges = flowEdgesField?.GetValue(graph) as IEnumerable;
            if (graphFlowEdges == null)
            {
                var flowEdgesProperty = graphType.GetProperty("flowEdges", flags);
                graphFlowEdges = flowEdgesProperty?.GetValue(graph) as IEnumerable;
            }

            var count = graphFlowEdges != null ? VfxGraphReflectionHelpers.Enumerate(graphFlowEdges).Cast<object>().Count() : -1;
            Debug.Log($"[MCP Tools] connect_flow_contexts: graph flow edge count = {count}");
        }

        private static void LogAnchorDetails(string label, object anchor)
        {
            if (anchor == null)
            {
                Debug.LogWarning($"[MCP Tools] connect_flow_contexts: {label} anchor is null");
                return;
            }

            var type = anchor.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var properties = type.GetProperties(flags)
                .Where(p => p.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("slot", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(p => p.Name)
                .ToArray();
            var fields = type.GetFields(flags)
                .Where(f => f.Name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("slot", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => f.Name)
                .ToArray();
            Debug.Log($"[MCP Tools] connect_flow_contexts: {label} anchor type {type.FullName}");
            Debug.Log($"[MCP Tools] connect_flow_contexts: {label} anchor flow properties => {string.Join(", ", properties)}");
            Debug.Log($"[MCP Tools] connect_flow_contexts: {label} anchor flow fields => {string.Join(", ", fields)}");
        }

        private static bool TryModelLink(object contextModel, object sourceSlot, object targetSlot)
        {
            if (contextModel == null || sourceSlot == null || targetSlot == null)
            {
                return false;
            }

            var slotType = targetSlot.GetType();
            var linkField = slotType.GetField("link", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (linkField == null)
            {
                Debug.LogWarning("[MCP Tools] connect_flow_contexts: Target slot has no 'link' field");
                return false;
            }

            var linkList = linkField.GetValue(targetSlot) as IList;
            if (linkList == null)
            {
                Debug.LogWarning("[MCP Tools] connect_flow_contexts: Target slot 'link' field is not a list");
                return false;
            }

            var contextSlotType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContextSlot");
            var linkStructType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContextLink");
            if (contextSlotType == null || linkStructType == null)
            {
                Debug.LogWarning("[MCP Tools] connect_flow_contexts: ContextSlot or ContextLink types unavailable");
                return false;
            }

            try
            {
                var newLink = Activator.CreateInstance(linkStructType);
                var contextField = linkStructType.GetField("context", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var slotIndexField = linkStructType.GetField("slotIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (contextField == null || slotIndexField == null)
                {
                    Debug.LogWarning("[MCP Tools] connect_flow_contexts: ContextLink struct missing fields");
                    return false;
                }

                var sourceContextField = sourceSlot.GetType().GetField("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object sourceContext = sourceContextField?.GetValue(sourceSlot);
                if (sourceContext == null)
                {
                    Debug.LogWarning("[MCP Tools] connect_flow_contexts: Source slot owner not found");
                    return false;
                }

                var indexProp = sourceSlot.GetType().GetProperty("index", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                int slotIndex = indexProp != null ? Convert.ToInt32(indexProp.GetValue(sourceSlot)) : 0;

                contextField.SetValueDirect(__makeref(newLink), sourceContext);
                slotIndexField.SetValueDirect(__makeref(newLink), slotIndex);
                linkList.Add(newLink);
                Debug.Log("[MCP Tools] connect_flow_contexts: Added context link via slot field fallback");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to append context link list: {ex.Message}");
                return false;
            }
        }

        private static bool TryControllerFlowLink(object controller, object sourceAnchor, object targetAnchor)
        {
            if (controller == null || sourceAnchor == null || targetAnchor == null)
            {
                return false;
            }

            var controllerType = controller.GetType();
            var createLinkMethod = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name.Contains("CreateFlowLink") && m.GetParameters().Length == 2);

            if (createLinkMethod != null)
            {
                try
                {
                    Undo.RegisterCompleteObjectUndo(controller as UnityEngine.Object, "connect_flow_contexts");
                    var newEdge = createLinkMethod.Invoke(controller, new[] { sourceAnchor, targetAnchor });
                    if (newEdge != null)
                    {
                        Debug.Log("[MCP Tools] connect_flow_contexts: Created flow link via controller method");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Controller CreateFlowLink failed: {ex.Message}");
                }
            }

            return false;
        }

        private static bool TryAddFlowEdgeFromModel(object graphModel, object sourceSlot, object targetSlot)
        {
            if (graphModel == null || sourceSlot == null || targetSlot == null)
            {
                return false;
            }

            var flowEdgesField = graphModel.GetType().GetField("flowEdges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? graphModel.GetType().GetField("m_FlowEdges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (flowEdgesField == null)
            {
                Debug.LogWarning("[MCP Tools] connect_flow_contexts: Graph model lacks flowEdges field");
                return false;
            }

            var flowEdges = flowEdgesField.GetValue(graphModel) as IList;
            if (flowEdges == null)
            {
                Debug.LogWarning("[MCP Tools] connect_flow_contexts: flowEdges collection unavailable");
                return false;
            }

            var edgeType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXFlowEdge");
            if (edgeType == null)
            {
                Debug.LogWarning("[MCP Tools] connect_flow_contexts: VFXFlowEdge type unavailable");
                return false;
            }

            try
            {
                var newEdge = Activator.CreateInstance(edgeType);
                edgeType.GetField("output", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(newEdge, sourceSlot);
                edgeType.GetField("input", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(newEdge, targetSlot);
                flowEdges.Add(newEdge);
                Debug.Log("[MCP Tools] connect_flow_contexts: Added flow edge to model collection");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] connect_flow_contexts: Failed to add flow edge to model: {ex.Message}");
                return false;
            }
        }
    }
}

