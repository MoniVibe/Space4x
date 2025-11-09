#if false
// get_slot_metadata disabled
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
    [McpForUnityTool("get_slot_metadata")]
    public static class GetSlotMetadataTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var nodeIdToken = @params["node_id"];
                var slotName = @params["slot_name"]?.ToString();

                if (!TryParseNodeId(nodeIdToken, out var nodeId, out var error))
                {
                    return Response.Error(error);
                }

                if (string.IsNullOrWhiteSpace(slotName))
                {
                    return Response.Error("slot_name is required");
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
                    return Response.Error($"Could not get model for node {nodeId}");
                }

                // Find the slot
                object targetSlot = FindSlot(model, slotName);
                if (targetSlot == null)
                {
                    return Response.Error($"Slot '{slotName}' not found on node {nodeId}");
                }

                var slotType = targetSlot.GetType();
                var metadata = new Dictionary<string, object>
                {
                    ["slotName"] = slotName,
                    ["slotType"] = slotType.FullName
                };

                // Get slot properties
                var name = slotType.GetProperty("name")?.GetValue(targetSlot) as string;
                var path = slotType.GetProperty("path")?.GetValue(targetSlot) as string;
                var valueType = slotType.GetProperty("valueType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(targetSlot) as Type;

                if (name != null) metadata["name"] = name;
                if (path != null) metadata["path"] = path;
                if (valueType != null) metadata["valueType"] = valueType.FullName;

                // Try to get constraints/range information
                var propertyProperty = slotType.GetProperty("property", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (propertyProperty != null)
                {
                    var slotProperty = propertyProperty.GetValue(targetSlot);
                    if (slotProperty != null)
                    {
                        var propType = slotProperty.GetType();
                        
                        // Check for range/min/max attributes or properties
                        var minProperty = propType.GetProperty("min", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var maxProperty = propType.GetProperty("max", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        
                        if (minProperty != null)
                        {
                            try
                            {
                                metadata["minValue"] = minProperty.GetValue(slotProperty);
                            }
                            catch { }
                        }
                        
                        if (maxProperty != null)
                        {
                            try
                            {
                                metadata["maxValue"] = maxProperty.GetValue(slotProperty);
                            }
                            catch { }
                        }
                    }
                }

                // Get default value if available
                var valueProperty = slotType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (valueProperty != null)
                {
                    try
                    {
                        var defaultValue = valueProperty.GetValue(targetSlot);
                        if (defaultValue != null)
                        {
                            metadata["defaultValue"] = ConvertValue(defaultValue);
                        }
                    }
                    catch { }
                }

                return Response.Success($"Slot metadata retrieved for '{slotName}' on node {nodeId}", new
                {
                    graphPath,
                    nodeId,
                    metadata
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to get slot metadata: {ex.Message}");
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

        private static object FindSlot(object nodeModel, string slotName)
        {
            var inputSlots = VfxGraphReflectionHelpers.GetProperty(nodeModel, "inputSlots");
            foreach (var slot in VfxGraphReflectionHelpers.Enumerate(inputSlots))
            {
                var name = VfxGraphReflectionHelpers.GetProperty(slot, "name") as string;
                var path = VfxGraphReflectionHelpers.GetProperty(slot, "path") as string;
                if (string.Equals(name, slotName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, slotName, StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }

            var outputSlots = VfxGraphReflectionHelpers.GetProperty(nodeModel, "outputSlots");
            foreach (var slot in VfxGraphReflectionHelpers.Enumerate(outputSlots))
            {
                var name = VfxGraphReflectionHelpers.GetProperty(slot, "name") as string;
                var path = VfxGraphReflectionHelpers.GetProperty(slot, "path") as string;
                if (string.Equals(name, slotName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, slotName, StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }
            return null;
        }

        private static object ConvertValue(object value)
        {
            if (value == null) return null;

            var valueType = value.GetType();
            if (valueType == typeof(Vector2))
            {
                var v = (Vector2)value;
                return new { x = v.x, y = v.y };
            }
            if (valueType == typeof(Vector3))
            {
                var v = (Vector3)value;
                return new { x = v.x, y = v.y, z = v.z };
            }
            if (valueType == typeof(Vector4))
            {
                var v = (Vector4)value;
                return new { x = v.x, y = v.y, z = v.z, w = v.w };
            }
            if (valueType == typeof(Color))
            {
                var c = (Color)value;
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            }
            if (valueType.IsPrimitive || valueType == typeof(string))
            {
                return value;
            }
            return value.ToString();
        }
    }
}
#endif

