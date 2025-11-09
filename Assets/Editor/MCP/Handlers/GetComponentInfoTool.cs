using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("get_component_info")]
    public static class GetComponentInfoTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string gameObjectName = @params["gameobject_name"]?.ToString();
                string searchMethod = @params["search_method"]?.ToString() ?? "by_name";
                bool includeHidden = @params["include_hidden"]?.ToObject<bool>() ?? false;
                
                if (string.IsNullOrEmpty(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }
                
                // Find GameObject
                GameObject targetGO = null;
                if (searchMethod == "by_id" && int.TryParse(gameObjectName, out int instanceID))
                {
                    targetGO = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                }
                else
                {
                    targetGO = GameObject.Find(gameObjectName);
                    if (targetGO == null)
                    {
                        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                        targetGO = allObjects.FirstOrDefault(go => go.name == gameObjectName);
                    }
                }
                
                if (targetGO == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' not found");
                }
                
                // Get all components
                var components = targetGO.GetComponents<Component>();
                var componentList = new List<object>();
                
                foreach (var component in components)
                {
                    if (component == null) continue;
                    
                    // Skip hidden components unless requested
                    if (!includeHidden && (component.hideFlags & HideFlags.HideInInspector) != 0)
                        continue;
                    
                    var componentInfo = GetComponentDetails(component);
                    componentList.Add(componentInfo);
                }
                
                return Response.Success($"Component info retrieved for {targetGO.name}", new
                {
                    gameObject = targetGO.name,
                    instanceID = targetGO.GetInstanceID(),
                    componentCount = componentList.Count,
                    components = componentList
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to get component info: {ex.Message}");
            }
        }
        
        private static object GetComponentDetails(Component component)
        {
            var details = new Dictionary<string, object>
            {
                ["typeName"] = component.GetType().FullName,
                ["shortTypeName"] = component.GetType().Name,
                ["instanceID"] = component.GetInstanceID()
            };
            
            // Get serialized properties
            SerializedObject so = new SerializedObject(component);
            SerializedProperty prop = so.GetIterator();
            var properties = new Dictionary<string, object>();
            
            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.propertyPath == "m_Script") continue; // Skip script reference
                    
                    var propValue = GetSerializedPropertyValue(prop);
                    properties[prop.propertyPath] = propValue;
                } while (prop.NextVisible(false));
            }
            
            details["properties"] = properties;
            return details;
        }
        
        private static object GetSerializedPropertyValue(SerializedProperty property)
        {
            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        return property.intValue;
                    case SerializedPropertyType.Boolean:
                        return property.boolValue;
                    case SerializedPropertyType.Float:
                        return property.floatValue;
                    case SerializedPropertyType.String:
                        return property.stringValue;
                    case SerializedPropertyType.Vector2:
                        return new { x = property.vector2Value.x, y = property.vector2Value.y };
                    case SerializedPropertyType.Vector3:
                        return new { x = property.vector3Value.x, y = property.vector3Value.y, z = property.vector3Value.z };
                    case SerializedPropertyType.Vector4:
                        var v4 = property.vector4Value;
                        return new { x = v4.x, y = v4.y, z = v4.z, w = v4.w };
                    case SerializedPropertyType.Quaternion:
                        var q = property.quaternionValue;
                        return new { x = q.x, y = q.y, z = q.z, w = q.w };
                    case SerializedPropertyType.Color:
                        var c = property.colorValue;
                        return new { r = c.r, g = c.g, b = c.b, a = c.a };
                    case SerializedPropertyType.Enum:
                        if (property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length)
                            return property.enumDisplayNames[property.enumValueIndex];
                        return property.enumValueIndex.ToString();
                    case SerializedPropertyType.ObjectReference:
                        if (property.objectReferenceValue != null)
                            return new { type = property.objectReferenceValue.GetType().Name, name = property.objectReferenceValue.name };
                        return null;
                    case SerializedPropertyType.Rect:
                        var rect = property.rectValue;
                        return new { x = rect.x, y = rect.y, width = rect.width, height = rect.height };
                    case SerializedPropertyType.RectInt:
                        var rectInt = property.rectIntValue;
                        return new { x = rectInt.x, y = rectInt.y, width = rectInt.width, height = rectInt.height };
                    case SerializedPropertyType.Bounds:
                        var bounds = property.boundsValue;
                        return new { 
                            center = new { x = bounds.center.x, y = bounds.center.y, z = bounds.center.z },
                            size = new { x = bounds.size.x, y = bounds.size.y, z = bounds.size.z }
                        };
                    case SerializedPropertyType.BoundsInt:
                        var boundsInt = property.boundsIntValue;
                        return new { 
                            center = new { x = boundsInt.center.x, y = boundsInt.center.y, z = boundsInt.center.z },
                            size = new { x = boundsInt.size.x, y = boundsInt.size.y, z = boundsInt.size.z }
                        };
                    case SerializedPropertyType.ArraySize:
                        return property.arraySize;
                    case SerializedPropertyType.LayerMask:
                        return property.intValue.ToString();
                    case SerializedPropertyType.AnimationCurve:
                        return $"[AnimationCurve: {property.animationCurveValue.length} keys]";
                    case SerializedPropertyType.Gradient:
                        return $"[Gradient: {property.gradientValue.colorKeys.Length} color keys]";
                    case SerializedPropertyType.Character:
                        return ((char)property.intValue).ToString();
                    default:
                        // For unknown types, try to get a string representation safely
                        try
                        {
                            if (property.hasChildren)
                            {
                                return $"[{property.propertyType} (has children)]";
                            }
                            return $"[{property.propertyType}: {property.propertyPath}]";
                        }
                        catch
                        {
                            return $"[{property.propertyType}]";
                        }
                }
            }
            catch (Exception ex)
            {
                // If anything fails, return a safe representation
                return $"[Error reading {property.propertyType}: {ex.Message}]";
            }
        }
    }
}

