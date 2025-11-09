using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("export_graph_snapshot")]
    public static class ExportGraphSnapshotTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var snapshotPath = @params["snapshot_path"]?.ToString();

                if (string.IsNullOrWhiteSpace(graphPath))
                {
                    return Response.Error("graph_path is required");
                }

                if (string.IsNullOrWhiteSpace(snapshotPath))
                {
                    snapshotPath = graphPath.Replace(".vfx", "_snapshot.json");
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out error))
                {
                    return Response.Error(error);
                }

                // Get graph structure
                var structure = GetGraphStructure(controller);

                // Serialize to JSON
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(structure, Newtonsoft.Json.Formatting.Indented);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(snapshotPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(snapshotPath, json);

                return Response.Success($"Graph snapshot exported to {snapshotPath}", new
                {
                    graphPath,
                    snapshotPath,
                    snapshotSize = json.Length
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to export graph snapshot: {ex.Message}");
            }
        }

        private static Dictionary<string, object> GetGraphStructure(object controller)
        {
            var structure = new Dictionary<string, object>();
            var nodes = new List<Dictionary<string, object>>();
            var connections = new List<Dictionary<string, object>>();

            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model != null)
                {
                    var nodeType = model.GetType();
                    var position = VfxGraphReflectionHelpers.GetProperty(model, "position");
                    var name = model.name;

                    nodes.Add(new Dictionary<string, object>
                    {
                        ["id"] = model.GetInstanceID(),
                        ["name"] = name,
                        ["type"] = nodeType.FullName,
                        ["position"] = position is Vector2 pos ? new { x = pos.x, y = pos.y } : null
                    });
                }
            }

            var dataEdgesProperty = controller.GetType().GetProperty("dataEdges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (dataEdgesProperty != null)
            {
                var dataEdges = dataEdgesProperty.GetValue(controller);
                if (dataEdges is IEnumerable edgesEnumerable)
                {
                    foreach (var edge in VfxGraphReflectionHelpers.Enumerate(edgesEnumerable))
                    {
                        if (edge == null) continue;

                        var edgeType = edge.GetType();
                        var inputProperty = edgeType.GetProperty("input", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var outputProperty = edgeType.GetProperty("output", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (inputProperty == null || outputProperty == null) continue;

                        var inputAnchor = inputProperty.GetValue(edge);
                        var outputAnchor = outputProperty.GetValue(edge);

                        var sourceNodeController = GetNodeControllerFromAnchor(outputAnchor, controller);
                        var targetNodeController = GetNodeControllerFromAnchor(inputAnchor, controller);

                        if (sourceNodeController != null && targetNodeController != null)
                        {
                            var sourceModel = VfxGraphReflectionHelpers.GetProperty(sourceNodeController, "model") as UnityEngine.Object;
                            var targetModel = VfxGraphReflectionHelpers.GetProperty(targetNodeController, "model") as UnityEngine.Object;

                            if (sourceModel != null && targetModel != null)
                            {
                                connections.Add(new Dictionary<string, object>
                                {
                                    ["sourceNodeId"] = sourceModel.GetInstanceID(),
                                    ["targetNodeId"] = targetModel.GetInstanceID()
                                });
                            }
                        }
                    }
                }
            }

            structure["nodes"] = nodes;
            structure["connections"] = connections;
            structure["timestamp"] = DateTime.UtcNow.ToString("O");

            return structure;
        }

        private static object GetNodeControllerFromAnchor(object anchor, object controller)
        {
            try
            {
                var ownerProperty = anchor?.GetType().GetProperty("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (ownerProperty != null)
                {
                    return ownerProperty.GetValue(anchor);
                }

                var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
                foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
                {
                    var inputPorts = VfxGraphReflectionHelpers.GetProperty(nodeController, "inputPorts");
                    var outputPorts = VfxGraphReflectionHelpers.GetProperty(nodeController, "outputPorts");

                    foreach (var port in VfxGraphReflectionHelpers.Enumerate(inputPorts))
                    {
                        if (port == anchor) return nodeController;
                    }
                    foreach (var port in VfxGraphReflectionHelpers.Enumerate(outputPorts))
                    {
                        if (port == anchor) return nodeController;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}

