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
    [McpForUnityTool("add_node_to_shader_graph")]
    public static class AddNodeToShaderGraphTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string graphPath = @params["graph_path"]?.ToString();
                string nodeType = @params["node_type"]?.ToString();
                float? positionX = @params["position_x"]?.ToObject<float?>();
                float? positionY = @params["position_y"]?.ToObject<float?>();
                
                if (string.IsNullOrEmpty(graphPath))
                {
                    return Response.Error("graph_path is required");
                }
                
                if (string.IsNullOrEmpty(nodeType))
                {
                    return Response.Error("node_type is required");
                }
                
                if (!positionX.HasValue || !positionY.HasValue)
                {
                    return Response.Error("position_x and position_y are required");
                }
                
                // Get graph data
                if (!ShaderGraphReflectionHelpers.TryGetGraphData(graphPath, out object graphData, out string error))
                {
                    return Response.Error(error);
                }
                
                // Try to find node type in Shader Graph library
                // This is simplified - actual implementation would need to search the node library
                var nodeTypeObj = ShaderGraphReflectionHelpers.GetEditorType($"UnityEditor.ShaderGraph.{nodeType}");
                if (nodeTypeObj == null)
                {
                    // Try alternative namespaces
                    nodeTypeObj = ShaderGraphReflectionHelpers.GetEditorType($"UnityEditor.Rendering.{nodeType}");
                }
                
                if (nodeTypeObj == null)
                {
                    return Response.Error($"Node type '{nodeType}' not found. Use a valid Shader Graph node type.");
                }
                
                // Create node instance
                object newNode = null;
                try
                {
                    newNode = Activator.CreateInstance(nodeTypeObj);
                }
                catch (Exception ex)
                {
                    return Response.Error($"Failed to create node instance: {ex.Message}");
                }
                
                if (newNode == null)
                {
                    return Response.Error("Failed to create node");
                }
                
                // Set position
                var drawState = ShaderGraphReflectionHelpers.GetProperty(newNode, "drawState");
                if (drawState != null)
                {
                    var position = Activator.CreateInstance(
                        ShaderGraphReflectionHelpers.GetEditorType("UnityEditor.Graphing.Rect") ?? typeof(Rect));
                    if (position != null)
                    {
                        var xProp = position.GetType().GetProperty("x");
                        var yProp = position.GetType().GetProperty("y");
                        if (xProp != null && yProp != null)
                        {
                            xProp.SetValue(position, positionX.Value);
                            yProp.SetValue(position, positionY.Value);
                            ShaderGraphReflectionHelpers.SetProperty(drawState, "position", position);
                        }
                    }
                }
                
                // Add node to graph
                var addNodeMethod = ShaderGraphReflectionHelpers.GetMethodInfo(
                    graphData.GetType(),
                    "AddNode",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                    new[] { newNode },
                    new[] { ShaderGraphReflectionHelpers.GetEditorType("UnityEditor.ShaderGraph.AbstractMaterialNode") });
                
                if (addNodeMethod != null)
                {
                    try
                    {
                        addNodeMethod.Invoke(graphData, new[] { newNode });
                    }
                    catch (Exception ex)
                    {
                        return Response.Error($"Failed to add node to graph: {ex.Message}");
                    }
                }
                else
                {
                    // Try alternative: get nodes collection and add
                    var nodes = ShaderGraphReflectionHelpers.GetProperty(graphData, "nodes");
                    if (nodes is System.Collections.IList nodeList)
                    {
                        nodeList.Add(newNode);
                    }
                    else
                    {
                        return Response.Error("Unable to add node to graph - nodes collection not accessible");
                    }
                }
                
                // Get node GUID for response
                var nodeGuid = ShaderGraphReflectionHelpers.GetProperty(newNode, "guid");
                
                // Mark asset dirty and save
                EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath));
                AssetDatabase.SaveAssets();
                
                return Response.Success($"Node {nodeType} added to graph", new
                {
                    graphPath = graphPath,
                    nodeType = nodeType,
                    nodeId = nodeGuid?.ToString() ?? "unknown",
                    position = new { x = positionX.Value, y = positionY.Value }
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to add node to shader graph: {ex.Message}");
            }
        }
    }
}
#endif

