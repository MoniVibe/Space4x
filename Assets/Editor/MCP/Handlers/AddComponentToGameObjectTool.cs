using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("add_component_to_gameobject")]
    public static class AddComponentToGameObjectTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string gameObjectName = @params["gameobject_name"]?.ToString();
                string componentType = @params["component_type"]?.ToString();
                string searchMethod = @params["search_method"]?.ToString() ?? "by_name";
                
                if (string.IsNullOrEmpty(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }
                
                if (string.IsNullOrEmpty(componentType))
                {
                    return Response.Error("component_type is required");
                }
                
                // Find GameObject
                GameObject targetGO = null;
                
                if (searchMethod == "by_id" && int.TryParse(gameObjectName, out int instanceID))
                {
                    targetGO = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                }
                else if (searchMethod == "by_name")
                {
                    targetGO = GameObject.Find(gameObjectName);
                    if (targetGO == null)
                    {
                        // Try finding in all scenes
                        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                        targetGO = allObjects.FirstOrDefault(go => go.name == gameObjectName);
                    }
                }
                
                if (targetGO == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' not found using method '{searchMethod}'");
                }
                
                // Resolve component type
                Type componentTypeObj = ResolveComponentType(componentType);
                if (componentTypeObj == null)
                {
                    return Response.Error($"Component type '{componentType}' could not be resolved. Try full namespace (e.g., 'Space4X.Authoring.Space4XCarrierAuthoring')");
                }
                
                // Check if component already exists
                if (targetGO.GetComponent(componentTypeObj) != null)
                {
                    return Response.Success($"Component {componentType} already exists on {targetGO.name}", new
                    {
                        gameObject = targetGO.name,
                        componentType = componentType,
                        instanceID = targetGO.GetInstanceID()
                    });
                }
                
                // Add component
                var addedComponent = targetGO.AddComponent(componentTypeObj);
                
                // Mark scene as dirty
                if (targetGO.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(targetGO.scene);
                }
                
                return Response.Success($"Component {componentType} added to {targetGO.name}", new
                {
                    gameObject = targetGO.name,
                    componentType = componentType,
                    componentInstanceID = addedComponent.GetInstanceID(),
                    gameObjectInstanceID = targetGO.GetInstanceID()
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to add component: {ex.Message}");
            }
        }
        
        private static Type ResolveComponentType(string typeName)
        {
            // Try direct type name first
            Type type = Type.GetType(typeName);
            if (type != null)
                return type;
            
            // Try with common namespaces
            string[] namespaces = new[]
            {
                "Space4X.Authoring",
                "Space4X.Registry",
                "Space4X.Runtime",
                "PureDOTS.Authoring",
                "PureDOTS.Runtime",
                "UnityEngine",
                "UnityEditor"
            };
            
            foreach (var ns in namespaces)
            {
                string fullName = $"{ns}.{typeName}";
                type = Type.GetType(fullName);
                if (type != null)
                    return type;
            }
            
            // Search all loaded assemblies
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;
                
                // Try with namespaces
                foreach (var ns in namespaces)
                {
                    string fullName = $"{ns}.{typeName}";
                    type = assembly.GetType(fullName);
                    if (type != null)
                        return type;
                }
            }
            
            return null;
        }
    }
}

