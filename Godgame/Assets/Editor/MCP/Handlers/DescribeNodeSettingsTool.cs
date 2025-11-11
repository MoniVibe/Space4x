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
    [McpForUnityTool("describe_node_settings")]
    public static class DescribeNodeSettingsTool
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

                var settings = DescribeSettings(model);

                return Response.Success($"Described settings for node {nodeId}", new
                {
                    graphPath,
                    nodeId,
                    nodeType = model.GetType().FullName,
                    settings = settings,
                    settingCount = settings.Count
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to describe node settings: {ex.Message}");
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

        private static List<Dictionary<string, object>> DescribeSettings(object model)
        {
            var settings = new List<Dictionary<string, object>>();
            var modelType = model.GetType();

            // Try GetSettings method (VFX-specific)
            var getSettingsMethod = modelType.GetMethod("GetSettings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getSettingsMethod != null)
            {
                try
                {
                    var settingsObj = getSettingsMethod.Invoke(model, null);
                    if (settingsObj is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var setting in enumerable)
                        {
                            var settingInfo = DescribeSetting(setting);
                            if (settingInfo != null)
                            {
                                settings.Add(settingInfo);
                            }
                        }
                    }
                }
                catch { }
            }

            // Also enumerate public properties that might be settings
            var properties = modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var prop in properties)
            {
                // Skip Unity Object properties
                if (prop.PropertyType == typeof(UnityEngine.Object) || typeof(UnityEngine.Object).IsAssignableFrom(prop.PropertyType))
                {
                    continue;
                }

                // Skip if already in settings list
                if (settings.Any(s => s["name"] as string == prop.Name))
                {
                    continue;
                }

                try
                {
                    var value = prop.GetValue(model);
                    var settingInfo = new Dictionary<string, object>
                    {
                        ["name"] = prop.Name,
                        ["type"] = prop.PropertyType.FullName,
                        ["canRead"] = prop.CanRead,
                        ["canWrite"] = prop.CanWrite,
                        ["value"] = ConvertSettingValue(value)
                    };
                    settings.Add(settingInfo);
                }
                catch { }
            }

            return settings;
        }

        private static Dictionary<string, object> DescribeSetting(object setting)
        {
            if (setting == null)
            {
                return null;
            }

            var settingType = setting.GetType();
            var nameProperty = settingType.GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var valueProperty = settingType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var typeProperty = settingType.GetProperty("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var name = nameProperty?.GetValue(setting) as string;
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var info = new Dictionary<string, object>
            {
                ["name"] = name
            };

            if (typeProperty != null)
            {
                var type = typeProperty.GetValue(setting) as Type;
                if (type != null)
                {
                    info["type"] = type.FullName;
                }
            }

            if (valueProperty != null)
            {
                try
                {
                    var value = valueProperty.GetValue(setting);
                    info["value"] = ConvertSettingValue(value);
                }
                catch { }
            }

            return info;
        }

        private static object ConvertSettingValue(object value)
        {
            if (value == null)
            {
                return null;
            }

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

