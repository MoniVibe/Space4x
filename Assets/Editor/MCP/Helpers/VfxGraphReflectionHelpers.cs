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
    /// Utility helpers for working with Unity's Visual Effect Graph editor types via reflection.
    /// </summary>
    internal static class VfxGraphReflectionHelpers
    {
        private const string AssemblyName = "Unity.VisualEffectGraph.Editor";

        private static Assembly _editorAssembly;
        private static Type _visualEffectResourceType;
        private static Type _viewControllerType;
        private static Type _resourceExtensionsType;

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
                // Allow null for reference types or nullable value types
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

        private static MethodInfo GetMethodInfo(Type type, string methodName, BindingFlags flags, object[] parameters = null, Type[] parameterTypes = null)
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
                .Where(n => n != null && n.IndexOf("VisualEffect", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            Debug.Log($"[MCP Tools] Loaded assemblies containing 'VisualEffect': {string.Join(", ", loadedCandidates)}");

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
                Debug.LogWarning($"[MCP Tools] VFX editor assembly not loaded while resolving type '{fullName}'");
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
                Debug.LogWarning($"[MCP Tools] Type '{fullName}' not found in any loaded assembly. Assemblies present: {string.Join(", ", AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name))}");
            }

            return type;
        }

        private static Type VisualEffectResourceType => _visualEffectResourceType ??= GetEditorType("UnityEditor.VFX.VisualEffectResource");
        private static Type ViewControllerType => _viewControllerType ??= GetEditorType("UnityEditor.VFX.UI.VFXViewController");
        private static Type ResourceExtensionsType => _resourceExtensionsType ??= GetEditorType("UnityEditor.VFX.VisualEffectResourceExtensions");

        public static bool TryGetResource(string graphPath, out object resource, out string error)
        {
            Debug.Log($"[MCP Tools] TryGetResource invoked for path: {graphPath}");
            resource = null;
            error = null;

            if (string.IsNullOrEmpty(graphPath))
            {
                error = "graph_path is required";
                Debug.LogWarning("[MCP Tools] TryGetResource: graph_path is null or empty");
                return false;
            }

            // Normalize path
            graphPath = graphPath.Replace('\\', '/');
            if (!graphPath.StartsWith("Assets/"))
            {
                Debug.LogWarning($"[MCP Tools] TryGetResource: Path doesn't start with Assets/: {graphPath}");
            }

            // Check if file exists
            if (!File.Exists(graphPath))
            {
                error = $"Graph file not found at path: {graphPath}";
                Debug.LogWarning($"[MCP Tools] TryGetResource: File does not exist: {graphPath}");
                return false;
            }

            // Check if asset exists in AssetDatabase
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath);
            if (asset == null)
            {
                Debug.LogWarning($"[MCP Tools] TryGetResource: AssetDatabase.LoadAssetAtPath returned null for: {graphPath}");
                // Try refreshing
                AssetDatabase.Refresh();
                System.Threading.Thread.Sleep(100);
                asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath);
                if (asset == null)
                {
                    error = $"Asset not found in AssetDatabase at path: {graphPath}";
                    Debug.LogError($"[MCP Tools] TryGetResource: Asset still not found after refresh: {graphPath}");
                    return false;
                }
            }

            Debug.Log($"[MCP Tools] TryGetResource: Asset loaded successfully: {asset.name} (type: {asset.GetType().FullName})");

            if (VisualEffectResourceType == null)
            {
                Debug.LogWarning("[MCP Tools] VisualEffectResourceType resolve failed; VFX editor assembly is still unavailable.");
                error = "Unity Visual Effect Graph editor assembly not found";
                return false;
            }

            Debug.Log($"[MCP Tools] TryGetResource: VisualEffectResourceType resolved: {VisualEffectResourceType.FullName}");

            // Verify asset type - it should be a VisualEffectAsset
            var assetType = AssetDatabase.GetMainAssetTypeAtPath(graphPath);
            Debug.Log($"[MCP Tools] TryGetResource: Asset type from AssetDatabase: {assetType?.FullName ?? "null"}");

            // Try asset-based method first (more reliable for newly created assets)
            var getResourceFromAsset = GetMethodInfo(
                VisualEffectResourceType,
                "GetResource",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
                parameterTypes: new[] { typeof(UnityEngine.Object) });
            
            if (getResourceFromAsset != null)
            {
                try
                {
                    Debug.Log("[MCP Tools] TryGetResource: Trying GetResource with asset object");
                    resource = getResourceFromAsset.Invoke(null, new object[] { asset });
                    if (resource != null)
                    {
                        Debug.Log($"[MCP Tools] TryGetResource: Success via asset-based method! Resource type: {resource.GetType().FullName}");
                        return true;
                    }
                }
                catch (Exception assetEx)
                {
                    Debug.LogWarning($"[MCP Tools] TryGetResource: Asset-based method failed: {assetEx.Message}");
                }
            }

            // Fallback to path-based methods
            var method = GetMethodInfo(
                VisualEffectResourceType,
                "GetResourceAtPath",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
                parameterTypes: new[] { typeof(string) });
            
            if (method == null)
            {
                // Try alternative: GetResource with string
                method = GetMethodInfo(
                    VisualEffectResourceType,
                    "GetResource",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
                    parameterTypes: new[] { typeof(string) });
            }

            if (method == null)
            {
                var allMethods = VisualEffectResourceType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                    .Where(m => m.Name.Contains("Resource") || m.Name.Contains("Get"))
                    .Select(m =>
                    {
                        var parameters = m.GetParameters();
                        return $"{m.Name}({string.Join(", ", parameters.Select(p => p.ParameterType.Name))})";
                    })
                    .ToArray();
                Debug.LogWarning($"[MCP Tools] Unable to locate GetResourceAtPath/GetResource. Available methods: {string.Join("; ", allMethods)}");
                error = "Unable to locate VisualEffectResource.GetResourceAtPath or GetResource";
                return false;
            }

            Debug.Log($"[MCP Tools] TryGetResource: Found path-based method: {method.Name}");

            try
            {
                resource = method.Invoke(null, new object[] { graphPath });
                if (resource == null)
                {
                    error = $"GetResourceAtPath/GetResource returned null for path: {graphPath}";
                    Debug.LogWarning($"[MCP Tools] TryGetResource: Path-based method returned null");
                    return false;
                }
                
                Debug.Log($"[MCP Tools] TryGetResource: Success via path-based method! Resource type: {resource.GetType().FullName}");
                return true;
            }
            catch (Exception ex)
            {
                error = $"Exception invoking GetResourceAtPath/GetResource: {ex.Message}";
                Debug.LogError($"[MCP Tools] TryGetResource: Exception: {ex}\nStackTrace: {ex.StackTrace}");
                return false;
            }
        }

        public static bool TryGetViewController(object resource, bool forceUpdate, out object controller, out string error)
        {
            Debug.Log($"[MCP Tools] TryGetViewController invoked (forceUpdate: {forceUpdate})");
            controller = null;
            error = null;

            if (resource == null)
            {
                error = "VisualEffectResource is null";
                Debug.LogError("[MCP Tools] TryGetViewController: resource is null");
                return false;
            }

            Debug.Log($"[MCP Tools] TryGetViewController: resource type: {resource.GetType().FullName}");

            if (ViewControllerType == null)
            {
                error = "Unable to resolve UnityEditor.VFX.UI.VFXViewController";
                Debug.LogError("[MCP Tools] TryGetViewController: ViewControllerType is null");
                return false;
            }

            Debug.Log($"[MCP Tools] TryGetViewController: ViewControllerType resolved: {ViewControllerType.FullName}");

            // Try multiple method signatures
            var getController = GetMethodInfo(
                ViewControllerType,
                "GetController",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
                parameterTypes: new[] { VisualEffectResourceType, typeof(bool) });
            
            if (getController == null)
            {
                // Try with just resource parameter
                getController = GetMethodInfo(
                    ViewControllerType,
                    "GetController",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
                    parameterTypes: new[] { VisualEffectResourceType });
            }

            if (getController == null)
            {
                // Try alternative: maybe it's an instance method
                var instanceMethods = ViewControllerType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "GetController" || m.Name == "CreateController")
                    .ToArray();
                
                if (instanceMethods.Length > 0)
                {
                    Debug.LogWarning($"[MCP Tools] TryGetViewController: GetController is not static, found instance methods: {string.Join(", ", instanceMethods.Select(m => m.Name))}");
                }
                
                var allStaticMethods = ViewControllerType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                    .Where(m => m.Name.Contains("Controller") || m.Name.Contains("Get") || m.Name.Contains("Create"))
                    .Select(m =>
                    {
                        var parameters = m.GetParameters();
                        return $"{m.Name}({string.Join(", ", parameters.Select(p => p.ParameterType.Name))})";
                    })
                    .ToArray();
                Debug.LogWarning($"[MCP Tools] Unable to locate VFXViewController.GetController. Available static methods: {string.Join("; ", allStaticMethods)}");
                error = "Unable to locate VFXViewController.GetController";
                return false;
            }

            Debug.Log($"[MCP Tools] TryGetViewController: Found method: {getController.Name}, parameters: {getController.GetParameters().Length}");

            try
            {
                object[] invokeArgs;
                if (getController.GetParameters().Length == 2)
                {
                    invokeArgs = new object[] { resource, forceUpdate };
                }
                else
                {
                    invokeArgs = new object[] { resource };
                }
                
                Debug.Log($"[MCP Tools] TryGetViewController: Invoking with {invokeArgs.Length} arguments");
                controller = getController.Invoke(null, invokeArgs);
                
                if (controller == null)
                {
                    error = "VFXViewController.GetController returned null";
                    Debug.LogWarning("[MCP Tools] TryGetViewController: Method invocation returned null");
                    return false;
                }
                
                Debug.Log($"[MCP Tools] TryGetViewController: Success! Controller type: {controller.GetType().FullName}");
                return true;
            }
            catch (TargetInvocationException tie)
            {
                error = tie.InnerException?.Message ?? tie.Message;
                Debug.LogError($"[MCP Tools] TryGetViewController: TargetInvocationException: {error}\nInner: {tie.InnerException}\nStackTrace: {tie.StackTrace}");
                return false;
            }
            catch (Exception ex)
            {
                error = $"Exception invoking GetController: {ex.Message}";
                Debug.LogError($"[MCP Tools] TryGetViewController: Exception: {ex}\nStackTrace: {ex.StackTrace}");
                return false;
            }
        }

        public static IEnumerable Enumerate(object enumerable)
        {
            return enumerable as IEnumerable ?? Array.Empty<object>();
        }

        public static void WriteAssetWithSubAssets(object resource)
        {
            if (resource == null || ResourceExtensionsType == null)
            {
                return;
            }

            var method = GetMethodInfo(
                ResourceExtensionsType,
                "WriteAssetWithSubAssets",
                BindingFlags.Public | BindingFlags.Static,
                parameterTypes: new[] { VisualEffectResourceType });
            method?.Invoke(null, new[] { resource });
        }

        public static object GetGraph(object resource)
        {
            Debug.Log("[MCP Tools] GetGraph invoked");
            
            if (resource == null)
            {
                Debug.LogWarning("[MCP Tools] GetGraph: resource is null");
                return null;
            }

            if (ResourceExtensionsType == null)
            {
                Debug.LogWarning("[MCP Tools] GetGraph: ResourceExtensionsType is null");
                return null;
            }

            Debug.Log($"[MCP Tools] GetGraph: ResourceExtensionsType resolved: {ResourceExtensionsType.FullName}");

            var method = GetMethodInfo(
                ResourceExtensionsType,
                "GetOrCreateGraph",
                BindingFlags.Public | BindingFlags.Static,
                parameterTypes: new[] { VisualEffectResourceType });
            
            if (method == null)
            {
                Debug.LogWarning("[MCP Tools] GetGraph: GetOrCreateGraph method not found");
                return null;
            }

            Debug.Log($"[MCP Tools] GetGraph: Found method: {method.Name}");

            try
            {
                var graph = method.Invoke(null, new[] { resource });
                if (graph != null)
                {
                    Debug.Log($"[MCP Tools] GetGraph: Success! Graph type: {graph.GetType().FullName}");
                }
                else
                {
                    Debug.LogWarning("[MCP Tools] GetGraph: Method returned null");
                }
                return graph;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Tools] GetGraph: Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return null;
            }
        }

        public static PropertyInfo GetPropertyInfo(Type type, string propertyName, BindingFlags flags)
        {
            if (type == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            return type
                .GetProperties(flags)
                .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.Ordinal));
        }

        public static object GetProperty(object instance, string propertyName, bool includeNonPublic = false)
        {
            if (instance == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public;
            if (includeNonPublic)
            {
                flags |= BindingFlags.NonPublic;
            }

            var prop = GetPropertyInfo(instance.GetType(), propertyName, flags);
            return prop?.GetValue(instance);
        }

        public static object InvokeInstanceMethod(object instance, string methodName, params object[] parameters)
        {
            if (instance == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var method = GetMethodInfo(
                instance.GetType(),
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                parameters: parameters);
            return method?.Invoke(instance, parameters);
        }

        public static IEnumerable<(object descriptor, object variant)> EnumerateVariants(IEnumerable descriptors)
        {
            if (descriptors == null)
            {
                yield break;
            }

            foreach (var descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    continue;
                }

                var variant = descriptor.GetType().GetProperty("variant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(descriptor);
                if (variant != null)
                {
                    yield return (descriptor, variant);
                }

                var subVariants = descriptor.GetType().GetProperty("subVariantDescriptors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(descriptor) as IEnumerable;
                if (subVariants != null)
                {
                    foreach (var tuple in EnumerateVariants(subVariants))
                    {
                        yield return tuple;
                    }
                }
            }
        }

        public static IEnumerable<object> GetLibraryDescriptors(string methodName)
        {
            var libraryType = GetEditorType("UnityEditor.VFX.VFXLibrary");
            if (libraryType == null)
            {
                return Array.Empty<object>();
            }

            var method = GetMethodInfo(
                libraryType,
                methodName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
                parameterTypes: Array.Empty<Type>());
            if (method == null)
            {
                return Array.Empty<object>();
            }

            if (method.Invoke(null, null) is IEnumerable enumerable)
            {
                return enumerable.Cast<object>().ToArray();
            }

            return Array.Empty<object>();
        }

        /// <summary>
        /// Safely saves the asset if it's dirty, then writes with sub-assets.
        /// </summary>
        public static void SaveAssetIfDirty(object resource)
        {
            if (resource == null)
            {
                return;
            }

            var unityObj = resource as UnityEngine.Object;
            if (unityObj != null && EditorUtility.IsDirty(unityObj))
            {
                EditorUtility.SetDirty(unityObj);
                WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Gets property metadata including type, readable, writable status.
        /// </summary>
        public static Dictionary<string, object> GetPropertyMetadata(object instance, string propertyName, bool includeNonPublic = false)
        {
            var metadata = new Dictionary<string, object>();
            if (instance == null || string.IsNullOrEmpty(propertyName))
            {
                return metadata;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public;
            if (includeNonPublic)
            {
                flags |= BindingFlags.NonPublic;
            }

            var prop = GetPropertyInfo(instance.GetType(), propertyName, flags);
            if (prop != null)
            {
                metadata["exists"] = true;
                metadata["type"] = prop.PropertyType.FullName;
                metadata["canRead"] = prop.CanRead;
                metadata["canWrite"] = prop.CanWrite;
                metadata["isPublic"] = prop.GetGetMethod(includeNonPublic)?.IsPublic ?? false;
            }
            else
            {
                metadata["exists"] = false;
            }

            return metadata;
        }

        /// <summary>
        /// Ensures controller is synced and changes are applied before saving.
        /// </summary>
        public static void SyncAndSave(object controller, object resource)
        {
            if (controller == null || resource == null)
            {
                return;
            }

            InvokeInstanceMethod(controller, "LightApplyChanges");
            var syncMethod = GetMethodInfo(
                controller.GetType(),
                "SyncControllerFromModel",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                parameters: new object[] { false });
            if (syncMethod != null)
            {
                var syncArgs = new object[] { false };
                syncMethod.Invoke(controller, syncArgs);
            }

            var unityObj = resource as UnityEngine.Object;
            if (unityObj != null)
            {
                EditorUtility.SetDirty(unityObj);
            }

            WriteAssetWithSubAssets(resource);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Creates a new VFX graph asset at the specified path.
        /// </summary>
        public static bool TryCreateGraph(string graphPath, out object resource, out string error)
        {
            resource = null;
            error = null;

            if (string.IsNullOrEmpty(graphPath))
            {
                error = "graph_path is required";
                return false;
            }

            if (!graphPath.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase))
            {
                error = "graph_path must end with .vfx";
                return false;
            }

            if (VisualEffectResourceType == null)
            {
                error = "Unity Visual Effect Graph editor assembly not found";
                return false;
            }

            // Check if asset already exists
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
                // Try using VisualEffectResource.CreateAsset first (the proper Unity API)
                var createMethod = GetMethodInfo(
                    VisualEffectResourceType,
                    "CreateAsset",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
                    parameterTypes: new[] { typeof(string) });
                if (createMethod != null)
                {
                    try
                    {
                        resource = createMethod.Invoke(null, new object[] { graphPath });
                        if (resource != null)
                        {
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            return true;
                        }
                    }
                    catch (Exception createEx)
                    {
                        Debug.LogWarning($"[MCP Tools] CreateAsset method failed: {createEx.Message}");
                    }
                }

                // Fallback: duplicate a known-good template graph if available (preferred over raw YAML write)
                var templateCandidates = new[]
                {
                    "Packages/com.unity.visualeffectgraph/Editor/DefaultResources/New VFX.vfx",
                    "Packages/com.unity.visualeffectgraph/Editor/DefaultResources/New VFX Graph.vfx",
                    "Assets/Samples/Visual Effect Graph/17.2.0/Learning Templates/VFX/New VFX.vfx",
                    "Assets/Samples/Visual Effect Graph/17.2.0/VisualEffectGraph Additions/VFX/Bonfire.vfx"
                };

                foreach (var templatePath in templateCandidates)
                {
                    if (string.IsNullOrEmpty(templatePath))
                    {
                        continue;
                    }

                    var templateAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(templatePath);
                    if (templateAsset == null)
                    {
                        Debug.Log($"[MCP Tools] Template not found at path '{templatePath}'");
                        continue;
                    }

                    Debug.Log($"[MCP Tools] Using template '{templatePath}' to seed new VFX graph.");

                    try
                    {
                        if (AssetDatabase.CopyAsset(templatePath, graphPath))
                        {
                            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                            AssetDatabase.Refresh();

                            if (TryGetResource(graphPath, out resource, out error))
                            {
                                return true;
                            }

                            Debug.LogWarning($"[MCP Tools] Copied template '{templatePath}' but resource lookup failed: {error}");
                        }
                    }
                    catch (Exception copyEx)
                    {
                        Debug.LogWarning($"[MCP Tools] Failed to copy template '{templatePath}' to '{graphPath}': {copyEx.Message}");
                    }
                }

                // Final fallback: Create a minimal valid VFX graph file structure manually.
                // This path is retained for compatibility but may require subsequent fixes on newer Unity versions.
                var fileId = System.DateTime.UtcNow.Ticks.GetHashCode() & 0x7FFFFFFF;
                var minimalVfxContent = $@"%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: d01270efd3285ea4a9d6c555cb0a8027, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  groupInfos: []
  stickyNoteInfos: []
";

                File.WriteAllText(graphPath, minimalVfxContent);
                
                // Import the asset so Unity recognizes it
                AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();
                
                // Verify the asset was imported correctly
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(graphPath);
                if (assetType == null)
                {
                    Debug.LogWarning($"[MCP Tools] TryCreateGraph: Asset type is null after import, retrying...");
                    AssetDatabase.Refresh();
                    System.Threading.Thread.Sleep(200);
                    assetType = AssetDatabase.GetMainAssetTypeAtPath(graphPath);
                }
                
                Debug.Log($"[MCP Tools] TryCreateGraph: Asset type after import: {assetType?.FullName ?? "null"}");

                // Retry logic with exponential backoff
                const int maxRetries = 5;
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    if (TryGetResource(graphPath, out resource, out error))
                    {
                        Debug.Log($"[MCP Tools] TryCreateGraph: Successfully got resource on retry {retry + 1}");
                        return true;
                    }
                    
                    if (retry < maxRetries - 1)
                    {
                        var waitTime = 100 * (retry + 1); // Exponential backoff: 100ms, 200ms, 300ms, 400ms
                        Debug.Log($"[MCP Tools] TryCreateGraph: Retry {retry + 1}/{maxRetries} failed, waiting {waitTime}ms...");
                        System.Threading.Thread.Sleep(waitTime);
                        AssetDatabase.Refresh();
                    }
                }
                
                Debug.LogError($"[MCP Tools] TryCreateGraph: Failed to get resource after {maxRetries} retries. Last error: {error}");
                return false;
            }
            catch (Exception ex)
            {
                error = $"Exception creating graph: {ex.Message}";
                Debug.LogError($"[MCP Tools] Failed to create VFX graph: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Duplicates an existing VFX graph asset.
        /// </summary>
        public static bool TryDuplicateGraph(string sourcePath, string destinationPath, out object resource, out string error)
        {
            resource = null;
            error = null;

            if (!TryGetResource(sourcePath, out var sourceResource, out error))
            {
                return false;
            }

            if (File.Exists(destinationPath))
            {
                error = $"Destination path already exists: {destinationPath}";
                return false;
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                // For VFX files, use AssetDatabase.CopyAsset instead of CreateAsset
                // This avoids the warning about CreateAsset not being appropriate for .vfx files
                if (sourcePath.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase) || 
                    destinationPath.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase))
                {
                    if (AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    {
                        AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                        AssetDatabase.Refresh();

                        if (TryGetResource(destinationPath, out resource, out error))
                        {
                            return true;
                        }

                        error = $"Copied VFX graph but resource lookup failed: {error}";
                        return false;
                    }
                    else
                    {
                        error = "AssetDatabase.CopyAsset failed";
                        return false;
                    }
                }

                // For other asset types, use the original Instantiate + CreateAsset approach
                var sourceObj = sourceResource as UnityEngine.Object;
                if (sourceObj == null)
                {
                    error = "Source resource is not a Unity Object";
                    return false;
                }

                var duplicate = UnityEngine.Object.Instantiate(sourceObj);
                AssetDatabase.CreateAsset(duplicate, destinationPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return TryGetResource(destinationPath, out resource, out error);
            }
            catch (Exception ex)
            {
                error = $"Exception duplicating graph: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Sets the visual effect graph on a VisualEffect component.
        /// </summary>
        public static bool TrySetVisualEffectGraph(string gameObjectName, string graphPath, out string error)
        {
            error = null;

            var go = GameObject.Find(gameObjectName);
            if (go == null)
            {
                error = $"GameObject '{gameObjectName}' not found";
                return false;
            }

            var visualEffect = go.GetComponent<UnityEngine.VFX.VisualEffect>();
            if (visualEffect == null)
            {
                error = $"GameObject '{gameObjectName}' does not have a VisualEffect component";
                return false;
            }

            var graphAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(graphPath);
            if (graphAsset == null)
            {
                error = $"VFX graph asset not found at path: {graphPath}";
                return false;
            }

            visualEffect.visualEffectAsset = graphAsset;
            EditorUtility.SetDirty(visualEffect);
            return true;
        }

        /// <summary>
        /// Lists all VFX graph instances (VisualEffect components) in the active scene.
        /// </summary>
        public static List<Dictionary<string, object>> ListGraphInstances()
        {
            var instances = new List<Dictionary<string, object>>();

            var visualEffects = UnityEngine.Object.FindObjectsByType<UnityEngine.VFX.VisualEffect>(FindObjectsSortMode.None);
            foreach (var vfx in visualEffects)
            {
                var graphPath = vfx.visualEffectAsset != null ? AssetDatabase.GetAssetPath(vfx.visualEffectAsset) : null;
                instances.Add(new Dictionary<string, object>
                {
                    ["gameObjectName"] = vfx.gameObject.name,
                    ["gameObjectPath"] = GetGameObjectPath(vfx.gameObject),
                    ["graphPath"] = graphPath,
                    ["graphName"] = vfx.visualEffectAsset?.name
                });
            }

            return instances;
        }

        private static string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        /// <summary>
        /// Sets the position property on a VFX graph model (node, context, etc.).
        /// </summary>
        public static void SetModelPosition(object model, Vector2 position)
        {
            if (model == null)
            {
                return;
            }

            var modelType = model.GetType();
            var positionProperty = modelType.GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (positionProperty != null && positionProperty.CanWrite)
            {
                positionProperty.SetValue(model, position);
            }
        }

        /// <summary>
        /// Safely looks up a property value with fallback options.
        /// </summary>
        public static object SafePropertyLookup(object instance, string primaryPropertyName, params string[] fallbackPropertyNames)
        {
            if (instance == null || string.IsNullOrEmpty(primaryPropertyName))
            {
                return null;
            }

            var value = GetProperty(instance, primaryPropertyName, includeNonPublic: true);
            if (value != null)
            {
                return value;
            }

            foreach (var fallbackName in fallbackPropertyNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(fallbackName))
                {
                    continue;
                }

                value = GetProperty(instance, fallbackName, includeNonPublic: true);
                if (value != null)
                {
                    Debug.Log($"[MCP Tools] Property '{primaryPropertyName}' not found, using fallback '{fallbackName}'");
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Safely sets a property value, trying multiple property names if needed.
        /// </summary>
        public static bool SafePropertySet(object instance, string primaryPropertyName, object value, params string[] fallbackPropertyNames)
        {
            if (instance == null || string.IsNullOrEmpty(primaryPropertyName))
            {
                return false;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();
            var prop = GetPropertyInfo(type, primaryPropertyName, flags);
            
            if (prop != null && prop.CanWrite)
            {
                try
                {
                    prop.SetValue(instance, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] Failed to set property '{primaryPropertyName}': {ex.Message}");
                }
            }

            foreach (var fallbackName in fallbackPropertyNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(fallbackName))
                {
                    continue;
                }

                prop = GetPropertyInfo(type, fallbackName, flags);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        prop.SetValue(instance, value);
                        Debug.Log($"[MCP Tools] Property '{primaryPropertyName}' not writable, used fallback '{fallbackName}'");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MCP Tools] Failed to set fallback property '{fallbackName}': {ex.Message}");
                    }
                }
            }

            return false;
        }
    }
}


