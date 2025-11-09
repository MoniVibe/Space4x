using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Utility helpers for working with Unity's Shader Graph editor types via reflection.
    /// </summary>
    public static class ShaderGraphReflectionHelpers
    {
        private const string AssemblyName = "Unity.ShaderGraph.Editor";

        private static Assembly _editorAssembly;
        private static Type _graphDataType;
        private static Type _materialGraphType;
        private static Type _subGraphType;
        private static Type _graphAssetType;
        private static Type _nodeType;
        private static Type _edgeType;
        private static Type _shaderInputType;
        private static Type _blackboardType;

        private static bool ParameterMatches(Type expectedType, Type actualType)
        {
            if (expectedType == null)
            {
                return false;
            }

            if (expectedType.IsByRef)
            {
                expectedType = expectedType.GetElementType();
            }

            if (actualType != null && actualType.IsByRef)
            {
                actualType = actualType.GetElementType();
            }

            if (actualType == null)
            {
                if (!expectedType.IsValueType)
                {
                    return true;
                }
                return Nullable.GetUnderlyingType(expectedType) != null;
            }

            if (expectedType == actualType || expectedType.IsAssignableFrom(actualType))
            {
                return true;
            }

            if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlying = Nullable.GetUnderlyingType(expectedType);
                if (underlying != null)
                {
                    return underlying.IsAssignableFrom(actualType);
                }
            }

            return false;
        }

        public static MethodInfo GetMethodInfo(Type type, string methodName, BindingFlags flags, object[] parameters = null, Type[] parameterTypes = null)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var methods = type.GetMethods(flags).Where(m => m.Name == methodName).ToArray();
            if (methods.Length == 0)
            {
                return null;
            }

            Type[] desiredTypes = parameterTypes;
            if ((desiredTypes == null || desiredTypes.Length == 0) && parameters != null)
            {
                desiredTypes = parameters.Select(p => p?.GetType()).ToArray();
            }

            if (desiredTypes != null && desiredTypes.Length > 0)
            {
                foreach (var method in methods)
                {
                    var paramInfos = method.GetParameters();
                    if (paramInfos.Length != desiredTypes.Length)
                    {
                        continue;
                    }

                    var allMatch = true;
                    for (int i = 0; i < paramInfos.Length; i++)
                    {
                        if (!ParameterMatches(paramInfos[i].ParameterType, desiredTypes[i]))
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (allMatch)
                    {
                        return method;
                    }
                }
            }

            if (parameters != null && parameters.Length > 0)
            {
                foreach (var method in methods)
                {
                    var paramInfos = method.GetParameters();
                    if (paramInfos.Length != parameters.Length)
                    {
                        continue;
                    }

                    var allMatch = true;
                    for (int i = 0; i < paramInfos.Length; i++)
                    {
                        var actualType = parameters[i]?.GetType();
                        if (!ParameterMatches(paramInfos[i].ParameterType, actualType))
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (allMatch)
                    {
                        return method;
                    }
                }
            }

            if (methods.Length == 1)
            {
                return methods[0];
            }

            return methods.FirstOrDefault(m => m.GetParameters().Length == 0) ?? methods.FirstOrDefault();
        }

        private static PropertyInfo GetPropertyInfo(Type type, string propertyName, BindingFlags flags)
        {
            if (type == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var properties = type.GetProperties(flags).Where(p => p.Name == propertyName).ToArray();
            if (properties.Length == 0)
            {
                return null;
            }

            if (properties.Length == 1)
            {
                return properties[0];
            }

            return properties.FirstOrDefault();
        }

        private static Assembly GetAssembly()
        {
            if (_editorAssembly != null)
            {
                return _editorAssembly;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var name = assembly.GetName().Name;
                    if (string.Equals(name, AssemblyName, StringComparison.Ordinal))
                    {
                        _editorAssembly = assembly;
                        return _editorAssembly;
                    }
                }
                catch
                {
                    // Ignore assemblies that cannot be inspected
                }
            }

            var loadedCandidates = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .Where(n => n != null && n.IndexOf("ShaderGraph", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            Debug.Log($"[MCP Tools] Loaded assemblies containing 'ShaderGraph': {string.Join(", ", loadedCandidates)}");

            // Try to find by partial match in loaded assemblies (for Unity 6000+)
            if (loadedCandidates.Length > 0)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var name = assembly.GetName().Name;
                        if (name != null && name.IndexOf("ShaderGraph", StringComparison.OrdinalIgnoreCase) >= 0 && 
                            name.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _editorAssembly = assembly;
                            Debug.Log($"[MCP Tools] Found Shader Graph assembly via partial match: {name} (FullName: {assembly.FullName})");
                            return _editorAssembly;
                        }
                    }
                    catch
                    {
                        // Ignore assemblies that cannot be inspected
                    }
                }
            }

            try
            {
                _editorAssembly = Assembly.Load(AssemblyName);
            }
            catch (Exception loadEx)
            {
                Debug.LogWarning($"[MCP Tools] Assembly.Load failed for {AssemblyName}: {loadEx.Message}");
                _editorAssembly = null;
            }

            if (_editorAssembly == null)
            {
                var projectPath = Directory.GetCurrentDirectory();
                var assemblyPath = Path.Combine(projectPath, "Library", "ScriptAssemblies", $"{AssemblyName}.dll");
                if (File.Exists(assemblyPath))
                {
                    try
                    {
                        _editorAssembly = Assembly.LoadFrom(assemblyPath);
                    }
                    catch (Exception loadEx)
                    {
                        Debug.LogWarning($"[MCP Tools] Failed to load {AssemblyName} from '{assemblyPath}': {loadEx.Message}");
                        _editorAssembly = null;
                    }
                }
                else
                {
                    Debug.LogWarning($"[MCP Tools] Could not locate {AssemblyName} at '{assemblyPath}'");
                }
            }

            if (_editorAssembly != null)
            {
                Debug.Log($"[MCP Tools] Loaded {AssemblyName} from assembly '{_editorAssembly.FullName}'");
            }

            return _editorAssembly;
        }

        public static Type GetEditorType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            var assembly = GetAssembly();
            if (assembly == null)
            {
                Debug.LogWarning($"[MCP Tools] Shader Graph editor assembly not loaded while resolving type '{fullName}'");
                return null;
            }

            var type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (type != null)
            {
                return type;
            }

            type = assembly.GetTypes().FirstOrDefault(t => string.Equals(t.FullName, fullName, StringComparison.Ordinal));
            if (type == null)
            {
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(fullName, throwOnError: false, ignoreCase: false))
                    .FirstOrDefault(t => t != null);
            }

            if (type == null)
            {
                Debug.LogWarning($"[MCP Tools] Type '{fullName}' not found in any loaded assembly.");
            }

            return type;
        }

        // Type properties
        private static Type GraphDataType => _graphDataType ??= GetEditorType("UnityEditor.ShaderGraph.GraphData") ?? GetEditorType("UnityEditor.ShaderGraph.AbstractMaterialGraph");
        private static Type MaterialGraphType => _materialGraphType ??= GetEditorType("UnityEditor.ShaderGraph.MaterialGraph");
        private static Type SubGraphType => _subGraphType ??= GetEditorType("UnityEditor.ShaderGraph.SubGraph");
        private static Type GraphAssetType => _graphAssetType ??= GetEditorType("UnityEditor.ShaderGraph.GraphAsset");
        private static Type NodeType => _nodeType ??= GetEditorType("UnityEditor.ShaderGraph.AbstractMaterialNode");
        private static Type EdgeType => _edgeType ??= GetEditorType("UnityEditor.ShaderGraph.IEdge");
        private static Type ShaderInputType => _shaderInputType ??= GetEditorType("UnityEditor.ShaderGraph.AbstractShaderProperty");
        private static Type BlackboardType => _blackboardType ??= GetEditorType("UnityEditor.ShaderGraph.Blackboard");

        public static bool TryGetGraphData(string graphPath, out object graphData, out string error)
        {
            graphData = null;
            error = null;

            if (string.IsNullOrEmpty(graphPath))
            {
                error = "graph_path is required";
                return false;
            }

            if (GraphAssetType == null)
            {
                error = "Unity Shader Graph editor assembly not found";
                return false;
            }

            // Load the graph asset
            var graphAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath);
            if (graphAsset == null)
            {
                error = $"Graph asset not found at path: {graphPath}";
                return false;
            }

            // Try to get graphData property from the asset
            var graphDataProp = GetPropertyInfo(GraphAssetType, "graphData", BindingFlags.Public | BindingFlags.Instance);
            if (graphDataProp == null)
            {
                // Try alternative property names
                graphDataProp = GetPropertyInfo(GraphAssetType, "graph", BindingFlags.Public | BindingFlags.Instance);
            }

            if (graphDataProp != null)
            {
                try
                {
                    graphData = graphDataProp.GetValue(graphAsset);
                    if (graphData != null)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    error = $"Failed to get graphData: {ex.Message}";
                    return false;
                }
            }

            error = "Unable to access graphData property on graph asset";
            return false;
        }

        public static bool TryCreateGraph(string graphPath, out object graphAsset, out string error)
        {
            graphAsset = null;
            error = null;

            if (string.IsNullOrEmpty(graphPath))
            {
                error = "graph_path is required";
                return false;
            }

            if (!graphPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) && 
                !graphPath.EndsWith(".shadersubgraph", StringComparison.OrdinalIgnoreCase))
            {
                error = "graph_path must end with .shadergraph or .shadersubgraph";
                return false;
            }

            if (File.Exists(graphPath))
            {
                error = $"Graph asset already exists at path: {graphPath}";
                return false;
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(graphPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                bool isSubGraph = graphPath.EndsWith(".shadersubgraph", StringComparison.OrdinalIgnoreCase);
                
                // Create a minimal valid Shader Graph file structure
                // Shader Graphs are YAML-based
                var fileId = System.DateTime.UtcNow.Ticks.GetHashCode() & 0x7FFFFFFF;
                var graphGuid = System.Guid.NewGuid().ToString("N").Substring(0, 32);
                
                // Determine the script GUID based on type
                string scriptGuid;
                if (isSubGraph)
                {
                    // SubGraph GUID: 71f5b5c5-4b8e-4a0a-9b3c-8e4f5a6b7c8d (example, need actual)
                    scriptGuid = "71f5b5c5-4b8e-4a0a-9b3c-8e4f5a6b7c8d";
                }
                else
                {
                    // ShaderGraph GUID: 12f5b5c5-4b8e-4a0a-9b3c-8e4f5a6b7c8d (example, need actual)
                    scriptGuid = "12f5b5c5-4b8e-4a0a-9b3c-8e4f5a6b7c8d";
                }

                // Create minimal YAML structure
                // Note: This is a simplified structure. Actual Shader Graph files are more complex.
                var minimalContent = $@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &{fileId}
MonoBehaviour:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
";

                File.WriteAllText(graphPath, minimalContent);
                
                // Import the asset
                AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();
                
                // Wait for import
                System.Threading.Thread.Sleep(200);
                AssetDatabase.Refresh();

                // Load the created asset
                graphAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath);
                if (graphAsset != null)
                {
                    return true;
                }

                error = "Failed to load created graph asset";
                return false;
            }
            catch (Exception ex)
            {
                error = $"Exception creating graph: {ex.Message}";
                Debug.LogError($"[MCP Tools] Failed to create Shader Graph: {ex}");
                return false;
            }
        }

        public static object InvokeInstanceMethod(object instance, string methodName, BindingFlags flags, params object[] parameters)
        {
            if (instance == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var type = instance.GetType();
            var method = GetMethodInfo(type, methodName, flags, parameters);
            if (method == null)
            {
                Debug.LogWarning($"[MCP Tools] Method '{methodName}' not found on type {type.Name}");
                return null;
            }

            try
            {
                return method.Invoke(instance, parameters);
            }
            catch (TargetInvocationException tie)
            {
                Debug.LogError($"[MCP Tools] Exception invoking {methodName}: {tie.InnerException?.Message ?? tie.Message}");
                return null;
            }
        }

        public static object GetProperty(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var type = instance.GetType();
            var prop = GetPropertyInfo(type, propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop == null)
            {
                return null;
            }

            try
            {
                return prop.GetValue(instance);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Tools] Exception getting property {propertyName}: {ex.Message}");
                return null;
            }
        }

        public static bool SetProperty(object instance, string propertyName, object value)
        {
            if (instance == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            var type = instance.GetType();
            var prop = GetPropertyInfo(type, propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop == null)
            {
                return false;
            }

            try
            {
                prop.SetValue(instance, value);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Tools] Exception setting property {propertyName}: {ex.Message}");
                return false;
            }
        }
    }
}

