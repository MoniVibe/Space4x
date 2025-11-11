#if false
// Shader Graph tools disabled - Shader Graph package not installed
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("get_shader_graph_diagnostics")]
    public static class GetShaderGraphDiagnosticsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string graphPath = @params["graph_path"]?.ToString();
                
                if (string.IsNullOrEmpty(graphPath))
                {
                    return Response.Error("graph_path is required");
                }
                
                // Get graph data
                if (!ShaderGraphReflectionHelpers.TryGetGraphData(graphPath, out object graphData, out string error))
                {
                    return Response.Error(error);
                }
                
                // Get compilation errors/warnings
                var errors = new List<string>();
                var warnings = new List<string>();
                
                // Try to get validation messages
                var validationMethod = ShaderGraphReflectionHelpers.GetMethodInfo(
                    graphData.GetType(),
                    "ValidateGraph",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                
                if (validationMethod != null)
                {
                    try
                    {
                        var validationResult = validationMethod.Invoke(graphData, null);
                        if (validationResult is IEnumerable validationMessages)
                        {
                            foreach (var msg in validationMessages)
                            {
                                if (msg == null) continue;
                                
                                var msgType = ShaderGraphReflectionHelpers.GetProperty(msg, "severity");
                                var msgText = ShaderGraphReflectionHelpers.GetProperty(msg, "message")?.ToString() ?? 
                                            ShaderGraphReflectionHelpers.GetProperty(msg, "description")?.ToString();
                                
                                if (!string.IsNullOrEmpty(msgText))
                                {
                                    var severity = msgType?.ToString() ?? "Info";
                                    if (severity.Contains("Error", StringComparison.OrdinalIgnoreCase))
                                    {
                                        errors.Add(msgText);
                                    }
                                    else if (severity.Contains("Warning", StringComparison.OrdinalIgnoreCase))
                                    {
                                        warnings.Add(msgText);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Validation check failed: {ex.Message}");
                    }
                }
                
                // Get graph statistics
                var nodes = ShaderGraphReflectionHelpers.GetProperty(graphData, "nodes") as IEnumerable;
                var edges = ShaderGraphReflectionHelpers.GetProperty(graphData, "edges") as IEnumerable;
                var properties = ShaderGraphReflectionHelpers.GetProperty(graphData, "properties") as IEnumerable;
                
                int nodeCount = 0;
                int edgeCount = 0;
                int propertyCount = 0;
                
                if (nodes != null)
                {
                    foreach (var _ in nodes) nodeCount++;
                }
                
                if (edges != null)
                {
                    foreach (var _ in edges) edgeCount++;
                }
                
                if (properties != null)
                {
                    foreach (var _ in properties) propertyCount++;
                }
                
                return Response.Success($"Diagnostics retrieved for {graphPath}", new
                {
                    graphPath = graphPath,
                    errorCount = errors.Count,
                    warningCount = warnings.Count,
                    errors = errors,
                    warnings = warnings,
                    statistics = new
                    {
                        nodeCount = nodeCount,
                        edgeCount = edgeCount,
                        propertyCount = propertyCount
                    }
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to get shader graph diagnostics: {ex.Message}");
            }
        }
    }
}
#endif

