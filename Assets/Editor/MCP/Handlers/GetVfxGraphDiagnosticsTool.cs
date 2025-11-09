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
    [McpForUnityTool("get_vfx_graph_diagnostics")]
    public static class GetVfxGraphDiagnosticsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                Debug.Log("[MCP Tools] Diagnostics: resource acquired");

                var graph = VfxGraphReflectionHelpers.GetGraph(resource);
                if (graph == null)
                {
                    return Response.Error("Unable to access graph model");
                }

                Debug.Log("[MCP Tools] Diagnostics: graph model retrieved");

                var diagnostics = new List<Dictionary<string, object>>();

                // Try to get compilation errors/warnings
                var graphType = graph.GetType();
                var errorsProperty = VfxGraphReflectionHelpers.GetPropertyInfo(
                    graphType,
                    "errors",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var warningsProperty = VfxGraphReflectionHelpers.GetPropertyInfo(
                    graphType,
                    "warnings",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (errorsProperty != null)
                {
                    Debug.Log("[MCP Tools] Diagnostics: processing errors");
                    var errors = errorsProperty.GetValue(graph);
                    if (errors is IEnumerable errorsEnumerable)
                    {
                        foreach (var errorItem in VfxGraphReflectionHelpers.Enumerate(errorsEnumerable))
                        {
                            if (errorItem == null) continue;
                            var message = VfxGraphReflectionHelpers.GetProperty(errorItem, "message") as string;
                            var line = VfxGraphReflectionHelpers.GetProperty(errorItem, "line");
                            diagnostics.Add(new Dictionary<string, object>
                            {
                                ["type"] = "error",
                                ["message"] = message ?? "Unknown error",
                                ["line"] = line
                            });
                        }
                    }
                }

                if (warningsProperty != null)
                {
                    Debug.Log("[MCP Tools] Diagnostics: processing warnings");
                    var warnings = warningsProperty.GetValue(graph);
                    if (warnings is IEnumerable warningsEnumerable)
                    {
                        foreach (var warning in VfxGraphReflectionHelpers.Enumerate(warningsEnumerable))
                        {
                            if (warning == null) continue;
                            var message = VfxGraphReflectionHelpers.GetProperty(warning, "message") as string;
                            var line = VfxGraphReflectionHelpers.GetProperty(warning, "line");
                            diagnostics.Add(new Dictionary<string, object>
                            {
                                ["type"] = "warning",
                                ["message"] = message ?? "Unknown warning",
                                ["line"] = line
                            });
                        }
                    }
                }

                // Try to get graph statistics
                var nodeCount = 0;
                var contextCount = 0;
                var connectionCount = 0;

                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, false, out var controller, out _))
                {
                    // Continue without controller if it fails
                }
                else
                {
                    Debug.Log("[MCP Tools] Diagnostics: controller acquired");

                    var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
                    if (nodesEnumerable != null)
                    {
                        foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
                        {
                            nodeCount++;
                            var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                            if (model != null)
                            {
                                var modelType = model.GetType();
                                var vfxContextType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContext");
                                if (vfxContextType != null && vfxContextType.IsAssignableFrom(modelType))
                                {
                                    contextCount++;
                                }
                            }
                        }
                    }

                    var dataEdgesProperty = VfxGraphReflectionHelpers.GetPropertyInfo(
                        controller.GetType(),
                        "dataEdges",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (dataEdgesProperty != null)
                    {
                        var dataEdges = dataEdgesProperty.GetValue(controller);
                        if (dataEdges is IEnumerable edgesEnumerable)
                        {
                            connectionCount = VfxGraphReflectionHelpers.Enumerate(edgesEnumerable).Cast<object>().Count();
                        }
                    }
                }

                return Response.Success($"Retrieved diagnostics for graph {graphPath}", new
                {
                    graphPath,
                    errorCount = diagnostics.Count(d => d["type"] as string == "error"),
                    warningCount = diagnostics.Count(d => d["type"] as string == "warning"),
                    diagnostics,
                    statistics = new Dictionary<string, object>
                    {
                        ["nodeCount"] = nodeCount,
                        ["contextCount"] = contextCount,
                        ["connectionCount"] = connectionCount
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Tools] get_vfx_graph_diagnostics failed: {ex}");
                return Response.Error($"Failed to get VFX graph diagnostics: {ex.Message} | {ex.StackTrace}");
            }
        }
    }
}

