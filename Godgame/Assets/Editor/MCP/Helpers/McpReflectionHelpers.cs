using System;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Utility helpers for resolving MCP For Unity types via reflection.
    /// Handles assembly name differences between package versions.
    /// </summary>
    internal static class McpReflectionHelpers
    {
        private static readonly string[] AssemblyHints =
        {
            "MCPForUnity.Editor",
            "com.coplaydev.unity-mcp"
        };

        /// <summary>
        /// Attempts to resolve a type from the MCP For Unity assemblies.
        /// </summary>
        /// <param name="fullTypeName">Fully-qualified type name without assembly suffix.</param>
        /// <returns>The resolved <see cref="Type"/> if found; otherwise null.</returns>
        public static Type GetMcpType(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return null;
            }

            foreach (var assembly in AssemblyHints)
            {
                try
                {
                    var type = Type.GetType($"{fullTypeName}, {assembly}");
                    if (type != null)
                    {
                        return type;
                    }

                    var loadedAssembly = Assembly.Load(assembly);
                    type = loadedAssembly?.GetType(fullTypeName, throwOnError: false, ignoreCase: false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // Ignore load failures and continue.
                }
            }

            // Fallback: search loaded assemblies by name
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullTypeName, throwOnError: false, ignoreCase: false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // Ignore assemblies that cannot be inspected
                }
            }

            return null;
        }
    }
}


