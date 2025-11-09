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
    [McpForUnityTool("remove_node_from_graph")]
    public static class RemoveNodeFromGraphTool
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

                // Try to find RemoveNode method
                var removeNodeMethod = controller.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "RemoveNode" || m.Name == "Remove");

                var removed = false;

                if (removeNodeMethod != null)
                {
                    try
                    {
                        var parameters = removeNodeMethod.GetParameters();
                        Debug.Log($"[MCP Tools] RemoveNode method signature: {string.Join(", ", parameters.Select(p => p.ParameterType.Name + " " + p.Name))}");

                        var invokeArgs = new object[parameters.Length];
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            if (i == 0)
                            {
                                invokeArgs[i] = nodeController;
                                continue;
                            }

                            var parameter = parameters[i];
                            if (parameter.ParameterType == typeof(bool))
                            {
                                invokeArgs[i] = false;
                            }
                            else if (parameter.HasDefaultValue)
                            {
                                invokeArgs[i] = parameter.DefaultValue;
                            }
                            else
                            {
                                invokeArgs[i] = parameter.ParameterType.IsValueType
                                    ? Activator.CreateInstance(parameter.ParameterType)
                                    : null;
                            }
                        }

                        removeNodeMethod.Invoke(controller, invokeArgs);
                        removed = true;
                    }
                    catch (Exception removeEx)
                    {
                        Debug.LogWarning($"[MCP Tools] RemoveNode method failed: {removeEx.Message}, will attempt RemoveChild fallback.");
                    }
                }

                if (!removed)
                {
                    var graph = VfxGraphReflectionHelpers.GetGraph(resource);
                    if (graph == null)
                    {
                        return Response.Error("Unable to access graph model");
                    }

                    var removeChildMethod = graph.GetType().GetMethod("RemoveChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (removeChildMethod == null)
                    {
                        return Response.Error("Unable to locate node removal method");
                    }

                    var removeChildParameters = removeChildMethod.GetParameters();
                    Debug.Log($"[MCP Tools] RemoveChild signature: {string.Join(", ", removeChildParameters.Select(p => p.ParameterType.Name + " " + p.Name))}");

                    var removeChildArgs = new object[removeChildParameters.Length];
                    for (int i = 0; i < removeChildParameters.Length; i++)
                    {
                        if (i == 0)
                        {
                            removeChildArgs[i] = model;
                            continue;
                        }

                        var parameter = removeChildParameters[i];
                        if (parameter.HasDefaultValue)
                        {
                            removeChildArgs[i] = parameter.DefaultValue;
                        }
                        else
                        {
                            removeChildArgs[i] = parameter.ParameterType.IsValueType
                                ? Activator.CreateInstance(parameter.ParameterType)
                                : null;
                        }
                    }

                    removeChildMethod.Invoke(graph, removeChildArgs);
                    removed = true;
                }

                if (!removed)
                {
                    return Response.Error("Node removal attempt failed");
                }

                // Use SyncAndSave helper for safe, guarded asset saving
                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                return Response.Success($"Node {nodeId} removed from graph", new
                {
                    graphPath,
                    nodeId
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to remove node from graph: {ex.Message}");
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
    }
}

