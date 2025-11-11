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
    [McpForUnityTool("instantiate_prefab")]
    public static class InstantiatePrefabTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string prefabPath = @params["prefab_path"]?.ToString();
                float? positionX = @params["position_x"]?.ToObject<float?>();
                float? positionY = @params["position_y"]?.ToObject<float?>();
                float? positionZ = @params["position_z"]?.ToObject<float?>();
                string parentName = @params["parent_name"]?.ToString();
                
                if (string.IsNullOrEmpty(prefabPath))
                {
                    return Response.Error("prefab_path is required");
                }
                
                // Load prefab
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    return Response.Error($"Prefab not found at path: {prefabPath}");
                }
                
                // Ensure we have an active scene
                if (!EditorSceneManager.GetActiveScene().IsValid())
                {
                    return Response.Error("No active scene. Please open or create a scene first.");
                }
                
                // Create position
                Vector3 position = new Vector3(
                    positionX ?? 0f,
                    positionY ?? 0f,
                    positionZ ?? 0f
                );
                
                // Instantiate prefab
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    return Response.Error("Failed to instantiate prefab");
                }
                
                instance.transform.position = position;
                
                // Set parent if specified
                if (!string.IsNullOrEmpty(parentName))
                {
                    var parentGO = GameObject.Find(parentName);
                    if (parentGO == null)
                    {
                        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                        parentGO = allObjects.FirstOrDefault(go => go.name == parentName && go.scene.IsValid());
                    }
                    
                    if (parentGO != null)
                    {
                        instance.transform.SetParent(parentGO.transform);
                    }
                }
                
                // Mark scene as dirty
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                
                // Register undo
                Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");
                
                // Select the new instance
                Selection.activeGameObject = instance;
                
                return Response.Success($"Prefab instantiated successfully", new
                {
                    prefabPath = prefabPath,
                    instanceName = instance.name,
                    position = new { x = position.x, y = position.y, z = position.z },
                    parent = parentName
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to instantiate prefab: {ex.Message}");
            }
        }
    }
}

