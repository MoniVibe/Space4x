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
    [McpForUnityTool("create_exposed_parameter")]
    public static class CreateExposedParameterTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var parameterName = @params["parameter_name"]?.ToString();
                var parameterType = @params["parameter_type"]?.ToString();
                var defaultValue = @params["default_value"];

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    return Response.Error("parameter_name is required");
                }

                if (string.IsNullOrWhiteSpace(parameterType))
                {
                    return Response.Error("parameter_type is required (e.g., 'Float', 'Vector3', 'Texture2D')");
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out error))
                {
                    return Response.Error(error);
                }

                var graph = VfxGraphReflectionHelpers.GetGraph(resource);
                if (graph == null)
                {
                    return Response.Error("Unable to access graph model");
                }

                // Create parameter - try multiple approaches
                var parameter = CreateParameter(graph, controller, parameterName, parameterType, defaultValue);
                if (parameter == null)
                {
                    return Response.Error($"Failed to create parameter '{parameterName}' of type '{parameterType}'");
                }

                VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();

                var paramType = parameter.GetType();
                var name = paramType.GetProperty("name")?.GetValue(parameter) as string;
                var unityObj = parameter as UnityEngine.Object;
                var paramId = unityObj?.GetInstanceID() ?? parameter.GetHashCode();

                return Response.Success($"Parameter '{parameterName}' created", new
                {
                    graphPath,
                    parameterName = name ?? parameterName,
                    parameterId = paramId,
                    parameterType
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create exposed parameter: {ex.Message}");
            }
        }

        private static object CreateParameter(object graph, object controller, string name, string typeName, JToken defaultValue)
        {
            var graphType = graph.GetType();
            var controllerType = controller?.GetType();
            var paramType = ResolveParameterType(typeName);
            
            if (paramType == null)
            {
                Debug.LogWarning($"[MCP Tools] create_exposed_parameter: Could not resolve type '{typeName}'");
                return null;
            }

            Debug.Log($"[MCP Tools] create_exposed_parameter: Graph type: {graphType.FullName}, Parameter type: {paramType.FullName}");
            
            // Diagnostic: List all methods on graph that might be related to parameters
            var allGraphMethods = graphType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name.Contains("Parameter", StringComparison.OrdinalIgnoreCase))
                .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                .ToArray();
            if (allGraphMethods.Length > 0)
            {
                Debug.Log($"[MCP Tools] create_exposed_parameter: Graph methods containing 'Parameter': {string.Join(", ", allGraphMethods)}");
            }
            else
            {
                Debug.LogWarning($"[MCP Tools] create_exposed_parameter: No methods containing 'Parameter' found on graph type {graphType.FullName}");
            }

            // Strategy 1: Try AddParameter on graph
            var addParameterMethods = graphType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "AddParameter")
                .ToArray();
            if (addParameterMethods.Length > 0)
            {
                foreach (var addParameterMethod in addParameterMethods)
                {
                    try
                    {
                        var parameters = addParameterMethod.GetParameters();
                        Debug.Log($"[MCP Tools] create_exposed_parameter: Trying graph.AddParameter with {parameters.Length} parameters: {string.Join(", ", parameters.Select(p => p.ParameterType.Name))}");
                        
                        object parameter = null;
                        if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == paramType)
                        {
                            parameter = addParameterMethod.Invoke(graph, new object[] { name, paramType });
                        }
                        else if (parameters.Length == 2)
                        {
                            // Try with default value for type
                            var defaultValueForType = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                            parameter = addParameterMethod.Invoke(graph, new object[] { name, defaultValueForType ?? paramType });
                        }
                        
                        if (parameter != null)
                        {
                            if (defaultValue != null)
                            {
                                SetParameterValue(parameter, defaultValue);
                            }
                            Debug.Log($"[MCP Tools] create_exposed_parameter: Created via graph.AddParameter");
                            return parameter;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MCP Tools] graph.AddParameter({addParameterMethod.GetParameters().Length} params) failed: {ex.Message}");
                    }
                }
            }

            // Strategy 2: Try CreateParameter on graph
            var createParameterMethods = graphType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "CreateParameter")
                .ToArray();
            if (createParameterMethods.Length > 0)
            {
                foreach (var createParameterMethod in createParameterMethods)
                {
                    try
                    {
                        var parameters = createParameterMethod.GetParameters();
                        Debug.Log($"[MCP Tools] create_exposed_parameter: Trying graph.CreateParameter with {parameters.Length} parameters: {string.Join(", ", parameters.Select(p => p.ParameterType.Name))}");
                        
                        object parameter = null;
                        if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == paramType)
                        {
                            parameter = createParameterMethod.Invoke(graph, new object[] { name, paramType });
                        }
                        else if (parameters.Length == 2)
                        {
                            var defaultValueForType = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                            parameter = createParameterMethod.Invoke(graph, new object[] { name, defaultValueForType ?? paramType });
                        }
                        
                        if (parameter != null)
                        {
                            if (defaultValue != null)
                            {
                                SetParameterValue(parameter, defaultValue);
                            }
                            Debug.Log($"[MCP Tools] create_exposed_parameter: Created via graph.CreateParameter");
                            return parameter;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MCP Tools] graph.CreateParameter({createParameterMethod.GetParameters().Length} params) failed: {ex.Message}");
                    }
                }
            }

            // Strategy 3: Try AddVFXParameter on controller using variant (like AddNodeToGraphTool does)
            if (controllerType != null)
            {
                // Find the parameter variant for this type
                object parameterVariant = FindParameterVariant(typeName, paramType);
                if (parameterVariant != null)
                {
                    var addVfxParameterMethods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name == "AddVFXParameter" && m.GetParameters().Length >= 2)
                        .ToArray();

                    foreach (var method in addVfxParameterMethods)
                    {
                        var methodParams = method.GetParameters();
                        try
                        {
                            object parameterModel = null;
                            var position = new Vector2(0, 0); // Default position
                            
                            if (methodParams.Length == 2 && methodParams[0].ParameterType == typeof(Vector2))
                            {
                                // AddVFXParameter(Vector2 position, Variant variant)
                                parameterModel = method.Invoke(controller, new object[] { position, parameterVariant });
                            }
                            else if (methodParams.Length == 3 && methodParams[0].ParameterType == typeof(Vector2))
                            {
                                // AddVFXParameter(Vector2 position, Variant variant, bool something)
                                parameterModel = method.Invoke(controller, new object[] { position, parameterVariant, true });
                            }

                            if (parameterModel != null)
                            {
                                // Get the actual parameter object from the model
                                object parameter = parameterModel;
                                var modelProperty = parameterModel.GetType().GetProperty("model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (modelProperty != null)
                                {
                                    parameter = modelProperty.GetValue(parameterModel);
                                }

                                if (parameter != null)
                                {
                                    // Set the parameter name
                                    var nameProperty = parameter.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (nameProperty != null && nameProperty.CanWrite)
                                    {
                                        nameProperty.SetValue(parameter, name);
                                    }

                                    if (defaultValue != null)
                                    {
                                        SetParameterValue(parameter, defaultValue);
                                    }

                                    // Sync the controller
                                    var syncMethod = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                        .FirstOrDefault(m => m.Name == "SyncControllerFromModel" && m.GetParameters().Length == 1);
                                    syncMethod?.Invoke(controller, new object[] { false });

                                    Debug.Log($"[MCP Tools] create_exposed_parameter: Created via controller.AddVFXParameter with variant");
                                    return parameter;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[MCP Tools] controller.AddVFXParameter({methodParams.Length} params) failed: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[MCP Tools] create_exposed_parameter: Could not find parameter variant for type '{typeName}'");
                }
            }

            // Strategy 4: Try accessing parameters collection and adding directly
            var parametersProperty = graphType.GetProperty("parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? graphType.GetProperty("m_Parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (parametersProperty != null)
            {
                try
                {
                    var parametersCollection = parametersProperty.GetValue(graph);
                    if (parametersCollection != null)
                    {
                        var addMethod = parametersCollection.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (addMethod != null)
                        {
                            // Try to instantiate a VFXParameter
                            var vfxParameterType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXParameter");
                            if (vfxParameterType != null)
                            {
                                var constructor = vfxParameterType.GetConstructor(new[] { typeof(string), paramType });
                                if (constructor != null)
                                {
                                    var parameter = constructor.Invoke(new object[] { name, Activator.CreateInstance(paramType) });
                                    addMethod.Invoke(parametersCollection, new[] { parameter });
                                    if (defaultValue != null)
                                    {
                                        SetParameterValue(parameter, defaultValue);
                                    }
                                    Debug.Log($"[MCP Tools] create_exposed_parameter: Created via parameters collection");
                                    return parameter;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] parameters collection Add failed: {ex.Message}");
                }
            }

            Debug.LogWarning($"[MCP Tools] create_exposed_parameter: All strategies failed for '{name}' of type '{typeName}'");
            return null;
        }

        private static object FindParameterVariant(string typeName, Type paramType)
        {
            try
            {
                // Search through parameter variants from the library
                var descriptors = new List<(object descriptor, object variant)>();
                foreach (var descriptor in VfxGraphReflectionHelpers.GetLibraryDescriptors("GetParameters"))
                {
                    var enumerableDescriptor = descriptor as IEnumerable;
                    var toEnumerate = enumerableDescriptor ?? new[] { descriptor };
                    descriptors.AddRange(VfxGraphReflectionHelpers.EnumerateVariants(toEnumerate));
                }

                // Try to match by type name first (e.g., "Float", "Vector3")
                var normalizedTypeName = typeName?.Trim();
                foreach (var (descriptor, variant) in descriptors)
                {
                    if (variant == null)
                    {
                        continue;
                    }

                    var variantType = variant.GetType();
                    var variantName = variantType.GetProperty("name")?.GetValue(variant) as string;
                    var uniqueIdentifier = variantType.GetMethod("GetUniqueIdentifier", Type.EmptyTypes)?.Invoke(variant, null) as string;
                    var category = variantType.GetProperty("category")?.GetValue(variant) as string;

                    // Match by name (case-insensitive, handle spaces)
                    if (!string.IsNullOrEmpty(variantName))
                    {
                        var normalizedVariantName = variantName.Replace(" ", "");
                        var normalizedInputName = normalizedTypeName.Replace(" ", "");
                        if (string.Equals(normalizedVariantName, normalizedInputName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(variantName, normalizedTypeName, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[MCP Tools] create_exposed_parameter: Found variant by name: {variantName} (identifier: {uniqueIdentifier})");
                            return variant;
                        }
                    }

                    // Match by identifier (e.g., "Parameter/Float", "/Vector 3")
                    if (!string.IsNullOrEmpty(uniqueIdentifier))
                    {
                        var normalizedIdentifier = uniqueIdentifier.Replace(" ", "");
                        var normalizedInput = normalizedTypeName.Replace(" ", "");
                        if (normalizedIdentifier.Contains(normalizedInput, StringComparison.OrdinalIgnoreCase) ||
                            uniqueIdentifier.Contains(normalizedTypeName, StringComparison.OrdinalIgnoreCase) ||
                            uniqueIdentifier.EndsWith($"/{normalizedTypeName}", StringComparison.OrdinalIgnoreCase) ||
                            normalizedIdentifier.EndsWith($"/{normalizedInput}", StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[MCP Tools] create_exposed_parameter: Found variant by identifier: {uniqueIdentifier}");
                            return variant;
                        }
                    }
                }

                // Fallback: try to match by the actual type
                foreach (var (descriptor, variant) in descriptors)
                {
                    if (variant == null)
                    {
                        continue;
                    }

                    // Check if the variant's type matches
                    var variantType = variant.GetType();
                    var typeProperty = variantType.GetProperty("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (typeProperty != null)
                    {
                        var variantTypeValue = typeProperty.GetValue(variant) as Type;
                        if (variantTypeValue == paramType || (variantTypeValue != null && variantTypeValue.IsAssignableFrom(paramType)))
                        {
                            Debug.Log($"[MCP Tools] create_exposed_parameter: Found variant by type: {variantTypeValue?.FullName}");
                            return variant;
                        }
                    }
                }

                Debug.LogWarning($"[MCP Tools] create_exposed_parameter: No variant found for type '{typeName}'");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] create_exposed_parameter: Error finding parameter variant: {ex.Message}");
                return null;
            }
        }

        private static Type ResolveParameterType(string typeName)
        {
            var normalized = typeName?.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            // Common type mappings
            var typeMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["Float"] = typeof(float),
                ["Int"] = typeof(int),
                ["Bool"] = typeof(bool),
                ["Vector2"] = typeof(Vector2),
                ["Vector3"] = typeof(Vector3),
                ["Vector4"] = typeof(Vector4),
                ["Color"] = typeof(Color),
                ["Texture2D"] = typeof(Texture2D),
                ["Texture3D"] = typeof(Texture3D),
                ["String"] = typeof(string)
            };

            if (typeMap.TryGetValue(normalized, out var mappedType))
            {
                return mappedType;
            }

            // Try to resolve as full type name
            var resolvedType = Type.GetType(normalized, throwOnError: false);
            if (resolvedType != null)
            {
                return resolvedType;
            }

            // Try VFX-specific types
            resolvedType = VfxGraphReflectionHelpers.GetEditorType(normalized);
            if (resolvedType != null)
            {
                return resolvedType;
            }

            return null;
        }

        private static void SetParameterValue(object parameter, JToken valueToken)
        {
            if (parameter == null || valueToken == null)
            {
                return;
            }

            var paramType = parameter.GetType();
            var valueProperty = paramType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (valueProperty != null && valueProperty.CanWrite)
            {
                try
                {
                    var targetType = valueProperty.PropertyType;
                    var convertedValue = ConvertValueToType(valueToken, targetType);
                    valueProperty.SetValue(parameter, convertedValue);
                }
                catch { }
            }
        }

        private static object ConvertValueToType(JToken token, Type targetType)
        {
            if (token == null)
            {
                return null;
            }

            if (targetType == typeof(float) || targetType == typeof(float?))
            {
                return token.ToObject<float>();
            }
            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return token.ToObject<int>();
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
            if (targetType == typeof(Color))
            {
                var obj = token.ToObject<JObject>();
                if (obj != null)
                {
                    return new Color(
                        obj["r"]?.ToObject<float>() ?? 0f,
                        obj["g"]?.ToObject<float>() ?? 0f,
                        obj["b"]?.ToObject<float>() ?? 0f,
                        obj["a"]?.ToObject<float>() ?? 1f
                    );
                }
            }

            return token.ToObject(targetType);
        }
    }
}

