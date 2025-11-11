using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.IO;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("create_scriptable_object_instance")]
    public static class CreateScriptableObjectInstanceTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string scriptType = @params["script_type"]?.ToString();
                string assetPath = @params["asset_path"]?.ToString();
                JObject propertyValues = @params["property_values"]?.ToObject<JObject>();
                
                if (string.IsNullOrEmpty(scriptType))
                {
                    return Response.Error("script_type is required");
                }
                
                if (string.IsNullOrEmpty(assetPath))
                {
                    return Response.Error("asset_path is required");
                }
                
                // Ensure path has .asset extension
                if (!assetPath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath += ".asset";
                }
                
                // Find the ScriptableObject type
                Type soType = Type.GetType(scriptType);
                
                // Try to find in all loaded assemblies if direct type lookup fails
                if (soType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        soType = assembly.GetType(scriptType, false);
                        if (soType != null) break;
                    }
                }
                
                if (soType == null || !typeof(ScriptableObject).IsAssignableFrom(soType))
                {
                    return Response.Error($"ScriptableObject type '{scriptType}' not found or is not a ScriptableObject");
                }
                
                // Create instance
                ScriptableObject instance = ScriptableObject.CreateInstance(soType);
                if (instance == null)
                {
                    return Response.Error($"Failed to create instance of {scriptType}");
                }
                
                // Apply property values if provided
                if (propertyValues != null)
                {
                    var serializedObject = new SerializedObject(instance);
                    foreach (var prop in propertyValues.Properties())
                    {
                        var serializedProp = serializedObject.FindProperty(prop.Name);
                        if (serializedProp != null)
                        {
                            SetSerializedPropertyValue(serializedProp, prop.Value);
                        }
                    }
                    serializedObject.ApplyModifiedProperties();
                }
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(assetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Create asset
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return Response.Success($"ScriptableObject instance created successfully", new
                {
                    scriptType = scriptType,
                    assetPath = assetPath,
                    assetName = instance.name
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create ScriptableObject instance: {ex.Message}");
            }
        }
        
        private static void SetSerializedPropertyValue(SerializedProperty prop, JToken value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = value.ToObject<int>();
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = value.ToObject<float>();
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.ToObject<bool>();
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value.ToString();
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = value.ToObject<int>();
                    break;
                default:
                    // For complex types, try to set via reflection
                    break;
            }
        }
    }
}

