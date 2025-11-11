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
    [McpForUnityTool("get_shader_graph_structure")]
    public static class GetShaderGraphStructureTool
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
                
                // Get graph data using helper
                if (!ShaderGraphReflectionHelpers.TryGetGraphData(graphPath, out object graphData, out string error))
                {
                    return Response.Error(error);
                }
                
                if (graphData == null)
                {
                    return Response.Error("Failed to retrieve graph data");
                }
                
                // Get nodes
                var nodes = ShaderGraphReflectionHelpers.GetProperty(graphData, "nodes") as IEnumerable;
                var nodeList = new List<object>();
                
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        if (node == null) continue;
                        
                        var nodeId = ShaderGraphReflectionHelpers.GetProperty(node, "guid");
                        var nodeName = ShaderGraphReflectionHelpers.GetProperty(node, "name") ?? 
                                     ShaderGraphReflectionHelpers.GetProperty(node, "title");
                        var nodeType = node.GetType().Name;
                        
                        // Get position
                        var position = ShaderGraphReflectionHelpers.GetProperty(node, "drawState");
                        Vector2 nodePos = Vector2.zero;
                        if (position != null)
                        {
                            var pos = ShaderGraphReflectionHelpers.GetProperty(position, "position");
                            if (pos != null)
                            {
                                var posType = pos.GetType();
                                var xProp = posType.GetProperty("x");
                                var yProp = posType.GetProperty("y");
                                if (xProp != null && yProp != null)
                                {
                                    nodePos = new Vector2(
                                        Convert.ToSingle(xProp.GetValue(pos)),
                                        Convert.ToSingle(yProp.GetValue(pos))
                                    );
                                }
                            }
                        }
                        
                        nodeList.Add(new
                        {
                            id = nodeId?.ToString() ?? "unknown",
                            name = nodeName?.ToString() ?? "Unknown",
                            type = nodeType,
                            position = new { x = nodePos.x, y = nodePos.y }
                        });
                    }
                }
                
                // Get edges/connections
                var edges = ShaderGraphReflectionHelpers.GetProperty(graphData, "edges") as IEnumerable;
                var edgeList = new List<object>();
                
                if (edges != null)
                {
                    foreach (var edge in edges)
                    {
                        if (edge == null) continue;
                        
                        var outputNode = ShaderGraphReflectionHelpers.GetProperty(edge, "outputSlot");
                        var inputNode = ShaderGraphReflectionHelpers.GetProperty(edge, "inputSlot");
                        
                        var outputNodeId = outputNode != null ? ShaderGraphReflectionHelpers.GetProperty(outputNode, "node") : null;
                        var inputNodeId = inputNode != null ? ShaderGraphReflectionHelpers.GetProperty(inputNode, "node") : null;
                        
                        edgeList.Add(new
                        {
                            outputNodeId = outputNodeId?.ToString() ?? "unknown",
                            inputNodeId = inputNodeId?.ToString() ?? "unknown",
                            outputSlot = ShaderGraphReflectionHelpers.GetProperty(outputNode, "slotId")?.ToString(),
                            inputSlot = ShaderGraphReflectionHelpers.GetProperty(inputNode, "slotId")?.ToString()
                        });
                    }
                }
                
                // Get properties (blackboard)
                var properties = ShaderGraphReflectionHelpers.GetProperty(graphData, "properties") as IEnumerable;
                var propertyList = new List<object>();
                
                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        if (prop == null) continue;
                        
                        var propName = ShaderGraphReflectionHelpers.GetProperty(prop, "displayName") ?? 
                                      ShaderGraphReflectionHelpers.GetProperty(prop, "referenceName");
                        var propType = prop.GetType().Name;
                        
                        propertyList.Add(new
                        {
                            name = propName?.ToString() ?? "Unknown",
                            type = propType
                        });
                    }
                }
                
                return Response.Success($"Graph structure retrieved for {graphPath}", new
                {
                    graphPath = graphPath,
                    nodeCount = nodeList.Count,
                    nodes = nodeList,
                    edgeCount = edgeList.Count,
                    edges = edgeList,
                    propertyCount = propertyList.Count,
                    properties = propertyList
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to get shader graph structure: {ex.Message}");
            }
        }
    }
}
#endif

