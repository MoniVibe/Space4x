using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Diagnostic tool to inspect Unity VFX Graph API structure in the current Unity version.
    /// Run via: Unity Editor Menu > MCP > Diagnose VFX API
    /// </summary>
    public static class DiagnoseVfxApi
    {
        [MenuItem("MCP/Diagnose VFX API")]
        public static void Diagnose()
        {
            Debug.Log("=== VFX Graph API Diagnosis ===");
            
            // 1. Check assembly loading
            DiagnoseAssembly();
            
            // 2. Check core types
            DiagnoseCoreTypes();
            
            // 3. Check a known graph asset
            DiagnoseKnownGraph();
            
            // 4. Check resource loading methods
            DiagnoseResourceMethods();
            
            // 5. Check controller methods
            DiagnoseControllerMethods();
            
            Debug.Log("=== Diagnosis Complete ===");
        }
        
        private static void DiagnoseAssembly()
        {
            Debug.Log("\n--- Assembly Loading ---");
            const string AssemblyName = "Unity.VisualEffectGraph.Editor";
            
            Assembly editorAssembly = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var name = assembly.GetName().Name;
                    if (string.Equals(name, AssemblyName, StringComparison.Ordinal))
                    {
                        editorAssembly = assembly;
                        Debug.Log($"✓ Found assembly: {name} (FullName: {assembly.FullName})");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to inspect assembly: {ex.Message}");
                }
            }
            
            if (editorAssembly == null)
            {
                var candidates = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetName().Name)
                    .Where(n => n != null && n.IndexOf("VisualEffect", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();
                Debug.LogWarning($"✗ Assembly '{AssemblyName}' not found. Candidates: {string.Join(", ", candidates)}");
            }
        }
        
        private static void DiagnoseCoreTypes()
        {
            Debug.Log("\n--- Core Types ---");
            const string AssemblyName = "Unity.VisualEffectGraph.Editor";
            
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, AssemblyName, StringComparison.Ordinal));
            
            if (assembly == null)
            {
                Debug.LogWarning("✗ Assembly not found, skipping type diagnosis");
                return;
            }
            
            var typesToCheck = new[]
            {
                "UnityEditor.VFX.VisualEffectResource",
                "UnityEditor.VFX.UI.VFXViewController",
                "UnityEditor.VFX.VisualEffectResourceExtensions",
                "UnityEditor.VFX.VFXGraph",
                "UnityEditor.VFX.VFXLibrary"
            };
            
            foreach (var typeName in typesToCheck)
            {
                var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (type != null)
                {
                    Debug.Log($"✓ Type found: {typeName}");
                    LogTypeMembers(type, typeName);
                }
                else
                {
                    Debug.LogWarning($"✗ Type not found: {typeName}");
                }
            }
        }
        
        private static void LogTypeMembers(Type type, string typeName)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name.Contains("Resource") || m.Name.Contains("Controller") || m.Name.Contains("Get") || m.Name.Contains("Create"))
                .Take(10)
                .ToArray();
            
            if (methods.Length > 0)
            {
                Debug.Log($"  Methods (sample): {string.Join(", ", methods.Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"))}");
            }
            
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Take(10)
                .ToArray();
            
            if (properties.Length > 0)
            {
                Debug.Log($"  Properties (sample): {string.Join(", ", properties.Select(p => p.Name))}");
            }
        }
        
        private static void DiagnoseKnownGraph()
        {
            Debug.Log("\n--- Known Graph Asset Test ---");
            
            // Try to find a known VFX graph in the project
            var allVfxAssets = AssetDatabase.FindAssets("t:VisualEffectAsset");
            if (allVfxAssets.Length == 0)
            {
                Debug.LogWarning("✗ No VFX assets found in project");
                return;
            }
            
            var firstVfxGuid = allVfxAssets[0];
            var firstVfxPath = AssetDatabase.GUIDToAssetPath(firstVfxGuid);
            Debug.Log($"Testing with: {firstVfxPath}");
            
            // Try loading as Unity object
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(firstVfxPath);
            if (asset != null)
            {
                Debug.Log($"✓ Loaded as VisualEffectAsset: {asset.name}");
            }
            else
            {
                Debug.LogWarning($"✗ Failed to load as VisualEffectAsset");
            }
            
            // Try using reflection to get resource
            var resourceType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.VFX.VisualEffectResource", throwOnError: false, ignoreCase: false))
                .FirstOrDefault(t => t != null);
            
            if (resourceType != null)
            {
                Debug.Log($"✓ VisualEffectResource type found");
                
                // Try GetResourceAtPath
                var getResourceMethod = resourceType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "GetResourceAtPath" && m.GetParameters().Length == 1);
                
                if (getResourceMethod != null)
                {
                    Debug.Log($"✓ GetResourceAtPath method found: {getResourceMethod}");
                    
                    try
                    {
                        var resource = getResourceMethod.Invoke(null, new object[] { firstVfxPath });
                        if (resource != null)
                        {
                            Debug.Log($"✓ GetResourceAtPath succeeded, resource type: {resource.GetType().FullName}");
                            
                            // Try to get controller
                            DiagnoseControllerCreation(resource);
                        }
                        else
                        {
                            Debug.LogWarning($"✗ GetResourceAtPath returned null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"✗ GetResourceAtPath failed: {ex.Message}\n{ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.LogWarning($"✗ GetResourceAtPath method not found");
                    var allMethods = resourceType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                        .Where(m => m.Name.Contains("Resource") || m.Name.Contains("Get"))
                        .ToArray();
                    Debug.Log($"  Available methods: {string.Join(", ", allMethods.Select(m => m.Name))}");
                }
            }
            else
            {
                Debug.LogWarning($"✗ VisualEffectResource type not found");
            }
        }
        
        private static void DiagnoseControllerCreation(object resource)
        {
            Debug.Log("\n--- Controller Creation Test ---");
            
            var controllerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.VFX.UI.VFXViewController", throwOnError: false, ignoreCase: false))
                .FirstOrDefault(t => t != null);
            
            if (controllerType == null)
            {
                Debug.LogWarning("✗ VFXViewController type not found");
                return;
            }
            
            Debug.Log($"✓ VFXViewController type found");
            
            // Try GetController method
            var getControllerMethod = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "GetController" && m.GetParameters().Length == 2);
            
            if (getControllerMethod != null)
            {
                Debug.Log($"✓ GetController method found");
                
                try
                {
                    var controller = getControllerMethod.Invoke(null, new object[] { resource, false });
                    if (controller != null)
                    {
                        Debug.Log($"✓ GetController succeeded, controller type: {controller.GetType().FullName}");
                        
                        // Try to get graph model
                        DiagnoseGraphModel(controller);
                    }
                    else
                    {
                        Debug.LogWarning($"✗ GetController returned null");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"✗ GetController failed: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                Debug.LogWarning($"✗ GetController method not found");
                var allMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                    .Where(m => m.Name.Contains("Controller") || m.Name.Contains("Get"))
                    .ToArray();
                Debug.Log($"  Available methods: {string.Join(", ", allMethods.Select(m => m.Name))}");
            }
        }
        
        private static void DiagnoseGraphModel(object controller)
        {
            Debug.Log("\n--- Graph Model Test ---");
            
            var controllerType = controller.GetType();
            
            // Try to get graph property
            var graphProperty = controllerType.GetProperty("graph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (graphProperty != null)
            {
                try
                {
                    var graph = graphProperty.GetValue(controller);
                    if (graph != null)
                    {
                        Debug.Log($"✓ Controller.graph property found, type: {graph.GetType().FullName}");
                        
                        // Try to get nodes/contexts
                        var graphType = graph.GetType();
                        var nodesProperty = graphType.GetProperty("children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) 
                                         ?? graphType.GetProperty("nodes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                         ?? graphType.GetProperty("m_Children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        
                        if (nodesProperty != null)
                        {
                            var nodes = nodesProperty.GetValue(graph);
                            Debug.Log($"✓ Graph has nodes/children property: {nodesProperty.Name}, type: {nodes?.GetType().FullName ?? "null"}");
                        }
                        else
                        {
                            Debug.LogWarning($"✗ Graph nodes/children property not found");
                            var allProps = graphType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Take(10)
                                .ToArray();
                            Debug.Log($"  Available properties: {string.Join(", ", allProps.Select(p => p.Name))}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"✗ Controller.graph returned null");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"✗ Failed to get graph: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"✗ Controller.graph property not found");
                var allProps = controllerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Take(10)
                    .ToArray();
                Debug.Log($"  Available properties: {string.Join(", ", allProps.Select(p => p.Name))}");
            }
        }
        
        private static void DiagnoseResourceMethods()
        {
            Debug.Log("\n--- Resource Methods ---");
            
            var resourceType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.VFX.VisualEffectResource", throwOnError: false, ignoreCase: false))
                .FirstOrDefault(t => t != null);
            
            if (resourceType == null)
            {
                Debug.LogWarning("✗ VisualEffectResource type not found");
                return;
            }
            
            var allMethods = resourceType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
                .ToArray();
            
            Debug.Log($"All methods ({allMethods.Length}):");
            foreach (var method in allMethods.OrderBy(m => m.Name))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                var isStatic = method.IsStatic ? "static " : "";
                Debug.Log($"  {isStatic}{method.Name}({parameters})");
            }
        }
        
        private static void DiagnoseControllerMethods()
        {
            Debug.Log("\n--- Controller Methods ---");
            
            var controllerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.VFX.UI.VFXViewController", throwOnError: false, ignoreCase: false))
                .FirstOrDefault(t => t != null);
            
            if (controllerType == null)
            {
                Debug.LogWarning("✗ VFXViewController type not found");
                return;
            }
            
            var allMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name.Contains("Get") || m.Name.Contains("Add") || m.Name.Contains("Create") || m.Name.Contains("Sync"))
                .ToArray();
            
            Debug.Log($"Relevant methods ({allMethods.Length}):");
            foreach (var method in allMethods.OrderBy(m => m.Name))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                var isStatic = method.IsStatic ? "static " : "";
                Debug.Log($"  {isStatic}{method.Name}({parameters})");
            }
        }
    }
}

