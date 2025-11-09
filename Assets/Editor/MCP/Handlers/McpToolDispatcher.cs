using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Dispatcher that routes MCP tool commands to their C# handlers.
    /// This is called by the MCP For Unity bridge when a Python tool sends a command.
    /// </summary>
    public static class McpToolDispatcher
    {
        private static Dictionary<string, MethodInfo> _handlerCache;
        
        static McpToolDispatcher()
        {
            BuildHandlerCache();
        }
        
        /// <summary>
        /// Call a custom MCP tool by name.
        /// </summary>
        public static object CallTool(string toolName, JObject parameters)
        {
            if (_handlerCache == null)
            {
                BuildHandlerCache();
            }
            
            if (!_handlerCache.TryGetValue(toolName, out MethodInfo handler))
            {
                return Response.Error($"Tool '{toolName}' not found. Available tools: {string.Join(", ", _handlerCache.Keys)}");
            }
            
            try
            {
                return handler.Invoke(null, new object[] { parameters });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to execute tool '{toolName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Get list of all registered tool names.
        /// </summary>
        public static List<string> GetRegisteredTools()
        {
            if (_handlerCache == null)
            {
                BuildHandlerCache();
            }
            return _handlerCache.Keys.ToList();
        }
        
        private static void BuildHandlerCache()
        {
            _handlerCache = new Dictionary<string, MethodInfo>();
            
            // Find all classes with [McpForUnityTool] attribute
            var attributeType = McpReflectionHelpers.GetMcpType("MCPForUnity.Editor.Tools.McpForUnityToolAttribute")
                                 ?? typeof(MCPForUnity.Editor.Tools.McpForUnityToolAttribute);
            
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.IsClass && t.IsAbstract && t.IsSealed) // static classes
                        .Where(t => t.GetCustomAttribute(attributeType) != null);
                    
                    foreach (var type in types)
                    {
                        var attribute = type.GetCustomAttribute(attributeType);
                        var handleMethod = type.GetMethod("HandleCommand", BindingFlags.Public | BindingFlags.Static);
                        
                        if (handleMethod != null && attribute != null)
                        {
                            var toolNameProp = attributeType.GetProperty("ToolName");
                            if (toolNameProp != null)
                            {
                                string toolName = toolNameProp.GetValue(attribute)?.ToString();
                                if (!string.IsNullOrEmpty(toolName))
                                {
                                    _handlerCache[toolName] = handleMethod;
                                }
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be loaded
                    continue;
                }
            }
        }
    }
}

