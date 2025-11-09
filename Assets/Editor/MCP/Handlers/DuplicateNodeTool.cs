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
    [McpForUnityTool("duplicate_node")]
    public static class DuplicateNodeTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var nodeIdToken = @params["node_id"];
                var positionX = @params["position_x"]?.ToObject<float?>();
                var positionY = @params["position_y"]?.ToObject<float?>();

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

                if (!nodeMap.TryGetValue(nodeId, out var sourceNodeController))
                {
                    return Response.Error($"Node with id {nodeId} not found");
                }

                var sourceModel = VfxGraphReflectionHelpers.GetProperty(sourceNodeController, "model");
                if (sourceModel == null)
                {
                    return Response.Error($"Could not get model for node {nodeId}");
                }

                // Get position for new node
                Vector2 newPosition;
                if (positionX.HasValue && positionY.HasValue)
                {
                    newPosition = new Vector2(positionX.Value, positionY.Value);
                }
                else
                {
                    // Use existing position offset by a small amount
                    var existingPosition = VfxGraphReflectionHelpers.GetProperty(sourceModel, "position");
                    if (existingPosition is Vector2 existingPos)
                    {
                        newPosition = existingPos + new Vector2(50, 50);
                    }
                    else
                    {
                        newPosition = new Vector2(0, 0);
                    }
                }

                // Get the variant from the source node
                var sourceModelType = sourceModel.GetType();
                var variantProperty = sourceModelType.GetProperty("variant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object variant = null;
                if (variantProperty != null)
                {
                    variant = variantProperty.GetValue(sourceModel);
                }

                // If we can't get variant, try to find it by node type
                if (variant == null)
                {
                    // Try to find a variant matching this node type
                    var descriptors = VfxGraphReflectionHelpers.GetLibraryDescriptors("GetOperators");
                    foreach (var descriptor in descriptors)
                    {
                        foreach (var (desc, var) in VfxGraphReflectionHelpers.EnumerateVariants(new[] { descriptor }))
                        {
                            if (var != null)
                            {
                                var varType = var.GetType();
                                var modelTypeProperty = varType.GetProperty("modelType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (modelTypeProperty?.GetValue(var) is Type modelType && modelType == sourceModelType)
                                {
                                    variant = var;
                                    break;
                                }
                            }
                        }
                        if (variant != null) break;
                    }
                }

                if (variant == null)
                {
                    return Response.Error("Could not determine node variant for duplication");
                }

                // Add the duplicated node using AddNode
                var allAddNodeMethods = controller.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "AddNode")
                    .ToArray();

                var addNodeMethod = allAddNodeMethods
                    .FirstOrDefault(m => m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(Vector2));

                if (addNodeMethod == null)
                {
                    return Response.Error("Unable to locate VFXViewController.AddNode method");
                }

                object newNodeController;
                try
                {
                    newNodeController = addNodeMethod.Invoke(controller, new object[] { newPosition, variant, null });
                }
                catch (Exception ex)
                {
                    return Response.Error($"Failed to duplicate node: {ex.Message}");
                }

                if (newNodeController == null)
                {
                    return Response.Error("Failed to create duplicated node");
                }

                var duplicatedModel = VfxGraphReflectionHelpers.GetProperty(newNodeController, "model") as UnityEngine.Object;
                var newNodeId = duplicatedModel?.GetInstanceID() ?? newNodeController.GetHashCode();

                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                return Response.Success($"Node {nodeId} duplicated in graph {graphPath}", new
                {
                    graphPath,
                    sourceNodeId = nodeId,
                    newNodeId,
                    position = new { x = newPosition.x, y = newPosition.y }
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to duplicate node: {ex.Message}");
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

