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
    [McpForUnityTool("list_available_node_settings")]
    public static class ListAvailableNodeSettingsTool
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

                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model");
                if (model == null)
                {
                    return Response.Error($"Could not get model for node {nodeId}");
                }

                var settings = new List<Dictionary<string, object>>();
                var modelType = model.GetType();

                // Get all properties
                foreach (var propInfo in modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (propInfo.CanRead)
                    {
                        var settingInfo = new Dictionary<string, object>
                        {
                            ["name"] = propInfo.Name,
                            ["type"] = propInfo.PropertyType.FullName,
                            ["canRead"] = propInfo.CanRead,
                            ["canWrite"] = propInfo.CanWrite,
                            ["isPublic"] = propInfo.GetGetMethod(true)?.IsPublic ?? false
                        };

                        // Try to get current value
                        try
                        {
                            var value = propInfo.GetValue(model);
                            if (value != null)
                            {
                                settingInfo["currentValue"] = ConvertValue(value);
                            }
                        }
                        catch { }

                        // Check for attributes that might indicate constraints
                        var attributes = propInfo.GetCustomAttributes(true);
                        foreach (var attr in attributes)
                        {
                            var attrType = attr.GetType();
                            if (attrType.Name.Contains("Range"))
                            {
                                var minProperty = attrType.GetProperty("min", BindingFlags.Instance | BindingFlags.Public);
                                var maxProperty = attrType.GetProperty("max", BindingFlags.Instance | BindingFlags.Public);
                                if (minProperty != null) settingInfo["minValue"] = minProperty.GetValue(attr);
                                if (maxProperty != null) settingInfo["maxValue"] = maxProperty.GetValue(attr);
                            }
                        }

                        settings.Add(settingInfo);
                    }
                }

                // Also check for settings via GetSettingValue/SetSettingValue methods
                var getSettingsMethod = modelType.GetMethod("GetSettings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getSettingsMethod != null)
                {
                    try
                    {
                        var settingsEnumerable = getSettingsMethod.Invoke(model, null);
                        if (settingsEnumerable is IEnumerable settingsEnum)
                        {
                            foreach (var setting in VfxGraphReflectionHelpers.Enumerate(settingsEnum))
                            {
                                if (setting == null) continue;

                                var settingType = setting.GetType();
                                var settingName = settingType.GetProperty("name")?.GetValue(setting) as string;
                                var settingValueType = settingType.GetProperty("type")?.GetValue(setting) as Type;

                                if (!string.IsNullOrEmpty(settingName))
                                {
                                    settings.Add(new Dictionary<string, object>
                                    {
                                        ["name"] = settingName,
                                        ["type"] = settingValueType?.FullName ?? "Unknown",
                                        ["canRead"] = true,
                                        ["canWrite"] = true,
                                        ["isPublic"] = false,
                                        ["isSetting"] = true
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }

                return Response.Success($"Found {settings.Count} available settings for node {nodeId}", new
                {
                    graphPath,
                    nodeId,
                    settingCount = settings.Count,
                    settings
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list available node settings: {ex.Message}");
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

