#if false
// Shader Graph tools disabled - Shader Graph package not installed
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("connect_shader_graph_nodes")]
    public static class ConnectShaderGraphNodesTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string graphPath = @params["graph_path"]?.ToString();
                string sourceNodeId = @params["source_node_id"]?.ToString();
                string sourceSlot = @params["source_slot"]?.ToString();
                string targetNodeId = @params["target_node_id"]?.ToString();
                string targetSlot = @params["target_slot"]?.ToString();
                
                if (string.IsNullOrEmpty(graphPath))
                {
                    return Response.Error("graph_path is required");
                }
                
                if (string.IsNullOrEmpty(sourceNodeId) || string.IsNullOrEmpty(targetNodeId))
                {
                    return Response.Error("source_node_id and target_node_id are required");
                }
                
                // Get graph data
                if (!ShaderGraphReflectionHelpers.TryGetGraphData(graphPath, out object graphData, out string error))
                {
                    return Response.Error(error);
                }
                
                // Find source and target nodes
                var nodes = ShaderGraphReflectionHelpers.GetProperty(graphData, "nodes") as IEnumerable;
                object sourceNode = null;
                object targetNode = null;
                
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        if (node == null) continue;
                        
                        var nodeGuid = ShaderGraphReflectionHelpers.GetProperty(node, "guid");
                        var guidStr = nodeGuid?.ToString();
                        
                        if (guidStr == sourceNodeId)
                        {
                            sourceNode = node;
                        }
                        if (guidStr == targetNodeId)
                        {
                            targetNode = node;
                        }
                    }
                }
                
                if (sourceNode == null)
                {
                    return Response.Error($"Source node with ID '{sourceNodeId}' not found");
                }
                
                if (targetNode == null)
                {
                    return Response.Error($"Target node with ID '{targetNodeId}' not found");
                }
                
                // Create edge/connection
                var edgeType = ShaderGraphReflectionHelpers.GetEditorType("UnityEditor.ShaderGraph.MaterialEdge");
                if (edgeType == null)
                {
                    edgeType = ShaderGraphReflectionHelpers.GetEditorType("UnityEditor.Graphing.Edge");
                }
                
                if (edgeType == null)
                {
                    return Response.Error("Unable to resolve Edge type for Shader Graph");
                }
                
                // Try to create edge using constructor or factory method
                object edge = null;
                var outputSlot = ShaderGraphReflectionHelpers.GetProperty(sourceNode, sourceSlot ?? "output");
                var inputSlot = ShaderGraphReflectionHelpers.GetProperty(targetNode, targetSlot ?? "input");
                try
                {
                    // Try constructor with output and input slots
                    if (outputSlot != null && inputSlot != null)
                    {
                        var constructor = edgeType.GetConstructor(new[] { outputSlot.GetType(), inputSlot.GetType() });
                        if (constructor != null)
                        {
                            edge = constructor.Invoke(new[] { outputSlot, inputSlot });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] Failed to create edge via constructor: {ex.Message}");
                }
                
                if (edge == null)
                {
                    // Try Activator
                    try
                    {
                        edge = Activator.CreateInstance(edgeType);
                        if (outputSlot != null && inputSlot != null)
                        {
                            ShaderGraphReflectionHelpers.SetProperty(edge, "outputSlot", outputSlot);
                            ShaderGraphReflectionHelpers.SetProperty(edge, "inputSlot", inputSlot);
                        }
                    }
                    catch (Exception ex)
                    {
                        return Response.Error($"Failed to create edge: {ex.Message}");
                    }
                }
                
                // Add edge to graph
                var edges = ShaderGraphReflectionHelpers.GetProperty(graphData, "edges");
                if (edges is System.Collections.IList edgeList)
                {
                    edgeList.Add(edge);
                }
                else
                {
                    return Response.Error("Unable to add edge to graph - edges collection not accessible");
                }
                
                // Mark asset dirty and save
                EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath));
                AssetDatabase.SaveAssets();
                
                return Response.Success($"Nodes connected in shader graph", new
                {
                    graphPath = graphPath,
                    sourceNodeId = sourceNodeId,
                    targetNodeId = targetNodeId,
                    sourceSlot = sourceSlot ?? "output",
                    targetSlot = targetSlot ?? "input"
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to connect shader graph nodes: {ex.Message}");
            }
        }
    }
}
#endif

