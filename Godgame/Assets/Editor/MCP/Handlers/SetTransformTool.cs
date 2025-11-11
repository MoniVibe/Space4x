using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("set_transform")]
    public static class SetTransformTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string gameObjectName = @params["gameobject_name"]?.ToString();
                string searchMethod = @params["search_method"]?.ToString() ?? "by_name";
                
                if (string.IsNullOrEmpty(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }
                
                // Find GameObject
                GameObject targetGO = FindGameObject(gameObjectName, searchMethod);
                if (targetGO == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' not found");
                }
                
                Transform transform = targetGO.transform;
                bool changed = false;
                
                // Set position
                if (@params["position"] != null)
                {
                    var pos = @params["position"];
                    float x = pos["x"]?.ToObject<float>() ?? transform.position.x;
                    float y = pos["y"]?.ToObject<float>() ?? transform.position.y;
                    float z = pos["z"]?.ToObject<float>() ?? transform.position.z;
                    transform.position = new Vector3(x, y, z);
                    changed = true;
                }
                
                // Set rotation (as Euler angles)
                if (@params["rotation"] != null)
                {
                    var rot = @params["rotation"];
                    float x = rot["x"]?.ToObject<float>() ?? transform.eulerAngles.x;
                    float y = rot["y"]?.ToObject<float>() ?? transform.eulerAngles.y;
                    float z = rot["z"]?.ToObject<float>() ?? transform.eulerAngles.z;
                    transform.eulerAngles = new Vector3(x, y, z);
                    changed = true;
                }
                
                // Set scale
                if (@params["scale"] != null)
                {
                    var scale = @params["scale"];
                    float x = scale["x"]?.ToObject<float>() ?? transform.localScale.x;
                    float y = scale["y"]?.ToObject<float>() ?? transform.localScale.y;
                    float z = scale["z"]?.ToObject<float>() ?? transform.localScale.z;
                    transform.localScale = new Vector3(x, y, z);
                    changed = true;
                }
                
                // Set parent
                if (@params["parent_name"] != null)
                {
                    string parentName = @params["parent_name"].ToString();
                    if (string.IsNullOrEmpty(parentName))
                    {
                        transform.SetParent(null);
                        changed = true;
                    }
                    else
                    {
                        GameObject parentGO = FindGameObject(parentName, searchMethod);
                        if (parentGO != null)
                        {
                            transform.SetParent(parentGO.transform);
                            changed = true;
                        }
                        else
                        {
                            return Response.Error($"Parent GameObject '{parentName}' not found");
                        }
                    }
                }
                
                if (!changed)
                {
                    return Response.Error("No transform properties specified. Provide position, rotation, scale, or parent_name.");
                }
                
                EditorUtility.SetDirty(targetGO);
                return Response.Success($"Transform updated for {targetGO.name}", new
                {
                    gameObject = targetGO.name,
                    position = new { x = transform.position.x, y = transform.position.y, z = transform.position.z },
                    rotation = new { x = transform.eulerAngles.x, y = transform.eulerAngles.y, z = transform.eulerAngles.z },
                    scale = new { x = transform.localScale.x, y = transform.localScale.y, z = transform.localScale.z },
                    parent = transform.parent != null ? transform.parent.name : null
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to set transform: {ex.Message}");
            }
        }
        
        private static GameObject FindGameObject(string name, string searchMethod)
        {
            if (searchMethod == "by_id" && int.TryParse(name, out int instanceID))
            {
                return EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            }
            
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                go = allObjects.FirstOrDefault(g => g.name == name && g.scene.IsValid());
            }
            return go;
        }
    }
}

