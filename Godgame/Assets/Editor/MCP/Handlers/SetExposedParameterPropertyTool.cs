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
    [McpForUnityTool("set_exposed_parameter_property")]
    public static class SetExposedParameterPropertyTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var parameterName = @params["parameter_name"]?.ToString();
                var propertyName = @params["property_name"]?.ToString();
                var propertyValue = @params["property_value"];

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    return Response.Error("parameter_name is required");
                }

                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    return Response.Error("property_name is required (e.g., 'exposedName', 'category', 'tooltip')");
                }

                if (propertyValue == null)
                {
                    return Response.Error("property_value is required");
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                object controller = null;
                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out controller, out error))
                {
                    Debug.LogWarning($"[MCP Tools] set_exposed_parameter_property: Unable to acquire controller: {error}");
                }

                var graph = VfxGraphReflectionHelpers.GetGraph(resource);
                if (graph == null)
                {
                    return Response.Error("Unable to access graph model");
                }

                var parameter = FindParameter(graph, controller, parameterName);
                if (parameter == null)
                {
                    return Response.Error($"Parameter '{parameterName}' not found");
                }

                var paramType = parameter.GetType();
                var property = paramType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                bool set = false;

                if (property != null)
                {
                    if (property.CanWrite)
                    {
                        try
                        {
                            var current = property.CanRead ? property.GetValue(parameter) : null;
                            var convertedValue = VfxValueConverter.ConvertTokenToType(propertyValue, property.PropertyType, current);
                            property.SetValue(parameter, convertedValue);
                            set = true;
                        }
                        catch (Exception convertEx)
                        {
                            Debug.LogWarning($"[MCP Tools] set_exposed_parameter_property: Direct property set failed: {convertEx.Message}");
                        }
                    }

                    if (!set)
                    {
                        // Try using the setter method directly (for non-public setters)
                        var setMethod = property.GetSetMethod(true);
                        if (setMethod != null)
                        {
                            try
                            {
                                var parameters = setMethod.GetParameters();
                                var targetType = parameters.FirstOrDefault()?.ParameterType ?? property.PropertyType;
                                var current = property.CanRead ? property.GetValue(parameter) : null;
                                var convertedValue = VfxValueConverter.ConvertTokenToType(propertyValue, targetType, current);
                                setMethod.Invoke(parameter, new object[] { convertedValue });
                                set = true;
                            }
                            catch (Exception setterEx)
                            {
                                Debug.LogWarning($"[MCP Tools] set_exposed_parameter_property: Setter method failed: {setterEx.Message}");
                            }
                        }
                    }

                    // Try field if property doesn't work
                    if (!set)
                    {
                        var field = paramType.GetField($"m_{propertyName}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? paramType.GetField($"_{propertyName}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? paramType.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field != null)
                        {
                            try
                            {
                                var current = field.GetValue(parameter);
                                var convertedValue = VfxValueConverter.ConvertTokenToType(propertyValue, field.FieldType, current);
                                field.SetValue(parameter, convertedValue);
                                set = true;
                            }
                            catch (Exception fieldEx)
                            {
                                Debug.LogWarning($"[MCP Tools] set_exposed_parameter_property: Field set failed: {fieldEx.Message}");
                            }
                        }
                    }
                }

                if (!set)
                {
                    return Response.Error($"Property '{propertyName}' not found or could not be set on parameter");
                }

                if (controller != null)
                {
                    VfxGraphReflectionHelpers.SyncAndSave(controller, resource);
                }
                else
                {
                    VfxGraphReflectionHelpers.SaveAssetIfDirty(resource);
                }

                return Response.Success($"Property '{propertyName}' set on parameter '{parameterName}'", new
                {
                    graphPath,
                    parameterName,
                    propertyName,
                    propertyValue = propertyValue.ToObject<object>()
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to set exposed parameter property: {ex.Message}");
            }
        }

        private static object FindParameter(object graph, object controller, string parameterName)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Try controller first
            if (controller != null)
            {
                object parameterControllers = null;
                var propertyCandidates = new[] { "parameterControllers", "parameters", "ParameterControllers" };
                foreach (var propertyName in propertyCandidates)
                {
                    var prop = controller.GetType().GetProperty(propertyName, flags);
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
                        var method = controller.GetType().GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                        if (method != null)
                        {
                            parameterControllers = method.Invoke(controller, null);
                            if (parameterControllers != null)
                            {
                                break;
                            }
                        }
                    }
                }

                foreach (var paramController in VfxGraphReflectionHelpers.Enumerate(parameterControllers))
                {
                    if (paramController == null)
                    {
                        continue;
                    }

                    var ctrlType = paramController.GetType();
                    var modelProperty = ctrlType.GetProperty("model", flags)
                        ?? ctrlType.GetProperty("parameter", flags);
                    var model = modelProperty?.GetValue(paramController) ?? paramController;

                    if (MatchesParameterName(model, parameterName, flags))
                    {
                        return model;
                    }
                }
            }

            // Fallback to graph
            var graphType = graph.GetType();
            var parametersProperty = graphType.GetProperty("parameters", flags)
                ?? graphType.GetProperty("m_Parameters", flags);

            if (parametersProperty != null)
            {
                var parameters = parametersProperty.GetValue(graph);
                if (parameters is IEnumerable enumerable)
                {
                    foreach (var param in VfxGraphReflectionHelpers.Enumerate(enumerable))
                    {
                        if (MatchesParameterName(param, parameterName, flags))
                        {
                            return param;
                        }
                    }
                }
            }

            var getParametersMethod = graphType.GetMethod("GetParameters", flags, null, Type.EmptyTypes, null);
            if (getParametersMethod != null)
            {
                var result = getParametersMethod.Invoke(graph, null);
                if (result is IEnumerable enumerable)
                {
                    foreach (var param in VfxGraphReflectionHelpers.Enumerate(enumerable))
                    {
                        if (MatchesParameterName(param, parameterName, flags))
                        {
                            return param;
                        }
                    }
                }
            }

            return null;
        }

        private static bool MatchesParameterName(object parameter, string parameterName, BindingFlags flags)
        {
            if (parameter == null || string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            var paramType = parameter.GetType();
            var nameValues = new List<string>();

            var nameProperty = paramType.GetProperty("name", flags);
            if (nameProperty != null)
            {
                nameValues.Add(nameProperty.GetValue(parameter) as string);
            }

            var exposedNameProperty = paramType.GetProperty("exposedName", flags);
            if (exposedNameProperty != null)
            {
                var exposedName = exposedNameProperty.GetValue(parameter);
                nameValues.Add(exposedName as string ?? exposedName?.ToString());
            }

            var displayNameProperty = paramType.GetProperty("displayName", flags);
            if (displayNameProperty != null)
            {
                nameValues.Add(displayNameProperty.GetValue(parameter) as string);
            }

            return nameValues.Any(n => !string.IsNullOrEmpty(n) && string.Equals(n, parameterName, StringComparison.OrdinalIgnoreCase));
        }
    }
}

