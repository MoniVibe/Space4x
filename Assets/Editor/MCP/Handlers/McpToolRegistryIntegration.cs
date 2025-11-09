using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Registers custom MCP tools with Unity's CommandRegistry so they can be called via MCP.
    /// This runs on Editor initialization to register all [McpForUnityTool] handlers.
    /// </summary>
    [InitializeOnLoad]
    public static class McpToolRegistryIntegration
    {
        static McpToolRegistryIntegration()
        {
            // Reduced verbosity - only log on first initialization
            // Debug.Log("[MCP Tools] McpToolRegistryIntegration static ctor");
            // Delay registration until Unity finishes loading assemblies so MCPForUnity types are available.
            EditorApplication.delayCall += RegisterCustomTools;
        }
        
        private static void RegisterCustomTools()
        {
            // According to CUSTOM_TOOLS.md, tools are auto-discovered by CommandRegistry.AutoDiscoverCommands()
            // which runs during CommandRegistry.Initialize(). We don't need to manually register them.
            // Just ensure initialization happens, and our handlers with [McpForUnityTool] attributes
            // will be automatically discovered.
            
            try
            {
                // Reduced verbosity - only log summary
                // Debug.Log("[MCP Tools] RegisterCustomTools called - tools should be auto-discovered by CommandRegistry");
                
                // Get the CommandRegistry type from MCP For Unity
                var registryType = McpReflectionHelpers.GetMcpType("MCPForUnity.Editor.Tools.CommandRegistry");
                if (registryType == null)
                {
                    Debug.LogWarning("[MCP Tools] CommandRegistry type not found. Custom tools may not be auto-discovered.");
                    return;
                }
                
                // Debug.Log($"[MCP Tools] CommandRegistry type found: {registryType.FullName}");
                
                // Ensure CommandRegistry is initialized (this triggers auto-discovery)
                var initializeMethod = registryType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (initializeMethod != null)
                {
                    try
                    {
                        initializeMethod.Invoke(null, null);
                        // Debug.Log("[MCP Tools] Called CommandRegistry.Initialize() - auto-discovery should have run");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MCP Tools] CommandRegistry.Initialize() call failed (possibly already initialized): {ex.Message}");
                    }
                }
                
                // Log discovered handlers for verification (summary only)
                var handlerTypes = DiscoverCustomHandlerTypes();
                Debug.Log($"[MCP Tools] Registered {handlerTypes.Count} custom MCP tool handlers");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Tools] Failed during initialization check: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private static List<Type> DiscoverCustomHandlerTypes()
        {
            var handlerTypes = new List<Type>();
            // Reduced verbosity - only log summary
            // Debug.Log("[MCP Tools] DiscoverCustomHandlerTypes scanning assemblies");
            
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.IsClass && t.IsAbstract && t.IsSealed) // static classes
                        .Where(t => t.Namespace == "PureDOTS.Editor.MCP");
                    
                    foreach (var type in types)
                    {
                        // Use reflection to get the attribute from MCP For Unity package
                        var attributeType = McpReflectionHelpers.GetMcpType("MCPForUnity.Editor.Tools.McpForUnityToolAttribute")
                                            ?? typeof(MCPForUnity.Editor.Tools.McpForUnityToolAttribute);
                        if (attributeType == null) continue;
                        var attribute = type.GetCustomAttribute(attributeType);
                        if (attribute == null) continue;
                        
                        // Verify it has HandleCommand method
                        var handleMethod = type.GetMethod("HandleCommand", 
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new[] { typeof(JObject) },
                            null);
                        
                        if (handleMethod != null)
                        {
                            handlerTypes.Add(type);
                            // Only log in verbose mode to reduce console spam
                            // Debug.Log($"[MCP Tools] Discovered handler type: {type.FullName}");
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }
            }
            
            Debug.Log($"[MCP Tools] DiscoverCustomHandlerTypes total: {handlerTypes.Count}");
            
            return handlerTypes;
        }
    }
}

