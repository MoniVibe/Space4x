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
    [McpForUnityTool("rename_exposed_parameter")]
    public static class RenameExposedParameterTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var parameterName = @params["parameter_name"]?.ToString();
                var newName = @params["new_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    return Response.Error("parameter_name is required");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    return Response.Error("new_name is required");
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                object controller = null;
                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out controller, out error))
                {
                    Debug.LogWarning($"[MCP Tools] rename_exposed_parameter: Unable to acquire controller: {error}");
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
                        Debug.LogWarning($"[MCP Tools] rename_exposed_parameter: Controller sync failed: {syncEx.Message}");
                    }
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
                try
                {
                    var nameRelatedMethods = paramType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                        .ToArray();
                    Debug.Log($"[MCP Tools] rename_exposed_parameter: Methods containing 'name' on {paramType.FullName}: {string.Join("; ", nameRelatedMethods)}");
                }
                catch { }

                bool renamed = TrySetName(parameter, paramType, newName);
                bool exposedRenamed = TrySetExposedName(parameter, paramType, newName);
                bool displayRenamed = TrySetDisplayName(parameter, paramType, newName);

                if (!renamed && !exposedRenamed && !displayRenamed)
                {
                    Debug.LogWarning($"[MCP Tools] rename_exposed_parameter: Unable to mutate name fields for parameter type {paramType.FullName}");
                    return Response.Error("Parameter does not support renaming");
                }

                VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();

                return Response.Success($"Parameter '{parameterName}' renamed to '{newName}'", new
                {
                    graphPath,
                    oldName = parameterName,
                    newName
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to rename exposed parameter: {ex.Message}");
            }
        }

        private static object FindParameter(object graph, object controller, string parameterName)
        {
            foreach (var param in EnumerateParameters(graph, controller))
            {
                var paramType = param.GetType();
                var nameProperty = paramType.GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var exposedNameProperty = paramType.GetProperty("exposedName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var displayNameProperty = paramType.GetProperty("displayName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                var namesToCheck = new List<string>();
                if (nameProperty != null)
                {
                    namesToCheck.Add(nameProperty.GetValue(param) as string);
                }
                if (exposedNameProperty != null)
                {
                    var value = exposedNameProperty.GetValue(param);
                    namesToCheck.Add(value as string ?? value?.ToString());
                }
                if (displayNameProperty != null)
                {
                    namesToCheck.Add(displayNameProperty.GetValue(param) as string);
                }

                Debug.Log($"[MCP Tools] rename_exposed_parameter: Candidate names => {string.Join(", ", namesToCheck.Where(n => !string.IsNullOrEmpty(n)))} (type: {paramType.FullName})");

                if (nameProperty != null)
                {
                    var name = nameProperty.GetValue(param) as string;
                    if (string.Equals(name, parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        return param;
                    }
                }

                if (namesToCheck.Any(n => !string.IsNullOrEmpty(n) && string.Equals(n, parameterName, StringComparison.OrdinalIgnoreCase)))
                {
                    return param;
                }
            }
            return null;
        }

        private static bool TrySetName(object parameter, Type paramType, string newName)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var nameProperty = paramType.GetProperty("name", flags);
            if (nameProperty != null)
            {
                if (nameProperty.CanWrite)
                {
                    nameProperty.SetValue(parameter, newName);
                    return true;
                }

                if (nameProperty.PropertyType == typeof(string))
                {
                    // Try invoking setter via SetMethod even if CanWrite false (private setter)
                    var setMethod = nameProperty.GetSetMethod(true);
                    if (setMethod != null)
                    {
                        setMethod.Invoke(parameter, new object[] { newName });
                        return true;
                    }
                }
            }

            var nameField = paramType.GetField("m_Name", flags) ?? paramType.GetField("name", flags);
            if (nameField != null && nameField.FieldType == typeof(string))
            {
                nameField.SetValue(parameter, newName);
                return true;
            }

            var setNameMethod = paramType.GetMethod("SetName", flags, null, new[] { typeof(string) }, null);
            if (setNameMethod != null)
            {
                setNameMethod.Invoke(parameter, new object[] { newName });
                return true;
            }

            return false;
        }

        private static bool TrySetExposedName(object parameter, Type paramType, string newName)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var exposedNameProperty = paramType.GetProperty("exposedName", flags);
            if (exposedNameProperty != null)
            {
                var propType = exposedNameProperty.PropertyType;
                if (exposedNameProperty.CanWrite)
                {
                    if (propType == typeof(string))
                    {
                        exposedNameProperty.SetValue(parameter, newName);
                        return true;
                    }

                    var ctor = propType.GetConstructor(new[] { typeof(string) });
                    if (ctor != null)
                    {
                        var exposedValue = ctor.Invoke(new object[] { newName });
                        exposedNameProperty.SetValue(parameter, exposedValue);
                        return true;
                    }

                    // Attempt to use ToString fallback
                    try
                    {
                        var converted = Convert.ChangeType(newName, propType);
                        exposedNameProperty.SetValue(parameter, converted);
                        return true;
                    }
                    catch
                    {
                        // fall through
                    }
                }

                var setMethod = exposedNameProperty.GetSetMethod(true);
                if (setMethod != null)
                {
                    if (exposedNameProperty.PropertyType == typeof(string))
                    {
                        setMethod.Invoke(parameter, new object[] { newName });
                        return true;
                    }

                    var ctor = exposedNameProperty.PropertyType.GetConstructor(new[] { typeof(string) });
                    if (ctor != null)
                    {
                        var value = ctor.Invoke(new object[] { newName });
                        setMethod.Invoke(parameter, new object[] { value });
                        return true;
                    }
                }
            }

            var exposedField = paramType.GetField("m_ExposedName", flags);
            if (exposedField != null)
            {
                if (exposedField.FieldType == typeof(string))
                {
                    exposedField.SetValue(parameter, newName);
                    return true;
                }

                var ctor = exposedField.FieldType.GetConstructor(new[] { typeof(string) });
                if (ctor != null)
                {
                    var value = ctor.Invoke(new object[] { newName });
                    exposedField.SetValue(parameter, value);
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetDisplayName(object parameter, Type paramType, string newName)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var displayNameProperty = paramType.GetProperty("displayName", flags);
            if (displayNameProperty != null)
            {
                if (displayNameProperty.CanWrite && displayNameProperty.PropertyType == typeof(string))
                {
                    displayNameProperty.SetValue(parameter, newName);
                    return true;
                }

                var setMethod = displayNameProperty.GetSetMethod(true);
                if (setMethod != null)
                {
                    setMethod.Invoke(parameter, new object[] { newName });
                    return true;
                }
            }

            var displayNameField = paramType.GetField("m_DisplayName", flags);
            if (displayNameField != null && displayNameField.FieldType == typeof(string))
            {
                displayNameField.SetValue(parameter, newName);
                return true;
            }

            return false;
        }

        private static IEnumerable<object> EnumerateParameters(object graph, object controller)
        {
            var results = new List<object>();

            if (controller != null)
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                object parameterControllers = null;

                var propertyCandidates = new[] { "parameterControllers", "parameters", "ParameterControllers" };
                foreach (var property in propertyCandidates)
                {
                    var propInfo = controller.GetType().GetProperty(property, flags);
                    if (propInfo != null)
                    {
                        parameterControllers = propInfo.GetValue(controller);
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
                    var modelProperty = ctrlType.GetProperty("model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? ctrlType.GetProperty("parameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    var model = modelProperty?.GetValue(paramController) ?? paramController;
                    if (model != null)
                    {
                        results.Add(model);
                    }
                }
            }

            if (results.Count == 0 && graph != null)
            {
                var graphType = graph.GetType();
                var parametersProperty = graphType.GetProperty("parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? graphType.GetProperty("m_Parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (parametersProperty != null)
                {
                    var parameters = parametersProperty.GetValue(graph);
                    results.AddRange(VfxGraphReflectionHelpers.Enumerate(parameters).Cast<object>());
                }
                else
                {
                    var getParametersMethod = graphType.GetMethod("GetParameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (getParametersMethod != null)
                    {
                        var result = getParametersMethod.Invoke(graph, null);
                        results.AddRange(VfxGraphReflectionHelpers.Enumerate(result).Cast<object>());
                    }
                }
            }

            return results;
        }
    }
}

