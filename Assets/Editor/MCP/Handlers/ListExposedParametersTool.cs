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
    [McpForUnityTool("list_exposed_parameters")]
    public static class ListExposedParametersTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                object controller = null;
                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out controller, out error))
                {
                    Debug.LogWarning($"[MCP Tools] list_exposed_parameters: Unable to acquire controller: {error}");
                }
                else if (controller != null)
                {
                    try
                    {
                        VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                        var syncArgs = new object[] { false };
                        controller.GetType()
                            .GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?.Invoke(controller, syncArgs);
                    }
                    catch (Exception syncEx)
                    {
                        Debug.LogWarning($"[MCP Tools] list_exposed_parameters: Controller sync failed: {syncEx.Message}");
                    }
                }

                var graph = VfxGraphReflectionHelpers.GetGraph(resource);
                if (graph == null)
                {
                    return Response.Error("Unable to access graph model");
                }

                var parameterModels = new List<object>();

                if (controller != null)
                {
                    parameterModels.AddRange(GetParametersFromController(controller));
                }

                if (parameterModels.Count == 0)
                {
                    parameterModels.AddRange(GetParametersFromGraph(graph));
                }

                var uniqueParameters = new List<object>();
                var seenIds = new HashSet<int>();

                foreach (var candidate in parameterModels)
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    int key;
                    if (candidate is UnityEngine.Object unityObj)
                    {
                        if (unityObj == null)
                        {
                            continue;
                        }
                        key = unityObj.GetInstanceID();
                    }
                    else
                    {
                        key = candidate.GetHashCode();
                    }

                    if (!seenIds.Add(key))
                    {
                        continue;
                    }

                    uniqueParameters.Add(candidate);
                }

                var parameterInfos = new List<Dictionary<string, object>>();

                foreach (var param in uniqueParameters)
                {
                    var paramType = param.GetType();
                    var paramInfo = new Dictionary<string, object>
                    {
                        ["type"] = paramType.FullName
                    };

                    var nameProperty = paramType.GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (nameProperty != null)
                    {
                        paramInfo["name"] = nameProperty.GetValue(param) as string ?? "Unknown";
                    }

                    var exposedNameProperty = paramType.GetProperty("exposedName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (exposedNameProperty != null)
                    {
                        paramInfo["exposedName"] = exposedNameProperty.GetValue(param) as string;
                    }

                    var valueProperty = paramType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (valueProperty != null)
                    {
                        try
                        {
                            var value = valueProperty.GetValue(param);
                            paramInfo["value"] = ConvertParameterValue(value);
                        }
                        catch { }
                    }

                    var valueTypeProperty = paramType.GetProperty("valueType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (valueTypeProperty != null)
                    {
                        var valueType = valueTypeProperty.GetValue(param) as Type;
                        if (valueType != null)
                        {
                            paramInfo["valueType"] = valueType.FullName;
                        }
                    }

                    var unityObj = param as UnityEngine.Object;
                    if (unityObj != null)
                    {
                        paramInfo["id"] = unityObj.GetInstanceID();
                    }

                    parameterInfos.Add(paramInfo);
                }

                return Response.Success($"Found {parameterInfos.Count} exposed parameters", new
                {
                    graphPath,
                    parameterCount = parameterInfos.Count,
                    parameters = parameterInfos
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list exposed parameters: {ex.Message}");
            }
        }

        private static IEnumerable<object> GetParametersFromGraph(object graph)
        {
            if (graph == null)
            {
                return Array.Empty<object>();
            }

            var graphType = graph.GetType();
            
            // Try parameters property
            var parametersProperty = graphType.GetProperty("parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? graphType.GetProperty("m_Parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (parametersProperty != null)
            {
                var parameters = parametersProperty.GetValue(graph);
                if (parameters is IEnumerable enumerable)
                {
                    return VfxGraphReflectionHelpers.Enumerate(enumerable).Cast<object>();
                }
            }

            // Try GetParameters method
            var getParametersMethod = graphType.GetMethod("GetParameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getParametersMethod != null)
            {
                var result = getParametersMethod.Invoke(graph, null);
                if (result is IEnumerable enumerable)
                {
                    return VfxGraphReflectionHelpers.Enumerate(enumerable).Cast<object>();
                }
            }

            return Array.Empty<object>();
        }

        private static IEnumerable<object> GetParametersFromController(object controller)
        {
            if (controller == null)
            {
                yield break;
            }

            var controllerType = controller.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            object parameterControllers = null;
            var propertyCandidates = new[] { "parameterControllers", "parameters", "ParameterControllers" };
            foreach (var propertyName in propertyCandidates)
            {
                var prop = controllerType.GetProperty(propertyName, flags);
                if (prop != null)
                {
                    parameterControllers = prop.GetValue(controller);
                    if (parameterControllers != null)
                    {
                        break;
                    }
                }
            }

            if (parameterControllers == null)
            {
                var methodCandidates = new[] { "GetParameterControllers", "GetParameters" };
                foreach (var methodName in methodCandidates)
                {
                    var method = controllerType.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                    if (method != null)
                    {
                        parameterControllers = method.Invoke(controller, null);
                        if (parameterControllers != null)
                        {
                            break;
                        }
                    }
                }

                if (parameterControllers == null)
                {
                    var propertyDiagnostics = controllerType
                        .GetProperties(flags)
                        .Select(p => p.Name)
                        .Where(n => n.IndexOf("param", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToArray();
                    var methodDiagnostics = controllerType
                        .GetMethods(flags)
                        .Select(m => m.Name)
                        .Where(n => n.IndexOf("param", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Distinct()
                        .ToArray();
                    Debug.LogWarning($"[MCP Tools] list_exposed_parameters: Unable to locate parameter controllers on {controllerType.FullName}. Properties containing 'param': {string.Join(", ", propertyDiagnostics)}; Methods containing 'param': {string.Join(", ", methodDiagnostics)}");
                }
            }
            foreach (var paramController in VfxGraphReflectionHelpers.Enumerate(parameterControllers))
            {
                if (paramController == null)
                {
                    continue;
                }

                var paramControllerType = paramController.GetType();
                var modelProperty = paramControllerType
                    .GetProperties(flags)
                    .FirstOrDefault(p => string.Equals(p.Name, "model", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(p.Name, "parameter", StringComparison.OrdinalIgnoreCase));

                var model = modelProperty?.GetValue(paramController) ?? paramController;
                if (modelProperty == null)
                {
                    var availableProps = paramControllerType
                        .GetProperties(flags)
                        .Select(p => p.Name)
                        .Where(n => n.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("param", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToArray();
                    Debug.LogWarning($"[MCP Tools] list_exposed_parameters: No direct model property found on {paramControllerType.FullName}. Candidate properties: {string.Join(", ", availableProps)}");
                }

                if (model != null)
                {
                    yield return model;
                }
            }
        }

        private static object ConvertParameterValue(object value)
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

