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
    [McpForUnityTool("duplicate_context")]
    public static class DuplicateContextTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var contextIdToken = @params["context_id"];
                var positionX = @params["position_x"]?.ToObject<float?>();
                var positionY = @params["position_y"]?.ToObject<float?>();

                if (!TryParseContextId(contextIdToken, out var contextId, out var error))
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

                if (!contextMap.TryGetValue(contextId, out var sourceContextController))
                {
                    return Response.Error($"Context with id {contextId} not found");
                }

                var sourceModel = VfxGraphReflectionHelpers.GetProperty(sourceContextController, "model");
                if (sourceModel == null)
                {
                    return Response.Error($"Could not get model for context {contextId}");
                }

                // Get position for new context
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

                // Use AddNode with the source model's variant to duplicate
                // First, get the variant from the source context
                var sourceModelType = sourceModel.GetType();
                var variantProperty = sourceModelType.GetProperty("variant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object variant = null;
                if (variantProperty != null)
                {
                    variant = variantProperty.GetValue(sourceModel);
                }

                // If we can't get variant, try to duplicate via AddNode with the same type
                if (variant == null)
                {
                    // Fallback: try to create a new context of the same type
                    var contextName = sourceModelType.Name;
                    // Try to find a variant matching this context type
                    var descriptors = VfxGraphReflectionHelpers.GetLibraryDescriptors("GetContexts");
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
                    return Response.Error("Could not determine context variant for duplication");
                }

                // Add the duplicated context using AddNode
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

                object newContextController;
                try
                {
                    newContextController = addNodeMethod.Invoke(controller, new object[] { newPosition, variant, null });
                }
                catch (Exception ex)
                {
                    return Response.Error($"Failed to duplicate context: {ex.Message}");
                }

                if (newContextController == null)
                {
                    return Response.Error("Failed to create duplicated context");
                }

                var duplicatedModel = VfxGraphReflectionHelpers.GetProperty(newContextController, "model") as UnityEngine.Object;

                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                var newContextId = duplicatedModel.GetInstanceID();

                return Response.Success($"Context {contextId} duplicated in graph {graphPath}", new
                {
                    graphPath,
                    sourceContextId = contextId,
                    newContextId,
                    position = new { x = newPosition.x, y = newPosition.y }
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to duplicate context: {ex.Message}");
            }
        }

        private static bool TryParseContextId(JToken token, out int id, out string error)
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
    }
}

