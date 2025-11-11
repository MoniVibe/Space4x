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
    [McpForUnityTool("delete_exposed_parameter")]
    public static class DeleteExposedParameterTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var parameterName = @params["parameter_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    return Response.Error("parameter_name is required");
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                object controller = null;
                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out controller, out error))
                {
                    Debug.LogWarning($"[MCP Tools] delete_exposed_parameter: Unable to acquire controller: {error}");
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
                        Debug.LogWarning($"[MCP Tools] delete_exposed_parameter: Controller sync failed: {syncEx.Message}");
                    }
                }

                var graph = VfxGraphReflectionHelpers.GetGraph(resource);
                if (graph == null)
                {
                    return Response.Error("Unable to access graph model");
                }

                var parameter = FindParameter(graph, controller, parameterName, out var parameterController);
                if (parameter == null)
                {
                    return Response.Error($"Parameter '{parameterName}' not found");
                }

                bool attemptedController = false;
                bool attemptedGraph = false;

                var graphType = graph.GetType();
                try
                {
                    var parameterMethods = graphType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name.IndexOf("Parameter", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                        .ToArray();
                    Debug.Log($"[MCP Tools] delete_exposed_parameter: Graph parameter methods => {string.Join("; ", parameterMethods)}");
                }
                catch { }

                var removeParameterMethod = graphType.GetMethod("RemoveParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (removeParameterMethod != null)
                {
                    var parametersForMethod = removeParameterMethod.GetParameters();
                    if (parametersForMethod.Length == 1)
                    {
                        var paramType = parametersForMethod[0].ParameterType;
                        object argument = parameter;
                        if (!paramType.IsInstanceOfType(parameter))
                        {
                            if (paramType == typeof(string))
                            {
                                argument = parameterName;
                            }
                            else
                            {
                                try
                                {
                                    argument = Convert.ChangeType(parameterName, paramType);
                                }
                                catch
                                {
                                    argument = parameter;
                                }
                            }
                        }

                        removeParameterMethod.Invoke(graph, new[] { argument });
                        attemptedController = true;
                        attemptedGraph = true;
                    }
                }

                if (controller != null)
                {
                    var controllerType = controller.GetType();
                    try
                    {
                        var parameterMethods = controllerType
                            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(m => m.Name.IndexOf("Parameter", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                            .ToArray();
                        Debug.Log($"[MCP Tools] delete_exposed_parameter: Controller parameter methods => {string.Join("; ", parameterMethods)}");
                    }
                    catch { }

                    var removeControllerMethods = controllerType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name.IndexOf("Remove", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                    m.GetParameters().Length == 1)
                        .ToArray();

                    Debug.Log($"[MCP Tools] delete_exposed_parameter: Controller remove candidates => {string.Join(", ", removeControllerMethods.Select(m => m.Name))}");

                    foreach (var method in removeControllerMethods)
                    {
                        try
                        {
                            var paramInfo = method.GetParameters()[0];
                            Debug.Log($"[MCP Tools] delete_exposed_parameter: Evaluating controller method {method.Name}({paramInfo.ParameterType.FullName})");
                            object argument = parameter;
                            if (!paramInfo.ParameterType.IsInstanceOfType(parameter))
                            {
                                if (paramInfo.ParameterType == typeof(string))
                                {
                                    argument = parameterName;
                                }
                                else if (paramInfo.ParameterType == typeof(int))
                                {
                                    argument = parameter.GetHashCode();
                                }
                                else if (parameterController != null && paramInfo.ParameterType.IsInstanceOfType(parameterController))
                                {
                                    argument = parameterController;
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            method.Invoke(controller, new[] { argument });
                            attemptedController = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[MCP Tools] delete_exposed_parameter: Controller remove method {method.Name} failed: {ex.Message}");
                        }
                    }
                }

                // Try collection removal regardless of earlier attempts
                var parametersProperty = graphType.GetProperty("parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? graphType.GetProperty("m_Parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (parametersProperty != null)
                {
                    var parametersCollection = parametersProperty.GetValue(graph);
                    if (parametersCollection != null)
                    {
                        var removeMethod = parametersCollection.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (removeMethod != null)
                        {
                            try
                            {
                                removeMethod.Invoke(parametersCollection, new[] { parameter });
                                attemptedGraph = true;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[MCP Tools] delete_exposed_parameter: parameters.Remove failed: {ex.Message}");
                            }
                        }
                    }
                }

                if (!TryRemoveViaRemoveChild(graph, parameter))
                {
                    Debug.LogWarning($"[MCP Tools] delete_exposed_parameter: RemoveChild fallback did not find parameter");
                }
                else
                {
                    attemptedGraph = true;
                }

                var stillInController = ParameterExists(graph, controller, parameter, parameterName, "Final(controller)");
                var stillInGraph = ParameterExists(graph, null, parameter, parameterName, "Final(graph)");

                if (stillInController || stillInGraph)
                {
                    var attempts = $"Attempted controller: {attemptedController}, graph: {attemptedGraph}";
                    Debug.LogWarning($"[MCP Tools] delete_exposed_parameter: Parameter '{parameterName}' persists after removal attempts. {attempts}");
                    return Response.Error($"Failed to delete parameter '{parameterName}'");
                }

                if (controller != null)
                {
                    try
                    {
                        controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?.Invoke(controller, new object[] { false });
                    }
                    catch (Exception syncEx)
                    {
                        Debug.LogWarning($"[MCP Tools] delete_exposed_parameter: Post-delete sync failed: {syncEx.Message}");
                    }
                }

                if (controller != null)
                {
                    VfxGraphReflectionHelpers.SyncAndSave(controller, resource);
                }
                else
                {
                    VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                    AssetDatabase.SaveAssets();
                }

                return Response.Success($"Parameter '{parameterName}' deleted", new
                {
                    graphPath,
                    parameterName
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to delete exposed parameter: {ex.Message}");
            }
        }

        private static object FindParameter(object graph, object controller, string parameterName, out object parameterController)
        {
            parameterController = null;
            foreach (var wrapper in EnumerateParameters(graph, controller))
            {
                var param = wrapper.Model;
                var paramType = param.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var nameProperty = paramType.GetProperty("name", flags);
                var exposedNameProperty = paramType.GetProperty("exposedName", flags);
                var displayNameProperty = paramType.GetProperty("displayName", flags);

                var candidateNames = new List<string>();
                if (nameProperty != null)
                {
                    candidateNames.Add(nameProperty.GetValue(param) as string);
                }
                if (exposedNameProperty != null)
                {
                    var value = exposedNameProperty.GetValue(param);
                    candidateNames.Add(value as string ?? value?.ToString());
                }
                if (displayNameProperty != null)
                {
                    candidateNames.Add(displayNameProperty.GetValue(param) as string);
                }

                if (candidateNames.Any(n => !string.IsNullOrEmpty(n) && string.Equals(n, parameterName, StringComparison.OrdinalIgnoreCase)))
                {
                    parameterController = wrapper.Controller;
                    Debug.Log($"[MCP Tools] delete_exposed_parameter: Matched parameter '{parameterName}' model type {paramType.FullName}, controller type {wrapper.Controller?.GetType().FullName ?? "<null>"}");
                    return param;
                }
            }
            return null;
        }

        private static IEnumerable<ParameterModelWrapper> EnumerateParameters(object graph, object controller)
        {
            var results = new List<ParameterModelWrapper>();

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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
                    if (model != null)
                    {
                        results.Add(new ParameterModelWrapper(model, paramController));
                    }
                }
            }

            if (results.Count == 0 && graph != null)
            {
                var graphType = graph.GetType();
                var parametersProperty = graphType.GetProperty("parameters", flags)
                    ?? graphType.GetProperty("m_Parameters", flags);

                if (parametersProperty != null)
                {
                    var parameters = parametersProperty.GetValue(graph);
                    foreach (var param in VfxGraphReflectionHelpers.Enumerate(parameters))
                    {
                        if (param != null)
                        {
                            results.Add(new ParameterModelWrapper(param, null));
                        }
                    }
                }
                else
                {
                    var getParametersMethod = graphType.GetMethod("GetParameters", flags);
                    if (getParametersMethod != null)
                    {
                        var result = getParametersMethod.Invoke(graph, null);
                        foreach (var param in VfxGraphReflectionHelpers.Enumerate(result))
                        {
                            if (param != null)
                            {
                                results.Add(new ParameterModelWrapper(param, null));
                            }
                        }
                    }
                }
            }

            return results;
        }

        private static bool TryRemoveViaRemoveChild(object graph, object parameter)
        {
            if (graph == null || parameter == null)
            {
                return false;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var graphType = graph.GetType();

            var removeChildMethod = graphType.GetMethod("RemoveChild", flags);
            if (removeChildMethod != null)
            {
                try
                {
                    removeChildMethod.Invoke(graph, new[] { parameter });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] delete_exposed_parameter: graph.RemoveChild failed: {ex.Message}");
                }
            }

            var childrenProperty = graphType.GetProperty("children", flags) ?? graphType.GetProperty("m_Children", flags);
            if (childrenProperty != null)
            {
                var children = childrenProperty.GetValue(graph);
                if (children != null)
                {
                    var removeMethod = children.GetType().GetMethod("Remove", flags);
                    if (removeMethod != null)
                    {
                        try
                        {
                            removeMethod.Invoke(children, new[] { parameter });
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[MCP Tools] delete_exposed_parameter: children.Remove failed: {ex.Message}");
                        }
                    }
                }
            }

            return false;
        }

        private static bool ParameterExists(object graph, object controller, object parameter, string parameterName, string context)
        {
            var matched = false;
            var inspected = new List<string>();
            foreach (var wrapper in EnumerateParameters(graph, controller))
            {
                if (ReferenceEquals(wrapper.Model, parameter))
                {
                    matched = true;
                    break;
                }

                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var modelType = wrapper.Model?.GetType();
                if (modelType == null)
                {
                    continue;
                }

                var nameValues = new List<string>();
                nameValues.Add(modelType.GetProperty("name", flags)?.GetValue(wrapper.Model) as string);
                var exposedNameValue = modelType.GetProperty("exposedName", flags)?.GetValue(wrapper.Model);
                if (exposedNameValue != null)
                {
                    nameValues.Add(exposedNameValue as string ?? exposedNameValue.ToString());
                }
                nameValues.Add(modelType.GetProperty("displayName", flags)?.GetValue(wrapper.Model) as string);

                inspected.Add(string.Join("|", nameValues.Where(n => !string.IsNullOrEmpty(n))));

                if (nameValues.Any(n => !string.IsNullOrEmpty(n) && string.Equals(n, parameterName, StringComparison.OrdinalIgnoreCase)))
                {
                    matched = true;
                    break;
                }
            }

            Debug.Log($"[MCP Tools] delete_exposed_parameter: [{context}] ParameterExists '{parameterName}' => {matched}, inspected: {string.Join(", ", inspected)}");
            return matched;
        }

        private sealed class ParameterModelWrapper
        {
            public ParameterModelWrapper(object model, object controller)
            {
                Model = model;
                Controller = controller;
            }

            public object Model { get; }
            public object Controller { get; }
        }
    }
}



