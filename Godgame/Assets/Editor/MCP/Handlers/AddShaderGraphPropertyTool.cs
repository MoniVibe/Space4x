#if false
// Shader Graph tools disabled - Shader Graph package not installed
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("add_shader_graph_property")]
    public static class AddShaderGraphPropertyTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string graphPath = @params["graph_path"]?.ToString();
                string propertyName = @params["property_name"]?.ToString();
                string propertyType = @params["property_type"]?.ToString() ?? "Float";
                object defaultValue = @params["default_value"]?.ToObject<object>();
                
                if (string.IsNullOrEmpty(graphPath))
                {
                    return Response.Error("graph_path is required");
                }
                
                if (string.IsNullOrEmpty(propertyName))
                {
                    return Response.Error("property_name is required");
                }
                
                // Get graph data
                if (!ShaderGraphReflectionHelpers.TryGetGraphData(graphPath, out object graphData, out string error))
                {
                    return Response.Error(error);
                }
                
                // Get or create blackboard
                var blackboard = ShaderGraphReflectionHelpers.GetProperty(graphData, "blackboard");
                if (blackboard == null)
                {
                    // Try to create blackboard
                    var blackboardType = ShaderGraphReflectionHelpers.GetEditorType("UnityEditor.ShaderGraph.Blackboard");
                    if (blackboardType != null)
                    {
                        blackboard = Activator.CreateInstance(blackboardType);
                        ShaderGraphReflectionHelpers.SetProperty(graphData, "blackboard", blackboard);
                    }
                }
                
                if (blackboard == null)
                {
                    return Response.Error("Unable to access or create blackboard");
                }
                
                // Create property based on type
                string propertyTypeName = $"UnityEditor.ShaderGraph.{propertyType}Property";
                var propertyTypeObj = ShaderGraphReflectionHelpers.GetEditorType(propertyTypeName);
                if (propertyTypeObj == null)
                {
                    // Try alternative namespace
                    propertyTypeName = $"UnityEditor.Rendering.{propertyType}Property";
                    propertyTypeObj = ShaderGraphReflectionHelpers.GetEditorType(propertyTypeName);
                }
                
                if (propertyTypeObj == null)
                {
                    return Response.Error($"Property type '{propertyType}' not found. Use: Float, Vector2, Vector3, Vector4, Color, Texture2D, etc.");
                }
                
                // Create property instance
                object newProperty = null;
                try
                {
                    newProperty = Activator.CreateInstance(propertyTypeObj);
                }
                catch (Exception ex)
                {
                    return Response.Error($"Failed to create property instance: {ex.Message}");
                }
                
                // Set property name
                ShaderGraphReflectionHelpers.SetProperty(newProperty, "displayName", propertyName);
                ShaderGraphReflectionHelpers.SetProperty(newProperty, "referenceName", propertyName.Replace(" ", ""));
                
                // Set default value if provided
                if (defaultValue != null)
                {
                    var valueProp = ShaderGraphReflectionHelpers.GetProperty(newProperty, "value");
                    if (valueProp != null)
                    {
                        ShaderGraphReflectionHelpers.SetProperty(newProperty, "value", defaultValue);
                    }
                }
                
                // Add property to blackboard
                var properties = ShaderGraphReflectionHelpers.GetProperty(blackboard, "properties");
                if (properties is System.Collections.IList propList)
                {
                    propList.Add(newProperty);
                }
                else
                {
                    // Try adding directly to graph
                    var graphProperties = ShaderGraphReflectionHelpers.GetProperty(graphData, "properties");
                    if (graphProperties is System.Collections.IList graphPropList)
                    {
                        graphPropList.Add(newProperty);
                    }
                    else
                    {
                        return Response.Error("Unable to add property - properties collection not accessible");
                    }
                }
                
                // Mark asset dirty and save
                EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath));
                AssetDatabase.SaveAssets();
                
                return Response.Success($"Property {propertyName} added to shader graph", new
                {
                    graphPath = graphPath,
                    propertyName = propertyName,
                    propertyType = propertyType
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to add shader graph property: {ex.Message}");
            }
        }
    }
}
#endif

