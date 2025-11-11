using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("configure_component_property")]
    public static class ConfigureComponentPropertyTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string gameObjectName = @params["gameobject_name"]?.ToString();
                string componentType = @params["component_type"]?.ToString();
                string propertyName = @params["property_name"]?.ToString();
                var propertyValue = @params["property_value"];
                string searchMethod = @params["search_method"]?.ToString() ?? "by_name";
                
                if (string.IsNullOrEmpty(gameObjectName) || string.IsNullOrEmpty(componentType) || 
                    string.IsNullOrEmpty(propertyName) || propertyValue == null)
                {
                    return Response.Error("gameobject_name, component_type, property_name, and property_value are required");
                }
                
                // Find GameObject (reuse logic from AddComponentToGameObjectTool)
                GameObject targetGO = FindGameObject(gameObjectName, searchMethod);
                if (targetGO == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' not found");
                }
                
                // Find component
                Type componentTypeObj = ResolveComponentType(componentType);
                if (componentTypeObj == null)
                {
                    return Response.Error($"Component type '{componentType}' could not be resolved");
                }
                
                var component = targetGO.GetComponent(componentTypeObj);
                if (component == null)
                {
                    return Response.Error($"Component {componentType} not found on {targetGO.name}");
                }
                
                // Set property using SerializedObject for proper serialization
                SerializedObject serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                
                if (property == null)
                {
                    return Response.Error($"Property '{propertyName}' not found on {componentType}");
                }
                
                // Set property value based on type
                SetSerializedPropertyValue(property, propertyValue);
                serializedObject.ApplyModifiedProperties();
                
                // Mark scene as dirty
                if (targetGO.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(targetGO.scene);
                }
                
                return Response.Success($"Property {propertyName} set on {componentType}", new
                {
                    gameObject = targetGO.name,
                    componentType = componentType,
                    propertyName = propertyName,
                    propertyValue = propertyValue.ToString()
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to configure component property: {ex.Message}");
            }
        }
        
        private static GameObject FindGameObject(string name, string method)
        {
            if (method == "by_id" && int.TryParse(name, out int instanceID))
            {
                return EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            }
            
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                go = allObjects.FirstOrDefault(g => g.name == name);
            }
            return go;
        }
        
        private static Type ResolveComponentType(string typeName)
        {
            Type type = Type.GetType(typeName);
            if (type != null) return type;
            
            string[] namespaces = new[]
            {
                "Space4X.Authoring", "Space4X.Registry", "Space4X.Runtime",
                "PureDOTS.Authoring", "PureDOTS.Runtime", "UnityEngine", "UnityEditor"
            };
            
            foreach (var ns in namespaces)
            {
                type = Type.GetType($"{ns}.{typeName}");
                if (type != null) return type;
            }
            
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
                
                foreach (var ns in namespaces)
                {
                    type = assembly.GetType($"{ns}.{typeName}");
                    if (type != null) return type;
                }
            }
            
            return null;
        }
        
        private static void SetSerializedPropertyValue(SerializedProperty property, JToken value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = value.ToObject<int>();
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = value.ToObject<float>();
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = value.ToObject<bool>();
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value.ToString();
                    break;
                case SerializedPropertyType.Vector3:
                    var vec3 = value.ToObject<Vector3>();
                    property.vector3Value = vec3;
                    break;
                case SerializedPropertyType.Vector2:
                    var vec2 = value.ToObject<Vector2>();
                    property.vector2Value = vec2;
                    break;
                case SerializedPropertyType.Color:
                    var color = value.ToObject<Color>();
                    property.colorValue = color;
                    break;
                case SerializedPropertyType.Enum:
                    if (value.Type == JTokenType.String)
                    {
                        property.enumValueIndex = System.Array.IndexOf(property.enumNames, value.ToString());
                    }
                    else
                    {
                        property.enumValueIndex = value.ToObject<int>();
                    }
                    break;
                default:
                    // Try direct assignment for complex types
                    if (value.Type == JTokenType.Object || value.Type == JTokenType.Array)
                    {
                        property.stringValue = value.ToString();
                    }
                    break;
            }
        }
    }
}

