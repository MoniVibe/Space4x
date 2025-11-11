using Newtonsoft.Json;
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
    [McpForUnityTool("describe_vfx_graph")]
    public static class DescribeVfxGraphTool
    {
        private static readonly HashSet<string> SupportedParameterTypes = new HashSet<string>
        {
            "Float",
            "Int",
            "Vector2",
            "Vector3",
            "Color"
        };

        private static bool IsSupportedParameterType(string typeStr)
        {
            return !string.IsNullOrEmpty(typeStr) && SupportedParameterTypes.Contains(typeStr);
        }

        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var graphId = @params["graph_id"]?.ToString();
                int lod = @params["lod"]?.ToObject<int>() ?? 2;

                // If graph_id provided, resolve to path
                if (!string.IsNullOrEmpty(graphId) && string.IsNullOrEmpty(graphPath))
                {
                    graphPath = ResolveGraphIdToPath(graphId);
                    if (string.IsNullOrEmpty(graphPath))
                    {
                        return Response.Error($"Graph with id '{graphId}' not found");
                    }
                }

                if (string.IsNullOrEmpty(graphPath))
                {
                    return Response.Error("graph_path or graph_id is required");
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                var graphIdFinal = System.IO.Path.GetFileNameWithoutExtension(graphPath);
                var tags = ExtractTagsFromPath(graphPath);

                // Get exposed parameters
                var exposedParams = GetExposedParameters(graphPath, resource, lod);
                
                // Get stages (contexts and their blocks)
                var stages = GetStages(graphPath, resource, lod);

                var descriptor = new Dictionary<string, object>
                {
                    ["graph_id"] = graphIdFinal,
                    ["exposed_params"] = exposedParams,
                    ["stages"] = stages,
                    ["tags"] = tags
                };

                // Add path for reference if lod >= 2
                if (lod >= 2)
                {
                    descriptor["path"] = graphPath;
                    
                    // Add graph structure (nodes, connections) for training
                    var structure = GetGraphStructure(resource, lod);
                    if (structure != null)
                    {
                        descriptor["structure"] = structure;
                    }
                }

                var assetDependencies = GetAssetDependencies(graphPath);
                if (assetDependencies != null && assetDependencies.Count > 0)
                {
                    descriptor["asset_dependencies"] = assetDependencies;
                }

                var serializer = new JsonSerializer
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
                var descriptorToken = JToken.FromObject(descriptor, serializer);

                return Response.Success($"Graph descriptor generated for {graphIdFinal}", descriptorToken);
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to describe graph: {ex.Message}");
            }
        }

        private static string ResolveGraphIdToPath(string graphId)
        {
            string[] guids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(graphId, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            return null;
        }

        private static List<string> ExtractTagsFromPath(string path)
        {
            var tags = new List<string>();
            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            bool skipAssets = true;

            foreach (var part in parts)
            {
                if (skipAssets && part.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                {
                    skipAssets = false;
                    continue;
                }

                if (skipAssets)
                {
                    continue;
                }

                if (part.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(part) && part.Length > 1)
                {
                    tags.Add(part.ToLowerInvariant());
                }
            }

            return tags;
        }

        private static Dictionary<string, object> GetAssetDependencies(string graphPath)
        {
            var result = new Dictionary<string, object>();

            try
            {
                var dependencies = AssetDatabase.GetDependencies(graphPath, true);
                if (dependencies == null || dependencies.Length == 0)
                {
                    return result;
                }

                var textures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var meshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var materials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var shaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var dependencyPath in dependencies)
                {
                    if (string.IsNullOrEmpty(dependencyPath))
                    {
                        continue;
                    }

                    var normalizedPath = dependencyPath.Replace('\\', '/');
                    if (string.Equals(normalizedPath, graphPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var mainType = AssetDatabase.GetMainAssetTypeAtPath(normalizedPath);
                    if (mainType == null)
                    {
                        if (normalizedPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                        {
                            shaders.Add(normalizedPath);
                        }
                        continue;
                    }

                    if (typeof(Material).IsAssignableFrom(mainType))
                    {
                        materials.Add(normalizedPath);
                        continue;
                    }

                    if (typeof(Texture).IsAssignableFrom(mainType))
                    {
                        textures.Add(normalizedPath);
                        continue;
                    }

                    if (typeof(Mesh).IsAssignableFrom(mainType))
                    {
                        meshes.Add(normalizedPath);
                        continue;
                    }

                    if (typeof(Shader).IsAssignableFrom(mainType))
                    {
                        shaders.Add(normalizedPath);
                        continue;
                    }

                    if (normalizedPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                    {
                        shaders.Add(normalizedPath);
                    }
                }

                if (textures.Count > 0)
                {
                    result["textures"] = textures
                        .Select(path => path.Replace('\\', '/'))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                if (meshes.Count > 0)
                {
                    result["meshes"] = meshes
                        .Select(path => path.Replace('\\', '/'))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                if (materials.Count > 0)
                {
                    result["materials"] = materials
                        .Select(path => path.Replace('\\', '/'))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                if (shaders.Count > 0)
                {
                    result["shaders"] = shaders
                        .Select(path => path.Replace('\\', '/'))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Failed to gather asset dependencies: {ex.Message}");
            }

            return result;
        }

        private static List<Dictionary<string, object>> GetExposedParameters(string graphPath, object resource, int lod)
        {
            var paramsList = new List<Dictionary<string, object>>();

            try
            {
                // Get controller to access parameter models directly
                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out var error))
                {
                    Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Unable to get controller for parameters: {error}");
                    // Fallback to list_exposed_parameters tool
                    return GetExposedParametersFallback(graphPath);
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                var syncArgs = new object[] { false };
                controller.GetType()
                    .GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                // Get parameter models from controller
                var parameterModels = GetParametersFromController(controller, null).ToList();
                
                if (parameterModels.Count == 0)
                {
                    Debug.LogWarning($"[MCP Tools] describe_vfx_graph: No parameters found from controller, using fallback");
                    return GetExposedParametersFallback(graphPath);
                }

                Debug.Log($"[MCP Tools] describe_vfx_graph: Processing {parameterModels.Count} parameters from controller");

                foreach (var paramModel in parameterModels)
                {
                    if (paramModel == null) continue;

                    try
                    {
                        var paramType = paramModel.GetType();
                        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                        // Get exposed name
                        var exposedNameProp = paramType.GetProperty("exposedName", flags) 
                            ?? paramType.GetProperty("name", flags);
                        var exposedName = exposedNameProp?.GetValue(paramModel) as string;
                    
                        if (string.IsNullOrEmpty(exposedName))
                    {
                            Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Parameter has no name/exposedName. Skipping.");
                        continue;
                    }
                    
                        // Get value type
                        var valueTypeProp = paramType.GetProperty("valueType", flags);
                        var valueType = valueTypeProp?.GetValue(paramModel) as Type;
                        
                        // Get value property (used for both type inference and default value)
                        var valueProp = paramType.GetProperty("value", flags);
                        
                        if (valueType == null)
                        {
                            // Try to infer from value property
                            if (valueProp != null)
                            {
                                var value = valueProp.GetValue(paramModel);
                                valueType = value?.GetType();
                            }
                        }
                        
                        if (valueType == null)
                        {
                            Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Could not determine type for parameter '{exposedName}'. Skipping.");
                            continue;
                        }
                        
                        var paramInfo = new Dictionary<string, object>
                        {
                            ["k"] = exposedName
                        };
                        
                        string typeStr = MapTypeToString(valueType);
                        if (!IsSupportedParameterType(typeStr))
                        {
                            Debug.Log($"[MCP Tools] describe_vfx_graph: Skipping unsupported parameter '{exposedName}' (type: {typeStr})");
                            continue;
                        }
                        paramInfo["t"] = typeStr;
                        
                        // Get default value (reuse valueProp declared above)
                        var defaultValue = valueProp?.GetValue(paramModel);
                        
                        // Extract ranges from parameter model (controller-backed)
                        ExtractParameterRanges(paramModel, valueType, paramInfo, defaultValue);

                        var defaultTypeName = defaultValue?.GetType().FullName ?? "null";
                        Debug.Log($"[MCP Tools] describe_vfx_graph: Added parameter '{exposedName}' (type: {typeStr}, defaultType: {defaultTypeName})");
                        paramsList.Add(paramInfo);
                    }
                    catch (Exception paramEx)
                    {
                        Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Failed to convert parameter: {paramEx.Message}");
                    }
                }
                
                Debug.Log($"[MCP Tools] describe_vfx_graph: Successfully converted {paramsList.Count} parameters");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Error getting parameters: {ex.Message}");
                // Fallback to list_exposed_parameters tool
                return GetExposedParametersFallback(graphPath);
            }

            return paramsList;
        }

        private static List<Dictionary<string, object>> GetExposedParametersFallback(string graphPath)
        {
            var paramsList = new List<Dictionary<string, object>>();

            try
            {
                // Reuse the working ListExposedParametersTool logic as fallback
                var listParamsResponse = ListExposedParametersTool.HandleCommand(JObject.FromObject(new { graph_path = graphPath }));
                
                JObject responseObj = listParamsResponse as JObject;
                if (responseObj == null) return paramsList;
                
                var success = responseObj["success"]?.ToObject<bool>();
                if (success != true) return paramsList;
                
                var dataToken = responseObj["data"];
                if (dataToken == null) return paramsList;
                
                JObject data = dataToken as JObject;
                if (data == null && dataToken != null)
                {
                    data = JObject.FromObject(dataToken);
                }
                if (data == null) return paramsList;
                
                var parametersToken = data["parameters"];
                if (parametersToken == null) return paramsList;
                
                if (!(parametersToken is JArray parametersArray)) return paramsList;
                if (parametersArray.Count == 0) return paramsList;
                
                Debug.Log($"[MCP Tools] describe_vfx_graph: Using fallback, converting {parametersArray.Count} parameters");
                
                for (int paramIndex = 0; paramIndex < parametersArray.Count; paramIndex++)
                {
                    var paramToken = parametersArray[paramIndex];
                    if (!(paramToken is JObject param)) continue;
                    
                    var exposedName = param["exposedName"]?.ToString() ?? param["name"]?.ToString();
                    if (string.IsNullOrEmpty(exposedName)) continue;
                    
                    var valueTypeStr = param["valueType"]?.ToString();
                    var value = param["value"];
                    Type valueType = InferTypeFromValue(value, valueTypeStr);
                    if (valueType == null) continue;
                    
                    var typeStr = MapTypeToString(valueType);
                    if (!IsSupportedParameterType(typeStr))
                    {
                        Debug.Log($"[MCP Tools] describe_vfx_graph: (fallback) Skipping unsupported parameter '{exposedName}' (type: {typeStr})");
                        continue;
                    }

                    var paramInfo = new Dictionary<string, object>
                    {
                        ["k"] = exposedName,
                        ["t"] = typeStr
                    };
                    
                    ExtractParameterRangesFromValue(value, valueType, paramInfo);
                    paramsList.Add(paramInfo);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Fallback failed: {ex.Message}");
            }

            return paramsList;
        }

        private static Type InferTypeFromValue(JToken value, string valueTypeStr)
        {
            Type valueType = ResolveTypeFromString(valueTypeStr);
            
            // Infer from value content when type resolution fails
            if (valueType == null && value != null)
            {
                switch (value.Type)
                {
                    case JTokenType.Float:
                        valueType = typeof(float);
                        break;
                    case JTokenType.Integer:
                        valueType = typeof(int);
                        break;
                    case JTokenType.Boolean:
                        valueType = typeof(bool);
                        break;
                    case JTokenType.String:
                        valueType = typeof(string);
                        break;
                    case JTokenType.Array:
                        if (value is JArray arrayValue)
                        {
                            switch (arrayValue.Count)
                            {
                                case 2: valueType = typeof(Vector2); break;
                                case 3: valueType = typeof(Vector3); break;
                                case 4: valueType = typeof(Vector4); break;
                            }
                        }
                        break;
                    case JTokenType.Object:
                        var valueObj = value as JObject;
                        if (valueObj != null)
                        {
                            if (valueObj["x"] != null && valueObj["y"] != null && valueObj["z"] != null && valueObj["w"] != null)
                            {
                                valueType = typeof(Vector4);
                            }
                            else if (valueObj["x"] != null && valueObj["y"] != null && valueObj["z"] != null)
                            {
                                valueType = typeof(Vector3);
                            }
                            else if (valueObj["x"] != null && valueObj["y"] != null)
                            {
                                valueType = typeof(Vector2);
                            }
                            else if (valueObj["r"] != null || valueObj["g"] != null || valueObj["b"] != null)
                            {
                                valueType = typeof(Color);
                            }
                        }
                        break;
                }
            }
            
            return valueType;
        }

        private static Type ResolveTypeFromString(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    foreach (var candidate in rtle.Types)
                    {
                        if (candidate != null && candidate.FullName == typeName)
                        {
                            return candidate;
                        }
                    }
                }
                catch
                {
                    // Ignore failures from dynamic/loading assemblies
                }
            }

            return null;
        }

        private static void ExtractParameterRangesFromValue(JToken value, Type valueType, Dictionary<string, object> paramInfo)
        {
            if (valueType == typeof(float) || valueType == typeof(double))
            {
                var floatVal = value?.ToObject<float>() ?? 0f;
                paramInfo["min"] = 0.0f;
                paramInfo["max"] = Math.Max(100.0f, floatVal * 2f); // Heuristic
                paramInfo["def"] = floatVal;
            }
            else if (valueType == typeof(int))
            {
                var intVal = value?.ToObject<int>() ?? 0;
                paramInfo["min"] = 0;
                paramInfo["max"] = Math.Max(100, intVal * 2); // Heuristic
                paramInfo["def"] = intVal;
            }
            else if (valueType == typeof(Vector2))
            {
                var v2 = value?.ToObject<Vector2>() ?? Vector2.zero;
                paramInfo["min"] = new[] { 0f, 0f };
                paramInfo["max"] = new[] { 10f, 10f }; // Heuristic
                paramInfo["def"] = new[] { v2.x, v2.y };
            }
            else if (valueType == typeof(Vector3))
            {
                var v3 = value?.ToObject<Vector3>() ?? Vector3.zero;
                paramInfo["min"] = new[] { -10f, -10f, -10f };
                paramInfo["max"] = new[] { 10f, 10f, 10f }; // Heuristic
                paramInfo["def"] = new[] { v3.x, v3.y, v3.z };
            }
            else if (valueType == typeof(Color))
            {
                paramInfo["min"] = new[] { 0f, 0f, 0f, 0f };
                paramInfo["max"] = new[] { 1f, 1f, 1f, 1f };
                if (value is JObject colorObj)
                {
                    paramInfo["def"] = new[] { 
                        colorObj["r"]?.ToObject<float>() ?? 1f,
                        colorObj["g"]?.ToObject<float>() ?? 1f,
                        colorObj["b"]?.ToObject<float>() ?? 1f,
                        colorObj["a"]?.ToObject<float>() ?? 1f
                    };
                }
                else
                {
                    paramInfo["def"] = new[] { 1f, 1f, 1f, 1f };
                }
            }
            else if (valueType == typeof(Vector4))
            {
                paramInfo["min"] = new[] { -10f, -10f, -10f, -10f };
                paramInfo["max"] = new[] { 10f, 10f, 10f, 10f };
                if (value is JObject vec4Obj)
                {
                    paramInfo["def"] = new[]
                    {
                        vec4Obj["x"]?.ToObject<float>() ?? 0f,
                        vec4Obj["y"]?.ToObject<float>() ?? 0f,
                        vec4Obj["z"]?.ToObject<float>() ?? 0f,
                        vec4Obj["w"]?.ToObject<float>() ?? 0f
                    };
                }
                else if (value is JArray vec4Arr && vec4Arr.Count >= 4)
                {
                    paramInfo["def"] = new[]
                    {
                        vec4Arr[0]?.ToObject<float>() ?? 0f,
                        vec4Arr[1]?.ToObject<float>() ?? 0f,
                        vec4Arr[2]?.ToObject<float>() ?? 0f,
                        vec4Arr[3]?.ToObject<float>() ?? 0f
                    };
                }
                else
                {
                    paramInfo["def"] = new[] { 0f, 0f, 0f, 0f };
                }
            }
            else if (valueType == typeof(bool))
            {
                paramInfo["def"] = value?.ToObject<bool>() ?? false;
            }
            else if (valueType == typeof(string))
            {
                paramInfo["def"] = value?.ToString() ?? string.Empty;
            }
        }

        private static string MapTypeToString(Type type)
        {
            if (type == typeof(float) || type == typeof(double))
                return "Float";
            if (type == typeof(int))
                return "Int";
            if (type == typeof(bool))
                return "Bool";
            if (type == typeof(Vector2))
                return "Vector2";
            if (type == typeof(Vector3))
                return "Vector3";
            if (type == typeof(Vector4))
                return "Vector4";
            if (type == typeof(Color))
                return "Color";
            if (type == typeof(Texture2D))
                return "Texture2D";

            return type.Name;
        }

        private static void ExtractParameterRanges(object param, Type valueType, Dictionary<string, object> paramInfo, object defaultValue)
        {
            var paramType = param.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Try to get min/max properties from parameter model
            var minProperty = paramType.GetProperty("min", flags);
            var maxProperty = paramType.GetProperty("max", flags);

            if (valueType == typeof(float) || valueType == typeof(double))
            {
                float minVal = minProperty != null ? Convert.ToSingle(minProperty.GetValue(param)) : 0.0f;
                float maxVal = maxProperty != null ? Convert.ToSingle(maxProperty.GetValue(param)) : 100.0f;
                float defVal = defaultValue != null ? Convert.ToSingle(defaultValue) : 0.0f;
                
                // If min/max are not set, use heuristic based on default value
                if (minProperty == null || maxProperty == null)
                {
                    minVal = 0.0f;
                    maxVal = Math.Max(100.0f, Math.Abs(defVal) * 2f);
                }
                
                paramInfo["min"] = minVal;
                paramInfo["max"] = maxVal;
                paramInfo["def"] = defVal;
            }
            else if (valueType == typeof(int))
            {
                int minVal = minProperty != null ? Convert.ToInt32(minProperty.GetValue(param)) : 0;
                int maxVal = maxProperty != null ? Convert.ToInt32(maxProperty.GetValue(param)) : 100;
                int defVal = defaultValue != null ? Convert.ToInt32(defaultValue) : 0;
                
                if (minProperty == null || maxProperty == null)
                {
                    minVal = 0;
                    maxVal = Math.Max(100, Math.Abs(defVal) * 2);
                }
                
                paramInfo["min"] = minVal;
                paramInfo["max"] = maxVal;
                paramInfo["def"] = defVal;
            }
            else if (valueType == typeof(Color))
            {
                // Color always uses [0,1] range per channel
                paramInfo["min"] = new[] { 0f, 0f, 0f, 0f };
                paramInfo["max"] = new[] { 1f, 1f, 1f, 1f };
                
                if (defaultValue is Color color)
                {
                    paramInfo["def"] = new[] { color.r, color.g, color.b, color.a };
                }
                else if (defaultValue is Vector4 vec4)
                {
                    paramInfo["def"] = new[] { vec4.x, vec4.y, vec4.z, vec4.w };
                }
                else
                {
                    paramInfo["def"] = new[] { 1f, 1f, 1f, 1f };
                }
            }
            else if (valueType == typeof(Vector2))
            {
                // Try to get min/max as Vector2, otherwise use defaults
                Vector2 minVec = Vector2.zero;
                Vector2 maxVec = new Vector2(10f, 10f);
                
                if (minProperty != null)
                {
                    var minVal = minProperty.GetValue(param);
                    if (minVal is Vector2 v2min) minVec = v2min;
                }
                if (maxProperty != null)
                {
                    var maxVal = maxProperty.GetValue(param);
                    if (maxVal is Vector2 v2max) maxVec = v2max;
                }
                
                paramInfo["min"] = new[] { minVec.x, minVec.y };
                paramInfo["max"] = new[] { maxVec.x, maxVec.y };
                
                if (defaultValue is Vector2 vec2)
                {
                    paramInfo["def"] = new[] { vec2.x, vec2.y };
                }
                else
                {
                    paramInfo["def"] = new[] { 0f, 0f };
                }
            }
            else if (valueType == typeof(Vector3))
            {
                Vector3 minVec = new Vector3(-10f, -10f, -10f);
                Vector3 maxVec = new Vector3(10f, 10f, 10f);
                
                if (minProperty != null)
                {
                    var minVal = minProperty.GetValue(param);
                    if (minVal is Vector3 v3min) minVec = v3min;
                }
                if (maxProperty != null)
                {
                    var maxVal = maxProperty.GetValue(param);
                    if (maxVal is Vector3 v3max) maxVec = v3max;
                }
                
                paramInfo["min"] = new[] { minVec.x, minVec.y, minVec.z };
                paramInfo["max"] = new[] { maxVec.x, maxVec.y, maxVec.z };
                
                if (defaultValue is Vector3 vec3)
                {
                    paramInfo["def"] = new[] { vec3.x, vec3.y, vec3.z };
                }
                else
                {
                    paramInfo["def"] = new[] { 0f, 0f, 0f };
                }
            }
            else if (valueType == typeof(Vector4))
            {
                Vector4 minVec = new Vector4(-10f, -10f, -10f, -10f);
                Vector4 maxVec = new Vector4(10f, 10f, 10f, 10f);
                
                if (minProperty != null)
                {
                    var minVal = minProperty.GetValue(param);
                    if (minVal is Vector4 v4min) minVec = v4min;
                }
                if (maxProperty != null)
                {
                    var maxVal = maxProperty.GetValue(param);
                    if (maxVal is Vector4 v4max) maxVec = v4max;
                }
                
                paramInfo["min"] = new[] { minVec.x, minVec.y, minVec.z, minVec.w };
                paramInfo["max"] = new[] { maxVec.x, maxVec.y, maxVec.z, maxVec.w };
                
                if (defaultValue is Vector4 vec4)
                {
                    paramInfo["def"] = new[] { vec4.x, vec4.y, vec4.z, vec4.w };
                }
                else
                {
                    paramInfo["def"] = new[] { 0f, 0f, 0f, 0f };
                }
            }
            else if (valueType == typeof(bool))
            {
                paramInfo["def"] = defaultValue != null ? Convert.ToBoolean(defaultValue) : false;
            }
            else if (valueType == typeof(string))
            {
                paramInfo["def"] = defaultValue?.ToString() ?? string.Empty;
            }
            else
            {
                // Default ranges for unknown types - store as string to avoid serialization loops
                paramInfo["def"] = defaultValue?.ToString() ?? string.Empty;
            }
        }

        private static Dictionary<string, List<string>> GetStages(string graphPath, object resource, int lod)
        {
            var stages = new Dictionary<string, List<string>>();

            try
            {
                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out var error))
                {
                    Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Unable to get controller for stages: {error}");
                    return stages;
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                var syncArgs = new object[] { false };
                controller.GetType()
                    .GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                var contextMap = BuildContextMap(controller);
                var vfxContextType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContext");

                foreach (var kvp in contextMap)
                {
                    var contextController = kvp.Value;
                    var model = VfxGraphReflectionHelpers.GetProperty(contextController, "model") as UnityEngine.Object;
                    if (model == null || vfxContextType == null || !vfxContextType.IsAssignableFrom(model.GetType()))
                    {
                        continue;
                    }

                    // Determine context type (Spawn, Initialize, Update, Output)
                    string stageName = DetermineStageName(model);
                    if (string.IsNullOrEmpty(stageName))
                    {
                        continue;
                    }

                    // Get blocks for this context
                    var blocks = GetContextBlocks(model);
                    var blockNames = new List<string>();

                    foreach (var block in blocks)
                    {
                        var blockType = block.GetType();
                        var blockName = ExtractBlockName(block, blockType);
                        if (!string.IsNullOrEmpty(blockName))
                        {
                            blockNames.Add(blockName);
                        }
                    }

                    if (!stages.ContainsKey(stageName))
                    {
                        stages[stageName] = new List<string>();
                    }

                    stages[stageName].AddRange(blockNames);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Error getting stages: {ex.Message}");
            }

            return stages;
        }

        private static string DetermineStageName(UnityEngine.Object contextModel)
        {
            var contextType = contextModel.GetType();
            var typeName = contextType.Name.ToLowerInvariant();

            if (typeName.Contains("spawn"))
                return "Spawn";
            if (typeName.Contains("initialize") || typeName.Contains("init"))
                return "Initialize";
            if (typeName.Contains("update"))
                return "Update";
            if (typeName.Contains("output"))
                return "Output";

            // Try to get type property
            try
            {
                var typeProperty = contextType.GetProperty("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (typeProperty != null)
                {
                    var typeValue = typeProperty.GetValue(contextModel);
                    var typeStr = typeValue?.ToString().ToLowerInvariant() ?? "";
                    if (typeStr.Contains("spawn")) return "Spawn";
                    if (typeStr.Contains("init")) return "Initialize";
                    if (typeStr.Contains("update")) return "Update";
                    if (typeStr.Contains("output")) return "Output";
                }
            }
            catch { }

            return null;
        }

        private static string ExtractBlockName(object block, Type blockType)
        {
            // Try to get a readable name from the block
            var nameProperty = blockType.GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (nameProperty != null)
            {
                try
                {
                    var name = nameProperty.GetValue(block) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        // Clean up the name (remove prefixes like "|Set|_")
                        return name.Replace("|Set|_", "").Replace("|", "").Trim();
                    }
                }
                catch { }
            }

            // Fallback to type name
            var typeName = blockType.Name;
            if (typeName.Contains("."))
            {
                typeName = typeName.Split('.').Last();
            }
            return typeName;
        }

        private static Dictionary<int, object> BuildContextMap(object controller)
        {
            var map = new Dictionary<int, object>();
            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model != null)
                {
                    var modelType = model.GetType();
                    var vfxContextType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContext");
                    if (vfxContextType != null && vfxContextType.IsAssignableFrom(modelType))
                    {
                        map[model.GetInstanceID()] = nodeController;
                    }
                }
            }
            return map;
        }

        private static IEnumerable<object> GetContextBlocks(UnityEngine.Object contextModel)
        {
            if (contextModel == null)
            {
                return Array.Empty<object>();
            }

            var contextType = contextModel.GetType();
            var blocksProperty = contextType.GetProperty("blocks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? contextType.GetProperty("m_Blocks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (blocksProperty != null)
            {
                var blocks = blocksProperty.GetValue(contextModel);
                if (blocks is IEnumerable enumerable)
                {
                    return VfxGraphReflectionHelpers.Enumerate(enumerable).Cast<object>();
                }
            }

            return Array.Empty<object>();
        }

        private static IEnumerable<object> GetParametersFromController(object controller, object graph)
        {
            if (controller == null && graph == null)
            {
                return Array.Empty<object>();
            }

            var parameters = new List<object>();

            if (controller != null)
            {
                var controllerType = controller.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // Get parameter controllers (same approach as ListExposedParametersTool)
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
                }

                // Extract model from each parameter controller (critical step!)
                if (parameterControllers != null)
                {
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
                        if (model != null)
                        {
                            parameters.Add(model);
                        }
                    }
                }
            }

            // Fallback to graph if no parameters found from controller
            if (parameters.Count == 0 && graph != null)
            {
                var graphType = graph.GetType();
                var parametersProperty = graphType.GetProperty("parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? graphType.GetProperty("m_Parameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (parametersProperty != null)
                {
                    var graphParams = parametersProperty.GetValue(graph);
                    if (graphParams is IEnumerable enumerable)
                    {
                        parameters.AddRange(VfxGraphReflectionHelpers.Enumerate(enumerable).Cast<object>());
                    }
                }
            }

            return parameters;
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

        private static object GetPropertyOrMethod(object obj, Type objType, BindingFlags flags, string[] propertyNames, string[] methodNames)
        {
            foreach (var propName in propertyNames)
            {
                var prop = objType.GetProperty(propName, flags);
                if (prop != null)
                {
                    var value = prop.GetValue(obj);
                    if (value != null)
                    {
                        return value;
                    }
                }
            }

            foreach (var methodName in methodNames)
            {
                var method = objType.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    var value = method.Invoke(obj, null);
                    if (value != null)
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        private static Dictionary<string, object> GetGraphStructure(object resource, int lod)
        {
            try
            {
                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out var error))
                {
                    Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Unable to get controller for structure: {error}");
                    return null;
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                var syncArgs = new object[] { false };
                controller.GetType()
                    .GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                var nodes = BuildNodeSummaries(controller);
                var dataConnections = BuildDataConnections(controller);
                var flowConnections = BuildFlowConnections(controller);

                // Extract node motifs (simplified types for training)
                var nodeTypes = new Dictionary<string, int>();
                var nodeMotifs = new List<string>();
                
                foreach (var node in nodes)
                {
                    var nodeType = node.ContainsKey("type") ? node["type"]?.ToString() : "";
                    var nodeTitle = node.ContainsKey("title") ? node["title"]?.ToString() : "";
                    
                    // Extract motif from type name (e.g., "VFXSubgraphContext" -> "Subgraph", "Random" -> "Random")
                    var motif = ExtractNodeMotif(nodeType, nodeTitle);
                    if (!string.IsNullOrEmpty(motif))
                    {
                        nodeMotifs.Add(motif);
                        nodeTypes[motif] = nodeTypes.GetValueOrDefault(motif, 0) + 1;
                    }
                }

                return new Dictionary<string, object>
                {
                    ["node_count"] = nodes.Count,
                    ["data_connection_count"] = dataConnections.Count,
                    ["flow_connection_count"] = flowConnections.Count,
                    ["node_motifs"] = nodeMotifs,
                    ["node_type_counts"] = nodeTypes,
                    ["nodes"] = nodes.Select(n => new Dictionary<string, object>
                    {
                        ["id"] = n.ContainsKey("id") ? n["id"] : 0,
                        ["type"] = n.ContainsKey("type") ? n["type"] : "",
                        ["title"] = n.ContainsKey("title") ? n["title"] : "",
                        ["motif"] = ExtractNodeMotif(n.ContainsKey("type") ? n["type"]?.ToString() : "", n.ContainsKey("title") ? n["title"]?.ToString() : "")
                    }).ToList(),
                    ["connections"] = dataConnections.Select(c => new Dictionary<string, object>
                    {
                        ["source_id"] = c.ContainsKey("sourceNodeId") ? c["sourceNodeId"] : 0,
                        ["target_id"] = c.ContainsKey("targetNodeId") ? c["targetNodeId"] : 0,
                        ["source_port"] = c.ContainsKey("sourcePort") ? c["sourcePort"] : "",
                        ["target_port"] = c.ContainsKey("targetPort") ? c["targetPort"] : ""
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] describe_vfx_graph: Error getting structure: {ex.Message}");
                return null;
            }
        }

        private static string ExtractNodeMotif(string nodeType, string nodeTitle)
        {
            if (string.IsNullOrEmpty(nodeType)) return "";
            
            // Extract meaningful motif from type name
            var typeLower = nodeType.ToLowerInvariant();
            if (typeLower.Contains("subgraph")) return "Subgraph";
            if (typeLower.Contains("random")) return "Random";
            if (typeLower.Contains("noise")) return "Noise";
            if (typeLower.Contains("multiply")) return "Multiply";
            if (typeLower.Contains("add")) return "Add";
            if (typeLower.Contains("remap")) return "Remap";
            if (typeLower.Contains("parameter")) return "Parameter";
            if (typeLower.Contains("operator")) return "Operator";
            if (typeLower.Contains("context"))
            {
                if (typeLower.Contains("spawn")) return "SpawnContext";
                if (typeLower.Contains("init")) return "InitContext";
                if (typeLower.Contains("update")) return "UpdateContext";
                if (typeLower.Contains("output")) return "OutputContext";
                return "Context";
            }
            
            // Use title if available and type is generic
            if (!string.IsNullOrEmpty(nodeTitle))
            {
                return nodeTitle.Replace("|", "").Replace("_", "").Trim();
            }
            
            return nodeType.Split('.').LastOrDefault() ?? "";
        }

        private static List<Dictionary<string, object>> BuildNodeSummaries(object controller)
        {
            var result = new List<Dictionary<string, object>>();
            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                try
                {
                    var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                    if (model == null) continue;
                    
                    var nodeId = model.GetInstanceID();
                    var nodeType = model.GetType();
                    var position = Vector2.zero;
                    
                    var positionProperty = nodeType.GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (positionProperty != null)
                    {
                        var rawPosition = positionProperty.GetValue(model);
                        if (rawPosition is Vector2 typedPosition)
                        {
                            position = typedPosition;
                        }
                    }
                    
                    var title = VfxGraphReflectionHelpers.GetProperty(nodeController, "title") as string ?? "";
                    
                    result.Add(new Dictionary<string, object>
                    {
                        ["id"] = nodeId,
                        ["type"] = nodeType.FullName,
                        ["title"] = title,
                        ["position"] = new Dictionary<string, object> { ["x"] = position.x, ["y"] = position.y }
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] Failed to build node summary: {ex.Message}");
                }
            }
            
            return result;
        }

        private static List<Dictionary<string, object>> BuildDataConnections(object controller)
        {
            var connections = new List<Dictionary<string, object>>();
            
            try
            {
                var dataEdgesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "dataEdges");
                if (dataEdgesEnumerable == null) return connections;
                
                foreach (var edgeController in VfxGraphReflectionHelpers.Enumerate(dataEdgesEnumerable))
                {
                    try
                    {
                        var sourceNode = VfxGraphReflectionHelpers.GetProperty(edgeController, "source");
                        var targetNode = VfxGraphReflectionHelpers.GetProperty(edgeController, "target");
                        var sourceSlot = VfxGraphReflectionHelpers.GetProperty(edgeController, "inputSlot");
                        var targetSlot = VfxGraphReflectionHelpers.GetProperty(edgeController, "outputSlot");
                        
                        var sourceModel = VfxGraphReflectionHelpers.GetProperty(sourceNode, "model") as UnityEngine.Object;
                        var targetModel = VfxGraphReflectionHelpers.GetProperty(targetNode, "model") as UnityEngine.Object;
                        
                        if (sourceModel == null || targetModel == null) continue;
                        
                        var sourcePort = VfxGraphReflectionHelpers.GetProperty(sourceSlot, "path") as string ?? "";
                        var targetPort = VfxGraphReflectionHelpers.GetProperty(targetSlot, "path") as string ?? "";
                        
                        connections.Add(new Dictionary<string, object>
                        {
                            ["sourceNodeId"] = sourceModel.GetInstanceID(),
                            ["targetNodeId"] = targetModel.GetInstanceID(),
                            ["sourcePort"] = sourcePort,
                            ["targetPort"] = targetPort
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MCP Tools] Failed to build data connection: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] Error building data connections: {ex.Message}");
            }
            
            return connections;
        }

        private static List<Dictionary<string, object>> BuildFlowConnections(object controller)
        {
            var connections = new List<Dictionary<string, object>>();
            
            try
            {
                var flowEdgesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "flowEdges");
                if (flowEdgesEnumerable == null) return connections;
                
                foreach (var edgeController in VfxGraphReflectionHelpers.Enumerate(flowEdgesEnumerable))
                {
                    try
                    {
                        var sourceNode = VfxGraphReflectionHelpers.GetProperty(edgeController, "source");
                        var targetNode = VfxGraphReflectionHelpers.GetProperty(edgeController, "target");
                        
                        var sourceModel = VfxGraphReflectionHelpers.GetProperty(sourceNode, "model") as UnityEngine.Object;
                        var targetModel = VfxGraphReflectionHelpers.GetProperty(targetNode, "model") as UnityEngine.Object;
                        
                        if (sourceModel == null || targetModel == null) continue;
                        
                        connections.Add(new Dictionary<string, object>
                        {
                            ["sourceNodeId"] = sourceModel.GetInstanceID(),
                            ["targetNodeId"] = targetModel.GetInstanceID()
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MCP Tools] Failed to build flow connection: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] Error building flow connections: {ex.Message}");
            }
            
            return connections;
        }
    }
}

