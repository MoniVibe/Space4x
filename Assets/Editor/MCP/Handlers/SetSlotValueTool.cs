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
    [McpForUnityTool("set_slot_value")]
    public static class SetSlotValueTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var nodeIdToken = @params["node_id"];
                var slotName = @params["slot_name"]?.ToString();
                var slotValue = @params["slot_value"];

                if (string.IsNullOrWhiteSpace(slotName))
                {
                    return Response.Error("slot_name is required");
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

                // Find the slot
                var slot = FindSlot(model, slotName);
                if (slot == null)
                {
                    return Response.Error($"Slot '{slotName}' not found on node");
                }

                // Set the slot value
                var oldValue = GetSlotValue(slot);
                var set = SetSlotValue(slot, slotValue, oldValue);
                if (!set)
                {
                    return Response.Error($"Failed to set value for slot '{slotName}'");
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                EditorUtility.SetDirty(resource as UnityEngine.Object);
                VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();

                return Response.Success($"Slot '{slotName}' value set", new
                {
                    graphPath,
                    nodeId,
                    slotName,
                    oldValue = ConvertValue(oldValue),
                    newValue = ConvertValue(GetSlotValue(slot))
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to set slot value: {ex.Message}");
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
                    Debug.LogWarning($"[MCP Tools] set_slot_value: GetChildren() invocation failed: {ex.Message}");
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

        private static object FindSlot(object model, string slotName)
        {
            var modelType = model.GetType();

            // Try inputSlots and outputSlots properties
            var inputSlotsProperty = modelType.GetProperty("inputSlots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (inputSlotsProperty != null)
            {
                var inputSlots = inputSlotsProperty.GetValue(model) as System.Collections.IEnumerable;
                if (inputSlots != null)
                {
                    foreach (var slot in VfxGraphReflectionHelpers.Enumerate(inputSlots))
                    {
                        if (SlotMatches(slot, slotName))
                        {
                            return slot;
                        }
                    }
                }
            }

            var outputSlotsProperty = modelType.GetProperty("outputSlots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (outputSlotsProperty != null)
            {
                var outputSlots = outputSlotsProperty.GetValue(model) as System.Collections.IEnumerable;
                if (outputSlots != null)
                {
                    foreach (var slot in VfxGraphReflectionHelpers.Enumerate(outputSlots))
                    {
                        if (SlotMatches(slot, slotName))
                        {
                            return slot;
                        }
                    }
                }
            }

            return null;
        }

        private static bool SlotMatches(object slot, string slotName)
        {
            if (slot == null || string.IsNullOrEmpty(slotName))
            {
                return false;
            }

            var slotType = slot.GetType();
            var name = slotType.GetProperty("name")?.GetValue(slot) as string;
            var path = slotType.GetProperty("path")?.GetValue(slot) as string;

            return string.Equals(name, slotName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(path, slotName, StringComparison.OrdinalIgnoreCase);
        }

        private static object GetSlotValue(object slot)
        {
            if (slot == null)
            {
                return null;
            }

            var slotType = slot.GetType();
            var valueProperty = slotType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (valueProperty != null && valueProperty.CanRead)
            {
                try
                {
                    return valueProperty.GetValue(slot);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static bool SetSlotValue(object slot, JToken value, object currentValue)
        {
            if (slot == null)
            {
                return false;
            }

            var slotType = slot.GetType();
            var setValueMethod = slotType.GetMethod("SetValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (setValueMethod != null)
            {
                var parameters = setValueMethod.GetParameters();
                var targetType = parameters.FirstOrDefault()?.ParameterType ?? currentValue?.GetType();
                var converted = VfxValueConverter.ConvertTokenToType(value, targetType, currentValue);

                var args = BuildSetterArguments(parameters, converted);
                if (args != null)
                {
                    try
                    {
                        setValueMethod.Invoke(slot, args);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MCP Tools] set_slot_value: SetValue failed: {ex.Message}");
                    }
                }
            }

            var valueProperty = slotType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (valueProperty != null && valueProperty.CanWrite)
            {
                var preferredType = valueProperty.PropertyType == typeof(object) && currentValue != null
                    ? currentValue.GetType()
                    : valueProperty.PropertyType;

                try
                {
                    var converted = VfxValueConverter.ConvertTokenToType(value, preferredType, currentValue);
                    valueProperty.SetValue(slot, converted);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] set_slot_value: Direct value set failed: {ex.Message}");
                }
            }

            return false;
        }

        private static object[] BuildSetterArguments(ParameterInfo[] parameters, object convertedValue)
        {
            if (parameters == null)
            {
                return null;
            }

            if (parameters.Length == 0)
            {
                return Array.Empty<object>();
            }

            if (parameters.Length == 1)
            {
                return new[] { convertedValue };
            }

            if (parameters.Length == 2 && parameters[1].ParameterType == typeof(bool))
            {
                return new[] { convertedValue, true };
            }

            return null;
        }

        private static object ConvertValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is Vector2 vec2)
            {
                return new { x = vec2.x, y = vec2.y };
            }

            if (value is Vector3 vec3)
            {
                return new { x = vec3.x, y = vec3.y, z = vec3.z };
            }

            if (value is Vector4 vec4)
            {
                return new { x = vec4.x, y = vec4.y, z = vec4.z, w = vec4.w };
            }

            if (value is Color color)
            {
                return new { r = color.r, g = color.g, b = color.b, a = color.a };
            }

            return value;
        }
    }
}

