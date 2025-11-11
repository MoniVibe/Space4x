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
    [McpForUnityTool("set_graph_node_property")]
    public static class SetGraphNodePropertyTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var nodeIdToken = @params["node_id"];
                var propertyName = @params["property_name"]?.ToString();
                var propertyValue = @params["property_value"];

                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    return Response.Error("property_name is required");
                }

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

                if (!TryResolveModel(controller, nodeMap, nodeId, out var model))
                {
                    return Response.Error($"Node with id {nodeId} not found");
                }

                if (model == null)
                {
                    return Response.Error("Resolved model was null");
                }

                var modelType = model.GetType();
                var oldValue = GetPropertyValue(model, propertyName);

                // Try SetSettingValue first (VFX-specific)
                var setSettingValueMethod = modelType.GetMethod("SetSettingValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (setSettingValueMethod != null)
                {
                    try
                    {
                        var convertedValue = ConvertPropertyValue(propertyValue, modelType, propertyName);
                        setSettingValueMethod.Invoke(model, new[] { propertyName, convertedValue });
                    }
                    catch (Exception setEx)
                    {
                        Debug.LogWarning($"[MCP Tools] SetSettingValue failed: {setEx.Message}, trying direct property");
                        // Fall through to direct property setting
                    }
                }

                // Try direct property setting
                var property = modelType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                bool propertySet = false;
                if (property != null && property.CanWrite)
                {
                    try
                    {
                        var convertedValue = ConvertPropertyValue(propertyValue, property.PropertyType);
                        property.SetValue(model, convertedValue);
                        propertySet = true;
                    }
                    catch (Exception propEx)
                    {
                        Debug.LogWarning($"[MCP Tools] Direct property set failed: {propEx.Message}, trying SafePropertySet");
                    }
                }

                // Fallback to SafePropertySet if direct setting failed
                if (!propertySet && setSettingValueMethod == null)
                {
                    var convertedValue = ConvertPropertyValue(propertyValue, modelType, propertyName);
                    propertySet = VfxGraphReflectionHelpers.SafePropertySet(model, propertyName, convertedValue);
                    if (!propertySet)
                    {
                        return Response.Error($"Property '{propertyName}' not found or is read-only");
                    }
                }

                // Use SyncAndSave helper for safe, guarded asset saving
                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                var newValue = GetPropertyValue(model, propertyName);

                return Response.Success($"Property '{propertyName}' set on node {nodeId}", new
                {
                    graphPath,
                    nodeId,
                    propertyName,
                    oldValue,
                    newValue
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to set graph node property: {ex.Message}");
            }
        }

        private static object GetPropertyValue(object model, string propertyName)
        {
            if (model == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var modelType = model.GetType();
            var property = modelType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead)
            {
                try
                {
                    return property.GetValue(model);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static object ConvertPropertyValue(JToken token, Type targetType, string propertyName = null)
        {
            if (token == null)
            {
                return null;
            }

            if (targetType == null)
            {
                // Try to infer from property name or use object
                return token.ToObject<object>();
            }

            try
            {
                if (targetType == typeof(int) || targetType == typeof(int?))
                {
                    return token.ToObject<int>();
                }
                if (targetType == typeof(float) || targetType == typeof(float?))
                {
                    return token.ToObject<float>();
                }
                if (targetType == typeof(bool) || targetType == typeof(bool?))
                {
                    return token.ToObject<bool>();
                }
                if (targetType == typeof(string))
                {
                    return token.ToString();
                }
                if (targetType == typeof(Vector2))
                {
                    var obj = token.ToObject<JObject>();
                    if (obj != null)
                    {
                        return new Vector2(
                            obj["x"]?.ToObject<float>() ?? 0f,
                            obj["y"]?.ToObject<float>() ?? 0f
                        );
                    }
                }
                if (targetType == typeof(Vector3))
                {
                    var obj = token.ToObject<JObject>();
                    if (obj != null)
                    {
                        return new Vector3(
                            obj["x"]?.ToObject<float>() ?? 0f,
                            obj["y"]?.ToObject<float>() ?? 0f,
                            obj["z"]?.ToObject<float>() ?? 0f
                        );
                    }
                }
                if (targetType == typeof(Vector4))
                {
                    var obj = token.ToObject<JObject>();
                    if (obj != null)
                    {
                        return new Vector4(
                            obj["x"]?.ToObject<float>() ?? 0f,
                            obj["y"]?.ToObject<float>() ?? 0f,
                            obj["z"]?.ToObject<float>() ?? 0f,
                            obj["w"]?.ToObject<float>() ?? 0f
                        );
                    }
                }

                // Fall back to generic conversion
                return token.ToObject(targetType);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] Failed to convert property value for '{propertyName}': {ex.Message}");
                return token.ToObject(targetType);
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

        private static bool TryResolveModel(object controller, Dictionary<int, object> nodeMap, int nodeId, out UnityEngine.Object model)
        {
            model = null;

            if (nodeMap.TryGetValue(nodeId, out var mappedNodeController))
            {
                model = VfxGraphReflectionHelpers.GetProperty(mappedNodeController, "model") as UnityEngine.Object;
                if (model != null)
                {
                    return true;
                }
            }

            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                var contextModel = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (contextModel == null)
                {
                    continue;
                }

                if (contextModel.GetInstanceID() == nodeId)
                {
                    model = contextModel;
                    return true;
                }

                foreach (var block in EnumerateContextBlocks(contextModel))
                {
                    if (block is UnityEngine.Object blockObject && blockObject.GetInstanceID() == nodeId)
                    {
                        model = blockObject;
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<object> EnumerateContextBlocks(object contextModel)
        {
            if (contextModel == null)
            {
                yield break;
            }

            var contextType = contextModel.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var propName in new[] { "children", "blocks", "m_Children" })
            {
                var prop = contextType.GetProperty(propName, flags);
                if (prop != null && prop.CanRead)
                {
                    var value = prop.GetValue(contextModel) as System.Collections.IEnumerable;
                    if (value != null)
                    {
                        foreach (var item in VfxGraphReflectionHelpers.Enumerate(value))
                        {
                            yield return item;
                        }
                    }
                    yield break;
                }
            }

            var getChildrenMethods = contextType.GetMethods(flags)
                .Where(m => m.Name == "GetChildren" && m.GetParameters().Length == 0)
                .ToArray();

            if (getChildrenMethods.Length > 0)
            {
                System.Collections.IEnumerable result = null;
                try
                {
                    result = getChildrenMethods[0].Invoke(contextModel, null) as System.Collections.IEnumerable;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] set_graph_node_property: GetChildren() invocation failed: {ex.Message}");
                }
                
                if (result != null)
                {
                    foreach (var item in VfxGraphReflectionHelpers.Enumerate(result))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}

